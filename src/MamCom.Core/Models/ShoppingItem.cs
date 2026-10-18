namespace MamCom.Core.Models;

/// <param name="Name">Name shown for the household's region.</param>
/// <param name="Quantity">Amount in market units, already rounded: 3 for "3 lạng", 2 for "2 quả".</param>
/// <param name="UnitDisplay">The market unit label, for example lạng or quả.</param>
/// <param name="PurchaseGrams">Grams to buy including waste, before rounding to market units.</param>
/// <param name="IsPantry">Pantry items are hidden from the list unless the user asks to see them.</param>
public sealed record ShoppingItem(
    string IngredientId,
    string Name,
    Aisle Aisle,
    double Quantity,
    string UnitDisplay,
    double PurchaseGrams,
    bool IsPantry);
