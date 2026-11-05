# ADR-034: Mâm mấy món, thêm hay bớt món hôm nay, và chia lượng theo mâm

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-11-05
- Liên quan: [SPEC §4.2, §5.1, §8.1](../SPEC.md), [ADR-022](022-man-mam-hom-nay-phien-quay-loc-nhanh-chot-mam.md), [ADR-033](033-giao-dien-mam-go-am.md)

## Bối cảnh

SPEC §5.1 cố định khung mâm: cơm, 1 canh, 1 món mặn, 1 rau. Nhà từ 5 suất mỗi bữa trở lên thì có thêm một món mặn. Chủ dự án hỏi: muốn ít món hơn hay nhiều món hơn thì làm sao? Với nhà nhỏ thì hôm nào mâm cũng 3 món.

Trước đây, mâm 4 món cũng nấu mỗi món mặn đủ suất của cả nhà, nên một nhà đông có hai đĩa món mặn đầy.

## Quyết định

- **Cài đặt "Mâm mấy món?"** có 4 lựa chọn, lưu cùng nhà ở `Household.TraySize`; không ghi gì nghĩa là "Tự theo nhà mình":
  - **Tự theo nhà mình:** như cũ, 3 món, nhà từ 5 suất thì 4 món;
  - **2 món:** canh và món mặn;
  - **3 món:** canh, món mặn, rau;
  - **4 món:** canh, hai món mặn, rau.
  Lựa chọn áp dụng từ lần quay sau.
- **"Bớt món" và "Thêm món"** nằm dưới các thẻ, giữa hai nút là số món. Hai nút chỉ đổi mâm hôm nay, từ 2 đến 4 món, và không dùng được khi mâm đã chốt.
  - **Thêm:** quay riêng món mới theo đúng luật. Mâm 2 món thêm rau, mâm 3 món thêm một món mặn. Mọi món đang có giữ nguyên, cả dấu khoá.
  - **Bớt:** mâm 4 món bỏ một món mặn, ưu tiên món chưa khoá, nếu cả hai đều khoá thì bỏ món sau. Mâm 3 món bỏ rau.
  - Cỡ mới được ghi vào phiên (`TraySession.SizeToday`, `SizeDate`), nên các lần quay sau trong ngày giữ cỡ đó. Sang ngày khác thì cỡ của nhà mình được dùng lại.
- **Chia lượng theo mâm** (`TrayShares`, hệ số trong `portion-defaults.json`, mục `trayShares`):
  - **Hai món cùng loại** chia nhau `sameSlotTotal` = 1,2 lần lượng của một món, tức mỗi món 0,6.
  - **Mâm 2 món** (có canh và món mặn, không có rau) nấu cả hai món nhiều hơn `withoutVegetable` = 1,2 lần.
  - Mâm 1 món (chỉ có khi một ô không điền được) không đổi lượng.
  - Áp dụng cho danh sách đi chợ, chế độ nấu, kcal của cả mâm, và trang chi tiết món khi món đó nằm trong mâm hôm nay. Xem món riêng lẻ thì vẫn đủ suất.
  - Không có `trayShares` trong config thì mọi món đủ suất như cũ.

## Phương án đã cân nhắc

1. **Chỉ có nút Thêm/Bớt, không có Cài đặt.** Nhà nào luôn ăn 2 món sẽ phải bấm mỗi ngày.
2. **Cho chọn tới 5–6 món.** Thêm món tráng miệng hay món phụ thì phải có loại món mới trong dữ liệu (SPEC §7.2). Để sau, nếu người dùng beta cần.
3. **Giữ lượng đủ suất cho mọi món.** Thêm món thành thêm đồ ăn, dễ thừa và tốn tiền chợ. Bớt món thì thiếu.

## Hệ quả

- Nhà đông dùng "Tự theo nhà mình" giờ nấu mỗi món mặn 0,6 suất, thay vì 1 suất như trước. Tổng lượng món mặn là 1,2 suất, nên danh sách đi chợ nhẹ hơn trước.
- Hai hệ số 1,2 là giá trị khởi điểm, tinh chỉnh sau beta (PLAN 5.3).
- Sửa kèm một lỗi có từ trước:
  - Màn mâm vẽ lại ngay lúc trình duyệt đang lưu phiên, khi thông tin của món mới chưa được tính, nên báo "Có lỗi xảy ra". Lỗi xảy ra với nút đổi món, và với thêm món.
  - Nay thông tin món được tính trước khi lưu.
  - Kho giả trong test có chế độ lưu chậm như trình duyệt, để test bắt được lỗi này.
