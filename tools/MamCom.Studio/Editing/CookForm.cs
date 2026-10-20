using MamCom.Core.Models;
using MamCom.Studio.Review;

namespace MamCom.Studio.Editing;

/// <summary>What a person fills in after cooking a dish for real. The numbers are empty until typed, never 0.</summary>
public sealed class CookForm
{
    public CookForm(DateOnly today) => CookedAt = today;

    public DateOnly CookedAt { get; set; }

    public int? Minutes { get; set; }

    public int? Servings { get; set; }

    public int? FinishedGrams { get; set; }

    /// <summary>The picture of the dish. Without a thumb or a full picture there is none and the dish keeps the one it has.</summary>
    public ImageForm Photo { get; set; } = new();

    public CookTestEntry ToEntry() => new(CookedAt, Minutes, Servings, FinishedGrams, Photo.IsEmpty ? null : Photo.ToImageRef());
}
