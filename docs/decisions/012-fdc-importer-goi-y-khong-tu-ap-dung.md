# ADR-012: FdcImporter gợi ý dinh dưỡng từ USDA và không bao giờ tự áp dụng

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án xác nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §6.3, §7.4](../SPEC.md), [PLAN 2.3, 2.6](../PLAN.md), [ADR-006](006-dinh-duong-null-lan-truyen-va-ty-le-nang-luong.md), [ADR-010](010-json-schema-thu-vien-va-pham-vi.md)

## Bối cảnh

PLAN 2.3 yêu cầu `FdcImporter` tìm USDA FoodData Central theo tên tiếng Anh, trả ba ứng viên kèm lý do, ghi vào `data/_suggestions/nutrition/<id>.json` và không tự ghi vào file nguyên liệu. CLAUDE.md cấm số dinh dưỡng không có nguồn tham chiếu. Còn ngỏ:

- Tên tiếng Anh lấy ở đâu: file nguyên liệu không có.
- Xếp hạng thế nào, và năng lượng lấy từ chất nào (Foundation Foods không có chất "Energy" thường).
- Giữ khoá API và giới hạn request ra sao.

## Quyết định

- **Tên tiếng Anh nằm trong `data/plan/ingredient-names-en.csv`** (cột `id`, `english`). Đó là từ khoá tìm kiếm, không phải dữ liệu, nên không thêm trường vào file nguyên liệu (làm vậy phải đổi kiểu `Ingredient` và schema). File này do tôi soạn cho 33 nguyên liệu seed; chủ dự án sửa được.
- **Chỉ tìm trong Foundation và SR Legacy**: bảng giá trị trên 100g của thực phẩm tươi sống, khớp với cách SPEC §6.3 cộng từ nguyên liệu sống. Thực phẩm Branded (sản phẩm) và Survey (món ăn) bị loại. Ghi nhận khi thử API: giá trị `dataType` có dấu ngoặc (`Survey (FNDDS)`) làm cổng trả 400 nên không dùng.
- **Thứ tự xếp hạng**, mạnh nhất trước:
  1. mô tả có `raw`, rồi không nói gì, rồi có chữ chế biến (`cooked`, `canned`, `dried`…; chữ nào chính câu tìm kiếm nêu thì không bị tính);
  2. ít chất còn thiếu hơn (một chất thiếu làm tổng của cả món thành không biết, ADR-006);
  3. khớp nhiều từ khoá hơn (hỏi "white" thì gạo lứt đứng sau, dù là dữ liệu mới hơn);
  4. Foundation trước SR Legacy;
  5. điểm liên quan của chính FDC, rồi `fdcId` nhỏ hơn, để kết quả không phụ thuộc thứ tự FDC trả về.
- **Năng lượng** lấy từ chất `Energy` (1008); không có thì `Energy (Atwater Specific Factors)` (2048), rồi `Energy (Atwater General Factors)` (2047). Ứng viên ghi tên chất đã dùng (`kcalSource`). Một chất chỉ được nhận khi đơn vị đúng (kcal, g, mg): năng lượng tính bằng kJ không bao giờ bị đọc thành kcal. Chất FDC không nêu là `null`, không bao giờ 0.
- **Số liệu giữ nguyên của FDC**, không làm tròn, kèm `fdcId` và đường dẫn `detailsUrl` để người kiểm. `per100g` dùng đúng tên trường của `nutritionPer100gEdible` nên Studio (PLAN 2.6) chép thẳng. Chỉ khi người chọn trong Studio thì số mới vào file nguyên liệu, với `nutritionSource.db = usda-fdc`.
- **File gợi ý** có schema `nutrition-suggestion` (ghi dòng cuối bằng LF, thụt hai khoảng trắng như các file dữ liệu khác). Chỉ có một ngày (`retrievedAt`), không có giờ. Ghi vào file tạm rồi đổi tên, nên tiến trình bị tắt giữa chừng không để lại nửa file. Nguyên liệu đã có file gợi ý thì bỏ qua để đỡ tốn hạn mức, trừ khi có `--refresh`. Không tìm thấy gì vẫn ghi file với danh sách rỗng, để biết đã tìm rồi.
- **Khoá API** đặt bằng `dotnet user-secrets set "Fdc:ApiKey" "<khoá>" --project tools/MamCom.FdcImporter`; không có thì dùng `DEMO_KEY`. Khoá đi trong header `X-Api-Key`, không bao giờ trong URL, và không được in ra, kể cả khi lỗi.
- **Tôn trọng giới hạn:**
  - đọc `X-RateLimit-Remaining`; hết thì dừng trước lần gọi kế tiếp và giữ file đã ghi (DEMO_KEY qua cổng này báo giới hạn 10, nên chạy lại sau để làm tiếp phần còn lại);
  - nghỉ giữa hai lần gọi (mặc định 1 giây), trần số lần gọi mỗi lượt (mặc định 25);
  - HTTP 429: hết hạn mức thì dừng; còn thì chờ theo `Retry-After` nếu không quá 60 giây (`--max-wait-seconds`), thử lại tối đa hai lần, không thì dừng;
  - khoá bị từ chối (401, 403) dừng ngay; lỗi khác của một nguyên liệu chỉ làm hỏng nguyên liệu đó;
  - Ctrl+C dừng giữa hai lần gọi.
- **Câu chữ** là tiếng Việt (`Messages.cs`); mã thoát 0 là xong, 1 là có lỗi hoặc dừng sớm, 2 là gõ sai lệnh.

## Phương án đã cân nhắc

1. **Thêm `englishName` vào file nguyên liệu.** Gọn hơn nhưng đổi mô hình lõi và schema chỉ vì một công cụ. Bị loại.
2. **Cho tìm cả Branded và Survey.** Nhiều kết quả hơn nhưng phần lớn là sản phẩm đóng gói và món đã nấu, không cộng được từ nguyên liệu sống. Bị loại.
3. **Tự chọn ứng viên đứng đầu và ghi vào nguyên liệu.** Trái CLAUDE.md và yêu cầu của PLAN: người xác nhận. Bị loại.
4. **Truyền khoá bằng tham số `api_key`.** Khoá lọt vào URL và nhật ký. Bị loại.
5. **Chờ cho đến hết giờ khi bị giới hạn.** Với DEMO_KEY là cả giờ; treo lệnh vô ích. Bị loại.

## Hệ quả

**Được**
- Mỗi nguyên liệu có vài ứng viên có nguồn, có lý do xếp hạng và đường dẫn kiểm tra, mà không con số nào vào dữ liệu khi chưa có người duyệt.
- Chạy lại được: lượt sau chỉ hỏi phần chưa có.

**Đổi lại**
- Xếp hạng là cách đoán: ứng viên hạng 2 và 3 có khi không liên quan (hỏi cà chua ra chuối); người chọn đọc lý do và số từ khoá khớp.
- Có nguyên liệu FDC không có hoặc có nhưng khác loại (cá lóc, rau ngót). Bảng thành phần thực phẩm Việt Nam mới là nguồn ưu tiên số 1 của SPEC §6.3 và công cụ này không đọc bảng đó; phần này là việc của người.
- Với DEMO_KEY chỉ chạy được vài nguyên liệu mỗi lượt; muốn chạy cả 33 cần khoá riêng (miễn phí đăng ký ở USDA).

## Khi nào xem lại

- Có thêm nguồn (ASEAN, bảng Việt Nam) cần cùng dạng gợi ý.
- Ứng viên không liên quan làm người duyệt mất thời gian: cân nhắc bỏ ứng viên khớp quá ít từ khoá.
- USDA đổi API hoặc giới hạn.
