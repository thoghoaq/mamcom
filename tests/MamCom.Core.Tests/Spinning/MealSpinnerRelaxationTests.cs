using MamCom.Core.Models;
using MamCom.Core.Spinning;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Spinning;

/// <summary>
/// SPEC §5.3 and §5.5 steps 3-4: tray rules are enforced, and when no draw satisfies them they are relaxed one
/// at a time in the order season/region weights, available minutes, one fried dish, distinct protein.
/// </summary>
public class MealSpinnerRelaxationTests
{
    private static readonly Dish Soup = D("canh", DishSlot.Soup, ProteinSources.None, CookingMethod.Simmer, ["bi-do"]);

    private static readonly Dish Veg = D("rau", DishSlot.Vegetable, ProteinSources.None, CookingMethod.Boil, ["cai"]);

    private static Dish MainDish(string id, string protein, CookingMethod method, int minutes = 20, string[]? seasons = null) =>
        D(id, DishSlot.Main, protein, method, ["thit-heo"], minutes: minutes, seasons: seasons);

    private static SpinResult Spin(Household home, IEnumerable<Dish> mains, ulong seed, Dish? soup = null, Dish? veg = null) =>
        Spinner(new SeededRandomSource(seed)).Spin(Context(home, [soup ?? Soup, veg ?? Veg, .. mains]));

    private static int Count(SpinResult result, Func<Dish, bool> predicate, IEnumerable<Dish> mains)
    {
        Dish[] catalog = [Soup, Veg, .. mains];
        return result.Tray.Slots.Count(s => predicate(catalog.Single(d => d.Id == s.DishId)));
    }

    // ---- each rule is enforced when it can be -------------------------------------------------

    [Fact]
    public void With_enough_variety_nothing_is_relaxed_and_the_tray_follows_every_rule()
    {
        var mains = new[]
        {
            MainDish("m-heo-kho", "heo", CookingMethod.Braise), MainDish("m-ga-ran", "ga", CookingMethod.Fry),
            MainDish("m-ca-ran", "ca", CookingMethod.Fry), MainDish("m-bo-xao", "bo", CookingMethod.StirFry),
            MainDish("m-tom-hap", "tom", CookingMethod.Steam),
        };

        for (ulong seed = 0; seed < 300; seed++)
        {
            var result = Spin(BigHome(), mains, seed);

            result.RelaxedRules.ShouldBeEmpty();
            Count(result, d => d.CookingMethod == CookingMethod.Fry, mains).ShouldBeLessThanOrEqualTo(1);
            var proteins = mains.Where(m => result.Tray.Slots.Any(s => s.DishId == m.Id)).Select(m => m.ProteinSource).ToArray();
            proteins.Distinct().Count().ShouldBe(proteins.Length);
        }
    }

    [Fact]
    public void Two_fried_mains_are_avoided_while_a_fry_free_pair_exists()
    {
        // Three fried mains and one braised: a valid pair always contains the braised one.
        var mains = new[]
        {
            MainDish("m-a", "ga", CookingMethod.Fry), MainDish("m-b", "ca", CookingMethod.Fry),
            MainDish("m-c", "tom", CookingMethod.Fry), MainDish("m-d", "heo", CookingMethod.Braise),
        };

        for (ulong seed = 0; seed < 300; seed++)
        {
            var result = Spin(BigHome(), mains, seed);

            result.RelaxedRules.ShouldBeEmpty();
            result.Tray.Slots.ShouldContain(s => s.DishId == "m-d");
        }
    }

    [Fact]
    public void Two_mains_with_the_same_protein_are_avoided_while_a_distinct_pair_exists()
    {
        var mains = new[]
        {
            MainDish("m-a", "heo", CookingMethod.Braise), MainDish("m-b", "heo", CookingMethod.Grill),
            MainDish("m-c", "heo", CookingMethod.StirFry), MainDish("m-d", "ga", CookingMethod.Braise),
        };

        for (ulong seed = 0; seed < 300; seed++)
        {
            var result = Spin(BigHome(), mains, seed);

            result.RelaxedRules.ShouldBeEmpty();
            result.Tray.Slots.ShouldContain(s => s.DishId == "m-d");
        }
    }

