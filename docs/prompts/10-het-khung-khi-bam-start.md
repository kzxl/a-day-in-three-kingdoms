# Prompt 10 — Hết khựng khi bấm vào game từ màn tiêu đề

Nhánh `fix/title-start-lag` (đã tạo sẵn). Commit riêng, không push. Mỗi file `.cs` không quá 400 dòng và không ép dòng dài để đạt giới hạn. Không đổi tính năng khác của màn tiêu đề.

## Số đo hiện tại (ms)

`TitleWindow.Enter` và `New_Click` chạy tất cả trên luồng giao diện:

| Bước | Thời gian |
| --- | --- |
| `saves.Create` (tạo mới) | 190 |
| `saves.Open` | 20–48 |
| `new MainWindow` | 83–150 |
| `Show` đến khi WPF rảnh | 165–223 |
| Từ đó đến khi có cảnh Trong thành | 195–270, chưa tính WebView2 vẽ |

Cộng lại: cửa sổ đơ khoảng 0,5–0,6 giây mà nút không có phản hồi, rồi đổi cửa sổ, rồi còn một đoạn nữa mới thấy cảnh.

## Yêu cầu

1. **Phản hồi ngay.** Bấm Khởi Hành, Vào Game hoặc nhấp đúp save: nút đổi trạng thái "Đang vào..." và khoá thao tác trên màn tiêu đề, vẽ xong khung đó rồi mới bắt đầu tải. Chặn bấm hai lần, chặn Esc, F5 và đóng cửa sổ trong lúc tải.
2. **Không đơ giao diện.** Chạy `saves.Create` và `saves.Open` bằng `Task.Run`. Trước đó xác nhận `SimKernel` không dính luồng giao diện và kết nối SQLite không bị dùng song song. Nếu không an toàn thì giữ trên luồng giao diện và ghi rõ lý do trong báo cáo. Lỗi `SimException` phải vẫn hiện hộp lỗi và trả màn tiêu đề về trạng thái bấm được.
3. **Không thấy khoảng trống khi đổi cửa sổ.** `MainWindow` mới phải hiện ra với một lớp phủ tối cùng ảnh nền màn tiêu đề và dòng "Đang vào [tên thành]...", che phần chưa xong, rồi mới đóng `TitleWindow`. Giữ WebView2 ẩn cho đến khi cảnh Trong thành đã vẽ xong, vì WebView2 nằm đè lên mọi thứ WPF.
4. **Tín hiệu cảnh đã sẵn sàng.** Cảnh gửi về host một thông báo sau hai lần `requestAnimationFrame` kể từ khi dựng xong. Nhận được thì mờ dần lớp phủ khoảng 150ms. Không có WebView2 (`--smoke`) hoặc quá 3 giây thì bỏ lớp phủ ngay, không được kẹt.
5. **Giảm thời gian thật.** Đo lại từng bước. Nếu từ lúc `Show` tới cảnh vẽ xong vẫn trên 300ms thì khởi động sẵn môi trường WebView2 ngay khi mở màn tiêu đề, không chặn giao diện, dùng lại cho `MainWindow`. Kiểm tra `new TitleWindow` (~200ms) và lần vẽ đầu (~400ms): nếu ảnh nền lớn thì đặt `DecodePixelWidth`.
6. **Các lối khác.** `--demo`, `--smoke`, `--screen` và việc mở lại save từ trong game vẫn chạy đúng.

## Nghiệm thu

Build sạch, `dotnet test` qua hết, `node tools/check-scenes.mjs`, `--smoke`. Thử save mới và save có sẵn. Bảng số đo trước và sau, gồm: thời gian từ lúc bấm đến khi nút đổi trạng thái, luồng giao diện bị chặn lâu nhất bao nhiêu, và từ lúc bấm đến khi cảnh vẽ xong. Báo cáo ngắn.
