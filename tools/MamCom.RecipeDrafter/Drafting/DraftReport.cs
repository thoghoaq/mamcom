using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MamCom.RecipeDrafter.Model;

namespace MamCom.RecipeDrafter.Drafting;

/// <param name="Severity"><c>error</c> or <c>warning</c>.</param>
/// <param name="Code">Stable id of the rule, as in DataBuilder, or one of the <c>model-*</c> and <c>missing-ingredient</c> codes.</param>
public sealed record DraftProblem(string Severity, string Code, string Location, string Message);

/// <summary>An ingredient the model needed that the ingredient list does not have.</summary>
public sealed record MissingIngredient(string Name, double GramsEdiblePerServing, string? Note);

/// <summary>
/// The note that goes with a draft (<c>data/dishes/_drafts/&lt;id&gt;.report.json</c>): whether it passes the checks
/// that the shipped data must pass, what is wrong if not, which ingredients are missing, and what the call cost.
/// </summary>
/// <param name="Validation">
/// <c>valid</c>, <c>warnings</c> (only warnings), <c>invalid</c> (the draft file exists but breaks a rule), or
/// <c>failed</c> (the model gave nothing usable, so there is no draft file).
/// </param>
/// <param name="StopReason">Why the model stopped: <c>end_turn</c>, <c>max_tokens</c>, <c>refusal</c>…; empty when no answer arrived.</param>
public sealed record DraftReport(
    string DishId,
    string Model,
    DateOnly DraftedAt,
    string Validation,
    string StopReason,
    IReadOnlyList<DraftProblem> Problems,
    IReadOnlyList<MissingIngredient> MissingIngredients,
    TokenUsage Usage)
{
    public const string Valid = "valid";
    public const string Warnings = "warnings";
    public const string Invalid = "invalid";
    public const string Failed = "failed";
}

/// <summary>Where drafts live and how they are written. Nothing here writes outside <c>data/dishes/_drafts</c>.</summary>
public static class DraftFiles
{
    private static readonly JsonSerializerOptions ReportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string DraftPath(string dataRoot, string dishId) => Path.Combine(dataRoot, "dishes", "_drafts", dishId + ".json");

    public static string ReportPath(string dataRoot, string dishId) => Path.Combine(dataRoot, "dishes", "_drafts", dishId + ".report.json");

    /// <summary>The dish file a person has taken over from the draft; it is never overwritten.</summary>
    public static string FinalPath(string dataRoot, string dishId) => Path.Combine(dataRoot, "dishes", dishId + ".json");

    public static string SerializeReport(DraftReport report) => JsonSerializer.Serialize(report, ReportOptions) + "\n";

    /// <summary>Writes through a temporary file and a rename, so a run that is killed leaves a whole file or none.</summary>
    public static void WriteAtomically(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporary, path, overwrite: true);
    }
}
