# ADR-026: Phản hồi sau bữa và Cài đặt: một hồ sơ khẩu vị, chặn món ở trang món, xoá dữ liệu hai bước

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-21
- Liên quan: [SPEC §5.2, §5.4, §8.1-6, §8.1-7](../SPEC.md), [PLAN 3.8](../PLAN.md), ADR-003 (không backend v1), ADR-021 (màn hình đầu), ADR-022 (mâm hôm nay), ADR-025 (chế độ nấu)

## Bối cảnh

PLAN 3.8: phản hồi sau bữa ("Cả nhà thích không?", ba lựa chọn, cập nhật `pref` cho lần quay sau) và Cài đặt (thành viên, vùng miền, cỡ chén, món đã chặn, xoá dữ liệu). Bộ quay (`SpinContext`) đã nhận sẵn tập món bị chặn và bảng ưa thích từ 1.4, nhưng đến 3.7 màn "Mâm hôm nay" vẫn đưa vào hai thứ rỗng. SPEC không nói:

- Phản hồi cho cả mâm hay từng món, và `pref` được giữ ở đâu, "bình thường" nghĩa là gì khi lưu.
- Món bị chặn xuất phát từ đâu (SPEC chỉ nói Cài đặt có danh sách món đã chặn).
- Cài đặt có làm lại form người trong nhà không, cỡ chén chọn thế nào.
- "Xoá dữ liệu" xoá những gì và xác nhận ra sao.
- Từ đâu vào màn sau bữa.

## Quyết định

- **Khẩu vị là một hồ sơ `TasteProfile` (Core) lưu ở khoá `taste.v1`:** `preferences` (id món → thích hoặc không thích) và `blockedDishIds` (theo thứ tự chặn). Một món "bình thường" **không có mục nào**: bình thường và chưa từng hỏi là một. `TasteDesk` là các hàm thuần từ hồ sơ này sang hồ sơ sau, như `TrayDesk` hay `CookingDesk`; hồ sơ đọc về không dùng được (id trống, giá trị lạ, món chặn hai lần) coi như hồ sơ trống. `TrayEngine.ContextFor(household, eaten, taste)` đưa cả hai phần vào `SpinContext`: món chặn bị loại hẳn, món thích nặng gấp đôi, món không thích nặng một phần năm (`spinner-defaults.json`, không đổi).
- **Trang `/sau-bua` ("Cả nhà thích không?")** hỏi **mâm đã chốt của hôm nay**; không có mâm đã chốt (chưa chốt, của hôm khác, có món không còn trong dữ liệu) thì nói vậy và không hỏi gì. Một hàng ba nút cho **cả mâm** (Thích, Bình thường, Không), và bên dưới **một hàng cho từng món** để chấm riêng. Chạm hàng cả mâm ghi cùng một câu trả lời cho mọi món. Ghi ngay khi chạm, không có nút Lưu; một dòng `role="status"` nói đã ghi và việc đó đổi gì ("Món cả nhà thích sẽ hay ra hơn").
- **"Bình thường" chỉ hiện là đã chọn sau khi người dùng chạm vào nó trong lần mở trang này.** Vì bình thường cũng là trạng thái của món chưa hỏi, hiện nó là đã chọn ngay từ đầu sẽ giống như app đã nghe câu trả lời mà thật ra chưa. Hàng cả mâm chỉ hiện một câu trả lời khi mọi món cùng câu đó (`CommonPreference`).
- **Vào màn sau bữa từ hai chỗ:** nút "Xong bữa" ở bước cuối của chế độ nấu (thay cho về trang chủ, ADR-025) và liên kết "Chấm bữa này" trong khung "Đã chốt mâm hôm nay". Không bắt buộc và không nhắc: không thông báo, không đếm.
- **Chặn món ở trang chi tiết món:** khung "Món này với nhà mình" ở cuối trang có "Không bao giờ quay món này", rồi đổi thành "Cho quay lại"; dòng giải thích đổi theo và là `role="status"`. Không đặt nút chặn trên thẻ món vì thẻ đã có khoá và đổi, và nút thứ ba không vừa 360px. Chặn chỉ ảnh hưởng **lần quay sau**: món chặn đang nằm trên mâm hay đang được khoá thì ở nguyên đến khi bị đổi.
- **Cài đặt `/cai-dat`**, vào bằng biểu tượng bánh răng ở đầu mọi trang (`AppHeader`, vùng chạm 48px), có "Về mâm hôm nay" ở đầu:
  - **Nhà mình:** dòng tóm tắt quen thuộc với liên kết "Sửa nhà mình" sang màn hình đầu (không dựng lại form người trong nhà ở hai nơi); **miền** (ba chip, như màn đầu) và **cỡ chén** (Nhỏ, Vừa, To, kèm dòng "Mỗi chén ≈ N g gạo sống" theo `portion-defaults.json`) là chip, **lưu ngay** và giữ nguyên người trong nhà, tag và kiêng khem. Cỡ chén chỉ đặt ở đây (màn đầu không hỏi, ADR-021); đổi nó đổi lượng gạo và kcal của mọi món.
  - **Món đã chặn:** danh sách theo thứ tự chặn, mỗi món một nút "Bỏ chặn" (có `aria-label` nêu tên món); món không còn trong dữ liệu mới vẫn được bỏ chặn, với tên "Món không còn trong dữ liệu".
  - **Xoá dữ liệu của tôi** hai bước: nút hỏi "Xoá hết? Không lấy lại được." với "Xoá hết" và "Thôi". Xoá là `IUserStateStore.ClearAsync` (chỉ khoá `mamcom.state.*`: nhà mình, mâm, đã ăn, đi chợ, nấu, khẩu vị; gói dữ liệu món ăn và cache ảnh **không** bị xoá, và chữ nói rõ như vậy), rồi **tải lại cả trang** (`forceLoad`) để không còn gì đã xoá nằm trong bộ nhớ, và app mở màn hình đầu.
