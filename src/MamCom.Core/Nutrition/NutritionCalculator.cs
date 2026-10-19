using MamCom.Core.Models;
using MamCom.Core.Portions;

namespace MamCom.Core.Nutrition;

/// <summary>
/// Adds up nutrition from ingredient data (SPEC §6.3). Numbers only come from the ingredients: an unknown value
/// stays unknown, it is never treated as zero.
/// </summary>
public sealed class NutritionCalculator
{
    private readonly NutritionConfig _nutrition;
    private readonly PortionConfig _portions;

    /// <exception cref="ArgumentException">A config has a value the formulas cannot use.</exception>
    public NutritionCalculator(NutritionConfig nutrition, PortionConfig portions)
    {
        ArgumentNullException.ThrowIfNull(nutrition);
        ArgumentNullException.ThrowIfNull(portions);
        nutrition.Validate();
        portions.Validate();
        _nutrition = nutrition;
        _portions = portions;
    }

    /// <summary>One serving of a dish.</summary>
    /// <exception cref="InvalidOperationException">The dish uses an ingredient that is not in <paramref name="ingredients"/>, or a recipe line has an unusable number.</exception>
    public DishNutrition ForDish(Dish dish, IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(dish);
        ArgumentNullException.ThrowIfNull(ingredients);

        var total = NutritionFacts.Zero;
        var missing = new List<string>();

        foreach (var line in dish.Ingredients)
        {
            if (!ingredients.TryGetValue(line.IngredientId, out var ingredient))
            {
                throw new InvalidOperationException($"Dish '{dish.Id}' uses unknown ingredient '{line.IngredientId}'.");
            }

            var grams = ConsumedGrams(dish, line);
            total += Contribution(ingredient.NutritionPer100gEdible, grams);

            if (grams > 0 && !ingredient.NutritionPer100gEdible.IsComplete() && !missing.Contains(line.IngredientId))
            {
                missing.Add(line.IngredientId);
            }
        }

        return new DishNutrition(dish.Id, total, missing);
    }

    /// <summary>
    /// Each member eats the dishes' per-serving nutrition times the member's servings, plus the member's own rice
    /// (bowls × raw grams per bowl of the household's bowl size).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="portions"/> was not calculated for this household.</exception>
    /// <exception cref="InvalidOperationException">An ingredient, including the rice ingredient, is missing from <paramref name="ingredients"/>.</exception>
    public MealNutrition ForMeal(
        Household household,
        PortionResult portions,
        IReadOnlyList<Dish> dishes,
        IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(portions);
        ArgumentNullException.ThrowIfNull(dishes);
        ArgumentNullException.ThrowIfNull(ingredients);

        if (portions.MemberServings.Count != household.Members.Count)
        {
            throw new ArgumentException(
                $"Portions are for {portions.MemberServings.Count} members but the household has {household.Members.Count}.",
                nameof(portions));
        }

        if (!ingredients.TryGetValue(_portions.RiceIngredientId, out var rice))
        {
            throw new InvalidOperationException($"The rice ingredient '{_portions.RiceIngredientId}' is not in the ingredient list.");
        }

        var dishNutrition = dishes.Select(dish => ForDish(dish, ingredients)).ToArray();
        var allDishesPerServing = dishNutrition.Aggregate(NutritionFacts.Zero, (sum, dish) => sum + dish.PerServing);
        var gramsPerBowl = _portions.GramsRawRicePerBowl.For(household.BowlSize);

        var perMember = new NutritionFacts[household.Members.Count];
        for (var i = 0; i < perMember.Length; i++)
        {
            var riceGrams = household.Members[i].BowlsPerMeal * gramsPerBowl;
            perMember[i] = allDishesPerServing.Times(portions.MemberServings[i])
                + Contribution(rice.NutritionPer100gEdible, riceGrams);
        }

        var total = perMember.Aggregate(NutritionFacts.Zero, (sum, member) => sum + member);
        return new MealNutrition(dishNutrition, perMember, total, EnergySharesOf(total));
    }

    /// <summary>
    /// Shares of the energy that protein, fat and carbohydrate contribute (kcal per gram from the config).
    /// Null when any of the three is unknown or the energy is zero.
    /// </summary>
    public EnergyShares? EnergySharesOf(NutritionFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.ProteinG is not { } protein || facts.FatG is not { } fat || facts.CarbG is not { } carb)
        {
            return null;
        }

        var proteinKcal = protein * _nutrition.KcalPerGram.Protein;
        var fatKcal = fat * _nutrition.KcalPerGram.Fat;
        var carbKcal = carb * _nutrition.KcalPerGram.Carb;
        var totalKcal = proteinKcal + fatKcal + carbKcal;

        return totalKcal > 0 && double.IsFinite(totalKcal)
            ? new EnergyShares(proteinKcal / totalKcal, fatKcal / totalKcal, carbKcal / totalKcal)
            : null;
    }

    /// <summary>Nutrients of <paramref name="grams"/> of food. Zero grams contribute nothing even where data is unknown.</summary>
    private static NutritionFacts Contribution(NutritionFacts per100g, double grams) =>
        grams == 0 ? NutritionFacts.Zero : per100g.Times(grams / 100);

    private static double ConsumedGrams(Dish dish, DishIngredient line)
    {
        if (!double.IsFinite(line.GramsEdiblePerServing) || line.GramsEdiblePerServing < 0)
        {
            throw new InvalidOperationException(
                $"Dish '{dish.Id}', ingredient '{line.IngredientId}': grams per serving must be a finite number, not negative.");
        }

        if (!(line.ConsumedRatio >= 0 && line.ConsumedRatio <= 1))
        {
            throw new InvalidOperationException(
                $"Dish '{dish.Id}', ingredient '{line.IngredientId}': consumed ratio must be between 0 and 1.");
        }

        return line.GramsEdiblePerServing * line.ConsumedRatio;
    }
}
