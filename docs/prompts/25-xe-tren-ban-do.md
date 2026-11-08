# Prompt 25 — Sprite người đi trên bản đồ

Làm trong worktree đã tạo sẵn, nhánh `feat/tq-08b-xe`. Đây là phần bổ sung cho đợt 8; đọc phần "Luật chung" (nhất là luật Giao diện) trong `docs/spec/nang-cap-tam-quoc.md`.

- Commit trên nhánh này, không push.
- Không sửa kernel.
- Ảnh nháp và ảnh QA đặt ở `D:\Hung DATA\Tool\_tq_shots\08b\`, không commit vào repo. Chỉ commit sprite đã duyệt.

## Vấn đề

Người đi trên bản đồ (`assets/scenes/map-travellers.js`) đang là hình SVG đơn sắc, rất nhỏ, lệch với phong cách tranh của bản đồ. Ở Lạc Dương và Trường An chúng còn chồng lên nhau và che tên thành.

## 1. Vẽ sprite (dùng công cụ tạo ảnh của bạn)

**Phong cách.** Pixel art cùng kiểu với `assets/scenes/art/sprites/*.png` và các nhà, núi pixel trên `art/map/thien_ha.webp`. Mở vài file đó ra xem trước khi vẽ. Yêu cầu chung:
- nhìn ngang, hướng sang phải;
- viền tối, màu trầm hợp bản đồ giấy cũ;
- chi tiết đủ để nhận ra ngay khi thu nhỏ còn khoảng 36px.

**Bốn loại**, mỗi loại 2 khung chuyển động (bánh xe quay, chân ngựa hoặc chân người bước):

| Loại | Hình |
| --- | --- |
| `rival` | Thương nhân đối thủ: một xe ngựa kéo chở hòm hàng, có phu xe. |
| `caravan` | Thương đoàn của người chơi: xe bò chở hàng phủ bạt, cắm một cờ hiệu nhỏ màu son (#a8321f) để nhận ra là của mình. |
| `npc` | Danh sĩ: một người áo dài, đội mũ, cưỡi ngựa. |
| `army` | Quân binh: tốp 3 lính bộ cầm giáo, một người vác cờ tam giác. Cờ để màu trắng ngà, để tô màu thế lực bằng code. Vẽ cờ thành lớp riêng hoặc tách được bằng mặt nạ. |

**Cách vẽ và tách nền.**
- Vẽ trên nền một màu phẳng, rồi tách nền bằng script.
- Kiểm tra không còn viền màu nền: mọi pixel có alpha > 0 không được gần màu nền (ví dụ nền hồng tím thì không còn pixel `r-g>40 && b-g>40`).
- Thu về khung 96×64 bằng nearest-neighbor. Hai khung của cùng một loại phải chung chân đế và chung tâm.

**Lưu.**
- `assets/scenes/art/map/travellers.png`: 4 hàng (`rival`, `caravan`, `npc`, `army`) × 2 cột khung, mỗi ô 96×64, nền trong suốt.
- Cờ quân nếu tách lớp thì lưu `travellers_army_flag.png` cùng lưới.
- Khai báo trong `manifest.json`, và `check-scenes.mjs` kiểm file tồn tại.

## 2. Gắn vào bản đồ (`map-travellers.js`, `map-scene.js`, `map-scene.css`)

**Hiển thị.**
- Thay hình SVG bằng `<image>` cắt đúng ô từ `travellers.png`, dùng `clipPath` hoặc `viewBox` của một `<svg>` con.
- Kích thước trên màn hình khoảng 36px rộng ở cửa sổ 1076 và khoảng 44px ở 1440. Tính theo tỉ lệ thu phóng hiện có của bản đồ.
- Bật `image-rendering: pixelated`.
- Đi về bên trái (theo hướng đoạn đường đang đi) thì lật ngang.
- Có bóng elip mờ dưới chân.
- Cờ quân tô theo `color` của thế lực.

**Chuyển động.**
- Đổi 2 khung mỗi khoảng 260ms, nhấp nhô 1px.
- Dùng chung vòng `requestAnimationFrame` đang có của lớp hạt mùa.
- Dừng khi cảnh ẩn hoặc khi `prefers-reduced-motion`.

**Không che thành.**
- Không đặt người đi trong 14% đầu và cuối của mỗi đoạn đường giữa hai thành. Nếu `progress` rơi vào đó thì kẹp lại.
- Hai người đi cách nhau dưới 24px thì đẩy lệch vuông góc với đường, xen kẽ ±10px.
- Người đi không được đè lên chấm thành hay nhãn tên thành. Dùng hộp nhãn có sẵn để kiểm.

**Chú thích và chú giải.**
- Rê chuột vẫn hiện tên và lộ trình như hiện tại.
- Phần chú giải "Người đi" dùng khung 0 của chính sprite thay cho hình SVG cũ.

## Kiểm tra

- `check-scenes.mjs`:
  - sprite tồn tại, đúng kích thước 192×256;
  - không còn viền màu nền;
  - người đi không nằm trong vùng 14% đầu và cuối đoạn;
  - hai người gần nhau được tách ra.
- Đo thời gian mở bản đồ, không chậm hơn quá 10ms so với trước.

## Nghiệm thu

- Build Release sạch, `dotnet test SimT.sln -c Release` qua, `node tools/check-scenes.mjs` qua, `--smoke` chạy được.
- Ảnh:
  - sprite sheet phóng to 4 lần;
  - bản đồ ở cỡ 1076 và 1440;
  - ảnh cắt phóng to vùng Lạc Dương – Trường An.
- Báo cáo ngắn.
