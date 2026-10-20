using System.Text;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.ImageTool.Cli;

namespace MamCom.ImageTool.Tests;

public sealed class CommandLineTests : IDisposable
{
    private readonly ImageData _data = new();

    public void Dispose() => _data.Dispose();

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = CommandLine.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private (int Code, string Output, string Error) Process(params string[] more) =>
        Run(["process", "--data", _data.Root, .. more]);

    // ---- the command line itself ------------------------------------------------------------------------------------------

    [Fact]
    public void Without_a_command_the_usage_is_shown_as_an_error()
    {
        var (code, output, error) = Run();

        code.ShouldBe(CommandLine.UsageError);
        output.ShouldBeEmpty();
        error.ShouldContain("MamCom.ImageTool process");
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_shows_the_usage_and_succeeds(string word)
    {
        var (code, output, error) = Run(word);

        code.ShouldBe(CommandLine.Success);
        error.ShouldBeEmpty();
        output.ShouldContain("MamCom.ImageTool process");
    }

    [Fact]
    public void The_usage_names_every_option_and_both_file_names()
    {
        var (_, output, _) = Run("help");

        foreach (var option in new[] { "--data", "--only", "--source", "--license", "--illustration", "--photo", "--force", "<id>-200.webp", "<id>-800.webp", "EXIF" })
        {
            output.ShouldContain(option);
        }
    }

    [Fact]
    public void An_unknown_command_is_a_usage_error()
    {
        var (code, _, error) = Run("resize");

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain("Lệnh 'resize' không có.");
    }

    [Theory]
    [InlineData("--size")]
    [InlineData("--thumb")]
    public void An_unknown_option_is_a_usage_error(string option)
    {
        var (code, _, error) = Run("process", option, "100");

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain($"Tuỳ chọn '{option}' không có.");
    }

    [Theory]
    [InlineData("--data")]
    [InlineData("--only")]
    [InlineData("--source")]
    [InlineData("--license")]
    public void An_option_without_a_value_is_a_usage_error(string option)
    {
        var (code, _, error) = Run("process", option);

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain($"Tuỳ chọn '{option}' cần một giá trị.");
    }

    [Theory]
    [InlineData("--source")]
    [InlineData("--license")]
    [InlineData("--data")]
    public void A_blank_value_is_no_value(string option)
    {
        Run("process", option, "   ").Code.ShouldBe(CommandLine.UsageError);
    }

    [Fact]
    public void Illustration_and_photo_together_are_a_usage_error_but_the_same_one_twice_is_fine()
    {
        var (code, _, error) = Run("process", "--illustration", "--photo");
        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain("không đi cùng nhau");

        Run("process", "--data", Path.Combine(_data.Root, "nope"), "--photo", "--photo").Code.ShouldBe(CommandLine.Failure);
    }

    [Fact]
    public void An_empty_list_of_ids_is_a_usage_error()
    {
        var (code, _, error) = Run("process", "--only", ",");

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain("--only");
    }

    // ---- a run -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_good_run_says_what_it_did_for_each_picture_and_succeeds()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(1000, 1000));
        _data.AddOriginal("rau-muong-luoc.jpg", Pictures.Jpeg(1600, 1200));

        var (code, output, error) = Process("--source", "tự chụp", "--license", "CC0");

        code.ShouldBe(CommandLine.Success);
        error.ShouldBeEmpty();
        output.ShouldContain("gao: đã tạo gao-200.webp (200×200,");
        output.ShouldContain("rau-muong-luoc-800.webp (800×600,");
        output.ShouldContain("Xong: 2 ảnh đã tạo, 0 chỉ sửa file dữ liệu, 0 bỏ qua vì đã mới, 0 lỗi.");
        _data.OutFiles().ShouldBe(["gao-200.webp", "gao-800.webp", "rau-muong-luoc-200.webp", "rau-muong-luoc-800.webp"]);
        _data.Validate().ShouldBeEmpty();
    }

    [Fact]
    public void A_run_that_finds_nothing_new_succeeds_and_says_so()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        Process("--source", "tự chụp", "--license", "CC0").Code.ShouldBe(CommandLine.Success);

        var (code, output, _) = Process();

