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
- [x] **1.3** `IngredientScaler` với 4 kiểu `linear | sublinear | integer | fixed` (§6.1). `→ 1.1`
  - Xong khi: có test cho từng kiểu và test làm tròn đơn vị đếm.
- [x] **1.4** `NutritionCalculator`: theo suất, theo người, theo mâm, % năng lượng đạm/béo/bột đường, có `consumedRatio` (§6.3). `→ 1.2, 1.3`
  - Xong khi: test với fixture tính tay (số liệu fixture có comment "test-only").
- [x] **1.5** `ShoppingListBuilder`: gộp, tỷ lệ thải bỏ, đơn vị theo vùng, pantry, chia aisle, text chia sẻ (§6.2). `→ 1.3`
  - Xong khi: golden test cho output text và danh sách với 2 vùng Bắc/Nam.
- [x] **1.6** `MealSpinner`: ràng buộc cứng, luật nới theo thứ tự, trọng số, khoá ô, đổi 1 ô, lịch sử 5 mâm, `RelaxedRules` (§5). Đọc trọng số từ `data/config/spinner-defaults.json`. `→ 1.2`
  - Xong khi: property test 10.000 seed không vi phạm ràng buộc cứng; cùng seed cho cùng kết quả; ô khoá không đổi; test thứ tự nới luật.
- [x] **1.7** `CookPlanScheduler` (§5.6). `→ 1.1`
  - Xong khi: test cơm bắt đầu đầu tiên; không chồng bước active; món serveHot kết thúc trong 10 phút cuối; mealTime ≥ tổng thời gian món dài nhất.
- [x] **1.8** Seed dữ liệu dev: khoảng 20 món (7 canh, 8 mặn, 5 rau) và các nguyên liệu cần thiết, trạng thái `draft`, dinh dưỡng `placeholder`.
  - Xong khi: `DataBuilder build` (dev) chạy được; seed đủ đa dạng để test ràng buộc (có món cay, món chiên, trùng nguồn đạm).
  - Hoàn tất ở task 2.2: `build` (dev) chạy sạch trên seed, 0 lỗi, 0 cảnh báo.

## Phase 2 — Pipeline dữ liệu (M1–M2)

- [x] **2.1** JSON Schema trong `data/schemas/` cho ingredient, dish, config. `→ 1.1`
  - Xong khi: seed 1.8 hợp lệ theo schema; có fixture sai để test bị bắt lỗi.
- [x] **2.2** `MamCom.DataBuilder` với lệnh `validate` (luật §7.3), `build` và `build --release`. Output `dist/data-bundle.json` có version hash và `dist/data-report.md`. Bật bước validate trong CI. `→ 2.1`
  - Xong khi: mỗi luật có test với fixture; release fail khi còn placeholder hoặc món `draft`; `build` (dev) chạy sạch trên seed 1.8 (khi đó tick 1.8). Luật "nguyên liệu không xuất hiện ở bước nào" bỏ qua nguyên liệu gạo (`riceIngredientId`), vì cơm không nằm trong món nào.
