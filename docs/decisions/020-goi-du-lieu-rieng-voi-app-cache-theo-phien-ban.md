# ADR-020: Gói dữ liệu triển khai riêng với app, cache theo phiên bản, chạy offline từ lần mở đầu

- Trạng thái: Đã áp dụng trong code
- Ngày: 2026-10-20
- Liên quan: [SPEC §7.6, §9](../SPEC.md), [PLAN 3.2](../PLAN.md), ADR-001 (JSON bundle), ADR-011 (bundle và `--release`), ADR-017 (ảnh `out/`), ADR-019 (ô giữ chỗ khi chưa có ảnh)

## Bối cảnh

PLAN 3.2: `BundleLoader`, `IUserStateStore` qua localStorage, service worker cache bundle và ảnh thumb, kiểm tra version khi online. Xong khi: app chạy offline sau lần tải đầu, và đổi bundle thì app nhận bản mới. SPEC §7.6 đã nói "cập nhật dữ liệu không cần phát hành lại app", nhưng chưa nói:

- Bundle nằm ở đâu so với app, và app biết có bản mới bằng cách nào mà không tải cả bundle (vài trăm KB đến vài MB) mỗi lần mở.
- Mở app có phải chờ mạng không, và đang lên mâm thì dữ liệu có được đổi dưới chân không.
- Lần ghé đầu tiên: trang tải xong **trước khi** service worker nắm quyền, nên mọi yêu cầu của lần đó không đi qua worker và không được cache. Bỏ mạng ngay sau lần đó thì có app mà không có món.
- Ảnh: thumb cần có offline, ảnh lớn chỉ khi xem.
- Trình duyệt không cho ghi localStorage (chế độ riêng tư, hết dung lượng) thì app làm gì.

## Quyết định

- **Gói dữ liệu là một thư mục, triển khai riêng với app.** `DataBuilder build --out <thư mục>` ghi `data-bundle.json`, `data-version.json` (`{ "version", "isRelease" }`, vài chục byte), `images/` (đúng các file thumb và full mà bundle nhắc tới, chép từ `data/images/out`; file không còn ai nhắc thì bị xoá khỏi thư mục) và `data-report.md`. Build lỗi thì xoá cả bundle lẫn file version cũ, như trước. Tên file nằm ở `BundleFiles` trong `MamCom.Core` cho công cụ và app dùng chung; bản JavaScript của service worker có cùng tên, và một test giữ hai bản bằng nhau.
- **Mỗi phiên bản của bundle là một địa chỉ bất biến** `data-bundle.json?v=<version>`. Cùng địa chỉ thì cùng từng byte, nên cache thế nào cũng không sai. `data-version.json` thì luôn hỏi mạng trước.
- **Mở app không chờ mạng.** `BundleLoader` nhớ phiên bản đang dùng (`mamcom.data.current`) và xin đúng địa chỉ đó; service worker trả từ cache. Chỉ máy chưa có bản nào mới hỏi `data-version.json` rồi tải. Sau khi màn hình đầu đã vẽ, **ở nền**: xin trình duyệt giữ dữ liệu (`navigator.storage.persist()`), hỏi `data-version.json`, nếu có bản mới thì tải về và nhớ là `mamcom.data.pending`, rồi nạp trước các thumb. Lần mở sau dùng bản `pending`; trong lúc đó, người dùng thấy thông báo "Đã tải xong dữ liệu món mới" kèm nút "Dùng ngay". **Dữ liệu không tự đổi trong lúc đang lên mâm.** Bản tải về hỏng (version không phải hash nội dung, hay không phải JSON) thì bị bỏ và không thử lại.
- **Hỏng, không phải "hết mạng".** `BundleLoadStatus`: `Loaded`, `NeedsNetwork` (máy chưa có bản nào và không với tới máy chủ), `NotPublished` (máy chủ trả 404, 410, hay một trang HTML thay cho file, như host SPA vẫn làm), `Invalid`. Mỗi trạng thái có thông báo riêng và nút "Thử lại" (tải lại trang). Lỗi máy chủ 5xx, hết giờ (8 giây cho file version, 60 giây cho bundle) và mất mạng đều là "không với tới".
- **Service worker: ba loại file, ba luật** (`service-worker.data.js`, tách khỏi worker của Blazor, chạy được trong Node để test):
  - `data-version.json`: mạng trước (`cache: 'no-store'`), giữ một bản; mất mạng thì bản đó trả lời.
  - `data-bundle.json?v=…`: cache trước, không bao giờ hỏi lại khi đã có; giữ hai phiên bản mới nhất.
  - `images/*`: có bản thì trả ngay và làm mới ở nền (stale-while-revalidate). Yêu cầu có header `X-MamCom-Preload` chỉ để nạp cache thì không làm mới ảnh đã có.
  - **Chỉ giữ câu trả lời tốt:** `ok` và đúng kiểu (JSON hay ảnh). Một 404, một lỗi máy chủ, hay trang `index.html` mà host SPA gửi cho đường dẫn nó không biết sẽ bị cache vĩnh viễn nếu không chặn.
  - Cache riêng `mamcom-data-v1`; precache của Blazor không có file nào của gói (`belongsToApp`), vì gói không thuộc bản build của app.
