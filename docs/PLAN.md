# PLAN — MâmCơm (tên tạm)

**Cách dùng file này**
- Làm task theo thứ tự. Mỗi task có mục **Xong khi**; chỉ tick `[x]` khi đạt đủ.
- `[NGƯỜI]` = việc của chủ dự án. Claude Code chỉ chuẩn bị công cụ, rồi dừng lại và nhắc.
- `[QUYẾT ĐỊNH]` = hỏi trước. Nếu task ghi "mặc định" thì dùng mặc định và đi tiếp.
- `→ phụ thuộc` = chỉ làm khi các task đó đã xong.
- Đặc tả chi tiết ở `docs/SPEC.md` (viết tắt §).

## Mốc và thời lượng ước tính (làm ngoài giờ, khoảng 10 giờ/tuần)

| Mốc | Nội dung | Tuần |
|---|---|---|
| M0 | Nền móng repo, CI | 1 |
| M1 | Domain core + pipeline dữ liệu chạy được với seed | 2–3 |
| M2 | UI MVP (PWA) chạy với seed, deploy staging | 3–5 |
| M3 | Nội dung thật: ≥ 60 món đã duyệt, ảnh nguyên liệu | 2–8 (song song) |
| M4 | Beta với 5 người nấu chính | 8–9 |
| M5 | Ra mắt v1 | 10 |

---

## Phase 0 — Nền móng (M0)

- [x] **0.1** Tạo solution `MamCom.sln` và các project theo cấu trúc trong CLAUDE.md. Thêm `Directory.Build.props` (Nullable, TreatWarningsAsErrors, LangVersion latest) và `.editorconfig`.
  - Xong khi: `dotnet build` xanh, không warning.
- [x] **0.2** Tạo các project test (xUnit, Shouldly, bUnit), mỗi project có 1 test mẫu. `→ 0.1`
  - Xong khi: `dotnet test` xanh.
- [x] **0.3** GitHub Actions `ci.yml`: restore → build → test → `DataBuilder validate` (bước validate tạm skip cho tới khi 2.2 xong). `→ 0.2`
  - Xong khi: pipeline xanh trên nhánh main.
- [x] **0.4** Viết ADR-001 (JSON bundle thay SQLite), ADR-002 (Blazor WASM PWA trước), ADR-003 (không backend ở v1) theo §9. Viết README quick start.
  - Xong khi: ADR và README có trong repo.
- [x] **0.5** `[QUYẾT ĐỊNH]` Tên app và hosting. Mặc định: `MamCom` và Azure Static Web Apps. Không chặn các task khác.

## Phase 1 — Domain core (M1)

- [x] **1.1** Models dạng record trong `MamCom.Core`: `Member`, `Household`, `Ingredient`, `Dish`, `DishIngredient`, `Step`, `MealTray`, `SlotResult`, `SpinResult`, `ShoppingItem`, `NutritionFacts`, `CookPlan`, `CookPlanStep`. Thêm `IRandomSource` (có seed) và `IClock`.
  - Xong khi: model khớp schema ở §7.1–7.2; có test serialize/deserialize JSON mẫu.
- [x] **1.2** `PortionCalculator` và nạp `data/config/portion-defaults.json` (§4.2). `→ 1.1`
  - Xong khi: test gia đình mẫu ra 3,79 ± 0,01 suất và 320g gạo; test tag MoreProtein/MoreVeg; test cỡ chén.
- [ ] **1.3** `IngredientScaler` với 4 kiểu `linear | sublinear | integer | fixed` (§6.1). `→ 1.1`
  - Xong khi: có test cho từng kiểu và test làm tròn đơn vị đếm.
- [ ] **1.4** `NutritionCalculator`: theo suất, theo người, theo mâm, % năng lượng đạm/béo/bột đường, có `consumedRatio` (§6.3). `→ 1.2, 1.3`
  - Xong khi: test với fixture tính tay (số liệu fixture có comment "test-only").
