# Prompt 01 — Cảnh diễn WebView2 và làm lại giao diện

Bạn là lập trình viên của repo này (game C#/.NET 8 WPF, Core + DLC). Đọc `README.md`, `docs/architecture.md`, `docs/module-authoring.md`, `docs/decisions.md` trước khi làm.

Mục tiêu: game đang là các bảng số. Biến nó thành game có tương tác: màn "Trong thành" làm trung tâm, nói chuyện với nhân vật bằng ảnh anime 2.5D, đấu võ bằng pixel art với hiệu ứng pixelfx. Các màn quản lý (chợ, kho, nghề…) vẫn là WPF nhưng đẹp và dễ bấm hơn.

## Luật chung

- Làm trên nhánh hiện tại `feat/scenes-webview2`. Commit theo từng giai đoạn. **Không push, không merge vào main.**
- Có người khác đang sửa 3 lỗi. **Không đụng vào:** `GameWorldMapControl.cs` (crash màu `Ink`), `samples/*.expected.txt` (golden), và chữ ký/logic của `TacticalDuelEngine.SimulateDuel` (seed). Vì thế màn Bản Đồ vẫn crash trong lúc bạn làm, đừng sửa nó. Golden replay đang lệch sẵn, đừng cập nhật hash.
- Máy này chỉ có .NET SDK 10 và Desktop Runtime 10. Đổi `global.json` sang `"rollForward": "latestMajor"` (giữ `net8.0`). Chạy test/app với biến môi trường `DOTNET_ROLL_FORWARD=Major`.
- Không vi phạm kiến trúc: `SimT.Host.Wpf` không biết kiểu của module; module chỉ tham chiếu `SimT.Contracts`; test trong `tests/SimT.Architecture.Tests` phải qua. Nếu thêm contract mới thì đặt ở `SimT.Contracts`.
- Mọi kết quả game do C# quyết định. JavaScript **chỉ diễn lại**, không tính tiền, sát thương, hảo cảm. Không dùng wall clock hay `new Random()` không seed trong code domain.
- Chữ tiếng Việt, một dòng, không xuống hàng: lời thoại ≤ 90 ký tự mỗi câu, lựa chọn ≤ 40 ký tự, nhãn nút ≤ 24 ký tự. Không dùng emoji trong UI mới; icon là SVG nét mảnh.
- File mới không quá 400 dòng. Không thêm thư viện ngoài `Microsoft.Web.WebView2`.

## Giai đoạn 1 — Khung cảnh diễn

1. Thêm NuGet `Microsoft.Web.WebView2` vào `SimT.Host.Wpf`.
2. `src/SimT.Host.Wpf/Scenes/`: một lớp phủ WebView2 nằm trong `MainWindow` (phủ toàn bộ vùng nội dung, không mở cửa sổ mới). Tạo WebView2 **một lần** lúc vào ván và giữ ấm, để mở cảnh không bị trễ. Dữ liệu người dùng WebView2 đặt trong `%LOCALAPPDATA%\SimT\webview`.
3. Nạp cục bộ bằng `SetVirtualHostNameToFolderMapping("simt.scene", <thư mục assets/scenes>, ...)`. Không có HTTP server, không tải gì từ mạng lúc chạy. Tắt DevTools, context menu, zoom và phím tắt trình duyệt ở bản Release.
4. Cầu nối hai chiều bằng JSON: C# gửi `PostWebMessageAsJson`, JS gửi `chrome.webview.postMessage`. **Ranh giới tin cậy:** C# chỉ nhận danh sách loại tin cho phép (`ready`, `choice`, `navigate`, `close`), kiểm tra mọi id có trong cảnh hiện tại, bỏ qua tin lạ.
5. Contract `SimT.Contracts/Scenes`: bản mô tả cảnh (`TalkScene`, `DuelScene`, `CityScene`) dạng record, và capability để module gửi cảnh tới host. Host render chung, không biết module nào.
6. `assets/scenes/`: `index.html`, `scene.css`, `bridge.js`, thư mục `art/`, `fonts/`, `vendor/pixelfx/`. Copy ra output và vào bản release (`release.cmd`, `docs/release.md`).
7. Font: `Noto Serif`, `Be Vietnam Pro`, `Ma Shan Zheng` đóng gói `.woff2` kèm giấy phép OFL. Không dùng Google Fonts lúc chạy.
8. Máy không có WebView2 runtime: game vẫn chạy, các cảnh rơi về hành vi cũ (`DuelBattleWindow`, dòng trạng thái). `--smoke` vẫn qua.

## Giai đoạn 2 — Nói chuyện 2.5D

Giao diện theo `docs/design/mockup/Dialogue.dc.html` (đọc README cùng thư mục để hiểu định dạng).

- Sân khấu parallax: trời, núi xa, đồi gần, sương, nhân vật, cành/hoa rơi lớp trước, vignette. Di chuột dịch lớp `-8, -18, +22, +40` px; nhân vật thêm `rotateY(mx*7deg) rotateX(-my*3deg)` trong `perspective: 1400px`. Nhân vật thở (`translateY -5px`, 4.6s) và lay nhẹ (±0.6deg, 7s). Tôn trọng `prefers-reduced-motion`.
- Hộp thoại giấy một dòng, bấm hoặc Space để sang câu, hiện `2 / 4`. Lựa chọn đánh số 壹貳參肆, bấm phím 1–4 được. Esc để rời cảnh.
- Tên, chức, hảo cảm và bậc quan hệ hiện ở trên hộp thoại.
- Ảnh tra qua `assets/scenes/art/manifest.json`: theo `np_npc.code`, không có thì theo vai trò.

**DLC mới `dlc.dialogue`** theo đúng `docs/module-authoring.md`, thêm vào `assets/products/merchant-slice.json`:
- Nội dung lưu trong migration của module (bảng `dg_*`). Thoại theo: nhân vật cụ thể (làm riêng cho Lưu Bị, Quan Vũ, Trương Phi, Gia Cát Lượng, Tào Tháo, Lữ Bố), vai trò (võ tướng, văn sĩ, thương nhân, nông dân, phụ nữ, lão nhân) và bậc quan hệ. Tối thiểu 3 mẩu thoại cho mỗi nhóm.
- Lựa chọn trong cảnh: hỏi chuyện thành này, hỏi giá hàng, tặng quà, đấu tay đôi, tạm biệt. Mỗi lựa chọn gọi capability có sẵn của `dlc.social`, `dlc.market`, `dlc.military`. Không viết lại luật của chúng; lựa chọn không có điều kiện thì ẩn đi.
- Nối vào nút "Gặp gỡ" ở `NpcDetailWindow` và các nút hồ sơ nhân vật.
- Thân phận hiện thô như `merchant` phải dịch ra tiếng Việt khi hiển thị.
- Test trong `tests/SimT.Modules.Dlc.Tests`: chọn đúng thoại theo bậc quan hệ; độ dài thoại đúng giới hạn; tắt DLC thì game vẫn boot.

## Giai đoạn 3 — Đấu võ pixel

Giao diện theo `docs/design/mockup/Duel.dc.html`, nhưng là pixel.

- Vendor pixelfx (MIT) vào `assets/scenes/vendor/pixelfx/`: `src/core`, các hiệu ứng dùng tới, `LICENSE`, `NOTICE.md`. Mã nguồn lấy ở `https://github.com/patch-gg/pixelfx`. **Không lấy font Patch Pixel.** Đọc `README.md` và `skills/pixelfx/SKILL.md` của nó.
- Khung canvas 480×270, `image-rendering: pixelated`, phóng số nguyên (3× ở 1440, 4× ở 1920). Sprite cao khoảng 120px. Hai sprite đứng đối diện, nền theo `art/backgrounds/`.
- Sprite chỉ có một tư thế, nên diễn bằng code: lấy đà, lao tới, khựng hình khi trúng đòn (`stop` 0.1–0.14s), rung màn, văng lùi, nhấp nháy trắng, về vị trí. Lật ngang sprite đối thủ.
- Bản đầu **phát lại `DuelResult.RoundLogs`** từ `TacticalDuelEngine` như hiện nay: mỗi hiệp hiện số sát thương, thanh sinh lực và tinh thần, dòng diễn biến. Giữ các nút tốc độ 1x/2x/4x và "Bỏ qua".
- Ánh xạ hiệu ứng theo `TacticalDuelAction` và binh chủng: chém `melee/sword-slash`, đâm `melee/thrust`, quét `melee/spear-sweep`, bắn `projectiles/arrow`, chí mạng `impacts/critical-hit`, đỡ đòn `impacts/hit-flash`, số `ui/damage-number`. Chọn thêm nếu hợp. Đổi màu bằng `palette`.
- Nút "Tỷ võ" ở Binh Nghiệp và trong cảnh nói chuyện mở cảnh này. Chọn binh chủng ngay trong cảnh trước khi đánh.

## Giai đoạn 4 — Màn "Trong thành" và làm đẹp WPF

**Màn "Trong thành"** (WebView2, là màn mặc định khi vào ván):
- Nền pixel của thành (`art/backgrounds/`), các điểm bấm được lấy từ `wd_sublocation` và các màn chính: Chợ, Kho, Quan phủ, Lôi đài, Quán trọ, Cổng thành. Bấm thì mở màn WPF tương ứng (tin `navigate`).
- Tối đa 6 nhân vật đang ở thành đứng trong cảnh bằng sprite, thở nhẹ, tên hiện khi rê chuột. Bấm thì vào cảnh nói chuyện.
- Thời tiết theo mùa bằng pixelfx: `weather/cherry-blossoms` mùa xuân, `weather/falling-leaves` mùa thu, tuyết mùa đông.
- Nút "Du ngoạn phố" hiện kết quả thành cảnh nhỏ thay vì dòng trạng thái.

**WPF** (`Themes/Dark.xaml`, `MainWindow.xaml`):
- Đổi bảng màu sang sơn mài và lụa: nền `#15110d #1d1611 #241a13`, viền `#3a2c20`, chữ `#eadfc6 #f3e6cc #b9aa8c`, đồng `#c4a062`, chu sa `#a8321f`, ngọc `#3f6b55`. Giữ nguyên tên key, thêm key `Ink` nếu cần nhưng **không sửa file map**. Font tiêu đề Noto Serif.
- Cột trái: thu gọn các thẻ thành một thẻ người chơi nhỏ, để danh sách màn **hiện đủ, không phải cuộn**, chia nhóm: Trong thành, Kinh doanh, Nhân duyên, Quan võ, Sổ sách.
- Vùng bấm tối thiểu 36px, bảng dày cột thì giảm cột phụ, số canh phải. Thay emoji ở nút bằng icon vẽ bằng `Path`.
- Tiêu đề cửa sổ là "Một Ngày Ở Tam Quốc", không phải "SimT".

## Nghiệm thu từng giai đoạn

1. `dotnet build SimT.sln -c Release` không lỗi không cảnh báo mới.
2. `dotnet test SimT.sln -c Release` qua hết, kể cả test kiến trúc.
3. `SimT.Host.Wpf.exe --smoke` qua.
4. Cập nhật `README.md`, thêm quyết định vào `docs/decisions.md` (D1 được xem lại: WPF + WebView2 cho cảnh diễn) và mục cảnh diễn vào `docs/architecture.md`.

Khi xong, báo: đã commit gì, phần nào chưa làm, phần nào chưa chạy thử được.
