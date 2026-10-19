using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class ReviewerIdentityTests
{
    [Fact]
    public void Starts_with_the_configured_name_without_the_spaces_around_it()
    {
        new ReviewerIdentity("  Mẹ Lan \t").Name.ShouldBe("Mẹ Lan");
    }

    [Fact]
    public void Starts_empty_when_no_name_is_configured()
    {
        new ReviewerIdentity().Name.ShouldBe(string.Empty);
        new ReviewerIdentity(null).Name.ShouldBe(string.Empty);
    }

    [Fact]
    public void The_name_can_be_changed()
    {
        var identity = new ReviewerIdentity("Mẹ Lan") { Name = "Bà Hoa" };

        identity.Name.ShouldBe("Bà Hoa");
    }
}
