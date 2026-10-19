using System.Text.Json;
using MamCom.Core.Json;
using MamCom.Core.Models;

namespace MamCom.Core.Portions;

/// <summary>One number per age group. Used for the age modifiers and for the default bowls per meal.</summary>
public sealed record AgeGroupValues(double Toddler, double Child, double Teen, double Adult, double Senior)
{
    public double For(AgeGroup ageGroup) => ageGroup switch
    {
        AgeGroup.Toddler => Toddler,
        AgeGroup.Child => Child,
        AgeGroup.Teen => Teen,
        AgeGroup.Adult => Adult,
        AgeGroup.Senior => Senior,
        _ => throw new ArgumentOutOfRangeException(nameof(ageGroup), ageGroup, null),
    };

    internal IEnumerable<double> All => [Toddler, Child, Teen, Adult, Senior];
}

public sealed record BowlSizeValues(double Small, double Medium, double Large)
{
    public double For(BowlSize bowlSize) => bowlSize switch
    {
        BowlSize.Small => Small,
        BowlSize.Medium => Medium,
        BowlSize.Large => Large,
        _ => throw new ArgumentOutOfRangeException(nameof(bowlSize), bowlSize, null),
    };

    internal IEnumerable<double> All => [Small, Medium, Large];
}

/// <summary>
/// The serving constants of SPEC §4.2, loaded from <c>data/config/portion-defaults.json</c>.
/// servings = (<see cref="ServingsBase"/> + <see cref="ServingsPerBowl"/> × bowls) × age modifier.
/// </summary>
/// <param name="SublinearExponent">Exponent applied to servings for sublinear ingredients such as spices and oil (SPEC §6.1); above 0 and at most 1.</param>
/// <param name="GramsRawRicePerBowl">Raw rice grams in one bowl, by bowl size.</param>
/// <param name="RiceRoundingGrams">Total raw rice is rounded up to a multiple of this.</param>
/// <param name="RiceIngredientId">Id of the raw-rice ingredient: rice is a pseudo-dish, so its nutrition and its shopping line come from this ingredient.</param>
/// <param name="DefaultBowlsPerMeal">Pre-filled bowls per meal for a new member of each age group.</param>
/// <param name="BowlsStep">Step of the bowls stepper in the UI; not enforced by the calculator.</param>
public sealed record PortionConfig(
    double ServingsBase,
    double ServingsPerBowl,
    AgeGroupValues AgeModifiers,
    double MoreProteinFactor,
    double MoreVegFactor,
    double SublinearExponent,
    BowlSizeValues GramsRawRicePerBowl,
    double RiceRoundingGrams,
    string RiceIngredientId,
    AgeGroupValues DefaultBowlsPerMeal,
    double MinBowlsPerMeal,
    double MaxBowlsPerMeal,
    double BowlsStep)
{
    /// <summary>Reads the content of <c>portion-defaults.json</c>. Does not validate values; see <see cref="Validate"/>.</summary>
    public static PortionConfig Parse(string json) =>
        JsonSerializer.Deserialize(json, CoreJsonContext.Default.PortionConfig)
        ?? throw new JsonException("portion-defaults.json must be an object, not null.");

    /// <exception cref="ArgumentException">A value is not finite or is outside the range the formulas need.</exception>
    public void Validate()
    {
        Require(double.IsFinite(ServingsBase) && ServingsBase >= 0, nameof(ServingsBase), "must be a finite number, not negative");
        Require(IsPositive(ServingsPerBowl), nameof(ServingsPerBowl), "must be a positive number");
        Require(AgeModifiers.All.All(IsPositive), nameof(AgeModifiers), "every age group needs a positive modifier");
        Require(IsPositive(MoreProteinFactor), nameof(MoreProteinFactor), "must be a positive number");
        Require(IsPositive(MoreVegFactor), nameof(MoreVegFactor), "must be a positive number");
        Require(IsPositive(SublinearExponent) && SublinearExponent <= 1, nameof(SublinearExponent), "must be above 0 and at most 1");
        Require(GramsRawRicePerBowl.All.All(IsPositive), nameof(GramsRawRicePerBowl), "every bowl size needs positive grams");
        Require(IsPositive(RiceRoundingGrams), nameof(RiceRoundingGrams), "must be a positive number");
        Require(!string.IsNullOrWhiteSpace(RiceIngredientId), nameof(RiceIngredientId), "must name an ingredient");
        Require(IsPositive(MinBowlsPerMeal), nameof(MinBowlsPerMeal), "must be a positive number");
        Require(double.IsFinite(MaxBowlsPerMeal) && MaxBowlsPerMeal >= MinBowlsPerMeal, nameof(MaxBowlsPerMeal), "must be finite and not below the minimum");
        Require(IsPositive(BowlsStep), nameof(BowlsStep), "must be a positive number");
        Require(
            DefaultBowlsPerMeal.All.All(v => v >= MinBowlsPerMeal && v <= MaxBowlsPerMeal),
            nameof(DefaultBowlsPerMeal),
            "every default must lie between the minimum and maximum bowls");
    }

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;

    private static void Require(bool condition, string name, string reason)
    {
        if (!condition)
        {
            throw new ArgumentException($"Portion config {name} {reason}.", name);
        }
    }
}
