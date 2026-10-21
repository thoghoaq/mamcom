# Staging của MâmCơm (PLAN 3.10, ADR-029)

Staging: **https://mamcom-pi.vercel.app** (Vercel, project `mamcom`, gói Hobby).

Workflow `.github/workflows/deploy-staging.yml` tự đưa lên mỗi lần `ci` xanh trên `main` (hoặc khi bấm chạy tay), nếu GitHub có secret `VERCEL_TOKEN`. Khi chưa có, nó vẫn dựng đủ rồi ghi "Staging not deployed" và bỏ qua bước tải lên.

## Để workflow tự đưa lên (việc của chủ dự án, khoảng 3 phút, làm được trên điện thoại)

1. Vào [vercel.com/account/settings/tokens](https://vercel.com/account/settings/tokens) → **Create Token**:
   - **Name:** `mamcom-github`
   - **Scope:** tài khoản có project `mamcom`
   - **Expiration:** tuỳ ý (ví dụ 90 ngày; hết hạn thì tạo lại).
2. Chép token, vào GitHub repo `thoghoaq/mamcom` → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:
   - **Name:** `VERCEL_TOKEN`
   - **Secret:** dán token.
3. Không dán token vào chat, issue hay file nào trong repo.
4. Thử: tab **Actions** → **deploy-staging** → **Run workflow**. Trang tóm tắt của lần chạy có dòng `Staging: https://mamcom-pi.vercel.app`.

## Đưa lên tay từ máy (khi chưa có token cho GitHub)

```bash
dotnet publish src/MamCom.Web/MamCom.Web.csproj --configuration Release --output <thư mục>
```

Rồi trong `<thư mục>/wwwroot`: xoá `data-*` và `images/`, chạy `dotnet run --project tools/MamCom.DataBuilder -- build --out <thư mục>/wwwroot`, chuyển `data-report.md` ra ngoài, xoá các file `.br` và `.gz`, rồi `vercel deploy --prod` (lần đầu `vercel link --project mamcom`). Workflow làm đúng các bước này.

## Ghi chú

- Staging dùng gói dữ liệu dev: chưa món nào được duyệt (ADR-011), mọi trang hiện "Đang dùng dữ liệu thử".
- Báo cáo của DataBuilder (`data-report.md`) không lên site; nó nằm trong **Artifacts** của mỗi lần chạy, tên `data-report`.
- Luật đường dẫn và cache nằm trong `src/MamCom.Web/wwwroot/vercel.json`.
