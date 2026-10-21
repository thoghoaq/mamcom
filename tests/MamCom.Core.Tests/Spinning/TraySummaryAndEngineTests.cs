using MamCom.Core.Cooking;
using MamCom.Core.Data;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Spinning;
using MamCom.Core.Taste;
using MamCom.Core.Tests.Shopping;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Spinning;

public class TraySummaryBuilderTests
{
    private static readonly NutritionConfig Nutrition = NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json"));
    private static readonly CookingConfig Cooking = CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"));

    private static TraySummaryBuilder Builder() => new(PortionSettings, Nutrition, new CookPlanScheduler(Cooking));

    /// <summary>One kcal for each gram of every ingredient (test-only numbers): the energy of a meal is then a sum of grams.</summary>
    private static Dictionary<string, Ingredient> OneKcalPerGram() =>
        ShoppingFixtures.Catalog().ToDictionary(p => p.Key, p => p.Value with { NutritionPer100gEdible = TestData.Facts(100, 1, 1, 1, 1, 1) });

    private static Dish WithSteps(Dish dish, params Step[] steps) => dish with { Steps = steps };

    private static Step Hands(int order, int active, int passive = 0) => new(order, "Làm.", active, passive, []);

    // ---- a card -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_dish_takes_all_its_steps_added_up_hands_on_and_unattended()
    {
        var dish = WithSteps(ShoppingFixtures.BraisedPork, Hands(1, 10, 20), Hands(2, 5), Hands(3, 0, 45));

        Builder().FactsOf(dish, OneKcalPerGram()).Minutes.ShouldBe(80);
    }

    [Fact]
    public void A_dish_with_no_steps_takes_no_minutes()
    {
        Builder().FactsOf(ShoppingFixtures.BraisedPork, OneKcalPerGram()).Minutes.ShouldBe(0);
    }

    [Fact]
    public void The_energy_of_a_dish_is_for_one_serving_when_every_ingredient_has_a_value()
    {
        var facts = Builder().FactsOf(ShoppingFixtures.BraisedPork, OneKcalPerGram());   // 70 g + 50 g

        facts.DishId.ShouldBe("thit-kho-trung");
        facts.KcalPerServing.ShouldBe(120);
    }

    [Fact]
    public void The_energy_of_a_dish_is_unknown_and_not_zero_when_an_ingredient_has_no_value()
    {
        var facts = Builder().FactsOf(ShoppingFixtures.BraisedPork, ShoppingFixtures.Catalog());   // placeholder data

        facts.KcalPerServing.ShouldBeNull();
    }

    [Fact]
    public void A_dish_with_an_ingredient_that_is_not_in_the_list_is_refused()
    {
        Should.Throw<InvalidOperationException>(() => Builder().FactsOf(ShoppingFixtures.BraisedPork, new Dictionary<string, Ingredient>()));
    }

    // ---- the summary ------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_summary_of_the_SPEC_family_and_a_tray_adds_up()
    {
        var dishes = ShoppingFixtures.Tray.Select(d => WithSteps(d, Hands(1, 10, 20))).ToArray();

        var summary = Builder().Build(ShoppingFixtures.Family(Region.North), dishes, OneKcalPerGram());

        // 120 + 25 + 108 g = 253 kcal a serving × 3.7875 servings, plus 8 bowls × 40 g = 320 kcal of rice, shared by four.
        summary.KcalPerPerson.Known().ShouldBe((253 * 3.7875 + 320) / 4, 1e-9);
        summary.RawRiceGrams.ShouldBe(320);
        summary.ItemsToBuy.ShouldBe(6);   // pork, egg, water spinach, fish, tomato, taro stem; the rice is a pantry item
        summary.MealMinutes.ShouldBe(new CookPlanScheduler(Cooking).EstimateMealMinutes(dishes, Cooking.DefaultStoves));   // the family did not say how many stoves
        summary.MealMinutes.ShouldBeGreaterThan(Cooking.RiceActiveMinutes + Cooking.RicePassiveMinutes - 1);
    }

    [Fact]
    public void The_time_of_the_whole_meal_is_planned_for_the_stoves_of_the_household()
    {
        var dishes = ShoppingFixtures.Tray.Select(d => WithSteps(d, Hands(1, 10, 20))).ToArray();
        var scheduler = new CookPlanScheduler(Cooking);

        int Minutes(int stoves) => Builder().Build(ShoppingFixtures.Family(Region.North) with { Stoves = stoves }, dishes, OneKcalPerGram()).MealMinutes;

        Minutes(1).ShouldBe(scheduler.EstimateMealMinutes(dishes, 1));
        Minutes(3).ShouldBe(scheduler.EstimateMealMinutes(dishes, 3));
        Minutes(1).ShouldBeGreaterThan(Minutes(3));   // three dishes on one stove go one after another
    }

