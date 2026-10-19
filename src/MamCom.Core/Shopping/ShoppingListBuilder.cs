using MamCom.Core.Models;
using MamCom.Core.Portions;
using MamCom.Core.Scaling;

namespace MamCom.Core.Shopping;

/// <summary>Builds the shopping list of a meal (SPEC §6.2): merged, waste included, in the region's market units.</summary>
public sealed class ShoppingListBuilder
{
    private readonly PortionConfig _portions;
    private readonly IngredientScaler _scaler;

    /// <exception cref="ArgumentException">The portion config has a value the formulas cannot use.</exception>
    public ShoppingListBuilder(PortionConfig portions)
    {
        ArgumentNullException.ThrowIfNull(portions);
        portions.Validate();
        _portions = portions;
        _scaler = new IngredientScaler(portions);
    }

    /// <summary>
    /// One item per ingredient across the whole meal, rice included. Items are ordered by aisle, and inside an aisle
    /// by first use: rice first, then the dishes in the order given. Pantry items are included and flagged; hiding
    /// them is the caller's choice (see <see cref="ShoppingGroups.ByAisle"/>). Ingredients that need nothing are left out.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A dish uses an unknown ingredient, the rice ingredient is missing, or an ingredient has unusable waste or market-unit data for the region.
    /// </exception>
    public IReadOnlyList<ShoppingItem> Build(
        Household household,
        PortionResult portions,
        IReadOnlyList<Dish> dishes,
        IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(portions);
        ArgumentNullException.ThrowIfNull(dishes);
        ArgumentNullException.ThrowIfNull(ingredients);

        var edibleGrams = new Dictionary<string, double>();
        var firstUse = new List<string>();

        void Need(string ingredientId, double grams)
        {
            if (edibleGrams.TryGetValue(ingredientId, out var soFar))
            {
                edibleGrams[ingredientId] = soFar + grams;
            }
            else
            {
                edibleGrams[ingredientId] = grams;
                firstUse.Add(ingredientId);
            }
        }

        if (portions.RawRiceGrams > 0)
        {
            if (!ingredients.ContainsKey(_portions.RiceIngredientId))
            {
                throw new InvalidOperationException($"The rice ingredient '{_portions.RiceIngredientId}' is not in the ingredient list.");
            }

            Need(_portions.RiceIngredientId, portions.RawRiceGrams);
        }

        foreach (var dish in dishes)
        {
            foreach (var line in _scaler.ScaleDish(dish, ingredients, household.Region, portions.ServingsFor(dish.Slot)))
            {
                Need(line.IngredientId, line.GramsEdible);
            }
        }

        return firstUse
            .Select(id => ToItem(ingredients[id], edibleGrams[id], household.Region))
            .OfType<ShoppingItem>()
            .OrderBy(item => (int)item.Aisle) // stable, so first-use order survives inside an aisle
            .ToArray();
    }

    private static ShoppingItem? ToItem(Ingredient ingredient, double edibleGrams, Region region)
    {
        var waste = ingredient.WasteRatio;
        if (!(waste >= 0 && waste < 1))
        {
            throw new InvalidOperationException($"Ingredient '{ingredient.Id}' has waste ratio {waste}; it must be at least 0 and below 1.");
        }

        var purchaseGrams = edibleGrams / (1 - waste);
        if (purchaseGrams <= 0)
        {
            return null;
        }

        var unit = ingredient.MarketUnitFor(region)
            ?? throw new InvalidOperationException(
                $"Ingredient '{ingredient.Id}' is on the shopping list but has no market unit for region '{region.ToKey()}'.");

        if (!double.IsFinite(unit.GramsPerUnit) || unit.GramsPerUnit <= 0 || !double.IsFinite(unit.Step) || unit.Step <= 0)
        {
            throw new InvalidOperationException(
                $"Ingredient '{ingredient.Id}' has market unit '{unit.Display}' with grams per unit {unit.GramsPerUnit} and step {unit.Step}; both must be positive.");
        }

        // Weighed units (lạng, g) round up to their step; counted units (quả, con) have a step of 1, so one formula covers both.
        var quantity = Math.Round(Rounding.CeilToMultiple(purchaseGrams / unit.GramsPerUnit, unit.Step), 9);

        return new ShoppingItem(
            ingredient.Id,
            ingredient.NameFor(region),
            ingredient.Aisle,
            quantity,
            unit.Display,
            purchaseGrams,
            ingredient.IsPantry,
            unit.ShowApproxGrams ? quantity * unit.GramsPerUnit : null);
    }
}
