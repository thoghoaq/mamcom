# ADR-002: Làm Blazor WASM PWA trước, MAUI Blazor Hybrid sau

- Trạng thái: Đã chấp nhận
- Ngày: 2026-10-18
- Liên quan: [SPEC §2, §8.1, §9, §12](../SPEC.md), [PLAN 3.2, 3.10, backlog](../PLAN.md), [ADR-001](001-json-bundle-thay-sqlite.md), [ADR-003](003-khong-backend-v1.md)

## Bối cảnh

- Zalo là kênh nhắn tin chính của người dùng mục tiêu, nên chia sẻ danh sách đi chợ qua Zalo là tính năng bắt buộc (SPEC §2). Người đi chợ hộ nhận danh sách qua Zalo.
- Người dùng chính là người nấu chính 30-55 tuổi; ông bà cũng dùng. Cách dễ nhất để đưa app đến tay họ là một đường link gửi qua Zalo, không bắt cài từ cửa hàng ứng dụng.
- Stack của dự án là .NET và C# (CLAUDE.md). Logic nghiệp vụ nằm ở `MamCom.Core` và chạy được trong WebAssembly.
- Một số tính năng dựa vào API trình duyệt: Web Share, Screen Wake Lock, rung, service worker offline (SPEC §8.1). SPEC §12 ghi nhận iOS PWA có hạn chế về wake lock và share.
- Ý định dài hạn là có thêm bản app (MAUI), nên UI không nên gắn chặt vào một host.

## Quyết định

- v1 là **Blazor WebAssembly PWA**. `MamCom.Web` là host và chứa service worker.
- Toàn bộ giao diện nằm trong Razor Class Library `MamCom.UI`, để sau này `.NET MAUI Blazor Hybrid` dùng lại. MAUI nằm trong backlog sau v1.
- Phân phối bằng link, không qua app store.
- Bật trimming, nén (Brotli) và cache PWA ngay từ đầu (chưa có task trong PLAN giao việc cấu hình này; cần xác nhận đã bật trước khi đo ở PLAN 3.10). Thời gian tải đầu trên 4G phải được đo ở bước deploy staging (PLAN 3.10).
- Hướng thiết kế (giả định, chưa là task trong PLAN): các API riêng của trình duyệt đặt sau interface, giống cách `IUserStateStore` đã làm cho lưu trữ, để `MamCom.UI` không phụ thuộc trực tiếp vào host.
- Zalo Mini App là phương án phân phối sau v1. Trước khi chọn phải kiểm chứng Mini App có hỗ trợ Wake Lock và chạy offline hay không.

## Phương án đã cân nhắc

1. **PWA bằng framework JavaScript.** Lần tải đầu nhẹ hơn Blazor WASM. Nhưng thuật toán trong `MamCom.Core` phải viết lại hoặc nhân đôi, và lệch khỏi stack .NET của dự án. Bị loại.
2. **MAUI Blazor Hybrid ngay từ v1.** Phải qua app store, không chia sẻ được bằng link thuần qua Zalo. Giữ cho sau v1, tái dùng `MamCom.UI`.
3. **Zalo Mini App trước.** Chưa kiểm chứng Wake Lock và offline, mà chế độ nấu và đi chợ cần cả hai. Để sau v1.
4. **Blazor Server làm host.** Cần kết nối và máy chủ đang chạy, mâu thuẫn với yêu cầu chạy offline (PLAN 3.2, 3.6) và [ADR-003](003-khong-backend-v1.md). Blazor Server chỉ dùng cho `MamCom.Studio` chạy local.
5. **Blazor WASM PWA (chọn).**

## Hệ quả

**Được**
- Chia sẻ bằng link qua Zalo, phát hành không phải chờ duyệt từ store.
- Một codebase C#: Core, UI và công cụ nội dung (Studio có thể tái dùng component của `MamCom.UI`, PLAN 2.5).
- Chạy offline sau lần tải đầu nhờ service worker (kết hợp với [ADR-001](001-json-bundle-thay-sqlite.md)).
- Đường lên MAUI Blazor Hybrid không phải viết lại giao diện, với điều kiện `MamCom.UI` không gọi trực tiếp API trình duyệt (xem hướng thiết kế ở phần Quyết định, chưa là task trong PLAN).

**Đổi lại**
- Lần tải đầu nặng hơn app JavaScript. Giảm thiểu bằng trimming, Brotli, cache PWA (SPEC §12). Mục tiêu hiện tại trong PLAN 3.10 là tải đầu trên 4G không quá 5 giây sau khi bật trimming và nén; con số thực phải được đo và ghi vào nhật ký, không suy ra từ đây.
- Thời điểm người dùng nhận bản app mới phụ thuộc vào cơ chế cập nhật của service worker. SPEC và PLAN chưa có task riêng cho việc này (PLAN 3.2 chỉ nói về cập nhật bundle); đề xuất chủ dự án xử lý ở PLAN 3.2.
- iOS PWA bị hạn chế wake lock và share. Mọi tính năng này phải feature-detect và có phương án dự phòng (PLAN 3.4, 3.6, 3.7).
- Giả định chưa được kiểm chứng trong SPEC hay PLAN: link gửi qua Zalo mở và chạy đúng trong trình duyệt hoặc webview mà người nhận dùng. Cần quan sát trong beta (PLAN 5.2).
- Chia sẻ danh sách đi chợ dựa vào Web Share API, nếu không có thì copy vào clipboard (SPEC §6.2).

## Khi nào xem lại

- Phép đo ở PLAN 3.10 không đạt mục tiêu tải đầu dù đã bật trimming, nén và cache.
- Ghi chú beta (PLAN 5.2, `docs/beta-notes.md`) cho thấy hạn chế trên iOS làm hỏng luồng chính, như chế độ nấu hoặc chia sẻ qua Zalo.
- Bắt đầu làm MAUI Blazor Hybrid: kiểm tra `MamCom.UI` không còn phụ thuộc riêng vào trình duyệt.
- Cân nhắc Zalo Mini App: chỉ bắt đầu sau khi kiểm chứng được Wake Lock và offline.
