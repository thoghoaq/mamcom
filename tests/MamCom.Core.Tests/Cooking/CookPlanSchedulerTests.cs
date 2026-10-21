using MamCom.Core.Cooking;
using MamCom.Core.Models;

namespace MamCom.Core.Tests.Cooking;

/// <summary>
/// SPEC §5.6 with rice at 5 minutes of hands plus 30 minutes unattended. Every expected number was worked out by hand
/// from the rules; the minutes are test-only.
/// </summary>
public class CookPlanSchedulerTests
{
    private static readonly CookingConfig Config = CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"));

    private static CookPlanScheduler Scheduler(CookingConfig? config = null) => new(config ?? Config);

    private static Dish Cook(string id, params (int Active, int Passive)[] steps) => DishWith(id, hot: false, steps);

    private static Dish Hot(string id, params (int Active, int Passive)[] steps) => DishWith(id, hot: true, steps);

    private static Dish DishWith(string id, bool hot, (int Active, int Passive)[] steps) =>
        TestData.Dish(id) with
        {
            ServeHot = hot,
            Steps = steps.Select((s, i) => new Step(i + 1, $"{id} bước {i + 1}", s.Active, s.Passive, [])).ToArray(),
        };

    private static (string Dish, int Start, int Active, int? Timer)[] Summary(CookPlan plan) =>
        plan.Steps.Select(s => (s.DishId, s.StartMinute, s.ActiveMinutes, s.TimerMinutes)).ToArray();

    // ---- rice ---------------------------------------------------------------------------------

    [Fact]
    public void A_meal_without_dishes_is_just_the_rice()
    {
        var plan = Scheduler().Schedule([]);

        Summary(plan).ShouldBe([("com", 0, 5, 30)]);
        plan.Steps[0].Text.ShouldBe(Config.RiceStepText);
        plan.MealTimeMinutes.ShouldBe(35);
    }

    [Fact]
    public void Rice_minutes_and_text_come_from_the_config()
    {
        var config = new CookingConfig(7, 45, "Nấu cơm.", 2, 4);

        var plan = Scheduler(config).Schedule([]);

        Summary(plan).ShouldBe([("com", 0, 7, 45)]);
        plan.Steps[0].Text.ShouldBe("Nấu cơm.");
        plan.MealTimeMinutes.ShouldBe(52);
    }

    [Fact]
    public void Rice_with_no_unattended_time_has_no_timer()
    {
        var plan = Scheduler(new CookingConfig(5, 0, "Nấu cơm.", 2, 4)).Schedule([]);

        plan.Steps[0].TimerMinutes.ShouldBeNull();
        plan.MealTimeMinutes.ShouldBe(5);
    }

    [Fact]
    public void Rice_starts_first_even_when_another_dish_takes_far_longer()
    {
        var plan = Scheduler().Schedule([Cook("hầm", (10, 200))]);

        plan.Steps[0].DishId.ShouldBe("com");
        plan.Steps[0].StartMinute.ShouldBe(0);
        plan.Steps[1].StartMinute.ShouldBe(5);   // the cook starts the stew as soon as the rice is on
    }

    // ---- cold dishes: one cook, hands never overlap --------------------------------------------

    [Fact]
    public void A_quick_dish_fits_inside_the_rice_time_so_the_meal_takes_as_long_as_the_rice()
    {
        var plan = Scheduler().Schedule([Cook("rau", (10, 0))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("rau", 5, 10, null)]);
        plan.MealTimeMinutes.ShouldBe(35);
    }

    [Fact]
    public void A_long_simmer_sets_the_meal_time()
    {
        var plan = Scheduler().Schedule([Cook("canh", (10, 40))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("canh", 5, 10, 40)]);
        plan.MealTimeMinutes.ShouldBe(55);
    }

    [Fact]
    public void The_dish_with_the_longest_total_time_starts_first()
    {
        // Braise 5 + 60 = 65 beats soup 10 + 40 = 50, so the braise gets the hands first.
        var plan = Scheduler().Schedule([Cook("canh", (10, 40)), Cook("kho", (5, 60))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("kho", 5, 5, 60), ("canh", 10, 10, 40)]);
        plan.MealTimeMinutes.ShouldBe(70);   // braise: 5 + 5 + 60
    }

