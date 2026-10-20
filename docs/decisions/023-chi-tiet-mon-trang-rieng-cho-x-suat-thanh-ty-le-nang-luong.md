# ADR-023: Chi tiết món: một trang riêng, công thức "cho ≈ X suất", thanh tỷ lệ năng lượng và kcal theo từng người

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-21
- Liên quan: [SPEC §4.2, §6.1, §6.3, §8.1-3](../SPEC.md), [PLAN 3.5](../PLAN.md), ADR-005 (đơn vị chợ và nguyên liệu tính theo quả), ADR-006 (số dinh dưỡng chưa biết và tỷ lệ năng lượng), ADR-019 (ô giữ chỗ), ADR-021 (màn hình đầu), ADR-022 (mâm hôm nay)

## Bối cảnh

PLAN 3.5: màn chi tiết một món gồm ảnh (ô giữ chỗ theo loại khi chưa có ảnh), ba phần xếp dọc là nguyên liệu "cho X suất", cách làm, dinh dưỡng (thanh đạm, béo, bột đường và kcal theo từng người) và chân trang "Số liệu ước tính, không thay tư vấn y tế." Mọi phép tính đã có (`IngredientScaler`, `PortionCalculator`, `NutritionCalculator`); phần còn lại là ghép chúng thành một màn hình. SPEC không nói rõ:

- Màn này là trang hay là hộp thoại phủ lên mâm, và từ đâu vào.
- "X suất" là số nào của nhà mình, và số gam viết thế nào cho người đứng bếp.
- Thanh tỷ lệ so với cái gì, và món có số dinh dưỡng chưa đủ thì vẽ gì.
- "kcal theo từng người" có gồm cơm không.

## Quyết định

- **Một trang riêng cho mỗi món, `/mon/<mã món>`, và tên món trên thẻ của mâm là liên kết sang đó.** Nút "Về mâm hôm nay" nằm đầu trang, cao 48px. Trang có địa chỉ riêng nên nút Back của điện thoại trả về mâm (mâm được lưu nên vẫn đúng mâm cũ), và chế độ nấu (3.7) nối được vào. Trang đứng sau `HouseholdGate`: công thức phải định lượng cho một nhà cụ thể, nên máy chưa có nhà thì sang "Nhà mình có ai?" như trang chủ.
- **"Cho ≈ X suất" là tổng suất của nhà mình cho loại món này**: canh dùng `baseServings`, món mặn `proteinServings`, rau `vegServings` (§4.2). Đó đúng là số mà danh sách đi chợ dùng, nên số gam trên trang chi tiết và số gam trong giỏ đi chợ không bao giờ lệch nhau. Viết một chữ số thập phân kiểu Việt kèm "≈" như dòng "Mỗi bữa nấu cho ≈ 3,8 suất" của màn hình đầu (SPEC viết "cho 3,8 suất", thêm "≈" cho nhất quán với dòng đó).
- **Số lượng nguyên liệu: gam nguyên từ 10 g trở lên, dưới 10 g có một chữ số thập phân** (một nhúm tiêu là "0,9 g", không làm tròn thành 1 hay 0). Dòng tính theo quả (trứng, cà chua) viết số quả ("3 quả") và để số gam dưới dạng "≈ 165 g" ở dòng dưới, để người nấu cân được. Ghi chú của dòng ("luộc, bóc vỏ") viết trong ngoặc sau tên. Tên nguyên liệu theo miền của nhà (`NameFor`).
- **Cách làm:** các bước theo `order`, mỗi bước có "Làm N phút" (cần tay) và "Chờ N phút" (chạy một mình), chỉ ghi phần nào lớn hơn 0. Tổng thời gian ở đầu trang là tổng của mọi bước, đúng số trên thẻ của mâm; nó khác "thời gian cả mâm" (§5.6) vốn tính cả nồi cơm và việc nấu song song.
- **Dinh dưỡng một suất dùng lại `NutritionSummary`** (kcal, đạm, béo, bột đường, chất xơ, natri, nguyên liệu thiếu số liệu), tắt dòng "Các con số chỉ là ước tính" vì chân trang đã nói (tham số `ShowNote`). Số nào chưa đủ thì ghi "chưa đủ số liệu", **không bao giờ ghi 0**, và liệt kê tên các nguyên liệu còn thiếu (theo tên miền của nhà).
- **Thanh tỷ lệ chia năng lượng một suất thành đạm, béo, bột đường** (4, 9, 4 kcal mỗi gam, từ `nutrition-defaults.json`) thành một thanh ba đoạn, dưới đó mỗi chất một hàng: "≈ 34%", "khuyến nghị 13–20%" (khoảng lấy từ `recommendedEnergyShare`) và một chữ cho biết đang ở đâu: **thấp hơn, vừa mức, cao hơn**. Trạng thái là chữ và nhãn, không chỉ màu; màu ba chất là token mới `--color-macro-*` (sáng và tối, test canh tương phản ≥ 3:1 trên ba nền). Sát biên thì tính là vừa mức. Chất nào chiếm 0% thì không có đoạn trên thanh nhưng vẫn có hàng của nó. Chỉ cần một trong ba chất chưa đủ số liệu thì **không vẽ thanh**, và một câu nói rõ là chưa đủ số liệu để chia tỷ lệ; kcal và các số đã biết vẫn hiện.
  - Dùng chữ "vừa mức" thay vì "đạt" hay "tốt": một món riêng lẻ gần như không bao giờ nằm trong khoảng của cả ngày (món thịt thì đạm và béo cao, món rau thì bột đường thấp), nên chữ phải mô tả chứ không chấm điểm. Đó cũng là lý do chân trang nói rõ "không thay tư vấn y tế".
