using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Scaling;

namespace MamCom.Core.Details;

/// <summary>
/// Works out what the detail screen of a dish says for a household: the recipe scaled to the servings of the dish's slot (SPEC §6.1),
/// the steps' time, and the nutrition of one serving with its energy shares and the energy each member gets (SPEC §6.3).
/// </summary>
public sealed class DishDetailBuilder
{
    private readonly PortionCalculator _portions;
    private readonly IngredientScaler _scaler;
    private readonly NutritionCalculator _nutrition;
    private readonly RecommendedShares _recommended;

    /// <exception cref="ArgumentException">A config has a value the formulas cannot use.</exception>
    public DishDetailBuilder(PortionConfig portions, NutritionConfig nutrition)
    {
        ArgumentNullException.ThrowIfNull(portions);
        ArgumentNullException.ThrowIfNull(nutrition);

        _portions = new PortionCalculator(portions);
        _scaler = new IngredientScaler(portions);
        _nutrition = new NutritionCalculator(nutrition, portions);
        _recommended = nutrition.RecommendedEnergyShare;
    }

    /// <exception cref="InvalidOperationException">The dish uses an ingredient that is not in <paramref name="ingredients"/>, or a whole-unit line has no market unit for the region.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A member's bowls per meal is outside the configured range.</exception>
    public DishDetail Build(Dish dish, Household household, IReadOnlyDictionary<string, Ingredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(dish);
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(ingredients);

        var portions = _portions.Calculate(household);
        var servings = portions.ServingsFor(dish.Slot);

        var lines = _scaler.ScaleDish(dish, ingredients, household.Region, servings)
            .Select(scaled => new IngredientLine(
                scaled.IngredientId,
                ingredients[scaled.IngredientId].NameFor(household.Region),
                scaled.GramsEdible,
                scaled.Units,
                scaled.UnitDisplay,
                scaled.Note))
            .ToArray();

        var nutrition = _nutrition.ForDish(dish, ingredients);

        return new DishDetail(
            dish,
            servings,
            lines,
            dish.Steps.Sum(step => step.ActiveMinutes),
            dish.Steps.Sum(step => step.PassiveMinutes),
            nutrition.PerServing,
            MacrosOf(nutrition.PerServing),
            [.. household.Members.Select((member, index) => new MemberEnergy(
                member.DisplayName,
                portions.MemberServings[index],
                nutrition.PerServing.Kcal * portions.MemberServings[index]))],
            [.. nutrition.IngredientIdsMissingData.Select(id => ingredients[id].NameFor(household.Region))]);
    }

    private IReadOnlyList<MacroShare>? MacrosOf(NutritionFacts perServing)
    {
        if (_nutrition.EnergySharesOf(perServing) is not { } shares)
        {
            return null;
        }

        return
        [
            Share(Macro.Protein, shares.Protein, _recommended.Protein),
            Share(Macro.Fat, shares.Fat, _recommended.Fat),
            Share(Macro.Carb, shares.Carb, _recommended.Carb),
        ];
    }

    private static MacroShare Share(Macro macro, double share, ShareRange range) => new(
        macro,
        share,
        range.Min,
        range.Max,
        share < range.Min ? RangeStatus.Below : share > range.Max ? RangeStatus.Above : RangeStatus.Within);
}
