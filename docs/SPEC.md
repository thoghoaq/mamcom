# SPEC — MâmCơm (tên tạm)

Tài liệu này tổng hợp toàn bộ phần phân tích trước khi làm. Các con số đánh dấu *(mặc định)* là giá trị khởi điểm, nằm trong `data/config/` và sẽ tinh chỉnh sau beta.

---

## 1. Vấn đề và mục tiêu

**Vấn đề.** Mỗi ngày người nấu chính phải trả lời "Hôm nay ăn gì?". Sau đó còn phải tự ước lượng nấu bao nhiêu cho vừa cả nhà, mua gì, mua bao nhiêu và nấu theo thứ tự nào.

**Khoảng trống thị trường.** Các app hiện có thuộc hai nhóm:
- random **một món**, có lọc theo bữa và chặn món không thích;
- kho công thức hoặc gợi ý thực đơn chung.

Chưa có app nào lấy **thành phần gia đình** làm đầu vào để trả ra mâm cơm cân đối kèm định lượng, danh sách đi chợ, dinh dưỡng và trình tự nấu.

**Ý tưởng cốt lõi.** Quay *cả mâm*, không quay một món. App tự tính *nấu bao nhiêu, mua gì* cho đúng nhà mình.

**Mục tiêu UX.** Từ lúc mở app đến khi biết tối nay nấu gì và mua gì: **dưới 60 giây** (lý tưởng là 30 giây).

**Ngoài phạm vi v1:**
- chế độ ăn bệnh lý (tiểu đường, thận…) và đếm calo giảm cân;
- tài khoản và đồng bộ nhiều thiết bị;
- mạng xã hội và ảnh do người dùng đăng;
- thực đơn tuần.

## 2. Người dùng và bối cảnh

| Ai | Vai trò |
|---|---|
| Người nấu chính (thường là mẹ/vợ, 30–55 tuổi) | Người dùng chính |
| Người đi chợ hộ | Nhận danh sách qua Zalo |
| Ông bà | Cần chữ to, thao tác đơn giản |
| Trẻ con | Thích bấm "Quay" |

Có 4 khoảnh khắc sử dụng, mỗi khoảnh khắc có chế độ giao diện riêng (chi tiết ở §8):
1. **Quyết định** (30 giây, lúc tan làm).
2. **Đi chợ** (một tay xách đồ, ngoài trời nắng).
3. **Đứng bếp** (tay ướt).
4. **Sau bữa** (phản hồi 1 chạm).

Bối cảnh Việt Nam: Zalo là kênh nhắn tin chính, đặc biệt với nhóm 44–64 tuổi. Vì vậy chia sẻ danh sách đi chợ qua Zalo là tính năng bắt buộc.

## 3. Phạm vi MVP (v1)

1. Onboarding hồ sơ gia đình trong 60 giây hoặc ít hơn.
2. Quay mâm: khoá món, đổi riêng từng món, quay lại 5 mâm trước.
3. Chi tiết món: nguyên liệu đã định lượng, cách làm, dinh dưỡng.
4. Danh sách đi chợ: gộp cả mâm, chia theo quầy, đơn vị chợ, có ảnh, chia sẻ qua Zalo.
5. Chế độ nấu: trình tự gộp mọi món, hẹn giờ, giữ màn hình sáng.
6. Phản hồi sau bữa 1 chạm để học khẩu vị.
7. Dữ liệu: khoảng 250 nguyên liệu và 120 món (tối thiểu 60 món đã duyệt để chạy beta), kèm ảnh.

Để sau v1: thực đơn tuần và đi chợ gộp tuần, "tủ lạnh còn gì", ước tính chi phí, app MAUI, Zalo Mini App, đồng bộ nhiều thiết bị.

---

## 4. Mô hình khẩu phần ("suất")

### 4.1 Đầu vào

