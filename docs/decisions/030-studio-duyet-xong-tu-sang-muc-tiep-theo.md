# ADR-030: Studio duyệt xong thì tự sang mục tiếp theo; chọn số liệu thì lưu luôn thay đổi đang sửa

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-28
- Liên quan: [SPEC §7.4](../SPEC.md), [PLAN 4.2, 4.3, 4.5](../PLAN.md), [ADR-014](014-studio-duyet-mon-sua-thang-file-json.md), [ADR-015](015-studio-xac-nhan-dinh-duong-nguyen-lieu.md)

## Bối cảnh

Chủ dự án bắt đầu xác nhận 138 nguyên liệu (PLAN 4.3) và thấy thao tác khó:

- Ứng viên USDA nằm cuối cột phải, sau thẻ dinh dưỡng và phần máy kiểm, nên phải cuộn xa mới thấy. Mỗi ứng viên lại là một thẻ cao, khó so ba ứng viên với nhau.
- Chọn xong thì trang đứng yên. Phải về danh sách, tìm nguyên liệu kế tiếp, rồi mở ra; lặp lại 138 lần. Duyệt 120 món (4.5) cũng vậy.
- Đã sửa đơn vị chợ (4.2) thì phải bấm "Lưu" trước, rồi mới bấm chọn được ứng viên (ADR-015).

## Quyết định

- **Tự sang mục tiếp theo.** Chọn xong ứng viên thì Studio mở nguyên liệu kế tiếp còn chưa có số liệu. Duyệt xong món thì mở món kế tiếp còn chờ duyệt (`draft` hoặc `validated`, gồm cả bản nháp).
  - Trang mới báo việc vừa làm, ví dụ "Đã ghi số liệu cho Bắp cải. Đây là nguyên liệu tiếp theo…". Thông báo hiện một lần.
  - "Kế tiếp" theo thứ tự của trang danh sách (theo mã), bắt đầu từ ngay sau mục hiện tại và vòng về đầu. Vì vậy mục đã bỏ qua sẽ gặp lại ở cuối vòng.
  - Hết mục chờ thì ở lại trang và nói đã xong hết. Riêng bản nháp cuối cùng vẫn sang trang món của nó như trước.
  - Thao tác không thành (file bị đổi ở chỗ khác, còn lỗi) thì ở lại.
- **Thanh tiến độ** dưới tiêu đề: "Còn N … chưa có số liệu / chờ duyệt", và nút "Bỏ qua, sang <tên> →".
- **Ứng viên lên đầu cột chính** và đứng cạnh nhau (lưới tự xuống hàng), nên đọc so được ba bộ số. Dòng "Số gốc của FDC…" chỉ nói một lần.
- **Chọn ứng viên khi còn thay đổi chưa lưu thì lưu thay đổi trước, rồi mới ghi số liệu.** Đây là lệch khỏi ADR-015, vốn bắt lưu riêng trước.
  - Vẫn cùng luật cũ: chỉ lưu khi máy kiểm không còn lỗi. Còn lỗi thì nút chọn tắt và báo "Sửa hết lỗi trước khi chọn số liệu".
  - Không có số nào đến từ màn hình: sáu số vẫn chép nguyên văn từ file gợi ý.
  - "Bỏ số liệu" vẫn cần không còn thay đổi chưa lưu.

## Phương án đã cân nhắc

1. **Nút "Lưu và sang tiếp" riêng, giữ nút chọn như cũ.** Thêm một nút cho việc mà người dùng làm 138 lần; chủ dự án muốn duyệt xong là tự chuyển.
2. **Một trang "hàng đợi" mới chỉ có ứng viên và đơn vị.** Gọn hơn, nhưng là màn hình thứ hai cho cùng một việc, và vẫn phải mở trang nguyên liệu khi cần sửa tên hay ảnh.
3. **Hỏi xác nhận trước khi chuyển.** Thêm một bấm mỗi lần mà không thêm an toàn: file đã ghi xong, và mục vừa làm mở lại được từ danh sách.

## Hệ quả

- Duyệt liên tục chỉ cần một bấm cho mỗi nguyên liệu (hai nếu có sửa) và một bấm cho mỗi món.
- Ai muốn làm theo thứ tự khác vẫn mở từ danh sách như cũ.
- Test Studio mới: hàng đợi (vòng về đầu, bỏ qua mục đang mở, không còn mục), thông báo một lần, chuyển sau khi chọn hoặc duyệt, ở lại khi hết hoặc khi thất bại, chọn kèm lưu thay đổi.