- [ ] **1.5** `ShoppingListBuilder`: gộp, tỷ lệ thải bỏ, đơn vị theo vùng, pantry, chia aisle, text chia sẻ (§6.2). `→ 1.3`
  - Xong khi: golden test cho output text và danh sách với 2 vùng Bắc/Nam.
- [ ] **1.6** `MealSpinner`: ràng buộc cứng, luật nới theo thứ tự, trọng số, khoá ô, đổi 1 ô, lịch sử 5 mâm, `RelaxedRules` (§5). Đọc trọng số từ `data/config/spinner-defaults.json`. `→ 1.2`
  - Xong khi: property test 10.000 seed không vi phạm ràng buộc cứng; cùng seed cho cùng kết quả; ô khoá không đổi; test thứ tự nới luật.
- [ ] **1.7** `CookPlanScheduler` (§5.6). `→ 1.1`
  - Xong khi: test cơm bắt đầu đầu tiên; không chồng bước active; món serveHot kết thúc trong 10 phút cuối; mealTime ≥ tổng thời gian món dài nhất.
- [ ] **1.8** Seed dữ liệu dev: khoảng 20 món (7 canh, 8 mặn, 5 rau) và các nguyên liệu cần thiết, trạng thái `draft`, dinh dưỡng `placeholder`.
  - Xong khi: `DataBuilder build` (dev) chạy được; seed đủ đa dạng để test ràng buộc (có món cay, món chiên, trùng nguồn đạm).

## Phase 2 — Pipeline dữ liệu (M1–M2)

- [ ] **2.1** JSON Schema trong `data/schemas/` cho ingredient, dish, config. `→ 1.1`
  - Xong khi: seed 1.8 hợp lệ theo schema; có fixture sai để test bị bắt lỗi.
- [ ] **2.2** `MamCom.DataBuilder` với lệnh `validate` (luật §7.3), `build` và `build --release`. Output `dist/data-bundle.json` có version hash và `dist/data-report.md`. Bật bước validate trong CI. `→ 2.1`
  - Xong khi: mỗi luật có test với fixture; release fail khi còn placeholder hoặc món `draft`.
- [ ] **2.3** `MamCom.FdcImporter`: từ tên tiếng Anh của nguyên liệu, tìm USDA FDC và trả top 3 ứng viên kèm lý do, ghi vào `data/_suggestions/nutrition/<id>.json`. Không tự ghi vào file nguyên liệu. API key lấy qua user-secrets, nếu không có thì dùng `DEMO_KEY`.
  - Xong khi: chạy được cho 5 nguyên liệu mẫu; có tôn trọng giới hạn request; có test với HTTP được mock.
- [ ] **2.4** `MamCom.RecipeDrafter`: đọc `data/plan/dish-list.csv`, gọi Claude API (model và key cấu hình qua user-secrets, xem docs.claude.com), yêu cầu JSON đúng schema, chỉ dùng ingredient id có sẵn. Nguyên liệu lạ đưa vào `missingIngredients`. Không xin số dinh dưỡng. Ghi vào `data/dishes/_drafts/`. Có khả năng chạy tiếp (resume) và bỏ qua món đã có. `→ 2.1`
  - Xong khi: chạy thử 3 món; output qua được validator hoặc bị gắn cờ rõ ràng; prompt nằm trong file riêng để dễ sửa.
- [ ] **2.5** `MamCom.Studio` (Blazor Server, chạy local), phần duyệt món: danh sách món theo trạng thái; xem và sửa món; hiện dinh dưỡng do Core tính và cảnh báo của validator; nút "Duyệt" ghi `status=reviewed`, `reviewedBy`, `reviewedAt` vào file JSON. Tái dùng component của `MamCom.UI` khi hợp lý. `→ 2.2, 1.4`
  - Xong khi: duyệt được một món seed end-to-end và diff Git sạch, đúng format.
- [ ] **2.6** Studio, phần nguyên liệu: sửa tên, đơn vị, tỷ lệ thải bỏ; chọn ứng viên dinh dưỡng từ 2.3 (ghi `nutritionSource` và `verifiedBy`); gắn ảnh. `→ 2.3, 2.5`
  - Xong khi: xác nhận được dinh dưỡng cho một nguyên liệu và validator nhận.
