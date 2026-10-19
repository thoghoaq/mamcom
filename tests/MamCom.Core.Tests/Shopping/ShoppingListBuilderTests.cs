using MamCom.Core.Models;
using MamCom.Core.Portions;
using MamCom.Core.Shopping;
using static MamCom.Core.Tests.Shopping.ShoppingFixtures;

namespace MamCom.Core.Tests.Shopping;

public class ShoppingListBuilderTests
{
    private const double Tolerance = 1e-9;

    private static readonly Household EmptyHome = new(Region.South, BowlSize.Medium, []);

    private static ShoppingItem Single(IReadOnlyList<Dish> dishes, Ingredient ingredient, Region region = Region.South) =>
        Build(region, dishes, TestData.Catalog(ingredient), EmptyHome with { Region = region }).Single();

    // ---- the SPEC example, Bắc and Nam -------------------------------------------------------

    [Fact]
    public void North_buys_in_lang_with_the_example_quantities()
    {
        var items = Build(Region.North);

        // Aisle order, then first use: pork and fish; greens, tomato, taro stem; egg; rice. (Rice is used first but sits in dry goods.)
        items.Select(i => i.IngredientId).ShouldBe(["thit-ba-chi", "ca-loc", "rau-muong", "ca-chua", "bac-ha", "trung", "gao"]);

        var pork = items[0];
        pork.Name.ShouldBe("Thịt ba chỉ");
        pork.Quantity.ShouldBe(3.0);                       // 70 g × 3.7875 = 265.125 g = 2.65 lạng, up to a half lạng
        pork.UnitDisplay.ShouldBe("lạng");
        pork.PurchaseGrams.ShouldBe(265.125, Tolerance);
        pork.ApproxGrams.ShouldBeNull();
        pork.Aisle.ShouldBe(Aisle.MeatAndFish);
    }

    [Fact]
    public void South_buys_the_same_pork_in_grams_under_its_regional_name()
    {
        var pork = Build(Region.South).First(i => i.IngredientId == "thit-ba-chi");

        pork.Name.ShouldBe("Thịt ba rọi");
        pork.UnitDisplay.ShouldBe("g");
        pork.Quantity.ShouldBe(300.0);                     // 265.125 g up to a step of 50
        pork.PurchaseGrams.ShouldBe(265.125, Tolerance);
    }

    [Theory]
    [InlineData(Region.North)]
    [InlineData(Region.South)]
    public void Counted_items_come_out_the_same_in_every_region(Region region)
    {
        var items = Build(region).ToDictionary(i => i.IngredientId);

        items["ca-loc"].Quantity.ShouldBe(1);              // 60 g × 3.7875 / 0.7 = 324.6 g of a 500 g fish
        items["ca-loc"].UnitDisplay.ShouldBe("con");
        items["ca-loc"].PurchaseGrams.ShouldBe(227.25 / 0.7, Tolerance);
        items["rau-muong"].Quantity.ShouldBe(1);           // 94.6875 g / 0.8 = 118 g of a 150 g bunch
        items["ca-chua"].Quantity.ShouldBe(2);             // 151.5 g of 100 g tomatoes
        items["bac-ha"].Quantity.ShouldBe(1);              // 30.3 g of a 50 g bunch
        items["trung"].Quantity.ShouldBe(4);               // 189.375 g needs 4 eggs of 55 g; 4 × 55 = 220 g
        items["trung"].PurchaseGrams.ShouldBe(220.0, Tolerance);
    }

    [Fact]
    public void Central_uses_the_default_unit_where_the_ingredient_has_no_central_key()
    {
        var pork = Item("thit", "Thịt", Aisle.MeatAndFish, new Dictionary<string, MarketUnit>
        {
            ["north"] = Unit("lạng", 100, 0.5),
            [RegionKeys.Default] = Unit("g", 1, 50),
        });

        var item = Single([ExactlyDish("thit", 260)], pork, Region.Central);

        item.UnitDisplay.ShouldBe("g");
        item.Quantity.ShouldBe(300);
    }

    [Fact]
    public void Only_the_unit_that_asks_for_it_carries_approximate_grams()
    {
        var items = Build(Region.South);

        items.Single(i => i.ApproxGrams != null).IngredientId.ShouldBe("ca-loc");
        items.Single(i => i.IngredientId == "ca-loc").ApproxGrams.ShouldBe(500.0);    // 1 fish × 500 g
    }

