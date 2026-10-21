using MamCom.Core.Cooking;
using MamCom.Core.Households;
using MamCom.Core.Models;
using MamCom.Core.Portions;
using MamCom.Core.Tests.Spinning;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Cooking;

/// <summary>
/// The stoves of the kitchen (ADR-028): a dish that is cooked holds one stove from the start of its first step to the end of its last,
/// and no more such dishes are under way at once than the household has stoves. A tossed salad needs none; the rice cooker is no stove.
/// </summary>
public class CookPlanSchedulerStoveTests
{
    private static readonly CookingConfig Config = CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"));

    private static CookPlanScheduler Scheduler() => new(Config);

    private static Dish Cook(string id, params (int Active, int Passive)[] steps) => With(id, hot: false, CookingMethod.Simmer, steps);

    private static Dish Hot(string id, params (int Active, int Passive)[] steps) => With(id, hot: true, CookingMethod.StirFry, steps);

    private static Dish Tossed(string id, params (int Active, int Passive)[] steps) => With(id, hot: false, CookingMethod.Toss, steps);

    private static Dish With(string id, bool hot, CookingMethod method, (int Active, int Passive)[] steps) =>
        TestData.Dish(id) with
        {
            ServeHot = hot,
            CookingMethod = method,
            Steps = [.. steps.Select((s, i) => new Step(i + 1, $"{id} {i + 1}", s.Active, s.Passive, []))],
        };

    private static int End(CookPlanStep step) => step.StartMinute + step.ActiveMinutes + (step.TimerMinutes ?? 0);

    /// <summary>The minutes a dish is under way in the plan: from its first step to the end of its last.</summary>
    private static (int Start, int End) Span(CookPlan plan, string dishId)
    {
        var mine = plan.Steps.Where(s => s.DishId == dishId).ToArray();
        return (mine.Min(s => s.StartMinute), mine.Max(End));
    }

    // ---- what a stove changes ------------------------------------------------------------------------------------------------

    [Fact]
    public void With_one_stove_two_dishes_that_are_cooked_go_one_after_the_other_the_longer_one_first()
    {
        Dish[] dishes = [Cook("canh", (10, 40)), Cook("kho", (5, 60))];

        var free = Scheduler().Schedule(dishes);
        var one = Scheduler().Schedule(dishes, stoves: 1);

        free.MealTimeMinutes.ShouldBe(70);   // the braise from minute 5 to 70, the soup from 10 to 60 beside it
        Span(one, "kho").ShouldBe((5, 70));
        Span(one, "canh").ShouldBe((70, 120));
        one.MealTimeMinutes.ShouldBe(120);
    }

    [Fact]
    public void Two_stoves_hold_two_dishes_at_once_and_the_third_waits_for_the_first_stove_given_back()
    {
        Dish[] dishes = [Cook("a", (5, 60)), Cook("b", (5, 40)), Cook("c", (5, 20))];

        var plan = Scheduler().Schedule(dishes, stoves: 2);

        Span(plan, "a").ShouldBe((5, 70));
        Span(plan, "b").ShouldBe((10, 55));
        Span(plan, "c").Start.ShouldBe(55);   // b gives its stove back first
    }

    [Fact]
    public void As_many_stoves_as_dishes_is_the_same_plan_as_no_limit()
    {
        Dish[] dishes = [Cook("a", (5, 60)), Cook("b", (5, 40)), Hot("c", (8, 0))];

        Scheduler().Schedule(dishes, stoves: 3).Steps.ShouldBe(Scheduler().Schedule(dishes).Steps);
    }

    [Fact]
    public void A_tossed_salad_needs_no_stove_and_is_made_while_the_one_stove_is_busy()
    {
        var plan = Scheduler().Schedule([Cook("kho", (5, 60)), Tossed("goi", (10, 0))], stoves: 1);

        Span(plan, "kho").ShouldBe((5, 70));
        Span(plan, "goi").ShouldBe((10, 20));
        plan.MealTimeMinutes.ShouldBe(70);
    }

    [Fact]
    public void The_rice_cooker_is_no_stove()
    {
        var plan = Scheduler().Schedule([Cook("canh", (10, 0))], stoves: 1);

        Span(plan, "canh").Start.ShouldBe(Config.RiceActiveMinutes);   // while the rice cooks, on the one stove
    }

    [Fact]
    public void A_dish_that_opens_with_a_wait_holds_its_stove_from_that_wait_on()
    {
        // Marinated from minute 0, then cooked: with one stove the other dish waits for the whole of it.
        var plan = Scheduler().Schedule([Cook("ga", (0, 30), (10, 0)), Cook("rau", (5, 5))], stoves: 1);

        Span(plan, "ga").ShouldBe((0, 40));
        Span(plan, "rau").Start.ShouldBe(40);
    }

