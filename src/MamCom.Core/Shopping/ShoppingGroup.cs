using MamCom.Core.Models;

namespace MamCom.Core.Shopping;

/// <summary>The items of one aisle, in shopping order.</summary>
public sealed record ShoppingGroup(Aisle Aisle, IReadOnlyList<ShoppingItem> Items);

public static class ShoppingGroups
{
    /// <summary>
    /// Groups items by aisle in the aisle order of SPEC §6.2 and leaves empty aisles out. Pantry items are
    /// dropped unless <paramref name="includePantry"/> is set (the "show what I already have" toggle).
    /// </summary>
    public static IReadOnlyList<ShoppingGroup> ByAisle(IEnumerable<ShoppingItem> items, bool includePantry)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Where(item => includePantry || !item.IsPantry)
            .GroupBy(item => item.Aisle)
            .OrderBy(group => (int)group.Key)
            .Select(group => new ShoppingGroup(group.Key, group.ToArray()))
            .ToArray();
    }
}