**Thành viên (`Member`):**
- `DisplayName`.
- `AgeGroup`: `Toddler` (2–5 tuổi) | `Child` (6–11) | `Teen` (12–17) | `Adult` (18–59) | `Senior` (60+).
- `BowlsPerMeal`: số chén cơm mỗi bữa, từ 0,5 đến 5, bước 0,5. Đây là câu hỏi chính trên UI: **"Thường ăn mấy chén cơm?"**
  - Mặc định theo nhóm tuổi: Toddler 1, Child 1,5, Teen 2,5, Adult 2, Senior 1,5.
- Tag: `MoreProtein` (ăn nhiều thịt), `MoreVeg` (ăn nhiều rau), `NoSpicy` (không ăn cay).
- `Avoid`: danh sách ingredient id hoặc category bị kiêng (ví dụ `bo`, `hai-san`).

**Hộ gia đình (`Household`):**
- `Region`: `north` | `central` | `south`. Ảnh hưởng tên nguyên liệu, đơn vị chợ và trọng số món.
- `BowlSize`: `small` 35g | `medium` 40g *(mặc định)* | `large` 50g gạo sống mỗi chén.
- `AvailableMinutes`: tuỳ chọn, nhập theo từng lần quay.

### 4.2 Công thức tính

```
servings_i       = (0.4 + 0.3 × bowls_i) × ageMod(ageGroup_i)
ageMod           = Toddler 0.6 | Child 0.85 | Teen 1.0 | Adult 1.0 | Senior 0.9     (mặc định)
baseServings     = Σ servings_i
proteinServings  = Σ servings_i × (MoreProtein ? 1.2 : 1.0)
vegServings      = Σ servings_i × (MoreVeg ? 1.2 : 1.0)
rawRiceGrams     = ceil10( Σ bowls_i × gramsPerBowl )
```

Mỗi ô mâm dùng một tổng suất khác nhau: **món mặn** dùng `proteinServings`, **rau** dùng `vegServings`, **canh** dùng `baseServings`.

**Ví dụ kiểm thử** (gia đình 4 người, chén cỡ vừa):

| Thành viên | Nhóm tuổi | Số chén | Suất |
|---|---|---|---|
| Bố | Adult | 3 | 1,3 |
| Mẹ | Adult | 2 | 1,0 |
| Con 10 tuổi | Child | 1,5 | 0,85 × 0,85 = 0,7225 |
| Bà | Senior | 1,5 | 0,85 × 0,9 = 0,765 |

Tổng `baseServings ≈ 3,79` (hiển thị "3,8 suất"). Gạo: 8 chén × 40g = 320g.

**Tham chiếu để đặt khoảng hợp lệ cho validator** (hướng dẫn chế độ ăn Việt Nam 2016–2020):
- 1 đơn vị tinh bột ≈ 55g cơm chín.
- 1 đơn vị đạm (7g protein) ≈ 31g thịt heo chín, 35g cá chín hoặc 58g đậu phụ.
- 1 đơn vị rau = 80g rau ăn được.

Mức thông dụng khác:
- 90–150g gạo khô mỗi người mỗi bữa.
- Khoảng 150g thịt/cá mỗi ngày.
- Khoảng 100g rau mỗi bữa chính.

## 5. Mâm cơm và thuật toán quay

### 5.1 Khung mâm

Mâm gồm cơm + 1 canh + 1 món mặn + 1 rau. Nếu `baseServings ≥ 5` *(mặc định)* thì thêm 1 món mặn thứ hai.

### 5.2 Ràng buộc cứng (không bao giờ nới)

1. Món chứa nguyên liệu hoặc nhóm nằm trong `Avoid` của bất kỳ ai → loại.
2. Có thành viên `NoSpicy` → loại món có `spicy = "required"`. Món `optional` vẫn được chọn; UI ghi "để ớt riêng".
3. Món bị chặn (`blocked`) → loại.
4. Món đã ăn trong 2 ngày gần nhất → loại.
5. Ô đã khoá giữ nguyên.

### 5.3 Ràng buộc có thể nới (theo thứ tự nới)

