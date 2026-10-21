using System.Text.Json.Serialization;

namespace MamCom.Core.Models;

/// <param name="AvailableMinutes">Optional time limit, entered per spin (SPEC §4.1).</param>
/// <param name="Stoves">How many stoves the kitchen has, the rice cooker not counted; null when the household has not said (ADR-028).</param>
public sealed record Household(Region Region, BowlSize BowlSize, IReadOnlyList<Member> Members, int? AvailableMinutes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Stoves = null);