    [Fact]
    public void A_dish_served_hot_waits_for_a_stove_and_still_ends_the_meal()
    {
        var plan = Scheduler().Schedule([Cook("ham", (5, 60), (5, 0)), Hot("xao", (8, 0))], stoves: 1);

        Span(plan, "ham").ShouldBe((5, 75));
        Span(plan, "xao").ShouldBe((75, 83));
        plan.MealTimeMinutes.ShouldBe(83);
    }

    [Fact]
    public void Two_dishes_served_hot_on_one_stove_go_one_after_the_other()
    {
        var plan = Scheduler().Schedule([Hot("dai", (10, 0)), Hot("ngan", (5, 0))], stoves: 1);

        var (longStart, longEnd) = Span(plan, "dai");
        var (shortStart, shortEnd) = Span(plan, "ngan");
        (longEnd <= shortStart || shortEnd <= longStart).ShouldBeTrue();
    }

    // ---- the lower bound ---------------------------------------------------------------------------------------------------

    [Fact]
    public void The_lower_bound_shares_the_minutes_on_the_stoves_out_over_the_stoves_and_leaves_tossed_dishes_out()
    {
        Dish[] three = [Cook("a", (10, 20)), Cook("b", (10, 20)), Cook("c", (10, 20))];

        Scheduler().MinimumMealMinutes(three, stoves: 1).ShouldBe(90);
        Scheduler().MinimumMealMinutes(three, stoves: 2).ShouldBe(45);
        Scheduler().MinimumMealMinutes(three).ShouldBe(35);   // the rice
        Scheduler().MinimumMealMinutes([Tossed("a", (10, 20)), Tossed("b", (10, 20))], stoves: 1).ShouldBe(35);
    }

    [Fact]
    public void Fewer_than_one_stove_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Scheduler().Schedule([], stoves: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => Scheduler().MinimumMealMinutes([], stoves: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => Scheduler().EstimateMealMinutes([], stoves: -1));
    }

    [Fact]
    public void Every_way_of_cooking_but_tossing_needs_a_stove()
    {
        Enum.GetValues<CookingMethod>().Where(m => !CookPlanScheduler.NeedsStove(TestData.Dish("x") with { CookingMethod = m }))
            .ShouldBe([CookingMethod.Toss]);
        Should.Throw<ArgumentNullException>(() => CookPlanScheduler.NeedsStove(null!));
    }

    // ---- many random meals -------------------------------------------------------------------------------------------------

    private static Dish RandomDish(IRandomSource random, string id)
    {
        var steps = Enumerable.Range(0, 1 + random.NextInt(4)).Select(i =>
        {
            var active = random.NextInt(4) == 0 ? 0 : 1 + random.NextInt(15);
            var passive = random.NextInt(3) == 0 ? 0 : 1 + random.NextInt(60);
            return (active, passive);
        }).ToArray();
        var method = random.NextInt(5) == 0 ? CookingMethod.Toss : CookingMethod.Simmer;

        return With(id, hot: random.NextInt(3) == 0, method, steps);
    }

    [Fact]
    public void Five_thousand_random_meals_on_one_to_four_stoves_never_cook_more_dishes_at_once_than_there_are_stoves()
    {
        var scheduler = Scheduler();

        for (ulong seed = 0; seed < 5_000; seed++)
        {
            var random = new SeededRandomSource(seed);
            var dishes = Enumerable.Range(0, random.NextInt(6)).Select(i => RandomDish(random, $"d{i}")).ToArray();
            var stoves = 1 + (int)(seed % 4);
            var plan = scheduler.Schedule(dishes, stoves);
            var where = $"seed {seed}, {stoves} stoves";

            // Every step once, rice first, hands never on two things at once.
            plan.Steps.Count.ShouldBe(1 + dishes.Sum(d => d.Steps.Count), where);
            plan.Steps[0].DishId.ShouldBe(CookPlan.RiceDishId, where);
            var active = plan.Steps.Where(s => s.ActiveMinutes > 0).OrderBy(s => s.StartMinute).ToArray();
            for (var i = 1; i < active.Length; i++)
            {
                active[i].StartMinute.ShouldBeGreaterThanOrEqualTo(active[i - 1].StartMinute + active[i - 1].ActiveMinutes, where);
            }

            // At the start of every dish that holds a stove, no more such dishes are under way than there are stoves.
            var spans = dishes.Where(CookPlanScheduler.NeedsStove).Where(d => d.Steps.Count > 0).Select(d => Span(plan, d.Id)).ToArray();
            foreach (var (start, _) in spans)
            {
                spans.Count(span => span.Start <= start && start < span.End).ShouldBeLessThanOrEqualTo(stoves, $"{where}: at minute {start}");
            }

            // The lower bound holds, and grows with the dishes.
            var minimum = scheduler.MinimumMealMinutes(dishes, stoves);
            plan.MealTimeMinutes.ShouldBeGreaterThanOrEqualTo(minimum, where);
            plan.MealTimeMinutes.ShouldBe(scheduler.EstimateMealMinutes(dishes, stoves), where);
            scheduler.MinimumMealMinutes([.. dishes, RandomDish(random, "them")], stoves).ShouldBeGreaterThanOrEqualTo(minimum, where);

            // The plan does not depend on the order of the dishes.
            scheduler.Schedule([.. dishes.Reverse()], stoves).Steps.ShouldBe(plan.Steps, where);
        }
    }

