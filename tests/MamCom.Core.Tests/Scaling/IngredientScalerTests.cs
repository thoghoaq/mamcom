using MamCom.Core.Models;
using MamCom.Core.Portions;
using MamCom.Core.Scaling;

namespace MamCom.Core.Tests.Scaling;

public class IngredientScalerTests
{
    private const double Tolerance = 1e-9;

    private static readonly PortionConfig Config = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    private static IngredientScaler Scaler(PortionConfig? config = null) => new(config ?? Config);

    // Test-only numbers: grams per unit and per serving are made up to exercise the arithmetic.
    private static Ingredient Item(string id, params (string Region, string Display, double GramsPerUnit)[] units) => new(
        Id: id,
        Names: new Dictionary<string, string> { [RegionKeys.Default] = id },
        Aliases: [],
        Category: "test",
        Aisle: Aisle.Other,
        ProteinSource: null,
        MarketUnit: units.ToDictionary(u => u.Region, u => new MarketUnit(u.Display, u.GramsPerUnit, 1)),
        WasteRatio: 0,
        IsPantry: false,
        NutritionPer100gEdible: new NutritionFacts(null, null, null, null, null, null),
        NutritionSource: new NutritionSource(NutritionDbs.Placeholder, null, null, null, null),
        Image: new ImageRef(null, null, null, null, false));

    private static DishIngredient Line(string id, double gramsPerServing, ScaleKind scale, string? note = null) =>
        new(id, gramsPerServing, scale, 1, note);

    private static Ingredient Egg => Item("trung", ("north", "quả", 55), ("south", "quả", 55));

    // ---- the four kinds ----------------------------------------------------------------------

    [Fact]
    public void Linear_multiplies_grams_by_servings()
    {
        var scaled = Scaler().Scale(Line("ca-loc", 80, ScaleKind.Linear), Item("ca-loc"), Region.South, 3.7875);

        scaled.GramsEdible.ShouldBe(303.0, Tolerance); // 80 × 3.7875
        scaled.Units.ShouldBeNull();
        scaled.UnitDisplay.ShouldBeNull();
    }

    [Fact]
    public void Sublinear_raises_servings_to_the_configured_exponent()
    {
        var scaled = Scaler().Scale(Line("nuoc-mam", 10, ScaleKind.Sublinear), Item("nuoc-mam"), Region.South, 4);

        // 4^0.8 = 3.0314331330…, worked out by hand; plain linear scaling would give 40.
        scaled.GramsEdible.ShouldBe(30.314331330, 1e-6);
        scaled.Units.ShouldBeNull();
    }

    [Fact]
    public void Sublinear_uses_the_exponent_from_the_config_not_a_constant()
    {
        var squareRoot = Config with { SublinearExponent = 0.5 };

        var scaled = Scaler(squareRoot).Scale(Line("nuoc-mam", 10, ScaleKind.Sublinear), Item("nuoc-mam"), Region.South, 4);

        scaled.GramsEdible.ShouldBe(20.0, Tolerance); // 10 × 4^0.5
    }

    [Theory]
    [InlineData(1.0, 10.0)]
    [InlineData(0.0, 0.0)]
    public void Sublinear_edges(double servings, double expectedGrams)
    {
        Scaler().Scale(Line("x", 10, ScaleKind.Sublinear), Item("x"), Region.South, servings).GramsEdible.ShouldBe(expectedGrams, Tolerance);
    }

    [Fact]
    public void Sublinear_gives_a_small_household_more_than_its_proportional_share()
    {
        // The reason spices are sublinear: below one serving, S^0.8 is larger than S.
        var scaled = Scaler().Scale(Line("hanh", 10, ScaleKind.Sublinear), Item("hanh"), Region.South, 0.5);

        scaled.GramsEdible.ShouldBeGreaterThan(5.0);
        scaled.GramsEdible.ShouldBe(10 * Math.Pow(0.5, 0.8), Tolerance);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(3.7875)]
    [InlineData(40.0)]
    public void Fixed_ignores_servings(double servings)
    {
        Scaler().Scale(Line("la-thom", 5, ScaleKind.Fixed), Item("la-thom"), Region.South, servings).GramsEdible.ShouldBe(5.0);
    }

    // ---- whole units -------------------------------------------------------------------------

