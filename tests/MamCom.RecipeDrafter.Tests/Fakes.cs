using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MamCom.Core;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;
using MamCom.RecipeDrafter.Cli;
using MamCom.RecipeDrafter.Drafting;
using MamCom.RecipeDrafter.Model;
using MamCom.RecipeDrafter.Plan;
using Microsoft.Extensions.Configuration;

namespace MamCom.RecipeDrafter.Tests;

/// <summary>A stand-in for the language model. Every number it answers with is test-only, not real data.</summary>
internal sealed class FakeModel(Func<ModelRequest, int, ModelReply> respond, string name = "fake-model") : IRecipeModel
{
    public List<ModelRequest> Requests { get; } = [];

    public string Name => name;

    public Task<ModelReply> DraftAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return Task.FromResult(respond(request, Requests.Count));
    }

    public static FakeModel Always(Func<ModelRequest, ModelReply> respond) => new((request, _) => respond(request));

    public static FakeModel Answering(JsonObject answer) => Always(_ => Answers.Reply(answer));

    public static FakeModel Failing(ModelFailure kind) => new((_, _) => throw new ModelException(kind, "boom"));
}

internal static class Answers
{
    public static readonly TokenUsage Usage = new(1200, 800, 1000, 0);

    /// <summary>A recipe the model could plausibly write for a braised fish main, using only seed ingredients.</summary>
    public static JsonObject Recipe(Action<JsonObject>? change = null)
    {
        var recipe = new JsonObject
        {
            ["seasons"] = new JsonArray("all"),
            ["spicy"] = "optional",
            ["cookingMethod"] = "kho",
            ["proteinSource"] = "ca",
            ["serveHot"] = false,
            ["ingredients"] = new JsonArray(
                Line("ca-loc", 120, "linear", "cắt khúc"),
                Line("nuoc-mam", 8, "sublinear", ""),
                Line("duong", 6, "sublinear", ""),
                Line("hanh-tim", 5, "sublinear", ""),
                Line("hat-tieu", 0.5, "sublinear", ""),
                Line("ot", 2, "sublinear", "tuỳ khẩu vị"),
                Line("dau-an", 3, "sublinear", "")),
            ["steps"] = new JsonArray(
                Step("Cắt cá thành khúc, ướp với nước mắm, hành tím và tiêu.", 8, 15, "ca-loc", "nuoc-mam", "hanh-tim", "hat-tieu"),
                Step("Làm nước màu từ đường và dầu ăn, cho cá vào đảo nhẹ.", 6, 0, "duong", "dau-an", "ca-loc"),
                Step("Thêm nước và ớt, kho nhỏ lửa đến khi nước sệt.", 3, 35, "ot", "ca-loc")),
            ["missingIngredients"] = new JsonArray(),
        };

        change?.Invoke(recipe);
        return recipe;
    }

    public static JsonObject Line(string id, double grams, string scale, string note) => new()
    {
        ["ingredientId"] = id,
        ["gramsEdiblePerServing"] = grams,
        ["scale"] = scale,
        ["note"] = note,
    };

    public static JsonObject Step(string text, int active, int passive, params string[] ids) => new()
    {
        ["text"] = text,
        ["activeMinutes"] = active,
        ["passiveMinutes"] = passive,
        ["ingredientIds"] = new JsonArray(ids.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray()),
    };

    public static JsonObject Missing(string name, double grams, string note = "") => new()
    {
        ["name"] = name,
        ["gramsEdiblePerServing"] = grams,
        ["note"] = note,
    };

    public static ModelReply Reply(JsonObject answer, string stopReason = "end_turn", TokenUsage? usage = null) =>
        new(answer.ToJsonString(), stopReason, null, usage ?? Usage, "fake-model");

    public static ModelReply Raw(string? text, string stopReason = "end_turn", string? category = null) =>
        new(text, stopReason, category, Usage, "fake-model");

    public static readonly DishRequest FishMain = new("ca-loc-kho-to", "Cá lóc kho tộ", DishSlot.Main, [Region.South]);
}

internal sealed class FixedClock(DateOnly today) : IClock
{
    public DateOnly Today { get; } = today;

    public DateTimeOffset Now => new(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}

/// <summary>A scratch copy of the shipped data folder with the dish list of the owner. Deleted with the object.</summary>
internal sealed class TempData : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("mamcom-drafts");

    public TempData(string? dishList = null)
    {
        CopyFolder(Path.Combine(AppContext.BaseDirectory, "data"), Root);
        if (dishList is not null)
        {
            File.WriteAllText(Path.Combine(Root, "plan", "dish-list.csv"), dishList);
        }
    }

