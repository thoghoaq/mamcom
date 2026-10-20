# ADR-024: Đi chợ: danh sách của mâm đang xem, tick giữ theo mâm, "Gửi Zalo" qua Web Share và chép vào bộ nhớ tạm

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-21
- Liên quan: [SPEC §6.2, §8.1-2, §8.1-4](../SPEC.md), [PLAN 3.6](../PLAN.md), ADR-007 (danh sách đi chợ và text chia sẻ), ADR-019 (ô giữ chỗ), ADR-020 (trạng thái người dùng và offline), ADR-022 (mâm hôm nay), ADR-023 (chi tiết món)

## Bối cảnh

PLAN 3.6: màn đi chợ nhóm theo quầy, thumbnail (ô giữ chỗ theo loại khi chưa có ảnh), checkbox 48px giữ trạng thái, toggle "Hiện đồ có sẵn", nút "Gửi Zalo" (Web Share API, nếu không có thì copy). Xong khi chạy offline và text chia sẻ khớp golden test. `ShoppingListBuilder` và `ShoppingTextFormatter` đã có từ 1.5 (ADR-007) nên phần tính và text đã chốt; còn lại là một màn hình có trạng thái. SPEC không nói rõ:

- Danh sách của mâm nào, và vào từ đâu: ADR-022 để "Chốt mâm" ở lại trang chủ vì màn này chưa có.
- "Giữ trạng thái" của checkbox là giữ đến khi nào, và sang mâm khác thì sao.
- Text gửi đi có đổi theo những thứ đã tick không; nút bật "Hiện đồ có sẵn" có được nhớ không.
- "Nếu không có thì copy" nghĩa là gì khi người dùng đóng bảng chia sẻ, hay khi cả hai đều không dùng được.

## Quyết định

- **Trang riêng `/di-cho`, đứng sau `HouseholdGate`** như trang chủ và chi tiết món (cần nhà để định lượng và cần số người cho dòng đầu của text). Đầu trang có "Về mâm hôm nay" (`BackLink`, dùng chung với trang chi tiết món).
- **Danh sách là của mâm đang hiện trên màn "Mâm hôm nay"** (mâm đang xem trong lịch sử, đọc từ `tray.v1`), nếu mâm đó là của hôm nay, mọi món của nó còn trong dữ liệu và nó có món (`TrayEngine.CanContinue`). Nếu không thì trang nói "Chưa có mâm nào để đi chợ" và không có nút gửi. Không đòi mâm phải đã chốt: người dùng có thể mở xem trước khi chốt, và đổi mâm trên trang chủ thì danh sách đổi theo.
- **"Chốt mâm" mở màn Đi chợ.** Đây là việc mở của ADR-022 (SPEC §8.1-2: "Nút Chốt mâm mở đi chợ và chế độ nấu"; luồng §10: chốt → đi chợ → chế độ nấu). Chốt vẫn ghi nhật ký, đưa mâm lên đầu và khoá nút như cũ; sau đó mới chuyển trang. Quay lại trang chủ thì mâm đã chốt có "Đã chốt mâm hôm nay" và thêm nút chính "Đi chợ" cạnh "Chọn mâm khác". Chế độ nấu (3.7) sẽ nối tiếp từ đây.
- **Tick giữ theo mâm, ở khoá `shopping.v1`.** Trạng thái gồm tên mâm, các nguyên liệu đã tick (theo thứ tự tick) và nút "Hiện đồ có sẵn". Tên mâm là ngày và các món theo thứ tự ô (`ShoppingDesk.TrayKeyOf`): khoá hay mở khoá một ô không đổi tên, còn đổi món hay quay mâm mới thì đổi, và tick của mâm cũ không hiện trên mâm mới vì danh sách đã là của những món khác. Chỉ giữ một mâm, mâm gần nhất. Khi mở màn hình, tick của nguyên liệu không còn trên danh sách bị bỏ. **Nút "Hiện đồ có sẵn" là của người dùng chứ không của mâm**, nên sống qua các mâm. Trạng thái đọc về từ máy mà hỏng (sửa tay, sai dạng) coi như chưa có gì (`ShoppingState.IsUsable`).
- **Dòng đã tick ở nguyên chỗ**, gạch ngang và nhạt đi, không dồn xuống cuối: danh sách không nhảy dưới ngón tay, và đã tick được nói bằng ô tick, nét gạch và màu nhạt chứ không chỉ màu. **Tiến độ** "Đã mua 2/7" chỉ đếm những dòng đang hiện (đồ có sẵn đang ẩn không tính, tick hay không) và đọc lên được (`role="status"`).
- **Dòng:** cả dòng là nhãn của ô tick (cao 72 px, vùng chạm lớn hơn nhiều so với ô 28 px), thumbnail 56 px (ảnh `thumb` nếu có, không thì ô giữ chỗ theo loại của ADR-019, ảnh lỗi cũng thay bằng ô giữ chỗ; "Ảnh minh hoạ" chỉ đọc cho trình đọc màn hình và hiện khi rê chuột vì 56 px không đủ chỗ cho nhãn), số lượng in đậm rồi tên, "(≈ 500 g)" cho thứ tính theo con có kích cỡ khác nhau (`showApproxGrams`), và nhãn "Có sẵn" cho đồ bếp luôn có khi đang hiện. Số lượng viết như text gửi đi: dấu phẩy thập phân, tối đa hai chữ số, không dấu chấm giữa hàng nghìn. Mỗi quầy một thẻ riêng với năm tên (Thịt cá, Rau củ quả, Trứng đậu phụ, Khô và gia vị, Khác); text gửi đi vẫn gộp ba quầy sau vào một dòng KHÁC theo ví dụ của SPEC (ADR-007), vì màn hình có chỗ còn tin nhắn thì nên ngắn.
- **"Gửi Zalo" gửi cả danh sách cho người đi chợ, nên các tick không làm đổi text**; đồ có sẵn chỉ có trong text khi đang hiện trên màn hình (SPEC §6.2). Ngày và số người ở dòng đầu là của mâm và của nhà. Cách gửi (`device.js`, `IDeviceFeatures.ShareTextAsync`):
  - có bảng chia sẻ của máy (`navigator.share`, trên Android và iOS có Zalo trong đó) thì dùng nó;
  - người dùng đóng bảng thì **không chép gì sau lưng họ**;
  - bảng chia sẻ lỗi vì lý do khác, hoặc máy không có bảng, thì chép vào bộ nhớ tạm (API bất đồng bộ, rồi cách cũ bằng ô văn bản ẩn và lệnh copy) và nói "Đã chép danh sách. Mở Zalo và dán vào để gửi.";
  - cả hai đều không dùng được thì "Chưa gửi được danh sách. Bạn thử lại nhé.";
  - nút bị khoá khi đang gửi và khi không có gì để gửi; dòng thông báo nằm trong thanh nút dưới đáy để người dùng thấy ngay chỗ vừa bấm.