1. Trọng số mùa và vùng miền (vốn đã là luật mềm).
2. `mealTime ≤ AvailableMinutes`.
3. Tối đa 1 món có `cookingMethod = "chien"`.
4. Không trùng `proteinSource` giữa các món trong mâm. Các giá trị `none` và `chay` không tính trùng.

### 5.4 Trọng số mềm

```
w = recency(d) × pref × season × region
recency: d ≤ 2 → loại | 3–6 ngày → 0.3 | ≥ 7 hoặc chưa ăn → 1.0
pref:    liked × 2.0 | disliked × 0.2 | neutral × 1.0
season:  khớp mùa hiện tại × 1.3
region:  khớp vùng hộ × 1.2
```

### 5.5 Thuật toán `MealSpinner`

1. Xác định các ô cần quay (ô chưa khoá). Ô nào có ít ứng viên hợp lệ nhất thì quay trước.
2. Với mỗi ô: lọc theo ràng buộc cứng, rồi chọn ngẫu nhiên có trọng số qua `IRandomSource`.
3. Kiểm tra ràng buộc cấp mâm. Nếu vi phạm thì quay lại, tối đa 50 lần.
4. Vẫn thất bại thì nới một luật theo thứ tự ở §5.3 và lặp lại. Kết quả trả về `RelaxedRules[]` để UI thông báo.
5. Đổi một món (vuốt thẻ) = chỉ quay lại ô đó, dùng cùng các ràng buộc.
6. Lưu 5 mâm gần nhất (`SpinHistory`) để có nút "Mâm trước".
7. Kết quả phải tái lập được: cùng seed và cùng input thì ra cùng mâm.

### 5.6 `CookPlanScheduler`: thời gian mâm và trình tự nấu

Mỗi bước (`Step`) có `ActiveMinutes` (cần tay người) và `PassiveMinutes` (ninh, ướp, nồi chạy).

Bộ lập lịch mô phỏng **một người nấu**:
- **Cơm** là món giả và luôn bắt đầu đầu tiên: vo gạo 5 phút (active), nồi cơm 30 phút (passive) *(mặc định)*.
- **Thứ tự món:** món có tổng thời gian dài nhất bắt đầu trước.
- **Món `serveHot`** (xào, rau) được đẩy về cuối để kết thúc trong 10 phút cuối.
- **Không bao giờ có 2 bước active chồng lên nhau.** Khi bước hiện tại là passive (tay rảnh), lấy bước active kế tiếp của món khác.
- **Output:** danh sách `CookPlanStep { StartMinute, DishId, Text, ActiveMinutes, TimerMinutes? }`.
  - `ActiveMinutes` là phần cần tay người; có nó mới kiểm tra được hai bước active không chồng nhau.
  - Cơm là món giả với `DishId = "com"`. Id này dành riêng, món thật không được dùng.
- **`mealTime`** = thời điểm bước cuối cùng kết thúc. Đây là con số "Thời gian cả mâm" trên UI. Thời gian cả mâm không phải tổng thời gian các món.

---

## 6. Định lượng, đi chợ, dinh dưỡng

### 6.1 Co giãn nguyên liệu (`IngredientScaler`)

`S` = số suất của ô tương ứng (§4.2). Công thức lưu theo 1 suất.

| `scale` | Công thức | Dùng cho |
|---|---|---|
| `linear` | g × S | thịt, cá, rau |
| `sublinear` | g × S^0.8 *(mặc định)* | gia vị, dầu, nước mắm |
| `integer` | ceil(g × S / gramsPerUnit) đơn vị | trứng, quả cà chua, con cá |
| `fixed` | g | lá thơm trang trí… |

- Số mũ của `sublinear` nằm trong `data/config/portion-defaults.json` (`sublinearExponent`, lớn hơn 0 và không quá 1).
- Với `integer`, `gramsPerUnit` lấy từ `marketUnit` của vùng (xem §7.1). Lượng dùng thực tế là số đơn vị nguyên nhân với `gramsPerUnit`, không phải `g × S`.
- Làm tròn lên bỏ qua sai số dấu phẩy động: 3 × 0,1 vẫn ra 3 đơn vị, không thành 4.

