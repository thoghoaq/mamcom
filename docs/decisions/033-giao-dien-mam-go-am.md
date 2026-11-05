# ADR-033: Giao diện "Mâm gỗ ấm"

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-11-05
- Liên quan: [SPEC §2, §8.1, §8.2](../SPEC.md), [ADR-019](019-he-thong-giao-dien-sang-toi-font-icon-o-giu-cho.md), [ADR-022](022-man-mam-hom-nay-phien-quay-loc-nhanh-chot-mam.md), [ADR-025](025-che-do-nau-moi-buoc-mot-man-hen-gio-luu-theo-gio-ket-thuc.md)
- Bản thiết kế: canvas Claude Design "MâmCơm — giao diện mới" (https://claude.ai/artifact/SEwB4E7Y6bB67inZjpSgTG), chủ dự án đã duyệt.

## Bối cảnh

Chủ dự án muốn giao diện đẹp và hợp người dùng hơn. Họ giao vai product owner cho Claude, và muốn có bản thiết kế để duyệt trước khi sửa app.

Người dùng chính (SPEC §2) là người nấu chính trong nhà, thường là mẹ/vợ 30–55 tuổi; ông bà cũng dùng. App được dùng ở 4 khoảnh khắc: quyết định lúc tan làm, đi chợ một tay ngoài nắng, đứng bếp tay ướt, chấm món sau bữa.

Tìm hiểu thêm cho thấy:
- người Việt dùng điện thoại nhiều; Zalo là kênh nhắn tin chính;
- các hướng dẫn thiết kế cho người lớn tuổi cùng khuyên vùng chạm từ khoảng 48px, chữ từ 16px, tương phản cao, ít cử chỉ khó, điều hướng đơn giản.

Bản cũ đúng các luật đó nhưng trông như một trang web:
- tên món là liên kết đỏ gạch chân;
- thẻ món cao, nút khoá và đổi nằm dưới ảnh;
- tóm tắt là một bảng chữ;
- màn nào cũng giống màn nào.

## Quyết định

- **Màn "Mâm hôm nay":**
  - Dưới tiêu đề có dòng ngày ("Thứ Năm, 5 tháng 11") và dòng "Nhà mình".
  - Hình **cái mâm nhìn từ trên xuống** (`TrayPlate`): bát cơm ở giữa, mỗi món một đĩa theo màu của ô; món đã khoá có viền màu nhấn. Hình chỉ để trang trí (`aria-hidden`); các thẻ bên dưới mới là nội dung. Khi quay, mâm xoay một vòng (tắt nếu người dùng xin bớt chuyển động).
  - Thẻ món gọn lại: đĩa tròn (ảnh hoặc ô giữ chỗ theo loại), loại món, tên món màu chữ (gạch chân khi trỏ hay focus), thời gian và kcal. Nút khoá và nút đổi xếp dọc ở mép phải, mỗi nút 48px.
  - Tóm tắt thành 4 ô số trên một nền, số to và nhãn nhỏ.
  - Nút "Mâm trước / Mâm sau" thành nút nhẹ trên một hàng.
- **Nút** bo tròn hẳn (viên thuốc); thanh nút đáy cao 56px, nút chính rộng hơn và nằm bên phải.
- **Đi chợ:** thanh tiến độ màu lá dưới "Đã mua 2/8", hàng cao hơn, ô tick màu lá.
- **Chế độ nấu** luôn ở nền tối (`data-theme="dark"` trên màn nấu, tràn hết bề ngang), cho bếp tối không bị chói. Chữ của bước lên 28px (`--font-size-cooking` từ 1,5rem lên 1,75rem), tên món thành nhãn màu.
- **Chi tiết món:** thời gian và kcal thành chip; các bước đánh số trong vòng tròn màu nhấn.
- **Logo:** bát canh màu nhấn cạnh "MâmCơm", chữ "Cơm" màu nhấn.
- **Thông báo "dữ liệu thử"** gọn lại (chỉ có ở bản dev).
- **Token mới:**
  - `--color-tray-rim`, `--color-tray-wood`, `--color-tray-rice` (sáng và tối);
  - `--shadow-raised`;
  - `--radius-large` (1rem) cho thẻ.

## Phương án đã cân nhắc

1. **Chỉ sửa CSS cho gọn, không có hình mâm.** Đẹp hơn nhưng vẫn là một danh sách. Ý tưởng cốt lõi là "quay cả mâm", và hình mâm làm ý đó hiện ra ngay. Trẻ con cũng thích bấm quay khi thấy mâm xoay.
2. **Bỏ nút đổi món, chỉ còn vuốt.** Gọn hơn, nhưng vuốt không dùng được bằng bàn phím hay trình đọc màn hình. Giữ nút.
3. **Chế độ nấu theo giao diện của máy.** Đúng với mọi màn khác, nhưng nền sáng ban đêm trong bếp gây chói. Màn nấu là một khoảnh khắc riêng (SPEC §2), nên có giao diện riêng.

## Hệ quả

- Màn chính dài hơn: thẻ món đầu tiên nằm dưới hình mâm, phải cuộn mới thấy. Thanh nút vẫn dính đáy.
- Đầu trang (header) của chế độ nấu vẫn theo giao diện của máy; phần còn lại luôn tối.
- Các luật cũ vẫn giữ: chữ từ 16px, vùng chạm 48px, tương phản đo bằng bài kiểm E2E ở cả hai nền, chữ gấp đôi không tràn ngang.
