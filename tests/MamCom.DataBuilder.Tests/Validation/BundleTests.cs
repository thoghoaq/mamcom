using System.Text.Json.Nodes;
using MamCom.Core.Data;
using MamCom.DataBuilder.Bundling;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Tests.Validation;

public class BundleTests
{
    private static DataBundle Build(TempData data, ValidationMode mode = ValidationMode.Dev) => BundleBuilder.Build(data.Load(), mode);

    [Fact]
    public void The_bundle_has_every_ingredient_and_dish_in_id_order()
    {
        using var data = new TempData();

        var bundle = Build(data);

        bundle.Ingredients.Count.ShouldBe(33);
        bundle.Dishes.Count.ShouldBe(20);
        bundle.Ingredients.Select(i => i.Id).ShouldBe(bundle.Ingredients.Select(i => i.Id).Order(StringComparer.Ordinal));
        bundle.Dishes.Select(d => d.Id).ShouldBe(bundle.Dishes.Select(d => d.Id).Order(StringComparer.Ordinal));
        bundle.Config.Portion.RiceIngredientId.ShouldBe("gao");
        bundle.Config.Cooking.RiceActiveMinutes.ShouldBe(5);
    }

    [Fact]
    public void A_development_bundle_keeps_drafts_and_says_it_is_not_a_release()
    {
        using var data = new TempData();

        var bundle = Build(data);

        bundle.IsRelease.ShouldBeFalse();
        bundle.Dishes.ShouldAllBe(d => d.Status == MamCom.Core.Models.DishStatus.Draft);
    }

    [Fact]
    public void A_release_bundle_says_so()
    {
        using var data = new TempData();
        data.MakeReleasable();

        var bundle = Build(data, ValidationMode.Release);

        bundle.IsRelease.ShouldBeTrue();
        bundle.Dishes.ShouldAllBe(d => d.Status >= MamCom.Core.Models.DishStatus.Reviewed);
    }

    [Fact]
    public void The_version_is_the_hash_of_the_content_and_is_stable_between_builds()
    {
        using var data = new TempData();

        var first = Build(data);
        var second = Build(data);

        first.Version.ShouldMatch("^[0-9a-f]{16}$");
        second.Version.ShouldBe(first.Version);
        first.VersionMatchesContent().ShouldBeTrue();
        second.ToJson().ShouldBe(first.ToJson());
    }

    [Fact]
    public void The_version_does_not_depend_on_the_order_the_files_are_read_in()
    {
        using var data = new TempData();
        var loaded = data.Load();
        var shuffled = loaded with
        {
            Ingredients = [.. loaded.Ingredients.Reverse()],
            Dishes = [.. loaded.Dishes.OrderByDescending(f => f.File, StringComparer.Ordinal)],
        };

        BundleBuilder.Build(shuffled, ValidationMode.Dev).ToJson().ShouldBe(BundleBuilder.Build(loaded, ValidationMode.Dev).ToJson());
    }

    [Fact]
    public void The_version_changes_when_a_recipe_a_unit_a_config_or_the_mode_changes()
    {
        using var data = new TempData();
        var original = Build(data).Version;

        data.Edit("dishes", "canh-bau-tom", o => o["ingredients"]![0]!["gramsEdiblePerServing"] = 101);
        var recipe = Build(data).Version;
        data.Edit("ingredients", "bau", o => o["wasteRatio"] = 0.25);
        var unit = Build(data).Version;
        data.Edit("config", "spinner-defaults", o => o["historySize"] = 6);
        var config = Build(data).Version;

        new[] { original, recipe, unit, config }.ShouldBeUnique();
        data.MakeReleasable();
        var releasable = Build(data, ValidationMode.Release).Version;
        Build(data, ValidationMode.Dev).Version.ShouldNotBe(releasable);
    }

    [Fact]
    public void The_version_does_not_change_when_only_the_file_layout_changes()
    {
        using var data = new TempData();
        var original = Build(data).Version;

        // Same content, different spacing and key order inside the file, as an editor might leave it.
        data.Edit("ingredients", "ca-loc", _ => { });
        File.WriteAllText(data.PathOf("dishes", "canh-bau-tom"), File.ReadAllText(data.PathOf("dishes", "canh-bau-tom")).Replace("\n", "\r\n"));

        Build(data).Version.ShouldBe(original);
    }

    [Fact]
    public void The_written_bundle_reads_back_with_the_core_and_matches_its_own_version()
    {
        using var data = new TempData();
        var (code, _, _) = data.Run("build", "--data", data.Root, "--out", data.Out);

        code.ShouldBe(0);
        var text = File.ReadAllText(Path.Combine(data.Out, "data-bundle.json"));
        var bundle = DataBundle.Parse(text);
        bundle.VersionMatchesContent().ShouldBeTrue();
        bundle.ToJson().ShouldBe(text);
        bundle.Dishes.Count.ShouldBe(20);
    }

    [Fact]
    public void Building_without_a_usable_config_is_refused()
    {
        using var data = new TempData();
        data.Delete("config", "cooking-defaults");

        Should.Throw<InvalidOperationException>(() => Build(data));
    }

    [Fact]
    public void Dishes_in_the_drafts_folder_never_reach_the_bundle()
    {
        using var data = new TempData();
        data.CopyFile("dishes", "canh-bau-tom", "_drafts/canh-nhap", o => o["id"] = "canh-nhap");

        Build(data).Dishes.ShouldNotContain(d => d.Id == "canh-nhap");
    }

    [Fact]
    public void An_edit_that_changes_a_dish_shows_up_in_the_bundle()
    {
        using var data = new TempData();
        data.Edit("dishes", "canh-bau-tom", o => o["name"] = "Canh bầu tôm khô");

        Build(data).Dishes.Single(d => d.Id == "canh-bau-tom").Name.ShouldBe("Canh bầu tôm khô");
    }

    [Fact]
    public void Loading_twice_gives_the_same_loaded_content()
    {
        using var data = new TempData();

        var first = data.Load();
        var second = DataLoader.Load(data.Root);

        first.Ingredients.Select(f => f.File).ShouldBe(second.Ingredients.Select(f => f.File));
        JsonNode.DeepEquals(JsonNode.Parse(BundleBuilder.Build(first, ValidationMode.Dev).ToJson()), JsonNode.Parse(BundleBuilder.Build(second, ValidationMode.Dev).ToJson())).ShouldBeTrue();
    }
}
