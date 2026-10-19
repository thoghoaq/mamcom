using System.Globalization;
using System.Reflection;
using MamCom.Core;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;
using MamCom.RecipeDrafter.Drafting;
using MamCom.RecipeDrafter.Model;
using MamCom.RecipeDrafter.Plan;
using Microsoft.Extensions.Configuration;

namespace MamCom.RecipeDrafter.Cli;

/// <summary>Everything the command line reaches outside the process for, so tests can stand in for it.</summary>
/// <param name="Model">A ready model; null means build the Claude model from the configuration.</param>
/// <param name="Handler">Only with a model built here: the HTTP stack to use instead of the real one.</param>
/// <param name="GetEnvironmentVariable">How an environment variable is read.</param>
public sealed record DrafterEnvironment(
    IConfiguration Configuration,
    IRecipeModel? Model,
    HttpMessageHandler? Handler,
    IClock Clock,
    Func<string, string?> GetEnvironmentVariable)
{
    public static DrafterEnvironment Create() => new(
        new ConfigurationBuilder().AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true).Build(),
        Model: null,
        Handler: null,
        new SystemClock(),
        Environment.GetEnvironmentVariable);
}

/// <summary><c>draft</c> (CLAUDE.md, "Lệnh thường dùng"). Exit code 0: done; 1: a dish could not be drafted or the run stopped early; 2: the command line is wrong.</summary>
public static class CommandLine
{
    public const int Success = 0;
    public const int Incomplete = 1;
    public const int UsageError = 2;

    public const int DefaultLimit = 5;
    public const string ApiKeySetting = "Claude:ApiKey";
    public const string ModelSetting = "Claude:Model";
    public const string EffortSetting = "Claude:Effort";
    public const string ApiKeyVariable = "ANTHROPIC_API_KEY";

    private sealed record Options(string Data, string? List, IReadOnlyList<string>? Only, int? Limit, bool Redo, string? Prompts);

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, DrafterEnvironment environment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(environment);

        if (args.Length == 0)
        {
            error.WriteLine(Messages.Usage);
            return UsageError;
        }

        if (args[0] is "help" or "--help" or "-h")
        {
            output.WriteLine(Messages.Usage);
            return Success;
        }

        if (args[0] != "draft")
        {
            error.WriteLine($"Lệnh '{args[0]}' không có.");
            error.WriteLine(Messages.Usage);
            return UsageError;
        }

        if (!TryParse(args.Skip(1).ToArray(), out var options, out var problem))
        {
            error.WriteLine(problem);
            error.WriteLine(Messages.Usage);
            return UsageError;
        }

