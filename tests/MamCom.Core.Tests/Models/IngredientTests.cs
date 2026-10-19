using MamCom.Core.Models;

namespace MamCom.Core.Tests.Models;

public class IngredientTests
{
    [Fact]
    public void NameFor_prefers_the_regional_name_then_the_default_then_the_id()
    {
        var ingredient = Create(new Dictionary<string, string>
        {
            ["default"] = "Thịt ba chỉ",
            ["south"] = "Thịt ba rọi",
        });

        ingredient.NameFor(Region.South).ShouldBe("Thịt ba rọi");
        ingredient.NameFor(Region.North).ShouldBe("Thịt ba chỉ");
        ingredient.NameFor(Region.Central).ShouldBe("Thịt ba chỉ");

        Create(new Dictionary<string, string>()).NameFor(Region.North).ShouldBe("thit-ba-chi");
    }

    [Fact]
    public void MarketUnitFor_prefers_the_regional_unit_then_the_default_then_null()
    {
        var north = new MarketUnit("lạng", 100, 0.5);
        var fallback = new MarketUnit("g", 1, 50);
        var ingredient = Create(new Dictionary<string, string>()) with
        {
            MarketUnit = new Dictionary<string, MarketUnit> { ["north"] = north, ["default"] = fallback },
        };

        ingredient.MarketUnitFor(Region.North).ShouldBe(north);
        ingredient.MarketUnitFor(Region.South).ShouldBe(fallback);
        ingredient.MarketUnitFor(Region.Central).ShouldBe(fallback);

        (ingredient with { MarketUnit = new Dictionary<string, MarketUnit> { ["north"] = north } })
            .MarketUnitFor(Region.Central).ShouldBeNull();
    }

    private static Ingredient Create(IReadOnlyDictionary<string, string> names) => new(
        Id: "thit-ba-chi",
        Names: names,
        Aliases: [],
        Category: "thit-heo",
        Aisle: Aisle.MeatAndFish,
        ProteinSource: "heo",
        MarketUnit: new Dictionary<string, MarketUnit>(),
        WasteRatio: 0,
        IsPantry: false,
        NutritionPer100gEdible: new NutritionFacts(null, null, null, null, null, null),
        NutritionSource: new NutritionSource(NutritionDbs.Placeholder, null, null, null, null),
        Image: new ImageRef(null, null, null, null, false));
}
