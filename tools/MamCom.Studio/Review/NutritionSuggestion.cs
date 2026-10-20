using System.Text.Json;
using MamCom.Core.Models;

namespace MamCom.Studio.Review;

/// <summary>One food FDC offered for an ingredient. <see cref="Per100g"/> is what FDC states, unchanged; null means FDC does not state it.</summary>
/// <param name="Missing">Names of the nutrients FDC does not state (<c>kcal</c>, <c>proteinG</c>…).</param>
/// <param name="KcalSource">The FDC nutrient the energy was taken from; null when there is none.</param>
public sealed record SuggestionCandidate(
    int Rank,
    long FdcId,
    string Description,
    string DataType,
    string? FoodCategory,
    string DetailsUrl,
    NutritionFacts Per100g,
    string? KcalSource,
    IReadOnlyList<string> Missing,
    string Reason)
{
    /// <summary>True when FDC states none of the six nutrients, so choosing it would call an empty entry "verified".</summary>
    public bool HasNoNumbers => Per100g is { Kcal: null, ProteinG: null, FatG: null, CarbG: null, FiberG: null, SodiumMg: null };
}

/// <summary>
/// What FdcImporter wrote for an ingredient (<c>data/_suggestions/nutrition/&lt;id&gt;.json</c>, ADR-012): the candidates a person
/// may choose from. The numbers only reach an ingredient file when a person chooses one in Studio.
/// </summary>
public sealed record NutritionSuggestion(
    string IngredientId,
    string Query,
    DateOnly RetrievedAt,
    string Source,
    int TotalHits,
    IReadOnlyList<SuggestionCandidate> Candidates)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The suggestion in <paramref name="text"/>; null when it is not one (a suggestion is a help, so a damaged one is not an error).</summary>
    public static NutritionSuggestion? TryParse(string text)
    {
        try
        {
            var suggestion = JsonSerializer.Deserialize<NutritionSuggestion>(text, Options);
            return suggestion is { IngredientId.Length: > 0, Candidates: not null }
                && suggestion.Candidates.All(c => c is { Description: not null, Per100g: not null, Missing: not null })
                ? suggestion
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
