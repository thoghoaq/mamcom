using MamCom.Core.Models;

namespace MamCom.Core.Portions;

/// <summary>Serving totals for one household and one meal (SPEC §4.2).</summary>
/// <param name="MemberServings">Servings per member, in the order of <see cref="Household.Members"/>.</param>
/// <param name="BaseServings">Sum of member servings; used for soups.</param>
/// <param name="ProteinServings">Member servings with the more-protein factor; used for main dishes.</param>
/// <param name="VegServings">Member servings with the more-vegetables factor; used for vegetable dishes.</param>
/// <param name="TotalBowls">Sum of bowls of rice per meal.</param>
/// <param name="RawRiceGrams">Raw rice to cook, rounded up to the configured multiple.</param>
public sealed record PortionResult(
    IReadOnlyList<double> MemberServings,
    double BaseServings,
    double ProteinServings,
    double VegServings,
    double TotalBowls,
    double RawRiceGrams)
{
    /// <summary>The serving total a dish in <paramref name="slot"/> is scaled to.</summary>
    public double ServingsFor(DishSlot slot) => slot switch
    {
        DishSlot.Soup => BaseServings,
        DishSlot.Main => ProteinServings,
        DishSlot.Vegetable => VegServings,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };
}
