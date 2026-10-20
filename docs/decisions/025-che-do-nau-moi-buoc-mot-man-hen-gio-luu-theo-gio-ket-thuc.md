# ADR-025: Chế độ nấu: mỗi bước một màn, hẹn giờ lưu theo giờ kết thúc, giữ màn hình sáng và chuông báo

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-21
- Liên quan: [SPEC §5.6, §8.1-5](../SPEC.md), [PLAN 3.7](../PLAN.md), ADR-003 (không backend v1), ADR-009 (lập lịch nấu một người), ADR-022 (mâm hôm nay), ADR-024 (đi chợ)

## Bối cảnh

PLAN 3.7: chế độ nấu với một bước mỗi màn hình, chữ từ 22 px, hẹn giờ ngay trong bước, các hẹn giờ song song hiện thành chip, Screen Wake Lock (dò tính năng, có thông báo nếu không có), chạm nửa phải để sang bước tiếp, âm báo hoặc rung khi hết giờ. `CookPlanScheduler` (1.7) đã cho thứ tự các bước cho một người nấu. SPEC không nói rõ:

- Các màn là những gì, và có hiện thời điểm bắt đầu của lịch mô phỏng không.
- Bước có hiện lượng nguyên liệu không (công thức trong dữ liệu chỉ có lời văn, lượng nằm ở chỗ khác).
- Ai bắt đầu một hẹn giờ, nhiều hẹn giờ cùng chạy thế nào, và hẹn giờ ra sao khi người dùng rời màn hình hay tắt trang.
- Chuông báo thế nào khi trình duyệt chặn âm thanh, hay khi điện thoại đã tắt màn hình.
- Nửa trái màn hình làm gì, và người dùng bàn phím hay trình đọc màn hình đi bước thế nào.
- Hết bước cuối thì sao, và từ đâu vào chế độ nấu.

## Quyết định

- **Trang `/nau` đứng sau `HouseholdGate`**, như đi chợ (ADR-024): nấu **mâm đang hiện trên màn "Mâm hôm nay"** nếu là của hôm nay, đủ món và có món; không thì trang nói "Chưa có mâm nào để nấu". Không đòi mâm đã chốt.
- **Các màn là các bước theo thứ tự của `CookPlanScheduler`** (cơm trước, rồi các bước xen kẽ của các món, món `serveHot` về cuối), mỗi bước một màn. **Không hiện phút bắt đầu của lịch**: đó là mô phỏng cho một người nấu theo đúng nhịp của thuật toán, người nấu thật chậm nhanh khác, và nếu hiện thì họ sẽ thấy mình "trễ" so với một con số không ai hứa.
- **Một màn gồm:** loại món theo màu và tên ("Cơm" cho nồi cơm), lời của bước (cỡ `--font-size-cooking`, 24 px), "Làm N phút" và "Chờ N phút", các nguyên liệu bước đó dùng, và đồng hồ đếm của bước nếu có. **Lượng nguyên liệu** lấy từ `IngredientScaler` cho đúng số suất của nhà (cùng số với trang chi tiết và danh sách đi chợ) và **chỉ ghi lần đầu món nhắc đến nguyên liệu đó**; các bước sau chỉ ghi tên, để "thịt 200 g" ở ba bước liền không bị đọc thành 600 g. Để làm vậy `CookPlanStep` có thêm `IngredientIds` (tuỳ chọn, không ghi ra JSON khi rỗng). **Bước cơm ghi số gạo sống** của nhà (§4.2), vì lượng đó không nằm trong công thức nào.
- **Hẹn giờ do người nấu bắt đầu**, bằng nút "Hẹn giờ N phút" trong bước có phần chờ, không tự chạy: phần cần tay đi trước, và chỉ người nấu biết lúc nào nồi lên bếp. Mỗi bước có tối đa một hẹn giờ, nhiều bước chạy cùng lúc; **chip ở đầu trang** (cao hơn 48 px) liệt kê các hẹn giờ đang có, cái sắp hết nhất đứng trước, bấm vào chip thì sang đúng bước đó. Đang đếm thì nút thành "Huỷ hẹn giờ", hết giờ thì thành "Đã xong" (tắt chip).
- **Hẹn giờ được lưu bằng giờ kết thúc, không bằng số giây còn lại** (`cooking.v1`: tên mâm, bước đang xem, các hẹn giờ gồm bước, giờ kết thúc và đã kêu chưa). Nên rời màn hình sang trang khác, tắt trang, hay điện thoại bị hệ điều hành làm chậm bộ đếm, hẹn giờ vẫn đúng khi quay lại. Mỗi giây màn hình tính lại từ đồng hồ (`ITicker`), không cộng dồn.
- **Chuông báo khi hết giờ:** rung theo nhịp (300-150-300-150-600 ms) và ba tiếng bíp (Web Audio, 880 Hz), thêm một chip và một dòng đọc được ("Hết giờ: Cơm.", `role="alert"`), nên hết giờ vẫn biết dù không rung được (iOS) hay máy đang tắt tiếng. Trình duyệt chỉ cho trang phát âm thanh sau một lần chạm, nên **âm thanh được chuẩn bị ngay ở lần chạm bắt đầu hẹn giờ** (`PrepareAlertAsync`) rồi mới phát lúc hết giờ. Mỗi hẹn giờ kêu **một lần**, kể cả khi nó hết lúc màn hình đóng: mở lại thì kêu ở nhịp đầu tiên, rồi ghi là đã kêu.
- **Giữ màn hình sáng bằng Screen Wake Lock:** xin khi mở màn hình (dò tính năng), xin lại khi trang trở về phía trước (trình duyệt tự nhả khoá khi trang bị ẩn), nhả khi rời màn hình. Máy không có hoặc từ chối thì **hiện thông báo** "Máy này không giữ được màn hình sáng. Hãy chỉnh thời gian tắt màn hình trong cài đặt máy." chứ không im lặng.
- **Chạm nửa phải sang bước tiếp, và nửa trái quay lại** (SPEC chỉ nói nửa phải; thêm nửa trái vì chạm nhầm mà không có đường lùi thì người đang bận tay phải mò nút). Hai nửa là hai nút phủ trên phần chữ của màn (`aria-hidden`, `tabindex="-1"`), **các điều khiển của bước nằm ngoài vùng phủ** nên không bị chặn. Thanh dưới đáy có "Trước" và "Tiếp" thấy được, cao hơn 48 px, cho bàn phím và trình đọc màn hình; bước mới được đọc lên qua một vùng `role="status"`. **Bước cuối:** "Tiếp" thành "Xong bữa" và về trang chủ (màn phản hồi sau bữa là 3.8).
- **Chữ:** lời của bước 24 px, mọi chữ khác của chế độ này từ 22 px (`--font-size-large`), chip và nút cao 53 px, giãn dòng 1,5; test canh từng selector.
- **Lối vào:** màn đi chợ có nút chính "Bắt đầu nấu" (còn "Gửi Zalo" thành nút viền), thanh nút của mâm đã chốt là "Đi chợ" và "Nấu". Ba nút (kể cả "Chọn mâm khác") không vừa 360 px, nên **"Chọn mâm khác" chuyển vào khung "Đã chốt mâm hôm nay."** thành nút phụ.
- **Phần tính nằm ở Core (`CookingDesk`):** dựng các bước, mở trạng thái, đi bước, bắt đầu và huỷ hẹn giờ, các hẹn giờ cho chip (làm tròn giây lên để "00:00" chỉ hiện khi đã hết), những hẹn giờ đến hạn chưa kêu. Tên mâm của đi chợ và nấu là một (`TrayKeys.Of`).

