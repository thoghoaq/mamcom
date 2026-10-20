using MamCom.Core.Details;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using static MamCom.Core.Tests.TestData;

namespace MamCom.Core.Tests.Details;

public class DishDetailBuilderTests
{
    private const double Tolerance = 1e-9;

    private static readonly PortionConfig PortionConfig = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    private static readonly NutritionConfig NutritionConfig = NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json"));

    private static DishDetailBuilder Builder(NutritionConfig? nutrition = null) => new(PortionConfig, nutrition ?? NutritionConfig);

    // ---- test-only fixture -------------------------------------------------------------------
    // The per-100 g numbers are invented for the arithmetic and are not nutrition data.

    private static Ingredient Pork => Ingredient("thit", Facts(200, 20, 12, 0, 0, 60)) with
    {
        Names = new Dictionary<string, string> { [RegionKeys.Default] = "thịt", ["north"] = "thịt lợn", ["south"] = "thịt heo" },
    };

    private static Ingredient Veg => Ingredient("rau", Facts(20, 2, 0, 3, 2, 10));

    private static Ingredient Egg => Ingredient("trung", Facts(150, 13, 10, 1, 0, 120)) with
    {
        MarketUnit = new Dictionary<string, MarketUnit> { [RegionKeys.Default] = new("quả", 55, 1) },
    };

    // 400 kcal per 100 g: protein 15 g, fat 10 g, carbohydrate 62.5 g make 15%, 22.5% and 62.5% of the energy, all inside the ranges.
    private static Ingredient Balanced => Ingredient("can-bang", Facts(400, 15, 10, 62.5, 0, 0));

    private static Dictionary<string, Ingredient> Pantry => Catalog(Pork, Veg, Egg, Balanced);

    // Father: (0.4 + 0.3 × 2) × 1.0 = 1.0 serving. Child: (0.4 + 0.3 × 1) × 0.85 = 0.595 serving. Base 1.595.
    private static Household Family(params MemberTag[] fatherTags) => Home(
        BowlSize.Medium,
        Person("Bố", AgeGroup.Adult, 2, fatherTags),
        Person("Con", AgeGroup.Child, 1)) with
    { Region = Region.North };

    private static Dish Recipe(params DishIngredient[] lines) => Dish("mon", lines);

    private static DishDetail Detail(Dish dish, Household? household = null, NutritionConfig? nutrition = null, IReadOnlyDictionary<string, Ingredient>? ingredients = null) =>
        Builder(nutrition).Build(dish, household ?? Family(), ingredients ?? Pantry);

    private static Step Step(int order, int active, int passive) => new(order, $"bước {order}", active, passive, []);

    // ---- servings ----------------------------------------------------------------------------

    [Fact]
    public void A_soup_is_scaled_to_the_base_servings_of_the_household()
    {
        var detail = Detail(Recipe(Line("rau", 100)) with { Slot = DishSlot.Soup }, Family(MemberTag.MoreProtein, MemberTag.MoreVeg));

        detail.Servings.ShouldBe(1.595, Tolerance);
    }

    [Fact]
    public void A_main_dish_uses_the_protein_servings()
    {
        var detail = Detail(Recipe(Line("thit", 100)) with { Slot = DishSlot.Main }, Family(MemberTag.MoreProtein, MemberTag.MoreVeg));

        detail.Servings.ShouldBe(1.0 * 1.2 + 0.595, Tolerance); // the father asks for more protein
    }

    [Fact]
    public void A_vegetable_dish_uses_the_vegetable_servings()
    {
        var detail = Detail(Recipe(Line("rau", 100)) with { Slot = DishSlot.Vegetable }, Family(MemberTag.MoreProtein, MemberTag.MoreVeg));

        detail.Servings.ShouldBe(1.0 * 1.2 + 0.595, Tolerance); // the father asks for more vegetables
    }

    [Fact]
    public void The_servings_of_a_slot_ignore_the_tag_of_another_slot()
    {
        var withVegTag = Family(MemberTag.MoreVeg);

        Detail(Recipe(Line("thit", 100)), withVegTag).Servings.ShouldBe(1.595, Tolerance);
        Detail(Recipe(Line("rau", 100)) with { Slot = DishSlot.Vegetable }, Family(MemberTag.MoreProtein)).Servings.ShouldBe(1.595, Tolerance);
    }

    // ---- ingredients -------------------------------------------------------------------------

