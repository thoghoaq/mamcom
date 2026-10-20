using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.Core.Spinning;
using static MamCom.Core.Tests.Spinning.SpinnerFixtures;

namespace MamCom.Core.Tests.Spinning;

public class TraySessionTests
{
    private static SpinResult Result(params string[] ids) => new(
        new MealTray(Today, [.. ids.Select((id, i) => new SlotResult((DishSlot)(i % 3), id, i == 0))]),
        [RelaxedRule.SingleFriedDish],
        [DishSlot.Vegetable]);

    private static TraySession Three() => new(new SpinHistory([Result("a"), Result("b"), Result("c")]), 0, 2, new QuickFilters(true, false, true), null);

    // ---- what is on the screen --------------------------------------------------------------------------------------------

    [Fact]
    public void An_empty_session_has_nothing_on_the_screen_and_nowhere_to_go()
    {
        var empty = TraySession.Empty;

        empty.Viewed.ShouldBeNull();
        empty.CanGoPrevious.ShouldBeFalse();
        empty.CanGoNext.ShouldBeFalse();
        empty.IsConfirmed.ShouldBeFalse();
        empty.SpinsSinceConfirm.ShouldBe(0);
        empty.Filters.ShouldBe(QuickFilters.None);
        empty.IsUsable.ShouldBeTrue();
    }

