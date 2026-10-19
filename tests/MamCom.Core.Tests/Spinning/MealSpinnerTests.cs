using MamCom.Core.Models;
using MamCom.Core.Spinning;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Spinning;

public class MealSpinnerTests
{
    private static string[] Kinds(SpinResult result) => result.Tray.Slots.Select(s => s.Slot.ToString()).ToArray();

    // ---- the frame of the tray (SPEC §5.1) ---------------------------------------------------

    [Fact]
    public void A_small_household_gets_soup_main_and_vegetable()
    {
        var result = Spinner().Spin(Context(SmallHome()));

        result.Tray.Slots.Select(s => s.Slot).ShouldBe([DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable]);
        result.UnfilledSlots.ShouldBeEmpty();
        result.Tray.Date.ShouldBe(Today);
    }

    [Fact]
    public void At_the_threshold_of_five_base_servings_the_tray_gets_a_second_main()
    {
        // Five adults at two bowls: (0.4 + 0.3 × 2) × 1 each = exactly 5.0.
        var result = Spinner().Spin(Context(BigHome()));

        result.Tray.Slots.Select(s => s.Slot).ShouldBe([DishSlot.Soup, DishSlot.Main, DishSlot.Main, DishSlot.Vegetable]);
        result.Tray.Slots.Select(s => s.DishId).Distinct().Count().ShouldBe(4);
    }

    [Fact]
    public void The_threshold_does_not_depend_on_the_order_of_the_members()
    {
        // These five adults total exactly 5.0 base servings. Summed in this order the doubles come to
        // 4.999999999999999, which a raw comparison with 5 would read as "below the threshold".
        Member Adult(double bowls) => TestData.Person("A", AgeGroup.Adult, bowls);
        var unluckyOrder = SmallHome(members: [Adult(1), Adult(2.5), Adult(3.5), Adult(1.5), Adult(1.5)]);
        var luckyOrder = SmallHome(members: [Adult(1.5), Adult(1.5), Adult(1), Adult(2.5), Adult(3.5)]);
        new MamCom.Core.Portions.PortionCalculator(PortionSettings).Calculate(unluckyOrder).BaseServings.ShouldNotBe(5.0);   // the premise

        Spinner().Spin(Context(unluckyOrder)).Tray.Slots.Count(s => s.Slot == DishSlot.Main).ShouldBe(2);
        Spinner().Spin(Context(luckyOrder)).Tray.Slots.Count(s => s.Slot == DishSlot.Main).ShouldBe(2);
    }

    [Fact]
    public void Just_below_the_threshold_there_is_only_one_main()
    {
        var fourAdults = SmallHome(members: Enumerable.Range(0, 4).Select(i => Person($"P{i}")).ToArray());

        Spinner().Spin(Context(fourAdults)).Tray.Slots.Count(s => s.Slot == DishSlot.Main).ShouldBe(1);
    }

    [Fact]
    public void The_threshold_comes_from_the_config()
    {
        var config = Config with { SecondMainMinBaseServings = 2.0 };   // two adults at two bowls = 2.0

        Spinner(config: config).Spin(Context(SmallHome())).Tray.Slots.Count(s => s.Slot == DishSlot.Main).ShouldBe(2);
        Spinner(config: Config with { SecondMainMinBaseServings = 2.01 }).Spin(Context(SmallHome()))
            .Tray.Slots.Count(s => s.Slot == DishSlot.Main).ShouldBe(1);
    }

    [Fact]
    public void An_empty_household_still_gets_a_single_main_tray()
    {
        var result = Spinner().Spin(Context(new Household(Region.South, BowlSize.Medium, [])));

        result.Tray.Slots.Count.ShouldBe(3);
    }

    // ---- hard rules (SPEC §5.2) --------------------------------------------------------------

    [Fact]
    public void A_dish_with_an_avoided_ingredient_id_is_not_eligible()
    {
        var home = SmallHome(members: [Person("A"), new Member("B", AgeGroup.Adult, 2, [], ["ca-chua"])]);
        var context = Context(home);
        var spinner = Spinner();

        spinner.IsEligible(Find("canh-chua-ca"), context).ShouldBeFalse();     // contains ca-chua
        spinner.IsEligible(Find("dau-phu-sot-ca"), context).ShouldBeFalse();
        spinner.IsEligible(Find("canh-bi-do"), context).ShouldBeTrue();
    }

