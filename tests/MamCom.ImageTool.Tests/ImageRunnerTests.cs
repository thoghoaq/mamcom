using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Validation;
using MamCom.ImageTool.Running;

namespace MamCom.ImageTool.Tests;

public sealed class ImageRunnerTests : IDisposable
{
    private const string Source = "tự chụp, Lan 2026";
    private const string License = "CC0";

    private readonly ImageData _data = new();

    public void Dispose() => _data.Dispose();

    private static RunOptions Options(string? source = Source, string? license = License, bool? illustration = null, bool force = false, params string[] only) =>
        new(only.Length == 0 ? null : only, source, license, illustration, force);

    private RunResult Run(RunOptions? options = null, Action<Outcome>? progress = null) =>
        new ImageRunner(_data.Root).Run(options ?? Options(), progress);

    private Outcome RunOne(RunOptions? options = null)
    {
        var result = Run(options);
        result.Problem.ShouldBeNull();
        return result.Outcomes.ShouldHaveSingleItem();
    }

    private static string[] ChangedLines(string before, string after)
    {
        var a = before.Split('\n');
        var b = after.Split('\n');
        a.Length.ShouldBe(b.Length, "the data file must not gain or lose a line");
        return [.. a.Zip(b).Where(pair => pair.First != pair.Second).Select(pair => $"{pair.First.Trim()} => {pair.Second.Trim()}")];
    }

    // ---- one original, start to end --------------------------------------------------------------------------------------

