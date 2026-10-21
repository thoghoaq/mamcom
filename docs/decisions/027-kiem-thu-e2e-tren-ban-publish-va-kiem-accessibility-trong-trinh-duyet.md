# ADR-027: Kiểm thử E2E trên bản publish và kiểm accessibility ngay trong trình duyệt

- Trạng thái: Đã áp dụng trong code, CI xanh
- Ngày: 2026-10-21
- Liên quan: [SPEC §8.2, §10](../SPEC.md), [PLAN 3.9](../PLAN.md), ADR-018 (seed đóng băng), ADR-020 (gói dữ liệu riêng với app), ADR-025 (chế độ nấu), ADR-026 (khẩu vị và Cài đặt)

## Bối cảnh

PLAN 3.9: kiểm contrast, vùng chạm, `aria-label` cho nút chỉ có icon, cỡ chữ hệ thống; E2E Playwright cho luồng chính (SPEC §10), "xong khi E2E xanh trong CI". Các ADR 020 đến 026 đều để lại cùng một loại việc: những thứ bUnit không thấy (`@key`, vuốt thật, tải lại giữa chừng, offline qua service worker, màu và cỡ thật sau khi CSS chạy). SPEC không nói:

- E2E chạy trên cái gì (dev server, bản publish, hay staging), dữ liệu nào, trình duyệt nào.
- "Cỡ chữ hệ thống" kiểm bằng cách nào khi Playwright không đặt được cỡ chữ mặc định của trình duyệt.
- Kiểm accessibility bằng thư viện hay bằng script, và kiểm những trạng thái nào.

## Quyết định

