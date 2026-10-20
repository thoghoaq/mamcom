using System.Text.Json;
using MamCom.Core.Cooking;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Core.Portions;
using static MamCom.Core.Tests.TestData;

namespace MamCom.Core.Tests.Cooking;

public class CookingDeskTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 21, 12, 0, 0, TimeSpan.FromHours(7));

    private static readonly PortionConfig Portions = PortionConfig.Parse(TestFiles.Config("portion-defaults.json"));

    private static readonly CookingConfig Cooking = CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"));

    private static CookingDesk Desk() => new(Portions, Cooking);

    // ---- test-only fixture: two adults make 2.0 servings and 160 g of raw rice -----------------------------------------------

    private static Ingredient Item(string id, string name, string? southName = null, MarketUnit? unit = null) => Ingredient(id) with
    {
        Names = southName is null
            ? new Dictionary<string, string> { [RegionKeys.Default] = name }
            : new Dictionary<string, string> { [RegionKeys.Default] = name, ["south"] = southName },
        MarketUnit = unit is null ? new Dictionary<string, MarketUnit>() : new Dictionary<string, MarketUnit> { [RegionKeys.Default] = unit },
    };

    private static Dictionary<string, Ingredient> Pantry() => Catalog(
        Item("thit", "Thịt", southName: "Thịt heo"),
        Item("trung", "Trứng", unit: new MarketUnit("quả", 55, 1)),
        Item("hanh", "Hành"),
        Item("rau", "Rau"),
        Item("gao", "Gạo"));

    private static Step Step(int order, string text, int active, int passive, params string[] ingredients) => new(order, text, active, passive, ingredients);

    private static DishIngredient Piece(string id, double grams) => new(id, grams, ScaleKind.Integer, 1, null);

    // Steps: 3 + 10 minutes with an egg, 6 + 15 with the pork and the onion, 8 + 0 with the pork again.
    private static Dish Braise => Dish("thit-kho", Line("thit", 100), Piece("trung", 50), Line("hanh", 10)) with
    {
        Name = "Thịt kho",
        Slot = DishSlot.Main,
        Steps =
        [
            Step(1, "Luộc trứng.", 3, 10, "trung"),
            Step(2, "Ướp thịt.", 6, 15, "thit", "hanh"),
            Step(3, "Kho thịt.", 8, 0, "thit", "trung"),
        ],
    };

    private static Dish Greens => Dish("rau-xao", Line("rau", 80)) with
    {
        Name = "Rau xào",
        Slot = DishSlot.Vegetable,
        ServeHot = true,
        Steps = [Step(1, "Xào rau.", 4, 0, "rau")],
    };

    private static Household TwoAdults(params MemberTag[] tags) => Home(BowlSize.Medium, Person("A", AgeGroup.Adult, 2, tags), Person("B", AgeGroup.Adult, 2));

    private static CookingGuide Guide(Household? household = null, IReadOnlyList<Dish>? dishes = null, IReadOnlyDictionary<string, Ingredient>? catalog = null) =>
        Desk().Guide(household ?? TwoAdults(), dishes ?? [Braise, Greens], catalog ?? Pantry());

    private static MealTray TheTray => new(new DateOnly(2026, 10, 21), [new SlotResult(DishSlot.Main, "thit-kho", false), new SlotResult(DishSlot.Vegetable, "rau-xao", false)]);

    private static string Key => TrayKeys.Of(TheTray);

    private static CookingStep StepOf(CookingGuide guide, string dishId, string text) => guide.Steps.Single(s => s.DishId == dishId && s.Text == text);

    // ---- the guide ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_guide_starts_with_the_rice_which_says_how_much_to_wash_and_has_a_timer()
    {
        var guide = Guide();

        var rice = guide.Steps[0];
        rice.IsRice.ShouldBeTrue();
        rice.DishName.ShouldBeEmpty();
        rice.Slot.ShouldBeNull();
        rice.Text.ShouldBe("Vo gạo và bắc nồi cơm.");
        rice.ActiveMinutes.ShouldBe(5);
        rice.TimerMinutes.ShouldBe(30);
        var line = rice.Ingredients.Single();
        line.Line.IngredientId.ShouldBe("gao");
        line.Line.Name.ShouldBe("Gạo");
        line.Line.GramsEdible.ShouldBe(160);   // 4 bowls of 40 g
        line.ShowAmount.ShouldBeTrue();
    }

    [Fact]
    public void The_steps_are_numbered_in_order_and_the_meal_time_is_the_one_of_the_plan()
    {
        var guide = Guide();

        guide.Steps.Select(s => s.Index).ShouldBe(Enumerable.Range(0, guide.Steps.Count));
        guide.Steps.Count.ShouldBe(1 + 3 + 1);   // rice, the three steps of the braise, the greens
        guide.MealMinutes.ShouldBe(new MamCom.Core.Cooking.CookPlanScheduler(Cooking).Schedule([Braise, Greens]).MealTimeMinutes);
    }

    [Fact]
    public void The_steps_of_a_dish_keep_their_order_and_say_whose_they_are_and_what_they_ask_of_the_cook()
    {
        var guide = Guide();

        var braise = guide.Steps.Where(s => s.DishId == "thit-kho").ToArray();
        braise.Select(s => s.Text).ShouldBe(["Luộc trứng.", "Ướp thịt.", "Kho thịt."]);
        braise.ShouldAllBe(s => s.DishName == "Thịt kho" && s.Slot == DishSlot.Main && !s.IsRice);
        braise.Select(s => (s.ActiveMinutes, s.TimerMinutes)).ShouldBe([(3, 10), (6, 15), (8, 0)]);   // no timer is 0
        guide.Steps.Single(s => s.DishId == "rau-xao").Slot.ShouldBe(DishSlot.Vegetable);
    }

    [Fact]
    public void A_step_says_the_amount_of_an_ingredient_the_first_time_the_dish_names_it_and_only_the_name_after()
    {
        var guide = Guide();

        var marinate = StepOf(guide, "thit-kho", "Ướp thịt.").Ingredients;
        var braise = StepOf(guide, "thit-kho", "Kho thịt.").Ingredients;

        marinate.Select(i => (i.Line.IngredientId, i.ShowAmount)).ShouldBe([("thit", true), ("hanh", true)]);
        braise.Select(i => (i.Line.IngredientId, i.ShowAmount)).ShouldBe([("thit", false), ("trung", false)]);   // pork and egg were named before
        braise[0].Line.GramsEdible.ShouldBe(200, 1e-9);   // the amount is still there for a screen to use
    }

    [Fact]
    public void Two_dishes_that_use_the_same_ingredient_each_say_the_amount_of_their_own_the_first_time()
    {
        var greens = Greens with
        {
            Ingredients = [Line("rau", 80), Line("hanh", 5)],
            Steps = [Step(1, "Xào rau.", 4, 0, "rau", "hanh")],
        };

        var guide = Guide(dishes: [Braise, greens]);

        var inBraise = StepOf(guide, "thit-kho", "Ướp thịt.").Ingredients.Single(i => i.Line.IngredientId == "hanh");
        var inGreens = StepOf(guide, "rau-xao", "Xào rau.").Ingredients.Single(i => i.Line.IngredientId == "hanh");
        (inBraise.ShowAmount, inBraise.Line.GramsEdible).ShouldBe((true, 20.0));
        (inGreens.ShowAmount, inGreens.Line.GramsEdible).ShouldBe((true, 10.0));   // the onion of this dish, not the one of the other
    }

    [Fact]
    public void The_amounts_are_those_of_the_dish_for_the_household_with_pieces_counted_in_pieces()
    {
        var guide = Guide();

        var egg = StepOf(guide, "thit-kho", "Luộc trứng.").Ingredients.Single().Line;
        var pork = StepOf(guide, "thit-kho", "Ướp thịt.").Ingredients[0].Line;
        var onion = StepOf(guide, "thit-kho", "Ướp thịt.").Ingredients[1].Line;

        egg.Units.ShouldBe(2);   // 50 g × 2.0 servings = 100 g needs 2 eggs of 55 g
        egg.UnitDisplay.ShouldBe("quả");
        egg.GramsEdible.ShouldBe(110, 1e-9);
        pork.GramsEdible.ShouldBe(200, 1e-9);
        pork.Units.ShouldBeNull();
        onion.GramsEdible.ShouldBe(20, 1e-9);
    }

    [Fact]
    public void A_line_counted_in_pieces_uses_the_market_unit_of_the_region_of_the_household()
    {
        var egg = Item("trung", "Trứng") with
        {
            MarketUnit = new Dictionary<string, MarketUnit> { ["north"] = new("quả", 55, 1), ["south"] = new("trái", 60, 1) },
        };
        var catalog = Catalog(Item("thit", "Thịt"), egg, Item("hanh", "Hành"), Item("rau", "Rau"), Item("gao", "Gạo"));

        var north = StepOf(Guide(TwoAdults() with { Region = Region.North }, catalog: catalog), "thit-kho", "Luộc trứng.").Ingredients.Single().Line;
        var south = StepOf(Guide(TwoAdults(), catalog: catalog), "thit-kho", "Luộc trứng.").Ingredients.Single().Line;

        // 50 g × 2.0 servings = 100 g: two eggs of 55 g in the north, two of 60 g in the south.
        (north.UnitDisplay, north.GramsEdible).ShouldBe(("quả", 110));
        (south.UnitDisplay, south.GramsEdible).ShouldBe(("trái", 120));
    }

    [Fact]
    public void A_main_dish_is_scaled_to_the_protein_servings_and_a_vegetable_dish_to_the_vegetable_servings()
    {
        var guide = Guide(TwoAdults(MemberTag.MoreProtein));

        StepOf(guide, "thit-kho", "Ướp thịt.").Ingredients[0].Line.GramsEdible.ShouldBe(220, 1e-9);   // 100 g × (1.2 + 1.0)
        StepOf(guide, "rau-xao", "Xào rau.").Ingredients.Single().Line.GramsEdible.ShouldBe(160, 1e-9);
    }

    [Fact]
    public void Names_follow_the_region_of_the_household()
    {
        var south = Guide(TwoAdults());   // Home() is in the south

        StepOf(south, "thit-kho", "Ướp thịt.").Ingredients[0].Line.Name.ShouldBe("Thịt heo");
        StepOf(Guide(TwoAdults() with { Region = Region.North }), "thit-kho", "Ướp thịt.").Ingredients[0].Line.Name.ShouldBe("Thịt");
    }

    [Fact]
    public void An_ingredient_named_twice_in_a_step_is_listed_once_and_one_the_recipe_does_not_have_is_left_out()
    {
        var dish = Braise with { Steps = [Step(1, "Làm.", 5, 0, "thit", "thit", "khong-co-trong-cong-thuc")] };

        var guide = Guide(dishes: [dish]);

        StepOf(guide, "thit-kho", "Làm.").Ingredients.Select(i => i.Line.IngredientId).ShouldBe(["thit"]);
    }

    [Fact]
    public void A_dish_without_steps_adds_nothing_and_no_dishes_leave_only_the_rice()
    {
        Guide(dishes: [Braise with { Steps = [] }]).Steps.Select(s => s.DishId).ShouldBe(["com"]);
        Guide(dishes: []).Steps.Select(s => s.DishId).ShouldBe(["com"]);
    }

    [Fact]
    public void Without_the_rice_ingredient_or_without_rice_to_cook_the_rice_step_names_nothing()
    {
        Guide(catalog: Catalog(Item("thit", "Thịt"), Item("trung", "Trứng", unit: new MarketUnit("quả", 55, 1)), Item("hanh", "Hành"), Item("rau", "Rau")))
            .Steps[0].Ingredients.ShouldBeEmpty();
        Guide(new Household(Region.South, BowlSize.Medium, [])).Steps[0].Ingredients.ShouldBeEmpty();
    }

    [Fact]
    public void A_dish_with_an_ingredient_missing_from_the_catalog_is_a_data_error()
    {
        Should.Throw<InvalidOperationException>(() => Guide(catalog: Catalog(Item("gao", "Gạo"))));
    }

    // ---- opening ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Nothing_saved_starts_at_the_first_step_with_no_timers()
    {
        var state = Desk().Open(null, TheTray, Guide());

        state.TrayKey.ShouldBe(Key);
        state.StepIndex.ShouldBe(0);
        state.Timers.ShouldBeEmpty();
    }

    [Fact]
    public void What_was_saved_for_this_tray_is_there_again()
    {
        var guide = Guide();
        var timer = new CookTimer(0, Noon.AddMinutes(30), true);
        var saved = new CookingState(Key, 3, [timer]);

        var state = Desk().Open(saved, TheTray, guide);

        state.StepIndex.ShouldBe(3);
        state.Timers.ShouldBe([timer]);
    }

    [Fact]
    public void What_was_saved_for_another_tray_is_not()
    {
        var saved = new CookingState("2026-10-20|cu-khac", 3, [new CookTimer(0, Noon, false)]);

        var state = Desk().Open(saved, TheTray, Guide());

        state.StepIndex.ShouldBe(0);
        state.Timers.ShouldBeEmpty();
        state.TrayKey.ShouldBe(Key);
    }

    [Fact]
    public void A_saved_step_outside_the_guide_is_brought_back_inside_it()
    {
        var guide = Guide();

        Desk().Open(new CookingState(Key, 99, []), TheTray, guide).StepIndex.ShouldBe(guide.Steps.Count - 1);
        Desk().Open(new CookingState(Key, 0, []), TheTray, Guide(dishes: [])).StepIndex.ShouldBe(0);
    }

    [Fact]
    public void A_timer_whose_step_is_gone_or_has_no_timer_is_dropped()
    {
        var guide = Guide();
        var noTimer = guide.Steps.First(s => s.TimerMinutes == 0).Index;
        var good = new CookTimer(0, Noon, false);
        var saved = new CookingState(Key, 0, [good, new CookTimer(noTimer, Noon, false), new CookTimer(99, Noon, false)]);

        Desk().Open(saved, TheTray, guide).Timers.ShouldBe([good]);
    }

    [Fact]
    public void A_state_that_cannot_be_used_is_the_same_as_none()
    {
        var state = Desk().Open(new CookingState(null!, 5, null!), TheTray, Guide());

        state.StepIndex.ShouldBe(0);
        state.Timers.ShouldBeEmpty();
        state.TrayKey.ShouldBe(Key);
    }

    [Fact]
    public void A_state_that_cannot_be_used_is_the_same_as_none_even_when_it_is_for_this_tray()
    {
        var saved = new CookingState(Key, 3, [new CookTimer(0, Noon, false), new CookTimer(0, Noon, true)]);   // two timers on one step

        var state = Desk().Open(saved, TheTray, Guide());

        state.StepIndex.ShouldBe(0);
        state.Timers.ShouldBeEmpty();
    }

    // ---- moving between steps ----------------------------------------------------------------------------------------------

    [Fact]
    public void Next_and_previous_move_one_step_and_stop_at_the_ends()
    {
        var desk = Desk();
        var guide = Guide();
        var last = guide.Steps.Count - 1;
        var first = new CookingState(Key, 0, []);

        desk.Next(first, guide).StepIndex.ShouldBe(1);
        desk.Previous(first, guide).StepIndex.ShouldBe(0);
        desk.Next(new CookingState(Key, last, []), guide).StepIndex.ShouldBe(last);
        desk.Previous(new CookingState(Key, 2, []), guide).StepIndex.ShouldBe(1);
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(2, 2)]
    [InlineData(40, 4)]
    public void Going_to_a_step_outside_the_guide_goes_to_the_nearest_one(int asked, int expected)
    {
        Desk().GoTo(new CookingState(Key, 1, []), Guide(), asked).StepIndex.ShouldBe(expected);
    }

    [Fact]
    public void A_guide_with_no_step_at_all_keeps_the_cook_on_step_zero_whatever_is_asked()
    {
        var none = new CookingGuide([], 0);

        Desk().GoTo(new CookingState(Key, 0, []), none, 3).StepIndex.ShouldBe(0);
        Desk().Next(new CookingState(Key, 0, []), none).StepIndex.ShouldBe(0);
        Desk().Open(new CookingState(Key, 5, [new CookTimer(0, Noon, false)]), TheTray, none).StepIndex.ShouldBe(0);
    }

    [Fact]
    public void Moving_does_not_touch_the_timers_or_the_tray()
    {
        var timers = new[] { new CookTimer(0, Noon, false) };

        var state = Desk().Next(new CookingState(Key, 0, timers), Guide());

        state.Timers.ShouldBe(timers);
        state.TrayKey.ShouldBe(Key);
    }

    // ---- timers ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_timer_started_on_a_step_ends_its_timer_minutes_from_now()
    {
        var guide = Guide();
        var marinate = StepOf(guide, "thit-kho", "Ướp thịt.").Index;

        var state = Desk().StartTimer(new CookingState(Key, 0, []), guide, marinate, Noon);

        state.Timers.ShouldBe([new CookTimer(marinate, Noon.AddMinutes(15), false)]);
        state.StepIndex.ShouldBe(0);
    }

    [Fact]
    public void Several_timers_run_at_once_one_for_each_step()
    {
        var guide = Guide();
        var desk = Desk();
        var state = desk.StartTimer(new CookingState(Key, 0, []), guide, 0, Noon);

        state = desk.StartTimer(state, guide, StepOf(guide, "thit-kho", "Luộc trứng.").Index, Noon.AddMinutes(1));

        state.Timers.Select(t => t.StepIndex).ShouldBe([0, StepOf(guide, "thit-kho", "Luộc trứng.").Index]);
    }

    [Fact]
    public void A_step_without_a_timer_cannot_have_one_and_one_that_has_it_does_not_start_again()
    {
        var guide = Guide();
        var desk = Desk();
        var noTimer = guide.Steps.First(s => s.TimerMinutes == 0).Index;
        var started = desk.StartTimer(new CookingState(Key, 0, []), guide, 0, Noon);

        desk.StartTimer(started, guide, noTimer, Noon).ShouldBeSameAs(started);
        desk.StartTimer(started, guide, 0, Noon.AddMinutes(5)).ShouldBeSameAs(started);   // it is not restarted
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void A_step_that_is_not_in_the_guide_cannot_have_a_timer(int stepIndex)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Desk().StartTimer(new CookingState(Key, 0, []), Guide(), stepIndex, Noon));
    }

    [Fact]
    public void Cancelling_takes_the_timer_away_and_leaves_the_others()
    {
        var desk = Desk();
        var state = new CookingState(Key, 0, [new CookTimer(0, Noon, false), new CookTimer(2, Noon, false)]);

        desk.CancelTimer(state, 0).Timers.Select(t => t.StepIndex).ShouldBe([2]);
        desk.CancelTimer(state, 4).Timers.Select(t => t.StepIndex).ShouldBe([0, 2], "nothing to cancel");
    }

    [Fact]
    public void The_timers_for_the_chips_are_sorted_by_when_they_end_and_say_how_long_is_left_rounded_up()
    {
        var guide = Guide();
        var state = new CookingState(Key, 0,
        [
            new CookTimer(StepOf(guide, "thit-kho", "Ướp thịt.").Index, Noon.AddMinutes(15), false),
            new CookTimer(0, Noon.AddSeconds(61.2), false),
        ]);

        var views = Desk().Timers(state, guide, Noon);

        views.Select(v => (v.StepIndex, v.DishId, v.RemainingSeconds, v.IsDone)).ShouldBe(
        [
            (0, "com", 62, false),                                                          // 61.2 s is shown as 62, so 0 means over
            (StepOf(guide, "thit-kho", "Ướp thịt.").Index, "thit-kho", 900, false),
        ]);
        views[0].IsRice.ShouldBeTrue();
        views[1].DishName.ShouldBe("Thịt kho");
        views[1].IsRice.ShouldBeFalse();
    }

    [Fact]
    public void A_timer_is_over_the_moment_its_time_comes_and_then_shows_no_seconds()
    {
        var guide = Guide();
        var state = new CookingState(Key, 0, [new CookTimer(0, Noon, false)]);

        Desk().Timers(state, guide, Noon.AddSeconds(-1)).Single().ShouldBe(new TimerView(0, "com", string.Empty, 1, false));
        Desk().Timers(state, guide, Noon).Single().IsDone.ShouldBeTrue();
        Desk().Timers(state, guide, Noon).Single().RemainingSeconds.ShouldBe(0);
        Desk().Timers(state, guide, Noon.AddMinutes(9)).Single().RemainingSeconds.ShouldBe(0);
    }

    [Fact]
    public void Timers_that_end_together_go_by_step_and_a_timer_for_a_step_that_is_gone_is_not_shown()
    {
        var guide = Guide();
        var state = new CookingState(Key, 0, [new CookTimer(2, Noon, false), new CookTimer(0, Noon, false), new CookTimer(99, Noon, false)]);

        Desk().Timers(state, guide, Noon).Select(v => v.StepIndex).ShouldBe([0, 2]);
    }

    // ---- the alarm ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Due_are_the_timers_that_ran_out_and_have_not_sounded_in_the_order_they_ended()
    {
        var state = new CookingState(Key, 0,
        [
            new CookTimer(1, Noon.AddMinutes(2), false),
            new CookTimer(3, Noon.AddMinutes(1), false),     // ends before step 1, though its step comes after
            new CookTimer(2, Noon.AddMinutes(1), true),      // sounded already
            new CookTimer(4, Noon.AddMinutes(10), false),    // still running
        ]);

        Desk().Due(state, Noon.AddMinutes(5)).ShouldBe([3, 1]);
        Desk().Due(state, Noon.AddMinutes(2)).ShouldBe([3, 1]);   // the minute it ends counts
        Desk().Due(state, Noon.AddMinutes(1.5)).ShouldBe([3]);
        Desk().Due(state, Noon).ShouldBeEmpty();
    }

    [Fact]
    public void Marking_a_timer_as_sounded_does_it_once_and_leaves_the_others()
    {
        var state = new CookingState(Key, 0, [new CookTimer(1, Noon, false), new CookTimer(2, Noon, false), new CookTimer(3, Noon, false)]);

        var marked = Desk().MarkAlerted(state, [1, 3, 9]);

        marked.Timers.Select(t => (t.StepIndex, t.Alerted)).ShouldBe([(1, true), (2, false), (3, true)]);
        Desk().Due(marked, Noon.AddMinutes(1)).ShouldBe([2]);
    }

    // ---- the state as it is kept -------------------------------------------------------------------------------------------

    [Fact]
    public void A_state_survives_being_written_and_read_back_with_the_minute_its_timers_end()
    {
        var state = new CookingState(Key, 2, [new CookTimer(0, Noon.AddMinutes(30), false), new CookTimer(3, Noon, true)]);

        var json = JsonSerializer.Serialize(state, CoreJsonContext.Default.CookingState);
        var back = JsonSerializer.Deserialize(json, CoreJsonContext.Default.CookingState)!;

        back.TrayKey.ShouldBe(Key);
        back.StepIndex.ShouldBe(2);
        back.Timers.Select(t => (t.StepIndex, t.EndsAt, t.Alerted)).ShouldBe([(0, Noon.AddMinutes(30), false), (3, Noon, true)]);
        json.ShouldNotContain("isUsable", Case.Insensitive);
    }

    [Fact]
    public void Only_a_state_with_a_tray_a_step_and_one_timer_at_most_on_a_step_can_be_used()
    {
        CookingState.Empty.IsUsable.ShouldBeTrue();
        new CookingState(Key, 4, [new CookTimer(0, Noon, false), new CookTimer(1, Noon, false)]).IsUsable.ShouldBeTrue();
        new CookingState(string.Empty, 0, []).IsUsable.ShouldBeTrue();
        new CookingState(string.Empty, 2, []).IsUsable.ShouldBeFalse();                       // a step that belongs to nothing
        new CookingState(string.Empty, 0, [new CookTimer(0, Noon, false)]).IsUsable.ShouldBeFalse();
        new CookingState(Key, -1, []).IsUsable.ShouldBeFalse();
        new CookingState(Key, 0, [new CookTimer(-1, Noon, false)]).IsUsable.ShouldBeFalse();
        new CookingState(Key, 0, [new CookTimer(1, Noon, false), new CookTimer(1, Noon, true)]).IsUsable.ShouldBeFalse();
        new CookingState(Key, 0, [null!]).IsUsable.ShouldBeFalse();
        new CookingState(null!, 0, []).IsUsable.ShouldBeFalse();
        new CookingState(Key, 0, null!).IsUsable.ShouldBeFalse();
    }

    // ---- bad input ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void Null_arguments_are_refused()
    {
        var desk = Desk();
        var guide = Guide();
        var state = CookingState.Empty;

        Should.Throw<ArgumentNullException>(() => new CookingDesk(null!, Cooking));
        Should.Throw<ArgumentNullException>(() => new CookingDesk(Portions, null!));
        Should.Throw<ArgumentNullException>(() => desk.Guide(null!, [], Pantry()));
        Should.Throw<ArgumentNullException>(() => desk.Guide(TwoAdults(), null!, Pantry()));
        Should.Throw<ArgumentNullException>(() => desk.Guide(TwoAdults(), [], null!));
        Should.Throw<ArgumentNullException>(() => desk.Open(null, null!, guide));
        Should.Throw<ArgumentNullException>(() => desk.Open(null, TheTray, null!));
        Should.Throw<ArgumentNullException>(() => desk.GoTo(null!, guide, 0));
        Should.Throw<ArgumentNullException>(() => desk.GoTo(state, null!, 0));
        Should.Throw<ArgumentNullException>(() => desk.Next(null!, guide));
        Should.Throw<ArgumentNullException>(() => desk.Previous(null!, guide));
        Should.Throw<ArgumentNullException>(() => desk.StartTimer(null!, guide, 0, Noon));
        Should.Throw<ArgumentNullException>(() => desk.StartTimer(state, null!, 0, Noon));
        Should.Throw<ArgumentNullException>(() => desk.CancelTimer(null!, 0));
        Should.Throw<ArgumentNullException>(() => desk.Timers(null!, guide, Noon));
        Should.Throw<ArgumentNullException>(() => desk.Timers(state, null!, Noon));
        Should.Throw<ArgumentNullException>(() => desk.Due(null!, Noon));
        Should.Throw<ArgumentNullException>(() => desk.MarkAlerted(null!, []));
        Should.Throw<ArgumentNullException>(() => desk.MarkAlerted(state, null!));
    }

    [Fact]
    public void A_config_the_formulas_cannot_use_is_refused()
    {
        Should.Throw<ArgumentException>(() => new CookingDesk(Portions with { ServingsPerBowl = 0 }, Cooking));
        Should.Throw<ArgumentException>(() => new CookingDesk(Portions, Cooking with { RiceActiveMinutes = -1 }));
    }

    [Fact]
    public void A_tray_key_is_the_one_the_shopping_screen_uses()
    {
        TrayKeys.Of(TheTray).ShouldBe(MamCom.Core.Shopping.ShoppingDesk.TrayKeyOf(TheTray));
        TrayKeys.Of(TheTray).ShouldBe("2026-10-21|thit-kho,rau-xao");
        Should.Throw<ArgumentNullException>(() => TrayKeys.Of(null!));
    }
}
