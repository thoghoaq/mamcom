# ADR-010: JSON Schema cho dữ liệu: thư viện, phạm vi, và phần để lại cho `validate`

- Trạng thái: Đã áp dụng trong code, chờ chủ dự án xác nhận
- Ngày: 2026-10-19
- Liên quan: [SPEC §7.1–7.3](../SPEC.md), [PLAN 2.1, 2.2](../PLAN.md), [ADR-001](001-json-bundle-thay-sqlite.md)

## Bối cảnh

SPEC §7.3 xếp "schema sai" vào lỗi của `DataBuilder validate` nhưng không nói schema kiểm đến đâu, và PLAN 2.1 chỉ yêu cầu schema cho nguyên liệu, món và config. Cần chọn thư viện kiểm schema cho .NET (task 2.2 dùng lại) và vạch ranh giới giữa việc schema làm được và việc `validate` phải làm.

## Quyết định

- **Thư viện: `LateApexEarlySpeed.Json.Schema` 4.2.2** (BSD-3-Clause, dựa trên System.Text.Json, hỗ trợ draft 2020-12, đã qua bộ test chính thức của JSON Schema).
  - Không dùng `JsonSchema.Net` bản 9.x: giấy phép là OSMF EULA chứ không phải giấy phép mã nguồn mở thường. PLAN 6.3 còn để ngỏ chuyện thương mại hoá và CLAUDE.md đã từng loại ImageSharp vì giấy phép.
  - Không dùng `NJsonSchema`: chỉ hỗ trợ đến draft 7 và kéo theo Newtonsoft.Json.
  - Thư viện dựa vào reflection của System.Text.Json, nên chỉ dùng trong công cụ (`MamCom.DataBuilder`), không đưa vào bản WebAssembly.
- **Một schema cho mỗi loại file, mỗi schema tự đứng được** (không `$ref` chéo file, nên không cần bộ đăng ký tài nguyên). Định nghĩa dùng chung (`id`, `image`) được chép lại ở nơi cần; test so hai bản bằng chữ.
- **Tên schema theo tên file:** `ingredient`, `dish` và `<tên file config>` (`portion-defaults`, `spinner-defaults`, `nutrition-defaults`, `cooking-defaults`). `SchemaSet` (trong `MamCom.DataBuilder`) nạp cả thư mục và kiểm một chuỗi JSON theo tên.
- **Schema chặt hơn việc đọc dữ liệu ở Core**, ở những chỗ SPEC và CLAUDE.md đã nói mà code đọc không bắt được:
  - mọi đối tượng cấm trường lạ (Core âm thầm bỏ qua trường gõ sai tên); trường viết hai lần là lỗi (`JsonDocument` mặc định lấy giá trị cuối);
  - nguồn `placeholder` thì mọi chất dinh dưỡng và tham chiếu nguồn phải `null` (quy tắc "không bịa số dinh dưỡng"); nguồn thật phải có `ref` và `matchedName`; `verifiedBy` và `verifiedAt` đi cặp;
  - vòng đời món: `draft` và `validated` không có dấu duyệt hay nấu thử, `reviewed` cần người và ngày, `cooktested` cần thêm ngày nấu thử, để không công cụ nào "lỡ" đặt trạng thái;
  - `names` và `marketUnit` phải có khoá `default` hoặc đủ ba vùng (SPEC cho thiếu thì rơi về `id` và gọi là lỗi dữ liệu ở chỗ dùng; schema bắt ngay ở file nguyên liệu);
  - ảnh khai báo phải có `source` và `license`; `consumedRatio` nhỏ hơn 1 phải có `note`; bước phải có ít nhất 1 phút; `com` là id dành riêng; `all` đứng một mình trong `seasons`; bảng tháng sang mùa không dùng `all`.
- **Khoảng giá trị chống gõ nhầm** là số do tôi chọn, không phải khuyến nghị dinh dưỡng: gram mỗi suất không quá 1.000, phút cần tay không quá 240, phút thụ động không quá 1.440, kcal trên 100g không quá 900 (chất béo nguyên chất), natri không quá 40.000 mg trên 100g (muối), hệ số nhân của config trong khoảng vài lần. Nới khi gặp ngoại lệ thật.
- **Mã cơ sở dữ liệu dinh dưỡng** cho phép: `placeholder`, `vn-fct-2017`, `usda-fdc`, `asean-fcd` (theo §6.3). Tên do tôi đặt; thêm khi có nguồn mới.
- **Việc schema không diễn đạt được thì để `validate` (PLAN 2.2) hoặc Core:** id trùng giữa các file, `ingredientId` và mã mùa không tồn tại, `order` liền nhau, nguyên liệu lặp trong một món, đơn vị chợ cho nguyên liệu `integer`, file ảnh có thật, các cảnh báo kcal/đạm/natri, `showApproxGrams` trên đơn vị cân, nguyên liệu không xuất hiện ở bước nào (trừ gạo), và quan hệ giữa các giá trị config (đã có `Validate` ở Core).
- **Báo cáo lỗi đã lọc.** Thư viện liệt kê mọi nút đã thất bại, kể cả bên trong `if`, `not` và các nhánh `anyOf` đã đi đúng hướng, nên một `names` hợp lệ vẫn bị "báo thiếu north". `SchemaSet` chỉ giữ nút làm file sai. Một `anyOf` hoặc `oneOf` thất bại cả nhóm là một lỗi kèm lời của từng nhánh. Nếu bộ lọc loại hết thì hiện danh sách thô.
- **Chống lệch hai nguồn.** Schema và kiểu C# mô tả cùng các file, nên test so enum, danh sách trường và trường bắt buộc của từng record với schema tương ứng.

## Phương án đã cân nhắc

1. **Chỉ kiểm bằng Core** (đọc rồi `Validate`), không có schema. Bị loại: PLAN và CLAUDE.md yêu cầu schema, lỗi không có vị trí, và Studio sau này cần schema để dựng form.
2. **Sinh schema từ kiểu C#.** Thư viện hỗ trợ, nhưng schema sinh ra chỉ nói được điều kiểu nói, không diễn đạt được "placeholder thì không có số" hay vòng đời món. Bị loại.
3. **Một schema chung có `$ref` chéo file.** Gọn hơn nhưng cần bộ đăng ký tài nguyên và làm schema khó đọc riêng lẻ. Bị loại.
4. **Giữ nhiễu của thư viện trong báo cáo.** Đơn giản nhưng làm người sửa dữ liệu đuổi theo lỗi không có thật. Bị loại.

## Hệ quả

**Được**
- Gõ sai tên trường, thiếu đơn vị chợ, số dinh dưỡng không nguồn, món đã "duyệt" mà không có người duyệt đều bị bắt ngay ở một file, kèm vị trí.
- Studio (PLAN 4.x) và DataBuilder (PLAN 2.2) dùng chung một bộ luật.

**Đổi lại**
- Thư viện kéo theo `Microsoft.Extensions.Http` 3.1.0 (dùng cho `$ref` từ xa, ta không dùng). Cần xem lại nếu cảnh báo bảo mật của NuGet nhắc đến nó.
- Các luật còn lại phải làm ở 2.2; schema một mình chưa đủ cho "validate".
- Bộ lọc nhiễu dựa vào đường dẫn từ khoá mà thư viện trả về; đổi thư viện hoặc nâng bản lớn phải chạy lại các test báo cáo.

## Khi nào xem lại

- Studio cần schema có `$ref` để dựng form.
- Cảnh báo bảo mật của NuGet với thư viện hoặc phụ thuộc của nó.
- Có nguồn dinh dưỡng mới ngoài bốn mã trên.