    [Fact]
    public void Integer_rounds_up_to_whole_units_and_reports_the_grams_of_those_units()
    {
        // 50 g × 3.7875 = 189.375 g needed; 189.375 / 55 = 3.44 eggs, so 4 eggs = 220 g.
        var scaled = Scaler().Scale(Line("trung", 50, ScaleKind.Integer, "đánh tan"), Egg, Region.South, 3.7875);

        scaled.Units.ShouldBe(4);
        scaled.UnitDisplay.ShouldBe("quả");
        scaled.GramsEdible.ShouldBe(220.0, Tolerance);
        scaled.Note.ShouldBe("đánh tan");
    }

    [Theory]
    [InlineData(55.0, 2.0, 2)]     // 110 g is exactly 2 eggs: stays 2
    [InlineData(55.0, 2.001, 3)]   // a hair over 2 eggs needs 3
    [InlineData(1.0, 0.1, 1)]      // any positive need is at least one unit
    [InlineData(55.0, 0.0, 0)]     // no servings, no eggs
    public void Integer_rounds_up_only_when_the_need_is_above_a_whole_unit(double gramsPerServing, double servings, int expectedUnits)
    {
        var scaled = Scaler().Scale(Line("trung", gramsPerServing, ScaleKind.Integer), Egg, Region.South, servings);

        scaled.Units.ShouldBe(expectedUnits);
        scaled.GramsEdible.ShouldBe(expectedUnits * 55.0, Tolerance);
    }

    [Fact]
    public void Integer_ignores_floating_point_noise_just_above_a_whole_unit()
    {
        // 0.1 × 3 is 0.30000000000000004; divided by 0.1 it is 3.0000000000000004, which a naive ceil turns into 4.
        var scaled = Scaler().Scale(Line("x", 0.1, ScaleKind.Integer), Item("x", ("south", "g", 0.1)), Region.South, 3);

        scaled.Units.ShouldBe(3);
    }

    [Fact]
    public void Integer_takes_grams_per_unit_from_the_households_region()
    {
        var shrimp = Item("tom", ("north", "lạng", 100), ("south", "con", 20));

        var north = Scaler().Scale(Line("tom", 60, ScaleKind.Integer), shrimp, Region.North, 2); // 120 g -> 2 × 100 g
        var south = Scaler().Scale(Line("tom", 60, ScaleKind.Integer), shrimp, Region.South, 2); // 120 g -> 6 × 20 g

        north.Units.ShouldBe(2);
        north.UnitDisplay.ShouldBe("lạng");
        north.GramsEdible.ShouldBe(200.0);
        south.Units.ShouldBe(6);
        south.UnitDisplay.ShouldBe("con");
        south.GramsEdible.ShouldBe(120.0, Tolerance);
    }

    [Fact]
    public void Integer_falls_back_to_the_default_market_unit_for_a_region_without_its_own()
    {
        var egg = Item("trung", (RegionKeys.Default, "quả", 55));

        var scaled = Scaler().Scale(Line("trung", 50, ScaleKind.Integer), egg, Region.Central, 3.7875);

        scaled.Units.ShouldBe(4);
    }

