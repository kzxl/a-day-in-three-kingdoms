# Prompt 04 — Hoạt ảnh toàn thân, bản đồ mới và màn tính năng tương tác

Làm trên nhánh `feat/animation-feature-ui`. Commit theo từng giai đoạn. **Không push, không merge vào main.**

Mockup đã duyệt nằm ở `docs/design/mockup-v2/`, đọc cách đọc file ở `docs/design/mockup/README.md`. Bám đúng bố cục, chữ, màu, hành vi của mockup. Số liệu trong mockup là mẫu; số thật lấy từ dịch vụ và contract của game.

| Board | File | Việc |
|---|---|---|
| 1 Đấu võ | `Main.dc.html` | Cảnh đấu dùng khung hình toàn thân |
| 2 Trong thành | `City.dc.html` | Nhân vật đứng, đi lại; chân dung chớp mắt, mấp máy miệng |
| 3 Nhân Duyên | `NhanDuyen.dc.html` | Thay màn bảng hiện tại |
| 4 Chợ | `Cho.dc.html` | Thay màn bảng hiện tại |
| 5 Bản đồ | `BanDo.dc.html` | Thay bản đồ hiện tại |
| 6 Hồ sơ | `HoSo.dc.html` | Danh Sĩ và hồ sơ có ảnh riêng |
| Thanh chức năng | `Rail.dc.html` | Thay cột điều hướng trái |

## Luật chung

- Giữ các luật ở đầu `docs/prompts/01-canh-dien-webview2.md`: kiến trúc Core + DLC, C# quyết định kết quả, JS chỉ diễn, chữ một dòng, không emoji, file không quá 400 dòng, máy chỉ có SDK 10 nên chạy với `DOTNET_ROLL_FORWARD=Major`.
- `SimulateDuel` và `samples/*.expected.txt` vẫn do người khác sửa, không đụng.
- **Tự kiểm bằng CDP** như prompt 03 (`--remote-debugging-port=9222`), xem ảnh chụp thật, console không lỗi. Với màn WPF thì chụp bằng `--demo --screen <id>`.

## Giai đoạn 1 — Bộ chạy hoạt ảnh

File khung: `assets/scenes/art/anim/<id>.png`, một hàng 24 ô, mỗi ô 340×170, chân đứng ở đáy, thân ở giữa ô. Thứ tự: 0–3 đứng thở, 4–11 đi, 12–19 đánh (ô 17 là lúc chạm), 20–22 trúng đòn, 23 khuỵu. Có file này thì dùng, không có thì rơi về sprite tĩnh như hiện tại.

Viết `assets/scenes/sprite-anim.js` dùng chung cho cảnh đấu và Trong thành, chép cách chạy từ `Main.dc.html` và `City.dc.html`:
- Vòng `requestAnimationFrame` theo thời gian thật. Không dùng `setInterval`, không nhảy vị trí theo nấc.
- Khung đi tính theo **quãng đường đã đi**, không theo thời gian, để chân không trượt. Thân nhấp nhô theo `|sin|` của quãng đường.
- Đánh theo bảng thời lượng từng khung, lao tới bằng smoothstep. Lúc chạm: hit-stop 0,1 giây, chớp trắng, rung màn giảm dần, đối thủ bị đẩy lùi chậm dần, rồi tự đi về chỗ.
- Bóng đổ dưới chân. Hết máu thì khuỵu ở khung 23.
- Dữ liệu vẫn phát lại từ `DuelResult.RoundLogs`. Hiệu ứng pixelfx giữ nguyên.

Trong thành: nhân vật có file khung thì đi qua lại và đứng thở như mockup. Chân dung: nếu có `portraits/<id>/frames/blink.webp` và `talk.webp` thì chớp mắt ngẫu nhiên và mấp máy miệng khi chữ đang chạy.

## Giai đoạn 2 — Thanh chức năng

Thay cột trái theo `Rail.dc.html`: 5 nhóm mở ra thu lại, mỗi mục có ấn chữ Hán, tên, một dòng mô tả, huy hiệu việc cần làm. Nhóm chứa màn đang mở tự mở. Mô tả lấy đúng chữ trong mockup. Giữ các nút Ngày tiếp, Nghỉ ngơi, Du ngoạn phố.

Thêm thanh **"Nên làm"** ở đầu các màn: dòng gợi ý bước tiếp theo do module trả về (trường tuỳ chọn trong read model), không có thì ẩn.

## Giai đoạn 3 — Bản đồ

Làm trong WebView2 theo `BanDo.dc.html`, nền `art/map/thien_ha.webp`, toạ độ thành là toạ độ thật trong `BanDo.dc.html` (dựa kinh vĩ độ). Cập nhật `MapX`, `MapY` hoặc giữ bảng riêng cho cảnh.
- Tuyến đường và số ngày lấy từ `wd_route`, đường ngắn nhất tính ở C#.
- Bộ hành, Dịch mã, Tiêu cục dùng đúng công thức có sẵn trong `WorldModule`.
- Bấm "Lên đường" gọi lệnh đi thật. Token trôi dọc lộ trình rồi cập nhật ngày, tiền.
- 3 lớp xem: Thành trì, Giá hàng, Độ hiểm trở.
- Giữ phóng to và kéo bản đồ từ bản hiện tại.

## Giai đoạn 4 — Nhân Duyên và Chợ

Hai màn này chạy trong WebView2 như cảnh, dùng read model và lệnh có sẵn (`chat:`, `gift:`, `recruit:`, `swear:`, `skill:equip:`, `skill:unequip:`, `buy:`, `sell:`…). Không viết lại luật.
- **Nhân Duyên:** tab Quan hệ, Tuyệt kỹ, Thư từ, Gia thần. Bản đồ vòng tròn theo hảo cảm, nút dịch vào khi hảo cảm tăng. Mốc mở khoá hiện đúng điều kiện từ `GetRankRequirements` và `SwearBrotherhood`. Tuyệt kỹ là 6 ô theo luật 1 phẩm S, 2 phẩm A, còn lại B–C.
- **Chợ:** thẻ hàng có đường giá 10 ngày (`mk_price_sample`), bảng so giá các thành, chọn số lượng, ước tính lãi.

## Giai đoạn 5 — Danh Sĩ và hồ sơ

- Thêm `manifest.profile`: tra `assets/scenes/art/profiles/<npc code>.webp`, không có thì dùng ảnh theo archetype. Dùng chung cho danh sách, thẻ, hồ sơ, cảnh nói chuyện.
- Làm lại `NpcDetailWindow` và màn Danh Sĩ theo `HoSo.dc.html`: ảnh lớn thay ô chữ Hán, biểu đồ 8 năng lực, đặc kỹ, tiểu sử.

## Nghiệm thu

1. `dotnet build SimT.sln -c Release` sạch, `dotnet test` qua hết, `node tools/check-scenes.mjs` qua, `--smoke` qua.
2. Bằng CDP: Trong thành → nói chuyện → đấu → Bản đồ → Lên đường → Nhân Duyên → Chợ, console không lỗi.
3. Cập nhật `README.md`, `docs/architecture.md`.

Báo cáo: commit nào, phần nào chưa làm, phần nào chưa chạy thử, kèm đường dẫn ảnh chụp.
