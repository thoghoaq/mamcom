using System.Globalization;

namespace MamCom.DataBuilder.Validation;

/// <summary>
/// The wording of every finding the builder makes itself, in Vietnamese for the people who fix the data. Schema
/// findings keep the validator's own English text. Kept in one place, like the app's strings.
/// </summary>
internal static class Messages
{
    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string MissingDataFolder(string path) => $"Không tìm thấy thư mục dữ liệu '{path}'.";

    public static string MissingSubfolder(string name) => $"Thiếu thư mục '{name}' trong thư mục dữ liệu.";

    public static string MissingSchema(string name) => $"Thiếu schema '{name}.schema.json' trong data/schemas.";

    public static string UnusableSchemas(string reason) => $"Không nạp được các schema: {reason}";

    public static string MissingConfig(string name) => $"Thiếu file cấu hình 'config/{name}.json'.";

    public static string InvalidConfig(string reason) => reason;

    public static string NotJson(string reason) => $"Không đọc được file: {reason}";

    public static string FileNameDiffersFromId(string stem, string id) => $"Tên file '{stem}.json' khác id '{id}'. Đặt tên file đúng bằng id.";

    public static string DuplicateId(string id, string otherFile) => $"Id '{id}' đã dùng ở {otherFile}.";

    public static string UnknownIngredient(string id) => $"Nguyên liệu '{id}' không có trong data/ingredients.";

    public static string UnknownSeason(string season) =>
        $"Mùa '{season}' không có trong seasonByMonth của spinner-defaults.json và cũng không phải 'all'.";

    public static string StepOrder(int count, IEnumerable<int> orders) =>
        $"Thứ tự các bước phải là 1 đến {count}, không trùng; hiện là {string.Join(", ", orders)}.";

    public static string DuplicateLine(string id, int count) => $"Nguyên liệu '{id}' có {count} dòng trong công thức; gộp thành một dòng.";

    public static string ImageMissing(string name) => $"Không có file ảnh '{name}' trong data/images/out.";

    public static string ImagePath(string name) => $"Tên file ảnh '{name}' phải là một tên file, không chứa đường dẫn.";

    public static string RiceMissing(string id) => $"portion-defaults.json: riceIngredientId '{id}' không có trong data/ingredients.";

    public static string ReleasePlaceholder() => "Nguyên liệu còn dinh dưỡng placeholder; bản release cần nguồn thật.";

    public static string ReleaseStatus(string statusName) =>
        $"Món đang ở trạng thái '{statusName}'; bản release chỉ gồm món từ 'reviewed' trở lên.";

    public static string KcalRange(double kcal, ValueRange range, string slotName) =>
        $"≈{N(Math.Round(kcal))} kcal mỗi suất, ngoài khoảng {N(range.Min)}–{N(range.Max)} của món '{slotName}'.";

    public static string ProteinRange(double grams, ValueRange range) =>
        $"{N(grams)} g nguồn đạm mỗi suất, ngoài khoảng {N(range.Min)}–{N(range.Max)} g của món mặn.";

    public static string SodiumHigh(double mg, double max) => $"≈{N(Math.Round(mg))} mg natri mỗi suất, vượt {N(max)} mg.";

    public static string UnusedIngredient() => "Nguyên liệu không xuất hiện ở bước nào của món nào.";

    public static string LineNotInSteps(string id) => $"Nguyên liệu '{id}' có trong công thức nhưng không có ở bước nào của món.";

    public static string StepIngredientNotInRecipe(int order, string id) => $"Bước {order} nhắc nguyên liệu '{id}' không có trong công thức của món.";

    public static string ApproxOnWeightUnit(string display) =>
        $"showApproxGrams đang bật cho đơn vị cân '{display}'; chỉ nên bật cho đơn vị đếm như con, quả.";
}
