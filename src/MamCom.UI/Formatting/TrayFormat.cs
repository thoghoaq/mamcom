using System.Globalization;
using MamCom.Core.Models;
using MamCom.Core.Spinning;

namespace MamCom.UI.Formatting;

/// <summary>The words of the "Mâm hôm nay" screen: how long a dish takes, the name of a slot, what a given-up rule means (SPEC §8.1-2, §5.3).</summary>
public static class TrayFormat
{
    /// <summary>"45 phút", "1 giờ", "1 giờ 10 phút": minutes up to an hour as they are, longer as hours and minutes.</summary>
    public static string Minutes(int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);

        var hours = minutes / 60;
        var rest = minutes % 60;
        return hours == 0
            ? string.Format(CultureInfo.InvariantCulture, Strings.Time_Minutes, rest)
            : rest == 0
                ? string.Format(CultureInfo.InvariantCulture, Strings.Time_Hours, hours)
                : string.Format(CultureInfo.InvariantCulture, Strings.Time_HoursMinutes, hours, rest);
    }

    public static string Slot(DishSlot slot) => slot switch
    {
        DishSlot.Soup => Strings.Tray_Slot_Soup,
        DishSlot.Main => Strings.Tray_Slot_Main,
        DishSlot.Vegetable => Strings.Tray_Slot_Vegetable,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    /// <summary>What the person is told when the spinner gave up a rule to fill the tray (SPEC §5.3), in the words of the rule.</summary>
    public static string Relaxed(RelaxedRule rule) => rule switch
    {
        RelaxedRule.SeasonAndRegionWeights => Strings.Tray_Relaxed_SeasonAndRegionWeights,
        RelaxedRule.AvailableMinutes => Strings.Tray_Relaxed_AvailableMinutes,
        RelaxedRule.SingleFriedDish => Strings.Tray_Relaxed_SingleFriedDish,
        RelaxedRule.DistinctProteinSources => Strings.Tray_Relaxed_DistinctProteinSources,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };

    /// <summary>"Chưa tìm được món mặn hợp." for a slot no dish could fill.</summary>
    public static string Unfilled(DishSlot slot) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Tray_UnfilledFormat, Slot(slot).ToLowerInvariant());

    /// <summary>The short filter's chip: "Dưới 30 phút".</summary>
    public static string ShortFilter(int maxMinutes) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Tray_FilterShortFormat, maxMinutes);

    /// <summary>"Mâm 2/5": the place of the tray on the screen among the last ones, the newest last.</summary>
    public static string Position(TraySession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return string.Format(CultureInfo.InvariantCulture, Strings.Tray_PositionFormat, session.History.Items.Count - session.ViewIndex, session.History.Items.Count);
    }

    /// <summary>"Cần mua 9 thứ": a count and its word, kept on one line.</summary>
    public static string Items(int count) => string.Format(CultureInfo.InvariantCulture, Strings.Tray_ItemsFormat, count);
}
