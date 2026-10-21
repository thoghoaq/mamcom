# ADR-029: Deploy staging lên Vercel: workflow chạy sau CI, gói dữ liệu dev đặt cạnh app

- Trạng thái: Đã áp dụng; staging chạy ở https://mamcom-pi.vercel.app
- Ngày: 2026-10-21
- Liên quan: [PLAN 3.10](../PLAN.md), [SPEC §13](../SPEC.md), ADR-003 (không backend v1), ADR-011 (gói release), ADR-020 (gói dữ liệu riêng với app), ADR-027 (E2E trên bản publish), [hướng dẫn](../deploy-staging.md)

## Bối cảnh

PLAN 3.10 và CLAUDE.md định host là Azure Static Web Apps (gói Free). Tài khoản Azure trên máy là của công ty; tài khoản Azure cá nhân của chủ dự án đòi xác thực hai bước ở bước đăng nhập mà phiên làm việc không làm thay được. Chủ dự án hỏi dùng Vercel thay, và đăng nhập Vercel CLI bằng mã trên điện thoại. ADR-020 đã nói quy trình phải đặt gói dữ liệu cạnh app sau khi publish, đưa `data-report.md` ra ngoài thư mục phục vụ và đặt cache cho từng loại file.

## Quyết định

- **Host staging là Vercel** (gói Hobby, project `mamcom`, URL https://mamcom-pi.vercel.app), thay cho Azure Static Web Apps. App chỉ là file tĩnh nên không có gì khác trong code; Vercel nén Brotli khi trả (kể cả `.wasm`).
- **`vercel.json`** (trong `wwwroot`, đi theo bản publish): mọi địa chỉ không phải file và không nằm trong `_framework`, `_content`, `css`, `images` trả `index.html`; `data-version.json`, `index.html` và các service worker `no-cache`; `data-bundle.json` cache một năm vì app luôn hỏi nó kèm `?v=<phiên bản>`; ảnh một ngày; kiểu MIME của `.wasm` và `.webmanifest`. Vercel không phục vụ chính `vercel.json` (404).
- **Workflow `deploy-staging`** chạy khi `ci` xanh trên `main` (lấy đúng commit CI đã kiểm) hoặc bấm tay: `dotnet publish` Release, xoá gói cũ, `DataBuilder build --out` đặt gói dev cạnh app, `data-report.md` thành artifact, bỏ các bản `.br` và `.gz` (Vercel tự nén), `vercel deploy --prod` với secret `VERCEL_TOKEN`, rồi kiểm `/`, `/nha-minh` và `data-version.json`. Thiếu token thì dựng đủ, ghi notice và bỏ qua bước tải lên.
- **Gói dev cho staging**: chưa món nào được duyệt nên gói release sẽ rỗng (ADR-011).
- **Worker của app không giữ** dữ liệu (`data-*`, do `service-worker.data.js` giữ theo phiên bản), ảnh, và `vercel.json`. Lý do: lần đưa lên đầu, worker cố giữ `vercel.json`, Vercel trả 404, và cả lượt cài hỏng, nên app **không chạy offline được** trên staging. Máy chủ của E2E nay cũng không trả `vercel.json`, và test offline bắt lỗi này (đã thử: bỏ dòng loại trừ thì test đỏ).
- **Worker của app được đăng ký sau khi Blazor đã chạy** (`autostart="false"` rồi `Blazor.start().then(register)`), để lượt cài không giành băng thông với chính app ở lần mở đầu.

## Phương án đã cân nhắc

1. **Azure Static Web Apps (như SPEC và PLAN).** Không đăng nhập được trong phiên này; vẫn đổi lại được, chỉ là `vercel.json` thành `staticwebapp.config.json` và bước tải lên.
2. **GitHub Pages.** Không có viết lại địa chỉ (phải dùng mẹo `404.html`) và không đặt được header cache.
3. **Phục vụ bản `.br` dựng sẵn (nhỏ hơn bản Vercel tự nén).** Vercel không tự chọn file `.br`; phải giải nén trong trình duyệt bằng JS, tốn CPU trên điện thoại. Để dành cho việc tối ưu tải đầu.

## Hệ quả

- **Đo tải lần đầu** (Edge không đầu, trạng thái trống, đến khi hiện "Nhà mình có ai?", mạng giả lập 9 Mbit/s và 170 ms): trung vị 6,8 giây khi worker được phép, 7,8 giây khi chặn worker, các lần đo dao động 5,5 đến 8 giây theo giờ; mạng chậm 1,6 Mbit/s và 150 ms khoảng 20 đến 30 giây. Trang tải về khoảng 3,4 MB. **Chưa đạt mục tiêu ≤ 5 giây.** Máy đo nhanh hơn điện thoại thật nên trên điện thoại sẽ chậm hơn.
- **Đã kiểm trên site thật:** địa chỉ của app trả trang, file trả file, `data-version.json` không cache, `.wasm` được nén Brotli (file lớn nhất 3,0 MB còn 1,2 MB), và sau một lần mở có mạng thì tắt mạng, tải lại vẫn mở với đúng mâm.
- **Gói Hobby** chỉ cho dùng cá nhân, phi thương mại: đủ cho staging và beta nhỏ; thương mại hoá thì cần gói trả phí hoặc đổi host (SPEC §13).
- **Việc mở:** giảm tải lần đầu (bỏ dữ liệu ICU bằng `InvariantGlobalization` nếu không ảnh hưởng chữ tiếng Việt, cắt bớt assembly, cân nhắc phục vụ bản `.br` dựng sẵn) rồi đo lại; token cho GitHub để workflow tự đưa lên (cho tới lúc đó, đưa lên bằng Vercel CLI trên máy).
