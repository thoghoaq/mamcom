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
