using System.Globalization;

namespace MamCom.ImageTool;

/// <summary>Every sentence the tool writes, in Vietnamese for the person who took the photos. One place, like the app's strings.</summary>
internal static class Messages
{
    public const string Usage = """
        Cách dùng:
          MamCom.ImageTool process [--data <thư mục>] [--only <id,id,...>] [--source <nguồn>] [--license <giấy phép>]
                                   [--illustration | --photo] [--force]

        process  đổi ảnh gốc trong data/images/raw thành hai file WebP trong data/images/out:
                   <id>-200.webp  ảnh nhỏ, cạnh dài 200px (đóng gói trong app)
                   <id>-800.webp  ảnh lớn, cạnh dài 800px
                 rồi ghi tên hai file vào file nguyên liệu hoặc món có mã đó. Ảnh được xoay đúng chiều, xoá hết EXIF
                 (kể cả vị trí chụp), không cắt và không phóng to.

        Ảnh gốc đặt tên theo mã của nguyên liệu hoặc món, ví dụ gao.jpg hay ca-loc-kho-to.jpg (đuôi .jpg, .jpeg, .png, .webp).
        Ảnh nguyên liệu nên vuông 1:1, ảnh món nên 4:3 (SPEC §7.5).

        --data          thư mục dữ liệu, mặc định "data".
        --only          chỉ các mã này; mặc định là mọi ảnh gốc trong thư mục.
        --source        nguồn ảnh, áp dụng cho mọi ảnh của lượt chạy này, ví dụ "tự chụp, Lan 2026".
        --license       giấy phép của ảnh. Ảnh nào chưa có nguồn và giấy phép (trong file dữ liệu hay ở đây) thì bị từ chối.
        --illustration  đánh dấu là ảnh minh hoạ (AI hoặc ảnh tạm).
        --photo         đánh dấu là ảnh thật, bỏ dấu minh hoạ đang có.
        --force         làm lại cả ảnh mới hơn ảnh gốc (mặc định bỏ qua).

        Ảnh gốc không nằm trong Git (xem .gitignore): ảnh chụp bằng điện thoại thường mang vị trí GPS.
        """;

    public const string DataNotUsable = "Dữ liệu chưa đọc được (thiếu schema hay config). Chạy: dotnet run --project tools/MamCom.DataBuilder -- validate";

    public static string RawFolderMissing(string path) =>
        $"Không thấy thư mục ảnh gốc '{path}'. Tạo nó và đặt ảnh gốc vào, đặt tên theo mã nguyên liệu hoặc món, ví dụ gao.jpg.";

    public static string NoOriginal(string id) => $"{id}: không có ảnh gốc tên '{id}' (.jpg, .jpeg, .png hoặc .webp) trong thư mục ảnh gốc.";

    public static string TwoOriginals(string id, IEnumerable<string> names) =>
        $"{id}: có hai ảnh gốc cùng mã ({string.Join(", ", names)}). Giữ một ảnh.";

    public static string UnsupportedFile(string name) =>
        Path.GetExtension(name).ToLowerInvariant() is ".heic" or ".heif"
            ? $"{name}: ảnh HEIC của iPhone chưa đọc được. Xuất sang JPEG rồi đặt lại."
            : $"{name}: đuôi file chưa hỗ trợ (dùng .jpg, .jpeg, .png hoặc .webp).";

    public static string BadId(string name) =>
        $"{name}: tên ảnh gốc phải là mã của nguyên liệu hoặc món, chỉ gồm chữ thường a-z, số và gạch ngang (ví dụ rau-muong.jpg).";

    public static string NoSuchTarget(string id, bool isDraft) => isDraft
        ? $"{id}: món này còn là bản nháp (data/dishes/_drafts). Duyệt nó trong Studio trước, rồi chạy lại."
        : $"{id}: không có nguyên liệu hay món nào mã '{id}' (data/ingredients, data/dishes).";

    public static string IngredientAndDish(string id) =>
        $"{id}: vừa là mã nguyên liệu vừa là mã món, không biết ảnh của cái nào. Đổi một trong hai mã.";

    public static string FileBroken(string file, string reason) => $"{file}: file dữ liệu hỏng, không đọc được ({reason}).";

    public static string WriteFailed(string id, string reason) => $"{id}: không ghi được file ({reason}).";

    public static string Unreadable(string name, string reason) => $"{name}: không đọc được ảnh ({reason}).";

    public static string SourceAndLicenseNeeded(string id) =>
        $"{id}: ảnh cần có nguồn và giấy phép, mà file dữ liệu chưa có. Thêm --source \"...\" và --license \"...\" (ảnh tự chụp: ghi người chụp và giấy phép bạn chọn).";

    public static string CheckFailed(string file, IEnumerable<string> errors) =>
        $"{file}: sau khi gắn ảnh máy kiểm còn lỗi nên chưa sửa file: {string.Join("; ", errors)}";

    public static string SmallOriginal(int width, int height, int fullPixels) =>
        $"ảnh gốc {width}×{height} có cạnh dài dưới {fullPixels}px nên ảnh lớn giữ nguyên cỡ gốc.";

    public static string NotSquare(int width, int height) => $"ảnh nguyên liệu nên vuông 1:1, ảnh gốc là {width}×{height}.";

    public static string NotFourByThree(int width, int height) => $"ảnh món nên có tỷ lệ 4:3, ảnh gốc là {width}×{height}.";

    public static string Rendition(string name, int width, int height, int bytes) =>
        string.Create(CultureInfo.InvariantCulture, $"{name} ({width}×{height}, {bytes / 1024.0:0.#} KB)");

    public static string Written(string id, string thumb, string full, string file, bool changed) =>
        $"{id}: đã tạo {thumb} và {full}; " + (changed ? $"đã ghi tên ảnh vào {file}." : $"{file} đã đúng.");

    public static string JsonOnly(string id, string file) => $"{id}: ảnh đã mới, chỉ cập nhật {file}.";

    public static string UpToDate(string id) => $"{id}: ảnh đã mới, bỏ qua.";

    public static string Summary(int written, int jsonOnly, int upToDate, int failed) =>
        $"Xong: {written} ảnh đã tạo, {jsonOnly} chỉ sửa file dữ liệu, {upToDate} bỏ qua vì đã mới, {failed} lỗi.";
}
