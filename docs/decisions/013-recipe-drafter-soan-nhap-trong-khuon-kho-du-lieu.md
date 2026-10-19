# ADR-013: RecipeDrafter soạn nháp công thức bằng Claude, trong khuôn khổ dữ liệu

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án xác nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §7.4](../SPEC.md), [PLAN 2.4](../PLAN.md), [ADR-010](010-json-schema-thu-vien-va-pham-vi.md), [ADR-011](011-dong-goi-du-lieu-va-luat-validate.md), CLAUDE.md (quy tắc dữ liệu)

## Bối cảnh

PLAN 2.4 yêu cầu `RecipeDrafter` đọc danh sách món, gọi Claude API, ép đầu ra đúng schema, chỉ dùng id nguyên liệu có sẵn, đưa nguyên liệu lạ vào `missingIngredients`, không xin số dinh dưỡng, ghi vào `data/dishes/_drafts/` và chạy tiếp được. Còn ngỏ:

- Dạng của `dish-list.csv`, và id của món lấy ở đâu.
- Ép "chỉ dùng id có sẵn" bằng cách nào, khi schema của món cấm trường lạ nên `missingIngredients` không có chỗ trong file món.
- Kiểm bản nháp ở đâu, và "gắn cờ rõ ràng" là gì.
- Chi phí: mỗi món là một lần gọi tính phí.
- CLAUDE.md cấm tự đặt trạng thái món, lấy công thức từ nguồn khác và lấy số dinh dưỡng từ LLM.

## Quyết định

- **Danh sách món** `data/plan/dish-list.csv`: cột `name,slot,regions` và cột `id` tuỳ chọn. `slot` là `canh`, `man`, `rau`; `regions` là `north`, `central`, `south` nối bằng `;` hoặc để trống. Không có cột `id` thì id là tên bỏ dấu, viết thường, nối bằng gạch ngang ("Cá lóc kho tộ" thành `ca-loc-kho-to`). Id trùng, id `com` (dành riêng cho cơm) hay id không phải kebab-case là lỗi, kèm số dòng. File mẫu có ba món là để chạy thử; chủ dự án chốt danh sách thật (SPEC §7.4 bước 1).
- **Gọi Claude qua SDK chính thức** (`Anthropic` 12.58.0, MIT), mỗi món một lần gọi. Model mặc định `claude-opus-5-5`, `effort` mặc định `medium`; khoá và model đặt trong user-secrets (`Claude:ApiKey`, `Claude:Model`, `Claude:Effort`), thiếu khoá thì dùng biến môi trường `ANTHROPIC_API_KEY`. Suy nghĩ để mặc định (Opus 5.5 không tắt được). Không đặt `temperature` (model không nhận). Khoá không bao giờ được in ra.
- **Model chỉ quyết định phần được phép:** mùa, độ cay, cách nấu, nguồn đạm, `serveHot`, các dòng công thức, các bước, và nguyên liệu còn thiếu. Tên, loại và vùng là của chủ dự án; trạng thái, dấu duyệt, nấu thử và ảnh do chương trình đặt: bản nháp luôn là `draft`, `draftedBy: llm`, không ai duyệt, không ảnh. Model không có chỗ nào để đặt các trường đó.
- **Ép đầu ra bằng structured outputs** với JSON Schema dựng từ dữ liệu: `ingredientId` (ở dòng công thức và ở bước), mùa và nguồn đạm là `enum` lấy từ dữ liệu hiện có, mọi đối tượng cấm trường lạ, và không có trường nào cho số dinh dưỡng. Nguyên liệu mà công thức cần nhưng danh sách chưa có thì vào `missingIngredients` (tên, gram một suất, ghi chú) chứ không thành dòng công thức. Các giới hạn mịn hơn (gram lớn hơn 0, số phút) để schema của món kiểm sau, nơi một lỗi được báo thay vì làm hỏng cả câu trả lời.
- **Kiểm bản nháp như dữ liệu thật:** schema `dish`, rồi các luật của DataBuilder có nghĩa với một món (nguyên liệu và mùa không tồn tại, `order`, dòng lặp, khoảng đạm của món mặn, nguyên liệu không có trong bước…), bằng cách chạy `DataValidator` với bản nháp là món duy nhất. Chỉ tính các phát hiện về chính bản nháp: "nguyên liệu không dùng" của cả kho không lọt vào.
- **Ghi:** `data/dishes/_drafts/<id>.json` (đúng định dạng chuẩn của file món) và `<id>.report.json` (schema `draft-report`): kết quả `valid`, `warnings`, `invalid` (có file nháp nhưng vi phạm luật) hay `failed` (model từ chối, bị cắt giữa chừng hoặc không trả đúng định dạng nên không có file nháp), danh sách vấn đề kèm vị trí, nguyên liệu thiếu, model, ngày và số token. Ghi qua file tạm rồi đổi tên. Chương trình không ghi vào chỗ nào khác, và không bao giờ ghi đè file thật `data/dishes/<id>.json`, kể cả khi `--redo`.
- **Chạy tiếp và kiểm soát chi phí:** món đã có file thật hoặc đã có bản nháp thì bỏ qua (`--redo` soạn lại bản nháp); món `failed` không có bản nháp nên lượt sau thử lại. Mỗi lượt chỉ soạn tối đa 5 món (`--limit`, `--all` để bỏ), vì mỗi món tốn tiền; `--only` chọn món. Dòng đầu cho biết sẽ soạn bao nhiêu món bằng model nào; cuối lượt in số token.
- **Lỗi API:** 401 hoặc 403, 429 (sau hai lần SDK tự thử lại) và tài khoản hết tín dụng dừng cả lượt và giữ các bản nháp đã ghi, món gặp lỗi không có báo cáo; lỗi khác chỉ làm hỏng món đó (`failed`). Hết tín dụng là một HTTP 400 có lời nhắn "credit balance is too low", cùng mã với một yêu cầu bị từ chối, nên nhận ra bằng lời nhắn (không phân biệt hoa thường), không bằng mã. Ghi thêm sau lần chạy thật đầu tiên (2026-10-19): lúc đó lỗi này bị coi là lỗi của từng món, nên cả ba món được gọi và cả ba bị ghi `failed`; có `--all` trên cả trăm món thì là cả trăm lần gọi hỏng.
- **Prompt nằm trong file riêng** `tools/MamCom.RecipeDrafter/prompts/system.md` và `dish.md`, copy cạnh file chạy (`--prompts` để dùng thư mục khác). Chỗ điền `{{tên}}` chặt chẽ: thiếu giá trị hoặc thừa giá trị là lỗi. Phần `system` giống nhau cho cả lượt và có điểm cache, nên một lượt nhiều món chỉ trả tiền phần đó một lần. Khoảng gram nguồn đạm của món mặn trong prompt lấy từ `validation-defaults.json`, cùng số với cảnh báo của DataBuilder.
- **Chưa bật `fallbacks` của API cho từ chối.** Công thức nấu ăn ít khi bị bộ phân loại an toàn từ chối; khi xảy ra, món được ghi là `failed` kèm nhóm từ chối và lượt sau thử lại. Bật fallback đòi dùng endpoint beta và một model thứ hai; để dành nếu thực tế từ chối nhiều.

