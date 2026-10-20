using System.Globalization;
using System.Text;

namespace MamCom.UI.Formatting;

/// <summary>
/// Numbers as Vietnamese writes them: a comma before the decimals and a dot between thousands ("1,5", "1.250"). Written here
/// rather than taken from the culture of the device, because the platform may not carry culture data (WebAssembly) and the app
/// must read the same everywhere.
/// </summary>
public static class VietnameseNumber
{
    /// <summary>A whole number with a dot between thousands: 1234567 becomes "1.234.567". The value is rounded to a whole number first.</summary>
    public static string Whole(double value)
    {
        var digits = Math.Abs(Math.Round(value, MidpointRounding.AwayFromZero)).ToString("F0", CultureInfo.InvariantCulture);
        var grouped = new StringBuilder(digits.Length + digits.Length / 3 + 1);
        if (value < 0 && digits != "0")
        {
            grouped.Append('-');
        }

        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                grouped.Append('.');
            }

            grouped.Append(digits[i]);
        }

        return grouped.ToString();
    }

    /// <summary>
    /// The number with up to <paramref name="places"/> decimals, without trailing zeros: 1.5 is "1,5", 2.0 is "2", 3.79 with one
    /// place is "3,8". Halves round away from zero.
    /// </summary>
    public static string Decimal(double value, int places = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(places);

        var rounded = Math.Round(value, places, MidpointRounding.AwayFromZero);
        var text = rounded.ToString("0." + new string('#', places), CultureInfo.InvariantCulture);
        return text.Replace('.', ',');
    }
}
