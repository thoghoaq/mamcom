# ADR-003: Không có backend ở v1 (beta dùng event log cục bộ có nút xuất)

- Trạng thái: Đã chấp nhận
- Ngày: 2026-10-18
- Liên quan: [SPEC §1, §9, §11](../SPEC.md), [PLAN 3.2, 5.1, 5.3, 6.2, backlog](../PLAN.md), [ADR-001](001-json-bundle-thay-sqlite.md), [ADR-002](002-blazor-wasm-pwa-truoc.md)

## Bối cảnh

- SPEC §1 đưa tài khoản, đồng bộ nhiều thiết bị, mạng xã hội và ảnh do người dùng đăng ra ngoài phạm vi v1.
- Các thuật toán (khẩu phần, quay mâm, co giãn nguyên liệu, đi chợ, dinh dưỡng, lập lịch nấu) nằm trong `MamCom.Core`, là logic thuần không IO, và chạy được trên máy khách ([ADR-002](002-blazor-wasm-pwa-truoc.md)).
- Nội dung đến từ bundle tĩnh ([ADR-001](001-json-bundle-thay-sqlite.md)). Hosting dự kiến là Azure Static Web Apps, gói free (CLAUDE.md, SPEC §13).
- Trang "Giới thiệu và nguồn dữ liệu" (PLAN 6.2) phải nói rõ dữ liệu chỉ lưu trên máy.
- Beta chỉ có 5 người nấu chính. SPEC §11 yêu cầu event log cục bộ, ẩn danh, không thu thập thông tin cá nhân, có nút "Xuất phản hồi".

## Quyết định

- v1 không có backend riêng. Không có tài khoản, không có API ứng dụng, không có cơ sở dữ liệu máy chủ.
- Mọi tính toán chạy trên thiết bị của người dùng.
- Trạng thái người dùng (gia đình, lịch sử, yêu thích, phản hồi) lưu trong localStorage qua interface `IUserStateStore` (`MamCom.Data`). Interface giúp đổi nơi lưu về sau mà không sửa Core và UI.
- Kênh dữ liệu từ máy chủ xuống máy khách chỉ có một chiều: tải nội dung tĩnh (`data-bundle.json` và ảnh).
- Trong beta, app ghi một event log cục bộ, ẩn danh (PLAN 5.1): quay, khoá, chốt, mở đi chợ, mở chế độ nấu, phản hồi, thời gian từ mở app đến chốt. Log không chứa thông tin cá nhân; cần bảo đảm tên thành viên không bị đưa vào log.
- Nút "Xuất phản hồi" cho ra JSON và text. Người thử tự gửi file xuất cho chủ dự án ngoài app; app không tự gửi dữ liệu đi đâu.
- Đồng bộ cả nhà (ASP.NET Core API + PostgreSQL) nằm trong backlog sau v1.

## Phương án đã cân nhắc

1. **Backend đồng bộ cả nhà ngay từ v1** (ASP.NET Core API + PostgreSQL, như backlog). Kéo theo tài khoản, quyền riêng tư và vận hành máy chủ, trong khi SPEC đã loại tài khoản khỏi v1. Để sau v1.
2. **Thu thập log beta từ xa** (dịch vụ phân tích hoặc endpoint nhận log). Gom dữ liệu tự động hơn, nhưng cần một nơi nhận dữ liệu, trái với "không backend" và với cam kết dữ liệu chỉ lưu trên máy. Bị loại.
3. **Không backend, log cục bộ có nút xuất (chọn).**

## Hệ quả

**Được**
- Không phải vận hành, bảo mật hay trả phí cho máy chủ ứng dụng; chỉ cần hosting tĩnh.
- Chạy offline sau lần tải đầu: không có cuộc gọi API nào cần mạng ngoài việc kiểm tra version bundle. Theo PLAN 3.2, chỉ bundle và ảnh thumb được cache; ảnh full 800px là lazy-load (SPEC §7.5) và việc dùng chúng offline chưa được SPEC hay PLAN quy định (xem [ADR-001](001-json-bundle-thay-sqlite.md)).
- Quyền riêng tư đơn giản: dữ liệu gia đình không rời khỏi máy.
- `MamCom.Core` test được với `IRandomSource` và `IClock`, không cần dựng môi trường máy chủ.

**Đổi lại**
- Không đồng bộ giữa các thiết bị. SPEC v1 chưa có cơ chế chuyển trạng thái sang máy khác.
- localStorage thuộc về trình duyệt và có thể bị xoá bởi trình duyệt hoặc bởi người dùng (kể cả qua "xoá dữ liệu" trong Cài đặt). Khi đó lịch sử và khẩu vị đã học mất, và không có bản sao nào để khôi phục.
- Việc thu thập số liệu beta là thủ công. Các chỉ số ở SPEC §11, nhất là giữ chân ngày 7 và ngày 30, chỉ tính được từ những file mà người thử đã xuất, nên phụ thuộc vào việc họ nhớ xuất và gửi.
- Beta chỉ kéo dài 1–2 tuần (PLAN 5.2), nên giữ chân ngày 30 không quan sát được và ngày 7 chỉ quan sát được ở người bắt đầu dùng sớm. Cần kéo dài beta hoặc chấp nhận bỏ chỉ số này; đây là quyết định của chủ dự án.
- Không có kênh để sửa dữ liệu người dùng từ xa hoặc biết lỗi xảy ra ngoài những gì người thử báo.

## Khi nào xem lại

- Bắt đầu làm mục "Đồng bộ cả nhà" trong backlog sau v1; khi đó cần ADR mới về tài khoản, lưu trữ và quyền riêng tư.
- Phân tích beta (PLAN 5.3) cho thấy không đủ dữ liệu để trả lời các chỉ số ở SPEC §11. Phân biệt hai nguyên nhân: luồng xuất file thủ công không đáng tin (người thử quên xuất), hoặc beta quá ngắn so với chỉ số cần đo.
- Beta hoặc ghi chú người dùng cho thấy nhu cầu dùng chung trạng thái giữa nhiều thiết bị, hoặc mất dữ liệu do localStorage bị xoá là vấn đề thực tế.
- Có mục trong backlog cần máy chủ, như ảnh mâm cơm do người dùng đăng.
- Quyết định thương mại hoá (PLAN 6.3), nếu phương án được chọn đòi hỏi tài khoản hoặc thanh toán.
