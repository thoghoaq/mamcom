# ADR-016: Studio ghi nhận nấu thử: chỉ từ món đã duyệt, thêm số suất và cân thành phẩm vào `provenance`, bỏ được

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-20
- Liên quan: [SPEC §7.2, §7.3, §7.4, §7.5](../SPEC.md), [PLAN 2.7](../PLAN.md), [ADR-014](014-studio-duyet-mon-sua-thang-file-json.md), [ADR-015](015-studio-xac-nhan-dinh-duong-nguyen-lieu.md), CLAUDE.md (quy tắc dữ liệu)

## Bối cảnh

PLAN 2.7: Studio có phần nấu thử để nhập thời gian thực đo, khối lượng thành phẩm và ảnh món, rồi đặt `status=cooktested`. CLAUDE.md cấm tự đặt `cooktested`: chỉ người, qua Studio. Còn ngỏ:

- `provenance` mới chỉ có `cookTestedAt` và `measuredTotalMinutes`; khối lượng thành phẩm chưa có chỗ ghi.
- Khối lượng của cả mẻ chỉ có nghĩa khi biết mẻ đó nấu cho bao nhiêu suất (công thức tính theo một suất).
- Từ trạng thái nào được ghi nhận nấu thử, ngày nấu lấy từ đâu, nhập nhầm thì gỡ thế nào.
- Ảnh món lấy đâu khi ImageTool (2.8) chưa có.

## Quyết định

- **Hai trường tuỳ chọn mới trong `provenance`:** `measuredServings` (số suất đã nấu, nguyên, 1–100) và `measuredFinishedGrams` (cân cả mẻ như dọn ra, gồm cả nước canh, nguyên, 1–50.000 g). **Đi cùng nhau**: có cái này phải có cái kia (hai luật `if/then` trong schema). Hai trường chỉ có trong file khi có nấu thử; khi `null` thì **không ghi ra file** (như `showApproxGrams` của `marketUnit`), nên 20 món seed và 3 bản nháp thật không đổi một byte. `measuredTotalMinutes` thêm trần 1.440 phút (một ngày) để chống gõ nhầm.
- **`draft`, `validated` và `reviewed` không có hai trường này** (schema ép `null` hoặc vắng), cùng luật với dấu nấu thử có sẵn.
- **Chỉ ghi nhận nấu thử cho món `reviewed`.** Nấu một công thức chưa ai duyệt là bỏ qua bước duyệt; món đã `cooktested` phải bỏ kết quả cũ trước khi ghi lại. Studio vẫn không bao giờ tự đặt trạng thái: chỉ khi người bấm "Ghi nhận đã nấu thử".
- **Bắt buộc số phút** (ô nút tắt tới khi có; service từ chối nếu thiếu). **Ngày nấu** mặc định hôm nay (`IClock`), sửa được về ngày trước; **không được là ngày mai trở đi**: nấu thử là việc đã xảy ra. Số suất và cân tuỳ chọn, nhưng nhập cả hai hoặc để trống cả hai.
- **Chỉ đổi những dòng cần đổi:** `status`, `cookTestedAt`, `measuredTotalMinutes`, và nếu có thì `measuredServings`, `measuredFinishedGrams`, cùng ảnh khi người chọn ảnh. Không có số suất và cân thì diff Git đúng ba dòng (có test). File luôn ghi qua `DishFile`, qua file tạm, và chỉ khi máy kiểm (`DishCheck`) không còn lỗi; chống ghi đè bằng `Version` như ADR-014.
- **Ảnh món:** chọn ảnh nhỏ và ảnh lớn trong các file có sẵn ở `data/images/out`, kèm nguồn, giấy phép và cờ ảnh minh hoạ, đúng như ảnh nguyên liệu (ADR-015). Hai việc đó dùng chung `ImageLibrary` (danh sách và đường dẫn ảnh, chặn tên có đường dẫn) và component `ImageFields`. Chọn ảnh thì thay ảnh của món (ảnh chụp thật thay ảnh minh hoạ tạm, SPEC §7.5); không chọn thì món giữ ảnh đang có. Ảnh chỉ có nguồn và giấy phép mà không có file thì coi như không có ảnh.
- **Bỏ kết quả nấu thử** (nhập nhầm): chỉ từ `cooktested`, đưa món về `reviewed` và xoá bốn giá trị trên. Không có ảnh mới thì file về đúng từng byte như trước khi ghi nhận (có test). **Ảnh được giữ lại**: Studio không bao giờ xoá ảnh, và người duyệt có thể đã chọn nó. Người duyệt và ngày duyệt không đổi.
- **Chưa có đường sửa số đã ghi:** muốn sửa thì bỏ rồi ghi lại, để mỗi lần ghi là một lần người nhìn đủ số.
- **Chỗ hiện lỗi:** nút ghi nằm cuối trang, còn thông báo chung nằm đầu trang và khuất khỏi tầm mắt. Nên mọi lý do từ chối mà người sửa được tại chỗ (ngày ở tương lai, số bị schema từ chối, ảnh thiếu nguồn) hiện ngay dưới nút, kèm vị trí trường lỗi; số đã gõ vẫn còn đó. Chỉ khi file đã đổi ở chỗ khác hay không còn thì trang nạp lại và dùng thông báo chung.
- **Số đo này chưa đi vào tính toán.** Thời gian nấu trong app vẫn là tổng các bước (SPEC §5.5); số đo thật để người đối chiếu và chỉnh lại các bước ở 4.7.

