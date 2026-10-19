# ADR-008: Quay mâm: các chỗ SPEC §5 chưa nói

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án xác nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §5](../SPEC.md), [PLAN 1.6, 1.7, 3.4](../PLAN.md), [ADR-004](004-cookplanstep-activeminutes-va-id-com.md)

## Bối cảnh

SPEC §5 mô tả luật và thuật toán nhưng để ngỏ nhiều chi tiết mà `MealSpinner` và test (10.000 seed) buộc phải chốt:

- "Khớp mùa hiện tại" cần mã mùa và cách đổi tháng ra mùa; SPEC chỉ có ví dụ `"all"`.
- "Nới một luật" theo thứ tự §5.3, nhưng luật đầu tiên (trọng số mùa và vùng) vốn không bao giờ làm một lần quay thất bại.
- Chưa nói khi nào một ô không có món hợp lệ nào, hay khi các món đã khoá tự phá một luật.
- `mealTime` (§5.6) do `CookPlanScheduler` tính, nhưng task đó (PLAN 1.7) làm sau 1.6.
- "Quay lại tối đa 50 lần" nhưng không nói kết quả trả về mô tả những luật nào.

## Quyết định

- **Mùa và vùng:** mã mùa là chuỗi theo dữ liệu. Bảng tháng sang mùa nằm trong `spinner-defaults.json`, giá trị khởi điểm là bốn mùa theo dương lịch (xuân tháng 3-5, hè 6-8, thu 9-11, đông 12-2) do tôi chọn, chủ dự án chỉnh được. Món khớp mùa khi liệt kê đúng mã mùa hiện tại; `all` không khớp (nếu khớp thì mọi món có `all` đều được cộng 1,3 và món đúng mùa không còn lợi thế). Món không đúng mùa không bị phạt, đúng chữ SPEC. Danh sách vùng rỗng là không khớp.
- **Nới luật:** nới theo thứ tự trọng số mùa/vùng, thời gian, một món chiên, trùng nguồn đạm, tích luỹ. Bỏ qua luật không áp dụng: trọng số khi không món nào có hệ số khác 1, thời gian khi hộ không đặt `AvailableMinutes`.
- **`RelaxedRules`** gồm trọng số khi đã bỏ và từng luật cấp mâm mà mâm trả về thực sự vi phạm. Một luật đã "nới" nhưng mâm cuối vẫn thoả thì không báo, để thông báo cho người dùng luôn đúng.
- **Món đã khoá:** luật nào riêng các món đã khoá đã phá (không thể thoả bằng cách quay thêm món) thì bỏ ngay từ đầu, không tốn các luật đứng trước nó. SPEC viết nới tuần tự; nếu làm đúng từng chữ thì khoá hai món chiên sẽ làm mất cả giới hạn thời gian vô ích (đo được 75% mâm vượt giờ trong trường hợp thử). Mâm khoá hết không quay và không nới gì.
- **Không đủ món:** ô thiếu không có trong mâm; `SpinResult.UnfilledSlots` ghi loại ô. Không ném lỗi, vì danh sách kiêng có thể loại hết một loại món.
- **Thứ tự:** ô ít ứng viên nhất quay trước, hoà theo thứ tự trong mâm; ứng viên xếp theo `id`; mỗi lần chọn luôn dùng một số ngẫu nhiên. Nhờ vậy cùng seed và cùng dữ liệu cho cùng mâm, kể cả khi đổi thứ tự danh mục.
- **Khung mâm:** canh, mặn, (mặn thứ hai), rau. Ngưỡng 5 suất so sau khi làm tròn 9 chữ số, vì tổng suất cộng theo thứ tự thành viên có thể ra 4,999999999999999.
- **Đổi một món** không đưa lại món đang có trừ khi đó là ứng viên duy nhất.
- **Thời gian cả mâm** đi qua `IMealTimeEstimator`. `CookPlanScheduler` (PLAN 1.7) sẽ hiện thực nó và phải bảo đảm thêm món không làm thời gian ngắn đi.
- **Tuỳ chọn người dùng** vào qua `SpinContext`: món chặn, ngày ăn lần cuối, đánh giá thích/không thích (`DishPreference`).

## Phương án đã cân nhắc

1. **Báo mọi luật đã nới, kể cả khi mâm cuối vẫn thoả.** Đúng chữ SPEC hơn nhưng giao diện sẽ nói "đã nới luật chiên" cho mâm chỉ có một món chiên. Bị loại.
2. **Nới chính xác theo thứ tự cả khi món khoá đã phá luật sau.** Đơn giản hơn nhưng làm mất các luật khả thi đứng trước. Bị loại.
3. **Ném lỗi khi thiếu món.** Buộc giao diện bắt lỗi cho một tình huống bình thường (ăn kiêng nhiều). Bị loại.
4. **Dùng `all` như một mùa khớp.** Làm mất tác dụng của hệ số mùa. Bị loại.
5. **Hai mùa theo khí hậu miền Nam (khô, mưa) hoặc bảng mùa riêng từng vùng.** Có thể đúng hơn nhưng cần dữ liệu và quyết định của chủ dự án; để dành cho sau beta.

## Hệ quả

**Được**
- Một mâm trả về luôn thoả mọi luật cứng, mọi luật cấp mâm không có trong `RelaxedRules`, và `RelaxedRules` chỉ nói điều đúng.
- Test property 10.000 seed kiểm điều đó bằng một bản luật viết độc lập với code spinner.

**Đổi lại**
- Bảng tháng sang mùa và mã mùa (`spring`, `summer`, `autumn`, `winter`) là do tôi đặt; dữ liệu món (PLAN 1.8, 4.x) phải dùng đúng các mã này nếu muốn có ưu tiên mùa.
- `Reroll` ném `InvalidOperationException` nếu một món của mâm không còn trong danh mục (sau khi cập nhật bundle); giao diện đổi món (PLAN 3.4) phải bắt lỗi này và quay lại cả mâm.
- Hộ có thành viên `MoreProtein` vẫn chỉ ảnh hưởng lượng nấu, không ảnh hưởng chọn món.
- Ngày ăn lần cuối ở tương lai (đồng hồ máy sai lúc chốt mâm) làm món bị ẩn đến ngày đó. Lớp lưu trạng thái (PLAN 3.2) nên chặn ngày ghi vào không quá hôm nay.

## Khi nào xem lại

- Beta cho thấy mâm quay ra lặp món hoặc thiếu đa dạng (có thể cần phạt món trái mùa).
- Dữ liệu món thật cho thấy bốn mùa dương lịch không hợp với từng vùng.
- Người dùng khoá nhiều món và hay gặp thông báo nới luật.