    [Fact]
    public void Approximate_grams_are_the_rounded_quantity_times_the_unit_size_not_the_need()
    {
        var fish = Item("ca", "Cá", Aisle.MeatAndFish, Everywhere(Unit("con", 400, 1, approx: true)));

        var item = Single([ExactlyDish("ca", 900)], fish);   // 900 g needs 3 fish of 400 g

        item.Quantity.ShouldBe(3);
        item.ApproxGrams.ShouldBe(1200.0);
        item.PurchaseGrams.ShouldBe(900.0);
    }

    // ---- merging and waste -------------------------------------------------------------------

    [Fact]
    public void An_ingredient_used_by_several_dishes_becomes_one_item_with_the_summed_need()
    {
        var gravy = TestData.Dish("nuoc-sot", new DishIngredient("thit-ba-chi", 30, ScaleKind.Linear, 1, null)) with { Slot = DishSlot.Soup };

        var items = Build(Region.South, [BraisedPork, gravy]);

        var pork = items.Single(i => i.IngredientId == "thit-ba-chi");
        pork.PurchaseGrams.ShouldBe(70 * 3.7875 + 30 * 3.7875, Tolerance);   // 378.75 g
        pork.Quantity.ShouldBe(400);                                          // up to a step of 50 g
    }

    [Fact]
    public void Merging_happens_before_rounding_to_market_units()
    {
        // Two lines of 30 g and 30 g: 60 g is 1 unit of 50 g... two separate roundings would give 2.
        var salt = Item("muoi", "Muối", Aisle.DryGoodsAndSpices, Everywhere(Unit("gói", 50)));
        var dish = TestData.Dish("hai-dong", new DishIngredient("muoi", 30, ScaleKind.Fixed, 1, null), new DishIngredient("muoi", 20, ScaleKind.Fixed, 1, null));

        Single([dish], salt).Quantity.ShouldBe(1);
    }

    [Fact]
    public void Waste_raises_the_amount_to_buy_after_merging()
    {
        var item = Single([ExactlyDish("rau-muong", 80)], WaterSpinach);

        item.PurchaseGrams.ShouldBe(100.0, Tolerance);       // 80 g edible / (1 - 0.2)
        item.Quantity.ShouldBe(1);
    }

    [Fact]
    public void An_ingredient_with_a_great_deal_of_waste_scales_up_a_lot()
    {
        var shell = Item("ghe", "Ghẹ", Aisle.MeatAndFish, Everywhere(Unit("g", 1, 100)), waste: 0.75);

        var item = Single([ExactlyDish("ghe", 100)], shell);

        item.PurchaseGrams.ShouldBe(400.0, Tolerance);       // 100 g edible is a quarter of what is bought
        item.Quantity.ShouldBe(400);
    }

    // ---- rounding to the step ----------------------------------------------------------------

    [Theory]
    [InlineData(250.0, 2.5)]       // exactly 2.5 lạng stays
    [InlineData(250.0001, 3.0)]    // a hair over goes up one step
    [InlineData(1.0, 0.5)]         // 0.01 lạng still costs a half lạng
    [InlineData(300.0, 3.0)]
    [InlineData(301.0, 3.5)]
    public void Weighed_units_round_up_to_their_step(double needGrams, double expectedLang)
    {
        var meat = Item("thit", "Thịt", Aisle.MeatAndFish, Everywhere(Unit("lạng", 100, 0.5)));

        Single([ExactlyDish("thit", needGrams)], meat).Quantity.ShouldBe(expectedLang, Tolerance);
    }

    [Theory]
    [InlineData(300.0, 300.0)]
    [InlineData(301.0, 350.0)]
    [InlineData(49.0, 50.0)]
    public void Grams_round_up_to_a_step_of_fifty(double needGrams, double expectedGrams)
    {
        var meat = Item("thit", "Thịt", Aisle.MeatAndFish, Everywhere(Unit("g", 1, 50)));

        Single([ExactlyDish("thit", needGrams)], meat).Quantity.ShouldBe(expectedGrams, Tolerance);
    }

    [Theory]
    [InlineData(100.0, 1)]
    [InlineData(100.5, 2)]
    [InlineData(199.9, 2)]
    [InlineData(200.0, 2)]
    [InlineData(200.1, 3)]
    public void Counted_units_round_up_to_whole_units(double needGrams, double expectedCount)
    {
        Single([ExactlyDish("ca-chua", needGrams)], Tomato).Quantity.ShouldBe(expectedCount);
    }

