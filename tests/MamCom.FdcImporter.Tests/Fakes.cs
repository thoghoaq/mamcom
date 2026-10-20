using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MamCom.Core;
using MamCom.FdcImporter.Cli;
using MamCom.FdcImporter.Fdc;
using Microsoft.Extensions.Configuration;

namespace MamCom.FdcImporter.Tests;

/// <summary>What the importer asked FDC for, as a test sees it.</summary>
internal sealed record SentRequest(Uri Uri, string? ApiKey, string Query);

/// <summary>A stand-in for the FDC server. Every number a test gives it is test-only, not real data.</summary>
internal sealed class FakeFdc(Func<SentRequest, int, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<SentRequest> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sent = new SentRequest(
            request.RequestUri!,
            request.Headers.TryGetValues("X-Api-Key", out var key) ? key.Single() : null,
            System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["query"] ?? string.Empty);

        Requests.Add(sent);
        return Task.FromResult(respond(sent, Requests.Count));
    }

    public static FakeFdc Always(Func<SentRequest, HttpResponseMessage> respond) => new((request, _) => respond(request));

    public static FakeFdc Returning(string json, int? limit = 100, int? remaining = 99) => Always(_ => Json(json, limit, remaining));

    public static HttpResponseMessage Json(string json, int? limit = 100, int? remaining = 99)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (limit is { } l)
        {
            response.Headers.Add("X-RateLimit-Limit", l.ToString());
        }

        if (remaining is { } r)
        {
            response.Headers.Add("X-RateLimit-Remaining", r.ToString());
        }

        return response;
    }

    public static HttpResponseMessage Status(HttpStatusCode status, TimeSpan? retryAfter = null, int? remaining = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        if (retryAfter is { } wait)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
        }

        if (remaining is { } r)
        {
            response.Headers.Add("X-RateLimit-Remaining", r.ToString());
        }

        return response;
    }
}

/// <summary>The kind of food JSON FDC sends, built from the few fields the importer reads.</summary>
internal static class Foods
{
    private static string Number(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Search(int totalHits, params string[] foods) =>
        "{ \"totalHits\": " + totalHits + ", \"foods\": [" + string.Join(", ", foods) + "] }";

    public static string Food(long id, string description, string dataType, double score, params (int Id, string Name, string Unit, double Value)[] nutrients)
    {
        var list = string.Join(", ", nutrients.Select(n =>
            "{ \"nutrientId\": " + n.Id + ", \"nutrientName\": \"" + n.Name + "\", \"unitName\": \"" + n.Unit + "\", \"value\": " + Number(n.Value) + " }"));

        return "{ \"fdcId\": " + id + ", \"description\": \"" + description + "\", \"dataType\": \"" + dataType
            + "\", \"foodCategory\": \"Test category\", \"score\": " + Number(score) + ", \"foodNutrients\": [" + list + "] }";
    }

    /// <summary>All six nutrients, energy as SR Legacy states it.</summary>
    public static (int, string, string, double)[] Complete(double kcal = 100, double protein = 10, double fat = 5, double carb = 20, double fiber = 2, double sodium = 50) =>
    [
        (1008, "Energy", "KCAL", kcal),
        (1003, "Protein", "G", protein),
        (1004, "Total lipid (fat)", "G", fat),
        (1005, "Carbohydrate, by difference", "G", carb),
        (1079, "Fiber, total dietary", "G", fiber),
        (1093, "Sodium, Na", "MG", sodium),
    ];

    public static FdcFood Parsed(long id, string description, string dataType = "SR Legacy", double score = 100, params (int Id, string Name, string Unit, double Value)[] nutrients) =>
        new(id, description, dataType, "Test category", score, [.. nutrients.Select(n => new FdcNutrient(n.Id, n.Name, n.Unit, n.Value))]);
}

internal sealed class FixedClock(DateOnly today) : IClock
{
    public DateOnly Today { get; } = today;

    public DateTimeOffset Now => new(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}

/// <summary>A scratch copy of the test data folder (live schemas, frozen seed plan and ingredients). Deleted with the object.</summary>
internal sealed class TempData : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("mamcom-fdc");

    public TempData()
    {
        CopyFolder(Path.Combine(AppContext.BaseDirectory, "data"), Root);
    }

    public string Root => _directory.FullName;

    public string SuggestionPath(string id) => Path.Combine(Root, "_suggestions", "nutrition", id + ".json");

    public string[] SuggestionFiles() =>
        Directory.Exists(Path.Combine(Root, "_suggestions"))
            ? [.. Directory.GetFiles(Path.Combine(Root, "_suggestions"), "*.json", SearchOption.AllDirectories).Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.Ordinal)]
            : [];

    /// <summary>Every file under the data folder with its content hash, to prove a run changed nothing else.</summary>
    public Dictionary<string, string> Snapshot(params string[] folders) =>
        folders.SelectMany(folder => Directory.GetFiles(Path.Combine(Root, folder), "*", SearchOption.AllDirectories))
            .ToDictionary(path => Path.GetRelativePath(Root, path), path => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));

    public async Task<(int Code, string Output, string Error, List<TimeSpan> Delays)> RunAsync(FakeFdc fdc, string? apiKey, params string[] args)
    {
        var settings = new Dictionary<string, string?>();
        if (apiKey is not null)
        {
            settings[CommandLine.ApiKeySetting] = apiKey;
        }

        var delays = new List<TimeSpan>();
        var environment = new ImporterEnvironment(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            fdc,
            new FixedClock(new DateOnly(2026, 10, 19)),
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var output = new StringWriter();
        var error = new StringWriter();
        var code = await CommandLine.RunAsync([.. args, "--data", Root], output, error, environment, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString(), delays);
    }

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
