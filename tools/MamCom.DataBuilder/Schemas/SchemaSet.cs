using System.Text.Json;
using LateApexEarlySpeed.Json.Schema;
using LateApexEarlySpeed.Json.Schema.Common;

namespace MamCom.DataBuilder.Schemas;

/// <summary>One thing wrong with a data file.</summary>
/// <param name="Location">JSON Pointer to the offending value; empty for the file as a whole.</param>
/// <param name="Keyword">The schema keyword that failed (<c>required</c>, <c>enum</c>…), or <c>json</c> for text that is not JSON.</param>
public sealed record SchemaIssue(string Location, string Keyword, string Message);

/// <summary>
/// The JSON Schemas under <c>data/schemas</c>, loaded once. A schema is named after its file:
/// <c>ingredient.schema.json</c> is <c>ingredient</c>.
/// </summary>
public sealed class SchemaSet
{
    public const string FileSuffix = ".schema.json";

    private static readonly JsonDocumentOptions StrictDocument = new() { AllowDuplicateProperties = false };

    private readonly Dictionary<string, JsonValidator> _validators;

    private SchemaSet(Dictionary<string, JsonValidator> validators) => _validators = validators;

    public IReadOnlyCollection<string> Names => _validators.Keys.Order(StringComparer.Ordinal).ToArray();

    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    /// <exception cref="InvalidOperationException">A schema file is not a usable JSON Schema.</exception>
    public static SchemaSet Load(string schemaDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaDirectory);

        var validators = new Dictionary<string, JsonValidator>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(schemaDirectory, "*" + FileSuffix))
        {
            var fileName = Path.GetFileName(path);
            var name = fileName[..^FileSuffix.Length];
            try
            {
                // Dates are checked as dates, not as any string that looks like one.
                validators[name] = new JsonValidator(File.ReadAllText(path));
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
            {
                throw new InvalidOperationException($"{fileName} is not a usable JSON Schema: {ex.Message}", ex);
            }
        }

        return new SchemaSet(validators);
    }

    /// <summary>Every problem of <paramref name="json"/> against the named schema; empty when the file is valid.</summary>
    /// <exception cref="ArgumentException">There is no schema with that name.</exception>
    public IReadOnlyList<SchemaIssue> Validate(string schemaName, string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (!_validators.TryGetValue(schemaName, out var validator))
        {
            throw new ArgumentException($"There is no schema named '{schemaName}'. Known: {string.Join(", ", Names)}.", nameof(schemaName));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, StrictDocument);
        }
        catch (JsonException ex)
        {
            return [new SchemaIssue(string.Empty, "json", ex.Message)];
        }

        using (document)
        {
            var result = validator.Validate(document, new JsonSchemaOptions { OutputFormat = OutputFormat.List, ValidateFormat = true });
            if (result.IsValid)
            {
                return [];
            }

            return ToIssues(result.ValidationErrors.Select(e => new Failure(
                e.InstanceLocation?.ToString() ?? string.Empty,
                e.Keyword ?? string.Empty,
                e.RelativeKeywordLocation?.ToString() ?? string.Empty,
                e.ErrorMessage ?? string.Empty)));
        }
    }

    /// <summary>One failed schema node as the validator lists it, with the path of the keyword that failed.</summary>
    internal sealed record Failure(string Location, string Keyword, string KeywordPath, string Message);

    // Keywords whose sub-schemas may fail without the data being wrong: a failed `if` just picks the other branch,
    // a failed branch of `anyOf` or `oneOf` is fine while another passes, and `not` wants its sub-schema to fail.
    private static readonly HashSet<string> TestingKeywords = new(StringComparer.Ordinal) { "if", "not", "anyOf", "oneOf" };

    /// <summary>
    /// The validator lists every node that failed, including those that only failed inside a test that then went the
    /// right way, so a valid-looking <c>names</c> is reported as missing <c>north</c>. Keep the failures that made the
    /// file invalid. An <c>anyOf</c> or <c>oneOf</c> that failed as a whole is one issue carrying what each branch said.
    /// </summary>
    internal static IReadOnlyList<SchemaIssue> ToIssues(IEnumerable<Failure> failures)
    {
        var all = failures.Distinct().ToList();
        var real = all.Where(f => !IsInsideTest(f.KeywordPath)).ToList();

        // The result is invalid, so something must remain; if the filter ate everything, show the raw list.
        var shown = real.Count > 0 ? real : all;

        return shown
            .Select(f => new SchemaIssue(f.Location, KeywordOf(f), MessageOf(f, all)))
            .Distinct()
            .OrderBy(issue => issue.Location, StringComparer.Ordinal)
            .ThenBy(issue => issue.Keyword, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsInsideTest(string keywordPath)
    {
        var segments = keywordPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (TestingKeywords.Contains(segments[i]))
            {
                return true;
            }
        }

        return false;
    }

    // A schema of `false` (additionalProperties: false) fails without naming a keyword; its path ends in the keyword.
    private static string KeywordOf(Failure failure) =>
        failure.Keyword.Length > 0 ? failure.Keyword : failure.KeywordPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;

    private static string MessageOf(Failure failure, List<Failure> all)
    {
        if (failure.Keyword is not ("anyOf" or "oneOf"))
        {
            return failure.Message;
        }

        var branches = all
            .Where(other => other.KeywordPath.StartsWith(failure.KeywordPath + "/", StringComparison.Ordinal))
            .Select(other => other.Message)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

        return branches.Length == 0 ? failure.Message : $"{failure.Message}: {string.Join("; ", branches)}";
    }
}
