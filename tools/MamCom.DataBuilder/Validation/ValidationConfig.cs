using System.Text.Json;
using MamCom.Core.Models;

namespace MamCom.DataBuilder.Validation;

public sealed record ValueRange(double Min, double Max)
{
    public bool Contains(double value) => value >= Min && value <= Max;
}

/// <summary>One range per dish slot, named like the slot values in the dish files.</summary>
public sealed record SlotRanges(ValueRange Canh, ValueRange Man, ValueRange Rau)
{
    public ValueRange For(DishSlot slot) => slot switch
    {
        DishSlot.Soup => Canh,
        DishSlot.Main => Man,
        DishSlot.Vegetable => Rau,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };
}

/// <summary>The thresholds of the warnings in SPEC §7.3, loaded from <c>data/config/validation-defaults.json</c>.</summary>
/// <param name="KcalPerServing">Plausible energy of one serving of a dish, by slot.</param>
/// <param name="MainProteinGramsPerServing">Plausible grams of protein-source ingredients in one serving of a main dish.</param>
/// <param name="MaxSodiumMgPerServing">Sodium above this per serving of a dish is flagged.</param>
public sealed record ValidationConfig(SlotRanges KcalPerServing, ValueRange MainProteinGramsPerServing, double MaxSodiumMgPerServing)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
    };

    /// <summary>Reads the content of <c>validation-defaults.json</c>. Does not check the values; see <see cref="Validate"/>.</summary>
    /// <exception cref="JsonException">Not JSON, a member missing, or an unknown member.</exception>
    public static ValidationConfig Parse(string json) =>
        JsonSerializer.Deserialize<ValidationConfig>(json, ReadOptions)
        ?? throw new JsonException("validation-defaults.json must be an object, not null.");

    /// <exception cref="ArgumentException">A value is not finite, or a range has its minimum above its maximum.</exception>
    public void Validate()
    {
        Require(IsRange(KcalPerServing.Canh), nameof(KcalPerServing), "canh range must satisfy 0 <= min <= max");
        Require(IsRange(KcalPerServing.Man), nameof(KcalPerServing), "man range must satisfy 0 <= min <= max");
        Require(IsRange(KcalPerServing.Rau), nameof(KcalPerServing), "rau range must satisfy 0 <= min <= max");
        Require(IsRange(MainProteinGramsPerServing), nameof(MainProteinGramsPerServing), "range must satisfy 0 <= min <= max");
        Require(double.IsFinite(MaxSodiumMgPerServing) && MaxSodiumMgPerServing > 0, nameof(MaxSodiumMgPerServing), "must be a positive number");
    }

    // Written so NaN fails: every comparison with NaN is false.
    private static bool IsRange(ValueRange range) => range.Min >= 0 && range.Min <= range.Max && double.IsFinite(range.Max);

    private static void Require(bool condition, string name, string reason)
    {
        if (!condition)
        {
            throw new ArgumentException($"Validation config {name}: {reason}.", name);
        }
    }
}
