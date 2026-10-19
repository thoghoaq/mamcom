using MamCom.Core.Models;
using MamCom.Core.Portions;
using MamCom.Core.Shopping;

namespace MamCom.Core.Tests.Shopping;

/// <summary>
/// The dishes and ingredients behind the SPEC §6.2 shopping text example. All grams, waste ratios and unit sizes are
/// test-only numbers chosen so that the SPEC family (3.7875 servings) buys exactly what the example shows.
/// </summary>
internal static class ShoppingFixtures
{
    public static readonly PortionConfig PortionSettings = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    public static MarketUnit Unit(string display, double gramsPerUnit, double step = 1, bool approx = false) =>
        new(display, gramsPerUnit, step, approx);

    public static Ingredient Item(
        string id,
        string name,
        Aisle aisle,
        IReadOnlyDictionary<string, MarketUnit> units,
        double waste = 0,
        bool pantry = false,
        string? southName = null) => new(
        Id: id,
        Names: southName is null
            ? new Dictionary<string, string> { [RegionKeys.Default] = name }
            : new Dictionary<string, string> { [RegionKeys.Default] = name, ["south"] = southName },
        Aliases: [],
        Category: "test",
        Aisle: aisle,
        ProteinSource: null,
        MarketUnit: units,
        WasteRatio: waste,
        IsPantry: pantry,
        NutritionPer100gEdible: TestData.Unknown,
        NutritionSource: new NutritionSource(NutritionDbs.Placeholder, null, null, null, null),
        Image: new ImageRef(null, null, null, null, false));

    public static Dictionary<string, MarketUnit> Everywhere(MarketUnit unit) => new() { [RegionKeys.Default] = unit };

    public static Ingredient PorkBelly => Item(
        "thit-ba-chi", "Thịt ba chỉ", Aisle.MeatAndFish,
        new Dictionary<string, MarketUnit> { ["north"] = Unit("lạng", 100, 0.5), ["south"] = Unit("g", 1, 50) },
        southName: "Thịt ba rọi");

    public static Ingredient SnakeheadFish => Item("ca-loc", "Cá lóc", Aisle.MeatAndFish, Everywhere(Unit("con", 500, 1, approx: true)), waste: 0.3);

    public static Ingredient WaterSpinach => Item("rau-muong", "Rau muống", Aisle.Produce, Everywhere(Unit("mớ", 150)), waste: 0.2);

    public static Ingredient Tomato => Item("ca-chua", "Cà chua", Aisle.Produce, Everywhere(Unit("quả", 100)));

    public static Ingredient TaroStem => Item("bac-ha", "Bạc hà", Aisle.Produce, Everywhere(Unit("mớ", 50)));

    public static Ingredient Egg => Item("trung", "Trứng", Aisle.EggsAndTofu, Everywhere(Unit("quả", 55)));

    public static Ingredient Rice => Item("gao", "Gạo", Aisle.DryGoodsAndSpices, Everywhere(Unit("kg", 1000, 0.5)), pantry: true);

    public static Dictionary<string, Ingredient> Catalog() =>
        TestData.Catalog(PorkBelly, SnakeheadFish, WaterSpinach, Tomato, TaroStem, Egg, Rice);

    private static DishIngredient Line(string id, double gramsPerServing, ScaleKind scale = ScaleKind.Linear) =>
        new(id, gramsPerServing, scale, 1, null);

    public static Dish BraisedPork => TestData.Dish("thit-kho-trung", Line("thit-ba-chi", 70), Line("trung", 50, ScaleKind.Integer))
        with { Slot = DishSlot.Main };

    public static Dish StirFriedGreens => TestData.Dish("rau-muong-xao", Line("rau-muong", 25))
        with { Slot = DishSlot.Vegetable };

    public static Dish SourSoup => TestData.Dish("canh-chua-ca-loc", Line("ca-loc", 60), Line("ca-chua", 40), Line("bac-ha", 8))
        with { Slot = DishSlot.Soup };

    /// <summary>Main, vegetable, soup: the order that makes the aisle lines read like the SPEC example.</summary>
    public static IReadOnlyList<Dish> Tray => [BraisedPork, StirFriedGreens, SourSoup];

    /// <summary>The SPEC §4.2 family of four.</summary>
    public static Household Family(Region region) => new(region, BowlSize.Medium,
    [
        TestData.Person("Bố", AgeGroup.Adult, 3),
        TestData.Person("Mẹ", AgeGroup.Adult, 2),
        TestData.Person("Con", AgeGroup.Child, 1.5),
        TestData.Person("Bà", AgeGroup.Senior, 1.5),
    ]);

    public static ShoppingListBuilder Builder() => new(PortionSettings);

    public static IReadOnlyList<ShoppingItem> Build(
        Region region,
        IReadOnlyList<Dish>? dishes = null,
        IReadOnlyDictionary<string, Ingredient>? catalog = null,
        Household? household = null)
    {
        household ??= Family(region);
        var portions = new PortionCalculator(PortionSettings).Calculate(household);
        return Builder().Build(household, portions, dishes ?? Tray, catalog ?? Catalog());
    }

    /// <summary>A dish that needs exactly <paramref name="grams"/> of one ingredient, whatever the servings.</summary>
    public static Dish ExactlyDish(string ingredientId, double grams) =>
        TestData.Dish("chinh-xac", Line(ingredientId, grams, ScaleKind.Fixed));
}