        code.ShouldBe(CommandLine.Success);
        output.ShouldContain("gao: ảnh đã mới, bỏ qua.");
        output.ShouldContain("1 bỏ qua vì đã mới");
    }

    [Fact]
    public void A_failed_picture_is_an_error_line_and_exit_code_one_but_the_summary_still_comes()
    {
        _data.AddOriginal("khong-co.jpg", Pictures.Jpeg(800, 800));
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));

        var (code, output, error) = Process("--source", "tự chụp", "--license", "CC0");

        code.ShouldBe(CommandLine.Failure);
        error.ShouldContain("khong-co: không có nguyên liệu hay món nào mã");
        output.ShouldContain("gao: đã tạo");
        output.ShouldContain("1 ảnh đã tạo, 0 chỉ sửa file dữ liệu, 0 bỏ qua vì đã mới, 1 lỗi.");
    }

    [Fact]
    public void Without_a_folder_of_originals_it_fails_with_the_reason_and_no_summary()
    {
        var (code, output, error) = Process();

        code.ShouldBe(CommandLine.Failure);
        error.ShouldContain("Không thấy thư mục ảnh gốc");
        output.ShouldBeEmpty();
    }

    [Fact]
    public void Files_that_are_ignored_are_listed()
    {
        _data.AddOriginal("iphone.heic", [1]);

        var (code, output, _) = Process();

        code.ShouldBe(CommandLine.Success);
        output.ShouldContain("iphone.heic: ảnh HEIC");
    }

    [Fact]
    public void A_warning_is_printed_under_the_picture_it_is_about()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(300, 200));

        var (code, output, _) = Process("--source", "tự chụp", "--license", "CC0");

        code.ShouldBe(CommandLine.Success);
        output.ShouldContain("Lưu ý, gao: ảnh gốc 300×200");
        output.ShouldContain("Lưu ý, gao: ảnh nguyên liệu nên vuông 1:1");
    }

    [Fact]
    public void Every_option_reaches_the_run()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        _data.AddOriginal("bau.jpg", Pictures.Jpeg(800, 800));

        Process("--only", "gao", "--source", " nguồn ", "--license", " giấy phép ", "--illustration").Code.ShouldBe(CommandLine.Success);

        _data.OutFiles().ShouldBe(["gao-200.webp", "gao-800.webp"]);
        IngredientFile.Parse(_data.ReadText("ingredients/gao.json")).Image.ShouldBe(new ImageRef("gao-200.webp", "gao-800.webp", "nguồn", "giấy phép", true));

        File.WriteAllBytes(_data.PathOf("images/out/gao-200.webp"), [1]);
        Process("--only", "gao", "--photo", "--force").Code.ShouldBe(CommandLine.Success);
        IngredientFile.Parse(_data.ReadText("ingredients/gao.json")).Image.IsIllustration.ShouldBeFalse();
        new FileInfo(_data.PathOf("images/out/gao-200.webp")).Length.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void The_ids_after_only_may_have_spaces_around_them()
    {
        _data.AddOriginal("gao.jpg", Pictures.Jpeg(800, 800));
        _data.AddOriginal("bau.jpg", Pictures.Jpeg(800, 800));

        var (code, _, _) = Process("--only", "gao, bau ", "--source", "tự chụp", "--license", "CC0");

        code.ShouldBe(CommandLine.Success);
        _data.OutFiles().ShouldBe(["bau-200.webp", "bau-800.webp", "gao-200.webp", "gao-800.webp"]);
    }

    [Fact]
    public void The_default_data_folder_is_data_next_to_where_it_runs()
    {
        // The tests run where no data/images/raw exists: the run fails there, naming the default path.
        var (code, _, error) = Run("process");

        code.ShouldBe(CommandLine.Failure);
        error.ShouldContain(Path.Combine("data", "images", "raw"));
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        Should.Throw<ArgumentNullException>(() => CommandLine.Run(null!, TextWriter.Null, TextWriter.Null));
        Should.Throw<ArgumentNullException>(() => CommandLine.Run([], null!, TextWriter.Null));
        Should.Throw<ArgumentNullException>(() => CommandLine.Run([], TextWriter.Null, null!));
    }

    // ---- the whole way, as the owner will use it --------------------------------------------------------------------------

    [Fact]
    public void A_phone_photo_with_a_place_in_it_ends_as_two_clean_files_and_a_valid_data_folder()
    {
        const string Place = "10.7769N 106.7009E";
        Directory.CreateDirectory(_data.PathOf("dishes/_drafts"));
        File.Move(_data.PathOf("dishes/rau-muong-luoc.json"), _data.PathOf("dishes/_drafts/rau-muong-luoc.json"));
        _data.AddOriginal("rau-muong-luoc.jpg", Pictures.Jpeg(1600, 1200, orientation: 1, description: Place));
        _data.AddOriginal("ca-chua.jpg", Pictures.Jpeg(1200, 1200, orientation: 6, description: Place));
        var dishBefore = _data.ReadText("dishes/_drafts/rau-muong-luoc.json");

        var (code, output, _) = Process("--source", "tự chụp, Lan", "--license", "CC0");

        // The dish is a draft, so it is refused; the ingredient goes through.
        code.ShouldBe(CommandLine.Failure);
        output.ShouldContain("ca-chua: đã tạo");
        _data.ReadText("dishes/_drafts/rau-muong-luoc.json").ShouldBe(dishBefore);
        foreach (var name in _data.OutFiles())
        {
            var bytes = File.ReadAllBytes(_data.PathOf($"images/out/{name}"));
            Webp.Chunks(bytes).ShouldNotContain("EXIF");
            Encoding.ASCII.GetString(bytes).ShouldNotContain(Place);
        }

        _data.Validate().ShouldBeEmpty();
    }
}
