using MamCom.UI;
using MamCom.UI.Formatting;

namespace MamCom.UI.Tests;

public class CookingFormatTests
{
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(9, "00:09")]
    [InlineData(59, "00:59")]
    [InlineData(61, "01:01")]
    [InlineData(1452, "24:12")]
    [InlineData(1800, "30:00")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3900, "1:05:00")]
    [InlineData(7325, "2:02:05")]
    public void A_countdown_is_minutes_and_seconds_and_from_an_hour_hours_too(int seconds, string expected)
    {
        CookingFormat.Countdown(seconds).ShouldBe(expected);
    }

    [Fact]
    public void A_countdown_cannot_be_negative()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CookingFormat.Countdown(-1));
    }

    [Theory]
    [InlineData(0, 9, "Bước 1/9")]
    [InlineData(2, 9, "Bước 3/9")]
    [InlineData(8, 9, "Bước 9/9")]
    [InlineData(0, 1, "Bước 1/1")]
    public void The_place_of_the_cook_counts_from_one(int index, int count, string expected)
    {
        CookingFormat.Position(index, count).ShouldBe(expected);
    }

    [Theory]
    [InlineData(-1, 9)]
    [InlineData(9, 9)]
    [InlineData(0, 0)]
    [InlineData(0, -2)]
    public void A_place_outside_the_steps_is_refused(int index, int count)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CookingFormat.Position(index, count));
    }

    [Fact]
    public void The_rice_is_named_cơm_and_a_dish_by_its_name()
    {
        CookingFormat.DishLabel(string.Empty, isRice: true).ShouldBe("Cơm");
        CookingFormat.DishLabel("Thịt kho", isRice: false).ShouldBe("Thịt kho");
        CookingFormat.DishLabel("Thịt kho", isRice: true).ShouldBe("Cơm");
    }

    [Theory]
    [InlineData(30, "Hẹn giờ 30 phút")]
    [InlineData(60, "Hẹn giờ 1 giờ")]
    [InlineData(95, "Hẹn giờ 1 giờ 35 phút")]
    public void The_button_to_start_a_timer_says_how_long(int minutes, string expected)
    {
        CookingFormat.StartTimer(minutes).Plain().ShouldBe(expected);
    }

    [Fact]
    public void A_number_and_its_unit_are_kept_on_one_line()
    {
        CookingFormat.StartTimer(30).ShouldBe("Hẹn giờ 30 phút");
    }

    [Fact]
    public void What_a_screen_reader_is_told_names_the_place_the_dish_and_the_step()
    {
        CookingFormat.Announce("Bước 3/9", "Thịt kho", "Ướp thịt.").ShouldBe("Bước 3/9, Thịt kho. Ướp thịt.");
        CookingFormat.AnnounceDone(["Cơm"]).ShouldBe("Hết giờ: Cơm.");
        CookingFormat.AnnounceDone(["Cơm", "Thịt kho"]).ShouldBe("Hết giờ: Cơm, Thịt kho.");
        Should.Throw<ArgumentNullException>(() => CookingFormat.AnnounceDone(null!));
    }
}

public class TimerTickerTests
{
    private sealed class StubTimer : ITimer
    {
        public bool Disposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A clock whose timer goes off when the test says so, and which remembers how it was asked.</summary>
    private sealed class ManualTime : TimeProvider
    {
        public TimerCallback? Callback { get; private set; }

        public TimeSpan Due { get; private set; }

        public TimeSpan Period { get; private set; }

        public StubTimer? Timer { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Callback = callback;
            Due = dueTime;
            Period = period;
            Timer = new StubTimer();
            return Timer;
        }

        public void Fire() => Callback!(null);
    }

    [Fact]
    public void It_ticks_after_the_period_and_then_every_period_until_it_is_stopped()
    {
        var time = new ManualTime();
        var ticks = 0;

        var handle = new TimerTicker(time).Every(TimeSpan.FromSeconds(1), () =>
        {
            ticks++;
            return Task.CompletedTask;
        });

        time.Due.ShouldBe(TimeSpan.FromSeconds(1));
        time.Period.ShouldBe(TimeSpan.FromSeconds(1));
        time.Fire();
        time.Fire();
        ticks.ShouldBe(2);

        handle.Dispose();

        time.Timer!.Disposed.ShouldBeTrue();
    }

    [Fact]
    public void A_tick_that_is_not_over_when_the_next_is_due_does_not_make_a_second_one_start()
    {
        var time = new ManualTime();
        var gate = new TaskCompletionSource();
        var started = 0;
        new TimerTicker(time).Every(TimeSpan.FromSeconds(1), () =>
        {
            started++;
            return gate.Task;
        });

        time.Fire();
        time.Fire();
        time.Fire();
        started.ShouldBe(1);

        gate.SetResult();
        time.Fire();

        started.ShouldBe(2);
    }

    [Fact]
    public void A_tick_that_fails_does_not_stop_the_next_one()
    {
        var time = new ManualTime();
        var started = 0;
        new TimerTicker(time).Every(TimeSpan.FromSeconds(1), () =>
        {
            started++;
            return Task.FromException(new InvalidOperationException("boom"));
        });

        time.Fire();
        time.Fire();

        started.ShouldBe(2);
    }

    [Fact]
    public void A_tick_that_does_not_come_back_with_a_task_is_refused_by_the_caller_not_by_the_ticker()
    {
        var time = new ManualTime();
        new TimerTicker(time).Every(TimeSpan.FromSeconds(1), () => Task.CompletedTask);

        Should.NotThrow(time.Fire);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_period_of_no_time_is_refused(int seconds)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new TimerTicker(new ManualTime()).Every(TimeSpan.FromSeconds(seconds), () => Task.CompletedTask));
    }

    [Fact]
    public void Something_to_call_is_required()
    {
        Should.Throw<ArgumentNullException>(() => new TimerTicker(new ManualTime()).Every(TimeSpan.FromSeconds(1), null!));
    }

    [Fact]
    public async Task On_the_real_clock_it_ticks_and_stops()
    {
        var ticks = 0;
        var first = new TaskCompletionSource();
        using var handle = new TimerTicker().Every(TimeSpan.FromMilliseconds(20), () =>
        {
            if (Interlocked.Increment(ref ticks) >= 2)
            {
                first.TrySetResult();
            }

            return Task.CompletedTask;
        });

        await first.Task.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);

        ticks.ShouldBeGreaterThanOrEqualTo(2);
    }
}
