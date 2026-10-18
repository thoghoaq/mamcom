namespace MamCom.Core.Models;

/// <param name="MealTimeMinutes">When the last step ends. This is the "whole meal" time, not the sum of dish times.</param>
public sealed record CookPlan(IReadOnlyList<CookPlanStep> Steps, int MealTimeMinutes)
{
    /// <summary>Rice is a pseudo-dish that always starts first, so it has an id but no data file. Real dishes must not use it.</summary>
    public const string RiceDishId = "com";
}
