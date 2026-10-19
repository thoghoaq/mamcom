using System.Text.RegularExpressions;
using MamCom.FdcImporter.Fdc;
using MamCom.FdcImporter.Suggestions;

namespace MamCom.FdcImporter.Ranking;

/// <summary>What a food's description says about how it was prepared.</summary>
internal enum DescriptionState
{
    Raw,
    Unspecified,
    Processed,
}

/// <summary>
/// Chooses the few FDC foods worth showing a person. The order, strongest first (ADR-012):
/// 1. State: a description that says raw, then one that says nothing, then one that says cooked, canned, dried…
///    (SPEC §6.3 adds nutrients from raw ingredients, so a cooked entry would count them twice).
/// 2. Completeness: fewer of the six nutrients missing; an unknown nutrient makes every dish total unknown.
/// 3. More of the query's words in the description: asking for "white" rice means brown rice comes later.
/// 4. Data type: Foundation (recent analytical data) before SR Legacy.
/// 5. FDC's own relevance score, then the lower <c>fdcId</c>, so the result never depends on arrival order.
/// </summary>
public static partial class CandidateRanker
{
    public const int NutrientCount = 6;

    private static readonly string[] ProcessedWords =
    [
        "cooked", "boiled", "fried", "roasted", "baked", "canned", "dried", "dehydrated", "frozen", "stewed", "steamed",
        "grilled", "broiled", "pickled", "smoked", "cured", "prepared", "microwaved", "braised", "sauteed",
    ];

    private static readonly HashSet<string> IgnoredQueryWords = new(StringComparer.Ordinal) { "and", "with", "without", "the", "of", "in", "or", "a" };

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex Word();

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="top"/> is below 1.</exception>
    public static IReadOnlyList<NutritionCandidate> Rank(string query, IEnumerable<FdcFood> foods, int top)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(foods);
        ArgumentOutOfRangeException.ThrowIfLessThan(top, 1);

        var queryWords = Words(query).Where(w => !IgnoredQueryWords.Contains(w)).Select(Stem).Distinct().ToArray();

        var scored = foods
            .Select(food => Score(food, queryWords))
            .OrderBy(s => s.State)
            .ThenBy(s => s.Extracted.Missing.Count)
            .ThenByDescending(s => s.Matched)
            .ThenBy(s => DataTypeOrder(s.Food.DataType))
            .ThenByDescending(s => s.Food.Score ?? 0)
            .ThenBy(s => s.Food.FdcId)
            .Take(top)
            .ToArray();

        return scored
            .Select((s, index) => new NutritionCandidate(
                Rank: index + 1,
                FdcId: s.Food.FdcId,
                Description: s.Food.Description,
                DataType: s.Food.DataType,
                FoodCategory: s.Food.FoodCategory,
                DetailsUrl: $"https://fdc.nal.usda.gov/food-details/{s.Food.FdcId}/nutrients",
                Per100g: s.Extracted.Per100g,
                KcalSource: s.Extracted.KcalSource,
                Missing: s.Extracted.Missing,
                Reason: Messages.Reason(s.Food.DataType, s.State, s.StateWord, s.Extracted, s.Matched, queryWords.Length)))
            .ToArray();
    }

    private sealed record Scored(FdcFood Food, ExtractedNutrition Extracted, DescriptionState State, string? StateWord, int Matched);

    private static Scored Score(FdcFood food, string[] queryWords)
    {
        var descriptionWords = Words(food.Description).ToArray();
        var stems = descriptionWords.Select(Stem).ToHashSet(StringComparer.Ordinal);

        // A word the query itself asks for (a "boiled egg") is not held against the food.
        var processed = ProcessedWords.Where(w => !queryWords.Contains(Stem(w))).FirstOrDefault(w => descriptionWords.Contains(w));
        var state = processed is not null ? DescriptionState.Processed : descriptionWords.Contains("raw") ? DescriptionState.Raw : DescriptionState.Unspecified;

        return new Scored(food, NutrientExtractor.Extract(food), state, processed, queryWords.Count(stems.Contains));
    }

    private static int DataTypeOrder(string dataType) => dataType switch
    {
        "Foundation" => 0,
        "SR Legacy" => 1,
        _ => 2,
    };

    private static IEnumerable<string> Words(string text) => Word().Matches(text.ToLowerInvariant()).Select(m => m.Value);

    /// <summary>Crude plural stripping so "tomatoes" matches "tomato"; it only has to be good enough to rank.</summary>
    private static string Stem(string word)
    {
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal))
        {
            return word[..^3] + "y";
        }

        if (word.Length > 4 && word.EndsWith("oes", StringComparison.Ordinal))
        {
            return word[..^2];
        }

        return word.Length > 3 && word.EndsWith('s') ? word[..^1] : word;
    }
}
