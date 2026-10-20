using System.Text.Json.Serialization;

namespace MamCom.Core.Models;

/// <summary>
/// Who wrote and checked a dish, and what the one cook test measured. Only MamCom.Studio, run by a person, may set the review and
/// cook-test fields.
/// </summary>
/// <param name="MeasuredTotalMinutes">Minutes the cook test took from the first cut to the table.</param>
/// <param name="MeasuredServings">
/// How many servings that cook made. Without it the weight below means nothing. Left out of the file while there is no cook test,
/// so dishes written before the field existed stay byte for byte as they are.
/// </param>
/// <param name="MeasuredFinishedGrams">The weight of everything that cook made, as it went to the table (a soup counts with its broth). Left out like <paramref name="MeasuredServings"/>.</param>
public sealed record Provenance(
    DraftAuthor DraftedBy,
    string? ReviewedBy,
    DateOnly? ReviewedAt,
    DateOnly? CookTestedAt,
    int? MeasuredTotalMinutes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MeasuredServings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MeasuredFinishedGrams = null);
