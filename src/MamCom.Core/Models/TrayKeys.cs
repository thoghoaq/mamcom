using System.Globalization;

namespace MamCom.Core.Models;

/// <summary>Names a tray, so what the person did for one tray (the ticks of the shopping list, how far the cooking got) is not shown on another.</summary>
public static class TrayKeys
{
    /// <summary>The day and the dishes in the order of the slots: "2026-10-18|thit-kho-trung,rau-muong-xao". A lock is not part of it; a swapped dish is.</summary>
    public static string Of(MealTray tray)
    {
        ArgumentNullException.ThrowIfNull(tray);

        return string.Create(CultureInfo.InvariantCulture, $"{tray.Date:yyyy-MM-dd}|{string.Join(',', tray.Slots.Select(slot => slot.DishId))}");
    }
}
