using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using static MamCom.Core.Tests.TestData;

namespace MamCom.Core.Tests.Nutrition;

public class NutritionCalculatorTests
{
    private const double Tolerance = 1e-9;

    private static readonly NutritionConfig NutritionConfig = NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json"));

    private static readonly PortionConfig PortionConfig = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    private static NutritionCalculator Calculator(NutritionConfig? nutrition = null, PortionConfig? portions = null) =>
        new(nutrition ?? NutritionConfig, portions ?? PortionConfig);

    // ---- test-only fixture -------------------------------------------------------------------
    // These per-100 g numbers are invented for the arithmetic and are not nutrition data.
    // Expected results below were worked out by hand and cross-checked with exact fractions in Python.

    private static Ingredient Rice => Ingredient("gao", Facts(360, 7, 1, 80, 1, 5));

    private static Ingredient Pork => Ingredient("thit", Facts(200, 20, 12, 0, 0, 60));

    private static Ingredient Veg => Ingredient("rau", Facts(20, 2, 0, 3, 2, 10));

    private static Ingredient Oil => Ingredient("dau", Facts(900, 0, 100, 0, 0, 0));

    private static Dictionary<string, Ingredient> Pantry => Catalog(Rice, Pork, Veg, Oil);

    // 100 g pork + 10 g oil of which half is eaten (deep-frying oil): per serving 245 kcal, 20 g protein, 17 g fat, 60 mg sodium.
    private static Dish Braised => Dish("thit-kho", Line("thit", 100), Line("dau", 10, consumedRatio: 0.5));

    // 150 g vegetables: per serving 30 kcal, 3 g protein, 4.5 g carbohydrate, 3 g fibre, 15 mg sodium.
    private static Dish Soup => Dish("canh-rau", Line("rau", 150));

    // Father: (0.4 + 0.3 × 2) × 1.0 = 1.0 serving, 2 bowls = 80 g raw rice.
    // Child: (0.4 + 0.3 × 1) × 0.85 = 0.595 serving, 1 bowl = 40 g raw rice.
    private static Household Family => Home(BowlSize.Medium, Person("Bố", AgeGroup.Adult, 2), Person("Con", AgeGroup.Child, 1));

    private static MealNutrition Meal(Household? household = null, IReadOnlyList<Dish>? dishes = null, IReadOnlyDictionary<string, Ingredient>? catalog = null)
    {
        household ??= Family;
        var portions = new PortionCalculator(PortionConfig).Calculate(household);
        return Calculator().ForMeal(household, portions, dishes ?? [Braised, Soup], catalog ?? Pantry);
    }

    private static void ShouldBeFacts(NutritionFacts actual, double kcal, double protein, double fat, double carb, double fiber, double sodium)
    {
        actual.Kcal.Known().ShouldBe(kcal, Tolerance);
        actual.ProteinG.Known().ShouldBe(protein, Tolerance);
        actual.FatG.Known().ShouldBe(fat, Tolerance);
        actual.CarbG.Known().ShouldBe(carb, Tolerance);
        actual.FiberG.Known().ShouldBe(fiber, Tolerance);
        actual.SodiumMg.Known().ShouldBe(sodium, Tolerance);
    }

    // ---- per serving of a dish ---------------------------------------------------------------

