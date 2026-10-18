namespace MamCom.Core.Models;

/// <summary>Who wrote and checked a dish. Only MamCom.Studio, run by a person, may set the review and cook-test fields.</summary>
public sealed record Provenance(
    DraftAuthor DraftedBy,
    string? ReviewedBy,
    DateOnly? ReviewedAt,
    DateOnly? CookTestedAt,
    int? MeasuredTotalMinutes);
