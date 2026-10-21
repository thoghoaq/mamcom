using MamCom.Core.Models;

namespace MamCom.Core.Cooking;

/// <summary>
/// How long cooking a set of dishes takes in total, rice included: the "mealTime" of SPEC §5.6, the moment the
/// last step ends for one cook. The spinner uses it for the available-minutes rule and does not know how it is
/// computed; <see cref="CookPlanScheduler"/> (PLAN 1.7) is the real implementation.
/// </summary>
public interface IMealTimeEstimator
{
    /// <summary>The planned meal time for exactly these dishes.</summary>
    /// <param name="stoves">The stoves of the kitchen (ADR-028); <see cref="CookPlanScheduler.AnyNumberOfStoves"/> when they do not matter.</param>
    int EstimateMealMinutes(IReadOnlyList<Dish> dishes, int stoves = CookPlanScheduler.AnyNumberOfStoves);

    /// <summary>
    /// A time no plan for these dishes, or for any larger set of dishes, can beat. It never decreases when a dish is
    /// added and never exceeds <see cref="EstimateMealMinutes"/> of this set or any superset. A greedy planner's
    /// estimate can shrink when a dish is added, so the spinner relies on this bound, not on the estimate, to tell
    /// when a time limit can no longer be met.
    /// </summary>
    int MinimumMealMinutes(IReadOnlyList<Dish> dishes, int stoves = CookPlanScheduler.AnyNumberOfStoves);
}