    [Fact]
    public void Ingredients_are_scaled_to_the_servings_and_named_for_the_region()
    {
        var dish = Recipe(Line("thit", 100), Line("rau", 50));

        var detail = Detail(dish);

        detail.Ingredients.Select(l => l.IngredientId).ShouldBe(["thit", "rau"]);
        detail.Ingredients.Select(l => l.Name).ShouldBe(["thịt lợn", "rau"]); // north name; the vegetable only has its default
        detail.Ingredients[0].GramsEdible.ShouldBe(159.5, Tolerance);
        detail.Ingredients[1].GramsEdible.ShouldBe(79.75, Tolerance);
        detail.Ingredients[0].Units.ShouldBeNull();
    }

    [Fact]
    public void Another_region_reads_its_own_name()
    {
        var south = Family() with { Region = Region.South };

        Detail(Recipe(Line("thit", 100)), south).Ingredients[0].Name.ShouldBe("thịt heo");
    }

    [Fact]
    public void A_line_counted_in_pieces_reports_whole_units_and_their_grams()
    {
        var dish = Recipe(new DishIngredient("trung", 50, ScaleKind.Integer, 1, "đánh tan"));

        var line = Detail(dish).Ingredients.Single();

        // 50 g × 1.595 = 79.75 g needed; 79.75 / 55 = 1.45 eggs, so 2 eggs = 110 g.
        line.Units.ShouldBe(2);
        line.UnitDisplay.ShouldBe("quả");
        line.GramsEdible.ShouldBe(110, Tolerance);
        line.Note.ShouldBe("đánh tan");
    }

    [Fact]
    public void A_line_counted_in_pieces_uses_the_market_unit_of_the_region_of_the_household()
    {
        var egg = Egg with
        {
            MarketUnit = new Dictionary<string, MarketUnit> { ["north"] = new("quả", 55, 1), ["south"] = new("trái", 60, 1) },
        };
        var dish = Recipe(new DishIngredient("trung", 50, ScaleKind.Integer, 1, null));
        var catalog = Catalog(Pork, Veg, egg, Balanced);

        var north = Detail(dish, Family(), ingredients: catalog).Ingredients.Single();
        var south = Detail(dish, Family() with { Region = Region.South }, ingredients: catalog).Ingredients.Single();

        // 50 g × 1.595 = 79.75 g needed: two pieces of 55 g in the north, two pieces of 60 g in the south.
        north.UnitDisplay.ShouldBe("quả");
        north.GramsEdible.ShouldBe(110, Tolerance);
        south.UnitDisplay.ShouldBe("trái");
        south.GramsEdible.ShouldBe(120, Tolerance);
    }

    [Fact]
    public void A_dish_without_ingredients_has_an_empty_recipe()
    {
        var detail = Detail(Recipe());

        detail.Ingredients.ShouldBeEmpty();
        detail.PerServing.ShouldBe(NutritionFacts.Zero);
        detail.Macros.ShouldBeNull();
    }

    // ---- steps -------------------------------------------------------------------------------

    [Fact]
    public void Minutes_add_up_over_the_steps_and_keep_active_and_passive_apart()
    {
        var dish = Recipe(Line("thit", 100)) with { Steps = [Step(1, 10, 0), Step(2, 5, 30), Step(3, 0, 15)] };

        var detail = Detail(dish);

        detail.ActiveMinutes.ShouldBe(15);
        detail.PassiveMinutes.ShouldBe(45);
        detail.TotalMinutes.ShouldBe(60);
    }

    [Fact]
    public void A_dish_without_steps_takes_no_time()
    {
        var detail = Detail(Recipe(Line("thit", 100)));

        detail.TotalMinutes.ShouldBe(0);
    }

    // ---- nutrition ---------------------------------------------------------------------------

    [Fact]
    public void Nutrition_is_one_serving_whatever_the_household_size()
    {
        var one = Home(BowlSize.Medium, Person("A", AgeGroup.Adult, 1));
        var many = Family() with { Members = [.. Family().Members, Person("B", AgeGroup.Adult, 4), Person("C", AgeGroup.Senior, 2)] };

        var small = Detail(Recipe(Line("thit", 100)), one).PerServing;
        var large = Detail(Recipe(Line("thit", 100)), many).PerServing;

        small.ShouldBe(large);
        small.Kcal.Known().ShouldBe(200, Tolerance);
        small.ProteinG.Known().ShouldBe(20, Tolerance);
    }

    [Fact]
    public void Each_member_gets_the_energy_of_a_serving_times_their_servings()
    {
        var detail = Detail(Recipe(Line("thit", 100)));

        detail.PerMember.Select(m => m.Name).ShouldBe(["Bố", "Con"]);
        detail.PerMember[0].Servings.ShouldBe(1.0, Tolerance);
        detail.PerMember[0].Kcal.Known().ShouldBe(200, Tolerance);
        detail.PerMember[1].Servings.ShouldBe(0.595, Tolerance);
        detail.PerMember[1].Kcal.Known().ShouldBe(119, Tolerance);
    }

