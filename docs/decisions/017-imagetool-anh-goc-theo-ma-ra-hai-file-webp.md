# ADR-017: ImageTool: ảnh gốc đặt tên theo mã, ra hai file WebP không mang EXIF, nguồn và giấy phép do người đặt

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-20
- Liên quan: [SPEC §7.3, §7.5](../SPEC.md), [PLAN 2.8](../PLAN.md), [ADR-011](011-dong-goi-du-lieu-va-luat-validate.md), [ADR-015](015-studio-xac-nhan-dinh-duong-nguyen-lieu.md), [ADR-016](016-studio-ghi-nhan-nau-thu.md), CLAUDE.md (quy tắc dữ liệu)

## Bối cảnh

PLAN 2.8: `MamCom.ImageTool` (SkiaSharp) đổi `data/images/raw/` thành WebP thumb 200px và full 800px, xoá EXIF, cập nhật đường dẫn ảnh trong JSON; xong khi xử lý được ảnh mẫu và validator kiểm tra file ảnh có tồn tại. Còn ngỏ:

- Ảnh gốc nào thuộc nguyên liệu hay món nào, và file ra tên gì (ADR-011: tên phẳng trong `data/images/out/`).
- "200px" và "800px" là cạnh nào; có cắt hay phóng to không.
- Ảnh chụp bằng điện thoại lưu chiều cầm máy trong EXIF, cùng vị trí GPS. Xoá EXIF mà không xoay trước thì ảnh nằm ngang.
- Schema bắt mọi ảnh phải có `source` và `license`; công cụ không thể biết chúng.
- Chạy lại thì sao; ảnh gốc có vào Git không.

## Quyết định

- **Một lệnh:** `MamCom.ImageTool process [--data] [--only] [--source] [--license] [--illustration | --photo] [--force]`. Mã thoát 0 là xong, 1 là còn ảnh lỗi hoặc không làm được gì, 2 là gõ sai lệnh.
- **Ảnh gốc** nằm phẳng trong `data/images/raw/`, tên `<id>.jpg` (hoặc `.jpeg`, `.png`, `.webp`, không phân biệt hoa thường), với `<id>` là mã của một nguyên liệu hoặc một món đã nằm trong `data/ingredients` hay `data/dishes`. Mã vừa là nguyên liệu vừa là món, hai ảnh gốc cùng mã, hay món còn là bản nháp thì từ chối ảnh đó và nói rõ lý do. Không có thư mục con theo loại: mã đủ để biết, và ADR-011 giữ `out/` phẳng. HEIC của iPhone chưa đọc được; công cụ nói cần xuất sang JPEG.
- **Ra hai file** `data/images/out/<id>-200.webp` và `<id>-800.webp`. Con số là **cạnh dài** của ảnh. **Không cắt**: người chụp đã dựng khung (SPEC §7.5: độ đúng quan trọng hơn độ đẹp). **Không phóng to**: ảnh gốc nhỏ hơn cỡ đó giữ nguyên cỡ và có lưu ý. WebP có mất mát, chất lượng 80.
- **Xoay đúng chiều trước, rồi mã hoá lại điểm ảnh.** Cả tám giá trị hướng của EXIF được áp vào điểm ảnh; mã hoá lại không mang theo gì, nên file ra chỉ có một chunk ảnh `VP8 `: không EXIF, không XMP, không vị trí chụp. Có test: ảnh JPEG mang một chuỗi riêng trong EXIF thì chuỗi không có trong hai file ra, và các hướng 1–8 cho đúng bốn góc.
- **Sửa file dữ liệu đúng như Studio** (`IngredientFile`, `DishFile`): chỉ đổi năm dòng của `image`, qua file tạm. Trước khi ghi, chạy `IngredientCheck` hoặc `DishCheck` với bản mới; còn lỗi thì không ghi file dữ liệu (hai file ảnh đã tạo vẫn nằm đó, vô hại, lần chạy sau dùng lại).
- **Nguồn và giấy phép không bao giờ do công cụ đặt.** Lấy từ `--source` và `--license` (áp cho mọi ảnh của lượt chạy), nếu không thì từ chính file dữ liệu; vẫn thiếu thì từ chối ảnh đó, không tạo file nào. Giấy phép của ảnh tự chụp là việc của chủ dự án. `--illustration` đặt cờ ảnh minh hoạ, `--photo` bỏ cờ (ảnh thật thay ảnh tạm); không nói gì thì giữ nguyên. Ảnh chọn từ ảnh gốc luôn **thay** ảnh đang có của nguyên liệu hay món.
- **Chạy lại được.** Hai file mới hơn ảnh gốc và file dữ liệu đã đúng thì bỏ qua; đổi nguồn hay giấy phép thì chỉ sửa file dữ liệu; ảnh gốc được thay thì làm lại; `--force` luôn làm lại. Mỗi ảnh gốc là một đơn vị: lỗi ở một ảnh không chặn ảnh khác.
- **Lưu ý, không phải lỗi:** ảnh gốc cạnh dài dưới 800px; ảnh nguyên liệu không vuông 1:1; ảnh món không 4:3 (xoay dọc vẫn được). Dung sai 3%, tính sau khi đã xoay đúng chiều.
- **Ảnh gốc không vào Git** (`.gitignore`: `data/images/raw/`): nặng, và ảnh điện thoại mang vị trí GPS; repo có thể công khai. Chỉ `data/images/out/` được commit. Ảnh gốc chỉ có ở máy người chụp, nên cần tự sao lưu.
- **SkiaSharp 3.119.4** (MIT) cùng `SkiaSharp.NativeAssets.Linux.NoDependencies` để CI chạy được mà không cần thư viện hệ thống. Bản 4.x đã có nhưng không có gì ta cần.

