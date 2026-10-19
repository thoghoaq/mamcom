using MamCom.Core.Models;

namespace MamCom.Core.Shopping;

/// <summary>The group headings of the shared text; aisles with the same heading share one line.</summary>
public sealed record AisleHeadings(string MeatAndFish, string Produce, string EggsAndTofu, string DryGoodsAndSpices, string Other)
{
    public string For(Aisle aisle) => aisle switch
    {
        Aisle.MeatAndFish => MeatAndFish,
        Aisle.Produce => Produce,
        Aisle.EggsAndTofu => EggsAndTofu,
        Aisle.DryGoodsAndSpices => DryGoodsAndSpices,
        Aisle.Other => Other,
        _ => throw new ArgumentOutOfRangeException(nameof(aisle), aisle, null),
    };
}

/// <summary>
/// Every word of the shopping text that is not data. The text is a message sent to Zalo and is golden-tested in
/// Core (PLAN 1.5), so its words live here, in one place, instead of in the UI resources.
/// </summary>
/// <param name="Shopping">Verb phrase opening the header: "Đi chợ".</param>
/// <param name="Meal">Which meal the shopping is for: "tối".</param>
/// <param name="People">Unit of the head count in brackets: "người".</param>
/// <param name="Weekdays">Short weekday names indexed by <see cref="DayOfWeek"/>, so Sunday first.</param>
public sealed record ShareTextLabels(
    string Shopping,
    string Meal,
    string People,
    IReadOnlyList<string> Weekdays,
    AisleHeadings Headings)
{
    /// <summary>
    /// The wording of the SPEC §6.2 example. Eggs and tofu, dry goods and everything else sit under one KHÁC line,
    /// which is how the example shows trứng.
    /// </summary>
    public static ShareTextLabels Vietnamese { get; } = new(
        Shopping: "Đi chợ",
        Meal: "tối",
        People: "người",
        Weekdays: ["CN", "T2", "T3", "T4", "T5", "T6", "T7"],
        Headings: new AisleHeadings(
            MeatAndFish: "THỊT CÁ",
            Produce: "RAU",
            EggsAndTofu: "KHÁC",
            DryGoodsAndSpices: "KHÁC",
            Other: "KHÁC"));
}
