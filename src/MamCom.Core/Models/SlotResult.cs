namespace MamCom.Core.Models;

/// <summary>One filled slot of a tray. Holds the dish id, not the dish, so trays stay small in saved history.</summary>
public sealed record SlotResult(DishSlot Slot, string DishId, bool IsLocked);