    [Fact]
    public void Floating_point_noise_just_above_a_whole_unit_does_not_buy_an_extra_one()
    {
        // 2.1 g in units of 0.3 g is exactly 7 units, but 2.1 / 0.3 is 7.000000000000001 in binary,
        // so a plain ceil would buy 8.
        (2.1 / 0.3).ShouldBeGreaterThan(7.0);
        var tiny = Item("hat", "Hạt", Aisle.Other, Everywhere(Unit("hạt", 0.3)));

        Single([ExactlyDish("hat", 2.1)], tiny).Quantity.ShouldBe(7.0, Tolerance);
    }

    [Fact]
    public void Floating_point_noise_just_above_a_whole_step_does_not_buy_an_extra_step()
    {
        // 2.1 g in units of 1 g with a step of 0.3: exactly 7 steps, but 2.1 / 0.3 is 7.000000000000001.
        var item = Item("x", "X", Aisle.Other, Everywhere(Unit("phần", 1, 0.3)));

        Single([ExactlyDish("x", 2.1)], item).Quantity.ShouldBe(2.1, Tolerance);
    }

    [Fact]
    public void Quantities_are_clean_multiples_of_the_step_without_binary_noise()
    {
        var item = Item("x", "X", Aisle.Other, Everywhere(Unit("phần", 10, 0.1)));

        var result = Single([ExactlyDish("x", 3)], item);

        // Exactly 0.3, not 0.30000000000000004 (3 × 0.1 in binary). Shouldly's double comparison is lenient,
        // so compare with == and show the full-precision value on failure.
        (3 * 0.1 == 0.3).ShouldBeFalse();
        (result.Quantity == 0.3).ShouldBeTrue($"quantity was {result.Quantity:R}");
    }

    // ---- rice and pantry ---------------------------------------------------------------------

    [Fact]
    public void Rice_is_on_the_list_from_the_portion_result_and_flagged_as_pantry()
    {
        var rice = Build(Region.South).Single(i => i.IngredientId == "gao");

        rice.PurchaseGrams.ShouldBe(320.0);                  // SPEC §4.2: 8 bowls × 40 g
        rice.Quantity.ShouldBe(0.5);                         // 0.32 kg up to a half kg
        rice.UnitDisplay.ShouldBe("kg");
        rice.IsPantry.ShouldBeTrue();
    }

    [Fact]
    public void Rice_merges_with_a_dish_that_also_uses_it()
    {
        var friedRice = TestData.Dish("com-chien", new DishIngredient("gao", 100, ScaleKind.Fixed, 1, null));

        var rice = Build(Region.South, [friedRice]).Single(i => i.IngredientId == "gao");

        rice.PurchaseGrams.ShouldBe(420.0);                  // 320 g for the bowls + 100 g in the dish
        rice.Quantity.ShouldBe(0.5);                         // 0.42 kg still fits the half-kg step
    }

    [Fact]
    public void A_household_without_members_needs_no_rice_and_does_not_need_the_rice_ingredient()
    {
        var items = Build(Region.South, dishes: [], catalog: TestData.Catalog(), household: EmptyHome);

        items.ShouldBeEmpty();
    }

    [Fact]
    public void Pantry_items_are_in_the_full_list_and_hidden_by_the_aisle_grouping_unless_asked()
    {
        var items = Build(Region.South);

        items.Count(i => i.IsPantry).ShouldBe(1);
        ShoppingGroups.ByAisle(items, includePantry: false).SelectMany(g => g.Items).ShouldNotContain(i => i.IsPantry);
        ShoppingGroups.ByAisle(items, includePantry: true).SelectMany(g => g.Items).Count().ShouldBe(items.Count);
    }

    // ---- aisles ------------------------------------------------------------------------------

    [Fact]
    public void Items_are_ordered_by_aisle_and_by_first_use_inside_an_aisle()
    {
        // First use: rice, pork, egg (BraisedPork), greens, fish, tomato, taro stem (SourSoup).
        var items = Build(Region.South);

        items.Select(i => i.Aisle).ShouldBe(items.Select(i => i.Aisle).Order());
        items.Where(i => i.Aisle == Aisle.Produce).Select(i => i.IngredientId).ShouldBe(["rau-muong", "ca-chua", "bac-ha"]);
        items.Where(i => i.Aisle == Aisle.MeatAndFish).Select(i => i.IngredientId).ShouldBe(["thit-ba-chi", "ca-loc"]);
    }

    [Fact]
    public void The_order_of_the_dishes_decides_the_order_inside_an_aisle()
    {
        var soupFirst = Build(Region.South, [SourSoup, BraisedPork, StirFriedGreens]);

        soupFirst.Where(i => i.Aisle == Aisle.Produce).Select(i => i.IngredientId).ShouldBe(["ca-chua", "bac-ha", "rau-muong"]);
        soupFirst.Where(i => i.Aisle == Aisle.MeatAndFish).Select(i => i.IngredientId).ShouldBe(["ca-loc", "thit-ba-chi"]);
    }

