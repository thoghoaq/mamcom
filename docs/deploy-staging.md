# Đưa MâmCơm lên staging (PLAN 3.10)

Workflow `.github/workflows/deploy-staging.yml` đã có sẵn: mỗi lần `ci` xanh trên `main` (hoặc khi bấm chạy tay), nó publish app, làm gói dữ liệu từ `data/` và tải cả hai lên Azure Static Web Apps. Khi chưa có token, nó vẫn dựng đủ rồi ghi "Staging not deployed" và bỏ qua bước tải lên, không báo lỗi.

Phần dưới đây là việc của chủ dự án (`[NGƯỜI]`): tạo tài nguyên trên Azure và đưa token vào GitHub. Khoảng 10 phút.

## 1. Tạo Static Web App (gói Free)

1. Vào [portal.azure.com](https://portal.azure.com) → **Create a resource** → tìm **Static Web App** → **Create**.
2. Điền:
   - **Subscription / Resource group:** chọn có sẵn, hoặc tạo nhóm mới, ví dụ `mamcom-staging`.
   - **Name:** ví dụ `mamcom-staging`.
   - **Plan type:** **Free**.
   - **Region:** East Asia (gần Việt Nam nhất trong các vùng có sẵn).
   - **Deployment details / Source:** chọn **Other** (không chọn GitHub: nếu chọn GitHub, Azure sẽ tự thêm một workflow của nó vào repo; repo đã có workflow riêng).
3. **Review + create** → **Create**, đợi xong rồi **Go to resource**.

## 2. Lấy token và đưa vào GitHub

1. Trong trang của Static Web App: **Overview** → **Manage deployment token** → **Copy**.
2. Vào GitHub repo `thoghoaq/mamcom` → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:
   - **Name:** `AZURE_STATIC_WEB_APPS_API_TOKEN`
   - **Secret:** dán token vừa chép.
3. Không dán token vào chat, issue hay file nào trong repo.

## 3. Chạy lần đầu

- Vào tab **Actions** → **deploy-staging** → **Run workflow** (nhánh `main`). Hoặc đợi lần push kế tiếp lên `main`: workflow chạy sau khi `ci` xanh.
- Khi xong, trang tóm tắt của lần chạy có dòng `Staging: https://….azurestaticapps.net`. Đó là URL staging.
- Báo lại URL này để Claude Code đo thời gian tải đầu trên 4G và ghi vào nhật ký (điều kiện xong của PLAN 3.10).

## Ghi chú

- Staging dùng gói dữ liệu dev: chưa món nào được duyệt nên gói release sẽ rỗng (ADR-011). Mọi trang hiện dòng "Đang dùng dữ liệu thử".
- Báo cáo của DataBuilder (`data-report.md`) không được đưa lên site; nó nằm trong mục **Artifacts** của mỗi lần chạy, tên `data-report`.
- Luật cache và đường dẫn của site nằm trong `src/MamCom.Web/wwwroot/staticwebapp.config.json` (ADR-029).
