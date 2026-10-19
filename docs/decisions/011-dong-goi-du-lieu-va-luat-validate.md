# ADR-011: Đóng gói dữ liệu và các luật của `validate`

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án xác nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §7.3, §7.6](../SPEC.md), [PLAN 2.2](../PLAN.md), [ADR-010](010-json-schema-thu-vien-va-pham-vi.md)

## Bối cảnh

PLAN 2.2 yêu cầu `validate`, `build` và `build --release`, nhưng SPEC §7.3 và §7.6 để ngỏ nhiều chỗ:

- CLAUDE.md viết `build --release` là "chỉ món reviewed, không placeholder", còn SPEC §7.3 và PLAN viết release **lỗi** khi còn placeholder hoặc món dưới `reviewed`. Hai cách hiểu khác nhau: lọc bớt hay từ chối.
- Bundle chưa có cấu trúc; app cần cả config chứ không chỉ nội dung.
- Chưa nói ảnh nằm ở đâu để kiểm "thiếu file", "nguyên liệu không xuất hiện ở bước nào" tính theo công thức hay theo nguyên liệu, và ngưỡng của các cảnh báo nằm ở đâu (CLAUDE.md cấm hằng số nghiệp vụ trong code).

## Quyết định

- **Release từ chối, không lọc.** Còn một nguyên liệu `placeholder` hoặc một món dưới `reviewed` thì `build --release` thất bại và không ghi bundle. Cách này khớp SPEC §7.2 ("bản release chỉ gồm món từ `reviewed` trở lên" đúng một cách tầm thường) và tránh một bản phát hành âm thầm thiếu món. Món chưa xong để ở `data/dishes/_drafts/`, thư mục này không được đọc.
- **Cấu trúc bundle** là kiểu `DataBundle` trong `MamCom.Core` (để cả DataBuilder và `MamCom.Data` dùng chung): `version`, `isRelease`, `config` (bốn file config chạy), `ingredients`, `dishes`, hai danh sách xếp theo `id`. Không có ngày giờ, nên cùng dữ liệu cho cùng từng byte.
- **`version`** là 16 chữ số hex đầu của SHA-256 trên JSON của bundle khi `version` rỗng. `isRelease` nằm trong phần được băm, nên bản dev và bản release không bao giờ trùng version. `VersionMatchesContent()` cho app biết file có bị sửa tay không.
- **`isRelease`** để app dán nhãn "dữ liệu thử" khi chạy với bundle dev.
- **Ngưỡng của các cảnh báo** nằm trong `data/config/validation-defaults.json` (kcal mỗi suất theo loại món, gram nguồn đạm của món mặn, natri tối đa), có schema, là cấu hình của riêng DataBuilder. Giá trị khởi điểm là các con số của SPEC §7.3.
- **Chi tiết các luật SPEC chưa nói:**
  - Ảnh: `image.thumb` và `image.full` là tên file nằm trong `data/images/out/`; tên chứa đường dẫn là lỗi `image-path`.
  - "Nguyên liệu không xuất hiện ở bước nào" tính theo nguyên liệu: không bước nào của món nào nhắc đến nó (trừ nguyên liệu gạo của `riceIngredientId`). Thêm hai cảnh báo cùng loại cho từng món: dòng công thức không có bước nào dùng, và bước nhắc nguyên liệu không có trong công thức.
  - Gram nguồn đạm của món mặn là tổng gram mọi nguyên liệu có `proteinSource` khác `null`, không chỉ nguồn của món (thịt kho trứng tính cả trứng).
  - kcal, natri chỉ được xét khi mọi nguyên liệu của món có số; số `null` không bị coi là 0 (ADR-006).
  - Đơn vị cân (để cảnh báo `showApproxGrams`) nhận biết theo nhãn: g, gam, kg, lạng, lượng, cân.
  - Mùa của món phải có trong `seasonByMonth` của `spinner-defaults.json` hoặc là `all`; id mùa không cố định trong code.
  - Thứ tự `order` của bước phải là 1 đến n không trùng; thứ tự khai báo trong mảng không quan trọng.
  - Tên file phải bằng `id`; id trùng giữa hai file là lỗi.
  - Một file nguyên liệu hỏng chỉ báo một lần, ở chính file đó; các món dùng nó không bị báo thêm "nguyên liệu không tồn tại".
- **Bản release** coi mọi cảnh báo là lỗi.
- **Dòng lệnh:** `validate [--data] [--release]`, `build [--data] [--out] [--release]`; mặc định `--data data`, `--out dist`. Mã thoát 0 là sạch, 1 là dữ liệu còn lỗi, 2 là gõ sai lệnh. Lỗi in ra luồng lỗi, cảnh báo ra luồng thường. `build` luôn ghi `data-report.md`; chỉ ghi `data-bundle.json` khi không có lỗi, và xoá bundle cũ trong thư mục đầu ra khi thất bại, để bản cũ không bị lấy nhầm là kết quả của lần này.
- **Câu chữ** của các lỗi do DataBuilder tự sinh là tiếng Việt, gom ở `Messages.cs`; lỗi schema giữ nguyên văn tiếng Anh của thư viện.
- **CI** chạy `validate` và `build` (dev) trên mọi lần push.

## Phương án đã cân nhắc

1. **Release lọc bớt món chưa duyệt.** Tiện cho chủ dự án khi mới duyệt được một phần, nhưng một bản phát hành thiếu món mà không ai hay là lỗi khó thấy. Dùng `_drafts/` thay thế. Bị loại.
2. **Ngưỡng cảnh báo hard-code trong DataBuilder.** Đơn giản hơn nhưng trái CLAUDE.md. Bị loại.
3. **`DataBundle` nằm trong DataBuilder.** App không tham chiếu được công cụ, nên phải chép kiểu. Bị loại.
4. **`version` là số tăng dần hoặc ngày giờ.** Cần trạng thái ngoài dữ liệu và làm bản dựng không tái lập được. Bị loại.

## Hệ quả

**Được**
- Bản dựng tái lập được: cùng dữ liệu, cùng byte, cùng version; app nhận ra bundle mới chỉ bằng so version.
- Lỗi chỉ ra file và vị trí; một file hỏng không kéo theo hàng loạt lỗi giả.

**Đổi lại**
- Tới khi có nguồn dinh dưỡng thật và món được duyệt, `build --release` luôn thất bại; chỉ bản dev dùng được.
- Nhãn đơn vị cân nằm trong code; nhãn mới (ví dụ "yến") cần thêm vào danh sách.
- Tên file ảnh phẳng (không thư mục con) trong `data/images/out/`; ImageTool (PLAN 4.x) phải theo quy ước này.

## Khi nào xem lại

- Chủ dự án muốn phát hành từng phần (lọc thay vì từ chối).
- ImageTool cần thư mục con cho ảnh.
- Beta cho thấy các ngưỡng cảnh báo báo nhiều lỗi giả.