    public string Root => _directory.FullName;

    public string DraftsFolder => Path.Combine(Root, "dishes", "_drafts");

    public LoadedData Load() => DataLoader.Load(Root);

    public SchemaSet Schemas => SchemaSet.Load(Path.Combine(Root, "schemas"));

    public string[] DraftFiles() =>
        Directory.Exists(DraftsFolder)
            ? [.. Directory.GetFiles(DraftsFolder).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)]
            : [];

    /// <summary>Every file under some data folders with its content hash, to prove a run changed nothing else.</summary>
    public Dictionary<string, string> Snapshot(params string[] folders) =>
        folders.SelectMany(folder => Directory.GetFiles(Path.Combine(Root, folder), "*", SearchOption.AllDirectories))
            .ToDictionary(path => Path.GetRelativePath(Root, path), path => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));

    public static PromptSet Prompts() => new(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", PromptSet.SystemFileName)),
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "prompts", PromptSet.DishFileName)));

    public async Task<DraftRunResult> DraftAsync(IRecipeModel model, IReadOnlyList<DishRequest> dishes, DrafterOptions? options = null, List<DishOutcome>? reported = null)
    {
        var drafter = new Drafter(model, new FixedClock(new DateOnly(2026, 10, 19)));
        return await drafter.RunAsync(
            Root, Load(), Schemas, dishes, Prompts(), options ?? new DrafterOptions(null, null, false), (reported ?? []).Add, TestContext.Current.CancellationToken);
    }

    public async Task<(int Code, string Output, string Error)> RunAsync(DrafterEnvironment? environment, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await CommandLine.RunAsync([.. args, "--data", Root], output, error, environment ?? Env(), TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    public static DrafterEnvironment Env(IRecipeModel? model = null, Dictionary<string, string?>? settings = null, HttpMessageHandler? handler = null, string? apiKeyVariable = null) => new(
        new ConfigurationBuilder().AddInMemoryCollection(settings ?? []).Build(),
        model,
        handler,
        new FixedClock(new DateOnly(2026, 10, 19)),
        name => name == CommandLine.ApiKeyVariable ? apiKeyVariable : null);

    public void Dispose() => _directory.Delete(recursive: true);

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }

        foreach (var folder in Directory.GetDirectories(from))
        {
            CopyFolder(folder, Path.Combine(to, Path.GetFileName(folder)));
        }
    }
}

/// <summary>What was sent to the Claude API, as a test sees it.</summary>
internal sealed record SentMessage(string Method, Uri Uri, string? ApiKey, JsonObject Body);

/// <summary>A stand-in for api.anthropic.com that answers /v1/messages with whatever a test says.</summary>
internal sealed class FakeAnthropicApi(Func<SentMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<SentMessage> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
        var sent = new SentMessage(
            request.Method.Method,
            request.RequestUri!,
            request.Headers.TryGetValues("x-api-key", out var key) ? key.Single() : null,
            JsonNode.Parse(body)!.AsObject());

        Requests.Add(sent);
        return respond(sent, Requests.Count);
    }

    public static FakeAnthropicApi Always(Func<SentMessage, HttpResponseMessage> respond) => new((request, _) => respond(request));

    public static FakeAnthropicApi Returning(HttpResponseMessage response) => Always(_ => response);

    /// <summary>A /v1/messages answer holding one text block, the way the API sends it.</summary>
    public static HttpResponseMessage Message(string? text, string stopReason = "end_turn", string? refusalCategory = null, int inputTokens = 1200, int outputTokens = 800)
    {
        var message = new JsonObject
        {
            ["id"] = "msg_test",
            ["type"] = "message",
            ["role"] = "assistant",
            ["model"] = "claude-opus-5-5",
            ["content"] = text is null ? new JsonArray() : new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
            ["stop_reason"] = stopReason,
            ["stop_sequence"] = null,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = inputTokens,
                ["output_tokens"] = outputTokens,
                ["cache_creation_input_tokens"] = 1000,
                ["cache_read_input_tokens"] = 0,
            },
        };

        if (refusalCategory is not null)
        {
            message["stop_details"] = new JsonObject { ["type"] = "refusal", ["category"] = refusalCategory, ["explanation"] = null };
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(message.ToJsonString(), Encoding.UTF8, "application/json") };
    }

    public static HttpResponseMessage Error(HttpStatusCode status, string type = "error", string message = "test error") =>
        new(status)
        {
            Content = new StringContent(
                new JsonObject { ["type"] = "error", ["error"] = new JsonObject { ["type"] = type, ["message"] = message } }.ToJsonString(),
                Encoding.UTF8,
                "application/json"),
        };
}