- [x] **2.3** `MamCom.FdcImporter`: từ tên tiếng Anh của nguyên liệu, tìm USDA FDC và trả top 3 ứng viên kèm lý do, ghi vào `data/_suggestions/nutrition/<id>.json`. Không tự ghi vào file nguyên liệu. API key lấy qua user-secrets, nếu không có thì dùng `DEMO_KEY`.
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
| 2026-10-19 | 1.3 | `IngredientScaler` với 4 kiểu `linear`, `sublinear`, `integer`, `fixed` và `ScaleDish`. Số mũ 0,8 chuyển vào `portion-defaults.json`. Lấp hai khoảng trống của SPEC (ADR-005): `marketUnit` nhận khoá `default` (vùng Trung dùng khoá này), nguyên liệu `integer` dùng `gramsPerUnit` của vùng và lượng thực tế là số đơn vị × `gramsPerUnit`. Test đã thử với 15 đột biến, bắt được cả 15. Việc mở: `DataBuilder validate` cần luật mỗi nguyên liệu dùng ở món có đơn vị chợ cho cả ba vùng (2.2); câu hỏi "(~Ng)" trong text chia sẻ vẫn mở cho 1.5. SDK máy đã lên 10.0.402 giữa chừng, build vẫn 0 warning |
| 2026-10-19 | 1.4 | `NutritionCalculator`: theo suất món (có `consumedRatio`), theo người (suất × `servings_i` + cơm của chính người đó), theo mâm, và tỷ lệ năng lượng đạm/béo/bột đường. Thêm `data/config/nutrition-defaults.json` (hệ số 4/9/4, khoảng khuyến nghị) và `riceIngredientId` (`gao`) trong `portion-defaults.json`. Giá trị `null` lan truyền, không bao giờ thành 0 (ADR-006). Fixture tính tay có comment "test-only", đối chiếu bằng số học chính xác bên ngoài C#; 20 đột biến, bắt được cả 20 sau khi siết một test. Việc mở: seed 1.8 phải có nguyên liệu `gao`; giao diện 3.5 cần trạng thái "chưa đủ số liệu" |
| 2026-10-19 | 1.5 | `ShoppingListBuilder` (gộp rồi mới làm tròn, tỷ lệ thải bỏ, đơn vị chợ theo vùng, cơm, pantry gắn cờ), `ShoppingGroups.ByAisle` và `ShoppingTextFormatter`. Golden test tái hiện đúng từng chữ ví dụ SPEC §6.2 cho Bắc ("3 lạng thịt ba chỉ") và Nam ("300 g thịt ba rọi"). Chốt các chỗ SPEC chưa nói (ADR-007): `marketUnit.showApproxGrams` quyết định khi nào hiện "(~Ng)"; trứng, đồ khô và khác cùng dòng KHÁC; một công thức làm tròn cho cả đơn vị cân và đếm. `ShoppingItem` thêm `ApproxGrams`. 25 đột biến, bắt được cả 25 sau khi siết hai test (nhiễu dấu phẩy động, tag theo quầy). Việc mở: `validate` (2.2) cần luật đơn vị chợ cho cả ba vùng và cảnh báo `showApproxGrams` trên đơn vị cân; từ "tối" trong tiêu đề đang cố định |
| 2026-10-19 | 1.6 | `MealSpinner` (khung mâm, luật cứng, chọn có trọng số, nới luật tuần tự, đổi một ô, khoá ô), `SpinHistory`, `SpinnerConfig` + `data/config/spinner-defaults.json`, `DishPreference`. Thời gian cả mâm qua `IMealTimeEstimator`: **task 1.7 phải hiện thực nó** (thêm món không làm thời gian ngắn đi). Property test 10.000 seed với bản luật viết độc lập: không vi phạm luật cứng, ô khoá giữ nguyên, tái lập theo seed, `RelaxedRules` đúng sự thật. 37 đột biến đều bị bắt. Một agent đọc độc lập tìm ra lỗi thật: ngưỡng "≥ 5 suất" lệch do sai số cộng theo thứ tự thành viên, đã sửa và có test. Chốt các chỗ SPEC chưa nói (ADR-008), gồm mã mùa và bảng tháng sang mùa do tôi đặt. `SpinResult` thêm `UnfilledSlots`. Việc mở: `Reroll` ném lỗi khi món của mâm không còn trong danh mục, UI 3.4 phải bắt; 3.2 nên chặn ngày ăn lần cuối ở tương lai |
| 2026-10-19 | 1.7 | `CookPlanScheduler` lập lịch cho một người nấu (bước cần tay không chồng nhau, bước chờ chạy song song, cơm bắt đầu ở phút 0, món `serveHot` xếp cuối) và hiện thực `IMealTimeEstimator`; cơm nằm trong `data/config/cooking-defaults.json` (`CookingConfig`). Thuật toán tham lam không đơn điệu (fuzz tìm ra ví dụ 140 → 132 phút khi thêm một món), nên thêm `MinimumMealMinutes`, một cận dưới không giảm khi thêm món, và `MealSpinner` dùng nó để bỏ luật thời gian sớm; việc này sửa giả định ở ADR-008 (ADR-009, SPEC §5.5 và §5.6 đã cập nhật). Bảo đảm cho món `serveHot`: không kết thúc trước phần còn lại của mâm, và kết thúc cách `mealTime` không quá tổng thời gian các món `serveHot` kết thúc sau nó. Property test 10.000 mâm với bản kiểm độc lập, 3.000 hoán vị thứ tự đầu vào, 30.000 mâm cho cận dưới. 22 đột biến đều bị bắt (hai cái bị bắt bằng cách làm bộ lập lịch treo, nên vòng thử đột biến phải có giới hạn thời gian mỗi lần chạy). Việc mở: chế độ nấu 3.7 hiển thị theo `StartMinute`, hai bước có thể bắt đầu cùng phút; câu "10 phút cuối" của SPEC chỉ đúng khi tổng thời gian các món xào không quá 10 phút |
| 2026-10-19 | 1.8 | Seed dev: 33 nguyên liệu (có `gao`, đánh dấu pantry) và 20 món (7 canh, 8 mặn, 5 rau) trong `data/ingredients`, `data/dishes`; mọi món `draft`, `draftedBy: llm`, công thức tôi soạn mới; dinh dưỡng toàn `null` với `source: placeholder`, không ảnh. Đa dạng cho các luật: có món cay `required` và `optional`, 3 món chiên, 2 món mặn cùng đạm heo và 2 canh cũng đạm heo, đủ 4 kiểu `scale`, đủ ba vùng, mùa `all`/`summer`/`autumn`/`winter` (mã mùa theo ADR-008), món `serveHot` và không, món 87 phút (thịt kho trứng) và món 9 phút (trứng chiên). Test mới (`Seed/`, 31 test): file đúng định dạng chuẩn, tham chiếu hợp lệ, mỗi dòng công thức có trong một bước, không số dinh dưỡng bịa; quay 300 seed mỗi vùng và cỡ hộ ra mâm đủ, không nới luật; danh sách đi chợ, dinh dưỡng và kế hoạch nấu chạy được trên seed. 23 đột biến lên chính file dữ liệu, bắt được cả 23. **Chưa tick:** tiêu chí "`DataBuilder build` chạy được" chưa làm được vì DataBuilder mới là khung; chuyển thành điều kiện hoàn tất của 2.2. Đơn vị chợ, tỷ lệ thải bỏ, số gram và thời gian là giá trị dev do tôi đặt, `[NGƯỜI]` kiểm ở 4.x; `consumedRatio` của dầu chiên để 1 vì chưa có nguồn đo |
| 2026-10-19 | 2.1 | CI xanh trên `a84386a` (run 39209623877, 51 giây) sau khi bạn push. Sáu JSON Schema draft 2020-12 trong `data/schemas/`: `ingredient`, `dish` và một cho mỗi file config. Kiểm bằng `SchemaSet` (trong `MamCom.DataBuilder`, dùng thư viện `LateApexEarlySpeed.Json.Schema`, BSD-3; loại `JsonSchema.Net` 9.x vì giấy phép OSMF; xem ADR-010). Seed 1.8 và 4 file config hợp lệ. Schema chặt hơn đọc dữ liệu ở Core: cấm trường lạ, `placeholder` thì không có số dinh dưỡng, vòng đời món (không dấu duyệt khi còn `draft`), `names`/`marketUnit` có `default` hoặc đủ ba vùng, ảnh phải có source và license, `consumedRatio` < 1 phải có `note`. Thư viện liệt kê cả các nhánh `if`/`anyOf`/`not` đã thất bại mà không làm file sai nên `SchemaSet` lọc nhiễu, nếu không báo cáo sẽ nói "thiếu north" cho `names` hợp lệ. Test (156 trong `MamCom.DataBuilder.Tests`): 95 ca file hỏng từng điều một, mỗi ca phải chỉ đúng vị trí và từ khoá; 13 ca khẳng định một lỗi chỉ ra một dòng báo cáo; test so enum, danh sách trường và trường bắt buộc của 21 kiểu Core với schema để hai nguồn không lệch. 25 đột biến lên chính các file schema, bắt được cả 25 (một cái sống sót lúc đầu vì test chỉ thử `note: null`, đã thêm ca bỏ hẳn trường). Việc mở cho 2.2: các luật schema không diễn đạt được (id trùng, tham chiếu chéo, mã mùa, `order` liền nhau, đơn vị chợ cho `integer`, file ảnh, cảnh báo kcal/đạm/natri, quan hệ giữa các giá trị config qua `Validate` của Core) và bước "Validate data" trong CI; `ci.yml` vẫn bỏ qua bước đó. Thư viện kéo theo `Microsoft.Extensions.Http` 3.1.0; NuGet audit không báo lỗ hổng |
| 2026-10-19 | 2.2 | `MamCom.DataBuilder` với `validate [--data] [--release]` và `build [--data] [--out] [--release]` (mã thoát 0 sạch, 1 dữ liệu còn lỗi, 2 gõ sai lệnh). Nạp `data/` (schema trước, rồi `Parse` và `Validate` của Core cho config), kiểm các luật §7.3 và ADR-010: tên file khớp id, id trùng, nguyên liệu và mã mùa không tồn tại, `order` liền nhau, dòng công thức lặp, file ảnh, gạo có mặt; cảnh báo kcal, đạm món mặn, natri, nguyên liệu không có bước, `showApproxGrams` trên đơn vị cân; release thêm placeholder và món dưới `reviewed`, cảnh báo thành lỗi. Ngưỡng cảnh báo vào `data/config/validation-defaults.json` (có schema). `DataBundle` ở `MamCom.Core` (`version` 16 hex là hash nội dung, `isRelease`, config, nguyên liệu và món xếp theo id; cùng dữ liệu cho cùng từng byte). Bundle seed khoảng 85 KB. `build` luôn ghi `data-report.md`, chỉ ghi `data-bundle.json` khi hết lỗi, và xoá bundle cũ khi thất bại. **Chốt hai chỗ lệch nhau giữa tài liệu (ADR-011):** `--release` từ chối chứ không lọc món (CLAUDE.md viết "chỉ món reviewed", SPEC và PLAN viết "lỗi"); món chưa xong để trong `dishes/_drafts/`. Một file nguyên liệu hỏng chỉ báo một lần, không kéo theo lỗi "nguyên liệu không tồn tại" ở các món dùng nó. CI bật `validate` và `build` (dev). Seed: `validate` 0 lỗi 0 cảnh báo, `build` ra bundle; `build --release` thất bại với 53 lỗi (33 nguyên liệu placeholder, 20 món `draft`) và chỉ ghi báo cáo. Test: 336 trong `MamCom.DataBuilder.Tests` (mỗi luật có fixture, báo cáo, bundle, dòng lệnh) và 11 cho `DataBundle` trong Core. 81 đột biến lên mã DataBuilder và `DataBundle`: 16 cái không biên dịch (`if (false)` thành mã không với tới) làm lại bằng điều kiện biên dịch được, một cái trùng lặp là mã thừa (`Distinct`) nên bỏ mã, hai cái sống sót thật (ảnh chỉ có `full`, mặc định `--out`) thêm test; cuối cùng bắt được hết. Phát hiện khi làm: `HasCorrectVersion` ban đầu là thuộc tính nên bị ghi vào JSON và lặp vô hạn, đổi thành phương thức. Tick luôn 1.8. Việc mở: ImageTool (4.x) phải ghi ảnh phẳng vào `data/images/out/`; nhãn đơn vị cân nằm trong code; trên Linux (CI) thứ tự đọc file dựa vào `Order` chứ không phải hệ điều hành, test trên Windows không chứng minh được điều đó |
| 2026-10-19 | 2.3 | `MamCom.FdcImporter suggest`: với mỗi nguyên liệu, tìm USDA FDC (Foundation và SR Legacy) bằng tên tiếng Anh trong `data/plan/ingredient-names-en.csv` (33 nguyên liệu seed, từ khoá tôi soạn) và ghi ba ứng viên vào `data/_suggestions/nutrition/<id>.json`, theo schema mới `nutrition-suggestion`. Mỗi ứng viên có `fdcId`, `detailsUrl`, số liệu trên 100g giữ nguyên của FDC (cùng tên trường với `nutritionPer100gEdible`, để Studio 2.6 chép thẳng), chất còn thiếu và lý do xếp hạng bằng tiếng Việt. **Không bao giờ sửa file nguyên liệu**: có test so từng byte. Xếp hạng: mô tả nói raw, ít chất thiếu, khớp nhiều từ khoá, Foundation trước SR Legacy, điểm FDC, `fdcId`. Foundation không có chất Energy thường nên lấy năng lượng từ Energy, rồi Atwater Specific, rồi Atwater General; kJ không bao giờ thành kcal. Khoá API qua user-secrets (`Fdc:ApiKey`), không có thì `DEMO_KEY`; khoá nằm trong header chứ không trong URL và không in ra. Tôn trọng giới hạn: đọc `X-RateLimit-Remaining` và dừng trước lần gọi kế khi hết, nghỉ 1 giây giữa các lần, trần 25 lần mỗi lượt, 429 chờ theo `Retry-After` nếu không quá 60 giây (thử lại tối đa 2 lần), 401/403 dừng ngay, lỗi của một nguyên liệu không chặn nguyên liệu khác; lượt sau chỉ hỏi phần chưa có file. Chạy thật bằng `DEMO_KEY` cho 5 nguyên liệu mẫu (gạo, trứng gà, cà chua, thịt ba chỉ, cà rốt): 5 lần gọi, 0 lỗi, hạng 1 đều đúng loại thực phẩm; năm file được commit làm mẫu cho Studio. Khi thử API: giá trị `dataType` có ngoặc (`Survey (FNDDS)`) làm cổng trả 400, và DEMO_KEY qua cổng này báo giới hạn 10. Test: 182 trong `MamCom.FdcImporter.Tests` (project mới) với HTTP giả (client, bộ trích chất, xếp hạng, đọc CSV, runner, dòng lệnh, schema, năm file mẫu thật). 81 đột biến lên mã importer: 6 lần đầu không bị bắt hoặc không biên dịch, trong đó ba cái là thiếu test (mặc định `--top`, mặc định `--max-wait-seconds`, dấu nháy giữa trường CSV), hai cái sửa mẫu đột biến, một cái là quy tắc "ss" của bộ cắt số nhiều không bao giờ ảnh hưởng kết quả nên bỏ mã; cuối cùng bắt được hết. Việc mở: ứng viên hạng 2 và 3 đôi khi không liên quan (hỏi cà chua ra chuối), người chọn đọc lý do; có nguyên liệu FDC không có hoặc khác loại (cá lóc, rau ngót) và Bảng thành phần thực phẩm Việt Nam, nguồn ưu tiên số 1 của §6.3, công cụ này không đọc; chạy hết 33 nguyên liệu cần khoá riêng hoặc nhiều lượt |