## Phương án đã cân nhắc

1. **Dùng tool use để lấy kết quả có cấu trúc.** Chạy được nhưng phải ép gọi tool bằng `tool_choice`, mà Opus 5.5 không nhận (400). Structured outputs gọn hơn. Bị loại.
2. **Để model tự đặt id nguyên liệu mới.** Tiện nhưng phá quy tắc "chỉ dùng id có sẵn" và làm dữ liệu nguyên liệu phình ra không qua người. Bị loại.
3. **Bắt model trả nguyên file món.** Phải tin model không tự đặt `status` hay `provenance`. Cho nó ít quyền nhất (chỉ phần được phép) an toàn hơn. Bị loại.
4. **Gọi song song hoặc dùng Batches API.** Batches rẻ hơn một nửa nhưng chạy bất đồng bộ; không đáng cho lượt 3 đến 5 món. Đáng xem lại khi soạn cả trăm món một lần.
5. **Ghi bản lỗi vào thư mục riêng.** Thêm thư mục và luật chuyển file. Báo cáo cạnh bản nháp đủ để người thấy và sửa. Bị loại.

## Hệ quả

**Được**
- Bản nháp luôn đúng khuôn: id nguyên liệu có thật, không số dinh dưỡng, không trạng thái; người mở ra là có cả công thức lẫn bản ghi những chỗ cần sửa.
- Chạy lại an toàn và rẻ: không soạn lại món đã có, không bao giờ mất bản của người.

**Đổi lại**
- Chưa chạy với khoá thật: PLAN 2.4 chờ chủ dự án chạy thử ba món bằng khoá của mình.
- Công thức vẫn do một mô hình viết: phải người đọc, sửa và nấu thử (SPEC §7.4 bước 6, 7). Ba lớp kiểm tự động chỉ bắt lỗi hình thức.
- Mỗi lượt gọi API tính phí vào tài khoản của chủ dự án; `--limit` mặc định 5 là hàng rào, không phải dự toán.
- Danh sách nguyên liệu nằm trong prompt nên mỗi món đọc cả danh sách; với vài trăm nguyên liệu thì phần cache giúp, nhưng đáng đo.

## Khi nào xem lại

- Chạy thử cho thấy công thức kém hoặc model hay từ chối: chỉnh `prompts/` hoặc bật fallback.
- Soạn cả trăm món: chuyển sang Batches API.
- Danh sách nguyên liệu đủ lớn để làm prompt quá dài: chỉ gửi các nhóm liên quan tới món.
