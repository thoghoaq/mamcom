using System.Globalization;
using MamCom.UI.Formatting;

namespace MamCom.UI.Tests;

public class NutritionFormatTests
{
    [Theory]
    [InlineData(0, "≈ 0 kcal")]
    [InlineData(4, "≈ 0 kcal")]
    [InlineData(5, "≈ 10 kcal")]
    [InlineData(444, "≈ 440 kcal")]
    [InlineData(445, "≈ 450 kcal")]
    [InlineData(450, "≈ 450 kcal")]
    [InlineData(999, "≈ 1.000 kcal")]
    [InlineData(1234.5, "≈ 1.230 kcal")]
    [InlineData(1234567, "≈ 1.234.570 kcal")]
    public void Energy_is_an_estimate_rounded_to_ten_with_a_dot_between_thousands(double kcal, string expected)
    {
        NutritionFormat.Kcal(kcal).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, "≈ 0 g")]
    [InlineData(0.4, "≈ 0 g")]
    [InlineData(0.5, "≈ 1 g")]
    [InlineData(17.6, "≈ 18 g")]
    [InlineData(1500, "≈ 1.500 g")]
    public void Grams_are_rounded_to_one(double grams, string expected)
    {
        NutritionFormat.Grams(grams).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, "≈ 0 mg")]
    [InlineData(634, "≈ 630 mg")]
    [InlineData(635, "≈ 640 mg")]
    [InlineData(1285, "≈ 1.290 mg")]
    public void Sodium_is_rounded_to_ten(double milligrams, string expected)
    {
        NutritionFormat.Milligrams(milligrams).ShouldBe(expected);
    }

    [Fact]
    public void An_unknown_value_is_said_to_be_unknown_and_never_shown_as_zero()
    {
        NutritionFormat.Kcal(null).ShouldBe("chưa đủ số liệu");
        NutritionFormat.Grams(null).ShouldBe("chưa đủ số liệu");
        NutritionFormat.Milligrams(null).ShouldBe("chưa đủ số liệu");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void A_number_that_cannot_be_an_amount_of_food_is_unknown_too(double value)
    {
        NutritionFormat.Kcal(value).ShouldBe("chưa đủ số liệu");
        NutritionFormat.Grams(value).ShouldBe("chưa đủ số liệu");
        NutritionFormat.Milligrams(value).ShouldBe("chưa đủ số liệu");
    }

    [Fact]
    public void The_text_does_not_depend_on_the_culture_of_the_machine()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            NutritionFormat.Kcal(1234.5).ShouldBe("≈ 1.230 kcal");

            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            NutritionFormat.Kcal(1234.5).ShouldBe("≈ 1.230 kcal");
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
