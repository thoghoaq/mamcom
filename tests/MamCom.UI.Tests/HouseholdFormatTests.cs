using MamCom.Core.Households;
using MamCom.Core.Models;
using MamCom.UI.Formatting;

namespace MamCom.UI.Tests;

public class VietnameseNumberTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(7, "7")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.000")]
    [InlineData(12345, "12.345")]
    [InlineData(1234567, "1.234.567")]
    [InlineData(1499.4, "1.499")]
    [InlineData(1499.5, "1.500")]
    [InlineData(0.5, "1")]
    [InlineData(2.5, "3")]
    [InlineData(-2.5, "-3")]
    [InlineData(-1234, "-1.234")]
    [InlineData(-0.2, "0")]
    public void A_whole_number_has_a_dot_between_thousands(double value, string expected)
    {
        VietnameseNumber.Whole(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(1.5, 1, "1,5")]
    [InlineData(2, 1, "2")]
    [InlineData(0.5, 1, "0,5")]
    [InlineData(3.79, 1, "3,8")]
    [InlineData(3.74, 1, "3,7")]
    [InlineData(3.75, 2, "3,75")]
    [InlineData(0.25, 2, "0,25")]
    [InlineData(0.25, 1, "0,3")]
    [InlineData(2.25, 0, "2")]
    [InlineData(2.5, 0, "3")]
    [InlineData(10, 2, "10")]
    [InlineData(-1.5, 1, "-1,5")]
    public void A_decimal_has_a_comma_and_no_trailing_zeros(double value, int places, string expected)
    {
        VietnameseNumber.Decimal(value, places).ShouldBe(expected);
    }

    [Fact]
    public void One_decimal_place_is_the_default()
    {
        VietnameseNumber.Decimal(1.26).ShouldBe("1,3");
    }

    [Fact]
    public void A_number_of_decimals_below_zero_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => VietnameseNumber.Decimal(1.5, -1));
    }

    [Fact]
    public void The_result_does_not_depend_on_the_culture_of_the_machine()
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "vi-VN", "en-US", "de-DE", "fr-FR" })
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);
                VietnameseNumber.Decimal(1234.5).ShouldBe("1234,5", culture);
                VietnameseNumber.Whole(1234567).ShouldBe("1.234.567", culture);
            }
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = before;
        }
    }
}

public class HouseholdFormatTests
{
    [Theory]
    [InlineData(2, "2 chén")]
    [InlineData(1.5, "1,5 chén")]
    [InlineData(0.5, "0,5 chén")]
    public void Bowls_are_a_number_and_the_word_for_a_bowl(double bowls, string expected)
    {
        HouseholdFormat.Bowls(bowls).Plain().ShouldBe(expected);
        HouseholdFormat.Bowls(bowls).ShouldContain(" chén");   // the number and the word are never split over two lines
    }

    [Theory]
    [InlineData(3.79, "3,8")]
    [InlineData(2, "2")]
    [InlineData(1.15, "1,2")]
    public void Servings_have_one_decimal(double servings, string expected)
    {
        HouseholdFormat.Servings(servings).ShouldBe(expected);
    }

    [Fact]
    public void Every_role_has_its_own_name()
    {
        var names = Enum.GetValues<MemberRole>().Select(HouseholdFormat.Role).ToArray();

        names.ShouldBe(["Bố", "Mẹ", "Ông", "Bà", "Con trai", "Con gái", "Bé"]);
        names.Distinct().Count().ShouldBe(names.Length);
    }

    [Fact]
    public void Every_age_group_says_its_years()
    {
        Enum.GetValues<AgeGroup>().Select(HouseholdFormat.Age).ShouldBe(["2–5 tuổi", "6–11 tuổi", "12–17 tuổi", "18–59 tuổi", "Từ 60 tuổi"]);
    }

    [Fact]
    public void Every_region_has_its_name()
    {
        Enum.GetValues<Region>().Select(HouseholdFormat.RegionName).ShouldBe(["Bắc", "Trung", "Nam"]);
    }

    [Fact]
    public void Something_that_is_not_a_role_an_age_group_or_a_region_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => HouseholdFormat.Role((MemberRole)99));
        Should.Throw<ArgumentOutOfRangeException>(() => HouseholdFormat.Age((AgeGroup)99));
        Should.Throw<ArgumentOutOfRangeException>(() => HouseholdFormat.RegionName((Region)99));
    }

    [Fact]
    public void Every_bowl_size_has_its_name_and_something_that_is_not_one_is_refused()
    {
        Enum.GetValues<BowlSize>().Select(HouseholdFormat.BowlSizeName).ShouldBe(["Nhỏ", "Vừa", "To"]);
        Should.Throw<ArgumentOutOfRangeException>(() => HouseholdFormat.BowlSizeName((BowlSize)99));
    }
}
