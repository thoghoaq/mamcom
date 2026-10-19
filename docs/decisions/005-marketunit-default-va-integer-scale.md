# ADR-005: marketUnit có khoá `default`; nguyên liệu `integer` dùng gramsPerUnit của vùng

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án xác nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §6.1, §7.1](../SPEC.md), [PLAN 1.3, 1.5, 2.2](../PLAN.md)

## Bối cảnh

- SPEC §7.1 cho `names` một khoá `default` nhưng `marketUnit` chỉ có ví dụ `north` và `south`. SPEC §4.1 có ba vùng, trong đó `central` không có khoá nào trong ví dụ. Chưa quy định vùng Trung lấy đơn vị chợ ở đâu.
- SPEC §6.1 viết `integer` là `ceil(g × S / gramsPerUnit)` nhưng không nói `gramsPerUnit` của nguyên liệu nào, và lượng dùng thực tế khi làm tròn lên là bao nhiêu.
- `IngredientScaler` (PLAN 1.3) cần cả hai điều này. `ShoppingListBuilder` (PLAN 1.5) sẽ dùng chung cách tra đơn vị chợ.

## Quyết định

- `marketUnit` nhận thêm khoá tuỳ chọn `default`, giống `names`. `Ingredient.MarketUnitFor(region)` trả đơn vị của vùng, nếu không có thì `default`, nếu không có nữa thì `null`.
- Với `integer`, `gramsPerUnit` lấy từ `MarketUnitFor(region)`. Số đơn vị là `ceil(g × S / gramsPerUnit)` và lượng dùng thực tế là số đơn vị nhân `gramsPerUnit`.
- `IngredientScaler` báo `InvalidOperationException` khi nguyên liệu `integer` không có đơn vị chợ cho vùng, hoặc `gramsPerUnit` không dương. Đó là lỗi dữ liệu.
- Số mũ của `sublinear` (0,8) chuyển vào `data/config/portion-defaults.json` (`sublinearExponent`) vì CLAUDE.md đặt hằng số nghiệp vụ trong `data/config`.

## Phương án đã cân nhắc

1. **Bắt mọi nguyên liệu khai đủ ba vùng.** Gọn về mặt logic nhưng buộc lặp dữ liệu cho mọi nguyên liệu có cùng đơn vị ở nhiều vùng. Bị loại.
2. **Vùng Trung dùng đơn vị của vùng Bắc hoặc Nam.** Phải chọn một bên, và chọn sai làm lệch tên đơn vị (lạng hay g). Bị loại.
3. **Khoá `default` (chọn).** Cùng cách với `names`, dữ liệu viết một lần cho nguyên liệu không khác theo vùng.
4. **Lượng dùng thực tế của `integer` là `g × S`, chỉ làm tròn số đơn vị để hiển thị.** Danh sách đi chợ và dinh dưỡng sẽ lệch so với số quả người nấu thực sự bỏ vào nồi. Bị loại.

## Hệ quả

**Được**
- Vùng Trung có đơn vị chợ mà không phải lặp dữ liệu.
- Số trứng hay số quả cà chua trong công thức, danh sách đi chợ và dinh dưỡng là cùng một con số.

**Đổi lại**
- Trong công thức, lượng thực tế của nguyên liệu `integer` thường lớn hơn `g × S`, tối đa thêm gần một đơn vị.
- `DataBuilder validate` (PLAN 2.2) cần kiểm: mỗi nguyên liệu dùng ở món phải có đơn vị chợ cho cả ba vùng (khoá riêng hoặc `default`). SPEC §7.3 chưa có luật này.
- `"default"` là khoá dành riêng, nên vùng mới (nếu có) không được đặt tên `default`.

## Khi nào xem lại

- Dữ liệu thật cho thấy `gramsPerUnit` của nguyên liệu đếm được khác nhau theo vùng đến mức một khoá `default` không đủ.
- Có nguyên liệu vừa đếm theo quả vừa mua theo cân trong cùng một vùng.
