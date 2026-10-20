# CLAUDE.md — MâmCơm (tên tạm)

App giúp gia đình Việt **quay ngẫu nhiên một mâm cơm** (cơm + canh + món mặn + rau) vừa với số người và sức ăn của từng người. Sau khi quay, app cho thấy:
- công thức đã định lượng cho nhà mình;
- danh sách đi chợ theo đơn vị chợ, có ảnh nguyên liệu;
- kcal và dinh dưỡng;
- thời gian nấu và trình tự nấu cả mâm.

- Đặc tả sản phẩm và kỹ thuật: `docs/SPEC.md` (đọc trước khi code).
- Kế hoạch và tiến độ: `docs/PLAN.md` (nguồn sự thật về việc cần làm).
- Quyết định kiến trúc: `docs/decisions/NNN-*.md`.

## Cách làm việc

1. Đầu mỗi phiên: đọc `docs/PLAN.md` và chọn **task đầu tiên chưa tick** mà các task phụ thuộc đã xong.
2. Task gắn `[NGƯỜI]` là việc của chủ dự án: duyệt món, nấu thử, chụp ảnh, xác nhận dữ liệu dinh dưỡng, tạo tài nguyên cloud.
   - Không làm thay.
   - Chuẩn bị công cụ hoặc file hỗ trợ, rồi dừng lại và nói rõ cần người làm gì.
3. Task gắn `[QUYẾT ĐỊNH]`: hỏi chủ dự án. Nếu task có ghi "mặc định", dùng mặc định và đi tiếp, không chặn.
4. Hoàn thành một task theo thứ tự sau:
   1. `dotnet build` không warning và `dotnet test` xanh.
   2. Tick checkbox trong PLAN.md.
   3. Thêm 1 dòng vào bảng "Nhật ký" cuối PLAN.md.
5. Khi làm lệch khỏi SPEC: viết ADR ngắn trong `docs/decisions/` và cập nhật SPEC trong cùng commit.
6. Commit nhỏ theo task, tiền tố: `feat(core)`, `feat(ui)`, `feat(tools)`, `data`, `test`, `docs`, `chore`.

## Quy tắc dữ liệu (không có ngoại lệ)

- **Không bao giờ tự bịa số dinh dưỡng.**
  - Giá trị chỉ đến từ nguồn có tham chiếu: USDA FoodData Central `fdcId`, hoặc Bảng thành phần thực phẩm Việt Nam kèm mã/trang.
  - Nếu chưa có nguồn: để `null` và ghi `source: "placeholder"`. Placeholder chỉ dùng cho dev; build `--release` phải fail nếu còn placeholder.
- **Không tự đặt trạng thái món** `reviewed` hoặc `cooktested`. Chỉ người làm qua MamCom.Studio.
- **Không lấy công thức từ nguồn khác.** Không copy hay diễn đạt lại từ Cookpad, Cooky hay website khác; soạn mới.
- **Không thu thập ảnh hàng loạt từ web.**
  - Mỗi ảnh phải có `source` và `license`.
  - Ảnh AI hoặc ảnh tạm phải có `isIllustration: true`.
- **Số trên UI luôn là ước tính:** kcal hiển thị kèm "≈" và làm tròn đến 10 kcal.
- **Không lấy số dinh dưỡng từ LLM.** RecipeDrafter chỉ được soạn tên, nguyên liệu (theo ID có sẵn), gram, các bước và thời gian.

## Stack

- .NET LTS mới nhất có trên máy (dự kiến .NET 10), C#, `Nullable` enable, `TreatWarningsAsErrors`.
- **Blazor WebAssembly PWA** (`MamCom.Web`) làm host. UI nằm trong Razor Class Library (`MamCom.UI`) để sau tái dùng cho .NET MAUI Blazor Hybrid.
- CSS thuần với design tokens (CSS variables), không dùng UI framework nặng. Font Be Vietnam Pro (self-host, OFL), icon Tabler Icons (MIT).
- **Dữ liệu nội dung:** JSON trong `/data`. `MamCom.DataBuilder` kiểm tra và đóng gói thành `data-bundle.json`. Không dùng SQLite ở v1 (xem ADR-001).
- **Trạng thái người dùng** (gia đình, lịch sử, yêu thích): localStorage qua interface `IUserStateStore`.
- **Công cụ nội dung:** `MamCom.Studio` là Blazor Server chạy local, đọc/ghi trực tiếp `/data`.
- **Ảnh:** SkiaSharp (MIT) để resize và encode WebP. Không dùng ImageSharp vì vướng giấy phép.
- **Test:** xUnit, Shouldly, bUnit, Playwright for .NET.
- **CI và host:** GitHub Actions; host Azure Static Web Apps, gói free.

## Cấu trúc repo