## Phương án đã cân nhắc

1. **Tự bắt đầu hẹn giờ khi sang bước.** Phần cần tay chưa làm xong thì giờ đã chạy, và hẹn giờ báo sai.
2. **Ghi lượng ở mọi bước.** Một nguyên liệu dùng ở ba bước bị đọc thành gấp ba.
3. **Chỉ có nút, không có vùng chạm.** Người đang nhào bột hay cầm thìa chạm một nút nhỏ khó hơn chạm nửa màn hình; SPEC cũng đòi vùng chạm lớn.
4. **Thông báo của hệ thống (Notification API, Web Push) khi app đóng.** Cần quyền, và push cần máy chủ (ADR-003); wake lock giữ trang mở là cách v1 xử lý.
5. **Đếm bằng `setInterval` cộng dồn.** Bộ đếm bị trình duyệt làm chậm khi trang ở nền, nên lệch; lưu giờ kết thúc thì luôn đúng.
6. **Một tệp âm thanh cho chuông.** Cũng cần chạm trước, lại thêm một tệp phải cache; ba tiếng bíp tạo bằng Web Audio không cần gì.
7. **Mỗi món một chuỗi bước, không xen kẽ.** Mất ý nghĩa của lịch một người nấu (cơm lên trước, nấu trong lúc chờ).
8. **Hiện phút bắt đầu của lịch ("phút 12").** Xem trên.

## Hệ quả

- **Đã kiểm trên trình duyệt thật** (360×740, sáng và tối): từ "Chốt mâm" qua đi chợ vào nấu; bước 1 là cơm với "Gạo 280 g" của nhà bốn người; chạm nửa phải sang bước 2 và nửa trái quay lại, bước được lưu; hẹn giờ bắt đầu, đếm từng giây, chip hiện đúng; một hẹn giờ đặt sẽ hết sau 14 giây: chuông được gọi đúng một lần, chip thành "Hết giờ", dòng `role="alert"` hiện, trạng thái ghi đã kêu, nút thành "Đã xong"; chữ 24/22 px, nút và chip 53 px, giãn dòng 1,5, không cuộn ngang, hai vùng chạm rộng 164 px; thanh nút mâm đã chốt là "Đi chợ" và "Nấu".
- **Chưa kiểm được:** đường Wake Lock thành công, và tiếng bíp và rung thật trên điện thoại. Trình duyệt dùng để kiểm **từ chối** wake lock (`NotAllowedError`), nên chỉ thấy đường "bị từ chối" với thông báo của nó; đường thành công, xin lại khi trang trở về, và âm thanh mới được test bằng Node với trình duyệt giả. **Chủ dự án nên thử một lần trên điện thoại thật** (Android và iPhone): giữ màn hình sáng, tiếng bíp, rung, và hẹn giờ ngắn đến hết.
- **Giới hạn đã biết:** chuông chỉ kêu khi trang đang mở (không có thông báo hệ thống); iOS không rung; tiếng bíp cần một lần chạm trước (đã làm ở lần bắt đầu hẹn giờ, nhưng mở lại trang rồi mới đến hạn thì chỉ còn rung và dòng chữ cho tới lần chạm kế tiếp).
- **Việc mở:** phản hồi sau bữa nối vào "Xong bữa" (3.8); E2E ở 3.9 nên có luồng chốt → đi chợ → nấu → hẹn giờ → tải lại; chip và nút nên có thêm thử nghiệm với cỡ chữ hệ thống lớn (3.9); mâm có hai món cùng số bước nặng thì lịch dài và nhiều chip: chưa gặp trong dữ liệu mẫu.
