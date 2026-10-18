namespace MamCom.Core.Models;

/// <summary>How an ingredient is bought in one region, for example 100g lạng in the north or grams in the south.</summary>
/// <param name="Display">Unit label shown to the user: lạng, g, kg, bó, quả…</param>
/// <param name="Step">Quantities are rounded up to a multiple of this, in <paramref name="Display"/> units.</param>
public sealed record MarketUnit(string Display, double GramsPerUnit, double Step);