- [ ] **2.7** Studio, phần nấu thử: nhập thời gian thực đo, khối lượng thành phẩm, ảnh món → `status=cooktested`. `→ 2.5`
- [ ] **2.8** `MamCom.ImageTool` (SkiaSharp): `data/images/raw/` → WebP thumb 200px và full 800px, xoá EXIF, cập nhật đường dẫn ảnh trong JSON.
  - Xong khi: xử lý được ảnh mẫu; validator kiểm tra file ảnh có tồn tại.

## Phase 3 — UI MVP PWA (M2)

- [ ] **3.1** Design tokens CSS (light và dark), self-host font Be Vietnam Pro, Tabler Icons, layout mobile-first, safe area (§8.2).
  - Xong khi: có trang "styleguide" hiển thị token, chữ và nút ở cả 2 mode.
- [ ] **3.2** `BundleLoader`, `IUserStateStore` (localStorage), service worker cache bundle và ảnh thumb, kiểm tra version bundle khi online. `→ 2.2`
  - Xong khi: app chạy offline sau lần tải đầu; đổi bundle thì app nhận bản mới.
- [ ] **3.3** Onboarding "Nhà mình có ai?" (§8.1-1). `→ 3.1, 3.2, 1.2`
  - Xong khi: tạo được hồ sơ 4 người trong ≤ 60 giây; bỏ qua được; có bUnit test cho stepper.
- [ ] **3.4** Màn "Mâm hôm nay": thẻ món, khoá, vuốt đổi, quay có hiệu ứng khoảng 1 giây, rung nhẹ (feature-detect), tóm tắt, "Mâm trước", gợi ý lọc sau 5 lần quay, thông báo luật đã nới, nút "Chốt mâm". `→ 1.6, 1.7, 3.3`
  - Xong khi: luồng quay → khoá → quay → chốt chạy được trên viewport 360×740.
- [ ] **3.5** Chi tiết món: ảnh (nhãn "Ảnh minh hoạ" nếu cần), nguyên liệu "cho X suất", cách làm, dinh dưỡng có thanh tỷ lệ và kcal theo từng người, chân trang "Số liệu ước tính…". `→ 1.4`
- [ ] **3.6** Đi chợ: nhóm theo quầy, thumbnail, checkbox 48px giữ trạng thái, toggle "Hiện đồ có sẵn", nút "Gửi Zalo" (Web Share API, nếu không có thì copy). `→ 1.5`
  - Xong khi: chạy offline; text chia sẻ khớp golden test.
- [ ] **3.7** Chế độ nấu: 1 bước mỗi màn hình, chữ ≥ 22px, hẹn giờ trong bước, chip hẹn giờ song song, Screen Wake Lock (feature-detect và có thông báo), chạm nửa phải để sang bước tiếp, âm báo hoặc rung khi hết giờ. `→ 1.7`
- [ ] **3.8** Phản hồi sau bữa và Cài đặt (thành viên, vùng, cỡ chén, món đã chặn, xoá dữ liệu). Phản hồi cập nhật `pref` cho lần quay sau. `→ 3.4`
- [ ] **3.9** Accessibility và hoàn thiện: kiểm tra contrast, vùng chạm, `aria-label` cho nút chỉ có icon, cỡ chữ hệ thống. E2E Playwright cho luồng chính (§10). `→ 3.3–3.8`
  - Xong khi: E2E xanh trong CI.
- [ ] **3.10** Deploy staging: Claude Code viết workflow deploy. `[NGƯỜI]` tạo resource Azure Static Web Apps và thêm deployment token vào GitHub secrets. `→ 3.9`
  - Xong khi: có URL staging; đo thời gian tải đầu trên 4G và ghi vào nhật ký (mục tiêu ≤ 5 giây sau khi bật trimming và nén).

## Phase 4 — Nội dung thật (M3, làm song song từ tuần 2)

