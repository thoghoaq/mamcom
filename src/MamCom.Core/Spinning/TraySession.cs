using System.Text.Json.Serialization;
using MamCom.Core.Models;

namespace MamCom.Core.Spinning;

/// <summary>The quick filters of SPEC §8.1-2 the person has switched on. What each one means is in <see cref="QuickFilterConfig"/>.</summary>
public sealed record QuickFilters(bool Short, bool NoRedMeat, bool LowFat)
{
    public static QuickFilters None { get; } = new(false, false, false);

    [JsonIgnore]
    public bool Any => Short || NoRedMeat || LowFat;
}

/// <summary>
/// The "Mâm hôm nay" screen as it stands (SPEC §8.1-2): the last spins, newest first, which of them is on the screen, how many spins
/// went by without confirming, the quick filters, and whether the tray on the screen is confirmed. Kept on the device so the same
/// tray is there when the app is opened again.
/// </summary>
/// <param name="History">The spins, newest first; at most <see cref="SpinnerConfig.HistorySize"/>.</param>
/// <param name="ViewIndex">Which entry of <paramref name="History"/> is on the screen; 0 is the newest.</param>
/// <param name="SpinsSinceConfirm">Spins since the last tray was confirmed (or since the session began).</param>
/// <param name="ConfirmedOn">The day the tray on the screen was confirmed, or null while it is still being chosen.</param>
public sealed record TraySession(SpinHistory History, int ViewIndex, int SpinsSinceConfirm, QuickFilters Filters, DateOnly? ConfirmedOn)
{
    public static TraySession Empty { get; } = new(SpinHistory.Empty, 0, 0, QuickFilters.None, null);

    /// <summary>The spin on the screen; null when nothing was spun yet or the index does not point at a spin.</summary>
    [JsonIgnore]
    public SpinResult? Viewed => ViewIndex >= 0 && ViewIndex < History.Items.Count ? History.Items[ViewIndex] : null;

    /// <summary>There is an older tray to go back to.</summary>
    [JsonIgnore]
    public bool CanGoPrevious => ViewIndex >= 0 && ViewIndex + 1 < History.Items.Count;

    /// <summary>The screen shows an older tray and a newer one can be shown again.</summary>
    [JsonIgnore]
    public bool CanGoNext => ViewIndex > 0 && ViewIndex < History.Items.Count;

    [JsonIgnore]
    public bool IsConfirmed => ConfirmedOn is not null;

    /// <summary>
    /// Whether a session that came back from the device can be used: counts that make sense and, when there is a history, an index
    /// that points into it. A session that fails this is the same as none.
    /// </summary>
    [JsonIgnore]
    public bool IsUsable =>
        SpinsSinceConfirm >= 0
        && (History.Items.Count == 0 ? ViewIndex == 0 : ViewIndex >= 0 && ViewIndex < History.Items.Count)
        && History.Items.All(r => r is { Tray.Slots: not null } && r.Tray.Slots.All(s => s is not null && !string.IsNullOrWhiteSpace(s.DishId) && Enum.IsDefined(s.Slot)));
}

/// <summary>
/// The last day each dish was eaten, for the recency rules of the spinner (SPEC §5.2, §5.4). A dish is recorded when its tray is
/// confirmed (ADR-022).
/// </summary>
public sealed record EatenLog(IReadOnlyDictionary<string, DateOnly> LastEatenOn)
{
    public static EatenLog Empty { get; } = new(new Dictionary<string, DateOnly>());

    /// <summary>A copy that says these dishes were eaten on <paramref name="date"/> (or later, when a later day was already there).</summary>
    public EatenLog With(IEnumerable<string> dishIds, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(dishIds);

        var next = new Dictionary<string, DateOnly>(LastEatenOn, StringComparer.Ordinal);
        foreach (var id in dishIds)
        {
            next[id] = next.TryGetValue(id, out var known) && known > date ? known : date;
        }

        return new EatenLog(next);
    }

    /// <summary>
    /// The days as the spinner should read them on <paramref name="today"/>: a day after today (a clock that was wrong once) is read
    /// as today, so a dish is not shut out for weeks because of it.
    /// </summary>
    public IReadOnlyDictionary<string, DateOnly> Until(DateOnly today) =>
        LastEatenOn.ToDictionary(p => p.Key, p => p.Value > today ? today : p.Value, StringComparer.Ordinal);
}
