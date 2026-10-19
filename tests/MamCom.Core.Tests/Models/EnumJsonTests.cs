using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MamCom.Core.Json;
using MamCom.Core.Models;

namespace MamCom.Core.Tests.Models;

public class EnumJsonTests
{
    private static readonly Type[] ModelEnums =
        typeof(Dish).Assembly.GetTypes()
            .Where(t => t.IsEnum && t.Namespace == typeof(Dish).Namespace)
            .ToArray();

    public static TheoryData<Type> EnumTypes => new(ModelEnums);

    [Fact]
    public void The_model_has_the_enums_this_test_is_meant_to_cover()
    {
        ModelEnums.Select(t => t.Name).Order().ShouldBe(
        [
            "AgeGroup", "Aisle", "BowlSize", "CookingMethod", "DishPreference", "DishSlot", "DishStatus", "DraftAuthor",
            "MemberTag", "Region", "RelaxedRule", "ScaleKind", "SpicyLevel",
        ]);
    }

    [Theory]
    [MemberData(nameof(EnumTypes))]
    public void Every_member_has_an_explicit_unique_JSON_name(Type enumType)
    {
        var names = enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name)
            .ToArray();

        names.ShouldAllBe(n => !string.IsNullOrWhiteSpace(n), $"{enumType.Name} has a member without [JsonStringEnumMemberName]");
        names.Distinct().Count().ShouldBe(names.Length, $"{enumType.Name} has duplicate JSON names");
    }

    // The exact strings below are the values SPEC §4.1, §5.3, §6.1, §6.2 and §7.2 define.
    [Theory]
    [InlineData(typeof(DishSlot), "canh,man,rau")]
    [InlineData(typeof(SpicyLevel), "none,optional,required")]
    [InlineData(typeof(CookingMethod), "luoc,hap,nau,kho,xao,chien,nuong,tron")]
    [InlineData(typeof(ScaleKind), "linear,sublinear,integer,fixed")]
    [InlineData(typeof(Aisle), "thit-ca,rau-cu-qua,trung-dau-phu,kho-gia-vi,khac")]
    [InlineData(typeof(DishStatus), "draft,validated,reviewed,cooktested")]
    [InlineData(typeof(DraftAuthor), "llm,human")]
    [InlineData(typeof(Region), "north,central,south")]
    [InlineData(typeof(BowlSize), "small,medium,large")]
    [InlineData(typeof(AgeGroup), "toddler,child,teen,adult,senior")]
    [InlineData(typeof(MemberTag), "moreProtein,moreVeg,noSpicy")]
    [InlineData(typeof(DishPreference), "liked,neutral,disliked")]
    public void Enum_values_serialize_to_the_strings_SPEC_defines(Type enumType, string expectedCsv)
    {
        var actual = Enum.GetValues(enumType).Cast<object>()
            .Select(v => JsonSerializer.Serialize(v, enumType, CoreJson.Options).Trim('"'))
            .ToArray();

        actual.ShouldBe(expectedCsv.Split(','));
    }

    [Theory]
    [MemberData(nameof(EnumTypes))]
    public void Every_value_survives_a_round_trip(Type enumType)
    {
        foreach (var value in Enum.GetValues(enumType).Cast<object>())
        {
            var json = JsonSerializer.Serialize(value, enumType, CoreJson.Options);

            JsonSerializer.Deserialize(json, enumType, CoreJson.Options).ShouldBe(value);
        }
    }

    [Fact]
    public void Relaxed_rules_are_declared_in_relaxation_order()
    {
        // SPEC §5.3: season and region weights, then available minutes, then at most one fried dish, then distinct protein.
        Enum.GetValues<RelaxedRule>().ShouldBe(
        [
            RelaxedRule.SeasonAndRegionWeights,
            RelaxedRule.AvailableMinutes,
            RelaxedRule.SingleFriedDish,
            RelaxedRule.DistinctProteinSources,
        ]);
    }

    [Fact]
    public void Dish_status_is_declared_in_lifecycle_order()
    {
        // SPEC §7.2: draft -> validated -> reviewed -> cooktested; release keeps reviewed and above.
        (DishStatus.Draft < DishStatus.Validated).ShouldBeTrue();
        (DishStatus.Validated < DishStatus.Reviewed).ShouldBeTrue();
        (DishStatus.Reviewed < DishStatus.CookTested).ShouldBeTrue();
    }

    [Theory]
    [InlineData(Region.North, "north")]
    [InlineData(Region.Central, "central")]
    [InlineData(Region.South, "south")]
    public void Region_key_matches_its_JSON_name(Region region, string expected)
    {
        region.ToKey().ShouldBe(expected);
        JsonSerializer.Serialize(region, CoreJson.Options).ShouldBe($"\"{expected}\"");
    }

    [Fact]
    public void Region_key_rejects_an_undefined_value()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ((Region)99).ToKey());
    }
}
