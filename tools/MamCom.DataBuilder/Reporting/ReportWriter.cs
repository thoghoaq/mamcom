using System.Text;
using System.Text.Json;
using MamCom.Core.Data;
using MamCom.Core.Json;
using MamCom.Core.Models;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Reporting;

/// <summary>The Markdown of <c>dist/data-report.md</c>: counts, findings, image and nutrition coverage. The text has no dates, so equal data gives an equal report.</summary>
public static class ReportWriter
{
    public static string Render(ValidationReport report, DataBundle? bundle)
    {
        ArgumentNullException.ThrowIfNull(report);

        var ingredients = report.Data.Ingredients.Select(f => f.Value).ToArray();
        var dishes = report.Data.Dishes.Select(f => f.Value).ToArray();
        var text = new StringBuilder();

        void Line(string line = "") => text.Append(line).Append('\n');

        Line("# Báo cáo dữ liệu MâmCơm");
        Line();
        Line($"- Chế độ: {(report.Mode == ValidationMode.Release ? "release" : "dev")}");
        Line($"- Phiên bản dữ liệu: {(bundle is null ? "không tạo bundle" : $"`{bundle.Version}`")}");
        Line($"- Kết quả: {report.Errors.Count()} lỗi, {report.Warnings.Count()} cảnh báo");
        Line();

        Line("## Số lượng");
        Line();
        Table(text, ["Mục", "Số lượng"],
        [
            ["Nguyên liệu", Count(ingredients.Length)],
            ["Món", Count(dishes.Length)],
            ["– canh", Count(dishes.Count(d => d.Slot == DishSlot.Soup))],
            ["– mặn", Count(dishes.Count(d => d.Slot == DishSlot.Main))],
            ["– rau", Count(dishes.Count(d => d.Slot == DishSlot.Vegetable))],
            .. Enum.GetValues<DishStatus>().Select(status => new[] { $"Món `{StatusName(status)}`", Count(dishes.Count(d => d.Status == status)) }),
        ]);

        Findings(text, "Lỗi", report.Errors);
        Findings(text, "Cảnh báo", report.Warnings);

        Line("## Độ phủ ảnh");
        Line();
        Table(text, ["Mục", "Có ảnh", "Tổng"],
        [
            ["Nguyên liệu", Count(ingredients.Count(i => HasImage(i.Image))), Count(ingredients.Length)],
            ["Món", Count(dishes.Count(d => HasImage(d.Image))), Count(dishes.Length)],
            ["Ảnh minh hoạ (isIllustration)", Count(ingredients.Count(i => i.Image.IsIllustration) + dishes.Count(d => d.Image.IsIllustration)), Count(ingredients.Count(i => HasImage(i.Image)) + dishes.Count(d => HasImage(d.Image)))],
        ]);

        Line("## Độ phủ dinh dưỡng");
        Line();
        Table(text, ["Mục", "Có số", "Tổng"],
        [
            ["Nguyên liệu có nguồn thật (không phải placeholder)", Count(ingredients.Count(i => i.NutritionSource.Db != NutritionDbs.Placeholder)), Count(ingredients.Length)],
            ["kcal", Count(ingredients.Count(i => i.NutritionPer100gEdible.Kcal is not null)), Count(ingredients.Length)],
            ["Đạm", Count(ingredients.Count(i => i.NutritionPer100gEdible.ProteinG is not null)), Count(ingredients.Length)],
            ["Béo", Count(ingredients.Count(i => i.NutritionPer100gEdible.FatG is not null)), Count(ingredients.Length)],
            ["Bột đường", Count(ingredients.Count(i => i.NutritionPer100gEdible.CarbG is not null)), Count(ingredients.Length)],
            ["Chất xơ", Count(ingredients.Count(i => i.NutritionPer100gEdible.FiberG is not null)), Count(ingredients.Length)],
            ["Natri", Count(ingredients.Count(i => i.NutritionPer100gEdible.SodiumMg is not null)), Count(ingredients.Length)],
        ]);

        return text.ToString();
    }

    private static string Count(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static bool HasImage(ImageRef image) => image.Thumb is not null || image.Full is not null;

    private static string StatusName(DishStatus status) => JsonSerializer.Serialize(status, CoreJson.Options).Trim('"');

    private static void Findings(StringBuilder text, string title, IEnumerable<Diagnostic> diagnostics)
    {
        var list = diagnostics.ToArray();
        text.Append("## ").Append(title).Append(" (").Append(list.Length).Append(")\n\n");
        if (list.Length == 0)
        {
            text.Append("Không có.\n\n");
            return;
        }

        Table(text, ["File", "Vị trí", "Mã", "Nội dung"], list.Select(d => new[] { d.File, d.Location, d.Code, d.Message }));
    }

    private static void Table(StringBuilder text, string[] header, IEnumerable<string[]> rows)
    {
        static string Cell(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

        text.Append("| ").Append(string.Join(" | ", header)).Append(" |\n");
        text.Append('|').Append(string.Concat(header.Select(_ => "---|"))).Append('\n');
        foreach (var row in rows)
        {
            text.Append("| ").Append(string.Join(" | ", row.Select(Cell))).Append(" |\n");
        }

        text.Append('\n');
    }
}
