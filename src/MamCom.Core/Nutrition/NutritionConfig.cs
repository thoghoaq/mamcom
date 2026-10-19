using System.Text.Json;
using MamCom.Core.Json;

namespace MamCom.Core.Nutrition;

public sealed record MacroValues(double Protein, double Fat, double Carb);

/// <summary>A recommended share of energy as fractions of 1, for example 0.13 to 0.20.</summary>
public sealed record ShareRange(double Min, double Max);

public sealed record RecommendedShares(ShareRange Protein, ShareRange Fat, ShareRange Carb);

/// <summary>The nutrition constants of SPEC §6.3, loaded from <c>data/config/nutrition-defaults.json</c>.</summary>
/// <param name="KcalPerGram">Energy per gram of protein, fat and carbohydrate (4, 9, 4).</param>
/// <param name="RecommendedEnergyShare">Recommended share of energy from each macronutrient, shown against the actual share.</param>
public sealed record NutritionConfig(MacroValues KcalPerGram, RecommendedShares RecommendedEnergyShare)
{
    /// <summary>Reads the content of <c>nutrition-defaults.json</c>. Does not validate values; see <see cref="Validate"/>.</summary>
    public static NutritionConfig Parse(string json) =>
        JsonSerializer.Deserialize(json, CoreJsonContext.Default.NutritionConfig)
        ?? throw new JsonException("nutrition-defaults.json must be an object, not null.");

    /// <exception cref="ArgumentException">A value is not finite or is outside the range the formulas need.</exception>
    public void Validate()
    {
        Require(IsPositive(KcalPerGram.Protein), nameof(KcalPerGram), "protein needs a positive energy per gram");
        Require(IsPositive(KcalPerGram.Fat), nameof(KcalPerGram), "fat needs a positive energy per gram");
        Require(IsPositive(KcalPerGram.Carb), nameof(KcalPerGram), "carbohydrate needs a positive energy per gram");
        Require(IsRange(RecommendedEnergyShare.Protein), nameof(RecommendedEnergyShare), "protein range must satisfy 0 <= min <= max <= 1");
        Require(IsRange(RecommendedEnergyShare.Fat), nameof(RecommendedEnergyShare), "fat range must satisfy 0 <= min <= max <= 1");
        Require(IsRange(RecommendedEnergyShare.Carb), nameof(RecommendedEnergyShare), "carbohydrate range must satisfy 0 <= min <= max <= 1");
    }

    private static bool IsPositive(double value) => double.IsFinite(value) && value > 0;

    // Written so NaN fails: every comparison with NaN is false.
    private static bool IsRange(ShareRange range) => range.Min >= 0 && range.Min <= range.Max && range.Max <= 1;

    private static void Require(bool condition, string name, string reason)
    {
        if (!condition)
        {
            throw new ArgumentException($"Nutrition config {name}: {reason}.", name);
        }
    }
}