        return await DraftAsync(options, output, error, environment, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> DraftAsync(Options options, TextWriter output, TextWriter error, DrafterEnvironment environment, CancellationToken cancellationToken)
    {
        var data = DataLoader.Load(options.Data);
        if (data.Config is null || data.Validation is null || data.Ingredients.Count == 0)
        {
            error.WriteLine(Messages.DataNotUsable());
            foreach (var diagnostic in data.Diagnostics)
            {
                error.WriteLine($"  {diagnostic.File} [{diagnostic.Code}] {diagnostic.Message}");
            }

            return Incomplete;
        }

        var listPath = options.List ?? Path.Combine(options.Data, DishList.RelativePath);
        if (!File.Exists(listPath))
        {
            error.WriteLine(Messages.DishListMissing(listPath));
            return Incomplete;
        }

        IReadOnlyList<DishRequest> dishes;
        try
        {
            dishes = DishList.Parse(File.ReadAllText(listPath));
        }
        catch (FormatException ex)
        {
            error.WriteLine(Messages.DishListInvalid(ex.Message));
            return Incomplete;
        }

        foreach (var id in (options.Only ?? []).Where(id => dishes.All(d => d.Id != id)))
        {
            error.WriteLine(Messages.UnknownDish(id));
            return Incomplete;
        }

        var promptsFolder = options.Prompts ?? Path.Combine(AppContext.BaseDirectory, "prompts");
        if (!TryLoadPrompts(promptsFolder, error, out var prompts))
        {
            return Incomplete;
        }

        var effort = environment.Configuration[EffortSetting];
        effort = string.IsNullOrWhiteSpace(effort) ? AnthropicRecipeModel.DefaultEffort : effort.Trim();

        HttpClient? http = null;
        try
        {
            var model = environment.Model;
            if (model is null)
            {
                var configuredModel = environment.Configuration[ModelSetting];
                var apiKey = environment.Configuration[ApiKeySetting];
                apiKey = string.IsNullOrWhiteSpace(apiKey) ? environment.GetEnvironmentVariable(ApiKeyVariable) : apiKey;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    error.WriteLine(Messages.NoApiKey());
                    return Incomplete;
                }

                if (!AnthropicRecipeModel.EffortNames.Contains(effort))
                {
                    error.WriteLine(Messages.BadEffort(effort, AnthropicRecipeModel.EffortNames));
                    return Incomplete;
                }

                http = environment.Handler is null ? null : new HttpClient(environment.Handler, disposeHandler: false);
                model = new AnthropicRecipeModel(
                    string.IsNullOrWhiteSpace(configuredModel) ? AnthropicRecipeModel.DefaultModel : configuredModel.Trim(),
                    effort,
                    apiKey.Trim(),
                    httpClient: http,
                    baseUrl: http is null ? null : "https://api.anthropic.com");
            }

            return await RunDraftsAsync(options, model, effort, data, dishes, prompts, output, error, environment, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            http?.Dispose();
        }
    }

    private static async Task<int> RunDraftsAsync(
        Options options,
        IRecipeModel model,
        string effort,
        LoadedData data,
        IReadOnlyList<DishRequest> dishes,
        PromptSet prompts,
        TextWriter output,
        TextWriter error,
        DrafterEnvironment environment,
        CancellationToken cancellationToken)
    {
        var schemas = SchemaSet.Load(Path.Combine(options.Data, "schemas"));
        var limit = options.Limit;
        var pending = dishes.Count(d =>
            (options.Only is null || options.Only.Contains(d.Id))
            && !File.Exists(DraftFiles.FinalPath(options.Data, d.Id))
            && (options.Redo || !File.Exists(DraftFiles.DraftPath(options.Data, d.Id))));
        output.WriteLine(Messages.Plan(Math.Min(limit ?? pending, pending), model.Name, effort));

        var drafter = new Drafter(model, environment.Clock);
        DraftRunResult result;
        try
        {
            result = await drafter.RunAsync(
                options.Data,
                data,
                schemas,
                dishes,
                prompts,
                new DrafterOptions(options.Only, limit, options.Redo),
                outcome => (outcome.Kind == DraftOutcomeKind.Failed ? error : output).WriteLine(outcome.Message),
                cancellationToken).ConfigureAwait(false);
        }
        catch (FormatException ex)
        {
            // The only place a format error can come from here is a prompt file with a place that has no value, or the reverse.
            error.WriteLine(Messages.PromptInvalid(ex.Message));
            return Incomplete;
        }

        if (result.StoppedBecause is not null)
        {
            error.WriteLine(result.StoppedBecause);
        }
        else if (result.LimitReached && limit is { } reached)
        {
            output.WriteLine(Messages.LimitReached(reached));
        }

        int Count(DraftOutcomeKind kind) => result.Outcomes.Count(o => o.Kind == kind);
        int Status(string status) => result.Outcomes.Count(o => o.Kind == DraftOutcomeKind.Drafted && o.Validation == status);

        output.WriteLine(Messages.Summary(
            Count(DraftOutcomeKind.Drafted),
            Status(DraftReport.Valid),
            Status(DraftReport.Warnings),
            Status(DraftReport.Invalid),
            Count(DraftOutcomeKind.Failed),
            Count(DraftOutcomeKind.SkippedFinal) + Count(DraftOutcomeKind.SkippedDraft),
            result.ModelCalls,
            result.Usage));

        return result.StoppedBecause is null && Count(DraftOutcomeKind.Failed) == 0 ? Success : Incomplete;
    }

    private static bool TryLoadPrompts(string folder, TextWriter error, out PromptSet prompts)
    {
        prompts = new PromptSet(string.Empty, string.Empty);
        foreach (var file in new[] { PromptSet.SystemFileName, PromptSet.DishFileName })
        {
            if (!File.Exists(Path.Combine(folder, file)))
            {
                error.WriteLine(Messages.PromptsMissing(folder, file));
                return false;
            }
        }

        prompts = new PromptSet(
            File.ReadAllText(Path.Combine(folder, PromptSet.SystemFileName)),
            File.ReadAllText(Path.Combine(folder, PromptSet.DishFileName)));
        return true;
    }

    private static bool TryParse(string[] args, out Options options, out string problem)
    {
        string data = "data";
        string? list = null, prompts = null;
        IReadOnlyList<string>? only = null;
        int? limit = DefaultLimit;
        var redo = false;
        options = new Options(data, list, only, limit, redo, prompts);
        problem = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            switch (name)
            {
                case "--redo":
                    redo = true;
                    continue;
                case "--all":
                    limit = null;
                    continue;
                case "--data" or "--list" or "--only" or "--limit" or "--prompts":
                    break;
                default:
                    problem = $"Tuỳ chọn '{name}' không có.";
                    return false;
            }

            if (i + 1 >= args.Length)
            {
                problem = $"Tuỳ chọn '{name}' cần một giá trị.";
                return false;
            }

            var value = args[++i];
            switch (name)
            {
                case "--data":
                    data = value;
                    break;
                case "--list":
                    list = value;
                    break;
                case "--prompts":
                    prompts = value;
                    break;
                case "--only":
                    only = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (only.Count == 0)
                    {
                        problem = "Tuỳ chọn '--only' cần ít nhất một id.";
                        return false;
                    }

                    break;
                default:
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
                    {
                        problem = $"Tuỳ chọn '--limit' cần một số nguyên từ 1, không phải '{value}'.";
                        return false;
                    }

                    limit = number;
                    break;
            }
        }

        options = new Options(data, list, only, limit, redo, prompts);
        return true;
    }
}
