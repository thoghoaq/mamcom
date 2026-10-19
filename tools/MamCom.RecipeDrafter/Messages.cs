using MamCom.RecipeDrafter.Model;

namespace MamCom.RecipeDrafter;

/// <summary>Every sentence the drafter writes, in Vietnamese for the person who reads the drafts. One place, like the app's strings.</summary>
internal static class Messages
{
    public const string Usage = """
        Cách dùng:
          MamCom.RecipeDrafter draft [--data <thư mục>] [--list <file csv>] [--only <id,id,...>]
                                     [--limit <n> | --all] [--redo] [--prompts <thư mục>]

        draft  đọc danh sách món (data/plan/dish-list.csv), nhờ Claude soạn nháp công thức cho từng món và ghi vào
               data/dishes/_drafts/<id>.json, kèm <id>.report.json nói bản nháp có qua được các luật kiểm không và
               còn thiếu nguyên liệu nào. Mỗi món là một lần gọi API, có tính phí.

        Món đã có file thật (data/dishes/<id>.json) hoặc đã có bản nháp thì bị bỏ qua, nên chạy lại là chạy tiếp.
        File thật không bao giờ bị ghi đè. Bản nháp luôn ở trạng thái draft; chỉ người mới duyệt được.

        --data     thư mục dữ liệu, mặc định "data".
        --list     file danh sách món, mặc định <data>/plan/dish-list.csv (cột name,slot,regions và id tuỳ chọn).
        --only     chỉ các món này (theo id).
        --limit    số món tối đa soạn trong một lượt, mặc định 5. --all để bỏ giới hạn.
        --redo     soạn lại cả món đã có bản nháp (không đụng file thật).
        --prompts  thư mục chứa system.md và dish.md, mặc định thư mục prompts cạnh file chạy.

        Cấu hình (user-secrets, dotnet user-secrets set "<tên>" "<giá trị>" --project tools/MamCom.RecipeDrafter):
          Claude:ApiKey  khoá API; không có thì dùng biến môi trường ANTHROPIC_API_KEY.
          Claude:Model   model, mặc định claude-opus-5-5.
          Claude:Effort  low, medium, high hoặc max; mặc định medium.
        """;

    public static string ModelRefused(string? category) =>
        category is null ? "Model từ chối soạn món này." : $"Model từ chối soạn món này (nhóm: {category}).";

    public static string ModelCutOff(int maxTokens) => $"Câu trả lời bị cắt giữa chừng vì hết {maxTokens} token.";

    public static string ModelNoText() => "Model không trả về nội dung nào.";

    public static string ModelBadAnswer(string reason) => $"Câu trả lời không đúng định dạng: {reason}";

    public static string ModelValueRefused(string reason) => $"Câu trả lời có giá trị không dùng được: {reason}";

    public static string ModelCallFailed(string reason) => $"Gọi API lỗi: {reason}";

    public static string MissingIngredient(string name, double grams) =>
        $"Món cần '{name}' (khoảng {grams:0.#} g mỗi suất) mà data/ingredients chưa có. Thêm nguyên liệu rồi soạn lại bằng --redo, hoặc thêm dòng vào công thức bằng tay.";

    public static string Drafted(string id, string validation, string path, int errors, int warnings) =>
        $"{id}: {validation}, ghi {path} ({errors} lỗi, {warnings} cảnh báo).";

    public static string SkippedFinal(string id) => $"{id}: bỏ qua, đã có file thật trong data/dishes.";

    public static string SkippedDraft(string id) => $"{id}: bỏ qua, đã có bản nháp. Dùng --redo để soạn lại.";

    public static string Failed(string id, string reason) => $"{id}: không soạn được. {reason}";

    public static string StoppedAuth() =>
        "Dừng: API không nhận khoá (401 hoặc 403). Kiểm Claude:ApiKey trong user-secrets hoặc biến ANTHROPIC_API_KEY.";

    public static string StoppedRateLimited() => "Dừng: API đang giới hạn khoá này (429), kể cả sau khi tự thử lại. Chạy lại sau; các bản nháp đã ghi được giữ.";

    public static string StoppedOutOfCredit() =>
        "Dừng: tài khoản Anthropic hết tín dụng API (\"credit balance is too low\"). Nạp thêm tín dụng ở Plans & Billing rồi chạy lại; các bản nháp đã ghi được giữ và món chưa soạn sẽ được thử lại.";

    public static string NoApiKey() =>
        "Chưa có khoá API. Đặt bằng: dotnet user-secrets set \"Claude:ApiKey\" \"<khoá>\" --project tools/MamCom.RecipeDrafter, hoặc đặt biến môi trường ANTHROPIC_API_KEY.";

    public static string BadEffort(string effort, IEnumerable<string> allowed) =>
        $"Claude:Effort '{effort}' không dùng được; chọn một trong: {string.Join(", ", allowed)}.";

    public static string DataNotUsable() => "Không đọc được dữ liệu cần để soạn (cấu hình hoặc danh sách nguyên liệu). Chạy DataBuilder validate để xem lỗi.";

    public static string DishListMissing(string path) => $"Không thấy danh sách món '{path}'.";

    public static string DishListInvalid(string reason) => $"Danh sách món sai: {reason}";

    public static string PromptsMissing(string folder, string file) => $"Không thấy '{file}' trong thư mục prompt '{folder}'.";

    public static string PromptInvalid(string reason) => $"Prompt sai: {reason}";

    public static string UnknownDish(string id) => $"Không có món '{id}' trong danh sách món.";

    public static string Plan(int count, string model, string effort) =>
        $"Soạn tối đa {count} món bằng {model} (effort {effort}); mỗi món một lần gọi API có tính phí.";

    public static string LimitReached(int limit) => $"Đã soạn đủ {limit} món của lượt này (--limit). Chạy lại để soạn tiếp, hoặc dùng --all.";

    public static string Summary(int drafted, int valid, int warnings, int invalid, int failed, int skipped, int calls, TokenUsage usage) =>
        $"Xong: soạn {drafted} (hợp lệ {valid}, cảnh báo {warnings}, không hợp lệ {invalid}), không soạn được {failed}, bỏ qua {skipped}; "
        + $"gọi API {calls} lần; token vào {usage.InputTokens}, ra {usage.OutputTokens}, ghi cache {usage.CacheCreationInputTokens}, đọc cache {usage.CacheReadInputTokens}.";

    public static string Catalog(string id, string name, string category, string? protein) =>
        protein is null ? $"- {id}: {name} ({category})" : $"- {id}: {name} ({category}; đạm: {protein})";

    public static string SlotLabel(MamCom.Core.Models.DishSlot slot) => slot switch
    {
        MamCom.Core.Models.DishSlot.Soup => "canh",
        MamCom.Core.Models.DishSlot.Main => "món mặn",
        _ => "rau",
    };

    public static string NoRegion() => "không đặc trưng vùng nào";
}
