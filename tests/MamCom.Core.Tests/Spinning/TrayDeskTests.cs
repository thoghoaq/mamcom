using MamCom.Core.Models;
using MamCom.Core.Spinning;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Spinning;

public class TrayDeskTests
{
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private static TrayDesk Desk(ulong seed = 1, FixedClock? clock = null, SpinnerConfig? config = null, IReadOnlyList<Dish>? catalog = null)
    {
        var settings = config ?? Config;
        var spinner = new MealSpinner(settings, PortionSettings, new SeededRandomSource(seed), clock ?? new FixedClock(Today), new SumOfStepsEstimator());
        return new TrayDesk(spinner, settings, clock ?? new FixedClock(Today));
    }

    private static SpinContext Home(IReadOnlyList<Dish>? catalog = null, Household? household = null) =>
        Context(household ?? SmallHome(), catalog);

    private static TraySession Spun(TrayDesk desk, int times = 1, SpinContext? context = null)
    {
        var session = TraySession.Empty;
        for (var i = 0; i < times; i++)
        {
            session = desk.Spin(session, context ?? Home());
        }

        return session;
    }

    private static IEnumerable<string> Ids(TraySession session) => session.Viewed!.Tray.Slots.Select(s => s.DishId);

    // ---- spinning ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_first_spin_fills_a_tray_for_today_and_puts_it_on_the_screen()
    {
        var session = Spun(Desk());

        session.History.Items.Count.ShouldBe(1);
        session.ViewIndex.ShouldBe(0);
        session.SpinsSinceConfirm.ShouldBe(1);
        session.IsConfirmed.ShouldBeFalse();
        session.Viewed!.Tray.Date.ShouldBe(Today);
        session.Viewed.Tray.Slots.Select(s => s.Slot).ShouldBe([DishSlot.Soup, DishSlot.Main, DishSlot.Vegetable]);
        session.Viewed.UnfilledSlots.ShouldBeEmpty();
    }

    [Fact]
    public void Each_spin_goes_to_the_front_and_only_the_last_few_are_kept()
    {
        var desk = Desk();
        var session = TraySession.Empty;
        var seen = new List<SpinResult>();
        for (var i = 0; i < Config.HistorySize + 3; i++)
        {
            session = desk.Spin(session, Home());
            seen.Add(session.Viewed!);
        }

        session.History.Items.Count.ShouldBe(Config.HistorySize);
        session.History.Items[0].ShouldBeSameAs(seen[^1]);
        session.History.Items[1].ShouldBeSameAs(seen[^2]);
        session.ViewIndex.ShouldBe(0);
        session.SpinsSinceConfirm.ShouldBe(Config.HistorySize + 3);
    }

