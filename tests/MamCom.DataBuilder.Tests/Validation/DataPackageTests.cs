using System.Text.Json;
using System.Text.Json.Nodes;
using MamCom.Core.Data;
using MamCom.DataBuilder.Bundling;
using MamCom.DataBuilder.Cli;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Tests.Validation;

/// <summary>The output of <c>build</c> is the whole data package the app loads: the bundle, a file naming its version, and the pictures it names (SPEC §7.6, ADR-020).</summary>
public class DataPackageTests
{
    private static string In(TempData data, params string[] parts) => Path.Combine([data.Out, .. parts]);

    private static void GiveAPicture(TempData data, string ingredient, string thumb, string full)
    {
        data.WriteImage(thumb);
        data.WriteImage(full);
        data.Edit("ingredients", ingredient, i => i["image"] = new JsonObject
        {
            ["thumb"] = thumb,
            ["full"] = full,
            ["source"] = "test",
            ["license"] = "CC0",
            ["isIllustration"] = false,
        });
    }

    // ---- the version file ----------------------------------------------------------------------------------------------------

    [Fact]
    public void The_version_file_names_the_version_and_kind_of_the_bundle_next_to_it()
    {
        using var data = new TempData();

        data.Run("build", "--data", data.Root, "--out", data.Out).Code.ShouldBe(CommandLine.Success);

        var bundle = DataBundle.Parse(File.ReadAllText(In(data, BundleFiles.Bundle)));
        var info = BundleVersionInfo.Parse(File.ReadAllText(In(data, BundleFiles.Version)));
        info.ShouldBe(new BundleVersionInfo(bundle.Version, IsRelease: false));
        info.Version.Length.ShouldBe(DataBundle.VersionLength);
    }

    [Fact]
    public void The_version_file_is_a_few_bytes_ending_with_a_line_feed_so_checking_for_news_costs_nothing()
    {
        using var data = new TempData();

        data.Run("build", "--data", data.Root, "--out", data.Out);

        var text = File.ReadAllText(In(data, BundleFiles.Version));
        text.Length.ShouldBeLessThan(80);
        text.ShouldEndWith("}\n");
        JsonNode.Parse(text)!.AsObject().Select(p => p.Key).ShouldBe(["version", "isRelease"]);
    }

    [Fact]
    public void The_version_changes_when_the_data_does_and_not_otherwise()
    {
        using var data = new TempData();
        data.Run("build", "--data", data.Root, "--out", data.Out);
        var first = File.ReadAllText(In(data, BundleFiles.Version));

        data.Run("build", "--data", data.Root, "--out", data.Out);
        File.ReadAllText(In(data, BundleFiles.Version)).ShouldBe(first);

        data.Edit("dishes", "rau-muong-luoc", d => d["name"] = "Rau muống luộc nước chanh");
        data.Run("build", "--data", data.Root, "--out", data.Out);
        File.ReadAllText(In(data, BundleFiles.Version)).ShouldNotBe(first);
    }

    [Fact]
    public void A_build_that_fails_leaves_no_version_file_behind_either()
    {
        using var data = new TempData();
        data.Run("build", "--data", data.Root, "--out", data.Out).Code.ShouldBe(CommandLine.Success);
        data.Edit("dishes", "rau-muong-luoc", d => d["ingredients"]![0]!["ingredientId"] = "khong-co");

        data.Run("build", "--data", data.Root, "--out", data.Out).Code.ShouldBe(CommandLine.DataInvalid);

        File.Exists(In(data, BundleFiles.Bundle)).ShouldBeFalse();
        File.Exists(In(data, BundleFiles.Version)).ShouldBeFalse();
    }

    [Fact]
    public void A_release_build_says_it_is_a_release_in_the_version_file()
    {
        using var data = new TempData();
        data.MakeReleasable();

        data.Run("build", "--release", "--data", data.Root, "--out", data.Out).Code.ShouldBe(CommandLine.Success);

        BundleVersionInfo.Parse(File.ReadAllText(In(data, BundleFiles.Version))).IsRelease.ShouldBeTrue();
    }

    // ---- the pictures --------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_pictures_the_bundle_names_are_copied_next_to_it()
    {
        using var data = new TempData();
        GiveAPicture(data, "gao", "gao-200.webp", "gao-800.webp");

        var run = data.Run("build", "--data", data.Root, "--out", data.Out);

        run.Code.ShouldBe(CommandLine.Success);
        Directory.GetFiles(In(data, "images")).Select(Path.GetFileName).Order(StringComparer.Ordinal).ShouldBe(["gao-200.webp", "gao-800.webp"]);
        File.ReadAllBytes(In(data, "images", "gao-200.webp")).ShouldBe([0]);
        run.Output.ShouldContain("2 ảnh trong images/");
    }

