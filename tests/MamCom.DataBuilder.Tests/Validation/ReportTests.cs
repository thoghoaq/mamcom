using MamCom.DataBuilder.Bundling;
using MamCom.DataBuilder.Reporting;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Tests.Validation;

public class ReportTests
{
    private static string RenderBuild(TempData data, ValidationMode mode = ValidationMode.Dev)
    {
        var report = data.Validate(mode);
        return ReportWriter.Render(report, report.HasErrors ? null : BundleBuilder.Build(report.Data, mode));
    }

    [Fact]
    public void The_report_of_the_seed_gives_the_mode_the_version_and_the_counts()
    {
        using var data = new TempData();
        var report = data.Validate();
        var bundle = BundleBuilder.Build(report.Data, ValidationMode.Dev);

        var text = ReportWriter.Render(report, bundle);

        text.ShouldStartWith("# Báo cáo dữ liệu MâmCơm\n");
        text.ShouldContain("- Chế độ: dev\n");
        text.ShouldContain($"- Phiên bản dữ liệu: `{bundle.Version}`\n");
        text.ShouldContain("- Kết quả: 0 lỗi, 0 cảnh báo\n");
        text.ShouldContain("| Nguyên liệu | 33 |\n");
        text.ShouldContain("| Món | 20 |\n");
        text.ShouldContain("| – canh | 7 |\n");
        text.ShouldContain("| – mặn | 8 |\n");
        text.ShouldContain("| – rau | 5 |\n");
        text.ShouldContain("| Món `draft` | 20 |\n");
        text.ShouldContain("| Món `reviewed` | 0 |\n");
        text.ShouldContain("## Lỗi (0)\n\nKhông có.\n");
        text.ShouldContain("## Cảnh báo (0)\n\nKhông có.\n");
    }

    [Fact]
    public void The_seed_has_no_image_and_no_real_nutrition_source_and_the_report_says_so()
    {
        using var data = new TempData();

        var text = RenderBuild(data);

        text.ShouldContain("| Nguyên liệu | 0 | 33 |\n");
        text.ShouldContain("| Món | 0 | 20 |\n");
        text.ShouldContain("| Nguyên liệu có nguồn thật (không phải placeholder) | 0 | 33 |\n");
        text.ShouldContain("| kcal | 0 | 33 |\n");
        text.ShouldContain("| Natri | 0 | 33 |\n");
    }

    [Fact]
    public void Coverage_counts_what_the_data_declares()
    {
        using var data = new TempData();
        data.Edit("ingredients", "ca-loc", o =>
        {
            o["nutritionSource"]!["db"] = "usda-fdc";
            o["nutritionSource"]!["ref"] = "0000000";
            o["nutritionSource"]!["matchedName"] = "test-only entry";
            o["nutritionPer100gEdible"] = new System.Text.Json.Nodes.JsonObject
            {
                ["kcal"] = 100, ["proteinG"] = 20, ["fatG"] = 3, ["carbG"] = 0, ["fiberG"] = null, ["sodiumMg"] = 50,
            };
            o["image"]!["thumb"] = "ca-loc-200.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
            o["image"]!["isIllustration"] = true;
        });
        data.WriteImage("ca-loc-200.webp");

        // A dish that only declares the large picture still has an image.
        data.Edit("dishes", "canh-bau-tom", o =>
        {
            o["image"]!["full"] = "canh-bau-tom-800.webp";
            o["image"]!["source"] = "tự chụp";
            o["image"]!["license"] = "CC0";
        });
        data.WriteImage("canh-bau-tom-800.webp");

        var text = RenderBuild(data);

        text.ShouldContain("| Nguyên liệu | 1 | 33 |\n");
        text.ShouldContain("| Món | 1 | 20 |\n");
        text.ShouldContain("| Ảnh minh hoạ (isIllustration) | 1 | 2 |\n");
        text.ShouldContain("| Nguyên liệu có nguồn thật (không phải placeholder) | 1 | 33 |\n");
        text.ShouldContain("| kcal | 1 | 33 |\n");
        text.ShouldContain("| Đạm | 1 | 33 |\n");
        text.ShouldContain("| Chất xơ | 0 | 33 |\n");
        text.ShouldContain("| Natri | 1 | 33 |\n");
    }

    [Fact]
    public void A_report_with_errors_lists_them_and_says_no_bundle_was_made()
    {
        using var data = new TempData();

        var text = RenderBuild(data, ValidationMode.Release);

        text.ShouldContain("- Chế độ: release\n");
        text.ShouldContain("- Phiên bản dữ liệu: không tạo bundle\n");
        text.ShouldContain("- Kết quả: 53 lỗi, 0 cảnh báo\n");
        text.ShouldContain("## Lỗi (53)\n");
        text.ShouldContain("| File | Vị trí | Mã | Nội dung |\n|---|---|---|---|\n");
        text.ShouldContain("| ingredients/gao.json | /nutritionSource/db | release-placeholder | ");
        text.ShouldContain("| dishes/thit-kho-trung.json | /status | release-status | ");
    }

    [Fact]
    public void Warnings_are_listed_apart_from_errors()
    {
        using var data = new TempData();
        data.CopyFile("ingredients", "ot", "ot-chi-thien", o => o["id"] = "ot-chi-thien");

        var text = RenderBuild(data);

        text.ShouldContain("## Lỗi (0)\n");
        text.ShouldContain("## Cảnh báo (1)\n");
        text.ShouldContain("| ingredients/ot-chi-thien.json |  | unused-ingredient | ");
    }

    [Fact]
    public void A_pipe_inside_a_message_does_not_break_the_table()
    {
        using var data = new TempData();
        var loaded = data.Load();
        var report = new ValidationReport(
            ValidationMode.Dev,
            loaded,
            [new Diagnostic(Severity.Error, "schema", "dishes/x.json", "/a", "one | two\nthree")]);

        var text = ReportWriter.Render(report, null);

        text.ShouldContain("| dishes/x.json | /a | schema | one \\| two three |\n");
    }

    [Fact]
    public void The_same_data_gives_the_same_report_with_lf_line_ends_and_no_dates()
    {
        using var data = new TempData();

        var first = RenderBuild(data);
        var second = RenderBuild(data);

        second.ShouldBe(first);
        first.ShouldNotContain('\r');
        first.ShouldEndWith("\n");
        first.ShouldNotContain("2026");
    }
}
