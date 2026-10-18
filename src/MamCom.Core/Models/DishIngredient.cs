namespace MamCom.Core.Models;

/// <param name="GramsEdiblePerServing">Edible grams for one serving; scaled to the household by <paramref name="Scale"/>.</param>
/// <param name="ConsumedRatio">Share of the ingredient actually eaten, for example less than 1 for deep-frying oil.</param>
public sealed record DishIngredient(
    string IngredientId,
    double GramsEdiblePerServing,
    ScaleKind Scale,
    double ConsumedRatio = 1,
    string? Note = null);
