namespace MamCom.Core.Models;

/// <summary>
/// Nutrients for some amount of food: per 100g edible on an <see cref="Ingredient"/>, or a computed total.
/// A null value means unknown (placeholder data), never zero.
/// </summary>
public sealed record NutritionFacts(
    double? Kcal,
    double? ProteinG,
    double? FatG,
    double? CarbG,
    double? FiberG,
    double? SodiumMg);
