using MamCom.Core.Models;

namespace MamCom.Core.Cooking;

/// <summary>
/// How long cooking a set of dishes takes in total, rice included: the "mealTime" of SPEC §5.6, the moment the
/// last step ends for one cook. The spinner uses it for the available-minutes rule and does not know how it is
/// computed; <c>CookPlanScheduler</c> (PLAN 1.7) is the real implementation. An implementation must never report a
/// shorter time for a larger set of dishes: the spinner relies on that to tell when a time limit can no longer be met.
/// </summary>
public interface IMealTimeEstimator
{
    int EstimateMealMinutes(IReadOnlyList<Dish> dishes);
}
