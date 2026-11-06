using MamCom.Core.Models;

namespace MamCom.Core.Spinning;

/// <summary>Everything the spinner reads that is not configuration: who eats, what exists, what has happened.</summary>
/// <param name="Household">Region, bowl size, members with their dislikes, and the optional minutes available.</param>
/// <param name="Dishes">The dishes that may be drawn. Which of them are released is decided before, when the data is built.</param>
/// <param name="Ingredients">Ingredient list, used to check members' avoid lists against recipes.</param>
/// <param name="BlockedDishIds">Dishes the household never wants to see.</param>
/// <param name="LastEatenOn">The last day each dish was eaten; dishes never eaten are absent.</param>
/// <param name="Preferences">Dishes the household rated; dishes absent are neutral.</param>
/// <param name="Size">How many dishes the tray has (ADR-034); <see cref="TraySize.Auto"/> goes by the servings of the household.</param>
/// <param name="Meal">The kind of meal to spin (ADR-037): a tray with rice, or one dish that is the whole meal.</param>
public sealed record SpinContext(
    Household Household,
    IReadOnlyList<Dish> Dishes,
    IReadOnlyDictionary<string, Ingredient> Ingredients,
    IReadOnlySet<string> BlockedDishIds,
    IReadOnlyDictionary<string, DateOnly> LastEatenOn,
    IReadOnlyDictionary<string, DishPreference> Preferences,
    int Stoves = Cooking.CookPlanScheduler.AnyNumberOfStoves,
    TraySize Size = TraySize.Auto,
    MealKind Meal = MealKind.Tray);