    [Fact]
    public void The_protein_rule_does_not_count_none_or_vegetarian_dishes_as_duplicates()
    {
        var soupWithoutProtein = D("canh-n", DishSlot.Soup, ProteinSources.None, CookingMethod.Simmer, ["bi-do"]);
        var veg2 = D("rau-n", DishSlot.Vegetable, ProteinSources.None, CookingMethod.Boil, ["cai"]);
        var mains = new[]
        {
            MainDish("m-chay-1", ProteinSources.Vegetarian, CookingMethod.Simmer), MainDish("m-chay-2", ProteinSources.Vegetarian, CookingMethod.Braise),
        };

        for (ulong seed = 0; seed < 100; seed++)
        {
            var result = Spin(BigHome(), mains, seed, soupWithoutProtein, veg2);

            result.RelaxedRules.ShouldBeEmpty();   // two vegetarian mains and a no-protein soup and vegetable: none counts as a duplicate
        }
    }

    [Fact]
    public void Dishes_with_no_protein_source_at_all_never_count_as_duplicates()
    {
        var mains = new[] { MainDish("m-a", "none", CookingMethod.Braise), MainDish("m-b", "none", CookingMethod.Grill) };

        for (ulong seed = 0; seed < 50; seed++)
        {
            Spin(BigHome(), mains, seed).RelaxedRules.ShouldBeEmpty();
        }
    }

    [Fact]
    public void Available_minutes_is_enforced_while_a_fast_enough_tray_exists()
    {
        // Soup 10 + vegetable 10 + the slow or fast main. With a limit of 45 only a main of 25 minutes or less fits.
        var soup = D("canh", DishSlot.Soup, ProteinSources.None, CookingMethod.Simmer, ["bi-do"], minutes: 10);
        var veg = D("rau", DishSlot.Vegetable, ProteinSources.None, CookingMethod.Boil, ["cai"], minutes: 10);
        var mains = new[]
        {
            MainDish("m-cham-1", "heo", CookingMethod.Braise, minutes: 60), MainDish("m-cham-2", "ga", CookingMethod.Braise, minutes: 50),
            MainDish("m-nhanh", "ca", CookingMethod.StirFry, minutes: 20),
        };

        for (ulong seed = 0; seed < 200; seed++)
        {
            var result = Spin(SmallHome(availableMinutes: 40), mains, seed, soup, veg);

            result.RelaxedRules.ShouldBeEmpty();
            result.Tray.Slots.Single(s => s.Slot == DishSlot.Main).DishId.ShouldBe("m-nhanh");
        }
    }

    // ---- relaxation, in order ----------------------------------------------------------------

    [Fact]
    public void When_every_main_is_fried_the_fried_rule_is_relaxed_and_nothing_else()
    {
        var mains = new[]
        {
            MainDish("m-a", "heo", CookingMethod.Fry), MainDish("m-b", "ga", CookingMethod.Fry), MainDish("m-c", "ca", CookingMethod.Fry),
        };

        for (ulong seed = 0; seed < 200; seed++)
        {
            var result = Spin(BigHome(), mains, seed);

            result.RelaxedRules.ShouldBe([RelaxedRule.SingleFriedDish]);
            result.Tray.Slots.Count(s => s.Slot == DishSlot.Main).ShouldBe(2);
        }
    }

    [Fact]
    public void When_every_main_shares_a_protein_only_the_protein_rule_is_reported_and_the_earlier_rules_hold()
    {
        // Relaxing goes weights, minutes, fried, protein; but only what the tray really breaks is reported,
        // and the fried rule was not needed, so it must still hold in every delivered tray.
        var mains = new[]
        {
            MainDish("m-a", "heo", CookingMethod.Braise), MainDish("m-b", "heo", CookingMethod.Fry), MainDish("m-c", "heo", CookingMethod.Grill),
        };

        for (ulong seed = 0; seed < 300; seed++)
        {
            var result = Spin(BigHome(), mains, seed);

            result.RelaxedRules.ShouldBe([RelaxedRule.DistinctProteinSources]);
            Count(result, d => d.CookingMethod == CookingMethod.Fry, mains).ShouldBeLessThanOrEqualTo(1);
        }
    }

    [Fact]
    public void When_both_fried_and_protein_rules_are_unsatisfiable_both_are_reported_in_order()
    {
        var mains = new[] { MainDish("m-a", "heo", CookingMethod.Fry), MainDish("m-b", "heo", CookingMethod.Fry) };

        var result = Spin(BigHome(), mains, 1);

        result.RelaxedRules.ShouldBe([RelaxedRule.SingleFriedDish, RelaxedRule.DistinctProteinSources]);
    }

    [Fact]
    public void When_the_time_limit_cannot_be_met_it_is_relaxed_first_and_alone()
    {
        var mains = new[] { MainDish("m-a", "heo", CookingMethod.Braise, minutes: 90), MainDish("m-b", "ga", CookingMethod.Braise, minutes: 80) };

        for (ulong seed = 0; seed < 100; seed++)
        {
            var result = Spin(SmallHome(availableMinutes: 30), mains, seed);

            result.RelaxedRules.ShouldBe([RelaxedRule.AvailableMinutes]);
        }
    }