### 6.2 Danh sách đi chợ (`ShoppingListBuilder`)

- Tính lượng cần mua: `purchaseGrams = Σ gramsEdible / (1 − wasteRatio)`.
- Gộp theo `ingredientId` trên cả mâm, gồm cả gạo.
- Quy đổi ra đơn vị chợ theo `marketUnit` của vùng:
  - đơn vị theo khối lượng (lạng, g, kg) làm tròn lên theo `step`;
  - đơn vị đếm (bó, mớ, quả, con) = `ceil(purchaseGrams / gramsPerUnit)`.
- Ẩn các món `isPantry` (bếp luôn có). Có toggle "Hiện đồ có sẵn".
- Chia nhóm theo `aisle`: `thit-ca` | `rau-cu-qua` | `trung-dau-phu` | `kho-gia-vi` | `khac`.
- Xuất text để chia sẻ (Web Share API, nếu không có thì copy vào clipboard):

```
Đi chợ tối CN 18/10 (4 người)
THỊT CÁ: 3 lạng thịt ba chỉ · 1 con cá lóc (~500g)
RAU: 1 mớ rau muống · 2 quả cà chua · 1 mớ bạc hà
KHÁC: 4 quả trứng
```

### 6.3 Dinh dưỡng (`NutritionCalculator`)

**Dữ liệu nguyên liệu**, tính trên 100g phần ăn được: `Kcal`, `ProteinG`, `FatG`, `CarbG`, `FiberG`, `SodiumMg`.

**Công thức:**
- Mỗi suất của món: `Σ gramsEdiblePerServing × consumedRatio × nutrient / 100`.
  - `consumedRatio` mặc định là 1. Dầu chiên ngập dùng giá trị nhỏ hơn, do người nhập và có ghi chú nguồn.
- Theo người: suất của món × `servings_i`. Riêng cơm tính theo `bowls_i × gramsPerBowl`.
- Cả mâm: tổng của tất cả.

**% năng lượng** tính với hệ số 4/9/4 kcal mỗi gram (đạm/béo/bột đường), so với khuyến nghị: đạm 13–20%, béo 20–25%, bột đường 55–65%.

**Hiển thị:**
- "≈" và làm tròn 10 kcal.
- Một thanh tỷ lệ đạm/béo/bột đường so với khuyến nghị.
- Kcal theo từng người.
- Chân trang: "Số liệu ước tính, không thay tư vấn y tế."

**Nguồn dữ liệu, theo thứ tự ưu tiên:**
1. **Bảng thành phần thực phẩm Việt Nam** (Viện Dinh dưỡng, 2017).
   - Bảng chỉ có thực phẩm sống, phù hợp với cách cộng từ nguyên liệu sống.
   - Cần kiểm tra điều khoản sử dụng trước khi thương mại hoá.
2. **USDA FoodData Central**: CC0, ghi nguồn; API mặc định 1.000 request/giờ.
3. **ASEAN Food Composition Database.**

Mỗi nguyên liệu lưu `nutritionSource` (db, mã gốc, tên khớp, người xác nhận).

---

## 7. Dữ liệu

### 7.1 Nguyên liệu: `data/ingredients/<id>.json`

```json
{
  "id": "thit-ba-chi",
  "names": { "default": "Thịt ba chỉ", "north": "Thịt ba chỉ", "south": "Thịt ba rọi" },
  "aliases": ["ba rọi", "thịt ba rọi"],
  "category": "thit-heo",
  "aisle": "thit-ca",
  "proteinSource": "heo",
  "marketUnit": {
    "north": { "display": "lạng", "gramsPerUnit": 100, "step": 0.5 },
    "south": { "display": "g", "gramsPerUnit": 1, "step": 50 }
  },
  "wasteRatio": 0.0,
  "isPantry": false,
  "nutritionPer100gEdible": { "kcal": null, "proteinG": null, "fatG": null, "carbG": null, "fiberG": null, "sodiumMg": null },
  "nutritionSource": { "db": "placeholder", "ref": null, "matchedName": null, "verifiedBy": null, "verifiedAt": null },
  "image": { "thumb": null, "full": null, "source": null, "license": null, "isIllustration": false }
}
```