    [Fact]
    public void A_member_is_not_given_the_extra_protein_servings_of_the_recipe_in_their_energy()
    {
        // The tag makes the recipe bigger for the household; the energy of one person still follows their own servings.
        var detail = Detail(Recipe(Line("thit", 100)), Family(MemberTag.MoreProtein));

        detail.PerMember[0].Servings.ShouldBe(1.0, Tolerance);
        detail.PerMember[0].Kcal.Known().ShouldBe(200, Tolerance);
    }

    [Fact]
    public void An_empty_household_has_no_members_and_no_servings()
    {
        var detail = Detail(Recipe(Line("thit", 100)), Home(BowlSize.Medium));

        detail.PerMember.ShouldBeEmpty();
        detail.Servings.ShouldBe(0);
        detail.Ingredients[0].GramsEdible.ShouldBe(0);
        detail.PerServing.Kcal.Known().ShouldBe(200, Tolerance);
    }

    [Fact]
    public void Unknown_energy_stays_unknown_for_everyone_and_is_never_zero()
    {
        var unknown = Catalog(Ingredient("thit"), Veg, Egg, Balanced);

        var detail = Detail(Recipe(Line("thit", 100)), ingredients: unknown);

        detail.PerServing.Kcal.ShouldBeNull();
        detail.PerMember.ShouldAllBe(m => m.Kcal == null);
        detail.Macros.ShouldBeNull();
    }

    [Fact]
    public void Ingredients_without_confirmed_data_are_named_for_the_region_and_only_when_they_contribute()
    {
        var noData = Pork with { NutritionPer100gEdible = Unknown };
        var dish = Recipe(Line("thit", 100), Line("rau", 50), Line("trung", 0));
        var catalog = Catalog(noData, Veg, Ingredient("trung"));

        var detail = Detail(dish, ingredients: catalog);

        detail.IngredientNamesMissingData.ShouldBe(["thịt lợn"]); // the egg has zero grams, so it adds nothing
    }

    [Fact]
    public void Complete_data_lists_no_missing_ingredient()
    {
        Detail(Recipe(Line("thit", 100), Line("rau", 50))).IngredientNamesMissingData.ShouldBeEmpty();
    }

    // ---- energy shares -----------------------------------------------------------------------

    [Fact]
    public void Shares_come_in_protein_fat_carbohydrate_order_with_their_recommended_range()
    {
        var detail = Detail(Recipe(Line("can-bang", 100)));

        detail.Macros.ShouldNotBeNull();
        detail.Macros!.Select(m => m.Macro).ShouldBe([Macro.Protein, Macro.Fat, Macro.Carb]);
        detail.Macros[0].Share.ShouldBe(0.15, Tolerance);
        detail.Macros[1].Share.ShouldBe(0.225, Tolerance);
        detail.Macros[2].Share.ShouldBe(0.625, Tolerance);
        detail.Macros[0].RecommendedMin.ShouldBe(0.13);
        detail.Macros[0].RecommendedMax.ShouldBe(0.20);
        detail.Macros[1].RecommendedMin.ShouldBe(0.20);
        detail.Macros[1].RecommendedMax.ShouldBe(0.25);
        detail.Macros[2].RecommendedMin.ShouldBe(0.55);
        detail.Macros[2].RecommendedMax.ShouldBe(0.65);
        detail.Macros.ShouldAllBe(m => m.Status == RangeStatus.Within);
    }

    [Fact]
    public void Shares_add_up_to_one()
    {
        var detail = Detail(Recipe(Line("thit", 100), Line("rau", 150)));

        detail.Macros!.Sum(m => m.Share).ShouldBe(1.0, Tolerance);
    }

    [Fact]
    public void A_share_is_below_within_or_above_its_range()
    {
        // 100 g pork: 80 kcal protein, 108 kcal fat, no carbohydrate → 80/188, 108/188, 0.
        var detail = Detail(Recipe(Line("thit", 100)));

        detail.Macros![0].Status.ShouldBe(RangeStatus.Above); // 0.4255 over 0.20
        detail.Macros[1].Status.ShouldBe(RangeStatus.Above);  // 0.5745 over 0.25
        detail.Macros[2].Status.ShouldBe(RangeStatus.Below);  // 0 under 0.55
        detail.Macros[2].Share.ShouldBe(0, Tolerance);
    }

