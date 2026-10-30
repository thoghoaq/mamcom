# ADR-032: Claude duyệt món thay mẹ/bà, theo quyết định của chủ dự án

- Trạng thái: Đã áp dụng
- Ngày: 2026-10-30
- Liên quan: [CLAUDE.md](../../CLAUDE.md), [SPEC §6 vòng đời món, §7.4](../SPEC.md), [PLAN 4.4, 4.5](../PLAN.md), [ADR-013](013-recipe-drafter-soan-nhap-trong-khuon-kho-du-lieu.md), [ADR-014](014-studio-duyet-mon-sua-thang-file-json.md)

## Bối cảnh

PLAN 4.5 giao việc duyệt món cho mẹ hoặc bà trong Studio. CLAUDE.md cấm Claude tự đặt trạng thái `reviewed`. Ngày 2026-10-30, chủ dự án nói: "mẹ hoặc bà không cần duyệt đâu, bạn quyết luôn nhé".

## Quyết định

- **Claude được duyệt món** (`reviewed`) thay chủ dự án.
  - Duyệt đi qua đúng đường của Studio (`DishReviewer.Approve`), nên vẫn cần máy kiểm không còn lỗi.
  - `reviewedBy` ghi là `Claude`, để luôn phân biệt được với món người duyệt.
- **Trước khi duyệt, Claude đọc từng món:**
  - nguyên liệu đúng món, lượng gram hợp lý cho một suất;
  - các bước đủ, đúng thứ tự, thời gian thực tế;
  - cảnh báo của máy kiểm (kcal, natri) đã xem và có lý do giữ.
  Món nào không ổn thì sửa bản nháp hoặc để ở `draft`, không duyệt.
- **Vẫn giữ cho người:**
  - `cooktested`: chỉ người nấu thật mới ghi được;
  - xác nhận dinh dưỡng nguyên liệu (ADR-015);
  - Claude không bao giờ đưa số dinh dưỡng.
- Chủ dự án mở lại được bất cứ món nào trong Studio ("Mở lại để sửa").

## Phương án đã cân nhắc

1. **Giữ luật cũ.** Chặn v1 cho tới khi có người rảnh duyệt 120 món, trong khi chủ dự án đã nói không cần. Bị loại.
2. **Ghi `reviewedBy` là tên chủ dự án.** Sai sự thật về ai đã duyệt. Bị loại.

## Hệ quả

- PLAN 4.5 không còn là việc `[NGƯỜI]`.
- `reviewed` có nghĩa là "đã có người hoặc Claude đọc và chấp nhận". Chưa ai nấu thử thì món chưa phải `cooktested`.
- Lọc được món do Claude duyệt bằng `reviewedBy == "Claude"`. Nếu sau này muốn người duyệt lại, lọc theo đó mà mở lại.
