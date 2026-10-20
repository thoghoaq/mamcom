using MamCom.Core.Cooking;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Shopping;

namespace MamCom.Core.Spinning;

/// <summary>What a dish card says besides its name (SPEC §8.1-2).</summary>
/// <param name="Minutes">All the steps of the dish added up, hands-on and unattended.</param>
/// <param name="KcalPerServing">Null when an ingredient has no confirmed energy value yet; never zero in its place.</param>
public sealed record DishFacts(string DishId, int Minutes, double? KcalPerServing);

/// <summary>The summary under the tray (SPEC §8.1-2).</summary>
/// <param name="MealMinutes">When the last step ends for one cook: the whole meal, not the sum of the dishes (SPEC §5.6).</param>
/// <param name="KcalPerPerson">The energy of the meal, rice included, shared out evenly over the household; null when it is not known yet.</param>
/// <param name="RawRiceGrams">Raw rice to cook for the household.</param>
/// <param name="ItemsToBuy">Things on the shopping list that are not pantry items.</param>
public sealed record TraySummary(int MealMinutes, double? KcalPerPerson, double RawRiceGrams, int ItemsToBuy);

/// <summary>Works out the facts for the cards and the summary of a tray from the calculators that already exist.</summary>
public sealed class TraySummaryBuilder
{
    private readonly PortionCalculator _portions;
    private readonly NutritionCalculator _nutrition;
    private readonly ShoppingListBuilder _shopping;
    private readonly CookPlanScheduler _scheduler;

    public TraySummaryBuilder(PortionConfig portions, NutritionConfig nutrition, CookPlanScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(portions);
        ArgumentNullException.ThrowIfNull(nutrition);
        ArgumentNullException.ThrowIfNull(scheduler);

        _portions = new PortionCalculator(portions);
        _nutrition = new NutritionCalculator(nutrition, portions);
        _shopping = new ShoppingListBuilder(portions);
        _scheduler = scheduler;
    }

    /// <exception cref="InvalidOperationException">The dish uses an ingredient that is not in <paramref name="ingredients"/>.</exception>
    public DishFacts FactsOf(Dish dish, IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(dish);
        ArgumentNullException.ThrowIfNull(ingredients);

        var minutes = dish.Steps.Sum(step => step.ActiveMinutes + step.PassiveMinutes);
        return new DishFacts(dish.Id, minutes, _nutrition.ForDish(dish, ingredients).PerServing.Kcal);
    }

    /// <exception cref="InvalidOperationException">A dish or the rice uses an ingredient that is not in <paramref name="ingredients"/>.</exception>
    public TraySummary Build(Household household, IReadOnlyList<Dish> dishes, IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(dishes);
        ArgumentNullException.ThrowIfNull(ingredients);

        var portions = _portions.Calculate(household);
        var meal = _nutrition.ForMeal(household, portions, dishes, ingredients);
        var items = _shopping.Build(household, portions, dishes, ingredients);

        return new TraySummary(
            _scheduler.EstimateMealMinutes(dishes),
            household.Members.Count == 0 ? null : meal.Total.Kcal / household.Members.Count,
            portions.RawRiceGrams,
            items.Count(item => !item.IsPantry));
    }
}
