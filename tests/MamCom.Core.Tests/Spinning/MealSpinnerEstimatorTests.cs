using MamCom.Core.Cooking;
using MamCom.Core.Models;
using MamCom.Core.Spinning;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Spinning;

/// <summary>How the spinner uses the meal-time estimator: the estimate for checking, the lower bound for giving up early.</summary>
public class MealSpinnerEstimatorTests
{
    private static readonly CookingConfig CookingDefaults = CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"));

    private static Dish Quick(string id, DishSlot slot, string protein) =>
        D(id, slot, protein, CookingMethod.StirFry, ["cai"], minutes: 5);

    private static Dish Slow(string id, DishSlot slot, string protein) =>
        D(id, slot, protein, CookingMethod.Braise, ["cai"], minutes: 60);

    // ---- the lower bound is what lets the spinner give up a time limit early ---------------------

    private static (int Calls, SpinResult Result) SpinWithLockedSlowMains(IMealTimeEstimator estimator)
    {
        // Two locked mains take 100 minutes against a limit of 40: no soup or vegetable can help.
        var mains = new[]
        {
            D("cham-a", DishSlot.Main, "heo", CookingMethod.Braise, ["thit-heo"], minutes: 60),
            D("cham-b", DishSlot.Main, "ga", CookingMethod.Braise, ["ga"], minutes: 40),
        };
        var soup = Quick("canh", DishSlot.Soup, ProteinSources.None);
        var veg = Quick("rau", DishSlot.Vegetable, ProteinSources.None);
        var tray = new MealTray(Today,
        [
            new SlotResult(DishSlot.Soup, "canh", false),
            new SlotResult(DishSlot.Main, "cham-a", true),
            new SlotResult(DishSlot.Main, "cham-b", true),
            new SlotResult(DishSlot.Vegetable, "rau", false),
        ]);
        var random = new ScriptedRandom();

        var result = Spinner(random, estimator: estimator).Spin(Context(BigHome(availableMinutes: 40), [soup, veg, .. mains]), tray);
        return (random.Calls, result);
    }

    [Fact]
    public void A_lower_bound_above_the_limit_gives_up_the_time_rule_before_any_draw_is_wasted()
    {
        var (calls, result) = SpinWithLockedSlowMains(new SumOfStepsEstimator());

        result.RelaxedRules.ShouldBe([RelaxedRule.AvailableMinutes]);
        calls.ShouldBe(2);   // one attempt, two slots to draw
    }

    [Fact]
    public void Without_a_useful_lower_bound_the_spinner_tries_every_draw_before_relaxing_the_time_rule()
    {
        var (calls, result) = SpinWithLockedSlowMains(new SumWithNoBoundEstimator());

        result.RelaxedRules.ShouldBe([RelaxedRule.AvailableMinutes]);
        calls.ShouldBe((Config.AttemptsPerLevel + 1) * 2);   // 50 failed attempts, then one that is accepted
    }

    // ---- with the real scheduler ---------------------------------------------------------------------

    [Fact]
    public void With_the_real_scheduler_a_time_limit_keeps_the_slow_main_out()
    {
        var dishes = new[]
        {
            Quick("canh", DishSlot.Soup, ProteinSources.None),
            Quick("rau", DishSlot.Vegetable, ProteinSources.None),
            Quick("man-nhanh", DishSlot.Main, "ga"),
            Slow("man-cham", DishSlot.Main, "heo"),
        };
        var scheduler = new CookPlanScheduler(CookingDefaults);

        for (ulong seed = 0; seed < 100; seed++)
        {
            // Rice 5 + 30; with the slow main (60 minutes of hands) the meal takes 75, with the quick one 35.
            var result = Spinner(new SeededRandomSource(seed), estimator: scheduler)
                .Spin(Context(SmallHome(availableMinutes: 45), dishes));

            result.RelaxedRules.ShouldBeEmpty();
            Ids(result).ShouldContain("man-nhanh");
            scheduler.EstimateMealMinutes(Dishes(result, dishes).ToArray()).ShouldBeLessThanOrEqualTo(45);
        }
    }

    [Fact]
    public void With_the_real_scheduler_the_time_rule_is_relaxed_when_nothing_fits()
    {
        var dishes = new[]
        {
            Quick("canh", DishSlot.Soup, ProteinSources.None),
            Quick("rau", DishSlot.Vegetable, ProteinSources.None),
            Slow("man-cham", DishSlot.Main, "heo"),
        };

        var result = Spinner(estimator: new CookPlanScheduler(CookingDefaults))
            .Spin(Context(SmallHome(availableMinutes: 45), dishes));

        result.RelaxedRules.ShouldBe([RelaxedRule.AvailableMinutes]);
        Ids(result).ShouldContain("man-cham");
    }

    [Fact]
    public void A_limit_equal_to_the_meal_time_is_met()
    {
        // Rice alone: 35 minutes. A limit of exactly 35 is not exceeded by quick dishes that fit inside the rice time.
        var dishes = new[]
        {
            Quick("canh", DishSlot.Soup, ProteinSources.None),
            Quick("rau", DishSlot.Vegetable, ProteinSources.None),
            Quick("man", DishSlot.Main, "ga"),
        };

        var result = Spinner(estimator: new CookPlanScheduler(CookingDefaults))
            .Spin(Context(SmallHome(availableMinutes: 35), dishes));

        result.RelaxedRules.ShouldBeEmpty();
    }

    private static IEnumerable<Dish> Dishes(SpinResult result, IEnumerable<Dish> catalog) =>
        result.Tray.Slots.Select(s => catalog.Single(d => d.Id == s.DishId));
}
