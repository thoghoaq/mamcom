using System.Globalization;

namespace MamCom.UI.Formatting;

/// <summary>The words of the cooking mode: where the cook is, what a timer says, what a timer button offers (SPEC §8.1-5).</summary>
public static class CookingFormat
{
    /// <summary>"24:12" for a timer with 24 minutes and 12 seconds left; "1:05:00" from an hour.</summary>
    public static string Countdown(int seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);

        var hours = seconds / 3600;
        var minutes = seconds / 60 % 60;
        var rest = seconds % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{rest:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes:00}:{rest:00}");
    }

    /// <summary>"Bước 3/9": <paramref name="index"/> counts from 0.</summary>
    public static string Position(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);

        return string.Format(CultureInfo.InvariantCulture, Strings.Cook_StepFormat, index + 1, count);
    }

    /// <summary>The dish a step belongs to: its name, and "Cơm" for the rice, which has none.</summary>
    public static string DishLabel(string dishName, bool isRice) => isRice ? Strings.Cook_Rice : dishName;

    /// <summary>"Hẹn giờ 30 phút".</summary>
    public static string StartTimer(int minutes) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Cook_StartTimerFormat, TrayFormat.Minutes(minutes));

    /// <summary>What a screen reader is told when the cook moves to a step: "Bước 3/9, Thịt kho. Ướp thịt với hành tím."</summary>
    public static string Announce(string position, string dishLabel, string text) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Cook_AnnounceFormat, position, dishLabel, text);

    /// <summary>"Hết giờ: Cơm." for one timer, and the same for several: "Hết giờ: Cơm, Thịt kho."</summary>
    public static string AnnounceDone(IEnumerable<string> dishLabels)
    {
        ArgumentNullException.ThrowIfNull(dishLabels);

        return string.Format(CultureInfo.InvariantCulture, Strings.Cook_AnnounceDoneFormat, string.Join(", ", dishLabels));
    }
}
