namespace MamCom.Core.Models;

/// <remarks>Collection members compare by reference; compare the tray's slots and <paramref name="RelaxedRules"/> element by element.</remarks>
/// <param name="RelaxedRules">
/// Rules given up to fill the tray, in relaxation order: the season and region weights when they were dropped, and
/// each tray rule the delivered tray actually breaks. The UI tells the user.
/// </param>
/// <param name="UnfilledSlots">Slots no dish could fill under the hard rules, one entry per missing dish; the tray has no entry for them.</param>
public sealed record SpinResult(MealTray Tray, IReadOnlyList<RelaxedRule> RelaxedRules, IReadOnlyList<DishSlot> UnfilledSlots);