- **Màn "Mâm hôm nay", trang món và Cài đặt đọc hồ sơ khi mở;** quay, đổi món và quay lại sau khi bỏ chặn đều dùng hồ sơ mới vì mỗi trang dựng lại khi chuyển trang.

## Phương án đã cân nhắc

1. **Chỉ hỏi cho cả mâm.** Một món ngon trong mâm dở sẽ không bao giờ được biết; thêm hàng từng món rẻ (một danh sách ba chip) và không bắt buộc.
2. **Bắt chấm từng món.** Ba món là ba lần chạm; SPEC nói một chạm.
3. **Lưu "bình thường" thành một mục.** Danh sách phình ra mãi mà không khác gì chưa hỏi, và cùng một món có hai cách ghi cho một ý.
4. **Chặn bằng vuốt hay nút trên thẻ món.** Thêm cử chỉ nhầm với vuốt để đổi món, hoặc nút thứ ba chật; trang món là nơi người ta đã đọc xong và biết mình muốn gì.
5. **Sửa người trong nhà ngay trong Cài đặt.** Làm lại một form lớn ở hai chỗ; liên kết sang màn đầu giữ một nơi.
6. **Xoá dữ liệu một chạm.** Mất cả nhà, mâm và ghi chú khẩu vị không lấy lại được vì một lần chạm nhầm ở cuối trang.
7. **Giữ `pref` trong JSON của món (`/data`).** Dữ liệu nội dung là của chủ dự án và dùng chung cho mọi người; khẩu vị là của từng nhà và nằm trên máy (ADR-003).
8. **Suy khẩu vị từ lịch sử (món nào hay được chốt).** Không thay được câu trả lời và khó giải thích; để sau v1.

## Hệ quả

- **Đã kiểm trên trình duyệt thật** (360×740, sáng và tối): bánh răng ở đầu trang mở Cài đặt; chạm "Nam" và "To" lưu ngay (`household.v1` đổi `region` và `bowlSize`, người trong nhà giữ nguyên) và dòng gợi ý đổi sang 50 g; chặn một món ở trang món lưu vào `taste.v1`, Cài đặt liệt kê tên món, "Bỏ chặn" gỡ nó; "Chốt mâm", "Chấm bữa này", chạm "Thích" cho cả mâm ghi `liked` cho ba món và mọi hàng cùng sáng "Thích"; từ chế độ nấu đến bước cuối, "Xong bữa" mở `/sau-bua`; "Xoá dữ liệu của tôi" hỏi, "Xoá hết" xoá mọi khoá `mamcom.state.*` (còn lại `mamcom.data.current`, con trỏ của gói dữ liệu) và mở màn hình đầu. Nút và chip cao từ 48px, chữ nhỏ nhất 18px, giãn dòng 1,6, không cuộn ngang.
- **Cỡ chữ của hàng "cả mâm" là `--font-size-large`** và "Bình thường" xuống hai dòng ở 360px; ba chip chia đều bề rộng và là hình chữ nhật bo góc (bo tròn hết cỡ thì chữ hai dòng làm chip thành hình tròn).
- **Giới hạn đã biết:**
  - Hồ sơ khẩu vị chỉ ở thiết bị này; đổi máy là mất (ADR-003).
  - SPEC §8.1-1 nói câu hỏi khẩu vị và kiêng khem được hỏi sau lần dùng thứ 3: màn sau bữa là đường vào khẩu vị, còn kiêng khem (`avoid`, `tag`) vẫn chưa có chỗ nhập, và việc nhắc sau lần thứ 3 chưa làm.
  - Dòng trạng thái của hai trang nằm dưới các thẻ nên trên 360×740 thường nằm dưới mép màn hình; phản hồi nhìn thấy là chip sáng lên hay món biến khỏi danh sách, dòng chữ chủ yếu cho trình đọc màn hình.
  - Sau khi chạm "Xoá dữ liệu của tôi" nút biến mất và tiêu điểm bàn phím rơi về đầu trang: để 3.9 (accessibility).
  - Ba nút trong khung "Đã chốt mâm hôm nay" (Chấm bữa này, Chọn mâm khác) xuống hàng ở 360px.
- **Việc mở:** E2E ở 3.9 nên có luồng chốt → nấu → xong bữa → chấm → quay lại và thấy món thích ra nhiều hơn (cần hạt giống cố định); một món bị chặn nằm trong ô đang khoá của mâm chưa chốt sẽ ở lại: chấp nhận ở v1, xem lại nếu người dùng thấy lạ; nếu sau này có đồng bộ giữa máy, hồ sơ này là thứ đầu tiên cần.