`names` và `marketUnit` là đối tượng khoá theo vùng (`north`, `central`, `south`). Có thể thêm khoá `default` cho mọi vùng không có khoá riêng. Nếu thiếu cả hai, `names` rơi về `id`, còn `marketUnit` coi là không có đơn vị chợ (lỗi dữ liệu với nguyên liệu dùng `integer` hoặc xuất hiện trong danh sách đi chợ).

### 7.2 Món: `data/dishes/<id>.json`

```json
{
  "id": "canh-chua-ca-loc",
  "name": "Canh chua cá lóc",
  "slot": "canh",
  "regions": ["south"],
  "seasons": ["all"],
  "spicy": "optional",
  "cookingMethod": "nau",
  "proteinSource": "ca",
  "serveHot": false,
  "ingredients": [
    { "ingredientId": "ca-loc", "gramsEdiblePerServing": 80, "scale": "linear", "consumedRatio": 1, "note": "cắt khúc" }
  ],
  "steps": [
    { "order": 1, "text": "Làm sạch cá, cắt khúc, ướp chút muối.", "activeMinutes": 10, "passiveMinutes": 0, "ingredientIds": ["ca-loc"] }
  ],
  "status": "draft",
  "provenance": { "draftedBy": "llm|human", "reviewedBy": null, "reviewedAt": null, "cookTestedAt": null, "measuredTotalMinutes": null },
  "image": { "thumb": null, "full": null, "source": null, "license": null, "isIllustration": false }
}
```

**Giá trị cho phép:**
- `slot`: `canh` | `man` | `rau`
- `spicy`: `none` | `optional` | `required`
- `cookingMethod`: `luoc` | `hap` | `nau` | `kho` | `xao` | `chien` | `nuong` | `tron`

**Vòng đời món:** `draft` → `validated` (máy kiểm) → `reviewed` (người duyệt) → `cooktested` (đã nấu thử). Bản release chỉ gồm món từ `reviewed` trở lên.

### 7.3 Luật của `DataBuilder validate`

**Lỗi (fail):**
- Schema sai.
- `id` không phải kebab-case ASCII, hoặc bị trùng.
- `ingredientId` không tồn tại.
- Có bước mà `activeMinutes + passiveMinutes = 0`.
- Ảnh khai báo nhưng thiếu file, `source` hoặc `license`.
- Chỉ ở chế độ `--release`: còn `placeholder`, hoặc còn món dưới `reviewed`.

**Cảnh báo** (thành lỗi ở `--release`):
- kcal mỗi suất ngoài khoảng:
  - canh: 30–250;
  - món mặn: 120–550;
  - rau: 20–220.
- Món mặn có tổng gram nguồn đạm mỗi suất ngoài khoảng 50–150g.
- Natri vượt 1.000mg mỗi suất.
- Nguyên liệu không xuất hiện ở bước nào.

**Output:**
- `dist/data-bundle.json`, có `version` = hash nội dung.
- `dist/data-report.md`: số lượng, cảnh báo, độ phủ ảnh, độ phủ dinh dưỡng.

### 7.4 Pipeline nội dung