- **"Kcal theo từng người" là năng lượng của món này nhân số suất của người đó, không gồm cơm**, kèm số suất ("Con trai 0,7 suất") để thấy vì sao mỗi người một số, và một dòng "Năng lượng của riêng món này, chưa tính cơm". Đúng cách `NutritionCalculator.ForMeal` tính cho cả mâm (ADR-006, phương án 4): nhãn "ăn nhiều đạm" làm công thức lớn hơn cho cả nhà (§4.2), không làm phần của người đó lớn hơn trong con số này. Năng lượng chưa biết thì mỗi người đều "chưa đủ số liệu". Cơm và tổng cả mâm một người vẫn ở phần tóm tắt của màn "Mâm hôm nay".
- **Ảnh:** dùng file `full` (800 px) nếu có, lỗi thì lùi về `thumb` (đã có trên máy vì được nạp trước), lỗi nữa thì ô giữ chỗ theo loại; ô giữ chỗ ở đây có chữ dưới hình vẽ (không rút gọn như trên thẻ); ảnh minh hoạ ghi "Ảnh minh hoạ". Món có ớt tuỳ chọn mà nhà có người không ăn cay thì cũng ghi "Để ớt riêng" như trên thẻ.
- **Món không có trong dữ liệu** (địa chỉ cũ, hay dữ liệu vừa đổi và món đã bỏ) cho một trang nói rõ kèm đường về mâm, không chuyển hướng im lặng. Chưa có dữ liệu thì không vẽ gì, các thông báo dữ liệu ở đầu trang giải thích; không nhầm là "món không tồn tại". Khi người dùng bấm "Dùng ngay" ở thông báo dữ liệu mới (ADR-020), trang dựng lại từ bundle mới.
- **Phần tính nằm ở Core (`DishDetailBuilder`)**, không ở giao diện: trả về công thức đã định lượng, tổng phút, dinh dưỡng một suất, các tỷ lệ kèm khoảng khuyến nghị và trạng thái, năng lượng từng người, tên nguyên liệu thiếu số liệu. Nên mọi luật ở trên test được không cần dựng giao diện.

## Phương án đã cân nhắc

1. **Hộp thoại phủ lên mâm.** Không có địa chỉ, nút Back của điện thoại thoát cả mâm, và chế độ nấu không nối vào được. Chọn trang.
2. **Công thức cho một suất, để người dùng tự nhân.** SPEC nói "cho 3,8 suất" và danh sách đi chợ đã dùng số của nhà; hai số khác nhau cho cùng món là lỗi chờ xảy ra.
3. **Ba tab (nguyên liệu, cách làm, dinh dưỡng).** SPEC nói "xếp dọc": người đứng bếp cần nhìn nguyên liệu và các bước cùng lúc bằng cuộn, không cần bấm.
4. **Trạng thái "thấp, vừa, cao" chỉ bằng màu của thanh.** Người mù màu và trình đọc màn hình không biết gì; chữ nằm cạnh khoảng khuyến nghị thì đọc ra được cả hai.
5. **So tỷ lệ với nhu cầu năng lượng cả ngày (kcal chia 2.000).** Cần nhu cầu theo tuổi và giới, mà dự án không có số có nguồn; "khuyến nghị" ở đây chỉ là khoảng phần trăm của `nutrition-defaults.json`.
6. **Có số kcal cả cơm cho từng người ở đây.** Cơm thuộc về cả mâm chứ không phải một món; đã có ở tóm tắt của màn "Mâm hôm nay".

## Hệ quả

- **Đã kiểm trên trình duyệt thật** (máy chủ dev, 360×740, sáng và tối): bấm tên món trên thẻ sang `/mon/<mã>` không tải lại trang, "Về mâm hôm nay" quay về đúng mâm cũ; đầu trang là ảnh (ô giữ chỗ), loại món, tên, thời gian và kcal; nguyên liệu và các bước đọc được, tên và ghi chú có dấu cách giữa chúng; không cuộn ngang, chữ cơ bản 18 px, nhỏ nhất 16 px, dòng cao 1,6, liên kết cao 48 px, thẻ món trên mâm vẫn cao 172 px. Với dữ liệu thật hiện nay (mọi nguyên liệu còn placeholder) trang ghi "chưa đủ số liệu" ở mọi chỗ, không có thanh, và nêu tên các nguyên liệu thiếu. Để nhìn thanh và kcal từng người, bundle dev **sinh ra** (không nằm trong git, tạo lại bằng DataBuilder) được viết lại tạm với con số bịa cho riêng lần xem này; không số nào vào `data/`.
- **Việc mở:** ảnh thật dùng file `full` khi chủ dự án có ảnh (4.6, 4.8); khoảng khuyến nghị là của cả ngày nên mới chỉ so từng món, so cả mâm có thể thêm ở phần tóm tắt (`MealNutrition.EnergyShares` đã có) khi có số dinh dưỡng đã xác nhận (5.x); trên máy tính, kéo chuột ngang trên chính tên món có thể vừa đổi món vừa mở trang (trên cảm ứng thì không, vì trình duyệt không gửi click sau một cái vuốt), E2E ở 3.9 nên có luồng thẻ → chi tiết → mâm; tiêu đề tab (`PageTitle`) chưa có test vì bUnit không có `HeadOutlet`.
