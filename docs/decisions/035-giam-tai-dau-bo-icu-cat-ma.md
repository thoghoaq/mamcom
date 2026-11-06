# ADR-035: Giảm lần tải đầu: bỏ ICU, cắt mã framework triệt để

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-11-06
- Liên quan: [ADR-002](002-blazor-wasm-pwa-truoc.md), [PLAN 3.10](../PLAN.md), [SPEC §1 mục tiêu UX](../SPEC.md)

## Bối cảnh

PLAN 3.10 đo lần tải đầu trên 4G giả lập: trung vị 6,8 giây, chưa đạt mục tiêu ≤ 5 giây. Đo lại bản publish, tổng dung lượng nén Brotli của các file app tải lúc mở lần đầu là khoảng 2 614 KB. Phần lớn là:

| File | Dung lượng nén |
|---|---|
| Runtime (`dotnet.native.wasm`) | khoảng 980 KB |
| `System.Private.CoreLib` | khoảng 520 KB |
| Dữ liệu ngôn ngữ ICU (bộ cho tiếng Việt) | khoảng 220 KB |

App không hỏi văn hoá của trình duyệt: mọi con số, ngày và chuỗi đều tự định dạng bằng tiếng Việt với `CultureInfo.InvariantCulture`. Việc đổi chữ hoa/thường chỉ có vài lời gọi `ToLowerInvariant`; từ .NET 8, chế độ bất biến làm việc này cho cả chữ có dấu mà không cần ICU.

## Quyết định

- `InvariantGlobalization=true`: không tải ICU.
- `TrimMode=full`: cắt mã framework tới phần app thật sự dùng.
- Giữ nguyên `MamCom.Web` và `MamCom.UI` (`TrimmerRootAssembly`). Nếu không, bộ cắt mã xoá hàm khởi tạo của những trang chỉ được tạo bằng reflection (`NotFound`, `OnboardingScreen`), và app không mở được. E2E bắt được lỗi này.

## Đã đo (Brotli, tổng các file app tải lúc mở lần đầu)

| Cấu hình | KB | E2E |
|---|---|---|
| Như cũ | 2 614 | 14/14 |
| Bất biến + cắt triệt để, không giữ assembly nào | 2 221 | hỏng (`NotFound`) |
| Bất biến + cắt triệt để, chỉ giữ `MamCom.Web` | 2 292 | hỏng |
| Bất biến + cắt mặc định | 2 307 | hỏng (`OnboardingScreen`) |
| **Bất biến + cắt triệt để, giữ `MamCom.Web` và `MamCom.UI`** | **2 362** | **14/14** |

Giảm khoảng 250 KB (−10 %); trên 4G 9 Mbit/s là khoảng 0,2 giây tải. Chưa đo lại thời gian thật trên site staging.

## Phương án đã cân nhắc

1. **Tự giải nén `.br` bằng JavaScript (`loadBootResource`).** Hữu ích khi máy chủ không phục vụ sẵn file `.br`: runtime nén bằng Brotli mức cao nhỏ hơn bản Vercel tự nén. Nhưng cách này thêm một thư viện giải nén và tốn CPU trên điện thoại yếu; nên đo trên staging trước khi làm.
2. **Đánh dấu từng component bằng `[DynamicDependency]` để cắt cả mã của app.** Chỉ lợi thêm khoảng 70–140 KB, nhưng mỗi component mới đều phải nhớ đánh dấu. Không đáng.
3. **AOT.** Chạy nhanh hơn nhưng file tải về lớn hơn nhiều; ngược với mục tiêu tải đầu.

## Hệ quả

- Mọi chỗ định dạng hay so sánh chuỗi phải tiếp tục dùng `InvariantCulture`/`Ordinal`. Định dạng theo văn hoá (`ToString("N")` với culture `vi-VN`) sẽ không ra dấu phẩy thập phân kiểu Việt.
- E2E là lưới an toàn cho cắt mã: test chạy trên chính bản publish Release.
- Mục tiêu ≤ 5 giây cần đo lại trên staging. Bước tiếp theo nếu vẫn chưa đạt là phương án 1.
