namespace MamCom.Core.Models;

/// <summary>Where an ingredient's nutrition numbers came from (SPEC §6.3).</summary>
/// <param name="Db">Database id, or <see cref="NutritionDbs.Placeholder"/> while there is no source.</param>
/// <param name="Ref">Code in that database, for example a USDA <c>fdcId</c>.</param>
public sealed record NutritionSource(string Db, string? Ref, string? MatchedName, string? VerifiedBy, DateOnly? VerifiedAt);
