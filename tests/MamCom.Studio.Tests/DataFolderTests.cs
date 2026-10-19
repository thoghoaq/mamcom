using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class DataFolderTests
{
    [Theory]
    [InlineData("ca-loc-kho-to")]
    [InlineData("com")]
    [InlineData("canh-bau-tom")]
    [InlineData("a1-b2")]
    public void A_kebab_case_ascii_id_is_valid(string id)
    {
        DataFolder.IsValidId(id).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Ca-loc")]
    [InlineData("ca_loc")]
    [InlineData("ca--loc")]
    [InlineData("-ca")]
    [InlineData("ca-")]
    [InlineData("../data")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a.b")]
    [InlineData("cá-lóc")]
    [InlineData("ca-loc\n")]
    public void Anything_that_could_leave_the_folder_or_is_not_kebab_case_is_not_a_valid_id(string? id)
    {
        DataFolder.IsValidId(id).ShouldBeFalse();
    }

    [Fact]
    public void Paths_follow_the_layout_of_the_data_folder()
    {
        using var data = new StudioData();

        data.Folder.DishPath(DishLocation.Data, "ca-kho").ShouldBe(Path.Combine(data.Root, "dishes", "ca-kho.json"));
        data.Folder.DishPath(DishLocation.Draft, "ca-kho").ShouldBe(Path.Combine(data.Root, "dishes", "_drafts", "ca-kho.json"));
        data.Folder.ReportPath("ca-kho").ShouldBe(Path.Combine(data.Root, "dishes", "_drafts", "ca-kho.report.json"));
        data.Folder.SchemaFolder.ShouldBe(Path.Combine(data.Root, "schemas"));
    }

    [Fact]
    public void The_file_name_the_validator_uses_for_a_dish_has_forward_slashes()
    {
        DataFolder.RelativeFile(DishLocation.Data, "ca-kho").ShouldBe("dishes/ca-kho.json");
        DataFolder.RelativeFile(DishLocation.Draft, "ca-kho").ShouldBe("dishes/_drafts/ca-kho.json");
    }

    [Fact]
    public void A_path_is_never_made_from_an_id_that_is_not_valid()
    {
        using var data = new StudioData();

        Should.Throw<ArgumentException>(() => data.Folder.DishPath(DishLocation.Data, "../../secrets"));
        Should.Throw<ArgumentException>(() => data.Folder.DishPath(DishLocation.Draft, "a/b"));
        Should.Throw<ArgumentException>(() => data.Folder.ReportPath("..\\x"));
    }

    [Fact]
    public void The_root_is_a_full_path()
    {
        new DataFolder(Path.Combine("some", "..", "data")).Root.ShouldBe(Path.GetFullPath("data"));
        Should.Throw<ArgumentException>(() => new DataFolder(" "));
    }

    // ---- Locate -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_configured_folder_is_used_as_it_is()
    {
        using var data = new StudioData();

        DataFolder.Locate(data.Root, Path.GetTempPath()).Root.ShouldBe(data.Root);
    }

    [Fact]
    public void A_relative_configured_folder_is_taken_from_the_start_folder()
    {
        using var data = new StudioData();
        var parent = Path.GetDirectoryName(data.Root)!;

        DataFolder.Locate(Path.GetFileName(data.Root), parent).Root.ShouldBe(data.Root);
    }

    [Fact]
    public void A_configured_folder_that_does_not_exist_is_an_error_not_a_search()
    {
        using var data = new StudioData();

        var error = Should.Throw<DirectoryNotFoundException>(() => DataFolder.Locate(Path.Combine(data.Root, "nope"), data.Root));

        error.Message.ShouldContain("nope");
    }

    [Fact]
    public void Without_a_configured_folder_the_nearest_data_folder_above_is_found()
    {
        using var data = new StudioData();
        var repo = Directory.CreateTempSubdirectory("mamcom-repo");
        try
        {
            var inner = Path.Combine(repo.FullName, "data");
            Directory.CreateDirectory(Path.Combine(inner, "schemas"));
            var deep = Directory.CreateDirectory(Path.Combine(repo.FullName, "tools", "MamCom.Studio", "bin")).FullName;

            DataFolder.Locate(null, deep).Root.ShouldBe(inner);
            DataFolder.Locate("  ", deep).Root.ShouldBe(inner);
        }
        finally
        {
            repo.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_folder_named_data_without_schemas_inside_is_not_the_data_folder()
    {
        var repo = Directory.CreateTempSubdirectory("mamcom-repo");
        try
        {
            Directory.CreateDirectory(Path.Combine(repo.FullName, "data", "notes"));
            var deep = Directory.CreateDirectory(Path.Combine(repo.FullName, "work")).FullName;

            var error = Should.Throw<DirectoryNotFoundException>(() => DataFolder.Locate(null, Path.Combine(deep, "data-not-here")));

            error.Message.ShouldContain("Studio:DataFolder");
        }
        finally
        {
            repo.Delete(recursive: true);
        }
    }

    [Fact]
    public void The_nearest_data_folder_wins_over_one_further_up()
    {
        var repo = Directory.CreateTempSubdirectory("mamcom-repo");
        try
        {
            Directory.CreateDirectory(Path.Combine(repo.FullName, "data", "schemas"));
            var nested = Path.Combine(repo.FullName, "sub");
            Directory.CreateDirectory(Path.Combine(nested, "data", "schemas"));

            DataFolder.Locate(null, nested).Root.ShouldBe(Path.Combine(nested, "data"));
        }
        finally
        {
            repo.Delete(recursive: true);
        }
    }

    [Fact]
    public void The_start_folder_is_required()
    {
        Should.Throw<ArgumentException>(() => DataFolder.Locate(null, " "));
    }
}
