using MamCom.Core.Models;

namespace MamCom.Core.Households;

/// <summary>
/// The chips of the first screen, "Nhà mình có ai?" (SPEC §8.1-1): a role is how a person says who is at the table. It only picks
/// the starting age group; the person can change it on the card.
/// </summary>
public enum MemberRole
{
    Father,
    Mother,
    Grandfather,
    Grandmother,
    Son,
    Daughter,
    Baby,
}

public static class MemberRoles
{
    /// <summary>The chips in the order the screen shows them (SPEC §8.1-1).</summary>
    public static IReadOnlyList<MemberRole> InChipOrder { get; } =
    [
        MemberRole.Father,
        MemberRole.Mother,
        MemberRole.Grandfather,
        MemberRole.Grandmother,
        MemberRole.Son,
        MemberRole.Daughter,
        MemberRole.Baby,
    ];

    /// <summary>
    /// The age group a role starts with. A son or daughter starts as a child (6-11), the age most families put on that chip; a
    /// teenager or a grown-up child is one change on the card. A baby is a toddler (2-5), the youngest group there is.
    /// </summary>
    public static AgeGroup DefaultAgeGroup(this MemberRole role) => role switch
    {
        MemberRole.Father or MemberRole.Mother => AgeGroup.Adult,
        MemberRole.Grandfather or MemberRole.Grandmother => AgeGroup.Senior,
        MemberRole.Son or MemberRole.Daughter => AgeGroup.Child,
        MemberRole.Baby => AgeGroup.Toddler,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
