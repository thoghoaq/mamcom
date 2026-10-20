using MamCom.Core.Models;

namespace MamCom.Core.Details;

/// <summary>The three macronutrients the share bar of SPEC §6.3 shows, in the order it shows them.</summary>
public enum Macro
{
    Protein,
    Fat,
    Carb,
}

/// <summary>Where a share of energy stands against the range that is recommended for it.</summary>
public enum RangeStatus
{
    Below,
    Within,
    Above,
}

/// <summary>One recipe line of the dish scaled to the household, with the name the household's region uses.</summary>
/// <param name="GramsEdible">Edible grams to use; for a whole-unit line the whole units times the grams of one unit.</param>
/// <param name="Units">Whole units, set only for lines counted in pieces (eggs, tomatoes).</param>
public sealed record IngredientLine(string IngredientId, string Name, double GramsEdible, int? Units, string? UnitDisplay, string? Note);

/// <summary>What one macronutrient contributes to the energy of a serving, against its recommended range.</summary>
/// <param name="Share">A fraction of 1; the three shares add up to 1.</param>
public sealed record MacroShare(Macro Macro, double Share, double RecommendedMin, double RecommendedMax, RangeStatus Status);

/// <summary>The energy one member of the household gets from this dish.</summary>
/// <param name="Servings">The member's servings (SPEC §4.2); the dish's energy per serving is multiplied by it.</param>
/// <param name="Kcal">Null when the energy of the dish is not known yet; never zero in its place.</param>
public sealed record MemberEnergy(string Name, double Servings, double? Kcal);

/// <summary>Everything the detail screen of a dish says for one household (SPEC §8.1-3).</summary>
/// <param name="Servings">The servings the recipe is scaled to: the household's total for the slot the dish belongs to (soup, main or vegetable).</param>
/// <param name="ActiveMinutes">Hands-on minutes of all steps added up.</param>
/// <param name="PassiveMinutes">Unattended minutes of all steps added up.</param>
/// <param name="PerServing">Nutrition of one serving; a nutrient is null when an ingredient has no confirmed value for it.</param>
/// <param name="Macros">Protein, fat and carbohydrate shares with their recommended ranges; null when any of the three is unknown.</param>
/// <param name="PerMember">One entry per member, in the order of the household.</param>
/// <param name="IngredientNamesMissingData">Names of the ingredients with grams above zero that lack a confirmed value, so the sums above are not complete.</param>
public sealed record DishDetail(
    Dish Dish,
    double Servings,
    IReadOnlyList<IngredientLine> Ingredients,
    int ActiveMinutes,
    int PassiveMinutes,
    NutritionFacts PerServing,
    IReadOnlyList<MacroShare>? Macros,
    IReadOnlyList<MemberEnergy> PerMember,
    IReadOnlyList<string> IngredientNamesMissingData)
{
    public int TotalMinutes => ActiveMinutes + PassiveMinutes;
}
