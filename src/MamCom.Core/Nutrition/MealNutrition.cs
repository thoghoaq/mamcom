using MamCom.Core.Models;

namespace MamCom.Core.Nutrition;

/// <summary>Nutrition of one dish for one serving (SPEC §6.3).</summary>
/// <param name="PerServing">Sum over the recipe lines of grams × consumed ratio × nutrient / 100. A nutrient is null when a line lacks data for it.</param>
/// <param name="IngredientIdsMissingData">Recipe-line ingredients with grams above zero and at least one unknown nutrient, in recipe order.</param>
public sealed record DishNutrition(
    string DishId,
    NutritionFacts PerServing,
    IReadOnlyList<string> IngredientIdsMissingData);

/// <summary>Nutrition of a whole meal, rice included.</summary>
/// <param name="Dishes">One entry per dish, in the order given.</param>
/// <param name="PerMember">What each member eats: dishes × the member's servings, plus the member's rice. In the order of <see cref="Household.Members"/>.</param>
/// <param name="Total">Sum of <paramref name="PerMember"/>.</param>
/// <param name="EnergyShares">Shares of <paramref name="Total"/>; null when protein, fat or carbohydrate is unknown or there is no energy.</param>
public sealed record MealNutrition(
    IReadOnlyList<DishNutrition> Dishes,
    IReadOnlyList<NutritionFacts> PerMember,
    NutritionFacts Total,
    EnergyShares? EnergyShares);
