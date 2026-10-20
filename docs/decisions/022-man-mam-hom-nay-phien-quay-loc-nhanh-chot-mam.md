# ADR-022: Màn "Mâm hôm nay": phiên quay, khoá, vuốt đổi, lọc nhanh và chốt mâm

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-21
- Liên quan: [SPEC §5.2–5.5, §8.1-2](../SPEC.md), [PLAN 3.4](../PLAN.md), ADR-008 (`MealSpinner`), ADR-009 (`CookPlanScheduler`), ADR-020 (trạng thái người dùng), ADR-021 (màn hình đầu)

## Bối cảnh

PLAN 3.4: màn "Mâm hôm nay" với thẻ món (ảnh, hoặc ô giữ chỗ theo loại), khoá, vuốt đổi, quay có hiệu ứng khoảng 1 giây, rung nhẹ, tóm tắt, "Mâm trước", gợi ý lọc sau 5 lần quay, thông báo luật đã nới và nút "Chốt mâm". Xong khi luồng quay → khoá → quay → chốt chạy được ở 360×740. `MealSpinner` (1.6), `CookPlanScheduler` (1.7), tính dinh dưỡng và danh sách đi chợ đã có; phần còn lại là ghép chúng thành một màn hình có trạng thái. SPEC không nói rõ:

- "Chốt mâm" làm gì khi đi chợ (3.6) và chế độ nấu (3.7) chưa có, và món nào được tính là "đã ăn" để luật 2 ngày của spinner có dữ liệu.
- Màn hình mở lại ở ngày hôm sau, hay khi dữ liệu đã đổi, thì làm gì với mâm cũ.
- Ba bộ lọc nhanh nghĩa là gì: "dưới 30 phút", "không thịt đỏ", "ít dầu mỡ".
- "Quay quá 5 lần" đếm cái gì.

## Quyết định

- **Phiên là một giá trị, mọi thao tác là hàm từ phiên này sang phiên sau (`TrayDesk` trong Core).** `TraySession` giữ: các lần quay gần nhất (mới nhất đứng đầu, tối đa `historySize` = 5), mâm nào đang hiện, số lần quay từ lần chốt trước, các bộ lọc đang bật, và ngày chốt (null khi còn đang chọn). `TrayDesk` làm: quay (giữ ô khoá), đổi một món, khoá hoặc mở khoá, lùi và tiến qua lịch sử, chốt, mở lại, đặt bộ lọc. Màn hình chỉ vẽ cái nó được đưa, nên luật test được không cần dựng giao diện. Phiên lưu ở `tray.v1`, nhật ký đã ăn ở `eaten.v1` (`ITrayStore`, `IEatenLogStore`).
- **Mở app thì tiếp tục mâm hôm nay; sang ngày mới hay dữ liệu đã đổi món thì quay mâm mới ngay.** Phiên cũ dùng tiếp được khi mâm của nó là của hôm nay và mọi món vẫn còn trong dữ liệu (`TrayEngine.CanContinue`); nếu không, màn hình bắt đầu từ phiên rỗng và **tự quay một mâm, không hiệu ứng và không rung**, để màn hình không bao giờ trống và người dùng không bị rung máy lúc mở app. Ô khoá không sang ngày mới và không sang sau lần chốt.
- **Quay giữ ô khoá; đổi một món không thêm vào lịch sử.** Quay đẩy mâm mới lên đầu lịch sử; khoá và đổi một món sửa đúng mâm đang hiện tại chỗ. Số lần quay (`SpinsSinceConfirm`) chỉ đếm lần quay cả mâm (kể cả lần tự quay lúc mở và lần quay do bật bộ lọc), không đếm đổi một món. Quay khi đang xem mâm cũ thì lấy ô khoá của mâm đó.
- **"Mâm trước" và "Mâm sau" đi qua lịch sử 5 mâm.** Hiện khi có từ hai mâm; mâm đang xem ghi "Mâm 4/5" (cũ nhất là 1). SPEC chỉ nói "Mâm trước"; thêm "Mâm sau" vì không có nó thì lùi một bước là mất đường về mâm mới hơn.
- **Chốt mâm: mâm thành mới nhất của lịch sử, các món của nó được ghi là đã ăn hôm nay, đếm lần quay về 0.** Đi chợ (3.6) và chế độ nấu (3.7) chưa có, nên hiện tại chốt giữ người dùng ở trang này với dòng "Đã chốt mâm hôm nay", khoá hết nút trên thẻ và đổi hai nút dưới đáy thành một nút "Chọn mâm khác"; hai màn kia sẽ nối vào đây. "Chọn mâm khác" bỏ trạng thái chốt, **các món vẫn ghi là đã ăn** (người dùng đã chọn món đó cho hôm nay). Ghi vào nhật ký lúc chốt chứ không đợi sau bữa: phản hồi sau bữa (3.8) chỉ cập nhật `pref`. Hết ngày thì mâm hôm qua không dùng tiếp nữa, còn nhật ký thì giữ để luật 2 ngày và 3–6 ngày của spinner có dữ liệu.
- **Ngày ăn lần cuối ở tương lai (đồng hồ sai) được đọc là hôm nay** (`EatenLog.Until`): món chỉ bị loại như vừa ăn, chứ không bị loại hàng tuần. Việc mở của 1.6 ("3.2 nên chặn ngày ăn lần cuối ở tương lai") xong ở đây, ở chỗ đọc chứ không ở kho, vì kho chỉ chứa JSON chung.
- **Ba bộ lọc nhanh nằm trong `spinner-defaults.json` (`quickFilters`), hiện sau 5 lần quay chưa chốt và ở lại khi còn bật.** Bật hay tắt một bộ lọc thì quay lại ngay (có hiệu ứng) để người dùng thấy kết quả. Món bị loại khỏi danh mục mà spinner nhìn thấy, **trừ món đang khoá và món đang có trên mâm** (không thì spinner coi món khoá là đã biến mất và quay lại nó, hay đổi một món bị lỗi vì món của mâm không còn trong danh mục).
  - **"Không thịt đỏ":** bỏ món có nguồn đạm là heo hoặc bò (`redMeatProteinSources`).
  - **"Ít dầu mỡ":** bỏ món chiên (`lowFatExcludedMethods: ["chien"]`). Định nghĩa theo cách nấu, **không theo số chất béo**: số dinh dưỡng của món còn chưa đủ (CLAUDE.md cấm tự bịa), nên không đặt ngưỡng gam chất béo. Phân tích log beta (5.3) có thể đổi sang số liệu.
  - **"Dưới N phút": N = 45, không phải 30 như SPEC viết. Đây là lệch khỏi SPEC.** Thời gian cả mâm tính cả nồi cơm, mà cơm đã mất 35 phút (vo gạo 5, nồi cơm 30; `cooking-defaults.json`), nên mâm nào cũng từ 35 phút trở lên và "dưới 30 phút" không bao giờ đạt: bộ lọc chỉ biết báo "đã nới luật". Với dữ liệu hiện tại (canh 16–67 phút, mặn 9–87, rau 11–17), 45 phút còn nhiều tổ hợp: sáu lần quay liên tiếp trên trình duyệt thật ra 40 đến 43 phút, không lần nào phải nới. Bộ lọc dùng luật mềm `AvailableMinutes` sẵn có (không thành luật cứng), nên không có mâm nào hợp thì mâm được trả về kèm thông báo; nếu hộ đã có số phút nhỏ hơn thì giữ số nhỏ hơn.
