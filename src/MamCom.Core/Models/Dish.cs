namespace MamCom.Core.Models;

/// <summary>A dish as stored in <c>data/dishes/&lt;id&gt;.json</c> (SPEC §7.2). Ingredient amounts are per serving.</summary>
/// <param name="Regions">Regions the dish is typical of; used for the soft region weight.</param>
/// <param name="Seasons">Data-defined season ids, or <see cref="SeasonIds.All"/>.</param>
/// <param name="ProteinSource">Protein source id, <see cref="ProteinSources.None"/> or <see cref="ProteinSources.Vegetarian"/>.</param>
/// <param name="ServeHot">True when the dish must come off the heat close to serving time (stir-fries, greens).</param>
public sealed record Dish(
    string Id,
    string Name,
    DishSlot Slot,
    IReadOnlyList<Region> Regions,
    IReadOnlyList<string> Seasons,
    SpicyLevel Spicy,
    CookingMethod CookingMethod,
    string ProteinSource,
    bool ServeHot,
    IReadOnlyList<DishIngredient> Ingredients,
    IReadOnlyList<Step> Steps,
    DishStatus Status,
    Provenance Provenance,
    ImageRef Image);
