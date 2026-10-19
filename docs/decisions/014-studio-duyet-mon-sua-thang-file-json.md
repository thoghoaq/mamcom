# ADR-014: Studio duyệt món: sửa thẳng file JSON, chỉ người bấm "Duyệt" mới đặt `reviewed`

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-19
- Liên quan: [SPEC §7.2, §7.4](../SPEC.md), [PLAN 2.5](../PLAN.md), [ADR-010](010-json-schema-thu-vien-va-pham-vi.md), [ADR-011](011-dong-goi-du-lieu-va-luat-validate.md), [ADR-013](013-recipe-drafter-soan-nhap-trong-khuon-kho-du-lieu.md), CLAUDE.md (quy tắc dữ liệu, chuỗi UI, Definition of Done)

## Bối cảnh

PLAN 2.5 yêu cầu `MamCom.Studio` (Blazor Server, chạy local) có phần duyệt món: danh sách theo trạng thái, xem và sửa, dinh dưỡng do Core tính, cảnh báo của validator, nút "Duyệt" ghi `status=reviewed`, `reviewedBy`, `reviewedAt` vào file JSON; xong khi duyệt được một món seed và diff Git sạch, đúng format. Còn ngỏ:

- Ghi file thế nào để diff Git chỉ có những dòng đã đổi.
- Ai đặt `validated`, ai đặt `reviewed`, và khi nào. CLAUDE.md cấm tự đặt `reviewed` và `cooktested`.
- Bản nháp của RecipeDrafter (`data/dishes/_drafts/`) thành món thật thế nào.
- Nhiều nơi cùng ghi một file: Studio, người sửa bằng tay, RecipeDrafter.
- Chuỗi tiếng Việt: CLAUDE.md nêu `MamCom.UI/Strings.vi.resx`, nhưng chưa có file đó, cũng chưa có design tokens.

## Quyết định

- **Studio đọc lại thư mục dữ liệu ở mỗi trang**, không cache, nên file sửa bằng tay hay RecipeDrafter vừa ghi hiện ngay. Thư mục dữ liệu lấy từ `Studio:DataFolder`, không có thì là thư mục `data` (có `schemas` bên trong) gần nhất từ thư mục chạy đi lên. Studio là công cụ của một người trên một máy: một tên người duyệt, nhớ lại giữa các món (`Studio:Reviewer` đặt sẵn được).
- **Một định dạng file món duy nhất**, `DishFile` trong DataBuilder, RecipeDrafter và Studio cùng dùng. Test chứng minh: mọi món seed đọc rồi ghi lại ra đúng từng byte; duyệt một món chỉ đổi đúng ba dòng (`status`, `reviewedBy`, `reviewedAt`); duyệt rồi mở lại trả file về đúng từng byte. Nhờ vậy "diff Git sạch" là tính chất được kiểm, không phải mong muốn.
- **Kiểm một món** bằng `DishCheck` trong DataBuilder: schema `dish`, rồi các luật của `DataValidator` có nghĩa với một món, chỉ lấy các phát hiện về chính món đó. Đây là phần `DraftChecker` của RecipeDrafter được chuyển sang để dùng chung; `DraftChecker` giờ chỉ đổi lời cho báo cáo nháp.
- **Luật vòng đời** nằm trong `DishReviewer`, không nằm trong trang:
  - **Lưu** chỉ khi máy kiểm không còn lỗi (cảnh báo không chặn). Món lưu xong là `validated`. Id, trạng thái, nguồn gốc và ảnh không do người sửa: lấy từ file.
  - **Duyệt** cần tên người duyệt, không còn lỗi và không còn thay đổi chưa lưu. Chỉ ghi ba trường trên (tên cắt khoảng trắng, ngày lấy từ `IClock`); không dòng nào khác đổi. Không có đường nào khác đến `reviewed`: chương trình không tự duyệt.
  - **Bản nháp được duyệt** thành `data/dishes/<id>.json`; bản nháp và `<id>.report.json` bị xoá sau khi file mới đã ghi. Nếu đã có món cùng mã thì từ chối, không ghi đè.
  - **Món `reviewed` và `cooktested` chỉ xem.** "Mở lại để sửa" chỉ có cho `reviewed`: về `draft`, xoá người và ngày duyệt. Món `cooktested` thuộc task 2.7.
