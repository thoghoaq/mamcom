using MamCom.DataBuilder.Validation;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class KitchenTests
{
    [Fact]
    public void The_seed_data_is_a_usable_kitchen_with_nothing_wrong_with_it()
    {
        using var data = new StudioData();

        var kitchen = data.Loader.Load();

        kitchen.IsUsable.ShouldBeTrue();
        kitchen.Problems.ShouldBeEmpty();
        kitchen.Schemas.ShouldNotBeNull();
        kitchen.Nutrition.ShouldNotBeNull();
        kitchen.Ingredients.Count.ShouldBe(Directory.GetFiles(data.PathOf("ingredients"), "*.json").Length);
    }

    [Fact]
    public void An_ingredient_is_shown_by_its_default_name_and_an_unknown_one_by_its_id()
    {
        using var data = new StudioData();
        var kitchen = data.Loader.Load();

        kitchen.IngredientName("rau-muong").ShouldBe("Rau muống");
        kitchen.IngredientName("khong-co").ShouldBe("khong-co");
    }

    [Fact]
    public void The_seasons_are_the_ones_of_the_spinner_config_then_all()
    {
        using var data = new StudioData();
        var kitchen = data.Loader.Load();

        kitchen.Seasons.ShouldBe([.. kitchen.Data.Config!.Spinner.SeasonByMonth.Distinct(), "all"]);
        kitchen.Seasons.Last().ShouldBe("all");
        kitchen.Seasons.Distinct().Count().ShouldBe(kitchen.Seasons.Count);
    }

    [Fact]
    public void The_protein_sources_start_with_none_and_vegetarian_then_the_ones_the_ingredients_have()
    {
        using var data = new StudioData();
        var kitchen = data.Loader.Load();

        kitchen.ProteinSourceIds.Take(2).ShouldBe(["none", "chay"]);
        kitchen.ProteinSourceIds.ShouldContain("ca");
        kitchen.ProteinSourceIds.ShouldContain("heo");
        kitchen.ProteinSourceIds.Distinct().Count().ShouldBe(kitchen.ProteinSourceIds.Count);
        kitchen.ProteinSourceIds.Skip(2).ShouldBe(kitchen.ProteinSourceIds.Skip(2).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Without_a_config_file_the_kitchen_is_not_usable_and_says_why()
    {
        using var data = new StudioData();
        data.Delete("config/nutrition-defaults.json");

        var kitchen = data.Loader.Load();

        kitchen.IsUsable.ShouldBeFalse();
        kitchen.Nutrition.ShouldBeNull();
        kitchen.Problems.ShouldContain(p => p.Code == DiagnosticCodes.ConfigMissing && p.File == "config/nutrition-defaults.json");
    }

    [Fact]
    public void Without_the_validation_config_the_kitchen_is_not_usable()
    {
        using var data = new StudioData();
        data.Delete("config/validation-defaults.json");

        data.Loader.Load().IsUsable.ShouldBeFalse();
    }

    [Fact]
    public void Without_the_schema_folder_the_kitchen_is_not_usable()
    {
        using var data = new StudioData();
        Directory.Delete(data.PathOf("schemas"), recursive: true);

        var kitchen = data.Loader.Load();

        kitchen.IsUsable.ShouldBeFalse();
        kitchen.Schemas.ShouldBeNull();
        kitchen.Problems.ShouldNotBeEmpty();
    }

    [Fact]
    public void A_schema_that_cannot_be_used_makes_the_kitchen_unusable_instead_of_failing()
    {
        using var data = new StudioData();
        data.WriteText("schemas/dish.schema.json", "{ not json");

        var kitchen = data.Loader.Load();

        kitchen.IsUsable.ShouldBeFalse();
        kitchen.Schemas.ShouldBeNull();
    }

    [Fact]
    public void A_broken_dish_file_is_a_problem_of_that_dish_and_not_of_the_kitchen()
    {
        using var data = new StudioData();
        data.WriteText("dishes/hong.json", "{ not json");

        var kitchen = data.Loader.Load();

        kitchen.IsUsable.ShouldBeTrue();
        kitchen.Problems.ShouldBeEmpty();
    }

    [Fact]
    public void A_broken_ingredient_file_is_a_problem_of_the_kitchen()
    {
        using var data = new StudioData();
        data.WriteText("ingredients/hong.json", "{ not json");

        var kitchen = data.Loader.Load();

        kitchen.Problems.ShouldContain(p => p.File == "ingredients/hong.json");
    }

    [Fact]
    public void Two_ingredients_with_the_same_id_do_not_stop_the_kitchen_from_loading()
    {
        using var data = new StudioData();

        var seed = data.Loader.Load().Ingredients.Count;
        data.WriteText("ingredients/rau-muong-2.json", data.ReadText("ingredients/rau-muong.json"));

        var kitchen = data.Loader.Load();

        kitchen.Ingredients.ContainsKey("rau-muong").ShouldBeTrue();
        kitchen.Ingredients.Count.ShouldBe(seed);
    }

    [Fact]
    public void A_kitchen_needs_its_data()
    {
        Should.Throw<ArgumentNullException>(() => new Kitchen(null!, null));
    }
}
