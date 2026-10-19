namespace MamCom.Core.Scaling;

/// <summary>One recipe line scaled to a number of servings.</summary>
/// <param name="GramsEdible">Edible grams to use. For whole-unit ingredients this is the whole units times grams per unit.</param>
/// <param name="Units">Whole units, set only for <see cref="Models.ScaleKind.Integer"/> lines.</param>
/// <param name="UnitDisplay">Label of those units (quả, con…), set together with <paramref name="Units"/>.</param>
/// <param name="Note">The recipe line's note, passed through.</param>
public sealed record ScaledIngredient(
    string IngredientId,
    double GramsEdible,
    int? Units,
    string? UnitDisplay,
    string? Note);
