# ADR-029: Deploy staging lên Azure Static Web Apps: workflow chạy sau CI, gói dữ liệu dev đặt cạnh app

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án tạo tài nguyên Azure và token
- Ngày: 2026-10-21
- Liên quan: [PLAN 3.10](../PLAN.md), ADR-003 (không backend v1), ADR-011 (gói release), ADR-020 (gói dữ liệu riêng với app), ADR-027 (E2E trên bản publish), [hướng dẫn cho chủ dự án](../deploy-staging.md)

## Bối cảnh

PLAN 3.10: Claude Code viết workflow deploy; chủ dự án tạo Azure Static Web Apps (gói Free) và thêm deployment token vào GitHub secrets. Xong khi có URL staging và đã đo thời gian tải đầu trên 4G (mục tiêu ≤ 5 giây). ADR-020 đã nói quy trình phải đặt gói dữ liệu cạnh app sau khi publish, đưa `data-report.md` ra ngoài thư mục phục vụ và đặt cache cho từng loại file.

## Quyết định

- **Workflow riêng `deploy-staging`**, chạy khi `ci` xanh trên `main` (sự kiện `workflow_run`, lấy đúng commit CI đã kiểm) hoặc bấm tay. Không chạy lại test: CI đã chạy, kể cả E2E trên bản publish.
- **Các bước:** `dotnet publish` Release (có trimming), xoá mọi file `data-*` và `images/` trong bản publish, `DataBuilder build --out` đặt gói vào chỗ đó, chuyển `data-report.md` sang artifact `data-report`, rồi `Azure/static-web-apps-deploy@v1` tải thư mục lên (không cho Azure tự build), rồi kiểm site trả về trang app ở `/` và `/nha-minh` và trả `data-version.json`.
- **Staging dùng gói dev**, không dùng `--release`: chưa món nào được duyệt nên gói release sẽ rỗng (ADR-011); app đã tự ghi "Đang dùng dữ liệu thử".
- **Chưa có token thì không báo lỗi:** workflow dựng đủ, ghi một notice "Staging not deployed" và bỏ qua bước tải lên, để `main` không đỏ trong lúc chờ chủ dự án.
- **`staticwebapp.config.json`** (trong `wwwroot`, đi theo bản publish): địa chỉ không phải file thì trả `index.html` (như máy chủ của E2E), trừ `_framework`, `_content`, `css`, `images` và các file có đuôi; `data-version.json`, `index.html` và các service worker `no-cache`; `data-bundle.json` cache một năm vì app luôn hỏi nó kèm `?v=<phiên bản>`; ảnh một ngày (một ảnh có thể được thay mà giữ tên); kiểu MIME của `.wasm` và `.webmanifest`.
- **Tài nguyên Azure tạo với nguồn "Other"**, không nối GitHub trong Azure: nếu nối, Azure tự thêm một workflow thứ hai vào repo.

## Phương án đã cân nhắc

1. **Để Azure tự build (workflow mẫu của Azure).** Không đặt được gói dữ liệu cạnh app theo ADR-020 và build lại lần nữa.
2. **Deploy trong cùng workflow `ci`.** Một file, nhưng mỗi pull request sẽ phải né bước deploy, và lỗi deploy làm CI đỏ dù code đúng.
3. **Gói release cho staging.** Rỗng cho tới khi có món được duyệt.

## Hệ quả

- Các bước dựng đã chạy thử trên máy (publish Release và gói dữ liệu); workflow tự nó chỉ chạy được trên GitHub, và bước tải lên chỉ chạy được khi có token.
- **Cỡ tải lần đầu:** khoảng 7 MB WebAssembly chưa nén, 2,3 MB khi nén Brotli. Trên 4G (khoảng 9 Mbit/s) bản nén vào khoảng 2 đến 3 giây, bản chưa nén khoảng 6 đến 7 giây. Azure Static Web Apps có trả bản `.br` dựng sẵn hay không thì phải đo trên URL thật; nếu không, cần thêm bộ giải nén Brotli phía trình duyệt cho Blazor (cách Microsoft hướng dẫn cho host không nén) để đạt mục tiêu ≤ 5 giây.
- **Việc mở:** đo thời gian tải đầu trên 4G khi có URL, rồi tick 3.10; cân nhắc tên miền riêng và một môi trường production riêng ở Phase 5.
