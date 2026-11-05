using MamCom.Core.Models;

namespace MamCom.Core.Portions;

/// <summary>
/// How the food is shared out when a tray has more or fewer dishes than the usual three (ADR-034), from <c>portion-defaults.json</c>.
/// </summary>
/// <param name="SameSlotTotal">Two dishes of one kind (two mains) are cooked to this many times the servings of one, between them.</param>
/// <param name="WithoutVegetable">A tray of soup and main with no vegetable dish cooks both this many times larger.</param>
public sealed record TrayShareConfig(double SameSlotTotal, double WithoutVegetable)
{
    /// <exception cref="ArgumentException">A factor is not a positive number.</exception>
    public void Validate()
    {
        if (!(double.IsFinite(SameSlotTotal) && SameSlotTotal > 0))
        {
            throw new ArgumentException("Portion config TrayShares.SameSlotTotal must be a positive number.", nameof(SameSlotTotal));
        }

        if (!(double.IsFinite(WithoutVegetable) && WithoutVegetable > 0))
        {
            throw new ArgumentException("Portion config TrayShares.WithoutVegetable must be a positive number.", nameof(WithoutVegetable));
        }
    }
}

public static class TrayShares
{
    /// <summary>
    /// The part of its usual servings a dish of <paramref name="slot"/> is cooked to on a tray with these <paramref name="traySlots"/>:
    /// 1 on a tray of one dish of each kind, and always 1 when there is no config or no tray to go by.
    /// </summary>
    public static double For(DishSlot slot, IReadOnlyCollection<DishSlot>? traySlots, TrayShareConfig? config)
    {
        if (config is null || traySlots is null || traySlots.Count == 0)
        {
            return 1;
        }

        var sameKind = traySlots.Count(s => s == slot);
        var share = sameKind > 1 ? config.SameSlotTotal / sameKind : 1;
        // A tray of soup and main with no vegetable dish is the tray of two; a tray of one dish (a slot left unfilled) is not made larger.
        var ofTwo = !traySlots.Contains(DishSlot.Vegetable) && traySlots.Contains(DishSlot.Soup) && traySlots.Contains(DishSlot.Main);
        return slot != DishSlot.Vegetable && ofTwo ? share * config.WithoutVegetable : share;
    }
}
