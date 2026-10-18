# ADR-004: CookPlanStep có ActiveMinutes, id "com" dành riêng cho cơm

- Trạng thái: Đã chấp nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §5.6, §10](../SPEC.md), [PLAN 1.1, 1.7](../PLAN.md)

## Bối cảnh

- SPEC §5.6 liệt kê đầu ra của `CookPlanScheduler` là `CookPlanStep { StartMinute, DishId, Text, TimerMinutes? }`.
- SPEC §5.6 và §10 đòi hỏi kiểm tra "không bao giờ có 2 bước active chồng lên nhau". Muốn kiểm tra điều đó từ chính kết quả lập lịch, cần biết mỗi bước chiếm tay người bao lâu. Bốn trường trên không cho biết: `TimerMinutes` chỉ là phần chạy không cần người, nên không nói phần cần tay người kéo dài bao lâu.
- Cơm là "món giả" (SPEC §5.6): có bước trong kế hoạch nhưng không có file dữ liệu, nên cần một `DishId` để gán cho bước đó.

## Quyết định

- Thêm `ActiveMinutes` (số phút cần tay người) vào `CookPlanStep`, đặt trước `TimerMinutes`. Một bước kết thúc ở `StartMinute + ActiveMinutes + (TimerMinutes ?? 0)`.
- Dùng `DishId = "com"` (hằng `CookPlan.RiceDishId`) cho bước của cơm. Id này dành riêng: món thật không được đặt `id` là `com`.
- SPEC §5.6 đã được cập nhật theo hai điểm trên, trong commit hiện thực PLAN 1.1 (`feat(core)`). ADR này bổ sung lý do sau đó.

## Phương án đã cân nhắc

1. **Giữ đúng bốn trường của SPEC.** Test "không chồng bước active" phải dò ngược từ `Dish.Steps`, khớp bước kế hoạch với bước món bằng thứ tự hoặc văn bản. Mong manh và dễ sai. Bị loại.
2. **Thêm `EndMinute` hoặc `PassiveMinutes` thay cho `ActiveMinutes`.** Cũng đủ để tính, nhưng `ActiveMinutes` là con số trực tiếp mà luật cần so sánh và khớp với `Step.ActiveMinutes` trong dữ liệu món. Chọn `ActiveMinutes`.
3. **`DishId` rỗng (null) cho cơm.** Giao diện và test phải đoán null nghĩa là cơm. Một hằng có tên rõ nghĩa hơn. Bị loại.

## Hệ quả

**Được**
- Các tính chất ở SPEC §10 (cơm bắt đầu đầu tiên, không chồng bước active, `serveHot` kết thúc trong 10 phút cuối) kiểm tra được trực tiếp trên `CookPlan`, không cần dữ liệu món.
- Chế độ nấu có sẵn thời lượng tay người của từng bước.

**Đổi lại**
- JSON của `CookPlanStep` có thêm một trường bắt buộc so với bản SPEC cũ. Kế hoạch nấu chưa được lưu ở đâu, nên chưa có dữ liệu cũ cần chuyển.
- Luật "món thật không dùng id `com`" chưa có code nào ép. `DataBuilder validate` (PLAN 2.2) nên từ chối `id = "com"`; việc đó chưa có trong SPEC §7.3.

## Khi nào xem lại

- Có bước nào cần thể hiện nhiều khoảng active tách rời trong cùng một bước.
- Cơm được mô hình hoá bằng dữ liệu thật (ví dụ cơm chiên, xôi) và id `com` bị trùng nghĩa.
