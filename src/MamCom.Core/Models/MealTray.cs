namespace MamCom.Core.Models;

/// <summary>The spun meal: one soup, one or two mains, one vegetable. Rice is implicit.</summary>
/// <remarks>Collection members compare by reference, so two equal trays are not <c>==</c>; compare <paramref name="Slots"/> element by element.</remarks>
/// <param name="Slots">In display order. A slot is addressed by its index here, because a tray can hold two mains.</param>
public sealed record MealTray(DateOnly Date, IReadOnlyList<SlotResult> Slots);