## Phương án đã cân nhắc

1. **Studio tải ảnh lên rồi tự xử lý.** ADR-015 đã bỏ: kéo SkiaSharp vào Studio và làm lẫn hai việc. Studio chỉ chọn trong file đã có.
2. **Thư mục con `raw/ingredients` và `raw/dishes`.** Rõ hơn khi mã trùng nhau, nhưng bắt người chụp biết loại; mã trùng hiếm và đã được báo.
3. **Cắt về 1:1 hay 4:3.** Phá khung người chụp và có thể cắt mất món; lưu ý đủ.
4. **Công cụ tự ghi một giấy phép mặc định (ví dụ "tự chụp").** Là quyết định pháp lý của chủ dự án, không phải của công cụ; schema đã bắt phải có nên từ chối rõ ràng tốt hơn.
5. **WebP không mất mát.** File lớn gấp nhiều lần, mà ảnh chỉ để nhận diện ngoài chợ.
6. **Đặt cỡ và chất lượng làm tuỳ chọn dòng lệnh.** Cỡ nằm trong SPEC, và tên file mang số `200`, `800`; đổi cỡ phải đổi cả SPEC, nên để là hằng số.
7. **ImageSharp.** CLAUDE.md đã loại vì giấy phép.

## Hệ quả

**Được**
- Một lệnh cho cả buổi chụp (4.6): bỏ ảnh vào `raw/`, chạy với `--source` và `--license`, file dữ liệu đổi đúng năm dòng và `validate` sạch.
- Ảnh trong app không bao giờ mang vị trí nhà người chụp.
- Chạy lại vô hại và nhanh khi không có gì mới.

**Đổi lại**
- Ảnh gốc không có trong Git: mất máy là mất ảnh gốc.
- Một lượt chạy dùng một nguồn và một giấy phép cho mọi ảnh; muốn khác nhau thì chạy `--only` nhiều lần hoặc sửa file dữ liệu.
- Chưa đọc HEIC, chưa xử lý nhiều ảnh cho một món (SPEC chỉ có một ảnh mỗi món).
- Bản Linux của Skia chưa chạy thử ở máy này; CI là lần đầu thấy.

## Khi nào xem lại

- Chụp xong 4.6 mà thấy hay mất ảnh gốc: đặt `raw/` ngoài repo hoặc dùng Git LFS.
- Cần HEIC: thêm bộ giải mã hoặc yêu cầu xuất JPEG trong quy trình chụp.
- Cần nhiều ảnh cho một món hay ảnh nền riêng cho ảnh minh hoạ: đổi quy ước tên.
- Chất lượng 80 cho ảnh nhỏ quá mờ hoặc quá nặng: đo trên ảnh thật rồi chỉnh.
