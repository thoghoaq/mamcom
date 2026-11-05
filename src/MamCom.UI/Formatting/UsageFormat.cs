using System.Globalization;
using MamCom.Core.Usage;

namespace MamCom.UI.Formatting;

/// <summary>The summary of "Xuất phản hồi" as text a person can paste into Zalo (SPEC §11).</summary>
public static class UsageFormat
{
    public static string Text(UsageSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var lines = new List<string>
        {
            Strings.Usage_TextTitle,
            Line(Strings.Usage_DaysFormat, summary.ActiveDays, Date(summary.FirstDay), Date(summary.LastDay)),
            Line(Strings.Usage_VisitsFormat, summary.Visits),
            Line(Strings.Usage_TraysFormat, summary.Confirmed, summary.Spins, summary.Swaps, summary.Locks),
            Line(Strings.Usage_AverageSpinsFormat, summary.AverageSpinsBeforeConfirm is { } spins ? VietnameseNumber.Decimal(spins, 1) : Strings.Usage_None),
            Line(Strings.Usage_TimeToConfirmFormat, summary.MedianSecondsToConfirm is { } seconds ? VietnameseNumber.Whole(seconds) : Strings.Usage_None),
            Line(Strings.Usage_FollowUpFormat, summary.ConfirmedThenShoppedOrCooked, summary.Confirmed),
            Line(Strings.Usage_FeedbackFormat, summary.FeedbackGiven),
            Line(Strings.Usage_CameBackFormat, CameBack(summary.CameBackDay7), CameBack(summary.CameBackDay30)),
        };

        return string.Join('\n', lines);
    }

    private static string Line(string format, params object[] values) => string.Format(CultureInfo.InvariantCulture, format, values);

    private static string Date(DateOnly? day) => day?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? Strings.Usage_None;

    private static string CameBack(bool? cameBack) => cameBack switch
    {
        true => Strings.Usage_Yes,
        false => Strings.Usage_No,
        null => Strings.Usage_NotYet,
    };
}
