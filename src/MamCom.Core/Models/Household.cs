namespace MamCom.Core.Models;

/// <param name="AvailableMinutes">Optional time limit, entered per spin (SPEC §4.1).</param>
public sealed record Household(Region Region, BowlSize BowlSize, IReadOnlyList<Member> Members, int? AvailableMinutes = null);
