# ADR-028: Số bếp của nhà: mỗi món nấu giữ một bếp từ bước đầu đến bước cuối

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-21
- Liên quan: [SPEC §4.1, §5.6, §8.1-1, §8.1-7](../SPEC.md), ADR-009 (lập lịch một người nấu), ADR-021 (màn hình đầu), ADR-026 (Cài đặt)

## Bối cảnh

Chủ dự án muốn người dùng nhập bếp nhà mình có mấy bếp. Đến nay `CookPlanScheduler` chỉ biết một người nấu: phần cần tay không chồng nhau, còn phần thụ động (nồi canh đang sôi, nồi kho đang liu riu) chạy song song không giới hạn. Một nhà chỉ có một bếp ga đơn thì ba nồi không thể cùng sôi, nên thời gian cả mâm, lịch của chế độ nấu và luật thời gian của bộ quay đều lạc quan quá. Dữ liệu món không ghi bước nào cần bếp: một bước có thể là thái, ướp, xào hay ninh.

## Quyết định

- **Tính theo món, không đổi dữ liệu** (chủ dự án chọn): mọi món có cách nấu khác "trộn" (`CookingMethod.Toss`) **giữ một bếp từ đầu bước đầu tiên đến hết bước cuối cùng**, kể cả bước ướp hay sơ chế mở đầu; món trộn không cần bếp. Không bao giờ có nhiều món đang nấu hơn số bếp. Nồi cơm điện không tính là bếp. Cách này ước dư (bước thái rau cũng bị tính là chiếm bếp), nhưng không đòi chủ dự án đánh dấu lại các bước của mọi món.
- **Lập lịch:** món thường vẫn theo thứ tự cũ (món dài trước, người nấu rảnh tay thì lấy bước đã sẵn sàng), thêm điều kiện: món chưa bắt đầu chỉ bắt đầu khi có bếp trống; hết bếp thì người nấu làm tiếp các món đang nấu, hoặc đợi tới lúc một bếp được trả. Món `serveHot` vẫn nhắm kết thúc đúng lúc mâm kết thúc; nếu khoảng đó hết bếp thì lùi tới lúc bếp đầu tiên được trả rồi xếp lại. Khi số bếp không ít hơn số món, lịch giống hệt trước.
- **Cận dưới `MinimumMealMinutes`** thêm một vế: tổng thời gian các món giữ bếp chia đều cho số bếp (làm tròn lên). Vẫn không giảm khi thêm món, và mọi lịch đều không ngắn hơn nó.
- **Số bếp ở đâu:** `Household.Stoves` (số nguyên, có thể để trống). Để trống nghĩa là nhà chưa nói, và dùng số mặc định trong `data/config/cooking-defaults.json` (`defaultStoves: 2`); lựa chọn là 1 đến `maxStoves` (4). Nhà đã lưu trước đây không có trường này, đọc lên là "chưa nói"; file của nhà chưa nói không ghi trường này.
- **Ai dùng nó:** thời gian cả mâm ở màn "Mâm hôm nay", lịch các bước của chế độ nấu, và luật thời gian của bộ quay (kể cả bộ lọc "dưới 45 phút"), đều tính với số bếp của nhà.
- **Hỏi ở đâu:** một hàng chip "Nhà mình có mấy bếp?" (1 bếp đến 4 bếp, kèm dòng "Không tính nồi cơm điện. Ít bếp thì các món phải nấu lần lượt, cả mâm lâu hơn.") ở **màn hình đầu** (cả khi sửa nhà mình) và ở **Cài đặt** (lưu ngay khi chạm). Chip của số mặc định hiện là đã chọn khi nhà chưa nói; chỉ khi chạm mới lưu một số.

## Phương án đã cân nhắc

1. **Thêm cờ "cần bếp" cho từng bước trong dữ liệu.** Chính xác hơn, nhưng phải đổi schema, Studio, RecipeDrafter và đánh dấu lại mọi món. Có thể làm sau, và khi đó cách tính theo món là cách dự phòng cho bước chưa đánh dấu.
2. **Chỉ lưu số bếp, chưa tính.** Không giúp gì cho người dùng có một bếp.
3. **Đoán bước cần bếp từ chữ trong bước ("xào", "luộc", "kho").** Dễ sai và khó giải thích.
4. **Hỏi số bếp chỉ ở Cài đặt.** Chủ dự án muốn hỏi cả ở màn hình đầu; câu hỏi có sẵn câu trả lời mặc định nên không làm màn đầu dài thêm một bước bắt buộc.

## Hệ quả

- Với một bếp, ba món nấu đi lần lượt và thời gian cả mâm dài ra thấy rõ; bộ quay với giới hạn thời gian sẽ nghiêng về mâm có món trộn hoặc món ngắn.
- Lịch vẫn là tham lam, không tối ưu. Test fuzz 5.000 mâm với 1 đến 4 bếp: không lúc nào có nhiều món giữ bếp hơn số bếp, tay không chồng, cận dưới đúng, không phụ thuộc thứ tự món; 30.000 mâm cũ (không giới hạn bếp) cho kết quả y như trước.
- **Việc mở:** cờ "cần bếp" theo bước nếu beta thấy lịch một bếp dài quá mức thật; nồi cơm nấu trên bếp (không có nồi điện) chưa được hỏi.