    [Fact]
    public void Grouping_by_aisle_follows_the_SPEC_order_and_skips_empty_aisles()
    {
        var groups = ShoppingGroups.ByAisle(Build(Region.South), includePantry: true);

        groups.Select(g => g.Aisle).ShouldBe([Aisle.MeatAndFish, Aisle.Produce, Aisle.EggsAndTofu, Aisle.DryGoodsAndSpices]);
    }

    // ---- nothing to buy ----------------------------------------------------------------------

    [Fact]
    public void An_ingredient_that_needs_nothing_is_left_out()
    {
        var items = Build(Region.South, [ExactlyDish("ca-chua", 0)], household: EmptyHome);

        items.ShouldBeEmpty();
    }

    // ---- data errors -------------------------------------------------------------------------

    [Fact]
    public void An_ingredient_without_a_unit_for_the_region_is_a_data_error()
    {
        var northOnly = Item("thit", "Thịt", Aisle.MeatAndFish, new Dictionary<string, MarketUnit> { ["north"] = Unit("lạng", 100, 0.5) });

        var error = Should.Throw<InvalidOperationException>(() => Single([ExactlyDish("thit", 100)], northOnly, Region.South));

        error.Message.ShouldContain("thit");
        error.Message.ShouldContain("south");
    }

    [Theory]
    [InlineData(0.0, 50.0)]
    [InlineData(-1.0, 50.0)]
    [InlineData(double.NaN, 50.0)]
    [InlineData(100.0, 0.0)]
    [InlineData(100.0, -1.0)]
    [InlineData(100.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void A_market_unit_with_a_non_positive_size_or_step_is_a_data_error(double gramsPerUnit, double step)
    {
        var bad = Item("thit", "Thịt", Aisle.MeatAndFish, Everywhere(new MarketUnit("lạng", gramsPerUnit, step)));

        Should.Throw<InvalidOperationException>(() => Single([ExactlyDish("thit", 100)], bad));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    public void A_waste_ratio_outside_zero_to_below_one_is_a_data_error(double waste)
    {
        var bad = Item("rau", "Rau", Aisle.Produce, Everywhere(Unit("mớ", 100)), waste: waste);

        Should.Throw<InvalidOperationException>(() => Single([ExactlyDish("rau", 100)], bad));
    }

    [Fact]
    public void A_dish_with_an_unknown_ingredient_is_a_data_error()
    {
        Should.Throw<InvalidOperationException>(() => Build(Region.South, [ExactlyDish("khong-co", 10)]));
    }

    [Fact]
    public void Missing_rice_ingredient_is_a_data_error_when_there_is_rice_to_buy()
    {
        var withoutRice = TestData.Catalog(PorkBelly, SnakeheadFish, WaterSpinach, Tomato, TaroStem, Egg);

        var error = Should.Throw<InvalidOperationException>(() => Build(Region.South, catalog: withoutRice));

        error.Message.ShouldContain("gao");
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var household = Family(Region.South);
        var portions = new PortionCalculator(PortionSettings).Calculate(household);
        var catalog = Catalog();

        Should.Throw<ArgumentNullException>(() => new ShoppingListBuilder(null!));
        Should.Throw<ArgumentNullException>(() => Builder().Build(null!, portions, Tray, catalog));
        Should.Throw<ArgumentNullException>(() => Builder().Build(household, null!, Tray, catalog));
        Should.Throw<ArgumentNullException>(() => Builder().Build(household, portions, null!, catalog));
        Should.Throw<ArgumentNullException>(() => Builder().Build(household, portions, Tray, null!));
        Should.Throw<ArgumentNullException>(() => ShoppingGroups.ByAisle(null!, true));
    }

    [Fact]
    public void An_unusable_portion_config_is_rejected_when_the_builder_is_created()
    {
        Should.Throw<ArgumentException>(() => new ShoppingListBuilder(PortionSettings with { RiceRoundingGrams = 0 }));
    }

    // ---- properties --------------------------------------------------------------------------

    [Fact]
    public void Every_item_covers_the_need_without_a_whole_spare_step_and_ids_are_unique()
    {
        var random = new SeededRandomSource(15);
        var catalog = Catalog();

        for (var run = 0; run < 200; run++)
        {
            var members = Enumerable.Range(0, 1 + random.NextInt(6))
                .Select(_ => TestData.Person("Người", Enum.GetValues<AgeGroup>()[random.NextInt(5)], 0.5 * (1 + random.NextInt(10))))
                .ToArray();
            var region = random.NextInt(2) == 0 ? Region.North : Region.South; // the fixture pork has no central unit
            var household = new Household(region, Enum.GetValues<BowlSize>()[random.NextInt(3)], members);
            var dishes = Tray.OrderBy(_ => random.NextInt(1000)).Take(1 + random.NextInt(3)).ToArray();

            var items = Build(region, dishes, catalog, household);

            items.Select(i => i.IngredientId).Distinct().Count().ShouldBe(items.Count);
            foreach (var item in items)
            {
                var unit = catalog[item.IngredientId].MarketUnitFor(region)!;
                var bought = item.Quantity * unit.GramsPerUnit;
                bought.ShouldBeGreaterThanOrEqualTo(item.PurchaseGrams - Tolerance);
                bought.ShouldBeLessThan(item.PurchaseGrams + unit.Step * unit.GramsPerUnit);
                (item.Quantity / unit.Step).ShouldBe(Math.Round(item.Quantity / unit.Step), 1e-6);
            }
        }
    }

    [Fact]
    public void Each_dish_is_scaled_to_the_servings_of_its_own_slot()
    {
        // Father eats more protein and Mother more vegetables, so soup, main and vegetable totals all differ.
        var household = new Household(Region.South, BowlSize.Medium,
        [
            TestData.Person("Bố", AgeGroup.Adult, 3, MemberTag.MoreProtein),
            TestData.Person("Mẹ", AgeGroup.Adult, 2, MemberTag.MoreVeg),
        ]);
        var portions = new PortionCalculator(PortionSettings).Calculate(household);
        portions.ProteinServings.ShouldNotBe(portions.BaseServings);
        portions.VegServings.ShouldNotBe(portions.BaseServings);
        portions.ProteinServings.ShouldNotBe(portions.VegServings);

        var catalog = TestData.Catalog(
            Item("a", "A", Aisle.Other, Everywhere(Unit("g", 1))),
            Item("b", "B", Aisle.Other, Everywhere(Unit("g", 1))),
            Item("c", "C", Aisle.Other, Everywhere(Unit("g", 1))),
            Item("gao", "Gạo", Aisle.Other, Everywhere(Unit("g", 1))));
        var dishes = new[]
        {
            TestData.Dish("main", TestData.Line("a", 10)) with { Slot = DishSlot.Main },
            TestData.Dish("veg", TestData.Line("b", 10)) with { Slot = DishSlot.Vegetable },
            TestData.Dish("soup", TestData.Line("c", 10)) with { Slot = DishSlot.Soup },
        };

        var items = Builder().Build(household, portions, dishes, catalog).ToDictionary(i => i.IngredientId);

        items["a"].PurchaseGrams.ShouldBe(10 * portions.ProteinServings, Tolerance);
        items["b"].PurchaseGrams.ShouldBe(10 * portions.VegServings, Tolerance);
        items["c"].PurchaseGrams.ShouldBe(10 * portions.BaseServings, Tolerance);
    }

    [Fact]
    public void The_edible_grams_of_the_list_equal_what_the_recipes_and_rice_need()
    {
        // With zero waste the grams to buy are exactly the scaled recipe lines plus the rice, split across merged items.
        var catalog = TestData.Catalog(
            Item("a", "A", Aisle.Other, Everywhere(Unit("g", 1))),
            Item("b", "B", Aisle.Other, Everywhere(Unit("g", 1))),
            Item("gao", "Gạo", Aisle.Other, Everywhere(Unit("g", 1))));
        var dishes = new[]
        {
            TestData.Dish("m1", TestData.Line("a", 10), TestData.Line("b", 20)) with { Slot = DishSlot.Main },
            TestData.Dish("m2", TestData.Line("a", 5)) with { Slot = DishSlot.Soup },
        };
        var household = Family(Region.South);
        var portions = new PortionCalculator(PortionSettings).Calculate(household);

        var items = Builder().Build(household, portions, dishes, catalog).ToDictionary(i => i.IngredientId);

        items["a"].PurchaseGrams.ShouldBe(10 * portions.ProteinServings + 5 * portions.BaseServings, Tolerance);
        items["b"].PurchaseGrams.ShouldBe(20 * portions.ProteinServings, Tolerance);
        items["gao"].PurchaseGrams.ShouldBe(portions.RawRiceGrams, Tolerance);
    }
}
