using MamCom.Core.Models;
using MamCom.Core.Spinning;
using MamCom.UI.Components;
using MamCom.UI.Formatting;
using Microsoft.AspNetCore.Components;

namespace MamCom.UI.Tests;

public class TrayFormatTests
{
    [Theory]
    [InlineData(0, "0 phút")]
    [InlineData(1, "1 phút")]
    [InlineData(45, "45 phút")]
    [InlineData(59, "59 phút")]
    [InlineData(60, "1 giờ")]
    [InlineData(61, "1 giờ 1 phút")]
    [InlineData(125, "2 giờ 5 phút")]
    [InlineData(180, "3 giờ")]
    public void Minutes_up_to_an_hour_are_minutes_and_longer_times_are_hours_and_minutes(int minutes, string expected)
    {
        TrayFormat.Minutes(minutes).Plain().ShouldBe(expected);
    }

    [Fact]
    public void A_number_and_its_unit_are_kept_on_one_line()
    {
        TrayFormat.Minutes(45).ShouldBe("45\u00a0phút");
        TrayFormat.Minutes(120).ShouldBe("2\u00a0giờ");
        TrayFormat.Minutes(65).ShouldBe("1\u00a0giờ 5\u00a0phút");
        TrayFormat.Items(9).ShouldBe("9\u00a0thứ");
        TrayFormat.ShortFilter(45).ShouldBe("Dưới 45\u00a0phút");
    }

    [Fact]
    public void A_negative_time_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => TrayFormat.Minutes(-1));
    }

    [Fact]
    public void Every_slot_has_its_name_and_nothing_else_is_a_slot()
    {
        Enum.GetValues<DishSlot>().Select(TrayFormat.Slot).ShouldBe(["Canh", "Món mặn", "Rau"]);
        Should.Throw<ArgumentOutOfRangeException>(() => TrayFormat.Slot((DishSlot)9));
    }

    [Fact]
    public void Every_rule_the_spinner_can_give_up_is_told_in_words()
    {
        Enum.GetValues<RelaxedRule>().Select(TrayFormat.Relaxed).ShouldBe(
        [
            "Chưa có đủ món hợp mùa và miền nên mâm này chọn rộng hơn.",
            "Mâm này nấu lâu hơn thời gian bạn có.",
            "Mâm này có hơn một món chiên.",
            "Mâm này có hai món cùng loại đạm.",
        ]);
        Should.Throw<ArgumentOutOfRangeException>(() => TrayFormat.Relaxed((RelaxedRule)9));
    }

    [Fact]
    public void A_slot_that_could_not_be_filled_is_told_with_the_name_of_the_slot()
    {
        TrayFormat.Unfilled(DishSlot.Main).ShouldBe("Chưa tìm được món mặn hợp. Thử quay lại hoặc bỏ bớt bộ lọc.");
        TrayFormat.Unfilled(DishSlot.Soup).ShouldStartWith("Chưa tìm được canh hợp.");
        TrayFormat.Unfilled(DishSlot.Vegetable).ShouldStartWith("Chưa tìm được rau hợp.");
    }

    [Fact]
    public void The_place_of_a_tray_among_the_last_ones_counts_from_the_oldest()
    {
        var five = new TraySession(new SpinHistory([.. Enumerable.Range(0, 5).Select(_ => new SpinResult(new MealTray(new DateOnly(2026, 10, 20), []), [], []))]), 0, 0, QuickFilters.None, null);

        TrayFormat.Position(five).ShouldBe("Mâm 5/5");
        TrayFormat.Position(five with { ViewIndex = 1 }).ShouldBe("Mâm 4/5");
        TrayFormat.Position(five with { ViewIndex = 4 }).ShouldBe("Mâm 1/5");
        Should.Throw<ArgumentNullException>(() => TrayFormat.Position(null!));
    }
}

public class TimerSpinPacerTests
{
    /// <summary>A clock whose timers go off at once and which remembers how long they were set for.</summary>
    private sealed class QuickTime : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            _ = Task.Run(() => callback(state));
            return new NoTimer();
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task The_roll_of_the_dice_takes_about_a_second()
    {
        var time = new QuickTime();

        await new TimerSpinPacer(new FakeDeviceFeatures(), time).WaitAsync(Xunit.TestContext.Current.CancellationToken);

        time.Delays.ShouldBe([TimeSpan.FromSeconds(1)]);
        TimerSpinPacer.Duration.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task A_person_who_asked_for_less_movement_does_not_wait()
    {
        var time = new QuickTime();

        await new TimerSpinPacer(new FakeDeviceFeatures { ReducedMotion = true }, time).WaitAsync(Xunit.TestContext.Current.CancellationToken);

        time.Delays.ShouldBeEmpty();
    }

    [Fact]
    public async Task Waiting_can_be_cancelled()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await new TimerSpinPacer(new FakeDeviceFeatures(), TimeProvider.System).WaitAsync(cancel.Token));
    }
}

public class TrayPartsTests : BunitContext
{
    // ---- the summary ------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_summary_says_the_time_the_energy_the_rice_and_what_to_buy()
    {
        var cut = Render<TraySummaryPanel>(p => p.Add(s => s.Summary, new TraySummary(95, 646, 320, 9)));

        var rows = cut.FindAll(".tray-summary__row").Select(r => (r.QuerySelector("dt")!.TextContent.Trim(), r.QuerySelector("dd")!.TextContent.Plain())).ToArray();
        rows.ShouldBe(
        [
            ("Nấu cả mâm", "1 giờ 35 phút"),
            ("Mỗi người, cả cơm", "≈ 650 kcal"),
            ("Gạo nấu cơm", "≈ 320 g"),
            ("Cần mua", "9 thứ"),
        ]);
        cut.Find("dl").GetAttribute("aria-label").ShouldBe("Tóm tắt mâm");
    }