- **E2E chạy trên bản publish của `MamCom.Web`**, do chính bộ test dựng một lần cho cả lượt chạy (`SiteFixture`): `dotnet publish` (dựng tăng dần, vài giây vì dự án test tham chiếu `MamCom.Web` để nó được dựng trước), chép ra thư mục tạm, xoá mọi gói dữ liệu có sẵn (bản publish trên máy dev có thể mang gói của dữ liệu thật), rồi **`DataBuilder` thật làm gói từ seed đóng băng** (ADR-018: schema và config lấy từ `data/`, món và nguyên liệu từ `tests/fixtures/seed`), và Kestrel trong tiến trình test phục vụ thư mục đó trên `127.0.0.1` với cổng tự chọn; địa chỉ không có file thì trả `index.html`, như Azure Static Web Apps sẽ làm. Không dùng dev server: nó không có service worker, và `index.html` của nó chưa giải các dấu vân tay (`dotnet publish --no-build` cũng để nguyên chúng, nên fixture không dùng cờ đó).
- **Trình duyệt:** Chromium của Playwright, rồi Edge, rồi Chrome (`MAMCOM_E2E_BROWSER` chọn một), nên máy chưa từng chạy `playwright install` vẫn có trình duyệt, không phải tải gì. Không có trình duyệt nào: test **bỏ qua và nói rõ** trên máy dev, và **thất bại** khi có biến `CI` (CI cài `chromium` trước khi test, bước riêng trong `ci.yml`). `MAMCOM_E2E_SITE` trỏ vào một thư mục đã publish và có gói để bỏ qua bước dựng khi đang viết test.
- **Mỗi test một phiên riêng:** một browser context của điện thoại (360×740, tỉ lệ 2, di động, cảm ứng, `vi-VN`, múi giờ Việt Nam), nên localStorage trống như lần mở đầu. Phiên ghi lại lỗi và cảnh báo của console, lỗi của trang, request hỏng và phản hồi từ 400 trở lên; các luồng chính đòi danh sách đó rỗng (một "404" lặt vặt ở console là lỗi thật: xem dưới).
- **Các luồng:** (1) từ lần mở đầu: thêm ba người, bắt đầu, quay, khoá món giữa, quay ba lần (món khoá ở nguyên), chốt, đi chợ (tick còn sau khi tải lại), nấu (hẹn giờ của bước cơm còn sau khi tải lại, chạm nửa phải sang bước sau và nửa trái quay lại, đi hết các bước), "Xong bữa", chấm "Thích" cho cả mâm, "Xong", mâm đã chốt; (2) mâm hôm nay và ô khoá còn sau khi tải lại; (3) vuốt ngang chuột đổi đúng một món, vuốt dọc không đổi gì; (4) món bị chặn không bao giờ ra qua sáu lần quay; (5) ô tên trống quay về tên cũ và thẻ còn lại là cùng một phần tử khi bỏ thẻ khác (`@key`); (6) Cài đặt: miền và cỡ chén còn sau khi tải lại, chặn món ở trang món rồi bỏ chặn, xoá dữ liệu; (7) **offline**: sau một lần mở có mạng (worker đã cài, gói đã vào cache, một lần tải lại để worker nắm trang) tắt mạng, tải lại vẫn mở, mâm vẫn đó, và một địa chỉ chưa từng mở vẫn mở.
- **Kiểm accessibility là script chạy trong trang** (`AccessibilityTests`), không phải thư viện: nó đọc màu, cỡ và tên thật sau khi CSS chạy, trên **mọi màn, ở cả sáng và tối, và ở các trạng thái có màu riêng** (hai người trong nhà, mâm có bộ lọc nhanh với một bộ lọc bật, trang món đã chặn, đi chợ đã tick, sau bữa đã chọn, Cài đặt có miền đã chọn và có câu hỏi xoá, mâm đã chốt, hẹn giờ đang chạy). Mỗi trang đòi: **một tên cho mọi điều khiển** (aria-label, aria-labelledby, nhãn, chữ không kể icon, title); **mục tiêu từ 48px mỗi chiều** (ô tick tính theo nhãn của nó); **không cuộn ngang và không điều khiển nào ngoài màn hình**, so với bề rộng điện thoại chứ không với `window.innerWidth` vì điện thoại gặp trang rộng quá thì thu nhỏ trang chứ không cuộn; có tiêu đề trang, một vùng `main`, một `h1`; **chữ từ 16px**; **tương phản WCAG** tính từ màu chữ và nền thật (4,5; chữ lớn 3; bỏ qua nút đã tắt và nền là ảnh). Một bước nữa đặt `font-size: 200%` cho `html` (cái mà `rem` phản ứng, tức là cái người dùng đổi khi chỉnh cỡ chữ của máy) và đòi: chữ lớn lên theo, không cuộn ngang, không điều khiển nào ra khỏi màn hình.
- **Bộ kiểm tự kiểm:** một test chèn vào trang một nút không tên 20px, đoạn chữ nhạt, chữ 10px, một phần tử rộng 1000px và một đoạn chữ cỡ px, rồi đòi script bắt đủ từng thứ. Chính test này lộ ra rằng cách đo cuộn ngang bằng `innerWidth` không bao giờ bắt được gì trên điện thoại.
- **Bàn phím:** mười hai lần Tab đầu của màn "Mâm hôm nay", mỗi phần tử nhận focus đều có viền. Nút "Xoá dữ liệu của tôi" biến mất khi bấm, và tiêu điểm rơi về đầu trang: nay chuyển sang "Thôi" (câu trả lời an toàn: Enter không xoá hết) và quay về nút khi rút câu hỏi.
- **Sửa ở sản phẩm vì những gì kiểm ra:**
  1. **Vuốt bằng chuột bắt đầu trên tên món hay ảnh không đổi món:** liên kết và ảnh bị kéo-thả gốc của trình duyệt chiếm, nó gửi `pointercancel`. Nay tên món và ảnh `draggable="false"` (trên cảm ứng không bị, nhưng chuột và bút thì có).
  2. **Console có lỗi 404 mỗi lần mở:** `index.html` không có icon nên trình duyệt xin `/favicon.ico`. Nay có `<link rel="icon">`.
  3. **Chữ 200% làm trang cuộn ngang ở mọi màn** (2 đến 137px): header (tên và bánh răng) cho phép xuống dòng, bánh răng xuống dưới tên; bộ tăng giảm chén, hàng tóm tắt mâm và thanh "Mâm trước, Mâm sau" xuống dòng; thẻ món xếp dọc; hàng đi chợ bỏ ảnh (chỉ để trang trí, `alt=""`) để nhường chỗ cho chữ; header và thanh nút ở đáy thôi dính và các nút xếp dọc (dính thì ăn hết màn hình). Các quy tắc này là **truy vấn container tính bằng `rem`**: `rem` là cỡ chữ nên câu hỏi là "chữ lớn quá cho chỗ này chưa" chứ không phải "màn hình có hẹp không"; trên điện thoại 360px chúng bật từ khoảng 1,5 lần cỡ thường và không bao giờ ở cỡ thường, nên giao diện bình thường không đổi.

