using System.Globalization;
using MamCom.Core.Households;
using MamCom.Core.Models;

namespace MamCom.UI.Formatting;

/// <summary>The words of the household screens: the names of roles, age groups and regions, and the numbers around them (SPEC §4.1, §8.1-1).</summary>
public static class HouseholdFormat
{
    /// <summary>"1,5 chén".</summary>
    public static string Bowls(double bowls) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Household_BowlsFormat, VietnameseNumber.Decimal(bowls));

    /// <summary>"2 bếp", with the number and the word on one line.</summary>
    public static string Stoves(int stoves) => string.Format(CultureInfo.InvariantCulture, Strings.Household_StovesFormat, stoves);

    /// <summary>The servings with one decimal, "3,8"; the screen writes the "≈" and "suất" around it.</summary>
    public static string Servings(double servings) => VietnameseNumber.Decimal(servings, 1);

    public static string Role(MemberRole role) => role switch
    {
        MemberRole.Father => Strings.Role_Father,
        MemberRole.Mother => Strings.Role_Mother,
        MemberRole.Grandfather => Strings.Role_Grandfather,
        MemberRole.Grandmother => Strings.Role_Grandmother,
        MemberRole.Son => Strings.Role_Son,
        MemberRole.Daughter => Strings.Role_Daughter,
        MemberRole.Baby => Strings.Role_Baby,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static string Age(AgeGroup ageGroup) => ageGroup switch
    {
        AgeGroup.Toddler => Strings.Age_Toddler,
        AgeGroup.Child => Strings.Age_Child,
        AgeGroup.Teen => Strings.Age_Teen,
        AgeGroup.Adult => Strings.Age_Adult,
        AgeGroup.Senior => Strings.Age_Senior,
        _ => throw new ArgumentOutOfRangeException(nameof(ageGroup), ageGroup, null),
    };

    public static string BowlSizeName(BowlSize bowlSize) => bowlSize switch
    {
        BowlSize.Small => Strings.Settings_BowlSmall,
        BowlSize.Medium => Strings.Settings_BowlMedium,
        BowlSize.Large => Strings.Settings_BowlLarge,
        _ => throw new ArgumentOutOfRangeException(nameof(bowlSize), bowlSize, null),
    };

    public static string RegionName(Region region) => region switch
    {
        Region.North => Strings.Region_North,
        Region.Central => Strings.Region_Central,
        Region.South => Strings.Region_South,
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
    };
}