    [Fact]
    public void A_dish_with_an_ingredient_of_an_avoided_category_is_not_eligible()
    {
        var home = SmallHome(members: [new Member("A", AgeGroup.Adult, 2, [], ["hai-san"])]);
        var context = Context(home);
        var spinner = Spinner();

        spinner.IsEligible(Find("tom-rang"), context).ShouldBeFalse();         // tom, hai-san
        spinner.IsEligible(Find("muc-xao-cay"), context).ShouldBeFalse();      // muc, hai-san
        spinner.IsEligible(Find("canh-khoai-mo-tom"), context).ShouldBeFalse();
        spinner.IsEligible(Find("ca-chien"), context).ShouldBeTrue();          // fish is category "ca", not "hai-san"
    }

    [Fact]
    public void One_members_avoid_list_is_enough_to_drop_a_dish_for_everyone()
    {
        var home = SmallHome(members:
        [
            Person("A"),
            new Member("B", AgeGroup.Adult, 2, [], ["bo"]),
            new Member("C", AgeGroup.Adult, 2, [], ["tom"]),
        ]);
        var context = Context(home);
        var spinner = Spinner();

        spinner.IsEligible(Find("thit-bo-xao"), context).ShouldBeFalse();      // B avoids beef
        spinner.IsEligible(Find("tom-rang"), context).ShouldBeFalse();         // C avoids prawns
        spinner.IsEligible(Find("ga-ran"), context).ShouldBeTrue();
    }

    [Fact]
    public void Avoid_matching_is_exact_not_a_substring()
    {
        var home = SmallHome(members: [new Member("A", AgeGroup.Adult, 2, [], ["thit"])]);

        Spinner().IsEligible(Find("thit-kho-trung"), Context(home)).ShouldBeTrue();   // "thit" is neither an id nor a category here
    }

    [Fact]
    public void No_spicy_drops_dishes_that_need_chilli_but_keeps_those_where_it_is_optional()
    {
        var home = SmallHome(members: [Person("A"), Person("B", MemberTag.NoSpicy)]);
        var context = Context(home);
        var spinner = Spinner();

        spinner.IsEligible(Find("muc-xao-cay"), context).ShouldBeFalse();          // required
        spinner.IsEligible(Find("rau-cai-xao-ot"), context).ShouldBeFalse();       // required
        spinner.IsEligible(Find("canh-chua-ca"), context).ShouldBeTrue();          // optional
        spinner.IsEligible(Find("canh-bi-do"), context).ShouldBeTrue();            // none
    }

    [Fact]
    public void Without_no_spicy_required_chilli_is_fine()
    {
        Spinner().IsEligible(Find("muc-xao-cay"), Context(SmallHome())).ShouldBeTrue();
    }

