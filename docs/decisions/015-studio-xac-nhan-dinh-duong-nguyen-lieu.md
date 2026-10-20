# ADR-015: Studio xác nhận dinh dưỡng của nguyên liệu: chỉ chép số của ứng viên FDC, kèm tên người

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-20
- Liên quan: [SPEC §6.3, §7.1, §7.4](../SPEC.md), [PLAN 2.6](../PLAN.md), [ADR-012](012-fdc-importer-goi-y-khong-tu-ap-dung.md), [ADR-014](014-studio-duyet-mon-sua-thang-file-json.md), CLAUDE.md (quy tắc dữ liệu)

## Bối cảnh

PLAN 2.6: Studio có phần nguyên liệu để sửa tên, đơn vị, tỷ lệ thải bỏ, chọn ứng viên dinh dưỡng từ 2.3 (ghi `nutritionSource` và `verifiedBy`) và gắn ảnh; xong khi xác nhận được dinh dưỡng cho một nguyên liệu và validator nhận. CLAUDE.md cấm bịa số dinh dưỡng: số chỉ đến từ nguồn có tham chiếu. Còn ngỏ:

- Số vào file nguyên liệu từ đâu, và ai chịu trách nhiệm về chúng.
- Chọn nhầm thì gỡ thế nào.
- Việc sửa các trường khác có được đụng vào số không.
- "Gắn ảnh" khi ImageTool (2.8) chưa có.

## Quyết định

- **Số chỉ đến từ ứng viên nằm sẵn trong file gợi ý của FdcImporter.** Giao diện chỉ gửi mã FDC của ứng viên và tên người; `ChooseNutrition` đọc lại file gợi ý trên đĩa, tìm ứng viên theo mã và chép sáu số nguyên văn (một số FDC không nêu vẫn là `null`). Không có ô nào để gõ số. File gợi ý phải nói đúng về nguyên liệu (`ingredientId` khớp tên file), không thì coi như không có.
- **Ghi cùng lúc** `nutritionSource`: `db` = `usda-fdc`, `ref` = mã FDC, `matchedName` = mô tả FDC, `verifiedBy` = tên người (cắt khoảng trắng), `verifiedAt` = ngày hôm nay lấy từ `IClock`. Thiếu tên thì từ chối. Ứng viên không nêu số nào (cả sáu `null`) bị từ chối, vì sẽ biến một mục rỗng thành "đã xác nhận". Ứng viên thiếu một vài chất vẫn chọn được: chất đó vẫn chưa biết, và món dùng nguyên liệu đó vẫn được báo thiếu số.
- **Lưu một chỉnh sửa không đụng vào số hay nguồn**: chúng lấy từ file đang có. Studio sửa tên theo vùng, tên gọi khác, nhóm, quầy đi chợ, nguồn đạm, tỷ lệ thải bỏ, đồ có sẵn, đơn vị đi chợ theo vùng và ảnh. Chỉ lưu khi máy kiểm không còn lỗi, như với món (ADR-014).
- **Gỡ nhầm:** "Bỏ số liệu" đưa nguyên liệu về placeholder, và file về đúng từng byte như trước khi chọn (có test). Chọn lại thì thay lựa chọn cũ, kèm người và ngày mới.
- **Chọn hay bỏ số liệu chỉ khi** không còn thay đổi chưa lưu và không còn lỗi, để số không bị ghi cạnh một bản người chưa thấy. Chống ghi đè bằng `Version` (băm nội dung) như với món, và ghi qua file tạm.
- **Máy kiểm của một nguyên liệu** là `IngredientCheck` trong DataBuilder: schema `ingredient`, rồi các luật có nghĩa với một nguyên liệu (tên file bằng id, file ảnh có thật và không chứa đường dẫn, đơn vị cân không xin "~Ng", nguyên liệu không món nào dùng). Chỉ lấy phát hiện về chính nguyên liệu đó, và bỏ những điều loader đã thấy sai ở chính file đó trên đĩa, để sửa được một nguyên liệu đang hỏng.
- **Một định dạng file** `IngredientFile`. Test: cả 33 nguyên liệu seed đọc rồi ghi lại ra đúng từng byte. Một vài file có khoá `north` trước `default`; form giữ thứ tự khoá của file, khoá mới thêm đứng sau theo thứ tự default, north, central, south.
- **Số gốc của ứng viên hiện nguyên văn**, không làm tròn và không "≈", vì người xác nhận cần thấy đúng số sẽ được ghi. Số đang lưu của nguyên liệu hiện theo quy ước chung (kcal kèm "≈", làm tròn 10).
- **Gắn ảnh:** chọn ảnh nhỏ và ảnh lớn trong các file đã có ở `data/images/out`, kèm nguồn, giấy phép và cờ ảnh minh hoạ (schema bắt nguồn và giấy phép đi cùng ảnh). Xem trước qua `/data-images/<tên>`, chỉ phục vụ một tên file phẳng của ảnh có đuôi đã biết nằm trong thư mục ảnh. Studio không tạo hay đổi cỡ ảnh: việc đó là của ImageTool (2.8), nên chọn ảnh chỉ có nghĩa khi đã có file ở đó.
- **Màn hình:** Nguyên liệu (lọc theo chưa có số liệu, có gợi ý chờ chọn, đã xác nhận); một nguyên liệu (form, dinh dưỡng và nguồn, ứng viên, máy kiểm, món dùng nó); tổng quan có thêm số nguyên liệu theo trạng thái số liệu.