1. **[NGƯỜI]** Chốt `data/plan/dish-list.csv` (`name, slot, regions`): khoảng 35 canh, 55 món mặn, 30 rau.
2. Sinh khung nguyên liệu (tên, alias, đơn vị, aisle, không có dinh dưỡng). **[NGƯỜI]** kiểm tra đơn vị và tỷ lệ thải bỏ.
3. **FdcImporter:** mỗi nguyên liệu gợi ý 3 ứng viên kèm lý do. **[NGƯỜI]** chọn trong Studio.
4. **RecipeDrafter** (Claude API):
   - Bắt buộc trả JSON đúng schema.
   - Chỉ dùng ID nguyên liệu đã có; nguyên liệu lạ đưa vào `missingIngredients`.
   - Không sinh số dinh dưỡng.
   - Ghi vào `data/dishes/_drafts/`.
5. **DataBuilder validate** chạy, sửa đến khi sạch.
6. **[NGƯỜI]** Mẹ hoặc bà duyệt trong Studio (khoảng 4–5 phút mỗi món). Studio hiện số dinh dưỡng do code tính và các cảnh báo.
7. **[NGƯỜI]** Nấu thử top 30 món trong bữa tối hằng ngày. Một lần nấu cho ra cùng lúc: công thức đã kiểm định, thời gian thật, ảnh món, cân thành phẩm.

### 7.5 Ảnh

- **Nguyên liệu:** tự chụp. Nền kem đồng nhất, tỷ lệ 1:1, cùng góc, cùng ánh sáng. Ở đây độ đúng quan trọng hơn độ đẹp, vì ảnh dùng để nhận diện ngoài chợ.
- **Món:** chụp trong buổi nấu thử, từ trên xuống, tỷ lệ 4:3, chén đĩa đồng bộ.
- **Ảnh tạm:** AI tạo hoặc ảnh kho có giấy phép, bắt buộc `isIllustration: true`. UI gắn nhãn "Ảnh minh hoạ".
- **Ảnh kho (Unsplash, Pexels):** chỉ lấy lẻ từng ảnh, kèm source và license. Giấy phép của họ cấm gom ảnh hàng loạt và cấm dùng API để xây dataset.
- **ImageTool:** chuyển `raw/` → WebP thumb 200px (đóng gói trong app, dùng offline) và full 800px (lazy-load), xoá EXIF.

### 7.6 Đóng gói và cập nhật

`data-bundle.json` được cache bằng service worker. Khi online, app kiểm tra `version` mới và tải về. Cập nhật dữ liệu không cần phát hành lại app.

---

## 8. UX và UI

### 8.1 Màn hình

1. **Onboarding — "Nhà mình có ai?"**
   - Chạm chip vai trò (Bố, Mẹ, Ông, Bà, Con trai, Con gái, Bé) để thêm thẻ thành viên. Mỗi thẻ có nút tăng giảm "mấy chén cơm".
   - Không hỏi dồn: câu về kiêng khem và khẩu vị hỏi sau lần dùng thứ 3. Cho phép bỏ qua và dùng mặc định.
2. **Mâm hôm nay.**
   - 3–4 thẻ món: icon theo loại, tên, thời gian, kcal/suất, nút khoá 48px. Vuốt ngang để đổi riêng món đó.
   - Nút "Quay mâm" ở vùng ngón cái, hiệu ứng khoảng 1 giây, rung nhẹ nếu thiết bị hỗ trợ.
   - Tóm tắt: thời gian cả mâm, kcal/suất (gồm cơm), số món cần mua.
   - Nút "Mâm trước" (lưu 5 mâm). Quay quá 5 lần chưa chốt thì gợi ý lọc nhanh: dưới 30 phút / không thịt đỏ / ít dầu mỡ.
   - Nút "Chốt mâm" mở đi chợ và chế độ nấu.
3. **Chi tiết món.** Ảnh và 3 phần xếp dọc: Nguyên liệu ("cho 3,8 suất"), Cách làm, Dinh dưỡng (thanh đạm/béo/bột đường và kcal theo từng người).
4. **Đi chợ.** Nhóm theo quầy, thumbnail nguyên liệu, checkbox 48px, tương phản cao, chạy offline, nút "Gửi Zalo".
5. **Chế độ nấu.**
   - Mỗi màn hình một bước, chữ ≥ 22px, hẹn giờ ngay trong bước, các hẹn giờ song song hiện thành chip.
   - Giữ màn hình sáng bằng Screen Wake Lock (feature-detect, có thông báo nếu không hỗ trợ).
   - Chạm nửa phải màn hình để sang bước tiếp.
