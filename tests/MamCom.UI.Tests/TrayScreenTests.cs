using AngleSharp.Dom;
using Bunit.TestDoubles;
using MamCom.Core;
using MamCom.Core.Models;
using MamCom.Core.Spinning;
using MamCom.Data;
using MamCom.Data.Bundles;
using MamCom.Data.Device;
using MamCom.Data.State;
using MamCom.UI.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace MamCom.UI.Tests;

public class TrayScreenTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    private readonly FakeAppData _data = new() { Bundle = TestConfig.SeedBundle };
    private readonly FakeTrayStore _trays = new();
    private readonly FakeEatenLogStore _eaten = new();
    private readonly FakeDeviceFeatures _device = new();
    private readonly FakeSpinPacer _pacer = new();
    private readonly FixedClock _clock = new(Today);

    public TrayScreenTests()
    {
        Services.AddSingleton<IAppData>(_data);
        Services.AddSingleton<ITrayStore>(_trays);
        Services.AddSingleton<IEatenLogStore>(_eaten);
        Services.AddSingleton<IDeviceFeatures>(_device);
        Services.AddSingleton<ISpinPacer>(_pacer);
        Services.AddSingleton<IClock>(_clock);
        Services.AddSingleton<IRandomSource>(new SeededRandomSource(11));
    }

    private static Member Adult(string name, params MemberTag[] tags) => new(name, AgeGroup.Adult, 2, tags, []);

    private static Household TwoAdults(params MemberTag[] tags) => new(Region.North, BowlSize.Medium, [Adult("Bố", tags), Adult("Mẹ")]);

    /// <summary>Five adults at two bowls are exactly 5.0 servings: the tray gets a second main dish.</summary>
    private static Household BigFamily() => new(Region.North, BowlSize.Medium, [.. Enumerable.Range(0, 5).Select(i => Adult($"P{i}"))]);

    private IRenderedComponent<TrayScreen> Show(Household? household = null) => Render<TrayScreen>(p => p.Add(s => s.Household, household ?? TwoAdults()));

    private static IReadOnlyList<IElement> Cards(IRenderedComponent<TrayScreen> cut) => cut.FindAll(".dish-card");

    private static string[] Names(IRenderedComponent<TrayScreen> cut) => [.. cut.FindAll(".dish-card__name").Select(n => n.TextContent)];

    private static IElement SpinButton(IRenderedComponent<TrayScreen> cut) => cut.FindAll(".app-actions button")[0];

    private static IElement ConfirmButton(IRenderedComponent<TrayScreen> cut) => cut.FindAll(".app-actions button")[1];

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    private static Dish DishNamed(string name) => TestConfig.SeedBundle.Dishes.Single(d => d.Name == name);

    private static async Task Spin(IRenderedComponent<TrayScreen> cut) => await SpinButton(cut).ClickAsync(new());

    private static IElement Lock(IRenderedComponent<TrayScreen> cut, int card) => Cards(cut)[card].QuerySelector(".dish-card__lock")!;

    private static IElement Swap(IRenderedComponent<TrayScreen> cut, int card) => Cards(cut)[card].QuerySelector(".dish-card__swap")!;

    private static MealTray TrayOf(params (DishSlot Slot, string Id, bool Locked)[] slots) =>
        new(Today, [.. slots.Select(s => new SlotResult(s.Slot, s.Id, s.Locked))]);

    private static TraySession SessionOf(SpinResult result, int spins = 1) => new(new SpinHistory([result]), 0, spins, QuickFilters.None, null);

    private static SpinResult Known(RelaxedRule[]? rules = null, DishSlot[]? unfilled = null) => new(
        TrayOf((DishSlot.Soup, "canh-chua-ca-loc", false), (DishSlot.Main, "thit-kho-trung", false), (DishSlot.Vegetable, "rau-muong-luoc", false)),
        rules ?? [],
        unfilled ?? []);

    // ---- opening the screen -----------------------------------------------------------------------------------------------

    [Fact]
    public void The_first_visit_spins_a_tray_at_once_and_keeps_it_without_a_rolling_effect_or_a_buzz()
    {
        var cut = Show();

        cut.Find("h1").TextContent.ShouldBe("Mâm hôm nay");
        cut.FindAll(".dish-card__slot").Select(s => s.TextContent).ShouldBe(["Canh", "Món mặn", "Rau"]);
        _trays.Saved!.History.Items.Count.ShouldBe(1);
        _trays.Saved.Viewed!.Tray.Date.ShouldBe(Today);
        _pacer.Waits.ShouldBe(0);
        _device.Vibrations.ShouldBeEmpty();
        _data.Initialized.ShouldBe(1);
    }

    [Fact]
    public void A_household_that_needs_it_gets_a_second_main_dish()
    {
        Show(BigFamily()).FindAll(".dish-card__slot").Select(s => s.TextContent).ShouldBe(["Canh", "Món mặn", "Món mặn", "Rau"]);
    }

    [Fact]
    public void The_household_is_told_at_the_end_of_the_page_with_a_way_to_change_it()
    {
        var cut = Show();

        cut.Find(".household-home").TextContent.Plain().ShouldContain("Nhà mình: 2 người, ≈ 2 suất mỗi bữa.");
        cut.Find(".household-home a").GetAttribute("href").ShouldBe(Routes.Household);
    }

    [Fact]
    public void A_tray_of_today_that_was_left_on_the_screen_is_there_again_with_nothing_new_spun()
    {
        var first = Show();
        var names = Names(first);
        var saves = _trays.Saves;
        var history = _trays.Saved!.History.Items.Count;

        var again = Show();

        Names(again).ShouldBe(names);
        _trays.Saves.ShouldBe(saves);
        _trays.Saved!.History.Items.Count.ShouldBe(history);
    }

    [Fact]
    public void The_locks_that_were_left_are_there_again()
    {
        var first = Show();
        Lock(first, 1).Click();

        var again = Show();

        Lock(again, 1).GetAttribute("aria-pressed").ShouldBe("true");
        Cards(again)[1].ClassList.ShouldContain("dish-card--locked");
    }

    [Fact]
    public void A_tray_of_another_day_is_not_taken_up_and_a_new_one_is_spun()
    {
        _trays.Saved = SessionOf(new SpinResult(new MealTray(Today.AddDays(-1), [new SlotResult(DishSlot.Soup, "canh-chua-ca-loc", true)]), [], []), spins: 7);

        Show();

        _trays.Saved!.History.Items.Count.ShouldBe(1);
        _trays.Saved.Viewed!.Tray.Date.ShouldBe(Today);
        _trays.Saved.SpinsSinceConfirm.ShouldBe(1);
        _trays.Saved.Viewed.Tray.Slots.ShouldAllBe(s => !s.IsLocked);
    }

    [Fact]
    public void A_tray_with_a_dish_that_is_no_longer_in_the_data_is_not_taken_up()
    {
        _trays.Saved = SessionOf(new SpinResult(TrayOf((DishSlot.Soup, "mon-da-bi-go", false)), [], []));

        var cut = Show();

        Names(cut).ShouldNotContain(n => n == "mon-da-bi-go");
        cut.FindAll(".dish-card").Count.ShouldBe(3);
    }

    [Fact]
    public void A_confirmed_tray_of_today_comes_back_confirmed()
    {
        var first = Show();
        ConfirmButton(first).Click();

        var again = Show();

        again.Find(".notice--ok").TextContent.ShouldBe("Đã chốt mâm hôm nay.");
        again.FindAll(".app-actions button").Select(b => b.TextContent.Trim()).ShouldBe(["Chọn mâm khác"]);
    }

    [Fact]
    public void Without_the_data_there_is_only_the_heading_because_the_notices_say_what_is_wrong()
    {
        _data.Bundle = null;
        _data.Status = BundleLoadStatus.NeedsNetwork;

        var cut = Show();

        cut.Find("h1").TextContent.ShouldBe("Mâm hôm nay");
        cut.FindAll(".dish-card").ShouldBeEmpty();
        cut.FindAll("button").ShouldBeEmpty();
        _trays.Saves.ShouldBe(0);
    }

    // ---- spinning ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task While_the_dice_roll_the_cards_show_it_and_nothing_can_be_pressed_and_then_the_new_tray_comes_with_a_buzz()
    {
        var cut = Show();
        var before = Names(cut);
        _pacer.Gate = new TaskCompletionSource();

        var spin = SpinButton(cut).ClickAsync(new());

        Cards(cut).ShouldAllBe(c => c.ClassList.Contains("dish-card--rolling"));
        cut.Find(".tray").GetAttribute("aria-busy").ShouldBe("true");
        SpinButton(cut).TextContent.Trim().ShouldBe("Đang quay…");
        SpinButton(cut).HasAttribute("disabled").ShouldBeTrue();
        ConfirmButton(cut).HasAttribute("disabled").ShouldBeTrue();
        Cards(cut).ShouldAllBe(c => c.QuerySelector(".dish-card__lock")!.HasAttribute("disabled") && c.QuerySelector(".dish-card__swap")!.HasAttribute("disabled"));
        Names(cut).ShouldBe(before);   // the old tray is still the one shown

        _pacer.Gate.SetResult();
        await spin;

        Cards(cut).ShouldAllBe(c => !c.ClassList.Contains("dish-card--rolling"));
        cut.Find(".tray").GetAttribute("aria-busy").ShouldBe("false");
        SpinButton(cut).TextContent.Trim().ShouldBe("Quay mâm");
        Names(cut).ShouldNotBe(before);
        _device.Vibrations.ShouldBe([TrayScreen.VibrationMilliseconds]);
        _pacer.Waits.ShouldBe(1);
        _trays.Saved!.History.Items.Count.ShouldBe(2);
        _trays.Saved.SpinsSinceConfirm.ShouldBe(2);
    }

    [Fact]
    public async Task A_locked_card_does_not_roll()
    {
        var cut = Show();
        Lock(cut, 1).Click();
        _pacer.Gate = new TaskCompletionSource();

        var spin = SpinButton(cut).ClickAsync(new());

        Cards(cut).Select(c => c.ClassList.Contains("dish-card--rolling")).ShouldBe([true, false, true]);

        _pacer.Gate.SetResult();
        await spin;
    }

    [Fact]
    public async Task A_second_press_while_the_dice_roll_does_nothing()
    {
        var cut = Show();
        _pacer.Gate = new TaskCompletionSource();

        var first = SpinButton(cut).ClickAsync(new());
        var second = SpinButton(cut).ClickAsync(new());   // the browser would not send it: the button is off, but the guard is there too

        _pacer.Waits.ShouldBe(1);
        _pacer.Gate.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);

        _trays.Saved!.History.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_phone_that_cannot_buzz_is_no_problem()
    {
        _device.CanVibrate = false;
        var cut = Show();

        await Spin(cut);

        Names(cut).Length.ShouldBe(3);
    }

    [Fact]
    public async Task After_a_spin_the_dishes_are_announced_to_a_screen_reader()
    {
        var cut = Show();

        await Spin(cut);

        var announced = cut.Find(".visually-hidden[role=status]").TextContent;
        announced.ShouldStartWith("Mâm: ");
        foreach (var name in Names(cut))
        {
            announced.ShouldContain(name);
        }
    }

    // ---- locking and swapping ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Spin_lock_spin_confirm_the_flow_of_the_task_works_from_start_to_end()
    {
        var cut = Show();
        await Spin(cut);

        Lock(cut, 1).Click();
        var kept = Names(cut)[1];
        await Spin(cut);

        Names(cut)[1].ShouldBe(kept);
        Cards(cut)[1].ClassList.ShouldContain("dish-card--locked");

        ConfirmButton(cut).Click();

        cut.Find(".notice--ok").ShouldNotBeNull();
        _trays.Saved!.IsConfirmed.ShouldBeTrue();
        _eaten.Saved.LastEatenOn.Keys.Order(StringComparer.Ordinal).ShouldBe(_trays.Saved.Viewed!.Tray.Slots.Select(s => s.DishId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Locking_is_kept_and_unlocking_works_too()
    {
        var cut = Show();

        Lock(cut, 0).Click();
        _trays.Saved!.Viewed!.Tray.Slots[0].IsLocked.ShouldBeTrue();
        Swap(cut, 0).HasAttribute("disabled").ShouldBeTrue();

        Lock(cut, 0).Click();
        _trays.Saved!.Viewed!.Tray.Slots[0].IsLocked.ShouldBeFalse();
        Swap(cut, 0).HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Swapping_a_dish_changes_that_dish_only_and_adds_no_tray_and_no_roll()
    {
        var cut = Show();
        var before = Names(cut);

        Swap(cut, 2).Click();

        var after = Names(cut);
        after[0].ShouldBe(before[0]);
        after[1].ShouldBe(before[1]);
        after[2].ShouldNotBe(before[2]);
        _trays.Saved!.History.Items.Count.ShouldBe(1);
        _trays.Saved.SpinsSinceConfirm.ShouldBe(1);   // a swap is not a spin
        _pacer.Waits.ShouldBe(0);
        cut.Find(".visually-hidden[role=status]").TextContent.ShouldContain(after[2]);
    }

    [Fact]
    public async Task A_swipe_along_a_card_swaps_it_and_a_swipe_along_a_locked_card_does_not()
    {
        var cut = Show();
        var before = Names(cut);

        await Cards(cut)[0].TriggerEventAsync("onpointerdown", new PointerEventArgs { ClientX = 260, ClientY = 40 });
        await Cards(cut)[0].TriggerEventAsync("onpointerup", new PointerEventArgs { ClientX = 120, ClientY = 44 });

        Names(cut)[0].ShouldNotBe(before[0]);
        Names(cut)[1].ShouldBe(before[1]);

        Lock(cut, 1).Click();
        var locked = Names(cut)[1];
        await Cards(cut)[1].TriggerEventAsync("onpointerdown", new PointerEventArgs { ClientX = 260, ClientY = 40 });
        await Cards(cut)[1].TriggerEventAsync("onpointerup", new PointerEventArgs { ClientX = 120, ClientY = 44 });

        Names(cut)[1].ShouldBe(locked);
    }

    [Fact]
    public async Task A_swap_asked_for_on_a_card_that_cannot_be_swapped_is_ignored()
    {
        var cut = Show();
        Lock(cut, 0).Click();
        var before = Names(cut);

        await Swap(cut, 0).ClickAsync(new());   // the browser keeps a disabled button quiet; the screen does not rely on that

        Names(cut).ShouldBe(before);
        _trays.Saved!.History.Items.Count.ShouldBe(1);
    }

    // ---- the summary ------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_summary_says_how_long_what_energy_how_much_rice_and_what_to_buy()
    {
        var cut = Show();

        var rows = cut.FindAll(".tray-summary__row").Select(r => (r.QuerySelector("dt")!.TextContent.Trim(), r.QuerySelector("dd")!.TextContent.Plain())).ToArray();

        rows.Select(r => r.Item1).ShouldBe(["Nấu cả mâm", "Mỗi người, cả cơm", "Gạo nấu cơm", "Cần mua"]);
        rows[0].Item2.ShouldEndWith("phút");
        rows[2].Item2.ShouldBe("≈ 160 g");   // two adults, two bowls each, 40 g a bowl
        rows[3].Item2.ShouldEndWith("thứ");
        int.Parse(rows[3].Item2.Split(' ')[0]).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void The_cards_say_how_long_each_dish_takes_and_that_the_energy_is_not_known_yet()
    {
        var cut = Show();

        foreach (var card in Cards(cut))
        {
            var facts = card.QuerySelectorAll(".dish-card__fact").Select(f => f.TextContent.Plain()).ToArray();
            facts[0].ShouldEndWith("phút");
            facts[1].ShouldBe("chưa đủ số liệu");   // the seed has no confirmed energy values, and an unknown value is said to be unknown
        }
    }

    // ---- going back -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task With_one_tray_there_is_nowhere_to_go_and_after_a_spin_the_trays_can_be_stepped_through()
    {
        var cut = Show();
        cut.FindAll(".tray-history").ShouldBeEmpty();
        var first = Names(cut);
        await Spin(cut);
        var second = Names(cut);

        var nav = cut.FindAll(".tray-history button");
        cut.Find(".tray-history__position").TextContent.ShouldBe("Mâm 2/2");
        nav[0].TextContent.Trim().ShouldBe("Mâm trước");
        nav[0].HasAttribute("disabled").ShouldBeFalse();
        nav[1].HasAttribute("disabled").ShouldBeTrue();

        nav[0].Click();

        Names(cut).ShouldBe(first);
        cut.Find(".tray-history__position").TextContent.ShouldBe("Mâm 1/2");
        cut.FindAll(".tray-history button")[0].HasAttribute("disabled").ShouldBeTrue();
        cut.FindAll(".tray-history button")[1].HasAttribute("disabled").ShouldBeFalse();
        _trays.Saved!.ViewIndex.ShouldBe(1);

        cut.FindAll(".tray-history button")[1].Click();

        Names(cut).ShouldBe(second);
        cut.Find(".tray-history__position").TextContent.ShouldBe("Mâm 2/2");
    }

    [Fact]
    public async Task The_trays_cannot_be_stepped_through_while_the_dice_roll()
    {
        var cut = Show();
        await Spin(cut);
        _pacer.Gate = new TaskCompletionSource();

        var spin = SpinButton(cut).ClickAsync(new());

        cut.FindAll(".tray-history button").ShouldAllBe(b => b.HasAttribute("disabled"));

        _pacer.Gate.SetResult();
        await spin;
        cut.FindAll(".tray-history button")[0].HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Going_forward_is_off_too_while_the_dice_roll_on_an_older_tray()
    {
        var cut = Show();
        await Spin(cut);
        await Spin(cut);
        cut.FindAll(".tray-history button")[0].Click();   // an older tray: both ways are open
        cut.FindAll(".tray-history button")[1].HasAttribute("disabled").ShouldBeFalse();
        _pacer.Gate = new TaskCompletionSource();

        var spin = SpinButton(cut).ClickAsync(new());

        cut.FindAll(".tray-history button").ShouldAllBe(b => b.HasAttribute("disabled"));

        _pacer.Gate.SetResult();
        await spin;
    }

    [Fact]
    public async Task The_trays_that_were_looked_at_are_remembered_so_the_one_on_the_screen_comes_back()
    {
        var cut = Show();
        await Spin(cut);
        var first = _trays.Saved!.History.Items[1].Tray.Slots.Select(s => s.DishId).ToArray();
        cut.FindAll(".tray-history button")[0].Click();

        var again = Show();

        again.Find(".tray-history__position").TextContent.ShouldBe("Mâm 1/2");
        _trays.Saved!.Viewed!.Tray.Slots.Select(s => s.DishId).ShouldBe(first);
    }

    // ---- confirming -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Confirming_says_so_turns_off_the_cards_and_offers_to_choose_another()
    {
        var cut = Show();

        ConfirmButton(cut).Click();

        cut.Find(".notice--ok").TextContent.ShouldBe("Đã chốt mâm hôm nay.");
        cut.FindAll(".app-actions button").Select(b => b.TextContent.Trim()).ShouldBe(["Chọn mâm khác"]);
        Cards(cut).ShouldAllBe(c => c.QuerySelector(".dish-card__lock")!.HasAttribute("disabled") && c.QuerySelector(".dish-card__swap")!.HasAttribute("disabled"));
        cut.FindAll(".tray-hint").ShouldBeEmpty();
        _eaten.Saves.ShouldBe(1);
        _eaten.Saved.LastEatenOn.Count.ShouldBe(3);
        _eaten.Saved.LastEatenOn.Values.ShouldAllBe(d => d == Today);
    }

    [Fact]
    public void Confirming_opens_the_shopping_list_and_the_way_back_finds_the_tray_confirmed_with_a_way_to_the_list_again()
    {
        var cut = Show();
        Navigation.History.ShouldBeEmpty();

        ConfirmButton(cut).Click();

        Navigation.History.ShouldHaveSingleItem().Uri.ShouldBe(Routes.Shopping);
        Routes.Shopping.ShouldBe("di-cho");
        var again = Show();
        again.Find(".notice--ok").TextContent.ShouldBe("Đã chốt mâm hôm nay.");
        var link = again.Find(".app-actions a");
        link.GetAttribute("href").ShouldBe(Routes.Shopping);
        link.TextContent.Trim().ShouldBe("Đi chợ");
    }

    [Fact]
    public async Task A_tray_still_being_chosen_has_no_shopping_link_and_spinning_does_not_open_the_list()
    {
        var cut = Show();
        await Spin(cut);

        cut.FindAll(".app-actions a").ShouldBeEmpty();
        Navigation.History.ShouldBeEmpty();
    }

    [Fact]
    public void Choosing_another_after_confirming_gives_the_controls_back_and_keeps_the_dishes_as_eaten()
    {
        var cut = Show();
        var names = Names(cut);
        ConfirmButton(cut).Click();

        cut.Find(".app-actions button").Click();

        cut.FindAll(".notice--ok").ShouldBeEmpty();
        cut.FindAll(".app-actions button").Select(b => b.TextContent.Trim()).ShouldBe(["Quay mâm", "Chốt mâm"]);
        Names(cut).ShouldBe(names);
        Lock(cut, 0).HasAttribute("disabled").ShouldBeFalse();
        _eaten.Saved.LastEatenOn.Count.ShouldBe(3);
    }

    [Fact]
    public async Task What_was_confirmed_is_kept_off_the_next_tray()
    {
        var cut = Show();
        await Spin(cut);
        ConfirmButton(cut).Click();
        var eaten = _eaten.Saved.LastEatenOn.Keys.ToHashSet();
        cut.Find(".app-actions button").Click();

        for (var i = 0; i < 4; i++)
        {
            await Spin(cut);
            foreach (var name in Names(cut))
            {
                eaten.ShouldNotContain(DishNamed(name).Id, $"spin {i}");
            }
        }
    }

    [Fact]
    public void A_tray_with_no_dish_cannot_be_confirmed()
    {
        _trays.Saved = SessionOf(new SpinResult(new MealTray(Today, []), [], [DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable]));

        var cut = Show();

        ConfirmButton(cut).HasAttribute("disabled").ShouldBeTrue();
        cut.FindAll(".dish-card").ShouldBeEmpty();
        cut.Markup.ShouldContain("Chưa có món nào hợp để quay.");
    }

    // ---- the quick filters ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_quick_filters_come_after_five_spins_and_go_when_the_tray_is_confirmed()
    {
        var cut = Show();
        cut.FindAll(".tray-filters").ShouldBeEmpty();

        for (var i = 0; i < 3; i++)
        {
            await Spin(cut);
            cut.FindAll(".tray-filters").ShouldBeEmpty();
        }

        await Spin(cut);   // the fifth, counting the one that opened the screen

        cut.FindAll(".tray-filters .chip").Select(c => c.TextContent.Plain()).ShouldBe(["Dưới 45 phút", "Không thịt đỏ", "Ít dầu mỡ"]);

        ConfirmButton(cut).Click();

        cut.FindAll(".tray-filters").ShouldBeEmpty();
    }

    [Fact]
    public async Task Switching_a_filter_on_spins_again_with_it_keeps_it_and_shows_it_pressed()
    {
        var cut = Show();
        for (var i = 0; i < 4; i++)
        {
            await Spin(cut);
        }

        var waits = _pacer.Waits;
        await cut.FindAll(".tray-filters .chip")[1].ClickAsync(new());   // no red meat

        _pacer.Waits.ShouldBe(waits + 1);
        cut.FindAll(".tray-filters .chip").Select(c => c.GetAttribute("aria-pressed")).ShouldBe(["false", "true", "false"]);
        _trays.Saved!.Filters.ShouldBe(new QuickFilters(false, true, false));
        Names(cut).ShouldAllBe(name => DishNamed(name).ProteinSource != "heo" && DishNamed(name).ProteinSource != "bo");
    }

    [Fact]
    public async Task Each_filter_changes_the_next_spins_and_switching_it_off_asks_for_another_spin()
    {
        var cut = Show();
        for (var i = 0; i < 4; i++)
        {
            await Spin(cut);
        }

        await cut.FindAll(".tray-filters .chip")[2].ClickAsync(new());   // low fat
        Names(cut).ShouldAllBe(name => DishNamed(name).CookingMethod != CookingMethod.Fry);
        _trays.Saved!.Filters.LowFat.ShouldBeTrue();

        await cut.FindAll(".tray-filters .chip")[2].ClickAsync(new());   // and off
        _trays.Saved!.Filters.LowFat.ShouldBeFalse();
        cut.FindAll(".tray-filters .chip")[2].GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public async Task Every_filter_can_be_switched_off_again()
    {
        var cut = Show();
        for (var i = 0; i < 4; i++)
        {
            await Spin(cut);
        }

        for (var chip = 0; chip < 3; chip++)
        {
            await cut.FindAll(".tray-filters .chip")[chip].ClickAsync(new());
            cut.FindAll(".tray-filters .chip")[chip].GetAttribute("aria-pressed").ShouldBe("true");
            await cut.FindAll(".tray-filters .chip")[chip].ClickAsync(new());
            cut.FindAll(".tray-filters .chip")[chip].GetAttribute("aria-pressed").ShouldBe("false");
        }

        _trays.Saved!.Filters.ShouldBe(QuickFilters.None);
    }

    [Fact]
    public async Task A_confirmed_tray_hides_the_filters_even_when_one_is_on()
    {
        var cut = Show();
        for (var i = 0; i < 4; i++)
        {
            await Spin(cut);
        }

        await cut.FindAll(".tray-filters .chip")[1].ClickAsync(new());
        cut.FindAll(".tray-filters").ShouldNotBeEmpty();

        ConfirmButton(cut).Click();

        cut.FindAll(".tray-filters").ShouldBeEmpty();
    }

    [Fact]
    public async Task The_short_filter_is_told_as_a_notice_when_no_tray_can_keep_to_it()
    {
        var shortDishes = TestConfig.SeedBundle with { Dishes = [.. TestConfig.SeedBundle.Dishes.Select(d => d with { Steps = [.. d.Steps.Select(s => s with { ActiveMinutes = 60, PassiveMinutes = 0 })] })] };
        _data.Bundle = shortDishes;
        var cut = Show();
        for (var i = 0; i < 4; i++)
        {
            await Spin(cut);
        }

        await cut.FindAll(".tray-filters .chip")[0].ClickAsync(new());

        cut.Find(".notice--warning").TextContent.ShouldContain("Mâm này nấu lâu hơn thời gian bạn có.");
    }

    [Fact]
    public async Task The_filters_that_were_on_are_still_on_when_the_screen_opens_again()
    {
        var cut = Show();
        for (var i = 0; i < 4; i++)
        {
            await Spin(cut);
        }

        await cut.FindAll(".tray-filters .chip")[0].ClickAsync(new());

        var again = Show();

        again.FindAll(".tray-filters .chip")[0].GetAttribute("aria-pressed").ShouldBe("true");
    }

    // ---- what the spinner gave up -----------------------------------------------------------------------------------------

    [Fact]
    public void A_rule_the_spinner_gave_up_and_a_slot_it_could_not_fill_are_told_above_the_cards()
    {
        _trays.Saved = SessionOf(new SpinResult(
            TrayOf((DishSlot.Soup, "canh-chua-ca-loc", false), (DishSlot.Main, "thit-kho-trung", false)),
            [RelaxedRule.DistinctProteinSources],
            [DishSlot.Vegetable]));

        var cut = Show();

        cut.Find(".notice--warning").QuerySelectorAll("li").Select(li => li.TextContent).ShouldBe(
        [
            "Mâm này có hai món cùng loại đạm.",
            "Chưa tìm được rau hợp. Thử quay lại hoặc bỏ bớt bộ lọc.",
        ]);
    }

    [Fact]
    public void A_tray_that_gave_up_nothing_has_no_notice_of_its_own()
    {
        _trays.Saved = SessionOf(Known());

        Show().FindAll(".notice--warning").ShouldBeEmpty();
    }

    // ---- chilli -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Somebody_who_does_not_eat_spicy_food_is_told_which_dish_has_chilli_to_leave_out()
    {
        _trays.Saved = SessionOf(Known());

        var cut = Show(TwoAdults(MemberTag.NoSpicy));

        var notes = Cards(cut).Select(c => c.QuerySelector(".dish-card__aside")?.TextContent).ToArray();
        notes.ShouldBe(["Để ớt riêng", null, null]);   // only the soup of the three has chilli that is optional
    }

    [Fact]
    public void A_dish_that_needs_its_chilli_is_not_offered_the_leave_it_out_note()
    {
        _trays.Saved = SessionOf(new SpinResult(
            TrayOf((DishSlot.Soup, "canh-chua-ca-loc", false), (DishSlot.Main, "ga-xao-sa-ot", false), (DishSlot.Vegetable, "rau-muong-luoc", false)), [], []));

        var cut = Show(TwoAdults(MemberTag.NoSpicy));

        Cards(cut).Select(c => c.QuerySelector(".dish-card__aside") is not null).ShouldBe([true, false, false]);   // a dish with chilli that cannot be left out has no such note
    }

    [Fact]
    public void Nobody_is_told_about_chilli_when_everyone_eats_it()
    {
        _trays.Saved = SessionOf(Known());

        Show(TwoAdults()).FindAll(".dish-card__aside").ShouldBeEmpty();
    }

    // ---- new data ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void When_a_newer_bundle_is_taken_in_the_screen_goes_on_with_the_dishes_that_are_still_there()
    {
        _trays.Saved = SessionOf(Known());
        var cut = Show();
        var newer = TestConfig.SeedBundle with { Version = "v2", Dishes = [.. TestConfig.SeedBundle.Dishes.Where(d => d.Id != "bap-cai-luoc")] };

        _data.Bundle = newer;
        _data.Raise();

        cut.WaitForAssertion(() => Names(cut).ShouldBe(["Canh chua cá lóc", "Thịt kho trứng", "Rau muống luộc"]));
    }

    [Fact]
    public void When_a_newer_bundle_takes_away_a_dish_on_the_screen_a_new_tray_is_spun_from_the_new_data()
    {
        _trays.Saved = SessionOf(Known());
        var cut = Show();
        var removed = new HashSet<string> { "canh-chua-ca-loc", "thit-kho-trung", "rau-muong-luoc" };
        var newer = TestConfig.SeedBundle with { Version = "v2", Dishes = [.. TestConfig.SeedBundle.Dishes.Where(d => !removed.Contains(d.Id))] };

        _data.Bundle = newer;
        _data.Raise();

        cut.WaitForAssertion(() =>
        {
            Names(cut).Length.ShouldBe(3);
            Names(cut).ShouldAllBe(name => newer.Dishes.Any(d => d.Name == name));
        });
    }

    [Fact]
    public async Task A_screen_that_is_gone_stops_listening_to_the_data()
    {
        Show();
        _data.Listeners.ShouldBe(2);   // the screen, and the line about the household inside it

        await DisposeComponentsAsync();

        _data.Listeners.ShouldBe(0);
    }

    // ---- everything can be reached ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Every_button_has_a_name_and_the_list_of_dishes_is_named()
    {
        var cut = Show();
        for (var i = 0; i < 4; i++)
        {
            await Spin(cut);
        }

        foreach (var button in cut.FindAll("button"))
        {
            (button.GetAttribute("aria-label") ?? button.TextContent.Trim()).ShouldNotBeNullOrWhiteSpace(button.OuterHtml);
            button.GetAttribute("type").ShouldBe("button");
        }

        cut.Find(".tray").GetAttribute("aria-label").ShouldBe("Các món của mâm");
        cut.Find("nav").GetAttribute("aria-label").ShouldBe("Các mâm đã quay");
    }

    [Fact]
    public void The_swipe_is_explained_while_the_tray_can_still_be_changed()
    {
        Show().Find(".tray-hint").TextContent.ShouldBe("Vuốt thẻ sang trái hoặc phải để đổi món đó.");
    }
}