## Phương án đã cân nhắc

1. **Cho gõ hoặc dán số, ví dụ từ Bảng thành phần thực phẩm Việt Nam.** Tiện, nhưng là chỗ số không nguồn lọt vào, và số của Bảng VN cần mã hoặc trang để kiểm. Hoãn: hiện sửa file bằng tay, schema và validator vẫn kiểm; thêm form nhập tay (bắt buộc cơ sở dữ liệu, mã hoặc trang, tên khớp) khi 4.3 cần.
2. **Tự chọn ứng viên xếp hạng 1.** Phạm vào nguyên tắc "chỉ người xác nhận" của ADR-012.
3. **Cho Studio tải ảnh lên rồi tự xử lý.** Trùng việc của ImageTool (2.8) và kéo SkiaSharp vào Studio.
4. **Lưu số cùng lúc với các sửa khác.** Số đổi lẫn với phần khác làm diff khó đọc, và khó nói "người đã thấy bản nào".
5. **Lấy tên người xác nhận từ cấu hình.** Nhiều người dùng chung một máy; hỏi tên và nhớ giữa các món, như khi duyệt món.

## Hệ quả

**Được**
- Một thao tác cho một nguyên liệu có số kèm nguồn, tên người và ngày; diff Git đúng 11 dòng.
- Qua Studio, không có đường nào cho số không nguồn vào file.

**Đổi lại**
- Chưa nhập tay số từ Bảng thành phần VN trong Studio; 4.3 ("ưu tiên số từ Bảng VN nếu có") vẫn phải sửa file bằng tay.
- Chọn ảnh chưa dùng được cho tới khi ImageTool tạo ra file ảnh.
- Ứng viên của FDC chưa phân biệt sống, chín hay phần ăn được: người xác nhận phải đọc mô tả và lý do xếp hạng.
- Ứng viên thiếu chất vẫn chọn được, nên món dùng nguyên liệu đó báo thiếu số cho tới khi có số đủ.

## Khi nào xem lại

- 4.3 cần nhập số từ Bảng VN: thêm form nhập tay.
- ImageTool xong: cho tải ảnh gốc vào `data/images/raw` từ Studio.
- Nhiều nguyên liệu đổi cùng lúc: thêm thao tác theo lô (hiện mỗi nguyên liệu một lần).
