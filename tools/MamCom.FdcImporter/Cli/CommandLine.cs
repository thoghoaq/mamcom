using System.Globalization;
using System.Reflection;
using MamCom.Core;
using MamCom.FdcImporter.Fdc;
using MamCom.FdcImporter.Names;
using MamCom.FdcImporter.Running;
using Microsoft.Extensions.Configuration;

namespace MamCom.FdcImporter.Cli;

/// <summary>Everything the command line reaches outside the process for, so tests can stand in for it.</summary>
/// <param name="Handler">The HTTP stack; null means the real one.</param>
/// <param name="Delay">How the importer waits; tests record instead of sleeping.</param>
public sealed record ImporterEnvironment(IConfiguration Configuration, HttpMessageHandler? Handler, IClock Clock, Func<TimeSpan, CancellationToken, Task> Delay)
{
    public static ImporterEnvironment Create() => new(
        new ConfigurationBuilder().AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true).Build(),
        Handler: null,
        new SystemClock(),
        Task.Delay);
}

/// <summary><c>suggest</c> (CLAUDE.md, "Lệnh thường dùng"). Exit code 0: done; 1: something failed or the run stopped early; 2: the command line is wrong.</summary>
public static class CommandLine
{
    public const int Success = 0;
    public const int Incomplete = 1;
    public const int UsageError = 2;

    public const string ApiKeySetting = "Fdc:ApiKey";
    public const string DemoKey = "DEMO_KEY";

    private sealed record Options(string Data, IReadOnlyList<string>? Ids, int Top, bool Refresh, int DelayMs, int MaxRequests, int MaxWaitSeconds);

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, ImporterEnvironment environment, CancellationToken cancellationToken = default)
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

        if (args[0] != "suggest")
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

        return await SuggestAsync(options, output, error, environment, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> SuggestAsync(Options options, TextWriter output, TextWriter error, ImporterEnvironment environment, CancellationToken cancellationToken)
    {
        var ingredientsFolder = Path.Combine(options.Data, "ingredients");
        if (!Directory.Exists(ingredientsFolder))
        {
            error.WriteLine($"Không thấy thư mục '{ingredientsFolder}'.");
            return Incomplete;
        }

        var existing = Directory.GetFiles(ingredientsFolder, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        var namesPath = Path.Combine(options.Data, EnglishNames.RelativePath);
        if (!File.Exists(namesPath))
        {
            error.WriteLine(Messages.NamesFileMissing(namesPath));
            return Incomplete;
        }

        IReadOnlyDictionary<string, string> names;
        try
        {
            names = EnglishNames.Parse(File.ReadAllText(namesPath));
        }
        catch (FormatException ex)
        {
            error.WriteLine(Messages.NamesFileInvalid(ex.Message));
            return Incomplete;
        }

        var unknownNames = names.Keys.Except(existing, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (unknownNames.Length > 0)
        {
            output.WriteLine(Messages.NamesForUnknownIngredients(unknownNames));
        }

        IReadOnlyList<string> ids = options.Ids ?? existing;
        foreach (var id in ids.Where(id => !existing.Contains(id, StringComparer.Ordinal)))
        {
            error.WriteLine(Messages.NoSuchIngredient(id));
            return Incomplete;
        }

        var apiKey = environment.Configuration[ApiKeySetting];
        var ownKey = !string.IsNullOrWhiteSpace(apiKey);
        output.WriteLine(ownKey ? Messages.UsingOwnKey() : Messages.UsingDemoKey());

        using var http = environment.Handler is null ? new HttpClient() : new HttpClient(environment.Handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MamCom.FdcImporter");

        var runner = new SuggestionRunner(new FdcClient(http, ownKey ? apiKey! : DemoKey), environment.Clock, environment.Delay);
        var runOptions = new RunOptions(
            options.Top,
            options.Refresh,
            TimeSpan.FromMilliseconds(options.DelayMs),
            options.MaxRequests,
            TimeSpan.FromSeconds(options.MaxWaitSeconds),
            MaxRetries: 2);

        var result = await runner.RunAsync(
            options.Data,
            ids,
            names,
            runOptions,
            outcome => (outcome.Kind == OutcomeKind.Failed ? error : output).WriteLine(outcome.Message),
            cancellationToken).ConfigureAwait(false);

        if (result.StoppedBecause is not null)
        {
            error.WriteLine(result.StoppedBecause);
        }

        var count = (OutcomeKind kind) => result.Outcomes.Count(o => o.Kind == kind);
        output.WriteLine(Messages.Summary(count(OutcomeKind.Written), count(OutcomeKind.SkippedExisting) + count(OutcomeKind.NoEnglishName), count(OutcomeKind.Failed), result.RequestsMade));

        return result.StoppedBecause is null && count(OutcomeKind.Failed) == 0 ? Success : Incomplete;
    }

    private static bool TryParse(string[] args, out Options options, out string problem)
    {
        string data = "data";
        IReadOnlyList<string>? ids = null;
        int top = 3, delayMs = 1000, maxRequests = 25, maxWait = 60;
        var refresh = false;
        options = new Options(data, ids, top, refresh, delayMs, maxRequests, maxWait);
        problem = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (name == "--refresh")
            {
                refresh = true;
                continue;
            }

            if (name is not ("--data" or "--ids" or "--top" or "--delay-ms" or "--max-requests" or "--max-wait-seconds"))
            {
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
                case "--ids":
                    ids = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (ids.Count == 0)
                    {
                        problem = "Tuỳ chọn '--ids' cần ít nhất một id.";
                        return false;
                    }

                    break;
                default:
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                    {
                        problem = $"Tuỳ chọn '{name}' cần một số nguyên không âm, không phải '{value}'.";
                        return false;
                    }

                    switch (name)
                    {
                        case "--top" when number is < 1 or > 10:
                            problem = "Tuỳ chọn '--top' phải từ 1 đến 10.";
                            return false;
                        case "--top":
                            top = number;
                            break;
                        case "--delay-ms":
                            delayMs = number;
                            break;
                        case "--max-requests" when number < 1:
                            problem = "Tuỳ chọn '--max-requests' phải ít nhất là 1.";
                            return false;
                        case "--max-requests":
                            maxRequests = number;
                            break;
                        default:
                            maxWait = number;
                            break;
                    }

                    break;
            }
        }

        options = new Options(data, ids, top, refresh, delayMs, maxRequests, maxWait);
        return true;
    }
}