    [Fact]
    public void An_ingredient_original_becomes_two_files_and_the_ingredient_names_them()
    {
        var before = _data.ReadText("ingredients/gao.json");
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(1000, 1000));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Written);
        outcome.Id.ShouldBe("gao");
        _data.OutFiles().ShouldBe(["gao-200.webp", "gao-800.webp"]);
        IngredientFile.Parse(_data.ReadText("ingredients/gao.json")).Image.ShouldBe(new ImageRef("gao-200.webp", "gao-800.webp", Source, License, false));
        ChangedLines(before, _data.ReadText("ingredients/gao.json")).Length.ShouldBe(4);
        _data.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void The_data_file_changes_only_the_picture_lines_and_is_written_the_way_studio_writes_it()
    {
        var before = _data.ReadText("ingredients/gao.json");
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(1000, 1000));

        RunOne();

        var after = _data.ReadText("ingredients/gao.json");
        ChangedLines(before, after).ShouldBe(
        [
            "\"thumb\": null, => \"thumb\": \"gao-200.webp\",",
            "\"full\": null, => \"full\": \"gao-800.webp\",",
            "\"source\": null, => \"source\": \"tự chụp, Lan 2026\",",
            "\"license\": null, => \"license\": \"CC0\",",
        ]);
        after.ShouldBe(IngredientFile.Serialize(IngredientFile.Parse(after)));
        after.ShouldEndWith("}\n");
        after.ShouldNotContain("\\u");   // Vietnamese is written as it is
    }

    [Fact]
    public void A_dish_original_becomes_two_files_and_the_dish_names_them()
    {
        var before = _data.ReadText("dishes/rau-muong-luoc.json");
        _data.AddOriginal("rau-muong-luoc.jpg", Pictures.Jpeg(1600, 1200));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Written);
        _data.OutFiles().ShouldBe(["rau-muong-luoc-200.webp", "rau-muong-luoc-800.webp"]);
        var after = _data.ReadText("dishes/rau-muong-luoc.json");
        DishFile.Parse(after).Image.ShouldBe(new ImageRef("rau-muong-luoc-200.webp", "rau-muong-luoc-800.webp", Source, License, false));
        ChangedLines(before, after).Length.ShouldBe(4);
        after.ShouldBe(DishFile.Serialize(DishFile.Parse(after)));
        outcome.Warnings.ShouldBeEmpty();
        _data.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void The_files_in_the_folder_are_the_sizes_of_the_spec_and_are_webp()
    {
        _data.AddOriginal("rau-muong-luoc.jpg", Pictures.Jpeg(1600, 1200));

        RunOne();

        using var thumb = Webp.Decode(File.ReadAllBytes(_data.PathOf("images/out/rau-muong-luoc-200.webp")));
        using var full = Webp.Decode(File.ReadAllBytes(_data.PathOf("images/out/rau-muong-luoc-800.webp")));
        (thumb.Width, thumb.Height).ShouldBe((200, 150));
        (full.Width, full.Height).ShouldBe((800, 600));
        Webp.Chunks(File.ReadAllBytes(_data.PathOf("images/out/rau-muong-luoc-200.webp"))).ShouldNotContain("EXIF");
    }

    [Fact]
    public void The_extension_may_be_in_capitals_and_a_png_works_too()
    {
        _data.AddOriginal("gao.JPG", Pictures.Jpeg(800, 800));
        _data.AddOriginal("bau.png", Pictures.Png(800, 800));

        var result = Run();

        result.Outcomes.Select(o => (o.Id, o.Kind)).ShouldBe([("bau", OutcomeKind.Written), ("gao", OutcomeKind.Written)]);
    }

    [Fact]
    public void The_validator_notices_when_a_picture_the_data_names_is_not_in_the_folder()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        RunOne();
        _data.Validate().ShouldBeEmpty();

        File.Delete(_data.PathOf("images/out/gao-800.webp"));

        _data.Validate().ShouldContain(d => d.Code == DiagnosticCodes.ImageMissing && d.File == "ingredients/gao.json" && d.Location == "/image/full");
    }

    [Theory]
    [InlineData("gao.jpeg")]
    [InlineData("gao.webp")]
    [InlineData("gao.WEBP")]
    public void The_other_kinds_of_original_are_taken_too(string name)
    {
        _data.AddOriginal(name, name.EndsWith("jpeg", StringComparison.Ordinal) ? Pictures.Jpeg(800, 800) : Pictures.WebpPicture(800, 800));

        RunOne().Kind.ShouldBe(OutcomeKind.Written);
        _data.OutFiles().ShouldBe(["gao-200.webp", "gao-800.webp"]);
    }

    [Fact]
    public void The_data_file_has_no_byte_order_mark()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        RunOne();

        File.ReadAllBytes(_data.PathOf("ingredients/gao.json")).Take(3).ToArray().ShouldNotBe([0xEF, 0xBB, 0xBF]);
        File.ReadAllBytes(_data.PathOf("ingredients/gao.json"))[0].ShouldBe((byte)'{');
    }

    [Fact]
    public void No_temporary_file_is_left_behind()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        RunOne();

        Directory.GetFiles(_data.Root, "*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    // ---- the source and the license -------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null, null)]
    [InlineData("tự chụp", null)]
    [InlineData(null, "CC0")]
    [InlineData("  ", "CC0")]
    [InlineData("tự chụp", "")]
    public void A_picture_without_a_source_or_a_license_is_refused_and_nothing_is_written(string? source, string? license)
    {
        var before = _data.ReadText("ingredients/gao.json");
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        var outcome = RunOne(Options(source, license));

        outcome.Kind.ShouldBe(OutcomeKind.Failed);
        outcome.Message.ShouldContain("--source");
        outcome.Message.ShouldContain("--license");
        _data.OutFiles().ShouldBeEmpty();
        _data.ReadText("ingredients/gao.json").ShouldBe(before);
    }

    [Fact]
    public void What_the_data_file_already_says_is_kept_when_the_run_gives_nothing()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        RunOne(Options("nguồn A", "giấy phép B", illustration: true));

        var outcome = RunOne(Options(null, null, force: true));

        outcome.Kind.ShouldBe(OutcomeKind.Written);
        IngredientFile.Parse(_data.ReadText("ingredients/gao.json")).Image.ShouldBe(new ImageRef("gao-200.webp", "gao-800.webp", "nguồn A", "giấy phép B", true));
    }

    [Fact]
    public void A_new_source_changes_only_the_data_file_and_leaves_the_pictures_alone()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        RunOne();
        var thumb = File.ReadAllBytes(_data.PathOf("images/out/gao-200.webp"));
        var thumbTime = File.GetLastWriteTimeUtc(_data.PathOf("images/out/gao-200.webp"));

        var outcome = RunOne(Options("nguồn mới", "CC-BY"));

        outcome.Kind.ShouldBe(OutcomeKind.JsonOnly);
        IngredientFile.Parse(_data.ReadText("ingredients/gao.json")).Image.ShouldBe(new ImageRef("gao-200.webp", "gao-800.webp", "nguồn mới", "CC-BY", false));
        File.ReadAllBytes(_data.PathOf("images/out/gao-200.webp")).ShouldBe(thumb);
        File.GetLastWriteTimeUtc(_data.PathOf("images/out/gao-200.webp")).ShouldBe(thumbTime);
    }

    [Fact]
    public void The_illustration_flag_is_set_cleared_and_otherwise_kept()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        RunOne(Options(illustration: true));
        IngredientFile.Parse(_data.ReadText("ingredients/gao.json")).Image.IsIllustration.ShouldBeTrue();

        RunOne(Options(illustration: null));   // says nothing: stays
        IngredientFile.Parse(_data.ReadText("ingredients/gao.json")).Image.IsIllustration.ShouldBeTrue();

        RunOne(Options(illustration: false));   // a real photo now
        IngredientFile.Parse(_data.ReadText("ingredients/gao.json")).Image.IsIllustration.ShouldBeFalse();
    }

    // ---- doing it again ----------------------------------------------------------------------------------------------------

    [Fact]
    public void A_second_run_has_nothing_to_do_and_touches_nothing()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        RunOne();
        var text = _data.ReadText("ingredients/gao.json");
        var time = File.GetLastWriteTimeUtc(_data.PathOf("ingredients/gao.json"));
        var thumbTime = File.GetLastWriteTimeUtc(_data.PathOf("images/out/gao-200.webp"));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.UpToDate);
        _data.ReadText("ingredients/gao.json").ShouldBe(text);
        File.GetLastWriteTimeUtc(_data.PathOf("ingredients/gao.json")).ShouldBe(time);
        File.GetLastWriteTimeUtc(_data.PathOf("images/out/gao-200.webp")).ShouldBe(thumbTime);
    }

    [Fact]
    public void An_original_that_was_replaced_makes_the_pictures_again()
    {
        var original = _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        RunOne();
        File.WriteAllBytes(_data.PathOf("images/out/gao-800.webp"), [1, 2, 3]);   // stands for the picture of the old photo
        File.SetLastWriteTimeUtc(original, DateTime.UtcNow.AddHours(1));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Written);
        File.ReadAllBytes(_data.PathOf("images/out/gao-800.webp")).Length.ShouldBeGreaterThan(3);
    }

    [Theory]
    [InlineData("images/out/gao-200.webp")]
    [InlineData("images/out/gao-800.webp")]
    public void One_picture_older_than_the_original_is_enough_to_make_them_again(string older)
    {
        var original = _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        RunOne();
        var now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(original, now);
        foreach (var name in new[] { "images/out/gao-200.webp", "images/out/gao-800.webp" })
        {
            File.WriteAllBytes(_data.PathOf(name), [1, 2, 3]);
            File.SetLastWriteTimeUtc(_data.PathOf(name), name == older ? now.AddHours(-1) : now.AddHours(1));
        }

        RunOne().Kind.ShouldBe(OutcomeKind.Written);
        new FileInfo(_data.PathOf("images/out/gao-200.webp")).Length.ShouldBeGreaterThan(3);
        new FileInfo(_data.PathOf("images/out/gao-800.webp")).Length.ShouldBeGreaterThan(3);
    }

    [Fact]
    public void Force_makes_the_pictures_again_even_when_they_are_new()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        RunOne();
        File.WriteAllBytes(_data.PathOf("images/out/gao-200.webp"), [1, 2, 3]);

        RunOne().Kind.ShouldBe(OutcomeKind.UpToDate);
        File.ReadAllBytes(_data.PathOf("images/out/gao-200.webp")).ShouldBe([1, 2, 3]);

        RunOne(Options(force: true)).Kind.ShouldBe(OutcomeKind.Written);
        File.ReadAllBytes(_data.PathOf("images/out/gao-200.webp")).Length.ShouldBeGreaterThan(3);
    }

    [Fact]
    public void A_missing_picture_is_made_again_even_if_the_other_is_there()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        RunOne();
        File.Delete(_data.PathOf("images/out/gao-800.webp"));

        RunOne().Kind.ShouldBe(OutcomeKind.Written);
        _data.OutFiles().ShouldBe(["gao-200.webp", "gao-800.webp"]);
    }

    // ---- an original that has no home ---------------------------------------------------------------------------------------

    [Fact]
    public void An_id_that_is_nothing_in_the_data_is_refused_and_nothing_is_written()
    {
        _data.AddOriginal("khong-co.jpg", Pictures.Jpeg(800, 800));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Failed);
        outcome.Message.ShouldContain("không có nguyên liệu hay món nào mã 'khong-co'");
        _data.OutFiles().ShouldBeEmpty();
    }

    [Fact]
    public void A_dish_that_is_still_a_draft_says_so()
    {
        Directory.CreateDirectory(_data.PathOf("dishes/_drafts"));
        File.Move(_data.PathOf("dishes/rau-muong-luoc.json"), _data.PathOf("dishes/_drafts/rau-muong-luoc.json"));
        _data.AddOriginal("rau-muong-luoc.jpg", Pictures.Jpeg(1600, 1200));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Failed);
        outcome.Message.ShouldContain("bản nháp");
        _data.OutFiles().ShouldBeEmpty();
    }

    [Fact]
    public void An_id_that_is_both_an_ingredient_and_a_dish_is_refused()
    {
        File.Copy(_data.PathOf("dishes/rau-muong-luoc.json"), _data.PathOf("dishes/gao.json"));
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Failed);
        outcome.Message.ShouldContain("vừa là mã nguyên liệu vừa là mã món");
        _data.OutFiles().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Gao.jpg")]
    [InlineData("rau muong.jpg")]
    [InlineData("-gao.jpg")]
    [InlineData("gao-.jpg")]
    [InlineData("gạo.jpg")]
    public void A_name_that_cannot_be_an_id_is_refused(string name)
    {
        _data.AddOriginal(name, Pictures.Jpeg(800, 800));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Failed);
        outcome.Message.ShouldContain("mã của nguyên liệu hoặc món");
        _data.OutFiles().ShouldBeEmpty();
    }

    [Fact]
    public void Two_originals_with_the_same_id_are_refused_and_neither_is_used()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        _data.AddOriginal("gao.png", Pictures.Png(800, 800));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Failed);
        outcome.Message.ShouldContain("gao.jpg");
        outcome.Message.ShouldContain("gao.png");
        _data.OutFiles().ShouldBeEmpty();
    }

    [Fact]
    public void Files_that_are_not_pictures_of_a_known_kind_are_listed_and_a_dot_file_is_not()
    {
        _data.AddOriginal("ghi-chu.txt", [1]);
        _data.AddOriginal("iphone.heic", [1]);
        _data.AddOriginal(".gitkeep", []);

        var result = Run();

        result.Problem.ShouldBeNull();
        result.Outcomes.ShouldBeEmpty();
        result.Ignored.Count.ShouldBe(2);
        result.Ignored.ShouldContain(i => i.StartsWith("ghi-chu.txt", StringComparison.Ordinal) && i.Contains("chưa hỗ trợ", StringComparison.Ordinal));
        result.Ignored.ShouldContain(i => i.StartsWith("iphone.heic", StringComparison.Ordinal) && i.Contains("JPEG", StringComparison.Ordinal));
    }

    // ---- an original that is not a good one ---------------------------------------------------------------------------------

    [Fact]
    public void A_file_that_is_not_a_picture_fails_alone_and_the_others_go_on()
    {
        var before = _data.ReadText("ingredients/gao.json");
        _data.AddOriginal("gao.jpg", [1, 2, 3, 4]);
        _data.AddOriginal("bau.jpg", Pictures.Jpeg(800, 800));

        var result = Run();

        result.Outcomes.Select(o => (o.Id, o.Kind)).ShouldBe([("bau", OutcomeKind.Written), ("gao", OutcomeKind.Failed)]);
        result.Outcomes[1].Message.ShouldContain("không đọc được ảnh");
        _data.OutFiles().ShouldBe(["bau-200.webp", "bau-800.webp"]);
        _data.ReadText("ingredients/gao.json").ShouldBe(before);
    }

    [Fact]
    public void A_file_that_cannot_be_written_fails_alone_and_leaves_no_temporary_file()
    {
        var before = _data.ReadText("ingredients/gao.json");
        Directory.CreateDirectory(_data.PathOf("images/out/gao-200.webp"));   // a folder where the file should go
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        _data.AddOriginal("bau.jpg", Pictures.Jpeg(800, 800));

        var result = Run();

        result.Outcomes.Select(o => (o.Id, o.Kind)).ShouldBe([("bau", OutcomeKind.Written), ("gao", OutcomeKind.Failed)]);
        result.Outcomes[1].Message.ShouldStartWith("gao: không ghi được file");
        _data.ReadText("ingredients/gao.json").ShouldBe(before);
        Directory.GetFiles(_data.Root, "*.tmp", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public void A_data_file_that_cannot_be_read_fails_alone()
    {
        _data.WriteText("ingredients/gao.json", "{ not json");
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Failed);
        outcome.Message.ShouldContain("ingredients/gao.json");
        outcome.Message.ShouldContain("hỏng");
        _data.OutFiles().ShouldBeEmpty();
    }

    [Fact]
    public void When_the_checker_finds_an_error_after_the_picture_is_attached_the_data_file_is_left_alone()
    {
        _data.EditJson("dishes/rau-muong-luoc.json", d => d["seasons"] = new System.Text.Json.Nodes.JsonArray("monsoon"));
        var before = _data.ReadText("dishes/rau-muong-luoc.json");
        _data.AddOriginal("rau-muong-luoc.jpg", Pictures.Jpeg(1600, 1200));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Failed);
        outcome.Message.ShouldStartWith("dishes/rau-muong-luoc.json: ");
        outcome.Message.ShouldContain("máy kiểm còn lỗi");
        _data.ReadText("dishes/rau-muong-luoc.json").ShouldBe(before);
    }

    // ---- which originals ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Only_the_ids_asked_for_are_done()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        _data.AddOriginal("bau.jpg", Pictures.Jpeg(800, 800));

        var result = Run(Options(only: "gao"));

        result.Outcomes.Select(o => o.Id).ShouldBe(["gao"]);
        _data.OutFiles().ShouldBe(["gao-200.webp", "gao-800.webp"]);
    }

    [Fact]
    public void The_ids_asked_for_are_exact_so_a_name_in_capitals_is_no_original()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        var result = Run(Options(only: "GAO"));

        result.Outcomes.ShouldHaveSingleItem().Kind.ShouldBe(OutcomeKind.Failed);
        _data.OutFiles().ShouldBeEmpty();
    }

    [Fact]
    public void An_id_asked_for_that_has_no_original_is_an_error()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        var result = Run(Options(only: ["gao", "bau"]));

        result.Outcomes.Select(o => (o.Id, o.Kind)).ShouldBe([("bau", OutcomeKind.Failed), ("gao", OutcomeKind.Written)]);
        result.Outcomes[0].Message.ShouldContain("không có ảnh gốc tên 'bau'");
    }

    [Fact]
    public void The_originals_are_done_in_the_order_of_their_ids_and_each_is_reported_when_done()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        _data.AddOriginal("bau.jpg", Pictures.Jpeg(800, 800));
        _data.AddOriginal("ca-chua.jpg", Pictures.Jpeg(800, 800));
        var seen = new List<string>();

        var result = Run(progress: o => seen.Add(o.Id));

        seen.ShouldBe(["bau", "ca-chua", "gao"]);
        result.Outcomes.Select(o => o.Id).ShouldBe(seen);
    }

    // ---- what is the matter with the whole run -----------------------------------------------------------------------------

    [Fact]
    public void Without_a_folder_of_originals_the_run_says_where_to_make_it()
    {
        var result = Run();

        result.Problem.ShouldNotBeNull();
        result.Problem.ShouldContain(Path.Combine("images", "raw"));
        result.Outcomes.ShouldBeEmpty();
    }

    [Fact]
    public void An_empty_folder_of_originals_is_a_run_that_does_nothing()
    {
        Directory.CreateDirectory(_data.PathOf("images/raw"));

        var result = Run();

        result.Problem.ShouldBeNull();
        result.Outcomes.ShouldBeEmpty();
        result.Ignored.ShouldBeEmpty();
    }

    [Fact]
    public void Data_that_cannot_be_used_stops_the_run_before_anything_is_written()
    {
        File.Delete(_data.PathOf("schemas/dish.schema.json"));
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        var result = Run();

        result.Problem.ShouldNotBeNull();
        result.Problem.ShouldContain("DataBuilder");
        _data.OutFiles().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("validation-defaults")]
    [InlineData("portion-defaults")]
    public void A_missing_config_stops_the_run_too(string config)
    {
        File.Delete(_data.PathOf($"config/{config}.json"));
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        Run().Problem.ShouldNotBeNull();
        _data.OutFiles().ShouldBeEmpty();
    }

    // ---- what is worth a look ------------------------------------------------------------------------------------------------

    [Fact]
    public void A_four_by_three_original_with_a_long_side_over_the_big_size_has_nothing_to_say()
    {
        _data.AddOriginal("rau-muong-luoc.jpg", Pictures.Jpeg(1000, 750));   // the short side is under 800, the long side is not

        RunOne().Warnings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(800, false)]
    [InlineData(801, false)]
    [InlineData(799, true)]
    public void An_original_is_too_small_only_below_the_big_size(int side, bool warns)
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(side, side));

        RunOne().Warnings.Any(w => w.Contains("dưới 800px", StringComparison.Ordinal)).ShouldBe(warns);
    }

    [Fact]
    public void An_original_smaller_than_the_big_size_is_a_warning_not_an_error()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(300, 300));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Written);
        outcome.Warnings.ShouldHaveSingleItem().ShouldContain("300×300");
    }

    [Theory]
    [InlineData("gao", 1600, 1200, true)]    // an ingredient is square
    [InlineData("gao", 1000, 1000, false)]
    [InlineData("gao", 1000, 1020, false)]   // two per cent off is the photographer's hand
    [InlineData("gao", 1000, 1100, true)]
    [InlineData("rau-muong-luoc", 1000, 1000, true)]   // a dish is four to three
    [InlineData("rau-muong-luoc", 1600, 1200, false)]
    [InlineData("rau-muong-luoc", 1200, 1600, false)]   // held the other way
    [InlineData("rau-muong-luoc", 1600, 1000, true)]
    public void A_shape_other_than_the_one_asked_for_is_a_warning(string id, int width, int height, bool warns)
    {
        _data.AddOriginal(id + ".jpg", Pictures.Jpeg(width, height));

        var outcome = RunOne();

        outcome.Kind.ShouldBe(OutcomeKind.Written);
        outcome.Warnings.Any(w => w.Contains("nên", StringComparison.Ordinal)).ShouldBe(warns);
    }

    [Fact]
    public void The_shape_is_judged_after_the_photo_is_turned_upright()
    {
        // 1200 × 1600 stored on its side is 1600 × 1200 for the eye: four to three, nothing to say.
        _data.AddOriginal("rau-muong-luoc.jpg", Pictures.Jpeg(1200, 1600, orientation: 6, description: "x"));

        RunOne().Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void The_argument_is_checked()
    {
        Should.Throw<ArgumentNullException>(() => new ImageRunner(_data.Root).Run(null!));
    }
}