- [ ] **4.1** Claude Code tạo `data/plan/dish-list.csv` gợi ý khoảng 120 món cơm nhà phổ biến (35 canh, 55 mặn, 30 rau, gắn vùng). `[NGƯỜI]` sửa và chốt danh sách.
- [ ] **4.2** Claude Code sinh khung khoảng 250 nguyên liệu từ danh sách món: tên theo vùng, alias, aisle, đơn vị, **không có dinh dưỡng**. `[NGƯỜI]` kiểm tra đơn vị chợ và tỷ lệ thải bỏ trong Studio. `→ 4.1, 2.6`
- [ ] **4.3** Chạy FdcImporter cho toàn bộ nguyên liệu. `[NGƯỜI]` xác nhận ứng viên trong Studio; ưu tiên số từ Bảng thành phần thực phẩm VN nếu có. `→ 4.2`
  - Xong khi: ≥ 95% nguyên liệu dùng trong món đã duyệt có dinh dưỡng đã xác nhận.
- [ ] **4.4** Chạy RecipeDrafter cho toàn bộ danh sách món; xử lý `missingIngredients`; chạy validator đến khi không còn lỗi. `→ 4.2, 2.4`
- [ ] **4.5** `[NGƯỜI]` Mẹ hoặc bà duyệt món trong Studio.
  - Xong khi: ≥ 60 món `reviewed` (đủ để chạy beta); 120 món cho v1.
- [ ] **4.6** `[NGƯỜI]` Chụp ảnh nguyên liệu (2–3 buổi, nền kem, tỷ lệ 1:1), sau đó chạy ImageTool. `→ 2.8`
- [ ] **4.7** `[NGƯỜI]` Nấu thử 30 món phổ biến nhất trong các bữa tối hằng ngày: bấm giờ, cân thành phẩm, chụp ảnh, nhập vào Studio. `→ 2.7`
- [ ] **4.8** `[NGƯỜI]` Chọn ảnh tạm cho món chưa chụp (ảnh AI hoặc ảnh kho có giấy phép, lấy lẻ từng ảnh, `isIllustration: true`).
  - Xong khi: data-report cho thấy 100% món có ảnh.

## Phase 5 — Beta (M4)

- [ ] **5.1** Event log cục bộ, ẩn danh: quay, khoá, chốt, mở đi chợ, mở chế độ nấu, phản hồi, thời gian từ mở app đến chốt. Thêm nút "Xuất phản hồi" (JSON và text). Không thu thập thông tin cá nhân. `→ 3.9`
- [ ] **5.2** `[NGƯỜI]` Cho 5 người nấu chính thật dùng thử 1–2 tuần; ghi vấn đề vào `docs/beta-notes.md`. `→ 4.5, 3.10`
- [ ] **5.3** Phân tích log beta theo các chỉ số ở §11. Tinh chỉnh hằng số khẩu phần và trọng số quay; ghi ADR cho mỗi thay đổi. `→ 5.2`
- [ ] **5.4** Sửa lỗi và tối ưu theo mức ưu tiên từ beta-notes.

## Phase 6 — Ra mắt v1 (M5)

- [ ] **6.1** `DataBuilder build --release` sạch: chỉ món ≥ `reviewed`, không còn placeholder. `→ 4.5`
- [ ] **6.2** Trang "Giới thiệu và nguồn dữ liệu": ghi nguồn USDA FoodData Central và Viện Dinh dưỡng, câu "Số liệu ước tính, không thay tư vấn y tế", thông tin quyền riêng tư (dữ liệu chỉ lưu trên máy).
- [ ] **6.3** `[QUYẾT ĐỊNH]` Có thương mại hoá không. Nếu có: `[NGƯỜI]` xin phép hoặc kiểm tra điều khoản dữ liệu của Viện Dinh dưỡng.
- [ ] **6.4** `[NGƯỜI]` Deploy production và gắn tên miền (nếu có).

## Backlog sau v1 (chưa làm)