- **Phần tính nằm ở Core (`ShoppingDesk`)** như `TrayDesk`: danh sách của một mâm, trạng thái khi mở, tick, nút đồ có sẵn, nhóm đang hiện, tiến độ, text. Màn hình chỉ vẽ.

## Phương án đã cân nhắc

1. **Tick giữ theo ngày.** Đổi mâm giữa ngày thì tick của món cũ nằm lại trên nguyên liệu trùng của món mới, và người đi chợ tưởng đã mua.
2. **Tick giữ mãi theo nguyên liệu, không bao giờ xoá.** Không có chỗ nào để người dùng bắt đầu lại.
3. **Dồn dòng đã tick xuống cuối.** Nhìn gọn hơn nhưng danh sách nhảy mỗi lần chạm, nhất là khi một tay xách đồ.
4. **Text gửi đi bỏ các dòng đã tick.** Người nhận là người khác với người đang tick; họ cần cả danh sách.
5. **Chỉ chép, không dùng Web Share.** Thêm mấy bước (mở Zalo, tìm người, dán) cho đúng nhóm người dùng của app.
6. **Liên kết riêng của Zalo (`zalo://`, hay trang chia sẻ của Zalo).** Cần SDK hoặc trang lưu trữ, và chỉ dùng được cho Zalo; bảng chia sẻ của máy đã có Zalo và mọi ứng dụng khác.
7. **Chỉ mở được khi mâm đã chốt.** Người dùng không xem được cần mua gì trước khi quyết định.
8. **Lưu tick của nhiều mâm cùng lúc.** Thêm chỗ lưu mà chỉ mâm gần nhất mới có người đi chợ.

## Hệ quả

- **Đã kiểm trên trình duyệt thật** (360×740): "Chốt mâm" sang `/di-cho`; các nhóm, thumbnail theo loại, ô tick bấm vào ô hay vào dòng đều được, tick còn nguyên sau khi tải lại trang; "Hiện đồ có sẵn" thêm nhóm Khô và gia vị với nhãn "Có sẵn" và đếm lại thành 13; text gửi đi (chặn lời gọi chép để đọc) khớp từng chữ với ví dụ của SPEC về dạng và gồm đồ có sẵn khi nút bật, ngày "T4 21/10"; thanh nút hiện "Đã chép danh sách…" trong trình duyệt không có `navigator.share`; không cuộn ngang, dòng cao 72 px, ô giữ chỗ 56 px. **Offline:** bản publish chạy bằng máy chủ tĩnh, service worker kích hoạt và có hai cache, tắt máy chủ rồi tải lại `/di-cho`: danh sách, tick và nút vẫn chạy, không có thông báo cần mạng.
- **Chưa kiểm:** đường `navigator.share` trên máy thật (bảng chia sẻ của Android hay iOS, Zalo trong đó): trình duyệt dùng để kiểm không có, nên đường đó mới được test bằng Node với trình duyệt giả; chủ dự án nên thử một lần trên điện thoại. Cũng chưa kiểm trên Safari iOS.
- **Việc mở:** chế độ nấu nối vào từ đây (3.7); ảnh thật của nguyên liệu thay ô giữ chỗ khi có (4.6, 4.8); E2E ở 3.9 nên có luồng quay → chốt → đi chợ → tick → tải lại; khi triển khai (3.10), thư mục publish không được chứa các file gói dữ liệu sinh ra từ `src/MamCom.Web/wwwroot` (nằm trong `.gitignore` nên CI không có, nhưng publish trên máy có thể mang theo bản nén `.br` và `.gz` cũ).

**Cập nhật (ADR-025, task 3.7):** chế độ nấu đã có, nên thanh nút của mâm đã chốt là "Đi chợ" và "Nấu" (không còn nút chính "Đi chợ" cạnh "Chọn mâm khác"; nút đó nằm trong khung "Đã chốt mâm hôm nay."), và màn Đi chợ có nút chính "Bắt đầu nấu" còn "Gửi Zalo" thành nút viền.
