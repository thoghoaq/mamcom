using MamCom.Core.Cooking;
using MamCom.Core.Models;

namespace MamCom.Core.Tests.Cooking;

/// <summary>
/// The four properties PLAN 1.7 asks for, plus the ones the spinner relies on, over thousands of random meals:
/// rice first, hands never overlap, hot dishes at the end, meal time at least the longest dish, order independence,
/// and "adding a dish never shortens the meal".
/// </summary>
public class CookPlanSchedulerPropertyTests
{
    private static readonly CookingConfig Config = CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"));

    private static Dish RandomDish(IRandomSource random, string id)
    {
        var steps = Enumerable.Range(0, 1 + random.NextInt(4)).Select(i =>
        {
            var active = random.NextInt(4) == 0 ? 0 : 1 + random.NextInt(15);
            var passive = random.NextInt(3) == 0 ? 0 : 1 + random.NextInt(60);
            return new Step(i + 1, $"{id} bước {i + 1}", active, passive, []);
        }).ToArray();

        return TestData.Dish(id) with { ServeHot = random.NextInt(3) == 0, Steps = steps };
    }

    private static Dish[] RandomMeal(IRandomSource random, int maxDishes = 6) =>
        Enumerable.Range(0, random.NextInt(maxDishes + 1)).Select(i => RandomDish(random, $"d{i}")).ToArray();

    private static int End(CookPlanStep step) => step.StartMinute + step.ActiveMinutes + (step.TimerMinutes ?? 0);

    private static int Total(Dish dish) => dish.Steps.Sum(s => s.ActiveMinutes + s.PassiveMinutes);

    [Fact]
    public void Ten_thousand_random_meals_keep_every_rule_of_the_plan()
    {
        var scheduler = new CookPlanScheduler(Config);

        for (ulong seed = 0; seed < 10_000; seed++)
        {
            var random = new SeededRandomSource(seed);
            var dishes = RandomMeal(random);
            var plan = scheduler.Schedule(dishes);
            var where = $"seed {seed}";

            // Rice first, at minute 0, and nothing before it.
            plan.Steps[0].DishId.ShouldBe(CookPlan.RiceDishId, where);
            plan.Steps[0].StartMinute.ShouldBe(0, where);
            plan.Steps.ShouldAllBe(s => s.StartMinute >= 0, where);

            // Every step is there exactly once, and the plan is in start order.
            plan.Steps.Count.ShouldBe(1 + dishes.Sum(d => d.Steps.Count), where);
            plan.Steps.Select(s => s.StartMinute).ShouldBe(plan.Steps.Select(s => s.StartMinute).Order().ToArray(), where);

            // One cook: no two hands-on stretches overlap.
            var active = plan.Steps.Where(s => s.ActiveMinutes > 0).ToArray();
            for (var i = 0; i < active.Length; i++)
            {
                for (var j = i + 1; j < active.Length; j++)
                {
                    var overlap = active[i].StartMinute < active[j].StartMinute + active[j].ActiveMinutes
                        && active[j].StartMinute < active[i].StartMinute + active[i].ActiveMinutes;
                    overlap.ShouldBeFalse($"{where}: hands overlap between {active[i]} and {active[j]}");
                }
            }

            // Inside a dish, step by step in order, each after the previous one is done.
            foreach (var dish in dishes)
            {
                var mine = plan.Steps.Where(s => s.DishId == dish.Id).ToArray();
                mine.Select(s => s.Text).ShouldBe(dish.Steps.OrderBy(s => s.Order).Select(s => s.Text).ToArray(), $"{where}: {dish.Id}");
                for (var k = 1; k < mine.Length; k++)
                {
                    mine[k].StartMinute.ShouldBeGreaterThanOrEqualTo(End(mine[k - 1]), $"{where}: {dish.Id} step {k}");
                }
            }

            // The meal ends when its last step ends, and no earlier than the rice or the longest dish.
            plan.MealTimeMinutes.ShouldBe(plan.Steps.Max(End), where);
            plan.MealTimeMinutes.ShouldBeGreaterThanOrEqualTo(Config.RiceActiveMinutes + Config.RicePassiveMinutes, where);
            foreach (var dish in dishes)
            {
                plan.MealTimeMinutes.ShouldBeGreaterThanOrEqualTo(Total(dish), $"{where}: {dish.Id}");
            }
        }
    }

    [Fact]
    public void Hot_dishes_end_after_the_cold_part_and_close_to_the_end_of_the_meal()
    {
        var scheduler = new CookPlanScheduler(Config);

        for (ulong seed = 0; seed < 10_000; seed++)
        {
            var dishes = RandomMeal(new SeededRandomSource(seed + 77));
            var plan = scheduler.Schedule(dishes);
            var coldOnly = scheduler.Schedule(dishes.Where(d => !d.ServeHot).ToArray()).MealTimeMinutes;
            var hot = dishes.Where(d => d.ServeHot && d.Steps.Count > 0)
                .Select(d => (Dish: d, End: plan.Steps.Where(s => s.DishId == d.Id).Max(End)))
                .ToArray();
            var where = $"seed {seed}";

            foreach (var (dish, end) in hot)
            {
                // A hot dish is never done before the cold dishes and rice are.
                end.ShouldBeGreaterThanOrEqualTo(coldOnly, $"{where}: {dish.Id}");

                // One cook finishes hot dishes one after another: it ends at most as far from the end of the meal
                // as the total time of the hot dishes that end after it.
                var laterTotal = hot.Where(other => other.End > end).Sum(other => Total(other.Dish));
                (plan.MealTimeMinutes - end).ShouldBeLessThanOrEqualTo(laterTotal, $"{where}: {dish.Id}");
            }

            // The last hot dish to finish ends exactly when the meal does, when it is the one that sets the meal time.
            if (hot.Length > 0 && hot.Max(h => h.End) >= coldOnly)
            {
                plan.MealTimeMinutes.ShouldBe(hot.Max(h => h.End), where);
            }
        }
    }

