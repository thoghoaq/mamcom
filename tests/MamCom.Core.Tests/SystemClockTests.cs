namespace MamCom.Core.Tests;

public class SystemClockTests
{
    [Fact]
    public void Today_is_the_local_date_not_the_UTC_date()
    {
        // 20:00 UTC on 18 Oct is already 03:00 on 19 Oct in UTC+7.
        var utc = new DateTimeOffset(2026, 10, 18, 20, 0, 0, TimeSpan.Zero);
        var clock = new SystemClock(new FixedTimeProvider(utc, UtcPlus7));

        clock.Today.ShouldBe(new DateOnly(2026, 10, 19));
        clock.Now.Offset.ShouldBe(TimeSpan.FromHours(7));
        clock.Now.ShouldBe(utc);
    }

    [Fact]
    public void Default_clock_reports_a_date_consistent_with_now()
    {
        var clock = new SystemClock();

        var before = DateOnly.FromDateTime(DateTime.Now.AddMinutes(-1));
        var after = DateOnly.FromDateTime(DateTime.Now.AddMinutes(1));

        clock.Today.ShouldBeInRange(before, after);
    }

    private static readonly TimeZoneInfo UtcPlus7 =
        TimeZoneInfo.CreateCustomTimeZone("test-utc+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");

    private sealed class FixedTimeProvider(DateTimeOffset utcNow, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