6. **Sau bữa.** "Cả nhà thích không?" với 3 lựa chọn: thích / bình thường / không. Kết quả cập nhật `pref`.
7. **Cài đặt.** Thành viên, vùng miền, cỡ chén, món đã chặn, xoá dữ liệu.

### 8.2 Ngôn ngữ thị giác

- **Màu:** nền kem ấm, màu nhấn đỏ gạch, màu phụ xanh lá chuối. Màu theo ô: canh xanh dương, mặn cam, rau xanh lá. **Dark mode bắt buộc.**
- **Chữ:** font Be Vietnam Pro. Line-height ≥ 1.5 vì tiếng Việt có dấu chồng. Chữ cơ bản 16px, chế độ nấu từ 22px. Tôn trọng cỡ chữ hệ thống.
- **Vùng chạm** ≥ 48px.
- **Gamification nhẹ:** không streak, không thông báo gây áp lực.
- **Lời văn:** tiếng Việt ngắn, thân mật, động từ đứng đầu ("Quay mâm", "Chốt mâm", "Gửi Zalo").

---

## 9. Kiến trúc kỹ thuật

```
data/*.json ──► DataBuilder ──► dist/data-bundle.json ──┐
                                                        ▼
MamCom.Web (Blazor WASM PWA, service worker)  ──►  MamCom.UI (RCL)
                                                        │
                       MamCom.Data (BundleLoader, IUserStateStore=localStorage)
                                                        │
MamCom.Core: PortionCalculator · MealSpinner · IngredientScaler ·
             ShoppingListBuilder · NutritionCalculator · CookPlanScheduler
```

**Quyết định chính** (mỗi mục có một ADR):

| ADR | Quyết định | Lý do |
|---|---|---|
| 001 | Dữ liệu dạng JSON bundle, không dùng SQLite ở v1 | Dữ liệu nhỏ (dưới 2MB). SQLite trong WebAssembly phức tạp. Thay thế cho đề xuất SQLite ban đầu. |
| 002 | Làm Blazor WASM PWA trước, MAUI Blazor Hybrid sau | Chia sẻ bằng link qua Zalo, không cần lên store. Tái dùng RCL cho MAUI. Đổi lại: lần tải đầu nặng hơn app JS, nên phải bật trimming, nén và cache, và đo thời gian tải đầu trên 4G. |
| 003 | Không có backend ở v1 | Mọi tính toán chạy trên máy. Beta dùng event log cục bộ có nút xuất. |

Zalo Mini App là phương án phân phối **sau v1**. Trước khi chọn cần kiểm chứng Mini App có hỗ trợ Wake Lock và chạy offline hay không.

## 10. Kiểm thử

| Khu vực | Loại test | Ca chính |
|---|---|---|
| PortionCalculator | Unit | Gia đình mẫu ở §4.2 = 3,79 ± 0,01 suất; gạo 320g; tag MoreProtein/MoreVeg |
| IngredientScaler | Unit | 4 kiểu co giãn; làm tròn đơn vị đếm |
| ShoppingListBuilder | Unit, golden file | Gộp trùng, tỷ lệ thải bỏ, đơn vị Bắc/Nam, ẩn pantry, text chia sẻ |
| NutritionCalculator | Unit | Fixture tính tay (số liệu fixture chỉ dùng trong test, ghi rõ) |
| MealSpinner | Property, 10.000 seed | Không bao giờ vi phạm ràng buộc cứng; khoá giữ nguyên; tái lập theo seed; luật nới đúng thứ tự |
| CookPlanScheduler | Unit, property | Cơm bắt đầu đầu tiên; không chồng bước active; serveHot kết thúc trong 10 phút cuối; mealTime ≥ món dài nhất |
| DataBuilder | Unit, fixture | Fixture đúng/sai cho từng luật; chế độ release |
| UI components | bUnit | Thẻ món, stepper chén, checkbox đi chợ |
| Luồng chính | Playwright E2E, 360×740 | Onboarding → quay → khoá → quay → chốt → đi chợ → chế độ nấu |
| Accessibility | Kiểm tay + E2E | Contrast, vùng chạm, nhãn cho icon-only button |