    [Fact]
    public void Integer_without_any_market_unit_for_the_region_is_a_data_error()
    {
        var northOnly = Item("trung", ("north", "quả", 55));

        var error = Should.Throw<InvalidOperationException>(() =>
            Scaler().Scale(Line("trung", 50, ScaleKind.Integer), northOnly, Region.Central, 3));

        error.Message.ShouldContain("trung");
        error.Message.ShouldContain("central");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Integer_with_an_unusable_grams_per_unit_is_a_data_error(double gramsPerUnit)
    {
        var broken = Item("trung", ("south", "quả", gramsPerUnit));

        Should.Throw<InvalidOperationException>(() =>
            Scaler().Scale(Line("trung", 50, ScaleKind.Integer), broken, Region.South, 3));
    }

    [Fact]
    public void Other_kinds_do_not_need_a_market_unit()
    {
        var noUnits = Item("rau");

        Should.NotThrow(() => Scaler().Scale(Line("rau", 100, ScaleKind.Linear), noUnits, Region.South, 3));
        Should.NotThrow(() => Scaler().Scale(Line("rau", 100, ScaleKind.Sublinear), noUnits, Region.South, 3));
        Should.NotThrow(() => Scaler().Scale(Line("rau", 100, ScaleKind.Fixed), noUnits, Region.South, 3));
    }

    [Fact]
    public void A_need_too_large_for_an_int_count_of_units_overflows_loudly()
    {
        var tiny = Item("hat", ("south", "hạt", 1e-6));

        Should.Throw<OverflowException>(() =>
            Scaler().Scale(Line("hat", 1000, ScaleKind.Integer), tiny, Region.South, 1000));
    }

    // ---- notes and ids pass through ----------------------------------------------------------

    [Fact]
    public void The_recipe_note_is_passed_through_for_every_kind()
    {
        foreach (var kind in Enum.GetValues<ScaleKind>())
        {
            var scaled = Scaler().Scale(Line("trung", 50, kind, "cắt khúc"), Egg, Region.South, 3);

            scaled.IngredientId.ShouldBe("trung");
            scaled.Note.ShouldBe("cắt khúc");
        }
    }

    [Fact]
    public void A_line_and_ingredient_that_do_not_match_are_rejected()
    {
        Should.Throw<ArgumentException>(() =>
            Scaler().Scale(Line("ca-loc", 80, ScaleKind.Linear), Item("thit-ga"), Region.South, 3));
    }

    // ---- input checks ------------------------------------------------------------------------

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Servings_must_be_finite_and_not_negative(double servings)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Scaler().Scale(Line("x", 10, ScaleKind.Linear), Item("x"), Region.South, servings));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Grams_per_serving_must_be_finite_and_not_negative(double grams)
    {
        Should.Throw<ArgumentException>(() =>
            Scaler().Scale(Line("x", grams, ScaleKind.Linear), Item("x"), Region.South, 2));
    }

