using System.Text.Json.Serialization;
using MamCom.Core.Json;

namespace MamCom.Core.Models;

[JsonConverter(typeof(StrictStringEnumConverter<AgeGroup>))]
public enum AgeGroup
{
    /// <summary>2-5 years.</summary>
    [JsonStringEnumMemberName("toddler")] Toddler,

    /// <summary>6-11 years.</summary>
    [JsonStringEnumMemberName("child")] Child,

    /// <summary>12-17 years.</summary>
    [JsonStringEnumMemberName("teen")] Teen,

    /// <summary>18-59 years.</summary>
    [JsonStringEnumMemberName("adult")] Adult,

    /// <summary>60 years and over.</summary>
    [JsonStringEnumMemberName("senior")] Senior,
}

/// <summary>Raw rice per bowl: small 35g, medium 40g, large 50g (values live in data/config).</summary>
[JsonConverter(typeof(StrictStringEnumConverter<BowlSize>))]
public enum BowlSize
{
    [JsonStringEnumMemberName("small")] Small,
    [JsonStringEnumMemberName("medium")] Medium,
    [JsonStringEnumMemberName("large")] Large,
}

[JsonConverter(typeof(StrictStringEnumConverter<Region>))]
public enum Region
{
    [JsonStringEnumMemberName("north")] North,
    [JsonStringEnumMemberName("central")] Central,
    [JsonStringEnumMemberName("south")] South,
}

public static class RegionExtensions
{
    /// <summary>The key used for this region in region-keyed JSON objects such as <c>names</c> and <c>marketUnit</c>.</summary>
    public static string ToKey(this Region region) => region switch
    {
        Region.North => "north",
        Region.Central => "central",
        Region.South => "south",
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
    };
}

[JsonConverter(typeof(StrictStringEnumConverter<MemberTag>))]
public enum MemberTag
{
    [JsonStringEnumMemberName("moreProtein")] MoreProtein,
    [JsonStringEnumMemberName("moreVeg")] MoreVeg,
    [JsonStringEnumMemberName("noSpicy")] NoSpicy,
}
