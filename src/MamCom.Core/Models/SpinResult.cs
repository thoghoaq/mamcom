namespace MamCom.Core.Models;

/// <remarks>Collection members compare by reference; compare the tray's slots and <paramref name="RelaxedRules"/> element by element.</remarks>
/// <param name="RelaxedRules">Rules given up to fill the tray, in the order they were relaxed; the UI tells the user.</param>
public sealed record SpinResult(MealTray Tray, IReadOnlyList<RelaxedRule> RelaxedRules);
