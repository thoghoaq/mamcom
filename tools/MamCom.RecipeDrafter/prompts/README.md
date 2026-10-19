# Prompt của RecipeDrafter

Hai file này là toàn bộ lời dặn gửi cho Claude. Sửa ở đây rồi chạy lại là đủ, không cần sửa code.

- `system.md`: luật và cách viết công thức, cộng danh sách nguyên liệu. Giống nhau cho mọi món trong một lượt chạy, nên được cache (trả tiền một lần cho cả lượt).
- `dish.md`: phần riêng của từng món.

Chỗ điền dạng `{{tên}}` do chương trình thay vào. Thiếu giá trị cho một chỗ điền, hoặc thừa một giá trị không có chỗ, là lỗi: không bao giờ có chuyện chuỗi `{{...}}` lọt nguyên vào prompt.

| Chỗ điền | File | Nội dung |
|---|---|---|
| `{{ingredient_catalog}}` | system | mỗi nguyên liệu một dòng: id, tên, nhóm, nguồn đạm |
| `{{season_ids}}` | system | các mã mùa trong `data/config/spinner-defaults.json` |
| `{{protein_min_grams}}`, `{{protein_max_grams}}` | system | khoảng gram nguồn đạm của món mặn, lấy từ `data/config/validation-defaults.json` |
| `{{name}}`, `{{slot}}`, `{{slot_label}}`, `{{regions}}` | dish | một dòng của `data/plan/dish-list.csv` |

Định dạng của câu trả lời (các trường, giá trị cho phép, danh sách id nguyên liệu) do JSON Schema quy định, không nằm trong prompt: model không thể trả id nguyên liệu không có trong danh sách, và không có chỗ nào cho số dinh dưỡng.

Chương trình đọc prompt từ thư mục `prompts` cạnh file chạy; `--prompts <thư mục>` để dùng thư mục khác.
