# ADR-021: "Nhà mình có ai?": chọn người theo vai, mỗi người một câu hỏi, bỏ qua được

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-20
- Liên quan: [SPEC §4.1, §8.1-1, §13](../SPEC.md), [PLAN 3.3](../PLAN.md), ADR-019 (giao diện), ADR-020 (trạng thái người dùng và dữ liệu)

## Bối cảnh

PLAN 3.3: màn hình đầu tiên của app. SPEC §8.1-1 nói: chạm chip vai trò (Bố, Mẹ, Ông, Bà, Con trai, Con gái, Bé) để thêm thẻ thành viên, mỗi thẻ có nút tăng giảm "mấy chén cơm"; không hỏi dồn, câu về kiêng khem và khẩu vị hỏi sau lần dùng thứ 3; cho phép bỏ qua và dùng mặc định. Xong khi tạo được hồ sơ 4 người trong ≤ 60 giây, bỏ qua được, có bUnit test cho stepper. Còn ngỏ:

- `Member` cần `AgeGroup` (hệ số `ageMod` và số chén mặc định phụ thuộc vào nó), mà chip vai trò không nói tuổi.
- `Household` cần `Region` và `BowlSize`; SPEC không nói màn hình này có hỏi không.
- "Bỏ qua và dùng mặc định" là mặc định nào.
- Màn hình này cũng là chỗ sửa nhà mình sau này (Cài đặt, 3.8), nên không nên viết một lần rồi bỏ.
- Logic thêm bớt, giới hạn số chén, đặt tên nằm ở đâu để test được mà không cần dựng giao diện.

## Quyết định

- **Logic thuần nằm ở `MamCom.Core` (`HouseholdEditor`), giao diện chỉ gọi.** Editor giữ danh sách người và bảo đảm kết quả luôn là thứ `PortionCalculator` chấp nhận: số chén luôn trong khoảng và trên lưới của `portion-defaults.json` (nhỏ nhất, lớn nhất, bước, số chén mặc định theo nhóm tuổi đều lấy từ config, không hard-code), tên không trống, không quá 12 người, `ToHousehold()` không bao giờ ném lỗi từ calculator. Mỗi người có một id riêng để trang phân biệt các thẻ.
- **Vai trò chỉ chọn nhóm tuổi ban đầu:** Bố và Mẹ là người lớn, Ông và Bà từ 60 tuổi, Bé là 2–5 tuổi, **Con trai và Con gái bắt đầu là 6–11 tuổi** (độ tuổi phần lớn gia đình gán cho chip đó). Thẻ nào cũng có một ô chọn tuổi (năm nhóm của SPEC §4.1) để đổi một chạm, ví dụ con tuổi teen hay con đã lớn. Đổi nhóm tuổi thì số chén **chưa ai đụng tới** (còn bằng số chén mặc định của nhóm cũ) đi theo số mặc định của nhóm mới; số chén người dùng đã chọn thì giữ nguyên.
- **Mỗi thẻ hỏi đúng một câu: "Thường ăn mấy chén cơm?"** bằng stepper hai nút 48px quanh con số (`BowlStepper`, nút tắt ở hai đầu khoảng). Còn tên (sửa được, trùng tên thì tự thêm số: "Con trai 2", tối đa 30 ký tự), tuổi và nút bỏ. Không hỏi kiêng khem, khẩu vị, `tag` (SPEC: hỏi sau lần dùng thứ 3, việc của phần sau); khi sửa một nhà đã lưu thì `tag` và `avoid` của từng người được **giữ nguyên**, không mất đi.
- **Miền được hỏi ngay trên màn hình này, một hàng ba chip (Bắc, Trung, Nam), mặc định Bắc.** Miền đổi tên nguyên liệu và đơn vị đi chợ mà người dùng thấy ngay trên danh sách đi chợ, nên không để ngầm; một hàng chip không phải "hỏi dồn". **Mặc định Bắc là quyết định tạm** (SPEC §13 còn ngỏ "vùng miền ưu tiên khi ra mắt"): chủ dự án đổi thì đổi hằng `HouseholdEditor.DefaultRegion`. Cỡ chén không hỏi: mặc định vừa (40 g), đổi ở Cài đặt (3.8).
- **Bỏ qua** cho ra một nhà hai người lớn (Bố, Mẹ), mỗi người số chén mặc định của người lớn; vẫn giữ miền người dùng đã chọn trên màn hình. Bỏ qua cũng là lưu, nên không bị hỏi lại.
- **Số và chữ:** số viết kiểu Việt (dấu phẩy thập phân, dấu chấm hàng nghìn) bằng `VietnameseNumber`, không phụ thuộc dữ liệu văn hoá của máy (WebAssembly có thể không có). Giữa số và đơn vị ("2 chén", "≈ 3,8 suất", "≈ 320 g") là dấu cách không ngắt dòng, để "g" không rớt xuống dòng một mình. Số suất và gạo ở dưới các thẻ ("Mỗi bữa nấu cho ≈ 3,8 suất, gạo ≈ 320 g") cho người dùng thấy ngay kết quả của việc mình chọn.
- **Lưu và tải:** `IHouseholdStore` trên `IUserStateStore`, khoá `household.v1` (ADR-020). Nhà đọc về từ máy phải qua `HouseholdEditor.IsUsable` (một đến 12 người, tên, số chén trong khoảng và là số thật, miền và cỡ chén có thật) trước khi dùng; không qua thì coi như chưa có và hỏi lại từ đầu, không ném lỗi.
- **Đường dẫn và cổng:** màn hình là trang `/nha-minh` đặt trong `MamCom.UI` (`OnboardingScreen`, router thêm assembly của UI; có test canh đường dẫn khớp `Routes.Household`). Trang chủ nằm sau `HouseholdGate`: máy chưa có nhà dùng được thì chuyển sang `/nha-minh` **thay vì thêm một mục lịch sử**, để nút "quay lại" không trả về trang lại đẩy đi. Không có bundle (lỗi tải) thì cổng không làm gì: thông báo dữ liệu (ADR-020) đã nói chuyện đó. Cổng và màn hình đều đợi dữ liệu xong và đợi nhà đã đọc xong mới vẽ form, vì form lấy giá trị ban đầu một lần.
- **Cùng một component cho lần đầu và lần sửa** (`HouseholdSetup`): lần đầu có "Bỏ qua" và "Bắt đầu"; sửa nhà đã lưu có "Lưu" và không có "Bỏ qua". Trang chủ tạm có dòng "Nhà mình: 4 người, ≈ 3,8 suất mỗi bữa." kèm "Sửa nhà mình" dẫn về màn hình này.
- **"≤ 60 giây" được đo bằng số lần chạm.** Gia đình bốn người trong SPEC §4.2 (Bố 3 chén, Mẹ 2, con 10 tuổi 1,5, Bà 1,5) cần bốn chip, hai lần tăng, một lần đổi tên con và một lần "Bắt đầu": tám lần chạm, và test bUnit làm đúng chuỗi đó, rồi kiểm tổng 3,79 ± 0,01 suất và 320 g gạo.

