# ADR-031: FdcImporter xếp "khớp từ khoá" lên trước "raw" và "đủ chất"

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-28
- Liên quan: [ADR-012](012-fdc-importer-goi-y-khong-tu-ap-dung.md), [SPEC §8 bước 3](../SPEC.md), [PLAN 4.3](../PLAN.md)

## Bối cảnh

Chủ dự án chọn ứng viên 1 cho các nguyên liệu (PLAN 4.3). Ở 31 nguyên liệu, ứng viên 1 là thực phẩm khác hẳn: nghệ thành "Chicken, ground, raw", sả thành "Beef, grass-fed, ground, raw", thì là thành "Pork, fresh, backfat, raw", vừng thành "Seeds, breadfruit seeds, raw".

Nguyên nhân nằm ở thứ tự của ADR-012: "mô tả có raw" và "đủ sáu chất" đứng trên "khớp từ khoá". Vì vậy một món sống, đủ chất, chỉ khớp một chữ ("ground", "raw") thắng đúng món được hỏi. Đúng món được hỏi thường ghi "ground"/"dried" hoặc thiếu một chất. Có khi món đúng nằm ngay ở vị trí 2: "Seeds, sesame seeds, whole, dried", "Water convolvulus, raw", "Sauce, hot chile, sriracha".

## Quyết định

- Thứ tự mới, mạnh nhất trước:
  1. khớp nhiều từ khoá hơn;
  2. mô tả có `raw`, rồi không nói gì, rồi có chữ chế biến;
  3. ít chất thiếu hơn;
  4. Foundation trước SR Legacy;
  5. điểm của FDC, rồi `fdcId`.
- Phải là đúng thực phẩm được hỏi rồi mới xét tới sống hay chín, đủ hay thiếu chất. Một ứng viên thiếu chất thì Studio vẫn cho thấy chất nào thiếu.
- Chạy lại `suggest --refresh` cho các nguyên liệu còn `placeholder`. Gợi ý của những nguyên liệu đã xác nhận giữ nguyên, vì số đã chọn đến từ chúng.

## Phương án đã cân nhắc

1. **Chỉ sửa từ khoá tiếng Anh.** Từ khoá đã đúng ("Spices, turmeric, ground"); cái sai là thứ tự xếp. Bị loại.
2. **Không đếm chữ `raw` khi đếm từ khoá khớp.** Tinh hơn một chút nhưng thêm một luật. Bước 2 đã xử lý chữ `raw` rồi. Để lại nếu sau này cần.

## Hệ quả

- Ứng viên 1 hợp lý hơn với nguyên liệu khô, gia vị, nước chấm, vốn hay ghi "dried", "ground" hoặc thiếu một chất.
- Người vẫn phải xem: ứng viên khớp chữ nhưng đã chế biến (ví dụ "dried") có thể đứng trên một món sống khớp ít chữ hơn.