    [Fact]
    public void The_energy_per_person_is_unknown_and_not_zero_while_the_data_is_not_complete()
    {
        var summary = Builder().Build(ShoppingFixtures.Family(Region.South), ShoppingFixtures.Tray, ShoppingFixtures.Catalog());

        summary.KcalPerPerson.ShouldBeNull();
        summary.RawRiceGrams.ShouldBe(320);   // what does not depend on the energy is still said
    }

    [Fact]
    public void A_meal_with_no_dish_is_just_the_rice()
    {
        var summary = Builder().Build(ShoppingFixtures.Family(Region.North), [], OneKcalPerGram());

        summary.MealMinutes.ShouldBe(Cooking.RiceActiveMinutes + Cooking.RicePassiveMinutes);
        summary.ItemsToBuy.ShouldBe(0);
        summary.KcalPerPerson.ShouldBe(320 / 4.0);
    }

    [Fact]
    public void The_summary_of_the_pantry_items_leaves_them_out_of_the_count_to_buy()
    {
        var catalog = OneKcalPerGram();
        catalog["thit-ba-chi"] = catalog["thit-ba-chi"] with { IsPantry = true };

        Builder().Build(ShoppingFixtures.Family(Region.North), ShoppingFixtures.Tray, catalog).ItemsToBuy.ShouldBe(5);
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        var scheduler = new CookPlanScheduler(Cooking);

        Should.Throw<ArgumentNullException>(() => new TraySummaryBuilder(null!, Nutrition, scheduler));
        Should.Throw<ArgumentNullException>(() => new TraySummaryBuilder(PortionSettings, null!, scheduler));
        Should.Throw<ArgumentNullException>(() => new TraySummaryBuilder(PortionSettings, Nutrition, null!));
        Should.Throw<ArgumentNullException>(() => Builder().FactsOf(null!, OneKcalPerGram()));
        Should.Throw<ArgumentNullException>(() => Builder().FactsOf(ShoppingFixtures.BraisedPork, null!));
        Should.Throw<ArgumentNullException>(() => Builder().Build(null!, [], OneKcalPerGram()));
        Should.Throw<ArgumentNullException>(() => Builder().Build(ShoppingFixtures.Family(Region.North), null!, OneKcalPerGram()));
        Should.Throw<ArgumentNullException>(() => Builder().Build(ShoppingFixtures.Family(Region.North), [], null!));
    }
}