```
/
├─ CLAUDE.md
├─ docs/  SPEC.md, PLAN.md, decisions/, beta-notes.md
├─ data/
│  ├─ schemas/        ingredient.schema.json, dish.schema.json, <tên file config>.schema.json
│  ├─ config/         portion-defaults.json, nutrition-defaults.json, spinner-defaults.json, cooking-defaults.json, validation-defaults.json
│  ├─ ingredients/    <ingredient-id>.json
│  ├─ dishes/         <dish-id>.json   (_drafts/ cho bản nháp LLM: <id>.json kèm <id>.report.json)
│  ├─ plan/           dish-list.csv (danh sách món cần làm, RecipeDrafter đọc), ingredient-names-en.csv (từ khoá tiếng Anh cho FdcImporter)
│  ├─ _suggestions/   nutrition/<ingredient-id>.json (gợi ý của FdcImporter, không tự áp dụng)
│  └─ images/         raw/ (gốc, không commit nếu nặng), out/ (webp)
├─ src/
│  ├─ MamCom.Core/    domain + thuật toán thuần (không IO, không UI)
│  ├─ MamCom.Data/    nạp bundle, IUserStateStore, cấu hình
│  ├─ MamCom.UI/      Razor Class Library: components, Strings.vi.resx (mọi chuỗi UI), wwwroot/tokens.css + base.css
│  └─ MamCom.Web/     Blazor WASM PWA host, service worker
├─ tools/
│  ├─ MamCom.DataBuilder/   validate | build [--release]
│  ├─ MamCom.FdcImporter/   gợi ý dinh dưỡng từ USDA FDC
│  ├─ MamCom.RecipeDrafter/ soạn nháp công thức bằng Claude API (prompts/ là file sửa được)
│  ├─ MamCom.ImageTool/     raw → webp 200/800
│  └─ MamCom.Studio/        Blazor Server local: sửa/duyệt dữ liệu
└─ tests/  MamCom.Core.Tests, MamCom.DataBuilder.Tests, MamCom.FdcImporter.Tests, MamCom.RecipeDrafter.Tests, MamCom.Studio.Tests, MamCom.UI.Tests, MamCom.E2E
```

## Lệnh thường dùng (tạo ở Phase 0–2)

```
dotnet build
dotnet test
dotnet run --project tools/MamCom.DataBuilder -- validate
dotnet run --project tools/MamCom.DataBuilder -- build            # dev, cho phép placeholder
dotnet run --project tools/MamCom.DataBuilder -- build --release  # thất bại nếu còn món dưới reviewed hoặc placeholder (ADR-011)
dotnet run --project tools/MamCom.FdcImporter -- suggest --ids gao,trung-ga   # gợi ý dinh dưỡng từ USDA (ADR-012)
dotnet user-secrets set "Fdc:ApiKey" "<khoá>" --project tools/MamCom.FdcImporter   # không đặt thì dùng DEMO_KEY
dotnet run --project tools/MamCom.RecipeDrafter -- draft --limit 3            # soạn nháp từ dish-list.csv, có tính phí (ADR-013)
dotnet user-secrets set "Claude:ApiKey" "<khoá>" --project tools/MamCom.RecipeDrafter   # hoặc biến môi trường ANTHROPIC_API_KEY
dotnet run --project tools/MamCom.Studio    # http://localhost:5194, duyệt món (ADR-014) và xác nhận dinh dưỡng nguyên liệu (ADR-015); --Studio:DataFolder=<thư mục data>, --Studio:Reviewer=<tên>
dotnet run --project src/MamCom.Web
```

## Quy ước code

- **Logic thuần nằm ở `MamCom.Core`:**
  - mọi ngẫu nhiên đi qua `IRandomSource` (có seed);
  - mọi thời gian "hôm nay" đi qua `IClock`.
- **Đơn vị nằm trong tên biến:** gram, phút, kcal, mg. Ví dụ `GramsEdible`, `ActiveMinutes`, `SodiumMg`.
- **Hằng số nghiệp vụ** (hệ số khẩu phần, trọng số quay) nằm trong `data/config/*.json`, không hard-code.
- **Chuỗi UI:** tiếng Việt có dấu đầy đủ, gom vào một chỗ (`MamCom.UI/Strings.vi.resx`). Code, tên biến và comment bằng tiếng Anh.
- **Không hard-code màu hay cỡ chữ** trong component; dùng CSS variables và hỗ trợ dark mode.

## Definition of Done

- Build không warning; test xanh; logic mới có unit test.
- UI kiểm trên viewport 360×740:
  - chữ cơ bản ≥ 16px, chế độ nấu ≥ 22px;
  - vùng chạm ≥ 48px;
  - line-height ≥ 1.5;
  - dark mode đọc được.
- PLAN.md đã cập nhật (checkbox + nhật ký).