    [Fact]
    public void Without_a_picture_the_folder_is_there_and_empty_so_the_server_has_something_to_serve()
    {
        using var data = new TempData();

        var run = data.Run("build", "--data", data.Root, "--out", data.Out);

        Directory.Exists(In(data, "images")).ShouldBeTrue();
        Directory.GetFiles(In(data, "images")).ShouldBeEmpty();
        run.Output.ShouldContain("0 ảnh trong images/");
    }

    [Fact]
    public void A_picture_nothing_names_any_more_is_removed_from_the_package()
    {
        using var data = new TempData();
        GiveAPicture(data, "gao", "gao-200.webp", "gao-800.webp");
        data.Run("build", "--data", data.Root, "--out", data.Out);

        data.Edit("ingredients", "gao", i => i["image"] = new JsonObject
        {
            ["thumb"] = null,
            ["full"] = null,
            ["source"] = null,
            ["license"] = null,
            ["isIllustration"] = false,
        });
        data.Run("build", "--data", data.Root, "--out", data.Out);

        Directory.GetFiles(In(data, "images")).ShouldBeEmpty();
    }

    [Fact]
    public void A_picture_the_data_changed_is_copied_again_with_its_new_content()
    {
        using var data = new TempData();
        GiveAPicture(data, "gao", "gao-200.webp", "gao-800.webp");
        data.Run("build", "--data", data.Root, "--out", data.Out);

        File.WriteAllBytes(Path.Combine(data.Root, "images", "out", "gao-200.webp"), [1, 2, 3]);
        data.Run("build", "--data", data.Root, "--out", data.Out);

        File.ReadAllBytes(In(data, "images", "gao-200.webp")).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void A_picture_two_things_name_is_copied_once()
    {
        using var data = new TempData();
        GiveAPicture(data, "gao", "chung-200.webp", "chung-800.webp");
        data.Edit("ingredients", "bau", i => i["image"] = new JsonObject
        {
            ["thumb"] = "chung-200.webp",
            ["full"] = null,
            ["source"] = "test",
            ["license"] = "CC0",
            ["isIllustration"] = false,
        });

        data.Run("build", "--data", data.Root, "--out", data.Out).Code.ShouldBe(CommandLine.Success);

        Directory.GetFiles(In(data, "images")).Length.ShouldBe(2);
    }

    [Fact]
    public void The_names_the_bundle_gives_are_distinct_and_in_order_and_a_missing_one_is_skipped()
    {
        using var data = new TempData();
        GiveAPicture(data, "gao", "b-200.webp", "b-800.webp");
        GiveAPicture(data, "bau", "a-200.webp", "a-800.webp");
        data.Edit("ingredients", "ca-chua", i => i["image"] = new JsonObject
        {
            ["thumb"] = "a-200.webp",
            ["full"] = null,
            ["source"] = "test",
            ["license"] = "CC0",
            ["isIllustration"] = false,
        });

        var bundle = BundleBuilder.Build(data.Load(), ValidationMode.Dev);

        ImagePackager.NamedBy(bundle).ShouldBe(["a-200.webp", "a-800.webp", "b-200.webp", "b-800.webp"]);
    }

    [Fact]
    public void A_picture_that_is_not_in_the_images_folder_is_not_packaged_silently()
    {
        using var data = new TempData();
        GiveAPicture(data, "gao", "gao-200.webp", "gao-800.webp");
        var bundle = BundleBuilder.Build(data.Load(), ValidationMode.Dev);
        File.Delete(Path.Combine(data.Root, "images", "out", "gao-800.webp"));

        Should.Throw<FileNotFoundException>(() => ImagePackager.Copy(bundle, data.Root, data.Out));
    }

    [Fact]
    public void The_usage_names_the_three_parts_of_the_package()
    {
        var output = new StringWriter();
        CommandLine.Run(["help"], output, TextWriter.Null);

        output.ToString().ShouldContain("data-bundle.json");
        output.ToString().ShouldContain("data-version.json");
        output.ToString().ShouldContain("images/");
    }

    [Fact]
    public void The_names_of_the_package_are_the_ones_of_the_spec()
    {
        BundleFiles.Bundle.ShouldBe("data-bundle.json");
        BundleFiles.Version.ShouldBe("data-version.json");
        BundleFiles.ImagesFolder.ShouldBe("images");
        CommandLine.VersionFileName.ShouldBe(BundleFiles.Version);
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        Should.Throw<ArgumentNullException>(() => ImagePackager.NamedBy(null!));
        Should.Throw<ArgumentException>(() => ImagePackager.Copy(null!, string.Empty, "out"));
        Should.Throw<ArgumentException>(() => ImagePackager.Copy(null!, "data", string.Empty));
        Should.Throw<ArgumentNullException>(() => BundleVersionInfo.Of(null!));
        Should.Throw<JsonException>(() => BundleVersionInfo.Parse("null"));
        Should.Throw<JsonException>(() => BundleVersionInfo.Parse("""{ "version": " ", "isRelease": false }"""));
        Should.Throw<JsonException>(() => BundleVersionInfo.Parse("""{ "isRelease": false }"""));
    }
}
