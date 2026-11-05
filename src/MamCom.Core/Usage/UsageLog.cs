using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MamCom.Core.Json;

namespace MamCom.Core.Usage;

/// <summary>What a person did that the beta measures (SPEC §11). Nothing about who they are or which dishes: only what and when.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<UsageEventKind>))]
public enum UsageEventKind
{
    /// <summary>The app was opened: once for each time the page is loaded.</summary>
    AppOpened,

    /// <summary>"Quay lại" was tapped: a new tray. The tray the app spins by itself when it opens is not counted.</summary>
    Spun,

    /// <summary>One dish of the tray was swapped.</summary>
    Swapped,

    /// <summary>A dish was locked (not unlocked).</summary>
    Locked,

    /// <summary>"Chốt mâm".</summary>
    Confirmed,

    ShoppingOpened,

    CookingOpened,

    /// <summary>A dish was rated after the meal.</summary>
    FeedbackGiven,
}

public sealed record UsageEvent(UsageEventKind Kind, DateTimeOffset At);

/// <summary>The events kept on this device, oldest first. Only the last <see cref="MaxEvents"/> are kept, so it never grows without end.</summary>
public sealed record UsageLog(IReadOnlyList<UsageEvent> Events)
{
    public const int MaxEvents = 5000;

    public static UsageLog Empty { get; } = new([]);

    /// <summary>False for a log edited by hand or of another shape.</summary>
    [JsonIgnore]
    public bool IsUsable => Events is not null && Events.All(e => e is not null && Enum.IsDefined(e.Kind));

    public static UsageLog Open(UsageLog? saved) => saved is { IsUsable: true } ? saved : Empty;

    public UsageLog Append(UsageEventKind kind, DateTimeOffset at)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        var events = Events.Count >= MaxEvents ? Events.Skip(Events.Count - MaxEvents + 1) : Events;
        return new UsageLog([.. events, new UsageEvent(kind, at)]);
    }
}

/// <summary>
/// The numbers of SPEC §11 worked out from a log.
/// </summary>
/// <param name="Visits">Times the app was opened.</param>
/// <param name="AverageSpinsBeforeConfirm">Spins and swaps between opening (or the last confirmed tray) and a confirmed tray, on average; null with no confirmed tray.</param>
/// <param name="MedianSecondsToConfirm">From opening the app to confirming, for trays confirmed in the visit they were opened in; null when there is none.</param>
/// <param name="ConfirmedThenShoppedOrCooked">Confirmed trays after which the list or the cooking mode was opened before the next confirmed tray.</param>
/// <param name="CameBackDay7">Used again 7 days or more after the first day; null while 7 days have not passed.</param>
/// <param name="CameBackDay30">The same for 30 days.</param>
public sealed record UsageSummary(
    int Visits,
    int Spins,
    int Swaps,
    int Locks,
    int Confirmed,
    double? AverageSpinsBeforeConfirm,
    int? MedianSecondsToConfirm,
    int ConfirmedThenShoppedOrCooked,
    int FeedbackGiven,
    DateOnly? FirstDay,
    DateOnly? LastDay,
    int ActiveDays,
    bool? CameBackDay7,
    bool? CameBackDay30);

/// <summary>What "Xuất phản hồi" hands over: the summary and every event it was worked out from.</summary>
public sealed record UsageExport(DateTimeOffset ExportedAt, UsageSummary Summary, IReadOnlyList<UsageEvent> Events);

public static class UsageReport
{
    public static UsageSummary Summarize(UsageLog log, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(log);

        var events = log.Events.OrderBy(e => e.At).ToArray();
        var spinsBeforeConfirm = new List<int>();
        var secondsToConfirm = new List<double>();
        var followedUp = 0;
        DateTimeOffset? openedAt = null;
        var spinsSince = 0;
        var awaitingFollowUp = false;

        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case UsageEventKind.AppOpened:
                    openedAt = e.At;
                    spinsSince = 0;
                    break;
                case UsageEventKind.Spun or UsageEventKind.Swapped:
                    spinsSince++;
                    break;
                case UsageEventKind.Confirmed:
                    spinsBeforeConfirm.Add(spinsSince);
                    if (openedAt is { } opened)
                    {
                        secondsToConfirm.Add((e.At - opened).TotalSeconds);
                        openedAt = null;   // a second tray confirmed in the same visit did not start at the opening
                    }

                    spinsSince = 0;
                    awaitingFollowUp = true;
                    break;
                case UsageEventKind.ShoppingOpened or UsageEventKind.CookingOpened when awaitingFollowUp:
                    followedUp++;
                    awaitingFollowUp = false;
                    break;
            }
        }

        var days = events.Select(e => DateOnly.FromDateTime(e.At.DateTime)).Distinct().Order().ToArray();
        DateOnly? first = days.Length > 0 ? days[0] : null;

        return new UsageSummary(
            Visits: Count(events, UsageEventKind.AppOpened),
            Spins: Count(events, UsageEventKind.Spun),
            Swaps: Count(events, UsageEventKind.Swapped),
            Locks: Count(events, UsageEventKind.Locked),
            Confirmed: spinsBeforeConfirm.Count,
            AverageSpinsBeforeConfirm: spinsBeforeConfirm.Count > 0 ? Math.Round(spinsBeforeConfirm.Average(), 1) : null,
            MedianSecondsToConfirm: secondsToConfirm.Count > 0 ? (int)Math.Round(Median(secondsToConfirm)) : null,
            ConfirmedThenShoppedOrCooked: followedUp,
            FeedbackGiven: Count(events, UsageEventKind.FeedbackGiven),
            FirstDay: first,
            LastDay: days.Length > 0 ? days[^1] : null,
            ActiveDays: days.Length,
            CameBackDay7: CameBack(first, days, today, 7),
            CameBackDay30: CameBack(first, days, today, 30));
    }

    public static UsageExport Export(UsageLog log, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);

        return new UsageExport(clock.Now, Summarize(log, clock.Today), log.Events);
    }

    /// <summary>The export as the JSON a person sends back: indented, Vietnamese left readable.</summary>
    public static string ToJson(UsageExport export) =>
        JsonSerializer.Serialize(export, (JsonTypeInfo<UsageExport>)CoreJson.Options.GetTypeInfo(typeof(UsageExport)));

    private static int Count(UsageEvent[] events, UsageEventKind kind) => events.Count(e => e.Kind == kind);

    private static bool? CameBack(DateOnly? first, DateOnly[] days, DateOnly today, int afterDays)
    {
        if (first is not { } start)
        {
            return null;
        }

        var due = start.AddDays(afterDays);
        return days.Any(d => d >= due) ? true : today < due ? null : false;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