    [Fact]
    public void The_view_index_picks_the_tray_and_decides_which_way_one_can_go()
    {
        var session = Three();

        session.Viewed.ShouldBeSameAs(session.History.Items[0]);
        (session.CanGoPrevious, session.CanGoNext).ShouldBe((true, false));

        var middle = session with { ViewIndex = 1 };
        middle.Viewed.ShouldBeSameAs(session.History.Items[1]);
        (middle.CanGoPrevious, middle.CanGoNext).ShouldBe((true, true));

        var oldest = session with { ViewIndex = 2 };
        oldest.Viewed.ShouldBeSameAs(session.History.Items[2]);
        (oldest.CanGoPrevious, oldest.CanGoNext).ShouldBe((false, true));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void An_index_that_points_outside_the_history_shows_nothing_and_goes_nowhere(int index)
    {
        var session = Three() with { ViewIndex = index };

        session.Viewed.ShouldBeNull();
        session.CanGoPrevious.ShouldBeFalse();
        session.CanGoNext.ShouldBeFalse();
    }

    [Fact]
    public void A_tray_is_confirmed_when_it_has_a_day_of_confirmation()
    {
        (Three() with { ConfirmedOn = Today }).IsConfirmed.ShouldBeTrue();
        Three().IsConfirmed.ShouldBeFalse();
    }

    [Fact]
    public void No_filter_is_on_in_none_and_any_filter_makes_any_true()
    {
        QuickFilters.None.Any.ShouldBeFalse();
        new QuickFilters(true, false, false).Any.ShouldBeTrue();
        new QuickFilters(false, true, false).Any.ShouldBeTrue();
        new QuickFilters(false, false, true).Any.ShouldBeTrue();
    }

    // ---- whether a session that came back can be used ---------------------------------------------------------------------

    [Fact]
    public void A_session_that_makes_sense_is_usable()
    {
        Three().IsUsable.ShouldBeTrue();
        (Three() with { ViewIndex = 2 }).IsUsable.ShouldBeTrue();
    }

    public static TheoryData<TraySession> Unusable() => new()
    {
        Three() with { SpinsSinceConfirm = -1 },
        Three() with { ViewIndex = -1 },
        Three() with { ViewIndex = 3 },
        TraySession.Empty with { ViewIndex = 1 },
        Three() with { History = new SpinHistory([Result("a"), Result("")]) },
        Three() with { History = new SpinHistory([Result("a"), Result("   ")]) },
        Three() with { History = new SpinHistory([new SpinResult(new MealTray(Today, [new SlotResult((DishSlot)9, "a", false)]), [], [])]) },
        Three() with { History = new SpinHistory([null!]) },
        Three() with { History = new SpinHistory([new SpinResult(new MealTray(Today, null!), [], [])]) },
        Three() with { History = new SpinHistory([new SpinResult(new MealTray(Today, [null!]), [], [])]) },
    };

    [Theory]
    [MemberData(nameof(Unusable))]
    public void A_session_that_does_not_make_sense_is_not_usable(TraySession session)
    {
        session.IsUsable.ShouldBeFalse();
    }

    // ---- the JSON on the device -------------------------------------------------------------------------------------------

    [Fact]
    public void A_session_survives_being_written_and_read_back()
    {
        var session = Three() with { ViewIndex = 1, ConfirmedOn = Today.AddDays(-1) };

        var json = JsonSerializer.Serialize(session, CoreJson.Options);
        var back = JsonSerializer.Deserialize<TraySession>(json, CoreJson.Options)!;

        back.ViewIndex.ShouldBe(1);
        back.SpinsSinceConfirm.ShouldBe(2);
        back.ConfirmedOn.ShouldBe(Today.AddDays(-1));
        back.Filters.ShouldBe(new QuickFilters(true, false, true));
        back.History.Items.Count.ShouldBe(3);
        back.History.Items[1].Tray.Date.ShouldBe(Today);
        back.History.Items[1].Tray.Slots.Select(s => (s.Slot, s.DishId, s.IsLocked)).ShouldBe([(DishSlot.Soup, "b", true)]);
        back.History.Items[1].RelaxedRules.ShouldBe([RelaxedRule.SingleFriedDish]);
        back.History.Items[1].UnfilledSlots.ShouldBe([DishSlot.Vegetable]);
        back.IsUsable.ShouldBeTrue();
    }

    [Fact]
    public void The_computed_parts_of_a_session_are_not_written()
    {
        var json = JsonSerializer.Serialize(Three(), CoreJson.Options);

        foreach (var name in new[] { "viewed", "canGoPrevious", "canGoNext", "isConfirmed", "isUsable", "any" })
        {
            json.ShouldNotContain($"\"{name}\"");
        }

        json.ShouldContain("\"viewIndex\"");
        json.ShouldContain("\"spinsSinceConfirm\"");
    }

    [Fact]
    public void A_session_without_a_confirmation_day_is_written_and_read_with_a_null()
    {
        var back = JsonSerializer.Deserialize<TraySession>(JsonSerializer.Serialize(TraySession.Empty, CoreJson.Options), CoreJson.Options)!;

        back.ConfirmedOn.ShouldBeNull();
        back.History.Items.ShouldBeEmpty();
    }

    // ---- the log of what was eaten ----------------------------------------------------------------------------------------

    [Fact]
    public void A_log_says_a_dish_was_eaten_on_a_day_and_the_old_log_is_not_touched()
    {
        var log = EatenLog.Empty;

        var next = log.With(["a", "b"], Today);

        next.LastEatenOn["a"].ShouldBe(Today);
        next.LastEatenOn["b"].ShouldBe(Today);
        log.LastEatenOn.ShouldBeEmpty();
        EatenLog.Empty.LastEatenOn.ShouldBeEmpty();
    }

    [Fact]
    public void A_newer_day_replaces_an_older_one_and_an_older_day_does_not_replace_a_newer_one()
    {
        var log = EatenLog.Empty.With(["a"], Today.AddDays(-5)).With(["b"], Today.AddDays(1));

        var next = log.With(["a", "b"], Today);

        next.LastEatenOn["a"].ShouldBe(Today);
        next.LastEatenOn["b"].ShouldBe(Today.AddDays(1));
    }

    [Fact]
    public void Dishes_that_are_not_named_keep_their_day()
    {
        var next = EatenLog.Empty.With(["a"], Today.AddDays(-3)).With(["b"], Today);

        next.LastEatenOn["a"].ShouldBe(Today.AddDays(-3));
        next.LastEatenOn.Count.ShouldBe(2);
    }

    [Fact]
    public void A_day_in_the_future_is_read_as_today_and_the_log_itself_is_not_changed()
    {
        var log = EatenLog.Empty.With(["a"], Today.AddDays(40)).With(["b"], Today.AddDays(-4)).With(["c"], Today);

        var read = log.Until(Today);

        read["a"].ShouldBe(Today);
        read["b"].ShouldBe(Today.AddDays(-4));
        read["c"].ShouldBe(Today);
        log.LastEatenOn["a"].ShouldBe(Today.AddDays(40));
    }

    [Fact]
    public void A_dish_with_a_day_in_the_future_is_still_kept_off_the_tray_as_if_just_eaten()
    {
        var log = EatenLog.Empty.With(["canh-bi-do"], Today.AddDays(400));
        var spinner = Spinner(new SeededRandomSource(3));

        for (var i = 0; i < 20; i++)
        {
            var result = spinner.Spin(Context(SmallHome(), lastEaten: log.Until(Today)));
            Ids(result).ShouldNotContain("canh-bi-do");
        }
    }

    [Fact]
    public void A_log_survives_being_written_and_read_back()
    {
        var log = EatenLog.Empty.With(["a", "b"], new DateOnly(2026, 10, 20));

        var back = JsonSerializer.Deserialize<EatenLog>(JsonSerializer.Serialize(log, CoreJson.Options), CoreJson.Options)!;

        back.LastEatenOn.OrderBy(p => p.Key, StringComparer.Ordinal).ShouldBe(log.LastEatenOn.OrderBy(p => p.Key, StringComparer.Ordinal));
        JsonSerializer.Serialize(log, CoreJson.Options).ShouldContain("2026-10-20");
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        Should.Throw<ArgumentNullException>(() => EatenLog.Empty.With(null!, Today));
    }
}
