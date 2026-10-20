using System.Globalization;
using MamCom.Core.Details;

namespace MamCom.UI.Formatting;

/// <summary>The words of the detail screen of a dish: how much of an ingredient, what a share of energy says about itself (SPEC §8.1-3).</summary>
public static class DishFormat
{
    /// <summary>"cho ≈ 3,8 suất": what the recipe is scaled to.</summary>
    public static string Servings(double servings) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Detail_ServingsFormat, VietnameseNumber.Decimal(servings, 1));

    /// <summary>"2 quả" for a line counted in pieces, else "160 g".</summary>
    public static string Amount(IngredientLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return line is { Units: { } units, UnitDisplay: { } unit }
            ? string.Format(CultureInfo.InvariantCulture, Strings.Detail_UnitsFormat, units, unit)
            : string.Format(CultureInfo.InvariantCulture, Strings.Detail_GramsFormat, Grams(line.GramsEdible));
    }

    /// <summary>"≈ 110 g" under a line counted in pieces, so the cook can weigh it; null for a line that is already in grams.</summary>
    public static string? AmountInGrams(IngredientLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return line.Units is null
            ? null
            : string.Format(CultureInfo.InvariantCulture, Strings.Detail_UnitsGramsFormat, Grams(line.GramsEdible));
    }

    /// <summary>"Làm 10 phút": the minutes of a step that need the cook's hands.</summary>
    public static string StepActive(int minutes) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Detail_StepActiveFormat, TrayFormat.Minutes(minutes));

    /// <summary>"Chờ 30 phút": the minutes of a step that run on their own.</summary>
    public static string StepPassive(int minutes) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Detail_StepPassiveFormat, TrayFormat.Minutes(minutes));

    public static string MacroName(Macro macro) => macro switch
    {
        Macro.Protein => Strings.Nutrition_Protein,
        Macro.Fat => Strings.Nutrition_Fat,
        Macro.Carb => Strings.Nutrition_Carb,
        _ => throw new ArgumentOutOfRangeException(nameof(macro), macro, null),
    };

    /// <summary>The part of a CSS class that gives a nutrient its colour.</summary>
    public static string MacroTone(Macro macro) => macro switch
    {
        Macro.Protein => "protein",
        Macro.Fat => "fat",
        Macro.Carb => "carb",
        _ => throw new ArgumentOutOfRangeException(nameof(macro), macro, null),
    };

    /// <summary>"≈ 34%": the share of the energy, as a whole percent.</summary>
    public static string Percent(double share) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Detail_ShareValueFormat, VietnameseNumber.Whole(share * 100));

    /// <summary>"khuyến nghị 13–20%".</summary>
    public static string Recommended(MacroShare share)
    {
        ArgumentNullException.ThrowIfNull(share);

        return string.Format(
            CultureInfo.InvariantCulture,
            Strings.Detail_ShareRangeFormat,
            VietnameseNumber.Whole(share.RecommendedMin * 100),
            VietnameseNumber.Whole(share.RecommendedMax * 100));
    }

    public static string Status(RangeStatus status) => status switch
    {
        RangeStatus.Below => Strings.Detail_Status_Below,
        RangeStatus.Within => Strings.Detail_Status_Within,
        RangeStatus.Above => Strings.Detail_Status_Above,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>What a screen reader says for the bar of shares, which is a picture: "Đạm ≈ 34%, béo ≈ 66%, bột đường ≈ 0%".</summary>
    public static string ShareBar(IReadOnlyList<MacroShare> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);

        string Of(Macro macro) => Percent(shares.Single(s => s.Macro == macro).Share);

        return string.Format(CultureInfo.InvariantCulture, Strings.Detail_ShareBarFormat, Of(Macro.Protein), Of(Macro.Fat), Of(Macro.Carb));
    }

    /// <summary>"1 suất", "0,6 suất": what a member's servings are.</summary>
    public static string MemberServings(double servings) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Detail_MemberServingsFormat, VietnameseNumber.Decimal(servings, 1));

    /// <summary>Whole grams from ten up; under ten a decimal, so a pinch of spice is not rounded to nothing.</summary>
    private static string Grams(double grams) => grams >= 10 ? VietnameseNumber.Whole(grams) : VietnameseNumber.Decimal(grams, 1);
}