- **Hiệu ứng quay khoảng 1 giây, bỏ qua khi người dùng đã xin bớt chuyển động.** Thẻ chưa khoá rung nhẹ và mờ đi trong lúc chờ, thẻ đã khoá đứng yên, các nút bị khoá và cờ `aria-busy` bật; kết quả có sẵn ngay nhưng chỉ hiện sau 1 giây (`ISpinPacer`, thay được trong test). `prefers-reduced-motion` (đọc qua `device.js`) thì hiện kết quả ngay, vì hiệu ứng người dùng không muốn xem chỉ là chờ vô ích; CSS tắt hoạt hình cùng điều kiện. Khi kết quả hiện, máy rung 30 ms nếu có thể (`navigator.vibrate`, Android có, iOS không; `device.js` dò từng tính năng và trả `false` thay vì lỗi), và đọc cho trình đọc màn hình tên các món vừa quay.
- **Vuốt ngang thẻ để đổi món, kèm nút "Đổi món" rõ ràng.** Vuốt không tự khám phá được và không dùng được bằng bàn phím hay trình đọc màn hình, nên mỗi thẻ có thêm hai nút 48px: khoá và đổi. Một cử chỉ tính là vuốt khi đi ngang ít nhất 60 px và xa gấp đôi chiều dọc, nếu không là người dùng đang cuộn trang. `touch-action: pan-y` để trình duyệt chỉ giữ chiều dọc. Thẻ đã khoá hay đang bị khoá thì bỏ qua cử chỉ.
- **Thẻ món:** ảnh `thumb` nếu có, không thì ô giữ chỗ theo loại (ADR-019), bản chỉ có hình vẽ; ảnh lỗi thì thay bằng ô giữ chỗ, không bao giờ để ảnh vỡ; ảnh minh hoạ ghi "Ảnh minh hoạ". Dưới tên món: tổng thời gian các bước và kcal một suất (≈ làm tròn 10; chưa đủ số liệu thì nói là chưa đủ, không ghi 0). Món có ớt tuỳ chọn mà trong nhà có người không ăn cay thì thêm "Để ớt riêng" (SPEC §5.2); món bắt buộc cay không có ghi chú đó (spinner đã loại nó với nhà này).
- **Tóm tắt dưới mâm:** thời gian cả mâm (`CookPlanScheduler`, không phải tổng các món), năng lượng cho một người cả cơm (tổng cả nhà chia số người), số gạo, và số thứ cần mua không kể đồ có sẵn (`ShoppingListBuilder`). Thông báo luật đã nới và ô không điền được hiện trên các thẻ, bằng chữ của từng luật.
- **Bố cục.** Thanh nút dưới đáy chạy từ mép đến mép (lấy lại phần đệm của trang) và giữ mỗi chữ cùng icon trên một dòng, nên hai nút "Quay mâm" và "Chốt mâm" vừa ở 360 px; nút khoá và đổi nằm dưới ảnh, vừa đúng bề rộng ảnh, nên thẻ không cao hơn chữ của nó (172 px). Dòng "Nhà mình: …" xuống cuối trang.
- **Trang chủ là màn hình này**, sau `HouseholdGate` (ADR-021). Dòng đếm món tạm của 3.2 (`DataSummary`) bỏ đi. Khi người dùng bấm "Dùng ngay" ở thông báo dữ liệu mới (ADR-020), màn hình dựng lại từ bundle mới (`AppDataComponentBase.OnDataChangedAsync`).

