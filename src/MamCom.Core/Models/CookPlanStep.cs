namespace MamCom.Core.Models;

/// <param name="StartMinute">Minutes after the cook starts.</param>
/// <param name="DishId">Dish id, or <see cref="CookPlan.RiceDishId"/> for the rice pseudo-dish.</param>
/// <param name="ActiveMinutes">Minutes this step occupies the cook; active stretches never overlap.</param>
/// <param name="TimerMinutes">Unattended time that follows the active part and gets a timer, or null.</param>
public sealed record CookPlanStep(int StartMinute, string DishId, string Text, int ActiveMinutes, int? TimerMinutes = null);
