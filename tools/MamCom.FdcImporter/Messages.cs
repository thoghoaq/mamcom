using MamCom.FdcImporter.Fdc;
using MamCom.FdcImporter.Ranking;

namespace MamCom.FdcImporter;

/// <summary>Every sentence the importer writes, in Vietnamese for the people who choose the nutrition source. One place, like the app's strings.</summary>
internal static class Messages
{
    private static readonly Dictionary<string, string> NutrientLabels = new(StringComparer.Ordinal)
    {
        ["kcal"] = "năng lượng",
        ["proteinG"] = "đạm",
        ["fatG"] = "béo",
        ["carbG"] = "bột đường",
        ["fiberG"] = "chất xơ",
        ["sodiumMg"] = "natri",
    };

    public const string Usage = """
        Cách dùng:
          MamCom.FdcImporter suggest [--data <thư mục>] [--ids <id,id,...>] [--top <n>] [--refresh]
                                     [--delay-ms <n>] [--max-requests <n>] [--max-wait-seconds <n>]

        suggest  hỏi USDA FoodData Central và ghi vài ứng viên dinh dưỡng cho mỗi nguyên liệu vào
                 data/_suggestions/nutrition/<id>.json. Không sửa file nguyên liệu: người chọn trong Studio.

        Tên tiếng Anh để tìm nằm trong data/plan/ingredient-names-en.csv (cột id, english).

        --data              thư mục dữ liệu, mặc định "data".
        --ids               chỉ các nguyên liệu này; mặc định là mọi nguyên liệu có tên tiếng Anh.
        --top               số ứng viên giữ lại, 1 đến 10, mặc định 3.
        --refresh           hỏi lại cả nguyên liệu đã có file gợi ý (mặc định bỏ qua để tiết kiệm hạn mức).
        --delay-ms          nghỉ giữa hai lần gọi, mặc định 1000.
        --max-requests      số lần gọi tối đa trong một lượt chạy, mặc định 25.
        --max-wait-seconds  chờ tối đa bao lâu khi bị giới hạn (429) rồi thử lại, mặc định 60.

        Khoá API: đặt bằng  dotnet user-secrets set "Fdc:ApiKey" "<khoá>" --project tools/MamCom.FdcImporter
        Không có thì dùng DEMO_KEY, hạn mức rất thấp.
        """;

    public static string Reason(string dataType, DescriptionState state, string? processedWord, ExtractedNutrition nutrition, int matched, int queryWords)
    {
        var parts = new List<string>
        {
            dataType switch
            {
                "Foundation" => "Foundation Foods (số liệu phân tích mới của USDA)",
                "SR Legacy" => "SR Legacy (bảng cũ của USDA)",
                _ => dataType,
            },
            state switch
            {
                DescriptionState.Raw => "mô tả có 'raw': nguyên liệu sống",
                DescriptionState.Processed => $"mô tả có '{processedWord}': đã chế biến, cần cân nhắc",
                _ => "mô tả không nói sống hay chín",
            },
            nutrition.Missing.Count == 0
                ? $"đủ {CandidateRanker.NutrientCount} chất"
                : "thiếu: " + string.Join(", ", nutrition.Missing.Select(m => NutrientLabels.GetValueOrDefault(m, m))),
            $"khớp {matched}/{queryWords} từ khoá",
        };

        if (nutrition.KcalSource is not null && nutrition.KcalSource != "Energy")
        {
            parts.Add($"năng lượng lấy từ '{nutrition.KcalSource}'");
        }

        return string.Join("; ", parts) + ".";
    }

    public static string UsingDemoKey() => "Khoá API: DEMO_KEY (hạn mức rất thấp). Đặt khoá riêng bằng user-secrets để chạy nhiều hơn.";

    public static string UsingOwnKey() => "Khoá API: lấy từ user-secrets.";

    public static string NoSuchIngredient(string id) => $"Không có nguyên liệu '{id}' trong data/ingredients.";

    public static string NamesFileMissing(string path) => $"Không thấy file tên tiếng Anh '{path}'. Cần các cột id và english.";

    public static string NamesFileInvalid(string reason) => $"File tên tiếng Anh sai: {reason}";

    public static string NamesForUnknownIngredients(IEnumerable<string> ids) =>
        $"Cảnh báo: tên tiếng Anh cho nguyên liệu không có file: {string.Join(", ", ids)}.";

    public static string Written(string id, string path, int count) =>
        count == 0
            ? $"{id}: ghi {path}; FDC không có món nào khớp, kiểm lại tên tiếng Anh."
            : $"{id}: ghi {path} ({count} ứng viên).";

    public static string SkippedExisting(string id) => $"{id}: bỏ qua, đã có file gợi ý. Dùng --refresh để hỏi lại.";

    public static string NoEnglishName(string id) => $"{id}: bỏ qua, chưa có tên tiếng Anh trong ingredient-names-en.csv.";

    public static string Failed(string id, string reason) => $"{id}: lỗi, {reason}";

    public static string StoppedMaxRequests(int max) => $"Dừng: đã dùng hết {max} lần gọi của lượt này (--max-requests).";

    public static string StoppedNoQuota(int? limit) =>
        $"Dừng: khoá API hết hạn mức{(limit is { } l ? $" ({l} lần)" : string.Empty)}. Chạy lại sau; các file đã ghi được giữ.";

    public static string StoppedRateLimited(TimeSpan? retryAfter) =>
        retryAfter is { } wait
            ? $"Dừng: FDC giới hạn (429) và bắt chờ {Math.Ceiling(wait.TotalSeconds)} giây, lâu hơn mức cho phép (--max-wait-seconds). Chạy lại sau."
            : "Dừng: FDC giới hạn (429) mà không nói chờ bao lâu. Chạy lại sau.";

    public static string StoppedAuth() => "Dừng: FDC không nhận khoá API (401 hoặc 403). Kiểm khoá trong user-secrets.";

    public static string Summary(int written, int skipped, int failed, int requests) =>
        $"Xong: ghi {written}, bỏ qua {skipped}, lỗi {failed}; đã gọi FDC {requests} lần.";
}
