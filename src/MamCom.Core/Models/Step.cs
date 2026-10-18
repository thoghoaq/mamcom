namespace MamCom.Core.Models;

/// <param name="ActiveMinutes">Minutes that need the cook's hands.</param>
/// <param name="PassiveMinutes">Minutes that run unattended: simmering, marinating, a pot on the stove.</param>
public sealed record Step(int Order, string Text, int ActiveMinutes, int PassiveMinutes, IReadOnlyList<string> IngredientIds);
