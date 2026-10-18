namespace MamCom.Core.Models;

/// <summary>An ingredient as stored in <c>data/ingredients/&lt;id&gt;.json</c> (SPEC §7.1).</summary>
/// <param name="Names">Display names keyed by region key (see <see cref="RegionExtensions.ToKey"/>) plus <see cref="NameKeys.Default"/>.</param>
/// <param name="Category">Data-defined category id such as <c>thit-heo</c>; members can avoid a whole category.</param>
/// <param name="ProteinSource">Protein source id such as <c>heo</c>, or null when the ingredient is not a protein source.</param>
/// <param name="MarketUnit">Market unit keyed by region key.</param>
/// <param name="WasteRatio">Fraction thrown away when preparing, 0 to below 1.</param>
public sealed record Ingredient(
    string Id,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyList<string> Aliases,
    string Category,
    Aisle Aisle,
    string? ProteinSource,
    IReadOnlyDictionary<string, MarketUnit> MarketUnit,
    double WasteRatio,
    bool IsPantry,
    NutritionFacts NutritionPer100gEdible,
    NutritionSource NutritionSource,
    ImageRef Image)
{
    /// <summary>The regional name, else the default name, else the id.</summary>
    public string NameFor(Region region) =>
        Names.TryGetValue(region.ToKey(), out var regional) ? regional
        : Names.TryGetValue(NameKeys.Default, out var fallback) ? fallback
        : Id;
}
