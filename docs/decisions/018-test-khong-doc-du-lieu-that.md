# ADR-018: Test không đọc dữ liệu thật: seed đóng băng cho mọi test, một project riêng cho dữ liệu thật

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-20
- Liên quan: [SPEC §10](../SPEC.md), [PLAN 2.9](../PLAN.md), [ADR-014](014-studio-duyet-mon-sua-thang-file-json.md), [ADR-015](015-studio-xac-nhan-dinh-duong-nguyen-lieu.md), CLAUDE.md (quy ước code)

## Bối cảnh

Sáu project test chép `data/ingredients`, `data/dishes`, `data/plan` và `data/_suggestions` từ thư mục dữ liệu **thật** vào thư mục chạy của chúng, và nhiều test nói về seed 1.8 ("seed có bảy canh, tám món mặn, năm rau", "món nào cũng là bản nháp chưa ai nấu", "không có số dinh dưỡng nào được bịa", "món `ca-loc-kho-to` chưa có file thật").

Dữ liệu thật thì đổi, và phải đổi: đó là mục đích của Studio. Ngày 2026-10-20 người dùng duyệt ba bản nháp, duyệt thêm vài món và xác nhận dinh dưỡng của năm nguyên liệu; 80 test đỏ trên cây làm việc (Core 3, DataBuilder 20, FdcImporter 1, RecipeDrafter 56) dù code không đổi. Commit dữ liệu đó sẽ làm CI đỏ. Càng làm theo PLAN (khoảng 120 món, 250 nguyên liệu) thì càng đỏ.

Có hai loại câu hỏi bị trộn vào nhau:
- Về **code**: "bộ tính dinh dưỡng, bộ quay, Studio, DataBuilder, RecipeDrafter chạy đúng trên dữ liệu có hình dạng của seed không". Cần dữ liệu đứng yên.
- Về **dữ liệu**: "dữ liệu thật hiện giờ có hợp lệ không". Cần dữ liệu đi theo thật.

## Quyết định

- **Seed đóng băng** ở `tests/fixtures/seed/`: 33 nguyên liệu, 20 món (đều nháp), hai file plan và 5 gợi ý FDC, chép từ commit đã có lúc viết ADR này. Mọi project test chép những thư mục đó từ đây, không từ `data/`. Tên đích trong thư mục chạy không đổi, nên code test không đổi.
- **`data/schemas` và `data/config` vẫn lấy từ `data/`.** Một thay đổi schema hay config làm seed đóng băng hỏng thì các test nạp seed (ví dụ `LoadingTests`, `RuleTests`) đỏ, và người đổi schema cập nhật seed cho khớp. Seed không phải bản chụp của dữ liệu thật: không bao giờ chép dữ liệu thật đè lên nó.
- **Một project đọc dữ liệu thật: `MamCom.LiveData.Tests`.** Nó không giả định có bao nhiêu món hay món nào đã duyệt. Nó kiểm:
  - `DataBuilder validate` chế độ dev không có lỗi;
  - mọi file món và nguyên liệu đúng định dạng chuẩn (`DishFile`, `IngredientFile`), để Git chỉ thấy dòng đã đổi;
  - nguyên liệu có nguồn dinh dưỡng thật thì có `verifiedBy` và `verifiedAt` (số chỉ vào qua người xác nhận);
  - file gợi ý FDC đúng schema và nói về nguyên liệu mang tên file;
  - báo cáo của RecipeDrafter đúng schema và thuộc về bản nháp của nó;
  - không bản nháp nào đã là `reviewed` hay `cooktested` (chỉ Studio chuyển bản nháp sang `dishes/`).
- **Thư mục dữ liệu chép sang thư mục chạy được dọn trước mỗi lần build** (`tests/Directory.Build.targets`). Chép kiểu `PreserveNewest` chỉ thêm file, nên một món vừa rời seed vẫn nằm lại trong `bin` và test vẫn thấy nó. Không dọn thì máy người dùng đỏ cho tới khi xoá `bin` tay.
- **Quy ước:** test không đọc `data/` trực tiếp, trừ `MamCom.LiveData.Tests`. Số dinh dưỡng trong test vẫn là số chỉ dùng cho test, ghi rõ như trước.

## Phương án đã cân nhắc

1. **Giữ nguyên, sửa test mỗi lần dữ liệu đổi.** Mỗi lần duyệt một món lại phải sửa test; không bền.
2. **Làm các khẳng định về seed bớt cứng** (không đếm, không nói món nào là nháp). Mất đúng những kiểm tra có ích: "không số nào được bịa", "bản nháp chưa ai duyệt" chỉ có nghĩa với dữ liệu biết trước.
3. **Dựng seed bằng code trong test.** Gọn, nhưng hai mươi món công thức viết bằng C# mất hết tính "file thật đi qua code thật" và khó đọc hơn JSON.
4. **Lấy seed từ lịch sử Git (`git show <commit>:data/...`).** Test phụ thuộc vào `.git` và một mã commit; clone nông trong CI không có.
5. **Gom luôn schemas và config vào seed đóng băng.** Mất khả năng bắt trường hợp đổi schema làm hỏng dữ liệu.
6. **Để các kiểm tra dữ liệu thật trong CI qua `DataBuilder validate` thôi.** Có sẵn và giữ lại, nhưng nó không kiểm định dạng chuẩn, người xác nhận hay bản nháp đã duyệt.

## Hệ quả

**Được**
- Duyệt món và xác nhận nguyên liệu bao nhiêu cũng không làm test của code đỏ; đã thử với dữ liệu thật hiện tại (23 món, 8 món đã duyệt, 5 nguyên liệu đã xác nhận): 2.207 test xanh, build 0 warning.
- Dữ liệu thật vẫn có chỗ bị kiểm, theo quy tắc của dự án chứ không theo con số của seed.

**Đổi lại**
- Thêm một thư mục (`tests/fixtures/seed`, khoảng 80 file) và một project test. Seed có thể lệch hình dạng so với dữ liệu thật (ví dụ chưa có món `cooktested`); các test cần hình dạng đó dựng nó trong bộ nhớ hay thư mục tạm.
- Khi đổi schema làm seed hỏng, người đổi phải sửa cả seed.
- Test của code trên dữ liệu thật hiếm hơn: lỗi chỉ xuất hiện với dữ liệu thật (một món thật làm bộ quay bó tay) chỉ bị `LiveData` và `validate` thấy, không bị các test của code thấy.

## Khi nào xem lại

- Dữ liệu thật đủ lớn để một lần quay thử trên toàn bộ (property test với 120 món thật) có ích: thêm vào `LiveData` hay một project riêng.
- Seed cần một món `reviewed` hay `cooktested` để nhiều test dùng chung: thêm vào seed có chủ ý thay vì dựng trong từng test.