Mục tiêu coverage: `MamCom.Core` ≥ 85% line.

## 11. Chỉ số và beta

**Chỉ số:**
- Thời gian từ mở app đến chốt mâm: dưới 60 giây.
- Số lần quay trước khi chốt: trung bình trên 5 là thuật toán chưa hợp.
- Tỷ lệ mâm đã chốt có mở tiếp đi chợ hoặc chế độ nấu.
- Tỷ lệ giữ chân ngày 7 và ngày 30.

**Beta:** 5 người nấu chính thật. Event log cục bộ, ẩn danh, không thu thập thông tin cá nhân, có nút "Xuất phản hồi".

## 12. Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| Dữ liệu là nút thắt | Pipeline LLM + validator + người duyệt; nấu thử ngay trong bữa tối hằng ngày |
| Dinh dưỡng thiếu chính xác | Hiển thị "≈", ghi nguồn, không phán xét mang tính y tế |
| Bản quyền | Công thức tự soạn; ảnh tự chụp; ghi nguồn dữ liệu dinh dưỡng |
| Blazor WASM tải nặng | Trimming, Brotli, cache PWA; đo thời gian tải trên 4G |
| iOS PWA hạn chế (wake lock, share) | Feature-detect và có phương án dự phòng |
| Người dùng bỏ app sớm | Onboarding ngắn, học khẩu vị từ phản hồi sau bữa |

## 13. Câu hỏi mở `[QUYẾT ĐỊNH]`

- Tên app chính thức. Mặc định: tên tạm "MâmCơm", namespace `MamCom`.
- Hosting: Azure Static Web Apps *(mặc định)* hay GitHub Pages.
- Có thương mại hoá không. Ảnh hưởng tới điều khoản dùng dữ liệu của Viện Dinh dưỡng.
- Vùng miền ưu tiên khi ra mắt.

## Phụ lục — Nguồn tham khảo đã dùng khi phân tích

- Hướng dẫn chế độ ăn VN 2016–2020, định nghĩa đơn vị ăn: https://static.cambridge.org/content/id/urn:cambridge.org:id:article:S2048679022000441/resource/name/S2048679022000441sup001.pdf
- Khuyến cáo dinh dưỡng cho người lao động (Cục ATTP, 2021), tỷ lệ đạm/béo/bột đường: https://bk.laodongcongdoan.vn/apiservice@/fileclick&i=222460
- Bảng thành phần thực phẩm VN 2017 chỉ có thực phẩm sống (INDDEX24): tài liệu bổ sung của bài INDDEX24 Viet Nam, British Journal of Nutrition, mã bài S0007114522001507
- USDA FoodData Central API (CC0): https://fdc.nal.usda.gov/api-guide.html
- Unsplash License: https://help.unsplash.com/articles/2612332-what-do-you-mean-by-compiling-photos-to-replicate-a-similar-or-competing-service
- Điều khoản Pexels API: https://help.pexels.com/hc/vi-vn/articles/900005880463
- Tỷ lệ bỏ app sức khoẻ (JMIR 2024): https://www.jmir.org/2024//e56897
- Mô hình quay lại và khoá món (Eat This Much): https://www.eatthismuch.com/faq
- Chế độ nấu từng bước, giữ màn hình sáng: https://vp0.com/blogs/recipe-cooking-app-step-by-step-ui-ios
- Số liệu Zalo 2026: https://www.vietnamplus.vn/zalo-co-813-trieu-nguoi-dung-hang-thang-post1127226.amp
