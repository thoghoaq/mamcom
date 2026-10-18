# ADR-001: Dữ liệu dạng JSON bundle, không dùng SQLite ở v1

- Trạng thái: Đã chấp nhận
- Ngày: 2026-10-18
- Liên quan: [SPEC §3, §7.3, §7.6, §9](../SPEC.md), [PLAN 0.4, 2.2, 3.2](../PLAN.md), [ADR-002](002-blazor-wasm-pwa-truoc.md), [ADR-003](003-khong-backend-v1.md)

## Bối cảnh

- Nội dung của app gồm khoảng 250 nguyên liệu và 120 món (SPEC §3). SPEC §9 ước lượng dữ liệu này nhỏ, dưới 2MB.
- Bản phân tích ban đầu đề xuất SQLite. Quyết định này thay thế đề xuất đó.
- SPEC ghi nhận SQLite chạy trong WebAssembly là phức tạp. ADR này không đo đạc thêm về độ phức tạp đó.
- Nội dung do người và công cụ cùng chỉnh sửa: `MamCom.Studio` đọc và ghi trực tiếp vào `/data`, và PLAN 2.5 yêu cầu diff Git sạch, đúng format sau khi duyệt một món.
- Bản phát hành cần cập nhật dữ liệu mà không phải phát hành lại app (SPEC §7.6).

## Quyết định

- Nguồn sự thật của nội dung là các file JSON trong `/data` (`ingredients/`, `dishes/`, `config/`). Không dùng SQLite ở v1.
- `MamCom.DataBuilder` kiểm tra các file theo luật §7.3 rồi đóng gói thành một file `dist/data-bundle.json`, có `version` là hash nội dung.
- `MamCom.Data` (`BundleLoader`) nạp bundle vào bộ nhớ. `MamCom.Core` nhận dữ liệu đã nạp, không tự đọc file hay truy vấn cơ sở dữ liệu.
- Service worker cache `data-bundle.json`. Khi online, app so `version` và tải bản mới nếu có (SPEC §7.6, PLAN 3.2).
- Cách phát hành bundle và cách so `version` chưa được SPEC hay PLAN 3.2 quy định. Giả định của ADR này: bundle là file tĩnh phục vụ riêng, không nằm trong danh sách asset mà service worker precache cùng bản app. Service worker mẫu hiện có (`src/MamCom.Web/wwwroot/service-worker.published.js`) cache-first mọi file `.json` trong assets manifest và đặt tên cache theo version của manifest. Nếu bundle nằm trong `wwwroot` và đi vào manifest, nó bị đóng băng theo bản app và lợi ích "không cần phát hành lại app" không còn đúng.
- Cách so `version` mà không tải cả bundle (ví dụ một file `version` nhỏ đi kèm, hoặc conditional request) được chốt ở PLAN 3.2; ADR này được cập nhật khi đó.
- Trạng thái người dùng (gia đình, lịch sử, yêu thích) không nằm trong bundle; xem [ADR-003](003-khong-backend-v1.md).

## Phương án đã cân nhắc

1. **SQLite chạy trong WebAssembly.** Cho phép truy vấn SQL, nhưng thêm phụ thuộc và độ phức tạp ở tầng chạy, trong khi dữ liệu nhỏ. Bị loại cho v1.
2. **Nhiều file JSON tải riêng lẻ lúc chạy.** Với khoảng 250 nguyên liệu và 120 món (SPEC §3), mỗi file là một request và một mục cache riêng, và không có một `version` duy nhất cho cả tập để đối chiếu offline. Bản release còn phải lọc món dưới `reviewed` và placeholder (SPEC §7.2, §7.3), việc đó cần một bước tạo bản release; chỉ file đã qua DataBuilder mới được phát hành. Bị loại.
3. **Một file JSON bundle do DataBuilder tạo (chọn).** Một đơn vị để cache, đánh version và kiểm tra, khớp với yêu cầu validate trước khi phát hành (`build --release`).

## Hệ quả

**Được**
- Dữ liệu là văn bản, xem được diff trong Git; Studio ghi file JSON và người duyệt thấy đúng thay đổi.
- Lỗi schema, id trùng, `ingredientId` không tồn tại bị chặn ở `validate` và `build`. Placeholder dinh dưỡng và món dưới `reviewed` chỉ bị chặn ở `build --release` (build dev cho phép placeholder), nên không lọt vào bản phát hành.
- `MamCom.Core` giữ thuần logic, không IO, dễ test với dữ liệu fixture.
- Một file duy nhất để cache offline và để đối chiếu `version`.
- Cập nhật nội dung không cần phát hành lại app, với điều kiện bundle được phục vụ ngoài phần precache của app (xem Quyết định).

**Đổi lại**
- Toàn bộ bundle phải được tải và parse khi khởi động; mỗi lần dữ liệu đổi, máy khách tải lại cả file (SPEC chưa quy định cập nhật từng phần). Mỗi lần mở app khi online còn có thêm một lần kiểm tra `version`.
- Không có truy vấn SQL linh hoạt. Việc lọc, ghép và chỉ mục dữ liệu phải viết bằng code C# trong Core.
- Kích thước bundle trở thành chỉ số cần theo dõi, vì quyết định dựa trên giả định dưới 2MB. `data-report.md` (SPEC §7.3) là nơi tự nhiên để ghi kích thước này, nhưng việc đó chưa có trong PLAN. Đề xuất cho chủ dự án: thêm vào PLAN việc ghi kích thước bundle vào `data-report.md`, và ghi riêng thời gian tải và parse bundle trong phép đo ở PLAN 3.10 (hiện chỉ đo tổng thời gian tải đầu).
- Ảnh không nằm trong JSON. PLAN 2.8 tạo ảnh WebP thumb và full; PLAN 3.2 cache ảnh thumb. Cách cache hoặc tải ảnh full (SPEC §7.5: lazy-load) chưa có task trong PLAN.

## Khi nào xem lại

- Bundle thực tế vượt giả định dưới 2MB của SPEC §9. SPEC không nói ngưỡng này tính trước hay sau nén; khi đo, ghi cả hai con số.
- Phép đo tải đầu trên 4G ở PLAN 3.10 cho thấy tải hoặc parse bundle là phần chậm đáng kể (cần đo riêng phần này, xem mục Đổi lại).
- Một mục trong backlog sau v1 cần truy vấn phức tạp trên dữ liệu, ví dụ thực đơn tuần hoặc gợi ý theo "tủ lạnh còn gì".
- Có backend và cơ sở dữ liệu cho đồng bộ cả nhà (xem [ADR-003](003-khong-backend-v1.md)). Khi đó nội dung và trạng thái người dùng có thể cần được xem xét lại cùng nhau.
- Làm .NET MAUI Blazor Hybrid ([ADR-002](002-blazor-wasm-pwa-truoc.md)): rào cản WebAssembly không còn áp dụng ở bản native, nhưng chỉ đổi khi có lý do ngoài lý do đó.
