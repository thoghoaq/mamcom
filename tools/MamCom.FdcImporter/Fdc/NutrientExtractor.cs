using MamCom.Core.Models;

namespace MamCom.FdcImporter.Fdc;

/// <summary>The six nutrients of an ingredient file, per 100 g, as one FDC food states them.</summary>
/// <param name="KcalSource">Name of the FDC nutrient the energy came from; null when there is no energy.</param>
/// <param name="Missing">JSON names (<c>kcal</c>, <c>proteinG</c>…) of the nutrients the food does not state, in the order of an ingredient file.</param>
public sealed record ExtractedNutrition(NutritionFacts Per100g, string? KcalSource, IReadOnlyList<string> Missing);

/// <summary>
/// Reads the nutrients SPEC §6.3 uses out of an FDC food. A value is taken only when its unit is the one expected
/// (kcal, g, mg), so an energy in kJ is never read as kcal. A nutrient the food does not state stays unknown (null),
/// never zero.
/// </summary>
public static class NutrientExtractor
{
    // SR Legacy states energy as 1008. Foundation foods state it as the two Atwater energies instead; the specific
    // factors are USDA's more exact ones, so they come first.
    private static readonly int[] EnergyIds = [1008, 2048, 2047];

    private const int ProteinId = 1003;
    private const int FatId = 1004;
    private const int CarbId = 1005;
    private const int FiberId = 1079;
    private const int SodiumId = 1093;

    public static ExtractedNutrition Extract(FdcFood food)
    {
        ArgumentNullException.ThrowIfNull(food);

        var nutrients = food.FoodNutrients ?? [];

        double? kcal = null;
        string? kcalSource = null;
        foreach (var id in EnergyIds)
        {
            if (Find(nutrients, id, "KCAL") is { } energy)
            {
                kcal = energy.Value;
                kcalSource = energy.NutrientName;
                break;
            }
        }

        var facts = new NutritionFacts(
            kcal,
            Find(nutrients, ProteinId, "G")?.Value,
            Find(nutrients, FatId, "G")?.Value,
            Find(nutrients, CarbId, "G")?.Value,
            Find(nutrients, FiberId, "G")?.Value,
            Find(nutrients, SodiumId, "MG")?.Value);

        var missing = new List<string>();
        if (facts.Kcal is null) { missing.Add("kcal"); }
        if (facts.ProteinG is null) { missing.Add("proteinG"); }
        if (facts.FatG is null) { missing.Add("fatG"); }
        if (facts.CarbG is null) { missing.Add("carbG"); }
        if (facts.FiberG is null) { missing.Add("fiberG"); }
        if (facts.SodiumMg is null) { missing.Add("sodiumMg"); }

        return new ExtractedNutrition(facts, kcalSource, missing);
    }

    private static FdcNutrient? Find(IReadOnlyList<FdcNutrient> nutrients, int id, string unit) =>
        nutrients.FirstOrDefault(n =>
            n.NutrientId == id
            && string.Equals(n.UnitName, unit, StringComparison.OrdinalIgnoreCase)
            && n.Value is { } value
            && double.IsFinite(value)
            && value >= 0);
}
