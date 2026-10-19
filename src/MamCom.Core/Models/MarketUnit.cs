using System.Text.Json.Serialization;

namespace MamCom.Core.Models;

/// <summary>How an ingredient is bought in one region, for example 100g lạng in the north or grams in the south.</summary>
/// <param name="Display">Unit label shown to the user: lạng, g, kg, bó, quả…</param>
/// <param name="Step">Quantities are rounded up to a multiple of this, in <paramref name="Display"/> units.</param>
/// <param name="ShowApproxGrams">
/// When true the shared shopping text adds "(~Ng)" after the name, N being units × <paramref name="GramsPerUnit"/>.
/// For counted units whose size varies a lot, such as one fish. Left out of the JSON when false.
/// </param>
public sealed record MarketUnit(
    string Display,
    double GramsPerUnit,
    double Step,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ShowApproxGrams = false);