    [Fact]
    public void A_dish_serving_sums_grams_times_consumed_ratio_times_nutrient_per_100_g()
    {
        var result = Calculator().ForDish(Braised, Pantry);

        result.DishId.ShouldBe("thit-kho");
        ShouldBeFacts(result.PerServing, kcal: 245, protein: 20, fat: 17, carb: 0, fiber: 0, sodium: 60);
        result.IngredientIdsMissingData.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1.0, 10.0)]   // all 10 g of oil eaten
    [InlineData(0.5, 5.0)]    // half eaten
    [InlineData(0.0, 0.0)]    // none eaten
    public void Consumed_ratio_scales_the_ingredient_without_touching_the_others(double ratio, double expectedFatG)
    {
        var dish = Dish("chien", Line("dau", 10, ratio));

        var result = Calculator().ForDish(dish, Pantry);

        result.PerServing.FatG.Known().ShouldBe(expectedFatG, Tolerance);
        result.PerServing.Kcal.Known().ShouldBe(expectedFatG * 9, Tolerance); // 900 kcal per 100 g
    }

    [Fact]
    public void A_dish_without_ingredients_has_zero_nutrition()
    {
        var result = Calculator().ForDish(Dish("trong"), Pantry);

        result.PerServing.ShouldBe(NutritionFacts.Zero);
        result.IngredientIdsMissingData.ShouldBeEmpty();
    }

    // ---- unknown data ------------------------------------------------------------------------

    [Fact]
    public void An_unknown_nutrient_makes_the_dish_total_unknown_not_lower()
    {
        var mystery = Ingredient("la-la", Facts(50, 5, 1, 2, 3, 4) with { ProteinG = null });
        var dish = Dish("thu", Line("thit", 100), Line("la-la", 100));

        var result = Calculator().ForDish(dish, Catalog(Pork, mystery));

        result.PerServing.ProteinG.ShouldBeNull();
        result.PerServing.Kcal.Known().ShouldBe(250, Tolerance); // 200 + 50, the known nutrients still add up
        result.PerServing.FatG.Known().ShouldBe(13, Tolerance);
        result.IngredientIdsMissingData.ShouldBe(["la-la"]);
    }

    [Fact]
    public void A_fully_placeholder_ingredient_leaves_every_nutrient_unknown_and_is_listed_once()
    {
        var dish = Dish("thu", Line("an", 10), Line("an", 20));

        var result = Calculator().ForDish(dish, Catalog(Ingredient("an")));

        result.PerServing.Kcal.ShouldBeNull();
        result.PerServing.SodiumMg.ShouldBeNull();
        result.IngredientIdsMissingData.ShouldBe(["an"]);
    }

    [Fact]
    public void Lines_that_contribute_nothing_do_not_count_as_missing_data()
    {
        // Zero grams, or none of it eaten, adds nothing, so unknown data there does not matter.
        var dish = Dish("thu", Line("thit", 100), Line("an", 0), Line("an", 50, consumedRatio: 0));

        var result = Calculator().ForDish(dish, Catalog(Pork, Ingredient("an")));

        result.PerServing.IsComplete().ShouldBeTrue();
        result.PerServing.Kcal.Known().ShouldBe(200, Tolerance);
        result.IngredientIdsMissingData.ShouldBeEmpty();
    }

    [Fact]
    public void Unknown_data_reaches_the_meal_totals_and_removes_the_energy_shares()
    {
        var noProtein = Ingredient("thit", Facts(200, 20, 12, 0, 0, 60) with { ProteinG = null });

        var result = Meal(catalog: Catalog(Rice, noProtein, Veg, Oil));

        result.Total.ProteinG.ShouldBeNull();
        result.PerMember.ShouldAllBe(m => m.ProteinG == null);
        result.Total.Kcal.ShouldNotBeNull();
        result.EnergyShares.ShouldBeNull();
        result.Dishes[0].IngredientIdsMissingData.ShouldBe(["thit"]);
    }

    [Fact]
    public void Rice_without_data_makes_every_member_unknown()
    {
        var result = Meal(catalog: Catalog(Ingredient("gao"), Pork, Veg, Oil));

        result.PerMember.ShouldAllBe(m => m.Kcal == null);
        result.Total.Kcal.ShouldBeNull();
    }

    // ---- meal: per person and total ----------------------------------------------------------

    [Fact]
    public void Each_member_gets_the_dishes_times_their_servings_plus_their_own_rice()
    {
        var result = Meal();

        // Father: (245 + 30) × 1.0 + 80 g rice × (360, 7, 1, 80, 1, 5) / 100
        ShouldBeFacts(result.PerMember[0], kcal: 563, protein: 28.6, fat: 17.8, carb: 68.5, fiber: 3.8, sodium: 79);

        // Child: (245 + 30) × 0.595 + 40 g rice
        ShouldBeFacts(result.PerMember[1], kcal: 307.625, protein: 16.485, fat: 10.515, carb: 34.6775, fiber: 2.185, sodium: 46.625);
    }

    [Fact]
    public void The_meal_total_is_the_sum_of_the_members()
    {
        var result = Meal();

        ShouldBeFacts(result.Total, kcal: 870.625, protein: 45.085, fat: 28.315, carb: 103.1775, fiber: 5.985, sodium: 125.625);
    }

    [Fact]
    public void Dishes_are_reported_in_the_order_given()
    {
        var result = Meal(dishes: [Soup, Braised]);

        result.Dishes.Select(d => d.DishId).ShouldBe(["canh-rau", "thit-kho"]);
        ShouldBeFacts(result.Dishes[0].PerServing, 30, 3, 0, 4.5, 3, 15);
        ShouldBeFacts(result.Dishes[1].PerServing, 245, 20, 17, 0, 0, 60);
    }

    [Fact]
    public void A_meal_without_dishes_is_just_rice()
    {
        var result = Meal(dishes: []);

        // Father 80 g and Child 40 g of rice: 120 g in total.
        ShouldBeFacts(result.PerMember[0], 288, 5.6, 0.8, 64, 0.8, 4);
        ShouldBeFacts(result.PerMember[1], 144, 2.8, 0.4, 32, 0.4, 2);
        ShouldBeFacts(result.Total, 432, 8.4, 1.2, 96, 1.2, 6);
    }

    [Theory]
    [InlineData(BowlSize.Small, 35)]
    [InlineData(BowlSize.Medium, 40)]
    [InlineData(BowlSize.Large, 50)]
    public void Rice_follows_the_bowl_size(BowlSize size, double gramsPerBowl)
    {
        var household = Home(size, Person("Bố", AgeGroup.Adult, 2));

        var result = Meal(household, dishes: []);

        result.PerMember[0].Kcal.Known().ShouldBe(2 * gramsPerBowl * 3.6, Tolerance); // 360 kcal per 100 g
    }

    [Fact]
    public void An_empty_household_has_nothing_to_eat()
    {
        var result = Meal(Home(BowlSize.Medium));

        result.PerMember.ShouldBeEmpty();
        result.Total.ShouldBe(NutritionFacts.Zero);
        result.EnergyShares.ShouldBeNull();
    }

    [Fact]
    public void Energy_shares_of_the_meal_come_from_the_total()
    {
        var result = Meal();

        // protein 45.085 g × 4, fat 28.315 g × 9, carbohydrate 103.1775 g × 4 kcal
        result.EnergyShares.ShouldNotBeNull();
        result.EnergyShares!.Protein.ShouldBe(0.2126939384468413, Tolerance);
        result.EnergyShares.Fat.ShouldBe(0.300553730753581, Tolerance);
        result.EnergyShares.Carb.ShouldBe(0.4867523307995778, Tolerance);
    }

    // ---- energy shares -----------------------------------------------------------------------

    [Fact]
    public void Energy_shares_use_4_9_4_kcal_per_gram_and_add_up_to_one()
    {
        var shares = Calculator().EnergySharesOf(Facts(0, 10, 10, 10, 0, 0));

        // 40 + 90 + 40 = 170 kcal
        shares.ShouldNotBeNull();
        shares!.Protein.ShouldBe(40.0 / 170, Tolerance);
        shares.Fat.ShouldBe(90.0 / 170, Tolerance);
        shares.Carb.ShouldBe(40.0 / 170, Tolerance);
        (shares.Protein + shares.Fat + shares.Carb).ShouldBe(1.0, Tolerance);
    }

    [Fact]
    public void Energy_shares_ignore_the_kcal_figure_and_use_the_factors_from_the_config()
    {
        var custom = NutritionConfig with { KcalPerGram = new MacroValues(5, 10, 3) };

        var shares = Calculator(custom).EnergySharesOf(Facts(9999, 10, 10, 10, 0, 0));

        // 50 + 100 + 30 = 180 kcal
        shares.ShouldNotBeNull();
        shares!.Protein.ShouldBe(50.0 / 180, Tolerance);
        shares.Fat.ShouldBe(100.0 / 180, Tolerance);
        shares.Carb.ShouldBe(30.0 / 180, Tolerance);
    }

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(10, 0, 10)]
    [InlineData(10, 10, 0)]
    public void A_macronutrient_at_zero_is_a_valid_zero_share(double protein, double fat, double carb)
    {
        var shares = Calculator().EnergySharesOf(Facts(0, protein, fat, carb, 0, 0));

        shares.ShouldNotBeNull();
        (shares!.Protein + shares.Fat + shares.Carb).ShouldBe(1.0, Tolerance);
    }

    [Fact]
    public void Energy_shares_are_unknown_when_a_macronutrient_is_unknown_or_there_is_no_energy()
    {
        var calculator = Calculator();

        calculator.EnergySharesOf(Facts(100, 10, 10, 10, 0, 0) with { ProteinG = null }).ShouldBeNull();
        calculator.EnergySharesOf(Facts(100, 10, 10, 10, 0, 0) with { FatG = null }).ShouldBeNull();
        calculator.EnergySharesOf(Facts(100, 10, 10, 10, 0, 0) with { CarbG = null }).ShouldBeNull();
        calculator.EnergySharesOf(TestData.Unknown).ShouldBeNull();
        calculator.EnergySharesOf(NutritionFacts.Zero).ShouldBeNull();
    }

    // ---- bad input ---------------------------------------------------------------------------

    [Fact]
    public void A_dish_with_an_unknown_ingredient_is_a_data_error()
    {
        var error = Should.Throw<InvalidOperationException>(() => Calculator().ForDish(Dish("canh-loi", Line("khong-co", 10)), Pantry));

        error.Message.ShouldContain("canh-loi");
        error.Message.ShouldContain("khong-co");
    }

    [Fact]
    public void A_meal_without_the_rice_ingredient_is_a_data_error()
    {
        var error = Should.Throw<InvalidOperationException>(() => Meal(catalog: Catalog(Pork, Veg, Oil)));

        error.Message.ShouldContain("gao");
    }

    [Fact]
    public void The_rice_ingredient_id_comes_from_the_portion_config()
    {
        var jasmine = Ingredient("gao-thom", Facts(360, 7, 1, 80, 1, 5));
        var portions = PortionConfig with { RiceIngredientId = "gao-thom" };
        var household = Family;
        var calculated = new PortionCalculator(portions).Calculate(household);

        var result = Calculator(portions: portions).ForMeal(household, calculated, [], Catalog(jasmine));

        result.Total.Kcal.Known().ShouldBe(432, Tolerance); // 120 g of rice
    }

    [Theory]
    [InlineData(-1.0, 1.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    [InlineData(10.0, -0.1)]
    [InlineData(10.0, 1.1)]
    [InlineData(10.0, double.NaN)]
    public void A_recipe_line_with_an_unusable_number_is_a_data_error(double grams, double ratio)
    {
        var dish = Dish("loi", Line("thit", grams, ratio));

        Should.Throw<InvalidOperationException>(() => Calculator().ForDish(dish, Pantry));
    }

    [Fact]
    public void Portions_calculated_for_another_household_are_rejected()
    {
        var other = new PortionCalculator(PortionConfig).Calculate(Home(BowlSize.Medium, Person("Một", AgeGroup.Adult, 2)));

        // An out-of-range read would also throw an ArgumentException, so check it is the deliberate check.
        var error = Should.Throw<ArgumentException>(() => Calculator().ForMeal(Family, other, [], Pantry));

        error.ParamName.ShouldBe("portions");
        error.Message.ShouldContain("1 members");
        error.Message.ShouldContain("2");
    }

    [Fact]
    public void Portions_for_more_members_than_the_household_are_rejected_too()
    {
        var other = new PortionCalculator(PortionConfig).Calculate(
            Home(BowlSize.Medium, Person("A", AgeGroup.Adult, 2), Person("B", AgeGroup.Adult, 2), Person("C", AgeGroup.Adult, 2)));

        Should.Throw<ArgumentException>(() => Calculator().ForMeal(Family, other, [], Pantry)).ParamName.ShouldBe("portions");
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var portions = new PortionCalculator(PortionConfig).Calculate(Family);

        Should.Throw<ArgumentNullException>(() => new NutritionCalculator(null!, PortionConfig));
        Should.Throw<ArgumentNullException>(() => new NutritionCalculator(NutritionConfig, null!));
        Should.Throw<ArgumentNullException>(() => Calculator().ForDish(null!, Pantry));
        Should.Throw<ArgumentNullException>(() => Calculator().ForDish(Braised, null!));
        Should.Throw<ArgumentNullException>(() => Calculator().ForMeal(null!, portions, [], Pantry));
        Should.Throw<ArgumentNullException>(() => Calculator().ForMeal(Family, null!, [], Pantry));
        Should.Throw<ArgumentNullException>(() => Calculator().ForMeal(Family, portions, null!, Pantry));
        Should.Throw<ArgumentNullException>(() => Calculator().ForMeal(Family, portions, [], null!));
        Should.Throw<ArgumentNullException>(() => Calculator().EnergySharesOf(null!));
    }

    // ---- properties --------------------------------------------------------------------------

    [Fact]
    public void Totals_and_shares_stay_consistent_for_many_random_households()
    {
        var random = new SeededRandomSource(2026);
        var calculator = new PortionCalculator(PortionConfig);

        for (var run = 0; run < 300; run++)
        {
            var members = Enumerable.Range(0, 1 + random.NextInt(6))
                .Select(_ => Person("Người", Enum.GetValues<AgeGroup>()[random.NextInt(5)], 0.5 * (1 + random.NextInt(10))))
                .ToArray();
            var household = Home(Enum.GetValues<BowlSize>()[random.NextInt(3)], members);
            var dishes = new[] { Braised, Soup }.Take(random.NextInt(3)).ToArray();

            var result = Calculator().ForMeal(household, calculator.Calculate(household), dishes, Pantry);

            result.PerMember.Count.ShouldBe(members.Length);
            result.Total.Kcal.Known().ShouldBe(result.PerMember.Sum(m => m.Kcal!.Value), Tolerance);
            result.Total.SodiumMg.Known().ShouldBe(result.PerMember.Sum(m => m.SodiumMg!.Value), Tolerance);
            result.PerMember.ShouldAllBe(m => m.Kcal > 0 && m.ProteinG >= 0 && m.FatG >= 0 && m.CarbG >= 0);
            var shares = result.EnergyShares!;
            (shares.Protein + shares.Fat + shares.Carb).ShouldBe(1.0, Tolerance);
            shares.Protein.ShouldBeInRange(0, 1);
        }
    }

    [Fact]
    public void A_larger_helping_never_has_less_of_anything()
    {
        // Same dishes, bigger member: more bowls means more servings and more rice.
        var small = Home(BowlSize.Medium, Person("A", AgeGroup.Adult, 1));
        var big = Home(BowlSize.Medium, Person("A", AgeGroup.Adult, 3));

        var before = Meal(small).Total;
        var after = Meal(big).Total;

        after.Kcal.Known().ShouldBeGreaterThan(before.Kcal.Known());
        after.ProteinG.Known().ShouldBeGreaterThan(before.ProteinG.Known());
        after.FatG.Known().ShouldBeGreaterThan(before.FatG.Known());
        after.CarbG.Known().ShouldBeGreaterThan(before.CarbG.Known());
        after.SodiumMg.Known().ShouldBeGreaterThan(before.SodiumMg.Known());
    }

    [Fact]
    public void Member_order_does_not_change_the_total()
    {
        var forward = Meal(Home(BowlSize.Medium, Person("A", AgeGroup.Adult, 2), Person("B", AgeGroup.Teen, 3), Person("C", AgeGroup.Toddler, 1)));
        var backward = Meal(Home(BowlSize.Medium, Person("C", AgeGroup.Toddler, 1), Person("B", AgeGroup.Teen, 3), Person("A", AgeGroup.Adult, 2)));

        backward.Total.Kcal.Known().ShouldBe(forward.Total.Kcal!.Value, Tolerance);
        backward.Total.FatG.Known().ShouldBe(forward.Total.FatG!.Value, Tolerance);
    }
}
