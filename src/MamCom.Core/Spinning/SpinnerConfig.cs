using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;

namespace MamCom.Core.Spinning;

/// <param name="ExcludeWithinDays">A dish eaten this many days ago or fewer is out (hard rule).</param>
/// <param name="ReducedUntilDays">A dish eaten after the excluded days and up to this many days ago gets <paramref name="ReducedWeight"/>.</param>
public sealed record RecencyConfig(int ExcludeWithinDays, int ReducedUntilDays, double ReducedWeight);

public sealed record PreferenceWeights(double Liked, double Neutral, double Disliked);

/// <summary>The quick filters of SPEC §8.1-2: after several spins that were not confirmed the screen offers a few ways to narrow the draw.</summary>
/// <param name="SuggestAfterSpins">Spins since the last confirmed tray before the filters are offered.</param>
/// <param name="MaxMinutes">The "short" filter: the whole meal takes at most this many minutes.</param>
/// <param name="RedMeatProteinSources">Protein source ids that count as red meat; the "no red meat" filter drops those dishes.</param>
/// <param name="LowFatExcludedMethods">Cooking methods the "low fat" filter drops. Nutrition numbers are not used: they are not complete yet.</param>
public sealed record QuickFilterConfig(
    int SuggestAfterSpins,
    int MaxMinutes,
    IReadOnlyList<string> RedMeatProteinSources,
    IReadOnlyList<CookingMethod> LowFatExcludedMethods);

/// <summary>The spinner constants of SPEC §5, loaded from <c>data/config/spinner-defaults.json</c>.</summary>
/// <param name="SecondMainMinBaseServings">With at least this many base servings the tray gets a second main dish.</param>
/// <param name="AttemptsPerLevel">Re-draws before the next rule is relaxed.</param>
/// <param name="HistorySize">How many recent spins are kept for "previous tray".</param>
/// <param name="SeasonMatchWeight">Weight factor for a dish that lists the current season (the id "all" does not count as a match).</param>
/// <param name="RegionMatchWeight">Weight factor for a dish that lists the household's region.</param>
/// <param name="MaxFriedDishes">The tray rule for fried dishes: at most this many.</param>
/// <param name="QuickFilters">The filters offered after several spins that were not confirmed.</param>
/// <param name="SeasonByMonth">Season id for January to December. Dishes name seasons with these ids.</param>
public sealed record SpinnerConfig(
    double SecondMainMinBaseServings,
    int AttemptsPerLevel,
    int HistorySize,
    RecencyConfig Recency,
    PreferenceWeights Preference,
    double SeasonMatchWeight,
    double RegionMatchWeight,
    int MaxFriedDishes,
    QuickFilterConfig QuickFilters,
    IReadOnlyList<string> SeasonByMonth)
{
    /// <summary>Reads the content of <c>spinner-defaults.json</c>. Does not validate values; see <see cref="Validate"/>.</summary>
    public static SpinnerConfig Parse(string json) =>
        JsonSerializer.Deserialize(json, CoreJsonContext.Default.SpinnerConfig)
        ?? throw new JsonException("spinner-defaults.json must be an object, not null.");

    /// <exception cref="ArgumentException">A value is not finite or is outside the range the algorithm needs.</exception>
    public void Validate()
    {
        Require(double.IsFinite(SecondMainMinBaseServings) && SecondMainMinBaseServings > 0, nameof(SecondMainMinBaseServings), "must be a positive number");
        Require(AttemptsPerLevel >= 1, nameof(AttemptsPerLevel), "must be at least 1");
        Require(HistorySize >= 1, nameof(HistorySize), "must be at least 1");
        Require(Recency.ExcludeWithinDays >= 0, nameof(Recency), "excludeWithinDays must not be negative");
        Require(Recency.ReducedUntilDays >= Recency.ExcludeWithinDays, nameof(Recency), "reducedUntilDays must not be below excludeWithinDays");
        Require(IsPositive(Recency.ReducedWeight), nameof(Recency), "reducedWeight must be a positive number");
        Require(IsPositive(Preference.Liked) && IsPositive(Preference.Neutral) && IsPositive(Preference.Disliked), nameof(Preference), "every preference weight must be a positive number");
        Require(IsPositive(SeasonMatchWeight), nameof(SeasonMatchWeight), "must be a positive number");
        Require(IsPositive(RegionMatchWeight), nameof(RegionMatchWeight), "must be a positive number");
        Require(MaxFriedDishes >= 0, nameof(MaxFriedDishes), "must not be negative");
        Require(QuickFilters.SuggestAfterSpins >= 1, nameof(QuickFilters), "suggestAfterSpins must be at least 1");
        Require(QuickFilters.MaxMinutes >= 1, nameof(QuickFilters), "maxMinutes must be at least 1");
        Require(
            QuickFilters.RedMeatProteinSources.Count > 0 && QuickFilters.RedMeatProteinSources.All(s => !string.IsNullOrWhiteSpace(s)) && QuickFilters.RedMeatProteinSources.Distinct(StringComparer.Ordinal).Count() == QuickFilters.RedMeatProteinSources.Count,
            nameof(QuickFilters),
            "redMeatProteinSources needs at least one distinct, non-empty id");
        Require(
            QuickFilters.LowFatExcludedMethods.Count > 0 && QuickFilters.LowFatExcludedMethods.All(Enum.IsDefined) && QuickFilters.LowFatExcludedMethods.Distinct().Count() == QuickFilters.LowFatExcludedMethods.Count,
            nameof(QuickFilters),
            "lowFatExcludedMethods needs at least one distinct cooking method");
        Require(
            SeasonByMonth.Count == 12 && SeasonByMonth.All(s => !string.IsNullOrWhiteSpace(s)),
            nameof(SeasonByMonth),
            "needs 12 non-empty season ids, January first");
    }

    // A dish is never made impossible to draw by a weight: zero and negative weights are rejected.
    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;

    private static void Require(bool condition, string name, string reason)
    {
        if (!condition)
        {
            throw new ArgumentException($"Spinner config {name}: {reason}.", name);
        }
    }
}
