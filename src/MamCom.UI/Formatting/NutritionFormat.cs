using System.Globalization;
using System.Text;

namespace MamCom.UI.Formatting;

/// <summary>
/// How nutrition numbers are shown (CLAUDE.md): always as an estimate, "≈", with energy and sodium rounded to 10 and
/// grams to 1. An unknown value is said to be unknown; it is never shown as 0.
/// </summary>
public static class NutritionFormat
{
    public static string Kcal(double? kcal) => Estimate(kcal, 10, "kcal");

    public static string Grams(double? grams) => Estimate(grams, 1, "g");

    public static string Milligrams(double? milligrams) => Estimate(milligrams, 10, "mg");

    private static string Estimate(double? value, int step, string unit)
    {
        if (value is not { } amount || !double.IsFinite(amount) || amount < 0)
        {
            return Strings.Nutrition_Unknown;
        }

        var rounded = Math.Round(amount / step, MidpointRounding.AwayFromZero) * step;
        return string.Format(CultureInfo.InvariantCulture, Strings.Nutrition_EstimateFormat, Group(rounded), unit);
    }

    /// <summary>Whole number with a dot between thousands, as Vietnamese writes it, without depending on the culture data of the platform.</summary>
    private static string Group(double whole)
    {
        var digits = whole.ToString("F0", CultureInfo.InvariantCulture);
        var grouped = new StringBuilder(digits.Length + digits.Length / 3);
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
}
