# ADR-019: Hệ thống giao diện: sáng và tối bằng token, font và icon tự host, ô giữ chỗ theo loại

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-20
- Liên quan: [SPEC §7.5, §8.2](../SPEC.md), [PLAN 3.1](../PLAN.md), [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md), CLAUDE.md (stack, quy ước code, Definition of Done)

## Bối cảnh

PLAN 3.1: design tokens CSS (sáng và tối), font Be Vietnam Pro tự host, Tabler Icons, layout mobile-first, vùng an toàn; xong khi có trang "styleguide" hiện token, chữ, nút ở cả hai chế độ. Chủ dự án thêm một yêu cầu: ảnh thật sẽ có **sau khi sản phẩm xong**, trong lúc đó dùng ô giữ chỗ, nhưng **không dùng một ô chung cho tất cả: mỗi loại một ô riêng cho hợp**. Còn ngỏ:

- Hai chế độ cùng nằm trên một trang styleguide thì chỉnh bằng gì, khi `prefers-color-scheme` chỉ cho một chế độ mỗi lần.
- Tương phản của hai chế độ kiểm bằng cách nào, để dark mode "đọc được" không phải cảm giác.
- Ô giữ chỗ lấy dữ liệu ở đâu, và loại chưa có hình thì sao.
- Font và icon lấy từ đâu mà vẫn tự host, chạy offline.

## Quyết định

- **Mọi màu, cỡ chữ, khoảng cách nằm trong `tokens.css`; component không viết gì riêng.** Test đọc `base.css` và `layout.css`: không màu hex, không `rgb()` hay `hsl()`, mọi `font-size` đi qua biến.
- **Sáng là mặc định; tối theo hệ thống (`prefers-color-scheme`) hoặc theo thuộc tính `data-theme="dark"` (hay `"light"`) đặt trên trang hay trên một phần tử.** Thuộc tính cho phép styleguide đặt hai chế độ cạnh nhau, và để dành cho một lựa chọn trong Cài đặt (3.8). CSS không dùng chung được một khối giữa media query và thuộc tính, nên các giá trị tối **xuất hiện hai lần**; một test bắt hai khối lệch nhau và bắt sáng và tối khai báo hai bộ biến khác nhau.
- **Tương phản được đo, không đoán.** `TokenTests` đọc `tokens.css` và tính tỷ lệ WCAG cho các cặp màu được dùng cùng nhau ở cả hai chế độ: chữ và chữ mờ trên nền, thẻ và nền nâng (4,5:1); màu nhấn làm chữ; chữ trên nút chính; chữ của nhãn và thông báo trên nền nhạt của chúng; nét vẽ của ô giữ chỗ trên ô của nó (3:1); màu chấm focus (3:1). Việc đo bắt ra một lỗ có thật: viền ô nhập `--color-border` chỉ khoảng 1,4:1 so với nền. Thêm `--color-border-strong` (từ 3,5:1 trở lên ở cả hai chế độ) cho viền ô nhập, nút "quiet" và chip của Studio; `--color-border` còn lại chỉ để kẻ đường trang trí. Màu chữ của loại món "mặn" tối đi một nấc để đạt tỷ lệ trên nền nhạt của nó.
- **Font Be Vietnam Pro tự host:** hai độ đậm (400, 600) và hai bộ ký tự (`latin`, `vietnamese`), bốn file woff2 khoảng 67 KB từ gói `@fontsource/be-vietnam-pro` 5.3.0 (OFL 1.1, kèm `OFL.txt`). `font-display: swap`, `unicode-range` để trình duyệt chỉ tải bộ cần. Dùng cho cả app và Studio. Chưa `preload`: 3.10 sẽ đo thời gian tải đầu rồi mới quyết.
- **Icon là một sprite SVG** `icons/icons.svg`, gọi bằng `<svg class="icon"><use href="…#tên"/></svg>` qua component `Icon`: một file được cache, một lần tải, nét vẽ theo `currentColor` nên theo chế độ sáng tối; một test cấm icon tự mang màu. 35 icon là Tabler Icons 3.49.0 (outline, MIT); `cow`, `shrimp`, `tofu`, `steak` Tabler không có nên được vẽ cùng lưới 24 px và nét 2 px. `Icon` không có `Label` thì là trang trí (`aria-hidden`); có `Label` thì là `role="img"`. Mọi nút chỉ có icon phải có `aria-label` (có test trên styleguide).
- **Ô giữ chỗ theo loại (SPEC §7.5):**
  - Nguyên liệu: màu theo quầy (`aisle`: thịt cá, rau củ quả, trứng đậu phụ, khô và gia vị, khác), hình theo `category` (13 loại của seed: rau củ, rau lá, quả, thịt heo, thịt bò, thịt gà, cá, hải sản, trứng, đậu phụ, nấm, gia vị, ngũ cốc). `category` chưa có hình thì dùng hình của quầy.
  - Món: màu theo loại món (`slot`: canh xanh dương, mặn cam, rau xanh lá, như SPEC §8.2), hình theo nguồn đạm (heo, bò, gà, cá, tôm, trứng, chay). Không có nguồn đạm (hay nguồn lạ) thì dùng hình của loại món: bát canh, dao nĩa, đĩa rau. Hai mươi bốn tổ hợp loại món và nguồn đạm đều khác nhau về màu hoặc hình.
  - Chỉ đọc các trường dữ liệu đã có. Bảng ánh xạ nằm trong code của `MamCom.UI` (`PlaceholderCatalog`), vì là trình bày chứ không phải hằng số nghiệp vụ.
  - Ô là hình vẽ trên nền nhạt kèm tên loại bằng chữ ("Thịt heo", "Món mặn · thịt bò") và nhãn đọc màn hình "Chưa có ảnh: thịt heo". Dạng `Compact` cho ảnh nhỏ bỏ chữ, giữ nhãn đọc. Ô không mang nhãn "Ảnh minh hoạ" (nhãn đó dành cho ảnh tạm có file) và không bao giờ là `<img>` hỏng. Hình co theo chỗ chữ để lại, nên tên dài hai dòng không đẩy hình ra khỏi ô.
  - Test: mọi `category`, nguồn đạm, quầy và loại món đã biết (và mọi `category` và nguồn đạm của seed) có ô riêng; ô dự phòng chỉ dành cho giá trị lạ; mọi hình đang dùng có trong sprite; mọi nhãn có chữ.
