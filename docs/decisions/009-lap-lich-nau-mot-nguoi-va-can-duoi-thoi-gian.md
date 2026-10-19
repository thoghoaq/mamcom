# ADR-009: Lập lịch nấu một người, và cận dưới cho thời gian cả mâm

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án xác nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §5.6, §5.5](../SPEC.md), [PLAN 1.6, 1.7](../PLAN.md), [ADR-004](004-cookplanstep-activeminutes-va-id-com.md), [ADR-008](008-quay-mam-cac-cho-spec-chua-noi.md)

## Bối cảnh

SPEC §5.6 mô tả `CookPlanScheduler` bằng bốn luật (cơm bắt đầu trước, món dài nhất trước, `serveHot` về cuối, không chồng bước cần tay) nhưng để ngỏ:

- Thứ tự giữa các bước trong một món, và bước chỉ chờ (không cần tay).
- Cách xếp các món `serveHot`: "đẩy về cuối để kết thúc trong 10 phút cuối" không thể đúng với mọi mâm khi chỉ có một người nấu. Hai món xào, mỗi món 8 phút cần tay, không thể cùng kết thúc trong 10 phút cuối nếu có món thứ ba.
- `MealSpinner` (PLAN 1.6) cần `mealTime` cho luật giới hạn thời gian và đã ghi trong `IMealTimeEstimator` rằng thêm món không bao giờ làm thời gian ngắn đi, để biết khi nào giới hạn đã mất.

## Quyết định

- **Một người nấu, hai phần thời gian.** Mỗi bước có phần cần tay rồi đến phần thụ động. Phần cần tay không bao giờ chồng nhau; phần thụ động chạy song song với mọi việc. Trong một món, bước sau chỉ bắt đầu khi bước trước đã xong cả phần thụ động. Bước chỉ có thụ động (`ActiveMinutes = 0`) không chiếm tay.
- **Món thường:** xếp theo kiểu danh sách, tay rảnh thì lấy bước đã sẵn sàng của món có tổng thời gian dài nhất; hoà thì theo `id`, nên kết quả không phụ thuộc thứ tự món đưa vào. Cơm đứng đầu tại phút 0.
- **Món `serveHot`:** xếp sau, mỗi món nhắm kết thúc đúng lúc mâm kết thúc rồi nhường chỗ cho phần cần tay đã xếp. Món dài nhất vào trước, món ngắn nhất kết thúc cuối. Bảo đảm là: không món `serveHot` nào kết thúc trước phần còn lại của mâm, và một món kết thúc cách `mealTime` không quá tổng thời gian các món `serveHot` kết thúc sau nó (một món đơn lẻ kết thúc đúng `mealTime`). "Trong 10 phút cuối" đúng khi tổng đó không quá 10 phút. Con số 10 không xuất hiện trong thuật toán nên không nằm trong config.
- **Thuật toán tham lam, không tối ưu.** Có thể thêm một món mà `mealTime` ngắn lại (tìm thấy bằng fuzz: 140 phút xuống 132 khi thêm một món lấp chỗ chờ). Test ghi lại ví dụ này.
- **Cận dưới `MinimumMealMinutes`** là lớn nhất của thời gian cơm, thời gian món dài nhất và tổng thời gian cần tay cộng thời gian cần tay của cơm. Nó không giảm khi thêm món và mọi lịch do bộ lập lịch này tạo ra đều không ngắn hơn nó (test fuzz 30.000 mâm). `IMealTimeEstimator` có thêm phương thức này, và `MealSpinner` dùng nó, thay vì dùng ước lượng, để bỏ luật thời gian sớm khi các món đã khoá tự vượt giới hạn. Điều này sửa giả định "ước lượng không bao giờ ngắn lại" ghi ở ADR-008.
- **Cơm** (5 phút cần tay, 30 phút thụ động, câu hướng dẫn) nằm trong `data/config/cooking-defaults.json`.

## Phương án đã cân nhắc

1. **Lập lịch tối ưu (nhánh cận hoặc quy hoạch động).** Cho thời gian ngắn nhất và đơn điệu, nhưng nặng và khó giải thích cho người nấu, trong khi mỗi mâm chỉ có vài món. Để dành nếu beta cho thấy lịch hiện tại tệ.
2. **Bỏ đơn điệu: để spinner luôn nới luật thời gian theo thứ tự.** Làm đúng chữ SPEC §5.5 nhưng mất giới hạn thời gian vô ích khi khoá hai món chiên (ADR-008). Bị loại.
3. **Xếp `serveHot` bằng cách lập lịch ngược từ cuối.** Cho cùng kết quả ở trường hợp thường nhưng phải xử lý xung đột tay hai chiều. Cách hiện tại (nhắm giờ kết thúc rồi đẩy sang sau khi tay bận) đơn giản và có chặn trên chứng minh được. Bị loại.
4. **Bắt mọi món `serveHot` kết thúc trong 10 phút cuối.** Không làm được với một người nấu khi có nhiều món xào. Bị loại.

## Hệ quả

**Được**
- Kế hoạch nấu không bao giờ bắt người nấu làm hai việc cần tay cùng lúc, và tận dụng thời gian chờ của món này cho món khác.
- Spinner có một căn cứ chắc chắn để biết giới hạn thời gian đã mất.

**Đổi lại**
- Lịch không tối ưu: có thể dài hơn lịch tốt nhất vài phút, và thêm món có thể làm ước lượng ngắn đi.
- Nhiều món `serveHot` (hơn hai món xào) không thể cùng nằm trong 10 phút cuối; món đầu kết thúc sớm hơn.
- Chế độ nấu (PLAN 3.7) hiển thị theo `StartMinute`; hai bước có thể bắt đầu cùng phút (một bước chờ và một bước cần tay).

## Khi nào xem lại

- Beta: người nấu nói lịch bắt họ chờ vô ích hoặc món xào nguội trước khi dọn.
- Dữ liệu thật có món nhiều bước thụ động xen kẽ mà lịch tham lam xếp xa tối ưu.