- **Không ghi đè bản người chưa thấy.** Mỗi lần đọc có `Version` (băm nội dung); lưu, duyệt, mở lại so với file đang có, lệch thì không ghi gì và nạp lại bản mới. Ghi qua file tạm rồi đổi tên; chuỗi đọc, kiểm, ghi nằm trong một khoá.
- **Id từ địa chỉ trang thành tên file**, nên chỉ nhận kebab-case ASCII, kiểm bằng `\z` chứ không phải `$` (`$` cho qua id có dấu xuống dòng ở cuối; test đã bắt được lỗi này).
- **Giao diện:** Tổng quan; Món ăn (lọc theo trạng thái); `/dishes/{id}` và `/drafts/{id}` cùng một `DishWorkbench` gồm form sửa, dinh dưỡng, máy kiểm, ghi chú của RecipeDrafter, nguồn gốc, và thanh nút dính ở đáy để Lưu và Duyệt luôn trong tầm tay trên màn hình 360px. Máy kiểm chạy lại mỗi lần đổi một giá trị. Thông báo kết quả nói rõ đã làm gì hay vì sao không.
- **Dinh dưỡng** là `NutritionCalculator.ForDish`. Giá trị chưa có hiện "chưa đủ số liệu" kèm tên nguyên liệu thiếu, không bao giờ là 0. `NutritionFormat` và `NutritionSummary` nằm trong `MamCom.UI` để app dùng lại: kcal luôn kèm "≈" và làm tròn 10.
- **Chuỗi UI** ở `MamCom.UI/Strings.vi.resx`, gồm cả chuỗi của Studio (tiền tố `Studio_`). MSBuild coi hậu tố `.vi` là file vệ tinh theo văn hoá, thứ không bao giờ được nạp khi máy không chọn tiếng Việt; nên file khai báo `WithCulture="false"` và `ManifestResourceName`, thành resource trung tính của assembly, và lớp có kiểu `Strings` được sinh khi build. Test chạy với nhiều văn hoá khác nhau.
- **Giao diện dùng chung** ở `MamCom.UI/wwwroot`: `tokens.css` (màu, cỡ chữ 18px và 16px, chế độ nấu 24px, vùng chạm 48px, line-height 1.6, dark mode theo `prefers-color-scheme`) và `base.css`. Component không viết màu hay cỡ chữ riêng. Chưa nhúng font Be Vietnam Pro: chờ Phase 3, trong lúc đó dùng bộ font hệ thống.
- **Chạy ở mọi môi trường:** ngoài Development, Studio gọi `UseStaticWebAssets()`, nếu không `dotnet run --no-launch-profile` mất hết CSS của `MamCom.UI`.

## Phương án đã cân nhắc

1. **Tự động lưu bản dở khi gõ.** Ghi file sai vào `data/dishes/` làm hỏng `validate` và build. Bị loại: chỉ lưu khi sạch lỗi.
2. **Sửa JSON thô trong ô văn bản.** Dễ gõ sai cú pháp, người duyệt (mẹ, bà) không phải người viết JSON. Bị loại.
3. **Studio giữ cơ sở dữ liệu hay bản sao riêng.** Hai nguồn sự thật, mà SPEC chọn JSON trong `/data` là nguồn duy nhất. Bị loại.
4. **Duyệt bản nháp ngay trong `_drafts/`.** Build không đọc thư mục đó, nên món "đã duyệt" mà không vào được app. Bị loại: duyệt là chuyển vào `dishes/`.
5. **Cho sửa món đã duyệt tại chỗ.** Dấu duyệt thành vô nghĩa: duyệt một bản, ship một bản khác. Bị loại: phải mở lại, và mở lại xoá dấu duyệt.

## Hệ quả

**Được**
- Duyệt một món là một thao tác, để lại đúng ba dòng đổi trong Git, tên người và ngày.
- Mọi đường dẫn đến `reviewed` đi qua một hàm duy nhất, có test cho từng nhánh từ chối.
- RecipeDrafter và Studio kiểm bằng cùng một bộ luật.

**Đổi lại**
- Không lưu được bản dở: phải sửa hết lỗi máy kiểm mới lưu. Người sửa dở dang mà đóng trang thì mất phần chưa lưu.
- Thông báo lỗi schema còn tiếng Anh vì do thư viện sinh (ví dụ `exclusiveMinimum: ...`). Luật riêng của DataBuilder thì có tiếng Việt.
- Hai tab cùng sửa một món được chặn bằng `Version`, không hợp nhất: tab đến sau phải nạp lại và làm lại.
- Font Be Vietnam Pro chưa tự host; chữ hiển thị bằng font hệ thống cho tới Phase 3.
- Trình duyệt của người dùng quyết định dark mode (theo `prefers-color-scheme`), chưa có nút đổi tay.

## Khi nào xem lại

- Người duyệt quen sửa dở nhiều: thêm bản lưu nháp ngoài `data/` (ví dụ trong thư mục riêng của Studio) thay vì bỏ luật "chỉ lưu khi sạch lỗi".
- Nhiều người duyệt cùng lúc: Studio cần tài khoản và hợp nhất; hiện không cần.
- Task 2.6 và 2.7 thêm màn hình nguyên liệu và nấu thử: tách `DishWorkbench` thành các phần nhỏ nếu nó phình ra.