## Phương án đã cân nhắc

1. **Giữ "dưới 30 phút" như SPEC.** Bộ lọc không bao giờ đạt, vô dụng. Bỏ cơm ra khỏi thời gian của bộ lọc đổi nghĩa "thời gian cả mâm" của §5.6. Chọn 45.
2. **"Ít dầu mỡ" theo gam chất béo mỗi suất.** Cần ngưỡng do mình đặt và số dinh dưỡng đã xác nhận của gần như mọi nguyên liệu; chưa có cả hai.
3. **Ghi "đã ăn" sau bữa (cùng phản hồi).** Người dùng không phải lúc nào cũng bấm phản hồi; mâm vừa chốt có thể quay lại ngay hôm sau nếu không ai bấm.
4. **Chỉ lưu mâm hiện tại, không lưu lịch sử.** Mất "Mâm trước" khi mở lại app.
5. **Mỗi lần đổi một món là một mục lịch sử.** Lịch sử 5 mâm bị đổi món đẩy hết trong vài lần vuốt.
6. **Giữ phiên qua ngày mới.** Ô khoá của hôm qua sang hôm nay, và hàng ngày người dùng phải tự xoá.
7. **Quay ngay không đợi, hiệu ứng chỉ là CSS trên kết quả đã hiện.** Người dùng thấy mâm mới ngay rồi mới thấy rung; không có khoảnh khắc "đang quay".

## Hệ quả

- **Đã kiểm trên trình duyệt thật** (máy chủ dev, 360×740): xong màn đầu tiên thì mâm đầu hiện ngay; "Quay mâm" mất khoảng 1,08 giây, trong lúc đó thẻ chưa khoá rung, `aria-busy` bật, nút bị khoá và nút đổi chữ thành "Đang quay…"; khoá món giữa thì quay lại món đó vẫn ở nguyên; đổi một món chỉ đổi đúng thẻ đó; vuốt ngang đổi món, vuốt dọc không, vuốt thẻ đã khoá không; năm lần quay thì hiện ba chip lọc; bật "Không thịt đỏ" quay lại và trong mâm không còn heo hay bò; "Dưới 45 phút" ra mâm 40–43 phút, sáu lần liền không phải nới; "Mâm trước" và "Mâm sau" đi qua "Mâm 4/5", "Mâm 5/5"; "Chốt mâm" ghi ba món vào `eaten.v1`, khoá nút và hiện "Chọn mâm khác"; giả ngày hôm qua thì mở lại sẽ quay mâm mới, một mục lịch sử, không còn bộ lọc. Nút dưới đáy cao 48 px một dòng, thẻ 172 px, chữ nhỏ nhất 16 px, không cuộn ngang.
- **Việc mở:** "Chốt mâm" mở đi chợ và chế độ nấu (3.6, 3.7); món chặn và ưa thích (`blocked`, `pref`) đang trống cho đến 3.8; thumb thật thay ô giữ chỗ khi chủ dự án có ảnh (4.6, 4.8); E2E ở 3.9 nên có luồng quay → khoá → quay → chốt và cử chỉ vuốt bằng cảm ứng thật, vì bUnit không phân biệt được phần tử được giữ lại với phần tử dựng lại.

**Cập nhật (ADR-024, task 3.6):** màn Đi chợ đã có, nên "Chốt mâm" không còn ở lại trang chủ: nó ghi nhật ký, đưa mâm lên đầu và khoá nút như đã viết ở trên, rồi mở `/di-cho`; trang chủ của mâm đã chốt có thêm nút "Đi chợ". Chế độ nấu (3.7) nối tiếp từ màn Đi chợ.
