using System.Text.Json;
using MamCom.Core.Cooking;
using MamCom.Core.Data;
using MamCom.Core.Models;
using MamCom.Core.Nutrition;
using MamCom.Core.Portions;
using MamCom.Core.Spinning;
using MamCom.Core.Tests.Seed;

namespace MamCom.Core.Tests.Data;

public class DataBundleTests
{
    private static readonly BundleConfig Config = new(
        PortionConfig.Parse(TestFiles.Config("portion-defaults.json")),
        NutritionConfig.Parse(TestFiles.Config("nutrition-defaults.json")),
        SpinnerConfig.Parse(TestFiles.Config("spinner-defaults.json")),
        CookingConfig.Parse(TestFiles.Config("cooking-defaults.json")));

    private static DataBundle Bundle(bool isRelease = false) =>
        new DataBundle(string.Empty, isRelease, Config, SeedData.IngredientList.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray(), SeedData.Dishes.OrderBy(d => d.Id, StringComparer.Ordinal).ToArray())
        .WithComputedVersion();

    [Fact]
    public void A_bundle_survives_writing_and_reading_without_change()
    {
        var bundle = Bundle();

        var again = DataBundle.Parse(bundle.ToJson());

        again.ToJson().ShouldBe(bundle.ToJson());
        again.Version.ShouldBe(bundle.Version);
        again.Ingredients.Select(i => i.Id).ShouldBe(bundle.Ingredients.Select(i => i.Id));
        again.Dishes.Select(d => d.Id).ShouldBe(bundle.Dishes.Select(d => d.Id));
        again.Config.Spinner.SeasonByMonth.ShouldBe(bundle.Config.Spinner.SeasonByMonth);   // list members compare by reference in a record
        again.Config.Portion.RiceIngredientId.ShouldBe("gao");
    }

    [Fact]
    public void The_text_ends_with_one_line_feed_keeps_vietnamese_readable_and_uses_no_carriage_returns()
    {
        var json = Bundle().ToJson();

        json.ShouldEndWith("}\n");
        json.ShouldNotEndWith("\n\n");
        json.ShouldNotContain('\r');
        json.ShouldContain("Thịt ba chỉ");   // not "Thịt ba chỉ"
    }

    [Fact]
    public void The_version_is_sixteen_lowercase_hex_digits_and_the_same_for_the_same_content()
    {
        var first = Bundle().Version;

        first.ShouldMatch("^[0-9a-f]{16}$");
        first.Length.ShouldBe(DataBundle.VersionLength);
        Bundle().Version.ShouldBe(first);
    }

    [Fact]
    public void The_version_does_not_depend_on_the_version_field_it_replaces()
    {
        var bundle = Bundle();

        (bundle with { Version = "anything" }).ComputeVersion().ShouldBe(bundle.Version);
        bundle.VersionMatchesContent().ShouldBeTrue();
        (bundle with { Version = "anything" }).VersionMatchesContent().ShouldBeFalse();
    }

    [Fact]
    public void Any_change_to_the_content_changes_the_version()
    {
        var bundle = Bundle();
        var dish = bundle.Dishes[0];
        var line = dish.Ingredients[0];

        var versions = new[]
        {
            bundle.Version,
            (bundle with { IsRelease = true }).ComputeVersion(),
            (bundle with { Dishes = [.. bundle.Dishes.Skip(1)] }).ComputeVersion(),
            (bundle with { Ingredients = [.. bundle.Ingredients.Skip(1)] }).ComputeVersion(),
            (bundle with { Dishes = [dish with { Ingredients = [line with { GramsEdiblePerServing = line.GramsEdiblePerServing + 1 }, .. dish.Ingredients.Skip(1)] }, .. bundle.Dishes.Skip(1)] }).ComputeVersion(),
            (bundle with { Dishes = [dish with { Name = dish.Name + "!" }, .. bundle.Dishes.Skip(1)] }).ComputeVersion(),
            (bundle with { Config = Config with { Spinner = Config.Spinner with { HistorySize = 6 } } }).ComputeVersion(),
        };

        versions.ShouldBeUnique();
    }

    [Fact]
    public void A_missing_member_is_an_error_not_a_default()
    {
        var json = Bundle().ToJson().Replace("\"isRelease\"", "\"isReleased\"");

        Should.Throw<JsonException>(() => DataBundle.Parse(json));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{ not json")]
    public void Text_that_is_not_a_bundle_is_a_JSON_error(string json)
    {
        Should.Throw<JsonException>(() => DataBundle.Parse(json));
    }

    [Fact]
    public void A_property_written_twice_is_an_error()
    {
        var json = Bundle().ToJson().Replace("\"isRelease\": false,", "\"isRelease\": false,\n  \"isRelease\": true,");

        Should.Throw<JsonException>(() => DataBundle.Parse(json));
    }

    [Fact]
    public void A_bundle_edited_by_hand_no_longer_has_the_right_version()
    {
        var edited = DataBundle.Parse(Bundle().ToJson().Replace("Thịt ba chỉ", "Thịt ba chỉ ngon"));

        edited.VersionMatchesContent().ShouldBeFalse();
    }
}
