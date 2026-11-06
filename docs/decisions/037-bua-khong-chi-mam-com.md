# ADR-037: Không chỉ mâm cơm: món một tô, lẩu, bữa sáng nhanh

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-11-06
- Liên quan: [SPEC §1, §5.1, §7.2, §8.1](../SPEC.md), [ADR-032](032-claude-duyet-mon-thay-chu-du-an.md), [ADR-034](034-mam-may-mon.md), [ADR-036](036-ten-app-bowlo.md)

## Bối cảnh

App quay mâm cơm: cơm cùng canh, món mặn, rau. Ngày nào cũng có cơm. Chủ dự án muốn Bowlo "không chỉ làm mỗi cơm nữa", và đã chọn:
- thêm cả ba loại bữa: món một tô, lẩu, bữa sáng nhanh;
- người dùng chọn loại bữa trước khi quay;
- Claude được chạy RecipeDrafter để soạn món mới.

## Quyết định

- **Ba loại món mới** (`DishSlot`; giá trị trong file món là `to`, `lau`, `sang`):
  - **Món một tô:** phở, bún, mì, miến, hủ tiếu, cháo, cơm tấm…;
  - **Lẩu:** ăn chung một nồi;
  - **Bữa sáng nhanh:** bánh mì, xôi, cháo…
  - Một suất của các món này là cả bữa của một người, có cả phần tinh bột (bánh phở, bún, mì, bánh mì, xôi) và rau ăn kèm.
  - Lượng nấu theo số suất cơ bản của nhà, như canh.
- **Loại bữa** (`MealKind`: `mam`, `to`, `lau`, `sang`).
  - Màn mâm có hàng "Hôm nay ăn: Mâm cơm · Một tô · Lẩu · Bữa sáng", mặc định là mâm cơm.
  - Chọn một loại thì quay ngay một bữa loại đó, với hiệu ứng như "Quay mâm". Các dấu khoá của mâm cơm không sang loại bữa khác.
  - Lựa chọn được giữ trong ngày (`TraySession.MealToday`, `MealDate`); sang ngày khác lại là mâm cơm.
  - Bữa không phải mâm cơm có đúng một món. "Thêm món / Bớt món" (ADR-034) chỉ dành cho mâm cơm.
- **Bữa có cơm khi và chỉ khi có món của mâm** (canh, mặn, rau), hoặc không có món nào (`HasRice`). Không cần thêm cờ: bữa một tô, lẩu hay bữa sáng tự bỏ
  - gạo khỏi danh sách đi chợ;
  - bước nấu cơm khỏi trình tự nấu;
  - cơm khỏi kcal của mỗi người;
  - dòng gạo khỏi phần tóm tắt.
- **Hình mâm:** bữa một món không có bát cơm ở giữa; đĩa của món nằm giữa và to hơn.
- **Màu và biểu tượng riêng** cho từng loại, cả nền sáng lẫn tối:
  - món một tô: tím, bát có đũa;
  - lẩu: đỏ, nồi lẩu;
  - bữa sáng: hổ phách, ổ bánh mì.
  Ba biểu tượng này vẽ theo phong cách Tabler.
- **Luật kiểm dữ liệu:** khoảng kcal mỗi suất cho từng loại mới (`validation-defaults.json`: món một tô 300–900, lẩu 300–1000, bữa sáng 200–700).
- **Dữ liệu đợt đầu:**
  - 30 món mới trong `dish-list.csv`: 16 món một tô, 6 lẩu, 8 bữa sáng.
  - RecipeDrafter soạn, Claude đọc và duyệt (ADR-032).
  - 16 nguyên liệu mới (bánh phở, bún, mì trứng, hủ tiếu, bánh canh, gạo nếp, đậu xanh, bánh mì, xương ống, chả lụa, pa tê, bò viên, ruốc, quế, hồi, thảo quả) chưa có số liệu, đã có gợi ý USDA để chủ dự án xác nhận.

## Phương án đã cân nhắc

1. **Chỉ thêm "ăn kèm bún/bánh mì" thay cơm.** Nhỏ hơn, nhưng không ra được phở, lẩu hay xôi, là những bữa nhà Việt hay ăn.
2. **App tự xen kẽ loại bữa theo tỉ lệ.** Ít bấm hơn, nhưng người dùng không đoán được mình sẽ nhận gì; chủ dự án chọn chọn trước khi quay.
3. **Mỗi loại bữa một màn riêng.** Nhân đôi giao diện cho cùng một việc: quay, khoá, đổi, chốt, đi chợ, nấu đều giống nhau.

## Hệ quả

- SPEC không còn chỉ nói về mâm cơm. Tên Bowlo (cái bát) hợp với hướng này.
- Lọc nhanh "Dưới 45 phút" vẫn tính từ nồi cơm 35 phút; với bữa một món, giới hạn này rộng hơn cần thiết, có thể chỉnh sau beta.
- Món của loại mới chỉ ra khi có dữ liệu. Thiếu món thì máy quay báo ô không điền được, như với mâm cơm.
