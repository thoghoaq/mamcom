using System.Text;
using MamCom.Studio.Review;

namespace MamCom.Studio.Tests;

public class IngredientStoreTests
{
    private const string Id = "rau-muong";

    [Fact]
    public void Ids_are_the_ingredient_files_in_id_order_and_nothing_else()
    {
        using var data = new StudioData();
        var seeds = Directory.GetFiles(data.PathOf("ingredients"), "*.json").Length;
        data.WriteText("ingredients/Khong-hop-le.json", "{}");
        data.WriteText("ingredients/ghi-chu.txt", "note");
        data.WriteText("ingredients/dang-ghi.json.tmp", "{");

        var ids = data.Ingredients.Ids();

        ids.Count.ShouldBe(seeds);
        ids.ShouldBe([.. ids.Order(StringComparer.Ordinal)]);
        ids.ShouldContain(Id);
        ids.ShouldNotContain("Khong-hop-le");
        ids.ShouldNotContain("dang-ghi");
    }

    [Fact]
    public void Suggestion_ids_are_the_ids_FdcImporter_has_a_file_for()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (1, [1, 2, 3, 4, 5, 6]));
        data.WriteSuggestion("ca-rot", (2, [1, 2, 3, 4, 5, 6]));
        data.WriteText("_suggestions/nutrition/Sai-ten.json", "{}");

        data.Ingredients.SuggestionIds().ShouldBe([.. data.Ingredients.SuggestionIds().Order(StringComparer.Ordinal)]);
        data.Ingredients.SuggestionIds().ShouldContain("gao");
        data.Ingredients.SuggestionIds().ShouldContain("ca-rot");
        data.Ingredients.SuggestionIds().ShouldNotContain("Sai-ten");
    }

    [Fact]
    public void A_missing_folder_has_no_ids()
    {
        using var data = new StudioData();
        Directory.Delete(data.PathOf("_suggestions"), recursive: true);

        data.Ingredients.SuggestionIds().ShouldBeEmpty();
    }

    [Fact]
    public void Exists_is_false_for_an_id_that_cannot_be_an_ingredient()
    {
        using var data = new StudioData();

        data.Ingredients.Exists(Id).ShouldBeTrue();
        data.Ingredients.Exists("khong-co").ShouldBeFalse();
        data.Ingredients.Exists("../config/portion-defaults").ShouldBeFalse();
        data.Ingredients.Exists("").ShouldBeFalse();
    }

    // ---- Read ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void An_ingredient_is_read_with_its_text_and_a_version()
    {
        using var data = new StudioData();

        var stored = data.Ingredients.Read(Id)!;

        stored.Id.ShouldBe(Id);
        stored.File.ShouldBe("ingredients/rau-muong.json");
        stored.Text.ShouldBe(data.ReadText("ingredients/rau-muong.json"));
        stored.Ingredient!.Names["default"].ShouldBe("Rau muống");
        stored.Problem.ShouldBeNull();
        stored.Version.Length.ShouldBe(16);
    }

    [Fact]
    public void The_version_changes_with_any_change_to_the_text()
    {
        using var data = new StudioData();
        var first = data.Ingredients.Read(Id)!;

        data.Ingredients.Read(Id)!.Version.ShouldBe(first.Version);

        data.WriteText("ingredients/rau-muong.json", first.Text.Replace("Rau muống", "Rau muống "));
        data.Ingredients.Read(Id)!.Version.ShouldNotBe(first.Version);
    }

    [Fact]
    public void An_ingredient_that_is_not_there_or_has_no_valid_id_is_null()
    {
        using var data = new StudioData();

        data.Ingredients.Read("khong-co").ShouldBeNull();
        data.Ingredients.Read("../config/portion-defaults").ShouldBeNull();
    }

    [Fact]
    public void A_file_that_is_not_an_ingredient_is_still_read_with_the_reason()
    {
        using var data = new StudioData();
        data.WriteText("ingredients/hong.json", "{ not json");

        var stored = data.Ingredients.Read("hong")!;

        stored.Ingredient.ShouldBeNull();
        stored.Problem.ShouldNotBeNullOrWhiteSpace();
        stored.Text.ShouldBe("{ not json");
    }

    // ---- suggestions --------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_suggestion_is_read()
    {
        using var data = new StudioData();
        data.WriteSuggestion("gao", (111, [370, 7, 1, 80, 0.1, 0.4]), (222, [365, 7, 0.7, 80, null, 5]));

        var suggestion = data.Ingredients.ReadSuggestion("gao")!;

        suggestion.IngredientId.ShouldBe("gao");
        suggestion.Candidates.Select(c => c.FdcId).ShouldBe([111, 222]);
        suggestion.Candidates[1].Per100g.FiberG.ShouldBeNull();
        suggestion.Candidates[1].Missing.ShouldBe(["fiberG"]);
    }

    [Fact]
    public void A_missing_damaged_or_foreign_suggestion_is_null()
    {
        using var data = new StudioData();
        data.WriteText("_suggestions/nutrition/hong.json", "{ not json");
        data.WriteSuggestion("ca-rot", (1, [1, 2, 3, 4, 5, 6]));
        File.Move(data.PathOf("_suggestions/nutrition/ca-rot.json"), data.PathOf("_suggestions/nutrition/gao.json"), overwrite: true);   // the file of gao says it is about ca-rot

        data.Ingredients.ReadSuggestion("khong-co").ShouldBeNull();
        data.Ingredients.ReadSuggestion("hong").ShouldBeNull();
        data.Ingredients.ReadSuggestion("gao").ShouldBeNull();
        data.Ingredients.ReadSuggestion("../x").ShouldBeNull();
    }

    // ---- images -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_image_files_are_the_pictures_in_the_images_folder_in_name_order()
    {
        using var data = new StudioData();
        data.WriteImage("rau-muong-800.webp");
        data.WriteImage("rau-muong-200.webp");
        data.WriteImage("anh.PNG");
        data.WriteImage("anh2.jpg");
        data.WriteImage("anh3.jpeg");
        data.WriteImage("ghi-chu.txt");
        data.WriteImage(".an.webp");
        data.WriteImage("co khoang trang.webp");

        data.Ingredients.ImageFiles().ShouldBe(["anh.PNG", "anh2.jpg", "anh3.jpeg", "rau-muong-200.webp", "rau-muong-800.webp"]);
    }

    [Fact]
    public void Without_an_images_folder_there_are_no_image_files()
    {
        using var data = new StudioData();

        data.Ingredients.ImageFiles().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("rau-muong-200.webp", true)]
    [InlineData("a.png", true)]
    [InlineData("A_b-1.JPG", true)]
    [InlineData("a.jpeg", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("a.gif", false)]
    [InlineData("a", false)]
    [InlineData(".webp", false)]
    [InlineData("-a.webp", false)]
    [InlineData("../a.webp", false)]
    [InlineData("a/b.webp", false)]
    [InlineData("a\\b.webp", false)]
    [InlineData("a..b.webp", false)]
    [InlineData("a b.webp", false)]
    [InlineData("a.webp\n", false)]
    [InlineData("tiếng-việt.webp", false)]
    public void Only_a_plain_file_name_of_a_known_picture_type_is_an_image_name(string? name, bool valid)
    {
        IngredientStore.IsImageName(name).ShouldBe(valid);
    }

    [Fact]
    public void The_path_of_an_image_is_given_only_for_a_file_that_is_there()
    {
        using var data = new StudioData();
        data.WriteImage("rau-muong-200.webp");

        data.Ingredients.ImagePath("rau-muong-200.webp").ShouldBe(data.PathOf("images/out/rau-muong-200.webp"));
        data.Ingredients.ImagePath("khong-co.webp").ShouldBeNull();
        File.Exists(data.PathOf("config/portion-defaults.json")).ShouldBeTrue();   // two levels up from images/out is the data folder, where this file is
        data.Ingredients.ImagePath("../../config/portion-defaults.json").ShouldBeNull();
        data.Ingredients.ImagePath("..\\..\\config\\portion-defaults.json").ShouldBeNull();
        data.Ingredients.ImagePath("..\\..\\x.webp").ShouldBeNull();
    }

    // ---- Write --------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Write_replaces_the_file_without_leaving_a_temporary_file_or_a_byte_order_mark()
    {
        using var data = new StudioData();

        data.Ingredients.Write(Id, "tiếng Việt\n");

        var bytes = File.ReadAllBytes(data.PathOf("ingredients/rau-muong.json"));
        bytes.Take(3).ShouldNotBe(Encoding.UTF8.GetPreamble());
        Encoding.UTF8.GetString(bytes).ShouldBe("tiếng Việt\n");
        Directory.GetFiles(data.PathOf("ingredients"), "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void Write_refuses_an_id_that_could_leave_the_folder()
    {
        using var data = new StudioData();

        Should.Throw<ArgumentException>(() => data.Ingredients.Write("../config/portion-defaults", "{}"));
        Should.Throw<ArgumentNullException>(() => data.Ingredients.Write(Id, null!));
    }
}
