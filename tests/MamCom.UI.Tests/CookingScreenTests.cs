using System.Reflection;
using AngleSharp.Dom;
using Bunit.TestDoubles;
using MamCom.Core;
using MamCom.Core.Cooking;
using MamCom.Core.Data;
using MamCom.Core.Models;
using MamCom.Core.Spinning;
using MamCom.Data;
using MamCom.Data.Bundles;
using MamCom.Data.Device;
using MamCom.Data.State;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class CookingScreenTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private static readonly DateTimeOffset Noon = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAppData _data = new() { Bundle = Bundle() };
    private readonly FakeTrayStore _trays = new();
    private readonly FakeCookingStore _cooking = new();
    private readonly FakeDeviceFeatures _device = new();
    private readonly FakeTicker _ticker = new();
    private readonly FixedClock _clock = new(Today) { Now = Noon };

    public CookingScreenTests()
    {
        Services.AddSingleton<IAppData>(_data);
        Services.AddSingleton<ITrayStore>(_trays);
        Services.AddSingleton<ICookingStore>(_cooking);
        Services.AddSingleton<IDeviceFeatures>(_device);
        Services.AddSingleton<ITicker>(_ticker);
        Services.AddSingleton<IClock>(_clock);
        Services.AddSingleton<IRandomSource>(new SeededRandomSource(5));
        _trays.Saved = SessionOf("mon-nau", "mon-rau");
    }

    // ---- test-only fixture: two adults make 2.0 servings and 160 g of raw rice ---------------------------------------------------

    private static Ingredient Item(string id, string name, MarketUnit? unit = null) => TestConfig.SeedBundle.Ingredients.First() with
    {
        Id = id,
        Names = new Dictionary<string, string> { [RegionKeys.Default] = name },
        MarketUnit = unit is null ? new Dictionary<string, MarketUnit>() : new Dictionary<string, MarketUnit> { [RegionKeys.Default] = unit },
    };

    private static Step Step(int order, string text, int active, int passive, params string[] ingredients) => new(order, text, active, passive, ingredients);

    private static DishIngredient Line(string id, double grams, ScaleKind scale = ScaleKind.Linear) => new(id, grams, scale, 1, null);

    private static Dish Braise => TestConfig.SeedBundle.Dishes.First() with
    {
        Id = "mon-nau",
        Name = "Thịt kho",
        Slot = DishSlot.Main,
        ServeHot = false,
        Ingredients = [Line("t-thit", 100), Line("t-trung", 50, ScaleKind.Integer), Line("t-hanh", 10)],
        Steps =
        [
            Step(1, "Luộc trứng.", 3, 10, "t-trung"),
            Step(2, "Ướp thịt.", 6, 15, "t-thit", "t-hanh"),
            Step(3, "Kho thịt.", 8, 0, "t-thit", "t-trung"),
        ],
    };

    private static Dish Greens => Braise with
    {
        Id = "mon-rau",
        Name = "Rau xào",
        Slot = DishSlot.Vegetable,
        ServeHot = true,
        Ingredients = [Line("t-rau", 80)],
        Steps = [Step(1, "Xào rau.", 4, 0, "t-rau")],
    };

    private static DataBundle Bundle() => TestConfig.SeedBundle with
    {
        Ingredients =
        [
            .. TestConfig.SeedBundle.Ingredients,
            Item("t-thit", "Thịt"),
            Item("t-trung", "Trứng", new MarketUnit("quả", 55, 1)),
            Item("t-hanh", "Hành"),
            Item("t-rau", "Rau"),
        ],
        Dishes = [.. TestConfig.SeedBundle.Dishes, Braise, Greens],
    };

    private static Household TwoAdults() => new(Region.North, BowlSize.Medium, [new Member("Bố", AgeGroup.Adult, 2, [], []), new Member("Mẹ", AgeGroup.Adult, 2, [], [])]);

    private static TraySession SessionOf(string main, string vegetable, DateOnly? date = null) => new(
        new SpinHistory([new SpinResult(new MealTray(date ?? Today, [new SlotResult(DishSlot.Main, main, false), new SlotResult(DishSlot.Vegetable, vegetable, false)]), [], [])]),
        0,
        1,
        QuickFilters.None,
        null);

    private static string TrayKey => $"{Today:yyyy-MM-dd}|mon-nau,mon-rau";

    /// <summary>The guide the screen shows, worked out the same way: the rice, then the braise (three steps) and the greens in the order of the plan.</summary>
    private static CookingGuide TheGuide(DataBundle? bundle = null)
    {
        bundle ??= Bundle();
        var dishes = new[] { bundle.Dishes.Single(d => d.Id == "mon-nau"), bundle.Dishes.Single(d => d.Id == "mon-rau") };
        return new CookingDesk(bundle.Config.Portion, bundle.Config.Cooking).Guide(TwoAdults(), dishes, bundle.Ingredients.ToDictionary(i => i.Id));
    }

    private static int IndexOf(string text) => TheGuide().Steps.Single(s => s.Text == text).Index;

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private IRenderedComponent<CookingScreen> Show(Household? household = null) => Render<CookingScreen>(p => p.Add(s => s.Household, household ?? TwoAdults()));

    private static string Words(IRenderedComponent<CookingScreen> cut, string selector) => cut.Find(selector).TextContent.Words();

    private static string[] AllWords(IRenderedComponent<CookingScreen> cut, string selector) => [.. cut.FindAll(selector).Select(e => e.TextContent.Words())];

    private static IElement Button(IRenderedComponent<CookingScreen> cut, string text) =>
        cut.FindAll(".cook-actions button").Single(b => b.TextContent.Words() == text);

    private async Task GoTo(IRenderedComponent<CookingScreen> cut, string text)
    {
        for (var i = 0; i < 20 && Words(cut, ".cook__text") != text; i++)
        {
            await cut.Find(".cook-zone--next").ClickAsync(new());
        }

        Words(cut, ".cook__text").ShouldBe(text);
    }

    // ---- the page and its way in -------------------------------------------------------------------------------------------

    [Fact]
    public void The_page_is_the_route_of_the_cooking_mode_and_the_route_is_the_one_the_app_links_to()
    {
        var route = typeof(CookingPage).GetCustomAttribute<RouteAttribute>();

        route.ShouldNotBeNull();
        route.Template.ShouldBe("/" + Routes.Cooking);
        Routes.Cooking.ShouldBe("nau");
    }

    [Fact]
    public void The_page_shows_the_steps_for_the_household_of_the_device_and_otherwise_sends_the_person_to_the_first_screen()
    {
        var store = new FakeHouseholdStore();
        Services.AddSingleton<IHouseholdStore>(store);

        var without = Render<CookingPage>();

        without.FindAll(".cook").ShouldBeEmpty();
        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.Household);

        store.Saved = TwoAdults();
        Render<CookingPage>().FindAll(".cook").Count.ShouldBe(1);
    }

    // ---- one step on the screen --------------------------------------------------------------------------------------------

    [Fact]
    public void The_first_step_is_the_rice_with_how_much_to_wash_and_how_long_it_takes()
    {
        var cut = Show();

        Words(cut, ".cook__position").ShouldBe($"Bước 1/{TheGuide().Steps.Count}");
        Words(cut, ".cook__dish").ShouldBe("Cơm");
        Words(cut, ".cook__text").ShouldBe("Vo gạo và bắc nồi cơm.");
        AllWords(cut, ".cook__time").ShouldBe(["Làm 5 phút", "Chờ 30 phút"]);
        AllWords(cut, ".cook__ingredients li").ShouldBe(["Gạo 160 g"]);
        cut.Find("h1").TextContent.ShouldBe("Nấu mâm");
        cut.Find("h1").ClassList.ShouldContain("visually-hidden");
    }

    [Fact]
    public async Task A_step_of_a_dish_says_whose_it_is_in_the_colour_of_that_kind_of_dish_and_what_it_asks_of_the_cook()
    {
        var cut = Show();

        await GoTo(cut, "Ướp thịt.");

        Words(cut, ".cook__dish").ShouldBe("Thịt kho");
        cut.Find(".cook__dish").ClassList.ShouldContain("slot--main");
        AllWords(cut, ".cook__time").ShouldBe(["Làm 6 phút", "Chờ 15 phút"]);
        await GoTo(cut, "Xào rau.");
        cut.Find(".cook__dish").ClassList.ShouldContain("slot--veg");
        AllWords(cut, ".cook__time").ShouldBe(["Làm 4 phút"]);   // nothing to wait for, so nothing said
    }

    [Fact]
    public async Task A_step_that_only_waits_says_how_long_to_wait_and_nothing_of_hands_on_time()
    {
        var soak = Greens with { Steps = [Step(1, "Ngâm rau.", 0, 20, "t-rau")] };
        _data.Bundle = Bundle() with { Dishes = [.. Bundle().Dishes.Where(d => d.Id != "mon-rau"), soak] };
        var cut = Show();

        await GoTo(cut, "Ngâm rau.");

        AllWords(cut, ".cook__time").ShouldBe(["Chờ 20 phút"]);
        Button(cut, "Hẹn giờ 20 phút").ShouldNotBeNull();
    }

    [Fact]
    public async Task The_rice_is_not_named_like_a_dish_and_has_its_own_colour_class()
    {
        var cut = Show();

        cut.Find(".cook__dish").ClassList.ShouldContain("cook__dish--rice");
        await GoTo(cut, "Ướp thịt.");
        cut.Find(".cook__dish").ClassList.ShouldNotContain("cook__dish--rice");
    }

    [Fact]
    public async Task The_amount_of_an_ingredient_is_said_the_first_time_and_only_the_name_after()
    {
        var cut = Show();

        await GoTo(cut, "Ướp thịt.");
        AllWords(cut, ".cook__ingredients li").ShouldBe(["Thịt 200 g", "Hành 20 g"]);

        await GoTo(cut, "Kho thịt.");
        AllWords(cut, ".cook__ingredients li").ShouldBe(["Thịt", "Trứng"]);   // the pork and the egg were named before
        cut.FindAll(".cook__ingredient-amount").ShouldBeEmpty();
    }

    [Fact]
    public async Task Pieces_are_counted_in_pieces_and_a_note_goes_with_the_amount()
    {
        var withNote = Braise with { Ingredients = [Line("t-thit", 100), new DishIngredient("t-trung", 50, ScaleKind.Integer, 1, "luộc, bóc vỏ"), Line("t-hanh", 10)] };
        _data.Bundle = Bundle() with { Dishes = [.. Bundle().Dishes.Where(d => d.Id != "mon-nau"), withNote] };
        var cut = Show();

        await GoTo(cut, "Luộc trứng.");

        AllWords(cut, ".cook__ingredients li").ShouldBe(["Trứng 2 quả (luộc, bóc vỏ)"]);
    }

    [Fact]
    public async Task A_step_with_no_ingredient_has_no_list()
    {
        var bare = Greens with { Steps = [Step(1, "Xào rau.", 4, 0)] };
        _data.Bundle = Bundle() with { Dishes = [.. Bundle().Dishes.Where(d => d.Id != "mon-rau"), bare] };
        var cut = Show();

        await GoTo(cut, "Xào rau.");

        cut.FindAll(".cook__ingredients").ShouldBeEmpty();
    }

    [Fact]
    public void The_step_is_announced_to_a_screen_reader_when_it_opens()
    {
        var cut = Show();

        cut.Find("p[role=status]").TextContent.ShouldBe($"Bước 1/{TheGuide().Steps.Count}, Cơm. Vo gạo và bắc nồi cơm.");
    }

    [Fact]
    public async Task The_new_step_is_announced_each_time_the_cook_moves()
    {
        var cut = Show();
        var marinate = IndexOf("Ướp thịt.");

        await GoTo(cut, "Ướp thịt.");

        cut.Find("p[role=status]").TextContent.ShouldBe($"Bước {marinate + 1}/{TheGuide().Steps.Count}, Thịt kho. Ướp thịt.");

        await Button(cut, "Trước").ClickAsync(new());

        cut.Find("p[role=status]").TextContent.ShouldBe($"Bước {marinate}/{TheGuide().Steps.Count}, {(marinate == 1 ? "Cơm" : "Thịt kho")}. {TheGuide().Steps[marinate - 1].Text}");
    }

    // ---- moving between steps ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_tap_on_the_right_half_goes_to_the_next_step_and_on_the_left_half_back()
    {
        var cut = Show();

        await cut.Find(".cook-zone--next").ClickAsync(new());
        Words(cut, ".cook__position").ShouldStartWith("Bước 2/");

        await cut.Find(".cook-zone--back").ClickAsync(new());
        Words(cut, ".cook__position").ShouldStartWith("Bước 1/");
    }

    [Fact]
    public async Task A_tap_on_the_left_half_of_the_first_step_stays_on_it()
    {
        var cut = Show();

        await cut.Find(".cook-zone--back").ClickAsync(new());

        Words(cut, ".cook__position").ShouldStartWith("Bước 1/");
    }

    [Fact]
    public void The_halves_are_for_a_finger_only_so_the_buttons_are_what_a_keyboard_and_a_screen_reader_use()
    {
        var cut = Show();

        cut.FindAll(".cook-zone").ShouldAllBe(z => z.GetAttribute("aria-hidden") == "true" && z.GetAttribute("tabindex") == "-1");
        cut.Find(".cook__stage").GetAttribute("aria-label").ShouldBe(Words(cut, ".cook__position"));
    }

    [Fact]
    public async Task The_buttons_do_the_same_and_the_one_to_go_back_is_off_on_the_first_step()
    {
        var cut = Show();
        Button(cut, "Trước").HasAttribute("disabled").ShouldBeTrue();

        await Button(cut, "Tiếp").ClickAsync(new());
        Words(cut, ".cook__position").ShouldStartWith("Bước 2/");
        Button(cut, "Trước").HasAttribute("disabled").ShouldBeFalse();

        await Button(cut, "Trước").ClickAsync(new());
        Words(cut, ".cook__position").ShouldStartWith("Bước 1/");
    }

    [Fact]
    public async Task The_last_step_ends_the_cooking_and_leads_to_the_question_how_the_meal_was()
    {
        var cut = Show();
        var last = TheGuide().Steps.Count - 1;
        for (var i = 0; i < last; i++)
        {
            await Button(cut, "Tiếp").ClickAsync(new());
        }

        var finish = Button(cut, "Xong bữa");
        finish.ClassList.ShouldContain("button--primary");
        cut.FindAll(".cook-actions button").Select(b => b.TextContent.Words()).ShouldNotContain("Tiếp");
        Navigation.History.ShouldBeEmpty();

        await finish.ClickAsync(new());

        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.AfterMeal);
        Routes.AfterMeal.ShouldBe("sau-bua");
        Words(cut, ".cook__position").ShouldBe($"Bước {last + 1}/{last + 1}");
    }

    [Fact]
    public async Task A_tap_on_the_right_half_of_the_last_step_also_ends_the_cooking()
    {
        var cut = Show();
        await GoTo(cut, TheGuide().Steps[^1].Text);

        await cut.Find(".cook-zone--next").ClickAsync(new());

        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.AfterMeal);
    }

    [Fact]
    public async Task The_step_is_kept_for_the_next_visit_with_the_name_of_the_tray()
    {
        var first = Show();
        await Button(first, "Tiếp").ClickAsync(new());
        await Button(first, "Tiếp").ClickAsync(new());

        _cooking.Saved!.StepIndex.ShouldBe(2);
        _cooking.Saved.TrayKey.ShouldBe(TrayKey);

        var again = Show();

        Words(again, ".cook__position").ShouldStartWith("Bước 3/");
    }

    [Fact]
    public void The_step_of_another_tray_is_not_kept()
    {
        _cooking.Saved = new CookingState("2026-10-20|cu-khac", 3, []);

        Words(Show(), ".cook__position").ShouldStartWith("Bước 1/");
    }

    // ---- timers ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_step_with_a_wait_offers_a_timer_for_that_long_and_a_step_without_one_does_not()
    {
        var cut = Show();

        var button = Button(cut, "Hẹn giờ 30 phút");
        button.ClassList.ShouldContain("button--primary");
        cut.FindAll(".cook__countdown").ShouldBeEmpty();
        cut.FindAll(".cook-timers").ShouldBeEmpty();
    }

    [Fact]
    public async Task A_step_without_a_wait_has_no_timer_button()
    {
        var cut = Show();

        await GoTo(cut, "Kho thịt.");

        cut.FindAll(".cook-actions__timer").ShouldBeEmpty();
    }

    [Fact]
    public async Task Starting_a_timer_readies_the_sound_keeps_the_end_and_shows_the_countdown_and_a_chip()
    {
        var cut = Show();

        await Button(cut, "Hẹn giờ 30 phút").ClickAsync(new());

        _device.Prepares.ShouldBe(1);   // on the tap, because a page may only make a sound after one
        _cooking.Saved!.Timers.ShouldBe([new CookTimer(0, Noon.AddMinutes(30), false)]);
        var countdown = cut.Find(".cook__countdown");
        countdown.TextContent.ShouldBe("30:00");
        countdown.GetAttribute("role").ShouldBe("timer");
        AllWords(cut, ".cook-chip").ShouldBe(["Cơm 30:00"]);
        cut.Find(".cook-timers").GetAttribute("aria-label").ShouldBe("Các hẹn giờ");
        Button(cut, "Huỷ hẹn giờ").ClassList.ShouldContain("button--quiet");
    }

    [Fact]
    public async Task The_countdown_follows_the_clock_on_every_tick()
    {
        var cut = Show();
        await Button(cut, "Hẹn giờ 30 phút").ClickAsync(new());

        _clock.Now = Noon.AddSeconds(1);
        await _ticker.FireAsync();
        cut.Find(".cook__countdown").TextContent.ShouldBe("29:59");
        AllWords(cut, ".cook-chip").ShouldBe(["Cơm 29:59"]);

        _clock.Now = Noon.AddMinutes(5).AddSeconds(30);
        await _ticker.FireAsync();
        cut.Find(".cook__countdown").TextContent.ShouldBe("24:30");
    }

    [Fact]
    public async Task A_timer_that_runs_out_sounds_the_alarm_once_and_says_so_in_words_on_the_chip_and_the_step()
    {
        var cut = Show();
        await Button(cut, "Hẹn giờ 30 phút").ClickAsync(new());
        _device.Alerts.ShouldBe(0);

        _clock.Now = Noon.AddMinutes(30);
        await _ticker.FireAsync();

        _device.Alerts.ShouldBe(1);
        _cooking.Saved!.Timers.Single().Alerted.ShouldBeTrue();
        AllWords(cut, ".cook-chip").ShouldBe(["Cơm Hết giờ"]);
        cut.Find(".cook-chip").ClassList.ShouldContain("cook-chip--done");
        cut.Find(".cook__countdown").TextContent.ShouldBe("Hết giờ");
        cut.Find(".cook__countdown").ClassList.ShouldContain("cook__countdown--done");
        cut.Find("p[role=alert]").TextContent.ShouldBe("Hết giờ: Cơm.");

        _clock.Now = Noon.AddMinutes(31);
        await _ticker.FireAsync();

        _device.Alerts.ShouldBe(1);   // it does not sound again
    }

    [Fact]
    public async Task A_timer_that_ran_out_is_dismissed_with_the_button_and_the_chip_goes()
    {
        var cut = Show();
        await Button(cut, "Hẹn giờ 30 phút").ClickAsync(new());
        _clock.Now = Noon.AddMinutes(30);
        await _ticker.FireAsync();

        var dismiss = Button(cut, "Đã xong");
        dismiss.ClassList.ShouldContain("button--primary");
        await dismiss.ClickAsync(new());

        cut.FindAll(".cook-timers, .cook__countdown").ShouldBeEmpty();
        _cooking.Saved!.Timers.ShouldBeEmpty();
        Button(cut, "Hẹn giờ 30 phút").ShouldNotBeNull();
    }

    [Fact]
    public async Task A_running_timer_is_cancelled_with_the_button()
    {
        var cut = Show();
        await Button(cut, "Hẹn giờ 30 phút").ClickAsync(new());

        await Button(cut, "Huỷ hẹn giờ").ClickAsync(new());

        cut.FindAll(".cook-timers, .cook__countdown").ShouldBeEmpty();
        _cooking.Saved!.Timers.ShouldBeEmpty();
        _device.Alerts.ShouldBe(0);
    }

    [Fact]
    public async Task Timers_run_side_by_side_as_chips_the_soonest_first_and_a_chip_leads_to_its_step()
    {
        var cut = Show();
        await Button(cut, "Hẹn giờ 30 phút").ClickAsync(new());                       // the rice
        await GoTo(cut, "Ướp thịt.");
        _clock.Now = Noon.AddMinutes(1);
        await Button(cut, "Hẹn giờ 15 phút").ClickAsync(new());                       // the marinade: ends at 16

        AllWords(cut, ".cook-chip").ShouldBe(["Thịt kho 15:00", "Cơm 29:00"]);
        cut.Find(".cook__countdown").TextContent.ShouldBe("15:00");                    // the countdown on the screen is that of this step

        await cut.FindAll(".cook-chip")[1].ClickAsync(new());                          // the rice chip

        Words(cut, ".cook__position").ShouldStartWith("Bước 1/");
        Words(cut, ".cook__text").ShouldBe("Vo gạo và bắc nồi cơm.");
        cut.Find(".cook__countdown").TextContent.ShouldBe("29:00");

        await cut.FindAll(".cook-chip")[0].ClickAsync(new());                          // the chip of the marinade, from the rice

        Words(cut, ".cook__text").ShouldBe("Ướp thịt.");
        cut.Find(".cook__countdown").TextContent.ShouldBe("15:00");
    }

    [Fact]
    public async Task Two_timers_that_run_out_together_are_told_in_one_alarm()
    {
        var cut = Show();
        await Button(cut, "Hẹn giờ 30 phút").ClickAsync(new());
        await GoTo(cut, "Luộc trứng.");
        await Button(cut, "Hẹn giờ 10 phút").ClickAsync(new());

        _clock.Now = Noon.AddMinutes(40);
        await _ticker.FireAsync();

        _device.Alerts.ShouldBe(1);
        cut.Find("p[role=alert]").TextContent.ShouldBe("Hết giờ: Thịt kho, Cơm.");   // in the order they ended
    }

    [Fact]
    public async Task A_timer_that_ran_out_while_the_screen_was_closed_sounds_on_the_first_tick_and_one_that_already_sounded_does_not()
    {
        _cooking.Saved = new CookingState(TrayKey, 0, [new CookTimer(0, Noon.AddMinutes(-5), false)]);
        var cut = Show();
        _device.Alerts.ShouldBe(0);

        await _ticker.FireAsync();

        _device.Alerts.ShouldBe(1);
        AllWords(cut, ".cook-chip").ShouldBe(["Cơm Hết giờ"]);

        _cooking.Saved = new CookingState(TrayKey, 0, [new CookTimer(0, Noon.AddMinutes(-5), true)]);
        var quiet = Show();
        await _ticker.FireAsync();

        _device.Alerts.ShouldBe(1);   // nothing new
        AllWords(quiet, ".cook-chip").ShouldBe(["Cơm Hết giờ"]);
    }

    [Fact]
    public async Task A_tick_with_no_timer_does_nothing_to_the_screen()
    {
        var cut = Show();
        var before = cut.Markup;

        await _ticker.FireAsync();

        cut.Markup.ShouldBe(before);
        _device.Alerts.ShouldBe(0);
        _cooking.Saves.ShouldBe(0);
    }

    [Fact]
    public void The_ticker_goes_every_second_and_only_once()
    {
        Show();

        _ticker.Starts.ShouldBe(1);
        _ticker.Period.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task A_timer_whose_alarm_cannot_sound_is_still_told_in_words()
    {
        _device.AlertResult = false;
        var cut = Show();
        await Button(cut, "Hẹn giờ 30 phút").ClickAsync(new());

        _clock.Now = Noon.AddMinutes(30);
        await _ticker.FireAsync();

        cut.Find(".cook-chip").TextContent.ShouldContain("Hết giờ");
        cut.Find("p[role=alert]").TextContent.ShouldBe("Hết giờ: Cơm.");
    }

    // ---- the lit screen ----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_screen_is_kept_lit_once_and_nothing_is_said_when_it_works()
    {
        var cut = Show();

        _device.KeepScreenOnCalls.ShouldBe(1);
        cut.FindAll(".notice").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(WakeLockOutcome.Unsupported)]
    [InlineData(WakeLockOutcome.Denied)]
    public void When_the_screen_cannot_be_kept_lit_the_screen_says_so_and_what_to_do(WakeLockOutcome outcome)
    {
        _device.WakeResult = outcome;

        var cut = Show();

        var notice = cut.Find(".notice");
        notice.ClassList.ShouldContain("notice--warning");
        notice.GetAttribute("role").ShouldBe("status");
        notice.TextContent.ShouldBe("Máy này không giữ được màn hình sáng. Hãy chỉnh thời gian tắt màn hình trong cài đặt máy.");
    }

    [Fact]
    public async Task Leaving_the_screen_gives_the_screen_back_and_stops_the_ticks()
    {
        Show();
        _device.Releases.ShouldBe(0);

        await DisposeComponentsAsync();

        _device.Releases.ShouldBe(1);
        _ticker.Stopped.ShouldBeTrue();
        _data.Listeners.ShouldBe(0);
    }

    [Fact]
    public async Task A_tick_that_comes_after_the_screen_went_does_nothing()
    {
        _cooking.Saved = new CookingState(TrayKey, 0, [new CookTimer(0, Noon.AddMinutes(-5), false)]);
        Show();
        await DisposeComponentsAsync();

        await _ticker.FireAsync();       // the ticker is stopped, so this one never comes
        await _ticker.FireLateAsync();   // and one that was already on its way finds the screen gone

        _device.Alerts.ShouldBe(0);
        _cooking.Saves.ShouldBe(0);
    }

    // ---- when there is nothing to cook, or the data is not there ---------------------------------------------------------------

    [Fact]
    public async Task Without_a_tray_the_page_says_so_and_neither_keeps_the_screen_lit_nor_ticks()
    {
        _trays.Saved = null;

        var cut = Show();

        cut.Find("h1").TextContent.ShouldBe("Nấu mâm");
        cut.Find("p").TextContent.ShouldBe("Chưa có mâm nào để nấu. Hãy quay và chốt một mâm trước.");
        cut.FindAll(".cook, .cook-actions").ShouldBeEmpty();
        cut.Find(".back__link").ShouldNotBeNull();
        _device.KeepScreenOnCalls.ShouldBe(0);
        _ticker.Starts.ShouldBe(0);
        await DisposeComponentsAsync();
    }

    [Fact]
    public void A_tray_of_yesterday_or_with_a_dish_that_is_gone_or_with_no_dish_is_not_cooked()
    {
        _trays.Saved = SessionOf("mon-nau", "mon-rau", Today.AddDays(-1));
        Show().FindAll(".cook").ShouldBeEmpty();

        _trays.Saved = SessionOf("mon-nau", "mon-da-bo");
        Show().FindAll(".cook").ShouldBeEmpty();

        _trays.Saved = new TraySession(new SpinHistory([new SpinResult(new MealTray(Today, []), [], [])]), 0, 1, QuickFilters.None, null);
        Show().FindAll(".cook").ShouldBeEmpty();
    }

    [Fact]
    public void Without_the_data_there_is_nothing_but_the_notices_that_the_layout_shows()
    {
        _data.Bundle = null;
        _data.Status = BundleLoadStatus.NeedsNetwork;

        var cut = Show();

        cut.FindAll("h1, .cook").ShouldBeEmpty();
        _data.Initialized.ShouldBe(1);
        _device.KeepScreenOnCalls.ShouldBe(0);
    }

    [Fact]
    public void The_data_arriving_after_the_page_draws_the_steps()
    {
        _data.Bundle = null;
        var cut = Show();

        _data.Bundle = Bundle();
        _data.Raise();

        cut.WaitForAssertion(() => cut.FindAll(".cook").Count.ShouldBe(1));
        _ticker.Starts.ShouldBe(1);
    }

    [Fact]
    public async Task A_newer_bundle_changes_the_steps_without_starting_the_ticks_or_the_lit_screen_again()
    {
        var cut = Show();
        await GoTo(cut, "Ướp thịt.");

        var renamed = Braise with { Name = "Thịt kho tàu" };
        _data.Bundle = Bundle() with { Dishes = [.. Bundle().Dishes.Where(d => d.Id != "mon-nau"), renamed] };
        _data.Raise();

        cut.WaitForAssertion(() => Words(cut, ".cook__dish").ShouldBe("Thịt kho tàu"));
        _ticker.Starts.ShouldBe(1);
        _device.KeepScreenOnCalls.ShouldBe(1);
        Words(cut, ".cook__text").ShouldBe("Ướp thịt.");   // and it is still on the step it was on
    }

    [Fact]
    public void The_same_bundle_announced_again_does_not_draw_the_steps_from_the_start()
    {
        var cut = Show();
        _cooking.Saved = new CookingState("2026-10-20|cu-khac", 4, []);   // what the store holds now is not what is on the screen

        _data.Raise();

        Words(cut, ".cook__position").ShouldStartWith("Bước 1/");
        _cooking.Saved.StepIndex.ShouldBe(4);
    }
}