## Phương án đã cân nhắc

1. **Một danh sách `cookTests` (nhiều lần nấu thử, mỗi lần một bản ghi).** Đúng hơn về lâu dài, nhưng SPEC có một `cookTestedAt` duy nhất, vòng đời món chỉ có một bước `cooktested`, và release chỉ cần biết "đã nấu thử". Hoãn tới khi có nhu cầu so sánh nhiều lần.
2. **Bắt hai trường mới có mặt (giá trị `null`) trong mọi file.** Đổi 23 file mà không thêm thông tin nào, và diff của mọi món đổi theo.
3. **Cho cân mà không cần số suất.** Con số không dùng được: không biết chia cho ai.
4. **Cho ghi nhận nấu thử từ `draft` hoặc `validated`.** Bỏ qua bước duyệt của người.
5. **Cho ngày nấu ở tương lai (nấu hộ trước).** Nấu thử là ghi lại việc đã làm.
6. **Sửa thẳng món `cooktested`.** Mâu thuẫn với "món `reviewed` và `cooktested` chỉ xem" (ADR-014); bỏ rồi ghi lại đơn giản hơn và để lại dấu rõ.
7. **Bắt `measuredTotalMinutes` có mặt trong schema khi `cooktested`.** Studio đã bắt buộc, nhưng schema chặt hơn thì file sửa tay cũng bị chặn; để dành cho khi cần (xem dưới).

## Hệ quả

**Được**
- Một lần nấu thử ghi được đủ: ngày, phút, số suất, cân thành phẩm và ảnh, với diff Git gọn và quay lui được.
- Không món seed hay bản nháp nào đổi; `DataBuilder validate` nhận kết quả (0 lỗi, 0 cảnh báo trên bản sao).
- Ảnh nguyên liệu và ảnh món dùng một chỗ chọn ảnh, một chỗ chặn tên file.

**Đổi lại**
- Schema và model `Provenance` có thêm hai trường; ai đọc bundle (3.x) cần biết chúng có thể vắng.
- Chọn ảnh vẫn cần file có sẵn: tới khi ImageTool (2.8) xong, người phải tự đặt ảnh đã xử lý vào `data/images/out`.
- Chỉ một kết quả nấu thử cho một món; nấu thử lần hai phải bỏ lần một.
- Schema chưa bắt `cooktested` phải có số phút: file sửa tay có thể thiếu, dù Studio không bao giờ ghi như vậy.

## Khi nào xem lại

- 4.7 (nấu thử 30 món) cho thấy cần giữ nhiều lần nấu hoặc sửa số đã ghi: chuyển sang danh sách `cookTests`.
- Dùng số đo thật để điều chỉnh thời gian trong tính toán: cần quy tắc gộp nhiều lần đo.
- ImageTool xong: cho tải ảnh gốc từ Studio.
- Thấy có file `cooktested` thiếu số phút: bắt trong schema.
