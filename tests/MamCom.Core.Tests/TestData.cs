using MamCom.Core.Models;

namespace MamCom.Core.Tests;

/// <summary>Builders for model objects in tests. Every number passed in by a test is test-only, not real data.</summary>
internal static class TestData
{
    /// <summary>The value of a nutrient the test expects to be known.</summary>
    public static double Known(this double? value)
    {
        value.ShouldNotBeNull();
        return value.Value;
    }

    public static readonly NutritionFacts Unknown = new(null, null, null, null, null, null);

    public static NutritionFacts Facts(double kcal, double proteinG, double fatG, double carbG, double fiberG, double sodiumMg) =>
        new(kcal, proteinG, fatG, carbG, fiberG, sodiumMg);

    public static Ingredient Ingredient(string id, NutritionFacts? per100g = null) => new(
        Id: id,
        Names: new Dictionary<string, string> { [RegionKeys.Default] = id },
        Aliases: [],
        Category: "test",
        Aisle: Aisle.Other,
        ProteinSource: null,
        MarketUnit: new Dictionary<string, MarketUnit>(),
        WasteRatio: 0,
        IsPantry: false,
        NutritionPer100gEdible: per100g ?? Unknown,
        NutritionSource: new NutritionSource(NutritionDbs.Placeholder, null, null, null, null),
        Image: new ImageRef(null, null, null, null, false));

    public static DishIngredient Line(string id, double gramsPerServing, double consumedRatio = 1) =>
        new(id, gramsPerServing, ScaleKind.Linear, consumedRatio, null);

    public static Dish Dish(string id, params DishIngredient[] lines) => new(
        Id: id,
        Name: id,
        Slot: DishSlot.Main,
        Regions: [Region.South],
        Seasons: [SeasonIds.All],
        Spicy: SpicyLevel.None,
        CookingMethod: CookingMethod.Simmer,
        ProteinSource: ProteinSources.None,
        ServeHot: false,
        Ingredients: lines,
        Steps: [],
        Status: DishStatus.Draft,
        Provenance: new Provenance(DraftAuthor.Human, null, null, null, null),
        Image: new ImageRef(null, null, null, null, false));

    public static Member Person(string name, AgeGroup age, double bowls, params MemberTag[] tags) =>
        new(name, age, bowls, tags, []);

    public static Household Home(BowlSize size, params Member[] members) => new(Region.South, size, members);

    public static Dictionary<string, Ingredient> Catalog(params Ingredient[] items) => items.ToDictionary(i => i.Id);
}
