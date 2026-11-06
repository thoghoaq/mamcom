# ADR-036: Tên app là Bowlo

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-11-06
- Liên quan: [ADR-033](033-giao-dien-mam-go-am.md), [PLAN 6.3](../PLAN.md)
- Thiết kế: canvas Claude Design "MâmCơm — giao diện mới" (https://claude.ai/artifact/SEwB4E7Y6bB67inZjpSgTG), các artboard "Tên và logo" và "Bowlo — logo đã chọn".

## Bối cảnh

"MâmCơm" là tên tạm. Chủ dự án nhờ đặt tên và logo, rồi lần lượt loại các hướng:
- **"Mâm Ơi":** nghe hơi sến.
- **Mọi tên có chữ "mâm"** (Quay Mâm, Mâm Nhà…): không thích chữ "mâm".
- **"Hôm nay ăn gì":** đã có ít nhất ba app trùng tên, và quá chung để tìm kiếm.

Sau đó chủ dự án chọn tên tiếng Anh.

Các tên tiếng Anh dễ nghĩ ra đều đã có người dùng hoặc rất gần một tên đã có:
- **DinnerDice:** gần Diner Dice;
- **HomeBite:** là một dịch vụ giao đồ ăn;
- **Cookroll:** gần nhãn hiệu "Cook and Roll";
- **Dinnerly:** là một hãng meal kit ở Mỹ.

## Quyết định

- **Tên app là Bowlo**, đọc "bâu-lô": từ "bowl" (bát cơm), thêm "o" cho vui tai.
  - Khẩu hiệu: *"Hôm nay ăn gì? Bowlo lo."*
- **Logo** là bát cơm sứ nhìn ngang: viền men xanh gợi bát Bát Tràng, cơm vun, hai làn khói, trên nền đỏ gạch của app.
  - Ở đầu trang chỉ có cái bát, tô bằng token giao diện nên đổi theo nền sáng và tối.
  - Biểu tượng `icon-192.png` và `icon-512.png` được vẽ ra từ cùng file SVG.
- **Những chỗ đổi tên:**
  - tiêu đề trang;
  - manifest PWA (tên, tên ngắn, mô tả);
  - đầu trang của app và của Studio ("Bowlo Studio");
  - bản xuất phản hồi;
  - báo cáo dữ liệu.
- **Giữ nguyên tên trong code** (`MamCom.*`, tên repo, các khoá `mamcom.state.*` trong localStorage): người dùng không thấy chúng. Đổi khoá localStorage còn làm mất dữ liệu của người đã dùng.

## Phương án đã cân nhắc

Các tên còn lại trong vòng chọn cuối:
- **Comi** (từ "cơm"): gần Komi và Commis, hai app công thức nấu ăn.
- **SupperSpin:** khó đọc với người lớn tuổi, và giống ý tưởng Dinner Spinner của Allrecipes.

## Hệ quả

- Trước khi ra mắt thật, chủ dự án cần kiểm hai việc mà phiên này chưa làm:
  - tên miền (`bowlo.app`, `bowlo.vn`) còn trống không;
  - tên đã có ai đăng ký nhãn hiệu ở Cục Sở hữu trí tuệ chưa (PLAN 6.3).
- Cài đặt "Xoá dữ liệu" và dữ liệu cũ của người dùng không bị ảnh hưởng.