    // ---- where the number comes from ---------------------------------------------------------------------------------------

    [Fact]
    public void A_household_that_did_not_say_has_the_stoves_of_the_config_and_one_that_did_has_its_own()
    {
        var household = SmallHome();

        Config.DefaultStoves.ShouldBe(2);
        Config.MaxStoves.ShouldBe(4);
        Config.StovesOf(household).ShouldBe(2);
        Config.StovesOf(household with { Stoves = 1 }).ShouldBe(1);
        Scheduler().StovesOf(household with { Stoves = 3 }).ShouldBe(3);
        Should.Throw<ArgumentNullException>(() => Config.StovesOf(null!));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(3, 2)]
    public void A_config_with_no_stove_or_with_a_most_below_the_usual_is_refused(int usual, int most)
    {
        Should.Throw<ArgumentException>(() => (Config with { DefaultStoves = usual, MaxStoves = most }).Validate());
    }

    [Fact]
    public void The_spinner_plans_the_time_limit_for_the_stoves_of_the_context()
    {
        var estimator = new SumOfStepsEstimator();

        Spinner(estimator: estimator).Spin(Context(SmallHome(availableMinutes: 500)) with { Stoves = 3 });

        estimator.StovesAsked.ShouldNotBeEmpty();
        estimator.StovesAsked.ShouldAllBe(stoves => stoves == 3);
    }

    [Fact]
    public void The_cooking_mode_plans_the_steps_for_the_stoves_of_the_household()
    {
        var portions = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));
        Dish[] dishes = [Cook("canh", (10, 40)), Cook("kho", (5, 60))];
        var desk = new CookingDesk(portions, Config);
        var ingredients = new Dictionary<string, Ingredient>();

        desk.Guide(SmallHome() with { Stoves = 1 }, dishes, ingredients).MealMinutes.ShouldBe(120);
        desk.Guide(SmallHome() with { Stoves = 2 }, dishes, ingredients).MealMinutes.ShouldBe(70);
    }

    // ---- the household keeps it --------------------------------------------------------------------------------------------

    [Fact]
    public void The_editor_keeps_the_stoves_of_a_saved_household_and_writes_them_back()
    {
        var portions = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));
        var saved = SmallHome() with { Stoves = 3 };

        var editor = HouseholdEditor.From(saved, portions).ShouldNotBeNull();

        editor.Stoves.ShouldBe(3);
        editor.Stoves = 1;
        editor.ToHousehold().Stoves.ShouldBe(1);
        new HouseholdEditor(portions).Stoves.ShouldBeNull();   // not said yet: the config has the usual number
    }

    [Fact]
    public void A_household_keeps_its_stoves_in_its_file_and_one_saved_before_there_were_stoves_reads_as_not_said()
    {
        var withStoves = SmallHome() with { Stoves = 3 };

        var json = System.Text.Json.JsonSerializer.Serialize(withStoves, MamCom.Core.Json.CoreJsonContext.Default.Household);
        var back = System.Text.Json.JsonSerializer.Deserialize(json, MamCom.Core.Json.CoreJsonContext.Default.Household)!;
        var older = System.Text.Json.JsonSerializer.Serialize(SmallHome(), MamCom.Core.Json.CoreJsonContext.Default.Household);

        json.ShouldContain("\"stoves\": 3");
        back.Stoves.ShouldBe(3);
        older.ShouldNotContain("stoves");   // a household that never said writes the file it wrote before
        System.Text.Json.JsonSerializer.Deserialize(older, MamCom.Core.Json.CoreJsonContext.Default.Household)!.Stoves.ShouldBeNull();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(0, false)]
    [InlineData(-2, false)]
    public void A_saved_household_is_usable_with_no_number_of_stoves_or_at_least_one(int? stoves, bool usable)
    {
        var portions = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

        HouseholdEditor.IsUsable(SmallHome() with { Stoves = stoves }, portions).ShouldBe(usable);
    }
}

public class CookingConfigOfAnOlderBundleTests
{
    [Fact]
    public void A_bundle_made_before_the_stoves_were_known_still_opens_with_two_stoves_and_a_choice_up_to_four()
    {
        var older = CookingConfig.Parse("""{ "riceActiveMinutes": 5, "ricePassiveMinutes": 30, "riceStepText": "Vo gạo." }""");

        older.DefaultStoves.ShouldBe(2);
        older.MaxStoves.ShouldBe(4);
        Should.NotThrow(older.Validate);
    }
}
