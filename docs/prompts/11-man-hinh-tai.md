# Prompt 11 — Màn hình tải khi vào game

Nhánh `feat/loading-screen` (đã tạo sẵn từ `fix/title-start-lag`). Commit riêng, không push. Mỗi file `.cs` không quá 400 dòng và không ép dòng dài để đạt giới hạn.

Mockup đã duyệt: `docs/design/loading/Main.dc.html`, xem bằng https://claude.ai/artifact/GjxtejwVXc8Y1xfqTPPSrr. Trong file đó `/_blob/c79...` là `assets/images/backgrounds/luoyang_grand_market.jpg`, `/_blob/3f75...` là `assets/scenes/art/anim/traveler.png`. Làm đúng bố cục, màu, chữ và chuyển động của mockup.

## Mục tiêu

Người chơi chờ game tải xong hẳn rồi mới vào, không ép chạy. Màn này thay lớp phủ "Đang vào..." của prompt 10.

## Màn hình

Tạo control WPF `GameLoadingScreen` trong `Controls/`, gồm:
- nền ảnh chợ Lạc Dương làm tối;
- dấu 漢 và chữ "MỘT NGÀY Ở TAM QUỐC";
- tên ván và lượt;
- "ĐANG VÀO", tên thành, chữ Hán, vùng, ngày, mùa;
- khung "CHUẨN BỊ" 6 bước;
- dải "MẸO" đổi mỗi 4,5 giây;
- quan đạo, cột mốc, lữ khách đi và phần trăm.

Chữ Hán của thành lấy từ dữ liệu thành, không có thì bỏ trống. Mẹo đặt ở một chỗ, giữ nguyên 5 câu của mockup. Lữ khách vẽ từ `traveler.png`: ô 340×170, khung 0–3 đứng, 4–11 đi. Phóng 0,62, `NearestNeighbor`, chân chạm đường. Khung đi tính theo quãng đã đi, mỗi 16px một khung; đứng yên thì dùng khung đứng. Vòng lặp dùng `CompositionTarget.Rendering` và tháo ra khi xong.

## Tiến độ phải thật

Sáu bước giữ nhãn như mockup. Mỗi bước chỉ đánh dấu xong khi việc thật đã xong, không dùng hẹn giờ giả:
1. Mở sổ sách ván chơi: `saves.Create`/`saves.Open` bắt đầu chạy.
2. Nạp 300 danh sĩ thiên hạ, 3. Dựng 20 thành trì và thương lộ, 4. Tính giá chợ 24 mặt hàng: lấy từ các dòng "ready" của module khi kernel khởi động. `KernelOptions.LogSink` đã có, ví dụ `base.npc: npc registry ready...`, `base.world: map ready...`, `dlc.economy: ... market(24 items)`. Không bắt được thì đánh dấu khi `Open` trả về; ghi rõ trong báo cáo.
5. Khởi động khung cảnh: WebView2 khởi tạo xong.
6. Vẽ cảnh Trong thành: nhận thông báo `painted` đã có từ prompt 10.

Đích tiến độ = số bước xong / 6 + phần của bước đang chạy nếu đo được. Lữ khách đuổi theo đích như mockup: tối đa 0,42 quãng mỗi giây, mượt theo hệ số 6. Thanh vàng đi theo lữ khách, không bao giờ vượt phần đã tải thật.

## Luồng

1. Bấm vào ván (hoặc tạo mới) ở màn tiêu đề thì mở một cửa sổ tải đúng kích thước, vị trí, trạng thái phóng to mà `MainWindow` sẽ dùng, rồi đóng màn tiêu đề. Bước 1–4 chạy nền trong cửa sổ này.
2. Xong bước 4: tạo và hiện `MainWindow` cùng vị trí. `MainWindow` hiện cùng `GameLoadingScreen` đè lên, tiếp tục từ đúng trạng thái đang có (vị trí lữ khách, bước, mẹo) để người chơi không thấy đổi cửa sổ. Rồi đóng cửa sổ tải. WebView2 giữ ẩn như prompt 10 cho đến bước 6.
3. Xong cả 6 bước và lữ khách tới cuối đường: lữ khách đứng nghỉ, hiện nút "Vào thành" kèm dòng "hoặc nhấn Enter". Chỉ khi người chơi bấm nút hoặc Enter mới mờ dần màn tải khoảng 300ms. Không tự vào.
4. Lỗi khi mở ván: báo lỗi như hiện tại, quay về màn tiêu đề, không kẹt.
5. Bước 6 quá 10 giây (WebView2 lỗi hoặc không có) thì vẫn cho bấm "Vào thành"; cảnh sẽ hiện khi sẵn sàng hoặc dùng giao diện WPF như hiện nay. Không có WebView2 thì đánh dấu bước 5–6 xong ngay.
6. `--smoke`, `--demo`, `--screen`, `SIMT_DEMO_SCREENSHOT` không hiện màn tải và không chờ bấm.
7. Xoá lớp phủ "Đang vào..." cũ của prompt 10 nếu không còn dùng.

## Kiểm tra thêm

Lần trước có một test DLC lỗi một lần, chạy lại thì qua. Chạy toàn bộ `dotnet test` ba lần. Nếu test nào lúc qua lúc lỗi thì ghi tên và nguyên nhân; sửa nếu lỗi ở test.

## Nghiệm thu

Build sạch, `dotnet test` qua hết, `node tools/check-scenes.mjs`, `--smoke`. Chụp màn tải ở bước giữa, ở trạng thái sẵn sàng, và cảnh sau khi bấm Vào thành. Đo thời gian từ lúc bấm vào ván đến lúc nút "Vào thành" hiện, với save mới và save có sẵn. Báo cáo ngắn.