    [Fact]
    public void Time_comes_before_fried_in_the_relaxation_order()
    {
        // Two mains needed, all fried and all slow, and the limit is unreachable.
        var mains = new[] { MainDish("m-a", "heo", CookingMethod.Fry, 90), MainDish("m-b", "ga", CookingMethod.Fry, 80), MainDish("m-c", "ca", CookingMethod.Fry, 70) };

        var result = Spin(BigHome(availableMinutes: 30), mains, 4);

        result.RelaxedRules.ShouldBe([RelaxedRule.AvailableMinutes, RelaxedRule.SingleFriedDish]);
    }

    [Fact]
    public void A_time_limit_that_was_never_set_is_never_reported()
    {
        var mains = new[] { MainDish("m-a", "heo", CookingMethod.Braise, minutes: 500) };

        Spin(SmallHome(availableMinutes: null), mains, 1).RelaxedRules.ShouldBeEmpty();
    }

    // ---- the weights are the first thing to go -----------------------------------------------

    [Fact]
    public void Season_and_region_weights_are_dropped_first_and_reported_when_they_apply()
    {
        // The dishes name a season, so the weights matter; every main is fried, so the fried rule fails.
        var mains = new[]
        {
            MainDish("m-a", "heo", CookingMethod.Fry, seasons: ["autumn"]), MainDish("m-b", "ga", CookingMethod.Fry),
            MainDish("m-c", "ca", CookingMethod.Fry),
        };

        var result = Spin(BigHome(), mains, 2);

        result.RelaxedRules.ShouldBe([RelaxedRule.SeasonAndRegionWeights, RelaxedRule.SingleFriedDish]);
    }

    [Fact]
    public void Weights_that_change_nothing_are_not_reported_as_relaxed()
    {
        // No dish names a season or a region the household shares, so every factor is 1.
        var mains = new[] { MainDish("m-a", "heo", CookingMethod.Fry), MainDish("m-b", "ga", CookingMethod.Fry) };

        var result = Spin(BigHome(), mains, 2);

        result.RelaxedRules.ShouldBe([RelaxedRule.SingleFriedDish]);
    }

    [Fact]
    public void Dropping_the_weights_flattens_the_draw_so_a_valid_tray_can_be_found()
    {
        // a-bad lists the current season, and the season weight is made huge, so with the weights on it is always
        // drawn; it shares its protein with the soup, which breaks the protein rule. With a fixed random value of 0.9
        // the weighted pick always lands on a-bad and the flat pick always lands on b-good.
        var soupHeo = D("canh-heo", DishSlot.Soup, "heo", CookingMethod.Simmer, ["thit-heo"]);
        var mains = new[]
        {
            MainDish("a-bad", "heo", CookingMethod.Braise, seasons: ["autumn"]),
            MainDish("b-good", "ga", CookingMethod.Braise),
        };
        var config = Config with { SeasonMatchWeight = 1_000_000, AttemptsPerLevel = 3 };
        var random = new ScriptedRandom(Enumerable.Repeat(0.9, 100).ToArray());

        var result = Spinner(random, config).Spin(Context(SmallHome(), [soupHeo, Veg, .. mains]));

        result.Tray.Slots.Single(s => s.Slot == DishSlot.Main).DishId.ShouldBe("b-good");
        result.RelaxedRules.ShouldBe([RelaxedRule.SeasonAndRegionWeights]);
        random.Calls.ShouldBe(3 * 3 + 1 * 3);   // 3 failed attempts with the weights, then one flat attempt that works
    }

    // ---- rules the fixed dishes already break ------------------------------------------------

    private static readonly Dish FastSoup = D("canh-nhanh", DishSlot.Soup, ProteinSources.None, CookingMethod.Simmer, ["bi-do"], minutes: 10);

    private static readonly Dish SlowSoup = D("canh-cham", DishSlot.Soup, ProteinSources.None, CookingMethod.Simmer, ["bi-do"], minutes: 60);

    private static readonly Dish FastVeg = D("rau-nhanh", DishSlot.Vegetable, ProteinSources.None, CookingMethod.Boil, ["cai"], minutes: 10);

    private static readonly Dish SlowVeg = D("rau-cham", DishSlot.Vegetable, ProteinSources.None, CookingMethod.Boil, ["cai"], minutes: 50);

    private static readonly Dish FriedA = MainDish("chien-a", "heo", CookingMethod.Fry, minutes: 10);