    [Fact]
    public void The_cook_fills_the_waiting_time_of_one_dish_with_another_and_comes_back()
    {
        // A: wash and marinate (5 + 30 unattended), then cook (5). B: 10 minutes of hands. Rice takes the first 5 minutes.
        var plan = Scheduler().Schedule([Cook("a", (5, 30), (5, 0)), Cook("b", (10, 0))]);

        Summary(plan).ShouldBe(
        [
            ("com", 0, 5, 30),
            ("a", 5, 5, 30),      // A's first step, ready again at minute 40
            ("b", 10, 10, null),  // B fills the wait
            ("a", 40, 5, null),   // the cook waits until 40, then finishes A
        ]);
        plan.MealTimeMinutes.ShouldBe(45);
    }

    [Fact]
    public void A_step_that_needs_no_hands_runs_alongside_other_work()
    {
        // D: 5 minutes of hands, 20 minutes resting, 5 minutes of hands. While D rests, E uses the hands.
        var plan = Scheduler().Schedule([Cook("d", (5, 0), (0, 20), (5, 0)), Cook("e", (10, 0))]);

        Summary(plan).ShouldBe(
        [
            ("com", 0, 5, 30),
            ("d", 5, 5, null),
            ("d", 10, 0, 20),    // resting starts the moment the first step ends and takes no hands
            ("e", 10, 10, null), // E works while D rests
            ("d", 30, 5, null),
        ]);
        plan.MealTimeMinutes.ShouldBe(35);
    }

    [Fact]
    public void A_step_with_no_time_at_all_is_kept_in_the_plan()
    {
        var plan = Scheduler().Schedule([Cook("trống", (0, 0))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("trống", 0, 0, null)]);
        plan.MealTimeMinutes.ShouldBe(35);
    }

    [Fact]
    public void Steps_of_a_dish_are_taken_in_their_order_number_not_their_list_position()
    {
        var dish = TestData.Dish("x") with
        {
            Steps = [new Step(2, "sau", 5, 0, []), new Step(1, "trước", 10, 0, [])],
        };

        var plan = Scheduler().Schedule([dish]);

        plan.Steps.Skip(1).Select(s => s.Text).ShouldBe(["trước", "sau"]);
        plan.Steps[1].StartMinute.ShouldBe(5);
        plan.Steps[2].StartMinute.ShouldBe(15);
    }

    [Fact]
    public void A_dish_without_steps_adds_nothing()
    {
        var plan = Scheduler().Schedule([TestData.Dish("trống không")]);

        Summary(plan).ShouldBe([("com", 0, 5, 30)]);
    }

    [Fact]
    public void Dishes_with_equal_total_time_go_in_id_order_whatever_the_input_order()
    {
        var a = Cook("a", (10, 0));
        var b = Cook("b", (10, 0));

        var forward = Scheduler().Schedule([a, b]);
        var backward = Scheduler().Schedule([b, a]);

        Summary(forward).ShouldBe([("com", 0, 5, 30), ("a", 5, 10, null), ("b", 15, 10, null)]);
        backward.Steps.ShouldBe(forward.Steps);
    }

    // ---- serve-hot dishes ----------------------------------------------------------------------

    [Fact]
    public void A_hot_dish_is_cooked_so_that_it_ends_when_the_meal_ends()
    {
        // The soup ends at minute 55. The stir-fry takes 8 minutes of hands, so it starts at 47 and ends at 55.
        var plan = Scheduler().Schedule([Cook("canh", (10, 40)), Hot("xào", (8, 0))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("canh", 5, 10, 40), ("xào", 47, 8, null)]);
        plan.MealTimeMinutes.ShouldBe(55);
    }

    [Fact]
    public void A_hot_dish_alone_ends_with_the_rice()
    {
        var plan = Scheduler().Schedule([Hot("xào", (8, 0))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("xào", 27, 8, null)]);   // 35 - 8
        plan.MealTimeMinutes.ShouldBe(35);
    }

