using System.Security.Cryptography;
using System.Text;
using MamCom.Core.Cooking;
using MamCom.Core.Data;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Spinning;

namespace MamCom.Core.Tests.Data;

/// <summary>
/// The check the app makes on a <c>data-bundle.json</c> it fetched: the version is the hash of the text as it came. A phone keeps the bundle
/// it has, so a bundle made before the app learned a field it can do without (the stoves of ADR-028) must still be whole.
/// </summary>
public class BundleTextVersionTests
{
    private static DataBundle Bundle() => new(
        string.Empty,
        IsRelease: false,
        new BundleConfig(
            PortionConfig.Parse(TestFiles.Config("portion-defaults.json")),
            NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json")),
            SpinnerConfig.Parse(TestFiles.Config("spinner-defaults.json")),
            CookingConfig.Parse(TestFiles.Config("cooking-defaults.json"))),
        [],
        []);

    /// <summary>What a builder writes: the hash of the text with the version empty, then the text with the version in it.</summary>
    private static (string Text, string Version) Written(string unstamped)
    {
        var version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(unstamped)))[..16];
        return (unstamped.Replace("\"version\": \"\"", $"\"version\": \"{version}\"", StringComparison.Ordinal), version);
    }

    [Fact]
    public void The_text_the_builder_writes_matches_its_version()
    {
        var bundle = Bundle().WithComputedVersion();

        DataBundle.TextMatchesVersion(bundle.ToJson(), bundle.Version).ShouldBeTrue();
    }

    [Fact]
    public void A_text_changed_after_it_was_written_does_not()
    {
        var bundle = Bundle().WithComputedVersion();

        DataBundle.TextMatchesVersion(bundle.ToJson().Replace("Vo gạo", "Vo gao", StringComparison.Ordinal), bundle.Version).ShouldBeFalse();
    }

    [Fact]
    public void A_bundle_made_before_the_stoves_were_known_is_whole_though_written_again_it_would_not_be()
    {
        var withoutStoves = Bundle().ToJson()
            .Replace(",\n      \"defaultStoves\": 2", string.Empty, StringComparison.Ordinal)
            .Replace(",\n      \"maxStoves\": 4", string.Empty, StringComparison.Ordinal);
        withoutStoves.ShouldNotContain("Stoves");
        var (text, version) = Written(withoutStoves);

        DataBundle.TextMatchesVersion(text, version).ShouldBeTrue();

        var read = DataBundle.Parse(text);
        read.Config.Cooking.DefaultStoves.ShouldBe(2);
        read.VersionMatchesContent().ShouldBeFalse();   // the check this replaced turned such a bundle away as broken
    }

    [Theory]
    [InlineData("")]
    [InlineData("0000000000000000")]
    public void A_version_that_is_empty_or_not_in_the_text_does_not_match(string version)
    {
        var bundle = Bundle().WithComputedVersion();

        DataBundle.TextMatchesVersion(bundle.ToJson(), version).ShouldBeFalse();
        Should.Throw<ArgumentNullException>(() => DataBundle.TextMatchesVersion(null!, version));
    }
}