- Thực đơn tuần và đi chợ gộp tuần.
- .NET MAUI Blazor Hybrid (tái dùng `MamCom.UI`).
- Zalo Mini App (kiểm chứng Wake Lock và offline trước).
- Gợi ý theo "tủ lạnh còn gì", ước tính chi phí đi chợ.
- Đồng bộ cả nhà (ASP.NET Core API + PostgreSQL).
- Nhập bằng giọng nói, ảnh mâm cơm do người dùng đăng.

---

## Nhật ký

| Ngày | Task | Ghi chú |
|---|---|---|
| 2026-10-18 | — | Tạo SPEC/PLAN từ phần phân tích ban đầu |
| 2026-10-18 | 0.1 | Solution `MamCom.sln` 13 project (4 src, 5 tools, 4 tests), `Directory.Build.props`, `.editorconfig`, `global.json`, central package management. Build 0 warning. Tool project mới là khung, thoát mã 1 cho tới task của chúng |
| 2026-10-18 | 0.2 | xunit.v3 (chạy qua Microsoft.Testing.Platform, bật bằng `test.runner` trong `global.json`), Shouldly, bUnit, Playwright; 4 test mẫu xanh. Đã sửa `.gitignore`: dòng `*.e2e` của mẫu Visual Studio làm `tests/MamCom.E2E` không vào git, clone sạch fail ở restore |
| 2026-10-18 | 0.3 | `ci.yml` đã viết (restore, build, test; bước validate tắt tới task 2.2). Chưa tick: repo chưa có remote nên chưa xác nhận được "pipeline xanh trên nhánh main". Cần tạo repo GitHub, thêm remote và push `main` |
| 2026-10-18 | 0.4 | ADR-001..003 trong `docs/decisions/` và `README.md` quick start |
| 2026-10-18 | 0.5 | Chủ dự án xác nhận dùng mặc định: tên `MamCom`, hosting Azure Static Web Apps |
| 2026-10-19 | 1.1 | Record domain trong `MamCom.Core` (13 kiểu theo PLAN cùng `MarketUnit`, `ImageRef`, `NutritionSource`, `Provenance`), `IRandomSource` (SplitMix64 tự cài để cùng seed ra cùng chuỗi trên mọi runtime), `IClock`. JSON qua source generator: enum chỉ nhận đúng một tên, thiếu trường bắt buộc thì lỗi; test round-trip bằng mẫu SPEC §7.1–7.2. SPEC §5.6 thêm `ActiveMinutes` và id `com` (ADR-004). Việc mở cho task sau: vùng Trung chưa có quy tắc fallback cho `marketUnit` và chưa rõ khi nào hiện "(~Ng)" trong text chia sẻ (1.5, 2.2); luật nới `AvailableMinutes` của 1.6 cần mealTime từ 1.7; ghi file JSON phải thêm `\n` cuối (2.2, 2.5) |
| 2026-10-19 | 1.2 | `PortionCalculator`, `PortionConfig` và `data/config/portion-defaults.json` (file dữ liệu đầu tiên). Mọi hằng số đọc từ config và được kiểm khi tạo calculator. Gia đình mẫu SPEC §4.2 ra 3,7875 suất và 320g gạo; so với bản tham chiếu độc lập (Fraction) hơn 40.000 ca không lệch. Rice làm tròn có ngưỡng 1e-9 để 320,00000000000006 không thành 330g. `Validate` chỉ kiểm giá trị hữu hạn và dương; kiểm khoảng hợp lý và quan hệ giữa các giá trị thuộc JSON Schema ở 2.1 |
| 2026-10-19 | 0.3 | Đã push `main` lên GitHub. Run `ci` [39191007838](https://github.com/thoghoaq/mamcom/actions/runs/39191007838) xanh trên commit 530f0d9 (ubuntu-24.04, build 0 warning, 168 test, mất 49 giây). Bước "Validate data" bị bỏ qua theo thiết kế tới task 2.2. GitHub báo `ubuntu-latest` chuyển sang Ubuntu 26 từ 2026-10-19 |