## Phương án đã cân nhắc

1. **Chip cho từng nhóm tuổi thay vì vai trò.** Đúng với công thức, nhưng "Con trai 6–11 tuổi" không phải điều người ta nghĩ khi nhìn bàn ăn; SPEC chọn vai trò.
2. **Hỏi tuổi bằng số.** Tốn một lần gõ cho mỗi người; năm nhóm đã đủ cho công thức.
3. **Không hỏi miền, để ngầm là Bắc.** Ít một hàng, nhưng nhà miền Nam sẽ thấy tên nguyên liệu lạ trên danh sách đi chợ mà không biết đổi ở đâu.
4. **Bỏ qua dẫn thẳng vào app không lưu gì (null).** Mọi màn hình sau phải xử lý "chưa có nhà"; lưu một nhà mặc định đơn giản hơn và người dùng vẫn sửa được.
5. **Nhà mặc định bốn người như ví dụ §4.2.** Đó là ví dụ để kiểm thử, không phải gia đình điển hình; hai người lớn là mặc định ít giả định nhất.
6. **Cổng chuyển trang theo cách thường (thêm một mục lịch sử).** "Quay lại" từ màn hình đầu sẽ về trang chủ rồi bị đẩy đi lần nữa; thay mục lịch sử thì không.

## Hệ quả

- **Đã kiểm trên trình duyệt thật** (máy chủ dev, 360×740): vào `/` khi chưa có gì thì sang `/nha-minh`; gia đình bốn người của SPEC ra "≈ 3,8 suất, gạo ≈ 320 g"; "Bắt đầu" lưu `household.v1` và về trang chủ với "Nhà mình: 4 người, ≈ 3,8 suất mỗi bữa."; tải lại vẫn ở trang chủ; "Sửa nhà mình" mở lại đúng bốn thẻ với nút "Lưu" và không có "Bỏ qua"; tên trống được trả về tên cũ, tên trùng thành "bố 2"; đổi tuổi sang 12–17 thì số chén theo sang 2,5; "Bỏ qua" lưu Bố và Mẹ hai chén. Mọi nút, chip và liên kết cao đúng 48 px, chữ 18 px (số chén 22 px), không cuộn ngang, đọc được ở chế độ sáng lẫn tối.
- **Việc mở:** `BowlSize` và việc xoá dữ liệu nằm ở Cài đặt (3.8); câu về kiêng khem và khẩu vị hỏi sau lần dùng thứ 3 (thuộc phần mâm hôm nay và phản hồi, 3.4 và 3.8); miền mặc định Bắc chờ chủ dự án chốt ở SPEC §13.
