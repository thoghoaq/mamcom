namespace MamCom.Core.Models;

/// <summary>
/// Nutrients for some amount of food: per 100g edible on an <see cref="Ingredient"/>, or a computed total.
/// A null value means unknown (placeholder data), never zero, and it stays unknown through addition.
/// </summary>
public sealed record NutritionFacts(
    double? Kcal,
    double? ProteinG,
    double? FatG,
    double? CarbG,
    double? FiberG,
    double? SodiumMg)
{
    public static NutritionFacts Zero { get; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>Nutrient by nutrient sum; a nutrient that is unknown on either side is unknown in the result.</summary>
    public static NutritionFacts operator +(NutritionFacts left, NutritionFacts right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new(
            left.Kcal + right.Kcal,
            left.ProteinG + right.ProteinG,
            left.FatG + right.FatG,
            left.CarbG + right.CarbG,
            left.FiberG + right.FiberG,
            left.SodiumMg + right.SodiumMg);
    }

    /// <summary>Multiplies every known nutrient by <paramref name="factor"/>; unknown stays unknown.</summary>
    public NutritionFacts Times(double factor) =>
        new(Kcal * factor, ProteinG * factor, FatG * factor, CarbG * factor, FiberG * factor, SodiumMg * factor);

    /// <summary>True when every nutrient has a value.</summary>
    public bool IsComplete() =>
        Kcal is not null && ProteinG is not null && FatG is not null && CarbG is not null && FiberG is not null && SodiumMg is not null;
}
