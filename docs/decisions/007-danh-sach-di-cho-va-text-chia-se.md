# ADR-007: Danh sách đi chợ và text chia sẻ: các chỗ SPEC §6.2 chưa nói

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án xác nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §6.2, §7.1](../SPEC.md), [PLAN 1.5](../PLAN.md), [ADR-005](005-marketunit-default-va-integer-scale.md)

## Bối cảnh

SPEC §6.2 cho công thức và một ví dụ text, nhưng PLAN 1.5 đòi golden test cho text nên mọi chi tiết phải được chốt:

- Ví dụ ghi `(~500g)` sau cá lóc nhưng không sau cà chua hay trứng, và không nói khi nào hiện.
- Ví dụ chỉ có ba dòng THỊT CÁ, RAU, KHÁC, trong khi SPEC có năm quầy và trứng thuộc quầy `trung-dau-phu`.
- Không nói thứ tự món trong một quầy, định dạng số, ngày, thứ, và text khi không có gì để mua.
- "Gộp theo `ingredientId`" không nói gộp trước hay sau khi làm tròn đơn vị chợ.

## Quyết định

- **`(~Ng)` do dữ liệu quyết định.** `marketUnit` có thêm trường tuỳ chọn `showApproxGrams` (mặc định `false`, không ghi ra file khi `false`). Khi `true`, text ghi `(~Ng)` với N bằng số lượng nhân `gramsPerUnit`. Người soạn dữ liệu bật cho đơn vị đếm kích cỡ khác nhau nhiều như con cá.
- **Một công thức cho đơn vị cân và đơn vị đếm:** `số lượng = ceil((purchaseGrams / gramsPerUnit) / step) × step`. Đơn vị đếm có `step` bằng 1. Sai số dấu phẩy động bị loại trước khi làm tròn lên.
- **Gộp trước, làm tròn sau.** Cộng gam ăn được của mọi dòng cùng nguyên liệu, chia cho `1 − wasteRatio`, rồi mới đổi đơn vị chợ. Cơm vào danh sách từ lượng gạo sống đã làm tròn.
- **Thứ tự:** theo quầy, trong quầy theo lần dùng đầu tiên (cơm trước, rồi các món theo thứ tự người gọi đưa vào).
- **Text:** nhóm `trung-dau-phu`, `kho-gia-vi` và `khac` dùng chung dòng `KHÁC` để khớp ví dụ. Số có dấu phẩy thập phân, tên viết thường, ngày `d/M`, thứ `T2`..`T7` và `CN`, từ "tối" cố định, ngăn cách bằng ` · `, dòng cách nhau bằng LF, không dòng trống cuối. Hết đồ cần mua thì chỉ còn dòng đầu.
- **Chữ của text nằm trong Core** (`ShareTextLabels`), không nằm trong tài nguyên của UI, vì text là tin nhắn gửi đi và có golden test ở Core. Mọi từ nằm trong một record, thay được.

## Phương án đã cân nhắc

1. **Ngưỡng cấu hình: hiện `(~Ng)` khi `gramsPerUnit` từ một mức trở lên.** Cần một con số tự đặt, và ví dụ SPEC không đủ để chọn mức đúng (mớ rau muống nặng cỡ nào thì hiện?). Bị loại.
2. **Luôn hiện `(~Ng)` cho đơn vị đếm.** Thêm nhiễu cho trứng, cà chua, và trái với ví dụ SPEC. Bị loại.
3. **Không bao giờ hiện.** Trái với ví dụ SPEC. Bị loại.
4. **Mỗi quầy một dòng, năm tiêu đề.** Rõ hơn, nhưng ví dụ SPEC để trứng dưới KHÁC. Có thể đổi bằng cách sửa `ShareTextLabels.Headings`. Bị loại cho v1 để khớp ví dụ.
5. **Làm tròn từng dòng của món rồi cộng.** Mua dư tới một đơn vị cho mỗi món dùng chung nguyên liệu. Bị loại.

## Hệ quả

**Được**
- Ví dụ trong SPEC tái hiện được từng chữ từ dữ liệu (golden test cho cả Bắc và Nam).
- Chủ dự án quyết định từng nguyên liệu có `(~Ng)` hay không, không cần sửa code.

**Đổi lại**
- Người soạn dữ liệu phải nhớ bật `showApproxGrams` cho cá, tôm nguyên con… `DataBuilder validate` (PLAN 2.2) có thể cảnh báo khi bật cho đơn vị cân.
- Mỗi nguyên liệu trên danh sách đi chợ phải có đơn vị chợ cho vùng đang dùng (khoá riêng hoặc `default`); thiếu là lỗi dữ liệu. Luật này cần có ở `validate`.
- Từ "tối" cố định: nếu sau này có lựa chọn bữa trưa, nhãn cần được truyền vào.

## Khi nào xem lại

- Người đi chợ hộ phản hồi họ cần tách trứng và đậu phụ khỏi KHÁC.
- Beta cho thấy `(~Ng)` cần ở nhiều loại đơn vị hơn cá.
