using System.Text.Json.Serialization;
using MamCom.Core.Json;

namespace MamCom.Core.Models;

/// <summary>
/// Which part of the meal a dish fills. Soup, main and vegetable make a tray eaten with rice; the others are a meal on their own
/// (ADR-037). Rice is not a slot: it is a pseudo-dish in the cook plan.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<DishSlot>))]
public enum DishSlot
{
    [JsonStringEnumMemberName("canh")] Soup,
    [JsonStringEnumMemberName("man")] Main,
    [JsonStringEnumMemberName("rau")] Vegetable,

    /// <summary>A meal in one bowl: phở, bún, mì, miến, hủ tiếu, cháo, cơm tấm…</summary>
    [JsonStringEnumMemberName("to")] Bowl,

    /// <summary>A hot pot shared at the table, with its noodles or vegetables in it.</summary>
    [JsonStringEnumMemberName("lau")] Hotpot,

    /// <summary>A quick breakfast: bánh mì, xôi, trứng…</summary>
    [JsonStringEnumMemberName("sang")] Breakfast,
}

/// <summary>What kind of meal is spun (ADR-037): a tray of dishes with rice, or one dish that is the whole meal.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<MealKind>))]
public enum MealKind
{
    [JsonStringEnumMemberName("mam")] Tray,
    [JsonStringEnumMemberName("to")] Bowl,
    [JsonStringEnumMemberName("lau")] Hotpot,
    [JsonStringEnumMemberName("sang")] Breakfast,
}

public static class DishSlotExtensions
{
    /// <summary>A dish of the rice tray (soup, main, vegetable): a meal with one of them is eaten with rice.</summary>
    public static bool ComesWithRice(this DishSlot slot) => slot is DishSlot.Soup or DishSlot.Main or DishSlot.Vegetable;

    /// <summary>A meal of these dishes has rice: it has a dish of the tray, or it has no dish at all (the rice alone).</summary>
    public static bool HasRice(this IEnumerable<DishSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        var any = false;
        foreach (var slot in slots)
        {
            if (slot.ComesWithRice())
            {
                return true;
            }

            any = true;
        }

        return !any;
    }
}

[JsonConverter(typeof(StrictStringEnumConverter<SpicyLevel>))]
public enum SpicyLevel
{
    [JsonStringEnumMemberName("none")] None,
    [JsonStringEnumMemberName("optional")] Optional,
    [JsonStringEnumMemberName("required")] Required,
}

[JsonConverter(typeof(StrictStringEnumConverter<CookingMethod>))]
public enum CookingMethod
{
    /// <summary>luộc</summary>
    [JsonStringEnumMemberName("luoc")] Boil,

    /// <summary>hấp</summary>
    [JsonStringEnumMemberName("hap")] Steam,

    /// <summary>nấu</summary>
    [JsonStringEnumMemberName("nau")] Simmer,

    /// <summary>kho</summary>
    [JsonStringEnumMemberName("kho")] Braise,

    /// <summary>xào</summary>
    [JsonStringEnumMemberName("xao")] StirFry,

    /// <summary>chiên</summary>
    [JsonStringEnumMemberName("chien")] Fry,

    /// <summary>nướng</summary>
    [JsonStringEnumMemberName("nuong")] Grill,

    /// <summary>trộn</summary>
    [JsonStringEnumMemberName("tron")] Toss,
}

/// <summary>How a recipe quantity grows with the number of servings (SPEC §6.1).</summary>
[JsonConverter(typeof(StrictStringEnumConverter<ScaleKind>))]
public enum ScaleKind
{
    /// <summary>grams × S</summary>
    [JsonStringEnumMemberName("linear")] Linear,

    /// <summary>grams × S^0.8 (the exponent lives in data/config)</summary>
    [JsonStringEnumMemberName("sublinear")] Sublinear,

    /// <summary>whole units: ceil(grams × S / gramsPerUnit)</summary>
    [JsonStringEnumMemberName("integer")] Integer,

    /// <summary>grams, independent of S</summary>
    [JsonStringEnumMemberName("fixed")] Fixed,
}

[JsonConverter(typeof(StrictStringEnumConverter<Aisle>))]
public enum Aisle
{
    /// <summary>thịt cá</summary>
    [JsonStringEnumMemberName("thit-ca")] MeatAndFish,

    /// <summary>rau củ quả</summary>
    [JsonStringEnumMemberName("rau-cu-qua")] Produce,

    /// <summary>trứng đậu phụ</summary>
    [JsonStringEnumMemberName("trung-dau-phu")] EggsAndTofu,

    /// <summary>khô và gia vị</summary>
    [JsonStringEnumMemberName("kho-gia-vi")] DryGoodsAndSpices,

    /// <summary>khác</summary>
    [JsonStringEnumMemberName("khac")] Other,
}

/// <summary>Declared in lifecycle order, so <c>status &gt;= DishStatus.Reviewed</c> means releasable (SPEC §7.2).</summary>
[JsonConverter(typeof(StrictStringEnumConverter<DishStatus>))]
public enum DishStatus
{
    [JsonStringEnumMemberName("draft")] Draft,
    [JsonStringEnumMemberName("validated")] Validated,
    [JsonStringEnumMemberName("reviewed")] Reviewed,
    [JsonStringEnumMemberName("cooktested")] CookTested,
}

[JsonConverter(typeof(StrictStringEnumConverter<DraftAuthor>))]
public enum DraftAuthor
{
    [JsonStringEnumMemberName("llm")] Llm,
    [JsonStringEnumMemberName("human")] Human,
}