    [Fact]
    public void An_undefined_scale_kind_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Scaler().Scale(Line("x", 10, (ScaleKind)99), Item("x"), Region.South, 2));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var line = Line("x", 10, ScaleKind.Linear);

        Should.Throw<ArgumentNullException>(() => new IngredientScaler(null!));
        Should.Throw<ArgumentNullException>(() => Scaler().Scale(null!, Item("x"), Region.South, 1));
        Should.Throw<ArgumentNullException>(() => Scaler().Scale(line, null!, Region.South, 1));
    }

    [Fact]
    public void An_unusable_config_is_rejected_when_the_scaler_is_created()
    {
        Should.Throw<ArgumentException>(() => new IngredientScaler(Config with { SublinearExponent = 2 }));
    }

    // ---- ScaleDish ---------------------------------------------------------------------------

    [Fact]
    public void ScaleDish_scales_every_line_in_recipe_order_for_the_given_servings()
    {
        var dish = SampleDish(
            Line("ca-loc", 80, ScaleKind.Linear, "cắt khúc"),
            Line("nuoc-mam", 10, ScaleKind.Sublinear),
            Line("trung", 50, ScaleKind.Integer),
            Line("la-thom", 5, ScaleKind.Fixed));
        var catalog = Catalog(Item("ca-loc"), Item("nuoc-mam"), Egg, Item("la-thom"));

        var scaled = Scaler().ScaleDish(dish, catalog, Region.South, 4);

        scaled.Select(s => s.IngredientId).ShouldBe(["ca-loc", "nuoc-mam", "trung", "la-thom"]);
        scaled[0].GramsEdible.ShouldBe(320.0, Tolerance);          // 80 × 4
        scaled[0].Note.ShouldBe("cắt khúc");
        scaled[1].GramsEdible.ShouldBe(30.314331330, 1e-6);        // 10 × 4^0.8
        scaled[2].Units.ShouldBe(4);                                // 200 g / 55 g = 3.64 -> 4
        scaled[3].GramsEdible.ShouldBe(5.0);
    }

    [Fact]
    public void ScaleDish_on_a_dish_without_lines_gives_an_empty_list()
    {
        Scaler().ScaleDish(SampleDish(), Catalog(), Region.South, 3).ShouldBeEmpty();
    }

    [Fact]
    public void ScaleDish_reports_the_dish_and_the_unknown_ingredient()
    {
        var dish = SampleDish(Line("ca-loc", 80, ScaleKind.Linear), Line("thieu", 10, ScaleKind.Linear));

        var error = Should.Throw<InvalidOperationException>(() =>
            Scaler().ScaleDish(dish, Catalog(Item("ca-loc")), Region.South, 3));

        error.Message.ShouldContain("canh-test");
        error.Message.ShouldContain("thieu");
    }

    [Fact]
    public void ScaleDish_rejects_null_arguments()
    {
        Should.Throw<ArgumentNullException>(() => Scaler().ScaleDish(null!, Catalog(), Region.South, 1));
        Should.Throw<ArgumentNullException>(() => Scaler().ScaleDish(SampleDish(), null!, Region.South, 1));
    }

    private static Dish SampleDish(params DishIngredient[] lines) => new(
        Id: "canh-test",
        Name: "Canh thử",
        Slot: DishSlot.Soup,
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

    private static Dictionary<string, Ingredient> Catalog(params Ingredient[] items) => items.ToDictionary(i => i.Id);

    // ---- properties --------------------------------------------------------------------------

    [Fact]
    public void Scaled_grams_never_shrink_as_servings_grow()
    {
        var random = new SeededRandomSource(31);
        foreach (var kind in Enum.GetValues<ScaleKind>())
        {
            var line = Line("trung", 50, kind);
            var previous = Scaler().Scale(line, Egg, Region.South, 0).GramsEdible;

            for (var servings = 0.25; servings <= 12; servings += 0.25 + random.NextDouble() / 10)
            {
                var current = Scaler().Scale(line, Egg, Region.South, servings).GramsEdible;
                current.ShouldBeGreaterThanOrEqualTo(previous - Tolerance, $"{kind} shrank at {servings} servings");
                previous = current;
            }
        }
    }

    [Fact]
    public void Linear_adds_up_and_sublinear_never_adds_up_to_more_than_one_big_batch()
    {
        var random = new SeededRandomSource(32);
        for (var run = 0; run < 300; run++)
        {
            double a = 0.5 + random.NextDouble() * 6, b = 0.5 + random.NextDouble() * 6;
            double Linear(double s) => Scaler().Scale(Line("x", 12, ScaleKind.Linear), Item("x"), Region.South, s).GramsEdible;
            double Sublinear(double s) => Scaler().Scale(Line("x", 12, ScaleKind.Sublinear), Item("x"), Region.South, s).GramsEdible;

            (Linear(a) + Linear(b)).ShouldBe(Linear(a + b), Tolerance);
            // Cooking two small pots takes more spice than one big pot: S^0.8 is subadditive.
            (Sublinear(a) + Sublinear(b)).ShouldBeGreaterThanOrEqualTo(Sublinear(a + b) - Tolerance);
        }
    }

    [Fact]
    public void Whole_units_cover_the_need_without_a_whole_spare_unit()
    {
        var random = new SeededRandomSource(33);
        for (var run = 0; run < 500; run++)
        {
            var gramsPerUnit = 5 + random.NextInt(200);
            var perServing = 1 + random.NextInt(150);
            var servings = 0.5 + random.NextDouble() * 12;
            var ingredient = Item("x", ("south", "cái", gramsPerUnit));

            var scaled = Scaler().Scale(Line("x", perServing, ScaleKind.Integer), ingredient, Region.South, servings);

            var needed = perServing * servings;
            scaled.GramsEdible.ShouldBeGreaterThanOrEqualTo(needed - Tolerance);
            scaled.GramsEdible.ShouldBeLessThan(needed + gramsPerUnit);
            (scaled.GramsEdible % gramsPerUnit).ShouldBe(0, Tolerance);
            scaled.GramsEdible.ShouldBe(scaled.Units!.Value * (double)gramsPerUnit, Tolerance);
        }
    }

    [Fact]
    public void The_SPEC_sample_dish_scales_to_the_sample_familys_servings()
    {
        // SPEC §7.2 sample: 80 g of cá lóc per serving; SPEC §4.2 family: 3.7875 soup servings.
        var dish = SampleDish(Line("ca-loc", 80, ScaleKind.Linear, "cắt khúc"));
        var family = new Household(Region.South, BowlSize.Medium,
        [
            new Member("Bố", AgeGroup.Adult, 3, [], []),
            new Member("Mẹ", AgeGroup.Adult, 2, [], []),
            new Member("Con", AgeGroup.Child, 1.5, [], []),
            new Member("Bà", AgeGroup.Senior, 1.5, [], []),
        ]);
        var portions = new PortionCalculator(Config).Calculate(family);

        var scaled = Scaler().ScaleDish(dish, Catalog(Item("ca-loc")), family.Region, portions.ServingsFor(dish.Slot));

        scaled.Single().GramsEdible.ShouldBe(303.0, Tolerance);
    }
}