    private static readonly Dish FriedB = MainDish("chien-b", "ga", CookingMethod.Fry, minutes: 10);

    [Fact]
    public void Locking_two_fried_mains_gives_up_the_fried_rule_at_once_and_keeps_the_time_limit()
    {
        // The locked mains break the fried rule whatever else is drawn, so relaxing the time limit first would be for nothing.
        // A tray within 40 minutes exists: the locked 20, plus the fast soup and the fast vegetable.
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "canh-cham", false),
            new SlotResult(DishSlot.Main, "chien-a", true),
            new SlotResult(DishSlot.Main, "chien-b", true),
            new SlotResult(DishSlot.Vegetable, "rau-cham", false),
        ]);
        var context = Context(BigHome(availableMinutes: 40), [FastSoup, SlowSoup, FriedA, FriedB, FastVeg, SlowVeg]);

        for (ulong seed = 0; seed < 300; seed++)
        {
            var result = Spinner(new SeededRandomSource(seed)).Spin(context, tray);

            result.RelaxedRules.ShouldBe([RelaxedRule.SingleFriedDish]);
            Ids(result).ShouldBe(["canh-nhanh", "chien-a", "chien-b", "rau-nhanh"]);
        }
    }

    [Fact]
    public void A_reroll_beside_two_fried_mains_keeps_the_time_limit_too()
    {
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "canh-cham", false),
            new SlotResult(DishSlot.Main, "chien-a", false),
            new SlotResult(DishSlot.Main, "chien-b", false),
            new SlotResult(DishSlot.Vegetable, "rau-nhanh", false),
        ]);
        var context = Context(BigHome(availableMinutes: 40), [FastSoup, SlowSoup, FriedA, FriedB, FastVeg, SlowVeg]);

        for (ulong seed = 0; seed < 100; seed++)
        {
            var result = Spinner(new SeededRandomSource(seed)).Reroll(context, tray, 0);

            result.Tray.Slots[0].DishId.ShouldBe("canh-nhanh");
            result.RelaxedRules.ShouldBe([RelaxedRule.SingleFriedDish]);
        }
    }

    [Fact]
    public void A_time_limit_the_locked_dishes_already_exceed_is_given_up_at_once_and_the_other_rules_still_hold()
    {
        // Two locked mains take 100 minutes against a limit of 40, so only the time rule can go. The fried and protein
        // rules are still satisfiable by the draw and must be kept.
        var slowA = MainDish("cham-a", "heo", CookingMethod.Braise, minutes: 60);
        var slowB = MainDish("cham-b", "ga", CookingMethod.Braise, minutes: 40);
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "canh-nhanh", false),
            new SlotResult(DishSlot.Main, "cham-a", true),
            new SlotResult(DishSlot.Main, "cham-b", true),
            new SlotResult(DishSlot.Vegetable, "rau-nhanh", false),
        ]);
        var soupHeo = D("canh-heo", DishSlot.Soup, "heo", CookingMethod.Simmer, ["thit-heo"], minutes: 10);
        var context = Context(BigHome(availableMinutes: 40), [FastSoup, soupHeo, slowA, slowB, FastVeg]);

        for (ulong seed = 0; seed < 200; seed++)
        {
            var result = Spinner(new SeededRandomSource(seed)).Spin(context, tray);

            result.RelaxedRules.ShouldBe([RelaxedRule.AvailableMinutes]);
            result.Tray.Slots[0].DishId.ShouldBe("canh-nhanh");   // the soup sharing the locked main's protein is never taken
        }
    }

    [Fact]
    public void The_number_of_draws_per_level_comes_from_the_config()
    {
        // Every slot has exactly one candidate, so each attempt makes three draws. The soup and the main are both fried,
        // so the fried rule fails on every attempt until it is relaxed.
        var mains = new[] { MainDish("m-a", "heo", CookingMethod.Fry) };
        var soupFry = D("canh-chien", DishSlot.Soup, "ga", CookingMethod.Fry, ["thit-heo"]);   // 2 fried dishes: the fried rule always fails
        var random = new ScriptedRandom();
        var config = Config with { AttemptsPerLevel = 7 };

        var result = Spinner(random, config).Spin(Context(SmallHome(), [soupFry, Veg, .. mains]));

        // Weights do not apply and no time limit is set, so the order here is fried, then protein. Level 0 enforces both
        // and fails 7 times; level 1 has the fried rule relaxed, the protein rule holds, and the first attempt works.
        result.RelaxedRules.ShouldBe([RelaxedRule.SingleFriedDish]);
        random.Calls.ShouldBe((7 + 1) * 3);
    }
}