- **Làm ấm cache khi cài worker** (`warm`, trong `install`, cạnh precache của app). Lần ghé đầu trang chưa do worker quản, nên bundle không qua worker; không có bước này thì bỏ mạng ngay sau lần đầu là có app mà không có món. `warm` hỏi file version rồi tải đúng bundle đó qua chính các luật trên. Không bao giờ làm hỏng việc cài (mất mạng, chưa phát hành gói, hay máy chủ không trả lời đều chỉ là "chưa có gì để làm ấm") và không giữ cài quá 20 giây. Cái giá: lần ghé đầu tải bundle hai lần (một lần của app, một lần của worker).
- **Ảnh:** app nạp trước mọi thumb sau khi dữ liệu đã lên màn hình (`ThumbPreloader`, bốn yêu cầu một lúc), nhưng chỉ khi máy chủ với tới được. Ảnh lớn không nạp trước: giữ lần đầu xem. Thumb chưa có thì là ô giữ chỗ theo loại (ADR-019), nên **ảnh không bao giờ chặn app**. Vì lần ghé đầu worker chưa quản trang, thumb chỉ có sau lần mở online thứ hai; bỏ mạng ngay sau lần đầu thì thấy ô giữ chỗ.
- **Trạng thái người dùng:** `IUserStateStore` lưu JSON theo kiểu của Core, dưới `mamcom.state.<khoá>`. Trình duyệt không cho hỏi "khoá nào là của tôi" nên có một chỉ mục khoá (`mamcom.state.@keys`), và `ClearAsync` ("xoá dữ liệu của tôi") xoá đúng các khoá đó, không đụng phiên bản bundle. Khoá chỉ gồm chữ thường, số, `.`, `_`, `-`. Giá trị hỏng trả về `null`, không ném lỗi. Kiểu phải có trong `CoreJsonContext` (JSON sinh mã) để vẫn chạy khi app bị trim.
- **localStorage không ghi được** (chế độ riêng tư, hết dung lượng): dữ liệu sống trong bộ nhớ trang, `SetAsync` trả `false`, `IsPersistent` là `false`, và app hiện cảnh báo "thông tin gia đình sẽ mất khi đóng trang". Không chặn ai dùng.
- **Giao diện:** `DataNotices` trong `MamCom.UI` (đầu mỗi trang): đang mở, dữ liệu thử (`isRelease: false`, SPEC §7.6), bản mới sẵn sàng, không lưu được, và ba lỗi ở trên. `AppData` (một cho cả app, qua `IAppData` để bUnit thay thế) khởi động lúc `MainLayout` dựng, không chặn vẽ trang.

## Phương án đã cân nhắc

1. **Gói dữ liệu nằm trong build của app.** Mỗi lần duyệt thêm một món là phát hành lại app, trái với §7.6; và bản app cũ trong cache của người dùng giữ bản dữ liệu cũ.
2. **Hỏi `data-bundle.json` không có `?v=` và dựa vào ETag.** Mỗi lần mở tốn một vòng hỏi lại, và offline thì phải cậy vào cách trình duyệt xử lý cache HTTP. Địa chỉ theo phiên bản cho cache đúng bằng cấu trúc, không bằng tiêu đề.
3. **Đổi dữ liệu ngay khi tải xong bản mới.** Mâm đang quay hay đang nấu đổi theo dưới chân (món biến mất, số lượng đổi). Chờ lần mở sau, hoặc người dùng bấm.
4. **Đặt thumb vào bundle (base64).** Bundle phình, mọi thay đổi một ảnh làm đổi version cả bundle.
5. **Worker tự nạp thumb khi cài, bỏ `ThumbPreloader`.** Thumb có ngay lần ghé đầu, nhưng worker phải đọc bundle để biết tên ảnh, và khi có bản bundle mới thì cũng phải biết để nạp lại. Giữ trong app: app đã có bundle trong tay, và ảnh chỉ là tiện (có ô giữ chỗ). Xem lại nếu đo thấy lần đầu offline thiếu ảnh làm khó người dùng.
6. **Nhúng bundle vào app qua `wwwroot` lúc build.** Dễ, nhưng mất mục đích của việc tách; vẫn dùng được cho chạy thử trên máy (xem dưới).

## Hệ quả

- **Chạy thử trên máy:** `dotnet run --project tools/MamCom.DataBuilder -- build --out src/MamCom.Web/wwwroot` ghi gói cạnh app. Các file đó nằm trong `.gitignore`. Không có gói thì app báo "Máy chủ chưa có gói dữ liệu món ăn".
- **Việc cho 3.9 và 3.10 (E2E, CI và triển khai):** E2E chạy trên bản publish nên cần gói dữ liệu cạnh app; quy trình triển khai phải chạy `build --release --out <thư mục publish>/wwwroot` sau `dotnet publish`, rồi đưa `data-report.md` ra ngoài thư mục phục vụ (nó là báo cáo, không phải phần của gói). Với Azure Static Web Apps, `staticwebapp.config.json` nên trả `Cache-Control` ngắn cho `data-version.json` và dài cho `data-bundle.json?v=` và `images/`; app đã đúng nếu không có (worker tự lo).
- **Đã kiểm trên trình duyệt thật** (bản publish Release, 360×740): lần ghé đầu trên origin sạch để lại `data-version.json` và bundle trong cache dù trang chưa do worker quản; tắt máy chủ rồi tải lại vẫn mở được với đủ 23 món; mở online lần hai lưu hai thumb; đặt gói phiên bản mới lên máy chủ đang chạy thì lần mở sau thấy "Đã tải xong dữ liệu món mới", bấm "Dùng ngay" thì số món đổi từ 23 sang 21 và `pending` được dọn; xoá cache dữ liệu và `localStorage` rồi tắt máy chủ thì hiện "Lần đầu mở app cần có mạng…" kèm nút "Thử lại".
- **Chưa kiểm:** iOS Safari (có thể xoá dữ liệu sau một tuần không dùng; `persist()` chỉ là xin), trình duyệt trong Zalo; nên kiểm trên máy thật ở 3.10 (staging), muộn nhất là 5.2 (beta).