    [Fact]
    public void An_energy_that_is_not_known_is_said_to_be_unknown_in_the_summary_too()
    {
        var cut = Render<TraySummaryPanel>(p => p.Add(s => s.Summary, new TraySummary(40, null, 160, 0)));

        cut.FindAll(".tray-summary__row dd")[1].TextContent.ShouldBe("chưa đủ số liệu");
        cut.FindAll(".tray-summary__row dd")[3].TextContent.Plain().ShouldBe("0 thứ");
    }

    [Fact]
    public void The_icons_of_the_summary_are_decoration()
    {
        Render<TraySummaryPanel>(p => p.Add(s => s.Summary, new TraySummary(40, 500, 160, 4))).FindAll("svg").ShouldAllBe(s => s.GetAttribute("aria-hidden") == "true");
    }

    // ---- the quick filters ------------------------------------------------------------------------------------------------

    private readonly List<QuickFilterKind> _toggled = [];

    private IRenderedComponent<QuickFilterBar> Bar(QuickFilters filters, bool disabled = false) => Render<QuickFilterBar>(p => p
        .Add(b => b.Filters, filters)
        .Add(b => b.MaxMinutes, 45)
        .Add(b => b.Disabled, disabled)
        .Add(b => b.OnToggle, EventCallback.Factory.Create<QuickFilterKind>(this, _toggled.Add)));

    [Fact]
    public void The_bar_offers_the_three_filters_as_switches_the_first_with_the_minutes_of_the_config()
    {
        var chips = Bar(QuickFilters.None).FindAll(".chip");

        chips.Select(c => c.TextContent.Plain()).ShouldBe(["Dưới 45 phút", "Không thịt đỏ", "Ít dầu mỡ"]);
        chips.ShouldAllBe(c => c.GetAttribute("aria-pressed") == "false" && c.GetAttribute("type") == "button");
    }

    [Fact]
    public void A_filter_that_is_on_is_pressed()
    {
        Bar(new QuickFilters(true, false, true)).FindAll(".chip").Select(c => c.GetAttribute("aria-pressed")).ShouldBe(["true", "false", "true"]);
        Bar(new QuickFilters(false, true, false)).FindAll(".chip").Select(c => c.GetAttribute("aria-pressed")).ShouldBe(["false", "true", "false"]);
    }

    [Fact]
    public void Each_chip_asks_for_its_own_filter()
    {
        var cut = Bar(QuickFilters.None);

        cut.FindAll(".chip")[0].Click();
        cut.FindAll(".chip")[1].Click();
        cut.FindAll(".chip")[2].Click();

        _toggled.ShouldBe([QuickFilterKind.Short, QuickFilterKind.NoRedMeat, QuickFilterKind.LowFat]);
    }

    [Fact]
    public void While_the_screen_is_busy_the_chips_are_off()
    {
        Bar(QuickFilters.None, disabled: true).FindAll(".chip").ShouldAllBe(c => c.HasAttribute("disabled"));
        Bar(QuickFilters.None).FindAll(".chip").ShouldAllBe(c => !c.HasAttribute("disabled"));
    }

    [Fact]
    public void The_bar_is_named_by_its_question()
    {
        var cut = Bar(QuickFilters.None);

        cut.Find("#tray-filters-title").TextContent.ShouldBe("Chưa ưng? Lọc nhanh:");
        cut.Find("section").GetAttribute("aria-labelledby").ShouldBe("tray-filters-title");
        cut.Find("[role=group]").GetAttribute("aria-labelledby").ShouldBe("tray-filters-title");
    }

    // ---- the notices ------------------------------------------------------------------------------------------------------

    private static SpinResult Result(RelaxedRule[] rules, DishSlot[] unfilled) => new(new MealTray(new DateOnly(2026, 10, 20), []), rules, unfilled);

    [Fact]
    public void A_tray_that_gave_up_nothing_has_no_notice()
    {
        Render<TrayNotices>(p => p.Add(n => n.Result, Result([], []))).Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Each_rule_that_was_given_up_and_each_slot_that_could_not_be_filled_is_told_in_order()
    {
        var cut = Render<TrayNotices>(p => p.Add(n => n.Result, Result([RelaxedRule.AvailableMinutes, RelaxedRule.SingleFriedDish], [DishSlot.Vegetable])));

        cut.FindAll("li").Select(li => li.TextContent).ShouldBe(
        [
            "Mâm này nấu lâu hơn thời gian bạn có.",
            "Mâm này có hơn một món chiên.",
            "Chưa tìm được rau hợp. Thử quay lại hoặc bỏ bớt bộ lọc.",
        ]);
        cut.Find(".notice").ClassList.ShouldContain("notice--warning");
        cut.Find(".notice").GetAttribute("role").ShouldBe("status");
    }

    [Fact]
    public void A_slot_that_could_not_be_filled_is_told_even_when_no_rule_was_given_up()
    {
        Render<TrayNotices>(p => p.Add(n => n.Result, Result([], [DishSlot.Soup]))).FindAll("li").Count.ShouldBe(1);
    }
}