    [Fact]
    public void Hot_dishes_stay_within_ten_minutes_of_the_end_when_the_dishes_after_them_are_quick()
    {
        // The SPEC's "finish in the last 10 minutes": true whenever the hot dishes that follow take at most 10 minutes.
        var scheduler = new CookPlanScheduler(Config);
        var soup = TestData.Dish("canh") with { Steps = [new Step(1, "hầm", 10, 40, [])] };
        var friedMain = TestData.Dish("mặn") with { ServeHot = true, Steps = [new Step(1, "xào", 7, 0, [])] };
        var greens = TestData.Dish("rau") with { ServeHot = true, Steps = [new Step(1, "xào", 3, 0, [])] };

        var plan = scheduler.Schedule([soup, friedMain, greens]);

        foreach (var hot in new[] { "mặn", "rau" })
        {
            var end = plan.Steps.Where(s => s.DishId == hot).Max(End);
            (plan.MealTimeMinutes - end).ShouldBeLessThanOrEqualTo(10, hot);
        }

        // The soup ends at 55; the 7-minute stir-fry takes 48-55, the 3-minute greens must wait for the hands: 55-58.
        plan.MealTimeMinutes.ShouldBe(58);
    }

    [Fact]
    public void The_plan_does_not_depend_on_the_order_the_dishes_are_given_in()
    {
        var scheduler = new CookPlanScheduler(Config);

        for (ulong seed = 0; seed < 3_000; seed++)
        {
            var random = new SeededRandomSource(seed + 5);
            var dishes = RandomMeal(random);
            var shuffled = dishes.OrderBy(_ => random.NextInt(1_000_000)).ToArray();

            scheduler.Schedule(shuffled).Steps.ShouldBe(scheduler.Schedule(dishes).Steps, $"seed {seed}");
        }
    }

    [Fact]
    public void The_minimum_never_decreases_when_a_dish_is_added_and_no_plan_beats_it()
    {
        // The spinner relies on this lower bound to tell when a time limit is already lost.
        var scheduler = new CookPlanScheduler(Config);

        for (ulong seed = 0; seed < 30_000; seed++)
        {
            var random = new SeededRandomSource(seed + 11);
            var dishes = RandomMeal(random, maxDishes: 5);
            var extra = RandomDish(random, "extra");
            Dish[] larger = [.. dishes, extra];

            var minimum = scheduler.MinimumMealMinutes(dishes);

            scheduler.MinimumMealMinutes(larger).ShouldBeGreaterThanOrEqualTo(minimum, $"seed {seed}: minimum shrank");
            scheduler.EstimateMealMinutes(dishes).ShouldBeGreaterThanOrEqualTo(minimum, $"seed {seed}: estimate below minimum");
            scheduler.EstimateMealMinutes(larger).ShouldBeGreaterThanOrEqualTo(minimum, $"seed {seed}: larger estimate below minimum");
        }
    }

    [Fact]
    public void The_minimum_is_rice_or_the_longest_dish_or_all_hands_on_time_added_up()
    {
        var scheduler = new CookPlanScheduler(Config);

        scheduler.MinimumMealMinutes([]).ShouldBe(35);                                                          // rice 5 + 30
        scheduler.MinimumMealMinutes([Chain("a", (10, 100))]).ShouldBe(110);                                     // the dish is longest
        scheduler.MinimumMealMinutes([Chain("a", (20, 0)), Chain("b", (20, 0)), Chain("c", (20, 0))]).ShouldBe(65); // 5 + 60 hands-on
        scheduler.MinimumMealMinutes([Chain("a", (2, 3))]).ShouldBe(35);
    }

    [Fact]
    public void The_estimate_is_a_greedy_plan_and_may_shrink_when_a_dish_is_added()
    {
        // Documented, not desired: this is why the spinner uses MinimumMealMinutes. Found by search: the extra dish
        // happens to fill a wait and lets a long chain start earlier.
        var scheduler = new CookPlanScheduler(Config);
        var d0 = Chain("d0", (5, 11), (12, 0), (12, 4));
        var d1 = Chain("d1", (5, 30), (7, 49), (10, 24));
        var extra = Chain("extra", (10, 0), (14, 0), (6, 40), (11, 32));

        scheduler.EstimateMealMinutes([d0, d1]).ShouldBe(140);
        scheduler.EstimateMealMinutes([d0, d1, extra]).ShouldBe(132);
        scheduler.MinimumMealMinutes([d0, d1, extra]).ShouldBeLessThanOrEqualTo(132);
    }

    private static Dish Chain(string id, params (int Active, int Passive)[] steps) => TestData.Dish(id) with
    {
        Steps = steps.Select((s, i) => new Step(i + 1, "x", s.Active, s.Passive, [])).ToArray(),
    };

    [Fact]
    public void The_same_dishes_always_give_the_same_plan()
    {
        var scheduler = new CookPlanScheduler(Config);
        var dishes = RandomMeal(new SeededRandomSource(123));

        scheduler.Schedule(dishes).Steps.ShouldBe(scheduler.Schedule(dishes).Steps);
    }
}