- **Layout phone trước:** `index.html` có `viewport-fit=cover` và `theme-color` theo chế độ; `layout.css` có khung `.app` (đầu trang 48 px, trang giữa, thanh nút `.app-actions` dính đáy), chừa vùng an toàn bằng `--safe-*`; trên màn rộng trang dừng ở `--content-width`. Màn hình đang tải và thanh lỗi của template được viết lại bằng token và tiếng Việt.
- **Trang `/styleguide`** nằm ở `MamCom.Web`, nội dung là component `Styleguide` trong `MamCom.UI` để test được bằng bUnit: màu, chữ, nút, ô nhập, thẻ, nhãn, thông báo, icon, ô giữ chỗ, hai lần: một trong khung sáng, một trong khung tối.
- **Tải font và icon một lần, có xin phép** (2026-10-20, từ npm qua jsDelivr), rồi commit vào repo. Build không cần mạng. Nguồn, phiên bản và giấy phép nằm trong `THIRD-PARTY-NOTICES.md` và trong đầu file sprite.

## Phương án đã cân nhắc

1. **Một ô giữ chỗ chung.** Chủ dự án không muốn; nguyên liệu nhận ra ngoài chợ nhờ hình.
2. **Emoji làm ô giữ chỗ.** Hình khác nhau theo máy, không chỉnh được màu theo token và chế độ tối, và không có đậu phụ.
3. **Ảnh AI hoặc ảnh kho làm ô giữ chỗ.** CLAUDE.md cấm gom ảnh hàng loạt, và mỗi ảnh phải có nguồn và giấy phép; ô giữ chỗ không cần ảnh.
4. **Chỉ dùng Tabler, không tự vẽ.** Không có bò, tôm, đậu phụ; ba loại đó là nguyên liệu quen thuộc.
5. **`light-dark()` của CSS, một dòng cho cả hai chế độ.** Bỏ được chuyện khai báo hai lần, nhưng cần trình duyệt từ 2024; app chạy trong trình duyệt của Zalo và máy Android cũ. Xem lại khi hết lo ngại đó.
6. **Chế độ tối bằng class trên `<html>` và JavaScript.** Cần mã chạy trước khi vẽ để tránh nháy; thuộc tính đặt từ Razor và CSS không cần.
7. **Google Fonts qua CDN.** Gửi địa chỉ người dùng cho bên thứ ba, không chạy offline, và CLAUDE.md đã chọn tự host.
8. **Icon font, hay SVG nhúng trong từng component.** Icon font vẽ lệch và khó đọc màn hình; SVG nhúng lặp lại cùng một nét vẽ ở mọi chỗ dùng.

## Hệ quả

**Được**
- Đổi một giá trị màu là đổi cả app ở cả hai chế độ, và test nói ngay nếu chữ thành khó đọc.
- App nhìn đúng và nhận ra được khi chưa có ảnh nào, theo đúng yêu cầu của chủ dự án; khi có ảnh, ImageTool (ADR-017) đưa ảnh vào mà không phải đổi giao diện.
- Font và icon chạy offline và không có yêu cầu nào ra ngoài.

**Đổi lại**
- Các giá trị tối của `tokens.css` phải giữ hai bản; test canh, nhưng người sửa phải sửa cả hai chỗ.
- Bốn icon tự vẽ đơn giản hơn Tabler; có thể nhờ người vẽ lại sau mà không đổi mã (cùng tên).
- Loại nguyên liệu mới (4.2 sẽ thêm nhiều) tạm dùng hình của quầy cho tới khi thêm hình riêng; thêm một loại là thêm một dòng trong `PlaceholderCatalog`, một icon trong sprite và một chuỗi.
- Chưa có component chọn giữa ảnh thật và ô giữ chỗ: nằm trong 3.4, 3.5, 3.6, nơi biết ảnh được tải từ đâu.

## Khi nào xem lại

- Có `image.thumb` thật: kiểm ô giữ chỗ và ảnh thật đứng cạnh nhau trên thẻ món và danh sách đi chợ.
- Trình duyệt mục tiêu đều có `light-dark()`: gộp hai khối tối làm một.
- 3.8 làm lựa chọn chế độ sáng tối: đặt `data-theme` trên `<html>` từ cài đặt, nhớ cả khi tải lại.
- 3.10 đo thời gian tải đầu: quyết định `preload` hai file font và kích cỡ bộ ký tự.