public class TrayEngineTests
{
    private static readonly DataBundle Bundle = new(
        "v1",
        IsRelease: false,
        new BundleConfig(
            PortionSettings,
            NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json")),
            Config,
            CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"))),
        [.. Ingredients().Values.OrderBy(i => i.Id, StringComparer.Ordinal)],
        Catalog());

    private static TrayEngine Engine(FixedClock? clock = null, ulong seed = 1) => TrayEngine.Create(Bundle, new SeededRandomSource(seed), clock ?? new FixedClock(Today));

    [Fact]
    public void The_context_of_a_spin_has_the_stoves_of_the_household_or_the_usual_number_of_the_config()
    {
        Engine().ContextFor(SmallHome(), EatenLog.Empty).Stoves.ShouldBe(2);
        Engine().ContextFor(SmallHome() with { Stoves = 1 }, EatenLog.Empty).Stoves.ShouldBe(1);
    }

    private static TraySession SessionWith(DateOnly date, params string[] ids) => TraySession.Empty with
    {
        History = new SpinHistory([new SpinResult(new MealTray(date, [.. ids.Select((id, i) => new SlotResult((DishSlot)(i % 3), id, false))]), [], [])]),
    };

    [Fact]
    public void The_engine_knows_the_catalog_by_id_and_the_quick_filters_of_the_bundle()
    {
        var engine = Engine();

        engine.DishesById.Count.ShouldBe(Catalog().Length);
        engine.DishesById["canh-bi-do"].Id.ShouldBe("canh-bi-do");
        engine.IngredientsById.ContainsKey("thit-heo").ShouldBeTrue();
        engine.QuickFilters.ShouldBeSameAs(Config.QuickFilters);
    }

    [Fact]
    public void The_context_has_the_catalog_the_household_and_what_was_eaten_read_as_of_today()
    {
        var household = SmallHome();
        var eaten = EatenLog.Empty.With(["cai-luoc"], Today.AddDays(30)).With(["ga-ran"], Today.AddDays(-4));

        var context = Engine().ContextFor(household, eaten);

        context.Household.ShouldBeSameAs(household);
        context.Dishes.Count.ShouldBe(Catalog().Length);
        context.Ingredients.Count.ShouldBe(Ingredients().Count);
        context.LastEatenOn["cai-luoc"].ShouldBe(Today);   // a day in the future is read as today
        context.LastEatenOn["ga-ran"].ShouldBe(Today.AddDays(-4));
        context.BlockedDishIds.ShouldBeEmpty();
        context.Preferences.ShouldBeEmpty();
    }

    [Fact]
    public void The_context_carries_what_the_household_said_about_dishes_the_blocked_ones_and_how_they_liked_the_rest()
    {
        var taste = new TasteProfile(new Dictionary<string, DishPreference> { ["cai-luoc"] = DishPreference.Liked, ["ga-ran"] = DishPreference.Disliked }, ["canh-bi-do", "ga-kho-gung"]);

        var context = Engine().ContextFor(SmallHome(), EatenLog.Empty, taste);

        context.BlockedDishIds.OrderBy(id => id, StringComparer.Ordinal).ShouldBe(["canh-bi-do", "ga-kho-gung"]);
        context.Preferences["cai-luoc"].ShouldBe(DishPreference.Liked);
        context.Preferences["ga-ran"].ShouldBe(DishPreference.Disliked);
        context.Preferences.Count.ShouldBe(2);
    }

    [Fact]
    public void A_blocked_dish_is_never_on_a_tray_the_engine_spins()
    {
        var engine = Engine();
        var all = engine.DishesById.Keys.ToArray();
        var blocked = all.Take(all.Length / 2).ToArray();
        var taste = new TasteProfile(new Dictionary<string, DishPreference>(), blocked);

        for (var run = 0; run < 20; run++)
        {
            var session = engine.Desk.Spin(TraySession.Empty, engine.ContextFor(SmallHome(), EatenLog.Empty, taste));

            session.Viewed!.Tray.Slots.Select(s => s.DishId).ShouldNotContain(id => blocked.Contains(id));
        }
    }

    [Fact]
    public void A_session_of_today_with_dishes_that_are_still_in_the_catalog_can_go_on()
    {
        Engine().CanContinue(SessionWith(Today, "canh-bi-do", "ga-kho-gung", "cai-luoc")).ShouldBeTrue();
    }

    [Fact]
    public void A_session_of_another_day_cannot_go_on()
    {
        Engine().CanContinue(SessionWith(Today.AddDays(-1), "canh-bi-do")).ShouldBeFalse();
        Engine(new FixedClock(Today.AddDays(1))).CanContinue(SessionWith(Today, "canh-bi-do")).ShouldBeFalse();
    }

    [Fact]
    public void A_session_with_a_dish_that_is_no_longer_in_the_catalog_cannot_go_on()
    {
        Engine().CanContinue(SessionWith(Today, "canh-bi-do", "mon-da-bi-go")).ShouldBeFalse();
    }

    [Fact]
    public void A_session_with_nothing_on_the_screen_or_one_that_is_not_usable_cannot_go_on()
    {
        Engine().CanContinue(TraySession.Empty).ShouldBeFalse();
        Engine().CanContinue(SessionWith(Today, "canh-bi-do") with { ViewIndex = 5 }).ShouldBeFalse();
        Engine().CanContinue(SessionWith(Today, "canh-bi-do") with { SpinsSinceConfirm = -2 }).ShouldBeFalse();
    }

    [Fact]
    public void The_dishes_of_a_tray_come_in_the_order_of_its_slots_and_one_that_is_gone_is_left_out()
    {
        var tray = SessionWith(Today, "cai-luoc", "gone", "canh-bi-do").Viewed!.Tray;

        Engine().DishesOf(tray).Select(d => d.Id).ShouldBe(["cai-luoc", "canh-bi-do"]);
    }

    [Fact]
    public void The_desk_of_the_engine_spins_a_tray_from_the_context()
    {
        var engine = Engine();
        var context = engine.ContextFor(SmallHome(), EatenLog.Empty);

        var session = engine.Desk.Spin(TraySession.Empty, context);

        session.Viewed!.Tray.Slots.Count.ShouldBe(3);
        session.Viewed.Tray.Slots.ShouldAllBe(s => engine.DishesById.ContainsKey(s.DishId));
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        var engine = Engine();

        Should.Throw<ArgumentNullException>(() => TrayEngine.Create(null!, new SeededRandomSource(1), new FixedClock(Today)));
        Should.Throw<ArgumentNullException>(() => TrayEngine.Create(Bundle, null!, new FixedClock(Today)));
        Should.Throw<ArgumentNullException>(() => TrayEngine.Create(Bundle, new SeededRandomSource(1), null!));
        Should.Throw<ArgumentNullException>(() => engine.ContextFor(null!, EatenLog.Empty));
        Should.Throw<ArgumentNullException>(() => engine.ContextFor(SmallHome(), null!));
        Should.Throw<ArgumentNullException>(() => engine.CanContinue(null!));
        Should.Throw<ArgumentNullException>(() => engine.DishesOf(null!));
    }
}