    [Fact]
    public void A_blocked_dish_is_not_eligible()
    {
        var context = Context(SmallHome(), blocked: ["ga-ran"]);

        Spinner().IsEligible(Find("ga-ran"), context).ShouldBeFalse();
        Spinner().IsEligible(Find("ga-kho-gung"), context).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, false)]    // eaten today
    [InlineData(1, false)]
    [InlineData(2, false)]    // "d ≤ 2 → loại"
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    [InlineData(30, true)]
    [InlineData(-1, false)]   // a date in the future (clock skew) counts as just eaten
    public void A_dish_eaten_within_two_days_is_not_eligible(int daysAgo, bool eligible)
    {
        var eaten = new Dictionary<string, DateOnly> { ["ga-ran"] = Today.AddDays(-daysAgo) };

        Spinner().IsEligible(Find("ga-ran"), Context(SmallHome(), lastEaten: eaten)).ShouldBe(eligible);
    }

    [Fact]
    public void A_dish_never_eaten_is_eligible_and_history_of_unknown_dishes_is_ignored()
    {
        var eaten = new Dictionary<string, DateOnly> { ["khong-co-trong-danh-muc"] = Today };

        Spinner().IsEligible(Find("ga-ran"), Context(SmallHome(), lastEaten: eaten)).ShouldBeTrue();
        Should.NotThrow(() => Spinner().Spin(Context(SmallHome(), lastEaten: eaten)));
    }

    [Fact]
    public void Eligibility_needs_every_ingredient_to_be_known()
    {
        var broken = TestData.Dish("hong", TestData.Line("khong-co", 10)) with { Slot = DishSlot.Main };

        Should.Throw<InvalidOperationException>(() => Spinner().IsEligible(broken, Context(SmallHome())))
            .Message.ShouldContain("khong-co");
    }

    [Fact]
    public void No_drawn_dish_ever_breaks_a_hard_rule_in_a_realistic_case()
    {
        var home = SmallHome(members:
        [
            Person("A", MemberTag.NoSpicy),
            new Member("B", AgeGroup.Adult, 2, [], ["hai-san", "bo"]),
        ]);
        var eaten = new Dictionary<string, DateOnly> { ["thit-kho-trung"] = Today.AddDays(-1), ["ga-ran"] = Today };
        var context = Context(home, blocked: ["ca-kho-to"], lastEaten: eaten);
        var forbidden = new[] { "tom-rang", "muc-xao-cay", "thit-bo-xao", "canh-khoai-mo-tom", "canh-cai-thit-bo", "canh-chua-tom", "rau-cai-xao-ot", "ca-kho-to", "thit-kho-trung", "ga-ran" };

        for (var seed = 0UL; seed < 300; seed++)
        {
            var result = Spinner(new SeededRandomSource(seed)).Spin(context);

            Ids(result).ShouldAllBe(id => !forbidden.Contains(id));
        }
    }

    // ---- soft weights (SPEC §5.4) ------------------------------------------------------------

    [Fact]
    public void Weight_is_one_when_nothing_applies()
    {
        Spinner().WeightOf(Find("canh-bi-do"), Context(SmallHome())).ShouldBe(1.0);
    }

    [Theory]
    [InlineData(3, 0.3)]
    [InlineData(6, 0.3)]
    [InlineData(7, 1.0)]
    [InlineData(20, 1.0)]
    public void Recently_eaten_dishes_weigh_less_until_a_week_has_passed(int daysAgo, double expected)
    {
        var eaten = new Dictionary<string, DateOnly> { ["canh-bi-do"] = Today.AddDays(-daysAgo) };

        Spinner().WeightOf(Find("canh-bi-do"), Context(SmallHome(), lastEaten: eaten)).ShouldBe(expected, 1e-12);
    }

    [Theory]
    [InlineData(DishPreference.Liked, 2.0)]
    [InlineData(DishPreference.Neutral, 1.0)]
    [InlineData(DishPreference.Disliked, 0.2)]
    public void Preference_multiplies_the_weight(DishPreference preference, double expected)
    {
        var prefs = new Dictionary<string, DishPreference> { ["canh-bi-do"] = preference };

        Spinner().WeightOf(Find("canh-bi-do"), Context(SmallHome(), preferences: prefs)).ShouldBe(expected, 1e-12);
    }

    [Fact]
    public void A_dish_for_the_current_season_weighs_more_and_the_id_all_is_not_a_match()
    {
        // Today is in October, "autumn" in the shipped season map. suon-nuong lists autumn; canh-bi-do lists "all".
        var spinner = Spinner();
        var context = Context(SmallHome());

        spinner.WeightOf(Find("suon-nuong"), context).ShouldBe(1.3, 1e-12);
        spinner.WeightOf(Find("canh-bi-do"), context).ShouldBe(1.0, 1e-12);
        spinner.WeightOf(Find("canh-chua-ca"), Context(SmallHome(Region.North))).ShouldBe(1.0, 1e-12);   // summer dish in autumn, no penalty
    }

    [Theory]
    [InlineData(1, "winter")]
    [InlineData(3, "spring")]
    [InlineData(5, "spring")]
    [InlineData(6, "summer")]
    [InlineData(8, "summer")]
    [InlineData(9, "autumn")]
    [InlineData(11, "autumn")]
    [InlineData(12, "winter")]
    public void The_season_follows_the_month_through_the_clock(int month, string season)
    {
        var dish = Find("canh-bi-do") with { Seasons = [season] };
        var spinner = Spinner(today: new DateOnly(2026, month, 15));

        spinner.WeightOf(dish, Context(SmallHome())).ShouldBe(1.3, 1e-12);
    }

    [Fact]
    public void A_dish_typical_of_the_households_region_weighs_more()
    {
        var spinner = Spinner();

        spinner.WeightOf(Find("ca-kho-to"), Context(SmallHome(Region.South))).ShouldBe(1.2, 1e-12);
        spinner.WeightOf(Find("ca-kho-to"), Context(SmallHome(Region.North))).ShouldBe(1.0, 1e-12);
        spinner.WeightOf(Find("canh-bi-do"), Context(SmallHome(Region.South))).ShouldBe(1.0, 1e-12);   // no region listed
    }

    [Fact]
    public void The_four_factors_multiply()
    {
        var dish = Find("canh-bi-do") with { Seasons = ["autumn"], Regions = [Region.South] };
        var eaten = new Dictionary<string, DateOnly> { [dish.Id] = Today.AddDays(-4) };
        var prefs = new Dictionary<string, DishPreference> { [dish.Id] = DishPreference.Liked };

        var weight = Spinner().WeightOf(dish, Context(SmallHome(Region.South), lastEaten: eaten, preferences: prefs));

        weight.ShouldBe(0.3 * 2.0 * 1.3 * 1.2, 1e-12);
    }

    [Fact]
    public void The_weight_constants_come_from_the_config()
    {
        var config = Config with
        {
            Recency = new RecencyConfig(1, 4, 0.5),
            Preference = new PreferenceWeights(3, 1.5, 0.1),
            SeasonMatchWeight = 2.5,
            RegionMatchWeight = 1.7,
        };
        var dish = Find("canh-bi-do") with { Seasons = ["autumn"], Regions = [Region.South] };
        var eaten = new Dictionary<string, DateOnly> { [dish.Id] = Today.AddDays(-3) };
        var prefs = new Dictionary<string, DishPreference> { [dish.Id] = DishPreference.Liked };

        Spinner(config: config).WeightOf(dish, Context(SmallHome(Region.South), lastEaten: eaten, preferences: prefs))
            .ShouldBe(0.5 * 3 * 2.5 * 1.7, 1e-12);
        Spinner(config: config).IsEligible(dish, Context(SmallHome(), lastEaten: new Dictionary<string, DateOnly> { [dish.Id] = Today.AddDays(-1) }))
            .ShouldBeFalse();
        Spinner(config: config).IsEligible(dish, Context(SmallHome(), lastEaten: new Dictionary<string, DateOnly> { [dish.Id] = Today.AddDays(-2) }))
            .ShouldBeTrue();
    }

    // How often a dish is drawn follows its weight. Two mains, one soup and one vegetable keep the draw simple.
    private static double ShareOfFirstMain(SpinContext context, int spins, string firstMainId)
    {
        var spinner = Spinner(new SeededRandomSource(2026));
        var first = 0;
        for (var i = 0; i < spins; i++)
        {
            if (spinner.Spin(context).Tray.Slots.Single(s => s.Slot == DishSlot.Main).DishId == firstMainId)
            {
                first++;
            }
        }

        return (double)first / spins;
    }

    private static SpinContext TwoMainsContext(
        Dish a,
        Dish b,
        IReadOnlyDictionary<string, DateOnly>? eaten = null,
        IReadOnlyDictionary<string, DishPreference>? prefs = null,
        Household? home = null) =>
        Context(home ?? SmallHome(), [Find("canh-bi-do"), a, b, Find("cai-luoc")], lastEaten: eaten, preferences: prefs);

    [Fact]
    public void A_liked_dish_is_drawn_twice_as_often_as_a_neutral_one()
    {
        var context = TwoMainsContext(Find("ga-kho-gung"), Find("thit-kho-trung"),
            prefs: new Dictionary<string, DishPreference> { ["ga-kho-gung"] = DishPreference.Liked });

        ShareOfFirstMain(context, 20_000, "ga-kho-gung").ShouldBe(2.0 / 3.0, 0.02);
    }

    [Fact]
    public void A_disliked_dish_is_drawn_one_fifth_as_often()
    {
        var context = TwoMainsContext(Find("ga-kho-gung"), Find("thit-kho-trung"),
            prefs: new Dictionary<string, DishPreference> { ["ga-kho-gung"] = DishPreference.Disliked });

        ShareOfFirstMain(context, 20_000, "ga-kho-gung").ShouldBe(0.2 / 1.2, 0.02);
    }

    [Fact]
    public void A_dish_eaten_four_days_ago_is_drawn_less_often()
    {
        var context = TwoMainsContext(Find("ga-kho-gung"), Find("thit-kho-trung"),
            eaten: new Dictionary<string, DateOnly> { ["ga-kho-gung"] = Today.AddDays(-4) });

        ShareOfFirstMain(context, 20_000, "ga-kho-gung").ShouldBe(0.3 / 1.3, 0.02);
    }

    [Fact]
    public void A_seasonal_dish_is_drawn_more_often_in_its_season()
    {
        var context = TwoMainsContext(Find("suon-nuong"), Find("thit-kho-trung"));   // suon-nuong lists autumn

        ShareOfFirstMain(context, 20_000, "suon-nuong").ShouldBe(1.3 / 2.3, 0.02);
    }

    [Fact]
    public void A_regional_dish_is_drawn_more_often_in_its_region()
    {
        var context = TwoMainsContext(Find("ca-kho-to"), Find("thit-kho-trung"), home: SmallHome(Region.South));

        ShareOfFirstMain(context, 20_000, "ca-kho-to").ShouldBe(1.2 / 2.2, 0.02);
    }

    // ---- order of the draw (SPEC §5.5 step 1) ------------------------------------------------

    [Fact]
    public void The_slot_with_the_fewest_candidates_is_drawn_first_and_each_pick_uses_the_cumulative_weights()
    {
        // Soup: 3 candidates, Main: 2, Vegetable: 4. The first random number goes to Main, the next to Soup, then Vegetable.
        var dishes = new[]
        {
            Find("canh-bi-do"), Find("canh-bi-xanh"), Find("canh-dau-ca-chua"),            // soups, by id: canh-bi-do, canh-bi-xanh, canh-dau-ca-chua
            Find("ga-kho-gung"), Find("thit-kho-trung"),                                    // mains, by id: ga-kho-gung, thit-kho-trung
            Find("bi-xao-trung"), Find("ca-chua-tron"), Find("cai-luoc"), Find("rau-muong-xao-toi"),
        };
        var random = new ScriptedRandom(0.99, 0.0, 0.0);

        var result = Spinner(random).Spin(Context(SmallHome(), dishes));

        result.Tray.Slots.Select(s => (s.Slot, s.DishId)).ShouldBe(
        [
            (DishSlot.Soup, "canh-bi-do"),            // second draw, value 0.0: first candidate
            (DishSlot.Main, "thit-kho-trung"),        // first draw, value 0.99: last of two
            (DishSlot.Vegetable, "bi-xao-trung"),     // third draw, value 0.0: first candidate
        ]);
    }

    [Fact]
    public void A_random_value_picks_the_candidate_whose_cumulative_weight_it_falls_into()
    {
        // Two mains by id: ga-kho-gung (liked, weight 2) then thit-kho-trung (weight 1); total 3.
        var prefs = new Dictionary<string, DishPreference> { ["ga-kho-gung"] = DishPreference.Liked };
        var context = TwoMainsContext(Find("ga-kho-gung"), Find("thit-kho-trung"), prefs: prefs);
        // Soup and vegetable have one candidate each, so the main (two candidates) is drawn last: its value is the third.
        string MainFor(double r) => Spinner(new ScriptedRandom(0.0, 0.0, r)).Spin(context).Tray.Slots.Single(s => s.Slot == DishSlot.Main).DishId;

        MainFor(0.0).ShouldBe("ga-kho-gung");
        MainFor(0.66).ShouldBe("ga-kho-gung");     // 0.66 × 3 = 1.98 < 2
        MainFor(0.67).ShouldBe("thit-kho-trung");  // 0.67 × 3 = 2.01 >= 2
        MainFor(0.9999).ShouldBe("thit-kho-trung");
    }

    [Fact]
    public void A_random_value_exactly_on_a_boundary_goes_to_the_next_candidate()
    {
        // Two neutral mains: 0.5 × 2 = 1.0 is exactly the end of the first candidate's share, which belongs to the second.
        var context = TwoMainsContext(Find("ga-kho-gung"), Find("thit-kho-trung"));
        string MainFor(double r) => Spinner(new ScriptedRandom(0.0, 0.0, r)).Spin(context).Tray.Slots.Single(s => s.Slot == DishSlot.Main).DishId;

        MainFor(0.4999999999).ShouldBe("ga-kho-gung");
        MainFor(0.5).ShouldBe("thit-kho-trung");
    }

    [Fact]
    public void The_result_does_not_depend_on_the_order_of_the_catalog()
    {
        var forward = Catalog();
        var reversed = Catalog().Reverse().ToArray();

        for (var seed = 0UL; seed < 100; seed++)
        {
            var a = Spinner(new SeededRandomSource(seed)).Spin(Context(SmallHome(), forward));
            var b = Spinner(new SeededRandomSource(seed)).Spin(Context(SmallHome(), reversed));

            Ids(b).ShouldBe(Ids(a));
        }
    }

    // ---- reproducibility (SPEC §5.5 step 7) --------------------------------------------------

    [Fact]
    public void The_same_seed_and_input_give_the_same_tray()
    {
        for (var seed = 0UL; seed < 200; seed++)
        {
            var first = Spinner(new SeededRandomSource(seed)).Spin(Context(BigHome()));
            var second = Spinner(new SeededRandomSource(seed)).Spin(Context(BigHome()));

            Ids(second).ShouldBe(Ids(first));
            second.RelaxedRules.ShouldBe(first.RelaxedRules);
            second.UnfilledSlots.ShouldBe(first.UnfilledSlots);
        }
    }

    [Fact]
    public void Different_seeds_give_different_trays()
    {
        var distinct = Enumerable.Range(0, 60)
            .Select(seed => string.Join("|", Ids(Spinner(new SeededRandomSource((ulong)seed)).Spin(Context(SmallHome())))))
            .Distinct()
            .Count();

        distinct.ShouldBeGreaterThan(20);
    }

    [Fact]
    public void One_spinner_gives_a_new_tray_on_each_call()
    {
        var spinner = Spinner(new SeededRandomSource(5));

        var trays = Enumerable.Range(0, 20).Select(_ => string.Join("|", Ids(spinner.Spin(Context(SmallHome()))))).Distinct().Count();

        trays.ShouldBeGreaterThan(5);
    }

    // ---- locks (SPEC §5.2 rule 5) ------------------------------------------------------------

    [Fact]
    public void A_locked_slot_keeps_its_dish_and_the_rest_are_drawn_again()
    {
        var first = Spinner(new SeededRandomSource(1)).Spin(Context(SmallHome()));
        var locked = first.Tray with
        {
            Slots = first.Tray.Slots.Select(s => s.Slot == DishSlot.Main ? s with { IsLocked = true } : s).ToArray(),
        };
        var mainId = locked.Slots.Single(s => s.Slot == DishSlot.Main).DishId;

        for (var seed = 10UL; seed < 60; seed++)
        {
            var again = Spinner(new SeededRandomSource(seed)).Spin(Context(SmallHome()), locked);

            again.Tray.Slots.Single(s => s.Slot == DishSlot.Main).ShouldBe(new SlotResult(DishSlot.Main, mainId, true));
            again.Tray.Slots.Where(s => s.Slot != DishSlot.Main).ShouldAllBe(s => !s.IsLocked);
        }
    }

    [Fact]
    public void A_locked_dish_is_kept_even_if_a_hard_rule_would_now_drop_it()
    {
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "canh-bi-do", false),
            new SlotResult(DishSlot.Main, "muc-xao-cay", true),     // chilli required
            new SlotResult(DishSlot.Vegetable, "cai-luoc", false),
        ]);
        var home = SmallHome(members: [Person("A", MemberTag.NoSpicy)]);

        var result = Spinner().Spin(Context(home, blocked: ["muc-xao-cay"]), tray);

        result.Tray.Slots.Single(s => s.Slot == DishSlot.Main).DishId.ShouldBe("muc-xao-cay");
    }

    [Fact]
    public void Locked_dishes_count_for_the_tray_rules_and_are_never_drawn_twice()
    {
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "canh-bi-do", true),
            new SlotResult(DishSlot.Main, "ga-ran", true),
            new SlotResult(DishSlot.Main, "tom-rang", true),
            new SlotResult(DishSlot.Vegetable, "cai-luoc", true),
        ]);

        var result = Spinner().Spin(Context(BigHome()), tray);

        Ids(result).ShouldBe(["canh-bi-do", "ga-ran", "tom-rang", "cai-luoc"]);
        result.RelaxedRules.ShouldBeEmpty();   // nothing to draw, so nothing to relax even though two fried dishes are locked
    }

    [Fact]
    public void Locks_match_by_kind_and_position_so_two_mains_keep_their_own_locks()
    {
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "canh-bi-do", false),
            new SlotResult(DishSlot.Main, "ga-kho-gung", false),
            new SlotResult(DishSlot.Main, "thit-kho-trung", true),   // the second main is locked
            new SlotResult(DishSlot.Vegetable, "cai-luoc", false),
        ]);

        for (var seed = 0UL; seed < 30; seed++)
        {
            var result = Spinner(new SeededRandomSource(seed)).Spin(Context(BigHome()), tray);

            var mains = result.Tray.Slots.Where(s => s.Slot == DishSlot.Main).ToArray();
            mains[1].ShouldBe(new SlotResult(DishSlot.Main, "thit-kho-trung", true));
            mains[0].DishId.ShouldNotBe("thit-kho-trung");
        }
    }

    [Fact]
    public void A_locked_dish_that_left_the_catalog_is_drawn_again()
    {
        var tray = new MealTray(Today, [new SlotResult(DishSlot.Main, "da-bi-go", true)]);

        var result = Spinner().Spin(Context(SmallHome()), tray);

        result.Tray.Slots.Single(s => s.Slot == DishSlot.Main).DishId.ShouldNotBe("da-bi-go");
        result.Tray.Slots.ShouldAllBe(s => !s.IsLocked);
    }

    [Fact]
    public void A_lock_on_a_second_main_is_dropped_when_the_household_no_longer_needs_one()
    {
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "canh-bi-do", false),
            new SlotResult(DishSlot.Main, "ga-kho-gung", false),
            new SlotResult(DishSlot.Main, "thit-kho-trung", true),
            new SlotResult(DishSlot.Vegetable, "cai-luoc", false),
        ]);

        var result = Spinner().Spin(Context(SmallHome()), tray);

        result.Tray.Slots.Count.ShouldBe(3);
        Ids(result).ShouldNotContain("thit-kho-trung");
    }

    // ---- swapping one dish (SPEC §5.5 step 5) ------------------------------------------------

    [Fact]
    public void Rerolling_a_slot_changes_only_that_slot_and_gives_a_different_dish()
    {
        var spinner = Spinner(new SeededRandomSource(3));
        var first = spinner.Spin(Context(SmallHome()));
        var mainIndex = first.Tray.Slots.ToList().FindIndex(s => s.Slot == DishSlot.Main);

        for (var i = 0; i < 40; i++)
        {
            var swapped = spinner.Reroll(Context(SmallHome()), first.Tray, mainIndex);

            swapped.Tray.Slots.Count.ShouldBe(first.Tray.Slots.Count);
            for (var s = 0; s < first.Tray.Slots.Count; s++)
            {
                if (s == mainIndex)
                {
                    swapped.Tray.Slots[s].DishId.ShouldNotBe(first.Tray.Slots[s].DishId);
                    swapped.Tray.Slots[s].Slot.ShouldBe(DishSlot.Main);
                }
                else
                {
                    swapped.Tray.Slots[s].ShouldBe(first.Tray.Slots[s]);
                }
            }
        }
    }

    [Fact]
    public void A_reroll_uses_the_same_hard_rules()
    {
        var home = SmallHome(members: [Person("A", MemberTag.NoSpicy), new Member("B", AgeGroup.Adult, 2, [], ["hai-san"])]);
        var context = Context(home, blocked: ["ca-kho-to"]);
        var tray = Spinner().Spin(context).Tray;
        var vegetableIndex = tray.Slots.ToList().FindIndex(s => s.Slot == DishSlot.Vegetable);
        var spinner = Spinner(new SeededRandomSource(8));

        for (var i = 0; i < 100; i++)
        {
            var id = spinner.Reroll(context, tray, vegetableIndex).Tray.Slots[vegetableIndex].DishId;

            id.ShouldNotBe("rau-cai-xao-ot");   // chilli required, someone is no-spicy
        }
    }

    [Fact]
    public void A_reroll_with_a_single_candidate_keeps_the_current_dish()
    {
        var dishes = new[] { Find("canh-bi-do"), Find("ga-ran"), Find("cai-luoc") };
        var context = Context(SmallHome(), dishes);
        var tray = Spinner().Spin(context).Tray;

        var swapped = Spinner().Reroll(context, tray, 1);

        swapped.Tray.Slots[1].DishId.ShouldBe("ga-ran");
        swapped.UnfilledSlots.ShouldBeEmpty();
    }

    [Fact]
    public void A_reroll_leaves_the_slot_unfilled_if_the_current_dish_is_no_longer_allowed()
    {
        var dishes = new[] { Find("canh-bi-do"), Find("ga-ran"), Find("cai-luoc") };
        var tray = Spinner().Spin(Context(SmallHome(), dishes)).Tray;

        var swapped = Spinner().Reroll(Context(SmallHome(), dishes, blocked: ["ga-ran"]), tray, 1);

        swapped.Tray.Slots.Select(s => s.Slot).ShouldBe([DishSlot.Soup, DishSlot.Vegetable]);
        swapped.UnfilledSlots.ShouldBe([DishSlot.Main]);
    }

    [Fact]
    public void A_reroll_never_repeats_a_dish_that_is_already_on_the_tray()
    {
        var context = Context(BigHome());
        var spinner = Spinner(new SeededRandomSource(11));
        var tray = spinner.Spin(context).Tray;

        for (var i = 0; i < 60; i++)
        {
            var swapped = spinner.Reroll(context, tray, 2);   // the second main

            swapped.Tray.Slots.Select(s => s.DishId).Distinct().Count().ShouldBe(4);
        }
    }

    [Fact]
    public void Rerolling_a_locked_slot_or_a_bad_index_is_rejected()
    {
        var spinner = Spinner();
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "canh-bi-do", true),
            new SlotResult(DishSlot.Main, "ga-ran", false),
            new SlotResult(DishSlot.Vegetable, "cai-luoc", false),
        ]);

        Should.Throw<ArgumentException>(() => spinner.Reroll(Context(SmallHome()), tray, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => spinner.Reroll(Context(SmallHome()), tray, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => spinner.Reroll(Context(SmallHome()), tray, -1));
    }

    [Fact]
    public void Rerolling_needs_the_other_dishes_of_the_tray_to_exist()
    {
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "da-bi-go", false),
            new SlotResult(DishSlot.Main, "ga-ran", false),
        ]);

        Should.Throw<InvalidOperationException>(() => Spinner().Reroll(Context(SmallHome()), tray, 1)).Message.ShouldContain("da-bi-go");
    }

    // ---- not enough dishes -------------------------------------------------------------------

    [Fact]
    public void A_slot_with_no_eligible_dish_is_reported_unfilled_and_the_rest_are_still_drawn()
    {
        var dishes = Catalog().Where(d => d.Slot != DishSlot.Soup).ToArray();

        var result = Spinner().Spin(Context(SmallHome(), dishes));

        result.UnfilledSlots.ShouldBe([DishSlot.Soup]);
        result.Tray.Slots.Select(s => s.Slot).ShouldBe([DishSlot.Main, DishSlot.Vegetable]);
    }

    [Fact]
    public void With_too_few_mains_for_two_main_slots_one_is_unfilled()
    {
        var dishes = Catalog().Where(d => d.Slot != DishSlot.Main || d.Id == "ga-ran").ToArray();

        var result = Spinner().Spin(Context(BigHome(), dishes));

        result.UnfilledSlots.ShouldBe([DishSlot.Main]);
        result.Tray.Slots.Count(s => s.Slot == DishSlot.Main).ShouldBe(1);
    }

    [Fact]
    public void An_empty_catalog_gives_an_empty_tray_with_every_slot_unfilled()
    {
        var result = Spinner().Spin(Context(SmallHome(), []));

        result.Tray.Slots.ShouldBeEmpty();
        result.UnfilledSlots.ShouldBe([DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable]);
        result.RelaxedRules.ShouldBeEmpty();
    }

    // ---- input checks ------------------------------------------------------------------------

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var spinner = Spinner();
        var context = Context(SmallHome());
        var tray = new MealTray(Today, [new SlotResult(DishSlot.Main, "ga-ran", false)]);
        var clock = new FixedClock(Today);

        Should.Throw<ArgumentNullException>(() => new MealSpinner(null!, PortionSettings, new SeededRandomSource(1), clock, new SumOfStepsEstimator()));
        Should.Throw<ArgumentNullException>(() => new MealSpinner(Config, null!, new SeededRandomSource(1), clock, new SumOfStepsEstimator()));
        Should.Throw<ArgumentNullException>(() => new MealSpinner(Config, PortionSettings, null!, clock, new SumOfStepsEstimator()));
        Should.Throw<ArgumentNullException>(() => new MealSpinner(Config, PortionSettings, new SeededRandomSource(1), null!, new SumOfStepsEstimator()));
        Should.Throw<ArgumentNullException>(() => new MealSpinner(Config, PortionSettings, new SeededRandomSource(1), clock, null!));
        Should.Throw<ArgumentNullException>(() => spinner.Spin(null!));
        Should.Throw<ArgumentNullException>(() => spinner.Reroll(null!, tray, 0));
        Should.Throw<ArgumentNullException>(() => spinner.Reroll(context, null!, 0));
        Should.Throw<ArgumentNullException>(() => spinner.IsEligible(null!, context));
        Should.Throw<ArgumentNullException>(() => spinner.IsEligible(Find("ga-ran"), null!));
        Should.Throw<ArgumentNullException>(() => spinner.WeightOf(null!, context));
        Should.Throw<ArgumentNullException>(() => spinner.WeightOf(Find("ga-ran"), null!));
    }

    [Fact]
    public void An_unusable_config_is_rejected_when_the_spinner_is_created()
    {
        Should.Throw<ArgumentException>(() => Spinner(config: Config with { AttemptsPerLevel = 0 }));

        var badPortions = PortionSettings with { RiceRoundingGrams = 0 };
        Should.Throw<ArgumentException>(() =>
            new MealSpinner(Config, badPortions, new SeededRandomSource(1), new FixedClock(Today), new SumOfStepsEstimator()));
    }

    [Fact]
    public void A_member_with_bowls_out_of_range_is_rejected_by_the_portion_rules()
    {
        var home = SmallHome(members: [TestData.Person("A", AgeGroup.Adult, 9)]);

        Should.Throw<ArgumentOutOfRangeException>(() => Spinner().Spin(Context(home)));
    }

    [Fact]
    public void A_dish_with_an_unknown_ingredient_makes_the_spin_fail_loudly()
    {
        var broken = TestData.Dish("hong", TestData.Line("khong-co", 10)) with { Slot = DishSlot.Main };

        Should.Throw<InvalidOperationException>(() => Spinner().Spin(Context(SmallHome(), [broken])));
    }
}
