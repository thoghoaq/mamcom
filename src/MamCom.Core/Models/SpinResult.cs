namespace MamCom.Core.Models;

/// <param name="RelaxedRules">Rules given up to fill the tray, in the order they were relaxed; the UI tells the user.</param>
public sealed record SpinResult(MealTray Tray, IReadOnlyList<RelaxedRule> RelaxedRules);