## Phương án đã cân nhắc

1. **Chạy E2E trên dev server của Blazor.** Nhanh hơn một chút nhưng không có worker (không kiểm được offline), và `index.html` chưa giải dấu vân tay.
2. **axe-core.** Thêm một phụ thuộc và giấy phép; script nhỏ ở trên đủ cho các luật dự án đã cam kết (tên, 48px, 16px, tương phản, cuộn ngang, cỡ chữ) và ta kiểm được chính nó. Thêm axe sau nếu cần các luật khác (vai trò ARIA, thứ tự đề mục).
3. **Chạy trên Azure staging.** Đó là 3.10, chậm hơn, và phụ thuộc mạng và tài nguyên của chủ dự án.
4. **Đặt cỡ chữ mặc định của trình duyệt khi test.** Playwright không có tuỳ chọn này; `font-size: 200%` ở `html` là đúng thứ `rem` đọc.
5. **Dùng trình duyệt thật của máy làm duy nhất.** Kết quả khác giữa các máy; Chromium của Playwright ở CI là chuẩn, máy dev dùng Edge hoặc Chrome khi chưa cài.
6. **Làm gói dữ liệu của E2E từ `data/` thật.** Mỗi lần duyệt món trong Studio làm đổi những thứ test khẳng định (ADR-018).

## Hệ quả

- **CI:** lần chạy đầu đỏ trên Ubuntu với Chromium vì một cảnh báo của console mà Edge ở máy dev không báo ("A preload for dotnet.js is found, but is not used because it is a cross-world service worker resource mismatch"): worker đã trả lời cho runtime nên gợi ý preload của Blazor chỉ tải thừa. Đã bỏ `<link rel="preload" id="webassembly" />` khỏi `index.html`; lần chạy sau xanh, 3466 test, không test nào bị bỏ qua, E2E mất 1 phút 8 giây. Việc đòi console sạch ở các luồng chính chính là thứ bắt được nó.
- **Đã chạy trên máy này:** 14 test E2E xanh ở cấu hình Debug và ở Release (khoảng 40 và 55 giây, trong đó phần lớn là dựng và nạp), trên Edge, ở cả giao diện sáng và tối.
- **Chưa kiểm:**
  - Safari và iOS (Playwright dùng Chromium; WebKit cần cài riêng), Firefox, thiết bị thật.
  - Trình đọc màn hình thật (TalkBack, VoiceOver): tên và vai trò đúng, nhưng chưa nghe; cỡ chữ lớn thật của Android và iOS (ta giả bằng `font-size: 200%`).
- **Giới hạn đã biết:**
  - Tiêu điểm bàn phím chỉ được quản ở câu hỏi xoá; bấm "Chọn mâm khác" làm khung biến mất và tiêu điểm rơi về đầu trang, như một số chỗ khác.
  - Script tương phản bỏ qua chữ trên nền là ảnh (ảnh món thật chưa có, ô giữ chỗ là màu phẳng nên được kiểm) và bỏ qua nút đã tắt.
  - Ở chữ 200% thanh nút ở đáy không còn dính: nút chính phải cuộn tới.
- **Việc mở:** nhờ chủ dự án thử với cỡ chữ lớn nhất của điện thoại và bật TalkBack hay VoiceOver một lần (3.10 hoặc 5.2); thêm WebKit vào CI nếu iOS là máy chính của người dùng; kiểm lại E2E khi có ảnh thật (4.6, 4.8: contrast chữ trên ảnh).
