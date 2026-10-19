using System.Text.Json;

namespace MamCom.Studio.Review;

public sealed record DraftNoteProblem(string Severity, string Code, string Location, string Message);

public sealed record DraftNoteMissingIngredient(string Name, double GramsEdiblePerServing, string? Note);

/// <summary>
/// What RecipeDrafter wrote about a draft (<c>data/dishes/_drafts/&lt;id&gt;.report.json</c>), as far as the reviewer needs it:
/// how the draft did against the checks, and which ingredients the recipe wanted that the kitchen does not have.
/// </summary>
/// <param name="Validation"><c>valid</c>, <c>warnings</c>, <c>invalid</c> or <c>failed</c>.</param>
public sealed record DraftNote(
    string Validation,
    string Model,
    DateOnly? DraftedAt,
    IReadOnlyList<DraftNoteProblem> Problems,
    IReadOnlyList<DraftNoteMissingIngredient> MissingIngredients)
{
    public const string Failed = "failed";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The note in <paramref name="text"/>; null when it is not one (a note is a help, so a damaged one is not an error).</summary>
    public static DraftNote? TryParse(string text)
    {
        try
        {
            var note = JsonSerializer.Deserialize<DraftNote>(text, Options);
            return note is { Validation.Length: > 0 }
                ? note with { Problems = note.Problems ?? [], MissingIngredients = note.MissingIngredients ?? [] }
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