    [Fact]
    public void A_locked_slot_stays_through_the_next_spin()
    {
        var desk = Desk();
        var first = desk.ToggleLock(Spun(desk), 1);
        var lockedDish = first.Viewed!.Tray.Slots[1].DishId;

        var second = desk.Spin(first, Home());

        second.Viewed!.Tray.Slots[1].DishId.ShouldBe(lockedDish);
        second.Viewed.Tray.Slots[1].IsLocked.ShouldBeTrue();
        second.History.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void Nothing_is_kept_from_a_tray_of_another_day()
    {
        var desk = Desk(clock: new FixedClock(Tomorrow));
        var yesterday = new MealTray(Today, [new SlotResult(DishSlot.Soup, "canh-bi-do", true), new SlotResult(DishSlot.Main, "ga-kho-gung", true), new SlotResult(DishSlot.Vegetable, "cai-luoc", true)]);
        var session = TraySession.Empty with { History = new SpinHistory([new SpinResult(yesterday, [], [])]) };

        var spun = desk.Spin(session, Home());

        spun.Viewed!.Tray.Date.ShouldBe(Tomorrow);
        spun.Viewed.Tray.Slots.ShouldAllBe(s => !s.IsLocked);
    }

    [Fact]
    public void A_spin_after_a_confirmation_starts_a_new_meal_with_nothing_kept_and_no_confirmation()
    {
        var desk = Desk();
        var locked = desk.ToggleLock(desk.ToggleLock(desk.ToggleLock(Spun(desk), 0), 1), 2);
        var (confirmed, _) = desk.Confirm(locked, EatenLog.Empty);

        var next = desk.Spin(confirmed, Home());

        next.IsConfirmed.ShouldBeFalse();
        next.Viewed!.Tray.Slots.ShouldAllBe(s => !s.IsLocked);
        next.SpinsSinceConfirm.ShouldBe(1);
    }

    [Fact]
    public void The_same_seed_and_the_same_session_give_the_same_tray()
    {
        Ids(Spun(Desk(seed: 7), 3)).ShouldBe(Ids(Spun(Desk(seed: 7), 3)));
    }

    // ---- swapping one dish ------------------------------------------------------------------------------------------------

    [Fact]
    public void Swapping_one_dish_changes_that_slot_only_and_adds_nothing_to_the_history()
    {
        var desk = Desk();
        var before = Spun(desk, 2);

        var after = desk.Reroll(before, Home(), 1);

        Ids(after).ElementAt(1).ShouldNotBe(Ids(before).ElementAt(1));
        Ids(after).ElementAt(0).ShouldBe(Ids(before).ElementAt(0));
        Ids(after).ElementAt(2).ShouldBe(Ids(before).ElementAt(2));
        after.History.Items.Count.ShouldBe(2);
        after.History.Items[1].ShouldBeSameAs(before.History.Items[1]);
        after.SpinsSinceConfirm.ShouldBe(before.SpinsSinceConfirm);   // a swap is not a spin
        after.ViewIndex.ShouldBe(0);
    }

    [Fact]
    public void Swapping_a_dish_of_an_older_tray_changes_that_tray_in_place()
    {
        var desk = Desk();
        var session = desk.Previous(Spun(desk, 3));

        var swapped = desk.Reroll(session, Home(), 0);

        swapped.ViewIndex.ShouldBe(1);
        swapped.History.Items[0].ShouldBeSameAs(session.History.Items[0]);
        swapped.History.Items[2].ShouldBeSameAs(session.History.Items[2]);
        Ids(swapped).First().ShouldNotBe(Ids(session).First());
    }

    [Fact]
    public void A_locked_slot_cannot_be_swapped()
    {
        var desk = Desk();
        var locked = desk.ToggleLock(Spun(desk), 0);

        Should.Throw<ArgumentException>(() => desk.Reroll(locked, Home(), 0));
    }

    [Fact]
    public void A_slot_that_is_not_on_the_tray_cannot_be_swapped()
    {
        var desk = Desk();
        var session = Spun(desk);

        Should.Throw<ArgumentOutOfRangeException>(() => desk.Reroll(session, Home(), 3));
        Should.Throw<ArgumentOutOfRangeException>(() => desk.Reroll(session, Home(), -1));
    }

    [Fact]
    public void There_is_nothing_to_swap_before_the_first_spin_or_after_the_tray_is_confirmed()
    {
        var desk = Desk();

        Should.Throw<InvalidOperationException>(() => desk.Reroll(TraySession.Empty, Home(), 0));

        var (confirmed, _) = desk.Confirm(Spun(desk), EatenLog.Empty);
        Should.Throw<InvalidOperationException>(() => desk.Reroll(confirmed, Home(), 0));
    }

    // ---- locking ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Locking_a_slot_and_locking_it_again_unlocks_it()
    {
        var desk = Desk();
        var session = Spun(desk);

        var locked = desk.ToggleLock(session, 2);
        var unlocked = desk.ToggleLock(locked, 2);

        locked.Viewed!.Tray.Slots.Select(s => s.IsLocked).ShouldBe([false, false, true]);
        unlocked.Viewed!.Tray.Slots.Select(s => s.IsLocked).ShouldBe([false, false, false]);
        Ids(locked).ShouldBe(Ids(session));   // only the lock changed
    }

    [Fact]
    public void Locking_changes_the_tray_on_the_screen_in_place_and_leaves_the_rest_of_the_history()
    {
        var desk = Desk();
        var session = desk.Previous(Spun(desk, 3));

        var locked = desk.ToggleLock(session, 0);

        locked.History.Items.Count.ShouldBe(3);
        locked.ViewIndex.ShouldBe(1);
        locked.History.Items[1].Tray.Slots[0].IsLocked.ShouldBeTrue();
        locked.History.Items[0].ShouldBeSameAs(session.History.Items[0]);
        locked.History.Items[2].ShouldBeSameAs(session.History.Items[2]);
    }

    [Fact]
    public void A_slot_that_is_not_on_the_tray_cannot_be_locked_and_neither_can_a_tray_that_is_not_there_or_is_confirmed()
    {
        var desk = Desk();
        var session = Spun(desk);

        Should.Throw<ArgumentOutOfRangeException>(() => desk.ToggleLock(session, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => desk.ToggleLock(session, -1));
        Should.Throw<InvalidOperationException>(() => desk.ToggleLock(TraySession.Empty, 0));

        var (confirmed, _) = desk.Confirm(session, EatenLog.Empty);
        Should.Throw<InvalidOperationException>(() => desk.ToggleLock(confirmed, 0));
    }

    // ---- going back -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Going_back_shows_older_trays_and_stops_at_the_oldest()
    {
        var desk = Desk();
        var session = Spun(desk, 3);

        session.CanGoPrevious.ShouldBeTrue();
        session.CanGoNext.ShouldBeFalse();

        var back1 = desk.Previous(session);
        var back2 = desk.Previous(back1);
        var stuck = desk.Previous(back2);

        back1.ViewIndex.ShouldBe(1);
        back2.ViewIndex.ShouldBe(2);
        back2.CanGoPrevious.ShouldBeFalse();
        stuck.ShouldBeSameAs(back2);
        back2.Viewed.ShouldBeSameAs(session.History.Items[2]);
    }

    [Fact]
    public void Going_forward_comes_back_to_the_newest_and_stops_there()
    {
        var desk = Desk();
        var back = desk.Previous(desk.Previous(Spun(desk, 3)));

        var forward1 = desk.Next(back);
        var forward2 = desk.Next(forward1);
        var stuck = desk.Next(forward2);

        forward1.ViewIndex.ShouldBe(1);
        forward2.ViewIndex.ShouldBe(0);
        forward2.CanGoNext.ShouldBeFalse();
        stuck.ShouldBeSameAs(forward2);
    }

    [Fact]
    public void Going_back_with_nothing_spun_does_nothing()
    {
        var desk = Desk();

        desk.Previous(TraySession.Empty).ShouldBeSameAs(TraySession.Empty);
        desk.Next(TraySession.Empty).ShouldBeSameAs(TraySession.Empty);
    }

    [Fact]
    public void Spinning_from_an_older_tray_keeps_its_locks_and_the_new_tray_goes_to_the_front()
    {
        var desk = Desk();
        var session = desk.ToggleLock(desk.Previous(Spun(desk, 3)), 1);   // the middle tray, with its main dish locked
        var lockedDish = session.Viewed!.Tray.Slots[1].DishId;

        var spun = desk.Spin(session, Home());

        spun.ViewIndex.ShouldBe(0);
        spun.Viewed!.Tray.Slots[1].DishId.ShouldBe(lockedDish);
        spun.History.Items.Count.ShouldBe(4);
        spun.History.Items[1].ShouldBeSameAs(session.History.Items[0]);
    }

    // ---- confirming -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Confirming_records_the_dishes_as_eaten_today_and_starts_the_count_again()
    {
        var desk = Desk();
        var session = Spun(desk, 4);
        var eaten = EatenLog.Empty.With(["cai-luoc"], Today.AddDays(-9));

        var (confirmed, log) = desk.Confirm(session, eaten);

        confirmed.IsConfirmed.ShouldBeTrue();
        confirmed.ConfirmedOn.ShouldBe(Today);
        confirmed.SpinsSinceConfirm.ShouldBe(0);
        foreach (var id in Ids(session))
        {
            log.LastEatenOn[id].ShouldBe(Today);
        }

        log.LastEatenOn["cai-luoc"].ShouldBe(Ids(session).Contains("cai-luoc") ? Today : Today.AddDays(-9));
        eaten.LastEatenOn.Count.ShouldBe(1);   // the log that was given is not changed
    }

    [Fact]
    public void Confirming_an_older_tray_makes_it_the_newest()
    {
        var desk = Desk();
        var session = desk.Previous(Spun(desk, 3));
        var chosen = session.Viewed!;

        var (confirmed, _) = desk.Confirm(session, EatenLog.Empty);

        confirmed.ViewIndex.ShouldBe(0);
        confirmed.History.Items[0].ShouldBeSameAs(chosen);
        confirmed.History.Items.Count.ShouldBe(3);
        confirmed.History.Items.Count(r => ReferenceEquals(r, chosen)).ShouldBe(1);
        confirmed.Viewed.ShouldBeSameAs(chosen);
    }

    [Fact]
    public void A_tray_cannot_be_confirmed_twice_or_before_it_exists_or_with_no_dish()
    {
        var desk = Desk();

        Should.Throw<InvalidOperationException>(() => desk.Confirm(TraySession.Empty, EatenLog.Empty));

        var (confirmed, _) = desk.Confirm(Spun(desk), EatenLog.Empty);
        Should.Throw<InvalidOperationException>(() => desk.Confirm(confirmed, EatenLog.Empty));

        var empty = TraySession.Empty with { History = new SpinHistory([new SpinResult(new MealTray(Today, []), [], [])]) };
        Should.Throw<InvalidOperationException>(() => desk.Confirm(empty, EatenLog.Empty));
    }

    [Fact]
    public void Reopening_takes_the_confirmation_back_and_the_tray_can_be_changed_again()
    {
        var desk = Desk();
        var (confirmed, _) = desk.Confirm(Spun(desk), EatenLog.Empty);

        var reopened = desk.Reopen(confirmed);

        reopened.IsConfirmed.ShouldBeFalse();
        reopened.Viewed.ShouldBeSameAs(confirmed.Viewed);
        Should.NotThrow(() => desk.ToggleLock(reopened, 0));
    }

    [Fact]
    public void What_was_confirmed_is_kept_off_the_next_days_trays()
    {
        var desk = Desk();
        var (_, eaten) = desk.Confirm(Spun(desk), EatenLog.Empty);
        var eatenIds = eaten.LastEatenOn.Keys.ToHashSet();

        var tomorrowDesk = Desk(clock: new FixedClock(Tomorrow));
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var next = Desk(seed, new FixedClock(Tomorrow)).Spin(TraySession.Empty, Context(SmallHome(), lastEaten: eaten.Until(Tomorrow)));
            Ids(next).ShouldNotContain(id => eatenIds.Contains(id), $"seed {seed}");
        }

        tomorrowDesk.ShouldNotBeNull();
    }

    // ---- the quick filters ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_quick_filters_are_not_offered_at_first_and_are_offered_after_enough_spins()
    {
        var desk = Desk();

        desk.ShowQuickFilters(TraySession.Empty).ShouldBeFalse();
        desk.ShowQuickFilters(Spun(desk, Config.QuickFilters.SuggestAfterSpins - 1)).ShouldBeFalse();
        desk.ShowQuickFilters(Spun(desk, Config.QuickFilters.SuggestAfterSpins)).ShouldBeTrue();
        desk.ShowQuickFilters(Spun(desk, Config.QuickFilters.SuggestAfterSpins + 4)).ShouldBeTrue();
    }

    [Fact]
    public void A_confirmation_starts_the_count_again_so_the_filters_go_away()
    {
        var desk = Desk();
        var (confirmed, _) = desk.Confirm(Spun(desk, Config.QuickFilters.SuggestAfterSpins + 1), EatenLog.Empty);

        desk.ShowQuickFilters(confirmed).ShouldBeFalse();
    }

    [Fact]
    public void A_filter_that_is_on_keeps_the_filters_on_the_screen_so_it_can_be_turned_off()
    {
        var desk = Desk();

        desk.ShowQuickFilters(desk.SetFilters(TraySession.Empty, new QuickFilters(false, true, false))).ShouldBeTrue();
        desk.ShowQuickFilters(desk.SetFilters(TraySession.Empty, QuickFilters.None)).ShouldBeFalse();
    }

    [Fact]
    public void No_filter_leaves_the_context_as_it_is()
    {
        var desk = Desk();
        var context = Home();

        desk.Narrow(context, QuickFilters.None, []).ShouldBeSameAs(context);
    }

    [Fact]
    public void The_red_meat_filter_drops_the_dishes_with_a_red_meat_protein_source()
    {
        var context = Home();
        var narrowed = Desk().Narrow(context, new QuickFilters(false, true, false), []);

        narrowed.Dishes.ShouldNotBeEmpty();
        narrowed.Dishes.ShouldAllBe(d => d.ProteinSource != "heo" && d.ProteinSource != "bo");
        narrowed.Dishes.Count.ShouldBe(Catalog().Count(d => d.ProteinSource is not ("heo" or "bo")));
        narrowed.Household.ShouldBeSameAs(context.Household);   // only the dishes change
    }

    [Fact]
    public void The_low_fat_filter_drops_the_dishes_cooked_in_a_way_the_config_names()
    {
        var narrowed = Desk().Narrow(Home(), new QuickFilters(false, false, true), []);

        narrowed.Dishes.ShouldAllBe(d => d.CookingMethod != CookingMethod.Fry);
        narrowed.Dishes.Count.ShouldBe(Catalog().Count(d => d.CookingMethod != CookingMethod.Fry));
        Catalog().Count(d => d.CookingMethod == CookingMethod.Fry).ShouldBeGreaterThan(0);   // there was something to drop
    }

    [Fact]
    public void The_filters_use_what_the_config_says_and_not_a_fixed_list()
    {
        var config = Config with { QuickFilters = Config.QuickFilters with { RedMeatProteinSources = ["ga"], LowFatExcludedMethods = [CookingMethod.Grill], MaxMinutes = 45 } };
        var desk = Desk(config: config);

        desk.Narrow(Home(), new QuickFilters(false, true, false), []).Dishes.ShouldAllBe(d => d.ProteinSource != "ga");
        desk.Narrow(Home(), new QuickFilters(false, true, false), []).Dishes.ShouldContain(d => d.ProteinSource == "heo");
        desk.Narrow(Home(), new QuickFilters(false, false, true), []).Dishes.ShouldAllBe(d => d.CookingMethod != CookingMethod.Grill);
        desk.Narrow(Home(), new QuickFilters(false, false, true), []).Dishes.ShouldContain(d => d.CookingMethod == CookingMethod.Fry);
        desk.Narrow(Home(), new QuickFilters(true, false, false), []).Household.AvailableMinutes.ShouldBe(45);
    }

    [Fact]
    public void The_short_filter_sets_the_minutes_available_and_never_raises_a_smaller_limit()
    {
        var desk = Desk();
        var quick = new QuickFilters(true, false, false);

        desk.Narrow(Home(household: SmallHome()), quick, []).Household.AvailableMinutes.ShouldBe(Config.QuickFilters.MaxMinutes);
        desk.Narrow(Home(household: SmallHome(availableMinutes: 20)), quick, []).Household.AvailableMinutes.ShouldBe(20);
        desk.Narrow(Home(household: SmallHome(availableMinutes: 90)), quick, []).Household.AvailableMinutes.ShouldBe(Config.QuickFilters.MaxMinutes);
        desk.Narrow(Home(), quick, []).Dishes.Count.ShouldBe(Catalog().Length);   // no dish is dropped by time: the spinner weighs it
    }

    [Fact]
    public void A_dish_that_has_to_stay_is_not_dropped_by_a_filter()
    {
        var narrowed = Desk().Narrow(Home(), new QuickFilters(false, true, true), ["ga-ran", "thit-kho-trung"]);

        narrowed.Dishes.Select(d => d.Id).ShouldContain("ga-ran");          // fried
        narrowed.Dishes.Select(d => d.Id).ShouldContain("thit-kho-trung");   // pork
        narrowed.Dishes.Select(d => d.Id).ShouldNotContain("tom-rang");      // fried and not kept
        narrowed.Dishes.Select(d => d.Id).ShouldNotContain("suon-nuong");    // pork and not kept
    }

    [Fact]
    public void Spinning_with_the_red_meat_filter_never_draws_red_meat_unless_it_is_locked()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var desk = Desk(seed);
            var session = desk.SetFilters(TraySession.Empty, new QuickFilters(false, true, false));
            var spun = desk.Spin(session, Home());

            Dishes(spun.Viewed!).ShouldAllBe(d => d.ProteinSource != "heo" && d.ProteinSource != "bo", $"seed {seed}");
        }
    }

    [Fact]
    public void Spinning_with_the_low_fat_filter_never_draws_a_fried_dish()
    {
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var desk = Desk(seed);
            var spun = desk.Spin(desk.SetFilters(TraySession.Empty, new QuickFilters(false, false, true)), Home());

            Dishes(spun.Viewed!).ShouldAllBe(d => d.CookingMethod != CookingMethod.Fry, $"seed {seed}");
        }
    }

    [Fact]
    public void Spinning_with_the_short_filter_keeps_to_the_minutes_when_that_can_be_done_and_says_so_when_it_cannot()
    {
        var quick = new QuickFilters(true, false, false);

        // Every dish takes 5 minutes here, so a tray is 15 minutes: well inside the limit.
        var shortDishes = Catalog().Select(d => d with { Steps = [new Step(1, "Nấu.", 5, 0, [])] }).ToArray();
        var fits = Desk().Spin(Desk().SetFilters(TraySession.Empty, quick), Home(shortDishes));
        fits.Viewed!.RelaxedRules.ShouldNotContain(RelaxedRule.AvailableMinutes);
        fits.Viewed.Tray.Slots.Sum(slot => shortDishes.Single(d => d.Id == slot.DishId).Steps.Sum(s => s.ActiveMinutes)).ShouldBeLessThanOrEqualTo(Config.QuickFilters.MaxMinutes);

        // Every dish takes an hour here, so no tray fits: the rule is given up, and the screen is told.
        var slowDishes = Catalog().Select(d => d with { Steps = [new Step(1, "Nấu.", 60, 0, [])] }).ToArray();
        var cannot = Desk().Spin(Desk().SetFilters(TraySession.Empty, quick), Home(slowDishes));
        cannot.Viewed!.RelaxedRules.ShouldContain(RelaxedRule.AvailableMinutes);
    }

    [Fact]
    public void Swapping_a_dish_with_a_filter_on_works_even_when_the_tray_holds_a_dish_the_filter_would_drop()
    {
        var desk = Desk();
        var friedTray = new MealTray(Today, [new SlotResult(DishSlot.Soup, "canh-bi-do", false), new SlotResult(DishSlot.Main, "ga-ran", false), new SlotResult(DishSlot.Vegetable, "cai-luoc", false)]);
        var session = desk.SetFilters(TraySession.Empty with { History = new SpinHistory([new SpinResult(friedTray, [], [])]) }, new QuickFilters(false, false, true));

        var swapped = Should.NotThrow(() => desk.Reroll(session, Home(), 0));

        Ids(swapped).ElementAt(1).ShouldBe("ga-ran");   // the dish that was not asked about stays
    }

    [Fact]
    public void A_locked_dish_that_a_filter_would_drop_is_still_kept_by_the_next_spin()
    {
        var friedTray = new MealTray(Today, [new SlotResult(DishSlot.Soup, "canh-bi-do", false), new SlotResult(DishSlot.Main, "ga-ran", true), new SlotResult(DishSlot.Vegetable, "cai-luoc", false)]);
        var session = TraySession.Empty with { History = new SpinHistory([new SpinResult(friedTray, [], [])]), Filters = new QuickFilters(false, false, true) };

        for (ulong seed = 1; seed <= 20; seed++)
        {
            var spun = Desk(seed).Spin(session, Home());

            spun.Viewed!.Tray.Slots[1].DishId.ShouldBe("ga-ran", $"seed {seed}");
            spun.Viewed.Tray.Slots[1].IsLocked.ShouldBeTrue();
            Dishes(spun.Viewed).Where((_, index) => index != 1).ShouldAllBe(d => d.CookingMethod != CookingMethod.Fry, $"seed {seed}");
        }
    }

    [Fact]
    public void Swapping_with_a_filter_on_draws_only_what_the_filter_allows()
    {
        var porkTray = new MealTray(Today, [new SlotResult(DishSlot.Soup, "canh-bi-do", false), new SlotResult(DishSlot.Main, "thit-kho-trung", false), new SlotResult(DishSlot.Vegetable, "cai-luoc", false)]);
        var session = TraySession.Empty with { History = new SpinHistory([new SpinResult(porkTray, [], [])]), Filters = new QuickFilters(false, true, false) };

        for (ulong seed = 1; seed <= 30; seed++)
        {
            var swapped = Desk(seed).Reroll(session, Home(), 1);

            var main = Find(swapped.Viewed!.Tray.Slots[1].DishId);
            main.ProteinSource.ShouldNotBeOneOf("heo", "bo");
            main.Id.ShouldNotBe("thit-kho-trung");
        }
    }

    [Fact]
    public void The_filters_are_kept_with_the_session_and_do_not_change_it_otherwise()
    {
        var desk = Desk();
        var session = Spun(desk, 2);
        var filters = new QuickFilters(true, false, true);

        var changed = desk.SetFilters(session, filters);

        changed.Filters.ShouldBe(filters);
        changed.History.ShouldBeSameAs(session.History);
        changed.SpinsSinceConfirm.ShouldBe(2);
    }

    // ---- arguments --------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_arguments_are_checked()
    {
        var spinner = Spinner();
        var clock = new FixedClock(Today);
        var desk = Desk();

        Should.Throw<ArgumentNullException>(() => new TrayDesk(null!, Config, clock));
        Should.Throw<ArgumentNullException>(() => new TrayDesk(spinner, null!, clock));
        Should.Throw<ArgumentNullException>(() => new TrayDesk(spinner, Config, null!));
        Should.Throw<ArgumentException>(() => new TrayDesk(spinner, Config with { HistorySize = 0 }, clock));
        Should.Throw<ArgumentNullException>(() => desk.Spin(null!, Home()));
        Should.Throw<ArgumentNullException>(() => desk.Spin(TraySession.Empty, null!));
        Should.Throw<ArgumentNullException>(() => desk.Reroll(Spun(desk), null!, 0));
        Should.Throw<ArgumentNullException>(() => desk.Previous(null!));
        Should.Throw<ArgumentNullException>(() => desk.Next(null!));
        Should.Throw<ArgumentNullException>(() => desk.Confirm(Spun(desk), null!));
        Should.Throw<ArgumentNullException>(() => desk.Reopen(null!));
        Should.Throw<ArgumentNullException>(() => desk.SetFilters(null!, QuickFilters.None));
        Should.Throw<ArgumentNullException>(() => desk.SetFilters(TraySession.Empty, null!));
        Should.Throw<ArgumentNullException>(() => desk.ShowQuickFilters(null!));
        Should.Throw<ArgumentNullException>(() => desk.Narrow(null!, QuickFilters.None, []));
        Should.Throw<ArgumentNullException>(() => desk.Narrow(Home(), null!, []));
        Should.Throw<ArgumentNullException>(() => desk.Narrow(Home(), QuickFilters.None, null!));
    }
}