    [Fact]
    public void A_hot_dish_longer_than_the_meal_starts_when_the_hands_are_free_and_sets_the_meal_time()
    {
        // 10 + 30 = 40 minutes against rice's 35: it would start at 0, but the hands wash the rice until 5.
        var plan = Scheduler().Schedule([Hot("chiên", (10, 30))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("chiên", 5, 10, 30)]);
        plan.MealTimeMinutes.ShouldBe(45);
    }

    [Fact]
    public void Two_hot_dishes_go_back_to_back_with_the_shorter_one_last()
    {
        var plan = Scheduler().Schedule([Hot("ngắn", (5, 0)), Hot("dài", (10, 0))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("dài", 25, 10, null), ("ngắn", 35, 5, null)]);
        plan.MealTimeMinutes.ShouldBe(40);
    }

    [Fact]
    public void A_hot_dish_waits_for_the_hands_when_the_last_cold_step_is_hands_on()
    {
        // The cold dish washes (5 + 60 unattended) and finishes by hand at 70-75. The stir-fry cannot overlap that.
        var plan = Scheduler().Schedule([Cook("hầm", (5, 60), (5, 0)), Hot("xào", (8, 0))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("hầm", 5, 5, 60), ("hầm", 70, 5, null), ("xào", 75, 8, null)]);
        plan.MealTimeMinutes.ShouldBe(83);
    }

    [Fact]
    public void A_hot_dish_that_starts_by_resting_rests_first_and_then_finishes_by_hand_at_the_end()
    {
        var plan = Scheduler().Schedule([Hot("ướp", (0, 10), (6, 0))]);

        Summary(plan).ShouldBe([("com", 0, 5, 30), ("ướp", 19, 0, 10), ("ướp", 29, 6, null)]);
        plan.MealTimeMinutes.ShouldBe(35);
    }

    [Fact]
    public void Hot_dishes_never_end_before_the_cold_part_of_the_meal_does()
    {
        var cold = new[] { Cook("canh", (10, 40)) };
        var coldOnly = Scheduler().Schedule(cold).MealTimeMinutes;

        var plan = Scheduler().Schedule([.. cold, Hot("xào", (8, 0)), Hot("luộc", (3, 2))]);

        foreach (var hot in new[] { "xào", "luộc" })
        {
            var last = plan.Steps.Where(s => s.DishId == hot).Max(s => s.StartMinute + s.ActiveMinutes + (s.TimerMinutes ?? 0));
            last.ShouldBeGreaterThanOrEqualTo(coldOnly);
        }
    }

    // ---- the meal time -------------------------------------------------------------------------

    [Fact]
    public void The_meal_time_is_not_the_sum_of_the_dish_times()
    {
        var dishes = new[] { Cook("a", (10, 40)), Cook("b", (10, 40)), Cook("c", (10, 40)) };

        var plan = Scheduler().Schedule(dishes);

        plan.MealTimeMinutes.ShouldBeLessThan(35 + dishes.Sum(d => 50));
        plan.MealTimeMinutes.ShouldBe(75);   // c, b, a start at 5, 15, 25 and the last ends at 25 + 10 + 40
    }

    [Fact]
    public void The_estimator_gives_the_meal_time_of_the_plan()
    {
        IMealTimeEstimator estimator = Scheduler();
        var dishes = new[] { Cook("canh", (10, 40)), Hot("xào", (8, 0)) };

        estimator.EstimateMealMinutes(dishes).ShouldBe(Scheduler().Schedule(dishes).MealTimeMinutes);
        estimator.EstimateMealMinutes([]).ShouldBe(35);
    }

    // ---- bad input -----------------------------------------------------------------------------

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void A_step_with_negative_minutes_is_rejected(int active, int passive)
    {
        Should.Throw<ArgumentException>(() => Scheduler().Schedule([Cook("lỗi", (active, passive))]))
            .Message.ShouldContain("lỗi");
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Should.Throw<ArgumentNullException>(() => new CookPlanScheduler(null!));
        Should.Throw<ArgumentNullException>(() => Scheduler().Schedule(null!));
    }

    [Fact]
    public void An_unusable_config_is_rejected_when_the_scheduler_is_created()
    {
        Should.Throw<ArgumentException>(() => Scheduler(Config with { RiceActiveMinutes = -1 }));
    }
}