    [Fact]
    public void A_share_just_under_its_minimum_is_below_and_just_over_its_maximum_is_above()
    {
        // 10 g of each: 40, 90 and 40 kcal of 170.
        var ten = Catalog(Ingredient("m", Facts(0, 10, 10, 10, 0, 0)));
        var dish = Recipe(Line("m", 100));
        var share = 40.0 / 170;

        var under = Detail(dish, nutrition: Recommended(protein: new ShareRange(share + 1e-6, 1)), ingredients: ten);
        var over = Detail(dish, nutrition: Recommended(protein: new ShareRange(0, share - 1e-6)), ingredients: ten);

        under.Macros![0].Status.ShouldBe(RangeStatus.Below);
        over.Macros![0].Status.ShouldBe(RangeStatus.Above);
    }

    [Fact]
    public void A_share_exactly_on_a_limit_of_its_range_is_within()
    {
        var ten = Catalog(Ingredient("m", Facts(0, 10, 10, 10, 0, 0)));
        var dish = Recipe(Line("m", 100));
        var share = 40.0 / 170; // the protein share is computed as 40 / 170, so it matches bit for bit

        var onMinimum = Detail(dish, nutrition: Recommended(protein: new ShareRange(share, 1)), ingredients: ten);
        var onMaximum = Detail(dish, nutrition: Recommended(protein: new ShareRange(0, share)), ingredients: ten);

        onMinimum.Macros![0].Status.ShouldBe(RangeStatus.Within);
        onMaximum.Macros![0].Status.ShouldBe(RangeStatus.Within);
    }

    [Fact]
    public void Each_macronutrient_is_judged_against_its_own_range()
    {
        var ten = Catalog(Ingredient("m", Facts(0, 10, 10, 10, 0, 0)));
        var dish = Recipe(Line("m", 100));

        // Protein and carbohydrate each make 40/170 = 0.235, fat 0.529.
        var detail = Detail(
            dish,
            nutrition: Recommended(new ShareRange(0.30, 0.40), new ShareRange(0.10, 0.20), new ShareRange(0.20, 0.25)),
            ingredients: ten);

        detail.Macros!.Select(m => m.Status).ShouldBe([RangeStatus.Below, RangeStatus.Above, RangeStatus.Within]);
        detail.Macros!.Select(m => m.RecommendedMin).ShouldBe([0.30, 0.10, 0.20]);
        detail.Macros!.Select(m => m.RecommendedMax).ShouldBe([0.40, 0.20, 0.25]);
    }

    [Fact]
    public void Shares_are_unknown_when_a_macronutrient_is_unknown()
    {
        var noFat = Catalog(Ingredient("m", Facts(100, 10, 10, 10, 0, 0) with { FatG = null }));

        var detail = Detail(Recipe(Line("m", 100)), ingredients: noFat);

        detail.Macros.ShouldBeNull();
        detail.PerServing.Kcal.Known().ShouldBe(100, Tolerance); // the known figures are still shown
    }

    // ---- bad input ---------------------------------------------------------------------------

    [Fact]
    public void A_dish_with_an_ingredient_missing_from_the_list_is_a_data_error()
    {
        var error = Should.Throw<InvalidOperationException>(() => Detail(Recipe(Line("khong-co", 10))));

        error.Message.ShouldContain("khong-co");
    }

    [Fact]
    public void Bowls_outside_the_configured_range_are_rejected()
    {
        var household = Home(BowlSize.Medium, Person("A", AgeGroup.Adult, 9));

        Should.Throw<ArgumentOutOfRangeException>(() => Detail(Recipe(Line("thit", 100)), household));
    }

    [Fact]
    public void An_unusable_nutrition_config_is_rejected_when_the_builder_is_made()
    {
        var broken = NutritionConfig with { KcalPerGram = new MacroValues(0, 9, 4) };

        Should.Throw<ArgumentException>(() => Builder(broken));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var builder = Builder();

        Should.Throw<ArgumentNullException>(() => new DishDetailBuilder(null!, NutritionConfig));
        Should.Throw<ArgumentNullException>(() => new DishDetailBuilder(PortionConfig, null!));
        Should.Throw<ArgumentNullException>(() => builder.Build(null!, Family(), Pantry));
        Should.Throw<ArgumentNullException>(() => builder.Build(Recipe(), null!, Pantry));
        Should.Throw<ArgumentNullException>(() => builder.Build(Recipe(), Family(), null!));
    }

    private static NutritionConfig Recommended(ShareRange? protein = null, ShareRange? fat = null, ShareRange? carb = null) => NutritionConfig with
    {
        RecommendedEnergyShare = new RecommendedShares(
            protein ?? NutritionConfig.RecommendedEnergyShare.Protein,
            fat ?? NutritionConfig.RecommendedEnergyShare.Fat,
            carb ?? NutritionConfig.RecommendedEnergyShare.Carb),
    };
}
