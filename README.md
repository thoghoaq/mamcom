# MâmCơm (tên tạm)

App giúp gia đình Việt quay ngẫu nhiên một mâm cơm (cơm + canh + món mặn + rau) vừa với số người và sức ăn của từng người.
Sau khi quay, app cho thấy công thức đã định lượng, danh sách đi chợ có ảnh nguyên liệu, kcal và dinh dưỡng, thời gian nấu và trình tự nấu cả mâm.
"MâmCơm" là tên tạm; namespace hiện dùng là `MamCom`.

## Yêu cầu

- .NET 10 SDK. Repo ghim phiên bản SDK trong `global.json` và cho phép roll-forward tới feature band mới nhất.
- Git.

## Bắt đầu nhanh

1. Clone repo (thay `<repo-url>` bằng địa chỉ repo):
   ```bash
   git clone <repo-url>
   ```
2. Vào thư mục repo:
   ```bash
   cd MamCom
   ```
3. Build (không được có warning):
   ```bash
   dotnet build
   ```
4. Chạy test:
   ```bash
   dotnet test
   ```
5. Chạy app Web, rồi mở địa chỉ hiển thị trong console. Hiện tại trang Web chỉ là trang giữ chỗ:
   ```bash
   dotnet run --project src/MamCom.Web
   ```

## Lệnh theo giai đoạn

Bảng này phản ánh trạng thái ngay sau Phase 0 và không tự cập nhật; nguồn sự thật về tiến độ là `docs/PLAN.md`. Cú pháp lệnh dự kiến của các công cụ ở Phase 2 nằm ở mục "Lệnh thường dùng" trong [CLAUDE.md](CLAUDE.md).

| Phase / task | Công cụ | Trạng thái |
|---|---|---|
| Phase 0 | build, test, chạy `MamCom.Web` (xem Bắt đầu nhanh) | Dùng được |
| 2.2 | `MamCom.DataBuilder`: `validate`, `build`, `build --release` | Chưa có |
| 2.3 | `MamCom.FdcImporter`: gợi ý dinh dưỡng từ USDA FoodData Central | Chưa có |
| 2.4 | `MamCom.RecipeDrafter`: soạn nháp công thức bằng Claude API | Chưa có |
| 2.5+ | `MamCom.Studio`: Blazor Server chạy local, duyệt và sửa dữ liệu | Chưa có |
| 2.8 | `MamCom.ImageTool`: ảnh gốc sang WebP 200/800 | Chưa có |

## Cấu trúc repo

```
docs/                        SPEC.md, PLAN.md, decisions/ (ADR), beta-notes.md
data/                        nội dung dạng JSON: schema, cấu hình, nguyên liệu, món, ảnh
src/MamCom.Core/             domain và thuật toán thuần (không IO, không UI)
src/MamCom.Data/             nạp bundle, IUserStateStore, cấu hình
src/MamCom.UI/               Razor Class Library: component và trang
src/MamCom.Web/              Blazor WebAssembly PWA host
tools/MamCom.DataBuilder/    kiểm tra và đóng gói data-bundle.json
tools/MamCom.FdcImporter/    gợi ý dinh dưỡng từ USDA FDC
tools/MamCom.RecipeDrafter/  soạn nháp công thức bằng Claude API
tools/MamCom.ImageTool/      ảnh gốc sang WebP
tools/MamCom.Studio/         Blazor Server local: sửa và duyệt dữ liệu
tests/                       MamCom.Core.Tests, MamCom.DataBuilder.Tests, MamCom.UI.Tests, MamCom.E2E (Playwright)
```

Một số mục chưa có ở thời điểm này và được tạo dần theo PLAN.md: thư mục `data/` (dữ liệu seed ở task 1.8, schema ở task 2.1) và `docs/beta-notes.md` (task 5.2). Các project trong `tools/` hiện chỉ là khung để build chung solution, chưa có chức năng.

## Tài liệu

- [docs/SPEC.md](docs/SPEC.md): đặc tả sản phẩm và kỹ thuật. Đọc trước khi code.
- [docs/PLAN.md](docs/PLAN.md): kế hoạch và tiến độ.
- [docs/decisions/](docs/decisions/): các quyết định kiến trúc (ADR).
- [CLAUDE.md](CLAUDE.md): quy ước làm việc, stack và quy tắc dữ liệu.

## Quy ước làm việc

- `docs/PLAN.md` là nguồn sự thật về việc cần làm. Làm task theo thứ tự, xong thì tick checkbox và thêm một dòng vào bảng "Nhật ký".
- Một task chỉ xong khi `dotnet build` không warning và `dotnet test` xanh. Task gắn `[NGƯỜI]` (duyệt món, nấu thử, chụp ảnh, xác nhận dinh dưỡng) là việc của chủ dự án.
- Commit nhỏ theo task, tiền tố: `feat(core)`, `feat(ui)`, `feat(tools)`, `data`, `test`, `docs`, `chore`.
- Dữ liệu: không tự bịa số dinh dưỡng (chưa có nguồn thì để `null` và `source: "placeholder"`), không lấy công thức từ website khác, mỗi ảnh phải có `source` và `license`.
- Làm lệch khỏi SPEC thì viết ADR trong `docs/decisions/` và cập nhật SPEC trong cùng commit.
