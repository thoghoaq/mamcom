using System.Text.Json.Serialization;
using MamCom.Core.Json;

namespace MamCom.Core.Models;

/// <summary>What the household said about a dish after a meal (SPEC §8.1, screen 6). Dishes never rated are neutral.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<DishPreference>))]
public enum DishPreference
{
    [JsonStringEnumMemberName("liked")] Liked,
    [JsonStringEnumMemberName("neutral")] Neutral,
    [JsonStringEnumMemberName("disliked")] Disliked,
}
