using MamCom.Core.Models;
using MamCom.Core.Portions;

namespace MamCom.Core.Scaling;

/// <summary>
/// Scales recipe quantities, which are stored per serving, to the servings of a meal slot (SPEC §6.1).
/// Whole-unit rounding goes up, so a recipe is never short.
/// </summary>
public sealed class IngredientScaler
{
    private readonly double _sublinearExponent;

    /// <exception cref="ArgumentException">The config has a value the formulas cannot use.</exception>
    public IngredientScaler(PortionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        _sublinearExponent = config.SublinearExponent;
    }

    /// <param name="servings">The slot's serving total, for example <see cref="PortionResult.ServingsFor"/>.</param>
    /// <exception cref="ArgumentException">The line and the ingredient do not match, or a number is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">A whole-unit line has no usable market unit for the region.</exception>
    public ScaledIngredient Scale(DishIngredient line, Ingredient ingredient, Region region, double servings)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(ingredient);

        if (line.IngredientId != ingredient.Id)
        {
            throw new ArgumentException(
                $"The recipe line is for '{line.IngredientId}' but the ingredient is '{ingredient.Id}'.", nameof(ingredient));
        }

        if (!double.IsFinite(servings) || servings < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(servings), servings, "Servings must be a finite number, not negative.");
        }

        var perServing = line.GramsEdiblePerServing;
        if (!double.IsFinite(perServing) || perServing < 0)
        {
            throw new ArgumentException(
                $"Grams per serving of '{line.IngredientId}' must be a finite number, not negative.", nameof(line));
        }

        return line.Scale switch
        {
            ScaleKind.Linear => Grams(line, perServing * servings),
            ScaleKind.Sublinear => Grams(line, perServing * Math.Pow(servings, _sublinearExponent)),
            ScaleKind.Fixed => Grams(line, perServing),
            ScaleKind.Integer => WholeUnits(line, ingredient, region, perServing * servings),
            _ => throw new ArgumentOutOfRangeException(nameof(line), line.Scale, null),
        };
    }

    /// <summary>Scales every line of a dish, in recipe order.</summary>
    /// <exception cref="InvalidOperationException">The dish uses an ingredient that is not in <paramref name="ingredients"/>.</exception>
    public IReadOnlyList<ScaledIngredient> ScaleDish(
        Dish dish,
        IReadOnlyDictionary<string, Ingredient> ingredients,
        Region region,
        double servings)
    {
        ArgumentNullException.ThrowIfNull(dish);
        ArgumentNullException.ThrowIfNull(ingredients);

        return dish.Ingredients
            .Select(line => ingredients.TryGetValue(line.IngredientId, out var ingredient)
                ? Scale(line, ingredient, region, servings)
                : throw new InvalidOperationException($"Dish '{dish.Id}' uses unknown ingredient '{line.IngredientId}'."))
            .ToArray();
    }

    private static ScaledIngredient Grams(DishIngredient line, double grams) =>
        new(line.IngredientId, grams, Units: null, UnitDisplay: null, line.Note);

    private static ScaledIngredient WholeUnits(DishIngredient line, Ingredient ingredient, Region region, double neededGrams)
    {
        var unit = ingredient.MarketUnitFor(region)
            ?? throw new InvalidOperationException(
                $"Ingredient '{ingredient.Id}' is scaled in whole units but has no market unit for region '{region.ToKey()}'.");

        if (!double.IsFinite(unit.GramsPerUnit) || unit.GramsPerUnit <= 0)
        {
            throw new InvalidOperationException(
                $"Ingredient '{ingredient.Id}' has a market unit '{unit.Display}' with grams per unit {unit.GramsPerUnit}; it must be positive.");
        }

        var units = checked((int)Rounding.CeilToMultiple(neededGrams / unit.GramsPerUnit, 1));
        return new ScaledIngredient(line.IngredientId, units * unit.GramsPerUnit, units, unit.Display, line.Note);
    }
}
