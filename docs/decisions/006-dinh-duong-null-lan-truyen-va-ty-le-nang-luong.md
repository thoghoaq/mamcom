# ADR-006: Dinh dưỡng: null lan truyền, tỷ lệ năng lượng theo ba chất, cơm tính từ gạo sống

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án xác nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §6.3](../SPEC.md), [PLAN 1.4, 1.5, 2.2](../PLAN.md), CLAUDE.md (quy tắc dữ liệu)

## Bối cảnh

- CLAUDE.md cấm tự bịa số dinh dưỡng; nguyên liệu chưa có nguồn để `null` và `source: "placeholder"` (dev). `NutritionCalculator` (PLAN 1.4) phải cộng các giá trị này mà không biến chỗ thiếu thành số.
- SPEC §6.3 viết "% năng lượng tính với hệ số 4/9/4" nhưng không nói mẫu số là gì, và chỉ nói cơm "tính theo `bowls_i × gramsPerBowl`" mà không nói lấy dinh dưỡng của nguyên liệu nào.
- SPEC §6.3 tính phần ăn của từng người bằng `servings_i`. Tag `MoreProtein` và `MoreVeg` ở §4.2 chỉ nhắc tới lượng nấu.

## Quyết định

- **Null lan truyền theo từng chất.** Cộng hai giá trị mà một bên `null` thì kết quả `null`. Các chất khác vẫn cộng. Dòng công thức có 0 gam hoặc `consumedRatio` bằng 0 không đóng góp và không bị coi là thiếu. Mỗi `DishNutrition` kèm danh sách nguyên liệu thiếu dữ liệu để Studio và giao diện chỉ ra.
- **Tỷ lệ năng lượng** là phần năng lượng mỗi chất đạm, béo, bột đường sinh ra (4, 9, 4 kcal mỗi gram) trên tổng của ba chất. Cộng lại đúng 100%. Không có tỷ lệ khi thiếu một chất hoặc tổng bằng 0.
- **Cơm tính từ gạo sống.** Dinh dưỡng cơm lấy từ nguyên liệu có id `riceIngredientId` trong `portion-defaults.json` (mặc định `gao`), nhân với `bowls_i × gramsPerBowl` của từng người. Danh sách đi chợ (PLAN 1.5) dùng cùng id.
- **Phần ăn mỗi người** dùng `servings_i` cơ bản cho mọi món, đúng chữ SPEC §6.3. Hai tag chỉ ảnh hưởng lượng nấu.
- Hệ số 4/9/4 và khoảng khuyến nghị nằm trong file mới `data/config/nutrition-defaults.json`, tách khỏi `portion-defaults.json`. Cây thư mục trong CLAUDE.md đã được cập nhật.

## Phương án đã cân nhắc

1. **Coi `null` là 0.** Tổng luôn có số nhưng thấp hơn thực tế mà người dùng không biết. Vi phạm quy tắc "không tự bịa số". Bị loại.
2. **Bỏ cả kết quả nếu có bất kỳ chất nào thiếu.** An toàn nhưng mất cả những chất đã biết, trong khi dữ liệu dev gần như toàn placeholder. Bị loại.
3. **Tỷ lệ theo cột `Kcal` của nguyên liệu.** Cách quy đổi kcal của mỗi bảng thành phần có thể khác 4/9/4 (chất xơ, cồn, hệ số riêng từng nhóm thực phẩm), nên ba tỷ lệ có thể không cộng thành 100% và còn phụ thuộc nguồn dữ liệu. Bị loại.
4. **Nhân phần ăn của người có tag `MoreProtein` thêm hệ số khi tính dinh dưỡng.** Sát thực tế hơn cho người ăn nhiều thịt nhưng trái chữ SPEC §6.3 và làm tổng cả mâm khác tổng lượng nấu. Để dành nếu beta cho thấy cần.
5. **Dinh dưỡng cơm từ cơm chín.** SPEC §6.3 nói bảng thành phần Việt Nam chỉ có thực phẩm sống, nên gạo sống khớp nguồn dữ liệu. Chọn gạo sống.

## Hệ quả

**Được**
- Không bao giờ hiện số thấp giả do thiếu dữ liệu; vẫn có số cho những chất đã biết.
- Ba tỷ lệ luôn cộng thành 100%, vẽ thanh tỷ lệ so với khuyến nghị được ngay.

**Đổi lại**
- Với dữ liệu placeholder, hầu hết tổng và tỷ lệ là `null`. Giao diện (PLAN 3.5) phải có trạng thái "chưa đủ số liệu".
- Người có tag `MoreProtein` được tính ít đạm hơn họ thực ăn (xem phương án 4).
- Seed dữ liệu (PLAN 1.8) và bộ nguyên liệu thật (4.2) phải có nguyên liệu `gao`, nếu không `ForMeal` báo lỗi dữ liệu.

## Khi nào xem lại

- Beta cho thấy người dùng nhận ra kcal cho người ăn nhiều đạm lệch rõ.
- Nguồn dinh dưỡng chọn ở PLAN 4.3 chỉ có số cho cơm chín.
