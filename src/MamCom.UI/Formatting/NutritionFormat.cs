using System.Globalization;

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
        return string.Format(CultureInfo.InvariantCulture, Strings.Nutrition_EstimateFormat, VietnameseNumber.Whole(rounded), unit);
    }
}
