using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Usage;

namespace MamCom.Core.Tests.Usage;

public class UsageReportTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 18, 0, 0, TimeSpan.FromHours(7));

    private static UsageLog Log(params (UsageEventKind Kind, double Minutes)[] events) =>
        events.Aggregate(UsageLog.Empty, (log, e) => log.Append(e.Kind, Start.AddMinutes(e.Minutes)));

    [Fact]
    public void The_numbers_of_the_beta_come_from_the_events()
    {
        var log = Log(
            (UsageEventKind.AppOpened, 0),
            (UsageEventKind.Spun, 0.2),
            (UsageEventKind.Swapped, 0.3),
            (UsageEventKind.Locked, 0.4),
            (UsageEventKind.Confirmed, 1),     // 60 s after opening, 2 spins
            (UsageEventKind.ShoppingOpened, 2),
            (UsageEventKind.CookingOpened, 3), // the same tray: counted once
            (UsageEventKind.FeedbackGiven, 60),
            (UsageEventKind.AppOpened, 60 * 24 * 8),
            (UsageEventKind.Confirmed, 60 * 24 * 8 + 0.5));   // 30 s, no spin, no follow-up

        var summary = UsageReport.Summarize(log, new DateOnly(2026, 10, 9));

        summary.ShouldBe(new UsageSummary(
            Visits: 2, Spins: 1, Swaps: 1, Locks: 1, Confirmed: 2,
            AverageSpinsBeforeConfirm: 1, MedianSecondsToConfirm: 45, ConfirmedThenShoppedOrCooked: 1, FeedbackGiven: 1,
            FirstDay: new DateOnly(2026, 10, 1), LastDay: new DateOnly(2026, 10, 9), ActiveDays: 2,
            CameBackDay7: true, CameBackDay30: null));
    }

    [Fact]
    public void Not_coming_back_is_only_said_once_the_days_have_passed()
    {
        var log = Log((UsageEventKind.AppOpened, 0));

        UsageReport.Summarize(log, new DateOnly(2026, 10, 5)).CameBackDay7.ShouldBeNull();
        UsageReport.Summarize(log, new DateOnly(2026, 10, 8)).CameBackDay7.ShouldBe(false);
        UsageReport.Summarize(UsageLog.Empty, new DateOnly(2026, 10, 8)).ShouldBe(new UsageSummary(0, 0, 0, 0, 0, null, null, 0, 0, null, null, 0, null, null));
    }

    [Fact]
    public void Only_the_last_events_are_kept()
    {
        var log = Enumerable.Range(0, UsageLog.MaxEvents + 3).Aggregate(UsageLog.Empty, (l, i) => l.Append(UsageEventKind.Spun, Start.AddSeconds(i)));

        log.Events.Count.ShouldBe(UsageLog.MaxEvents);
        log.Events[0].At.ShouldBe(Start.AddSeconds(3));
    }

    [Fact]
    public void The_log_goes_through_json_and_a_log_of_another_shape_is_not_used()
    {
        var log = Log((UsageEventKind.AppOpened, 0), (UsageEventKind.Confirmed, 1));
        var json = JsonSerializer.Serialize(log, CoreJsonContext.Default.UsageLog);

        JsonSerializer.Deserialize(json, CoreJsonContext.Default.UsageLog)!.Events.ShouldBe(log.Events);
        json.ShouldContain("\"kind\": \"Confirmed\"");
        UsageLog.Open(new UsageLog([new UsageEvent((UsageEventKind)99, Start)])).ShouldBe(UsageLog.Empty);
    }
}
