# ADR-038: Màn "Mâm hôm nay" như một trò chơi: bàn quay bữa ăn

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-11-06
- Liên quan: [SPEC §8.1-2, §8.2](../SPEC.md), [ADR-024](024-di-cho-danh-sach-cua-mam-dang-xem-tick-giu-theo-mam-gui-zalo.md), [ADR-025](025-che-do-nau-moi-buoc-mot-man-hen-gio-luu-theo-gio-ket-thuc.md), [ADR-033](033-giao-dien-mam-go-am.md), [ADR-034](034-mam-may-mon.md), [ADR-037](037-bua-khong-chi-mam-com.md)

## Bối cảnh

Chủ dự án muốn app "giống một game hơn": có chuyển động, hình bắt mắt, thú vị, dễ dùng, ít thao tác.

Trước đây, mỗi thẻ món có hai nút nhỏ (khoá, đổi), dưới các thẻ có hàng "Bớt món · 3 món · Thêm món", và "Chốt mâm" mở thẳng trang đi chợ.

Bản thử "Bàn quay bữa ăn" được vẽ trên canvas Claude Design (artboard `Game`) và chủ dự án đã duyệt trước khi sửa code.

## Quyết định

- **Đĩa trên mâm là nút.** Chạm một đĩa thì giữ món đó: đĩa có vòng vàng và ổ khoá vàng, thẻ món ghi "Đang giữ". Chạm lần nữa thì bỏ giữ.
  - Thẻ món không còn nút riêng.
  - Vuốt ngang thẻ vẫn đổi riêng món đó; thẻ đang giữ không đổi.
  - Dòng gợi ý nói cả hai cách.
- **Quay như máy xèng.** Trong lúc quay:
  - các đĩa không giữ chạy qua tên các món cùng loại (tối đa 8);
  - sau đó các món "đáp" xuống lần lượt, có nảy.
  Mâm có quầng sáng thở chậm, và nút "Quay!" thỉnh thoảng lắc nhẹ.
- **Loại bữa là bốn ô lớn có biểu tượng.** Ô đang chọn lún xuống và màu vàng.
- **Hai nút lớn:** "Quay!" (vàng) và "Chốt!" (xanh lá). Viền mực đậm và bóng cứng.
  - Nút, thẻ và chip cũng có viền mực và bóng cứng (`--shadow-pop`).
  - Nền trang có chấm như bàn cờ.
- **Chốt mâm thì ở lại màn mâm.** Chốt không còn mở trang đi chợ như SPEC §8.1-2 ghi trước đây.
  - Pháo giấy rơi một lần.
  - Một khung "Ngon rồi!" trồi lên ở đáy, với hai thẻ lớn:
    - "Đi chợ" (số thứ cần mua);
    - "Nấu ngay" (thời gian cả bữa). Thẻ này nảy ba lần rồi đứng yên, để ngón tay không phải đuổi theo.
  - Bên dưới có "Chấm bữa này" và "Quay mâm khác".
  - Khung này thay thanh nút "Đi chợ / Nấu" và khung "Đã chốt mâm hôm nay." cũ.
- **"Bớt món / Thêm món" rời màn mâm.** Số món của mâm chỉ còn chỉnh trong Cài đặt ("Mâm mấy món?", ADR-034).
  - Lý do: ít thao tác hơn trên màn chính.
  - `TrayDesk.AddDish`/`RemoveDish` vẫn còn trong Core.
- **Màu mới là token, cả sáng lẫn tối:**
  - `--color-ink`;
  - `--color-spin` và `--color-on-spin`;
  - `--color-go` và `--color-on-go`;
  - `--color-dot`;
  - `--color-glow`.
  Hai cặp chữ trên nền nút đạt tương phản 4,5 (có test).
- **Bớt chuyển động:** người dùng đã xin bớt chuyển động thì mọi hiệu ứng mới đều tắt, và không có pháo giấy.
- **Font:** vẫn dùng Be Vietnam Pro. Bản thử dùng Baloo 2 (OFL). Muốn đổi sang Baloo 2 thì phải tải font về để tự host, và việc tải cần chủ dự án cho phép trước.

## Hệ quả

- **Ít thao tác hơn:**
  - giữ món là một chạm vào đĩa to (khoảng 72px) thay vì nút 48px ở mép thẻ;
  - từ lúc chốt đến đi chợ hay nấu vẫn là một chạm.
- **Muốn đi chợ ngay sau khi chốt thì thêm một chạm.** Bù lại, người dùng thấy cả hai lối đi tiếp.
- **Test:**
  - E2E đi chợ qua thẻ "Đi chợ" của khung mới.
  - Bài kiểm trợ năng đợi các hiệu ứng có hồi kết chạy xong rồi mới đo, vì thẻ món đang hiện dần thì chưa phải thứ người dùng đọc.
