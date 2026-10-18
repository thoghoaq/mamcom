using System.Text.Json.Serialization;
using MamCom.Core.Json;

namespace MamCom.Core.Models;

/// <summary>A soft rule the spinner had to give up to fill the tray. Declared in relaxation order (SPEC §5.3).</summary>
[JsonConverter(typeof(StrictStringEnumConverter<RelaxedRule>))]
public enum RelaxedRule
{
    [JsonStringEnumMemberName("seasonAndRegionWeights")] SeasonAndRegionWeights,
    [JsonStringEnumMemberName("availableMinutes")] AvailableMinutes,
    [JsonStringEnumMemberName("singleFriedDish")] SingleFriedDish,
    [JsonStringEnumMemberName("distinctProteinSources")] DistinctProteinSources,
}
