namespace MamCom.Studio.Review;

/// <summary>
/// The name of the person at the screen, kept between dishes so it is typed once. It starts as <c>Studio:Reviewer</c>, or empty.
/// Studio is one person's tool on one machine, so one name is enough.
/// </summary>
public sealed class ReviewerIdentity(string? name = null)
{
    public string Name { get; set; } = name?.Trim() ?? string.Empty;
}
