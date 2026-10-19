using MamCom.Core.Data;
using MamCom.DataBuilder.Cli;

namespace MamCom.DataBuilder.Tests.Validation;

public class CommandLineTests
{
    private static string BundlePath(TempData data) => Path.Combine(data.Out, CommandLine.BundleFileName);

    private static string ReportPath(TempData data) => Path.Combine(data.Out, CommandLine.ReportFileName);

    // ---- usage -------------------------------------------------------------------------------------------------------

    [Fact]
    public void No_arguments_print_the_usage_and_fail_with_the_usage_code()
    {
        using var data = new TempData();

        var (code, output, error) = data.Run();

        code.ShouldBe(CommandLine.UsageError);
        output.ShouldBeEmpty();
        error.ShouldContain("Cách dùng:");
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Asking_for_help_prints_the_usage_and_succeeds(string flag)
    {
        using var data = new TempData();

        var (code, output, error) = data.Run(flag);

        code.ShouldBe(CommandLine.Success);
        output.ShouldContain("validate");
        output.ShouldContain("build");
        error.ShouldBeEmpty();
    }

    [Fact]
    public void An_unknown_command_is_a_usage_error()
    {
        using var data = new TempData();

        var (code, _, error) = data.Run("publish");

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain("'publish'");
    }

    [Theory]
    [InlineData("validate", "--fast")]
    [InlineData("build", "--fast")]
    [InlineData("validate", "--out")]
    [InlineData("validate", "--data")]
    [InlineData("build", "--out")]
    [InlineData("build", "--data")]
    public void A_wrong_option_is_a_usage_error_and_nothing_is_built(string command, string option)
    {
        using var data = new TempData();

        var (code, _, error) = data.Run(command, option);

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain(option);
        Directory.Exists(data.Out).ShouldBeFalse();
    }

    [Fact]
    public void The_out_option_is_only_for_build()
    {
        using var data = new TempData();

        var (code, _, error) = data.Run("validate", "--data", data.Root, "--out", data.Out);

        code.ShouldBe(CommandLine.UsageError);
        error.ShouldContain("build");
    }

    [Fact]
    public void The_defaults_are_the_data_folder_and_the_dist_folder_and_the_usage_says_so()
    {
        CommandLine.DefaultDataFolder.ShouldBe("data");
        CommandLine.DefaultOutFolder.ShouldBe("dist");
        CommandLine.BundleFileName.ShouldBe("data-bundle.json");
        CommandLine.ReportFileName.ShouldBe("data-report.md");

        using var data = new TempData();
        var usage = data.Run("--help").Output;
        usage.ShouldContain("\"data\"");
        usage.ShouldContain("\"dist\"");
    }

    // ---- validate ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Validating_the_seed_succeeds_and_says_how_much_it_checked()
    {
        using var data = new TempData();

        var (code, output, error) = data.Run("validate", "--data", data.Root);

        code.ShouldBe(CommandLine.Success);
        output.ShouldContain("Đã kiểm 33 nguyên liệu và 20 món (dev): 0 lỗi, 0 cảnh báo.");
        output.ShouldContain("Dữ liệu hợp lệ.");
        error.ShouldBeEmpty();
    }

    [Fact]
    public void Validating_writes_nothing()
    {
        using var data = new TempData();

        data.Run("validate", "--data", data.Root);

        Directory.Exists(data.Out).ShouldBeFalse();
        Directory.Exists(Path.Combine(data.Root, "dist")).ShouldBeFalse();
    }

    [Fact]
    public void Errors_go_to_the_error_stream_one_per_line_with_file_place_and_code_and_the_exit_code_is_one()
    {
        using var data = new TempData();
        data.Edit("ingredients", "ca-loc", o => o["wasteRatio"] = 1);

        var (code, output, error) = data.Run("validate", "--data", data.Root);

        code.ShouldBe(CommandLine.DataInvalid);
        error.ShouldContain("error: ingredients/ca-loc.json:/wasteRatio [schema] exclusiveMaximum:");
        output.ShouldContain("1 lỗi");
        output.ShouldContain("Dữ liệu còn lỗi.");
    }

    [Fact]
    public void Warnings_go_to_the_output_and_do_not_fail_a_development_validate()
    {
        using var data = new TempData();
        data.CopyFile("ingredients", "ot", "ot-chi-thien", o => o["id"] = "ot-chi-thien");

        var (code, output, error) = data.Run("validate", "--data", data.Root);

        code.ShouldBe(CommandLine.Success);
        output.ShouldContain("warning: ingredients/ot-chi-thien.json [unused-ingredient]");
        output.ShouldContain("0 lỗi, 1 cảnh báo");
        error.ShouldBeEmpty();
    }

    [Fact]
    public void A_release_validate_fails_on_the_seed_and_passes_on_releasable_data()
    {
        using var data = new TempData();

        data.Run("validate", "--release", "--data", data.Root).Code.ShouldBe(CommandLine.DataInvalid);

        data.MakeReleasable();
        var (code, output, _) = data.Run("validate", "--release", "--data", data.Root);
        code.ShouldBe(CommandLine.Success);
        output.ShouldContain("(release)");
    }

    [Fact]
    public void A_missing_data_folder_is_an_error_with_the_path()
    {
        using var data = new TempData();
        var missing = Path.Combine(data.Root, "nowhere");

        var (code, _, error) = data.Run("validate", "--data", missing);

        code.ShouldBe(CommandLine.DataInvalid);
        error.ShouldContain("[data-folder]");
        error.ShouldContain(missing);
    }

    // ---- build -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Building_the_seed_writes_the_bundle_and_the_report()
    {
        using var data = new TempData();

        var (code, output, error) = data.Run("build", "--data", data.Root, "--out", data.Out);

        code.ShouldBe(CommandLine.Success);
        error.ShouldBeEmpty();
        File.Exists(BundlePath(data)).ShouldBeTrue();
        File.Exists(ReportPath(data)).ShouldBeTrue();
        var bundle = DataBundle.Parse(File.ReadAllText(BundlePath(data)));
        output.ShouldContain(bundle.Version);
        File.ReadAllText(ReportPath(data)).ShouldContain($"`{bundle.Version}`");
        bundle.IsRelease.ShouldBeFalse();
    }

    [Fact]
    public void The_output_folder_is_created_when_it_does_not_exist()
    {
        using var data = new TempData();
        var nested = Path.Combine(data.Out, "a", "b");

        data.Run("build", "--data", data.Root, "--out", nested).Code.ShouldBe(CommandLine.Success);

        File.Exists(Path.Combine(nested, CommandLine.BundleFileName)).ShouldBeTrue();
    }

    [Fact]
    public void The_files_are_utf8_without_a_byte_order_mark_and_with_lf_line_ends()
    {
        using var data = new TempData();
        data.Run("build", "--data", data.Root, "--out", data.Out);

        foreach (var path in new[] { BundlePath(data), ReportPath(data) })
        {
            var bytes = File.ReadAllBytes(path);
            bytes.Take(3).ShouldNotBe([0xEF, 0xBB, 0xBF]);
            bytes.ShouldNotContain((byte)'\r');
            bytes[^1].ShouldBe((byte)'\n');
        }
    }

    [Fact]
    public void Building_twice_gives_byte_identical_files()
    {
        using var data = new TempData();
        data.Run("build", "--data", data.Root, "--out", data.Out);
        var bundle = File.ReadAllBytes(BundlePath(data));
        var report = File.ReadAllBytes(ReportPath(data));

        data.Run("build", "--data", data.Root, "--out", data.Out);

        File.ReadAllBytes(BundlePath(data)).ShouldBe(bundle);
        File.ReadAllBytes(ReportPath(data)).ShouldBe(report);
    }

    [Fact]
    public void A_build_with_errors_writes_the_report_but_no_bundle_and_fails()
    {
        using var data = new TempData();
        data.Edit("ingredients", "ca-loc", o => o["wasteRatio"] = 1);

        var (code, output, _) = data.Run("build", "--data", data.Root, "--out", data.Out);

        code.ShouldBe(CommandLine.DataInvalid);
        File.Exists(BundlePath(data)).ShouldBeFalse();
        File.ReadAllText(ReportPath(data)).ShouldContain("1 lỗi");
        output.ShouldContain("Không ghi data-bundle.json");
    }

    [Fact]
    public void A_failed_build_removes_the_bundle_an_earlier_build_left_behind()
    {
        using var data = new TempData();
        data.Run("build", "--data", data.Root, "--out", data.Out).Code.ShouldBe(CommandLine.Success);
        File.Exists(BundlePath(data)).ShouldBeTrue();
        data.Edit("ingredients", "ca-loc", o => o["wasteRatio"] = 1);

        data.Run("build", "--data", data.Root, "--out", data.Out).Code.ShouldBe(CommandLine.DataInvalid);

        File.Exists(BundlePath(data)).ShouldBeFalse();
        File.Exists(ReportPath(data)).ShouldBeTrue();
    }

    [Fact]
    public void A_release_build_of_the_seed_fails_with_a_report_and_no_bundle()
    {
        using var data = new TempData();

        var (code, _, error) = data.Run("build", "--release", "--data", data.Root, "--out", data.Out);

        code.ShouldBe(CommandLine.DataInvalid);
        File.Exists(BundlePath(data)).ShouldBeFalse();
        File.ReadAllText(ReportPath(data)).ShouldContain("53 lỗi");
        error.ShouldContain("[release-placeholder]");
        error.ShouldContain("[release-status]");
    }

    [Fact]
    public void A_release_build_of_releasable_data_writes_a_release_bundle()
    {
        using var data = new TempData();
        data.MakeReleasable();

        var (code, _, _) = data.Run("build", "--release", "--data", data.Root, "--out", data.Out);

        code.ShouldBe(CommandLine.Success);
        var bundle = DataBundle.Parse(File.ReadAllText(BundlePath(data)));
        bundle.IsRelease.ShouldBeTrue();
        bundle.VersionMatchesContent().ShouldBeTrue();
    }
}
