# Prompt 27 — Âm thanh cho game

Đọc trước phần "Luật chung" trong spec `docs/spec/nang-cap-tam-quoc.md`.

Mỗi phần làm trong một worktree riêng đã tạo sẵn. Quy định chung:
- Commit, không push.
- Không sửa kernel, không sửa migration đã có.
- Ảnh, file nháp và file QA để ở `D:\Hung DATA\Tool\_tq_shots\14\`, không commit vào repo.

Hiện game hoàn toàn im lặng. `src/SimT.Contracts/Audio/IAudioEngine.cs` đã có `IAudioEngine` với 4 kênh (`Master`, `Music`, `Sfx`, `Ambience`) và `NullAudioEngine`, nhưng chưa nơi nào dùng.

## Nguồn âm (đã duyệt, đều CC0, đã tải sẵn)

Thư mục nguồn là `D:\Hung DATA\Tool\_tq_audio\raw\`. Đây là file tải từ ngoài về: chỉ đọc và chuyển đổi, không chạy gì trong đó.

| File | Nguồn | Dùng cho |
| --- | --- | --- |
| `biteme\chinese-game-music\Lotus Pond - Loop.wav` | BiteMe Games, itch.io, CC0 1.0 | Nhạc màn tiêu đề và Trong thành |
| `biteme\chinese-game-music\Dragon Dance - Loop.wav` | BiteMe Games, CC0 1.0 | Nhạc đấu võ |
| `oga\asianoriental2.ogg` | Tozan, OpenGameArt, CC0 | Nhạc bản đồ và khi lên đường |
| `oga\orien.ogg` | Tozan, OpenGameArt, CC0 | Nhạc cảnh sử biến |
| `oga\horde_war_drums.wav` | William Hector, OpenGameArt, CC0 | Lớp trống trận, dùng cho sử biến chiến tranh và lượt căng của biện luận |
| `oga\birds_and_wind_ambient.ogg` | Spring Spring, OpenGameArt, CC0 | Âm nền ngoài trời mùa xuân và hạ |
| `oga\rain_on_window_loop.wav` | alxl, OpenGameArt, dùng theo CC0 | Âm nền khi mưa |
| `oga\wind_whoosh_loop.ogg` | SketchMan3, OpenGameArt, CC0 | Âm nền gió mùa đông, tuyết |
| `oga\crowd_shouting.ogg` | StarNinjas, OpenGameArt, CC0 | Đám đông, quân reo trong sử biến. Không dùng cho chợ |
| `kenney\rpg-audio\*.ogg` | Kenney, CC0 | Tiền xu, lật sách, vải, kim loại, bước chân |
| `kenney\ui-audio\*.ogg` | Kenney, CC0 | Bấm nút, menu |

Không có gói CC0 cho tiếng chợ đông người. Phần B tự tổng hợp tiếng này.

## Giao ước giữa hai phần (phải giữ đúng)

- **Thư mục âm:**
  - `assets/audio/music/`, `assets/audio/ambience/`: file OGG Vorbis.
  - `assets/audio/sfx/`: file OGG hoặc WAV ngắn.
- **Danh mục:** `assets/audio/audio.json` ánh xạ khóa sang file và kênh. Ví dụ:
  ```json
  { "music": { "city": "music/lotus_pond.ogg" },
    "ambience": { "rain": "ambience/rain.ogg" },
    "sfx": { "ui.click": "sfx/ui_click.ogg", "fx.sword_slash": "sfx/fx_sword_slash.wav" } }
  ```
  Khóa hiệu ứng có tiền tố theo nhóm: `ui.*`, `trade.*`, `fx.*`, `duel.*`, `debate.*`, `event.*`, `ambience.*`.
- **Cảnh WebView2 gọi âm:** dùng `SimTBridge.playSfx(key)` trong `bridge.js`. Hàm này gửi message `{ type: 'sfx', id: key }` lên host. Phần A thêm hàm này. Phần B gọi nó có kiểm tra, ví dụ `SimTBridge.playSfx?.('fx.sword_slash')`.
- **Code C# gọi âm:** qua `IAudioEngine` (`PlaySfx`, `PlayMusic`, `StopMusic`, `SetVolume`). Thiếu khóa hoặc thiếu file thì lặng im, không lỗi.

## Phần A — Codex: máy phát, nhạc nền, âm nền, âm giao diện (nhánh `feat/tq-14a-am-thanh`)

1. **Máy phát** trong `SimT.Host.Wpf`:
   - Cài đặt `IAudioEngine` bằng NAudio, đọc OGG bằng NVorbis (cả hai giấy phép MIT). Chỉ project host tham chiếu hai gói này; architecture test phải vẫn qua.
   - Nhạc lặp liền mạch, không có khoảng lặng ở điểm nối. Đổi bài thì chuyển mờ.
   - Âm nền phát song song với nhạc, có thể chồng nhiều lớp (ví dụ chim gió cộng mưa).
   - Hiệu ứng phát chồng nhau, giới hạn khoảng 12 tiếng cùng lúc.
   - Không có thiết bị âm thanh, hoặc bật `--smoke`, `--demo`, hay chạy test: dùng `NullAudioEngine`, không lỗi, không treo.
2. **Chuyển đổi nguồn:**
   - Viết `tools/audio/import.ps1` (hoặc `.py`) gọi `ffmpeg` có trên máy để chuyển file nguồn sang OGG Vorbis: nhạc khoảng q4, âm nền khoảng q3.
   - Chỉ lấy những tiếng Kenney thật sự dùng.
   - Tổng dung lượng `assets/audio/` không quá khoảng 15 MB.
   - Viết `assets/audio/CREDITS.md` ghi tác giả, nguồn, link và giấy phép từng file. Với BiteMe, ghi rõ giấy phép CC0 lấy từ trang itch.io, vì file `About.txt` trong gói không ghi.
3. **Gắn vào game:**
   - **Nhạc theo màn và cảnh:** tiêu đề và Trong thành phát `city`; bản đồ phát `map`; đấu võ phát `duel`; sử biến phát `event`, kèm trống nếu biến cố có chiến sự; biện luận dùng nhạc Trong thành.
   - **Âm nền theo mùa và thời tiết** của Trong thành: xuân và hạ là chim gió; đông là gió; ngày có mưa (nếu có thời tiết mưa) là mưa.
   - **Âm giao diện:**
     - bấm nút WPF và lựa chọn trong cảnh: `ui.click`;
     - mở sổ, Nhật Trình: `ui.book`;
     - mua bán thành công: `trade.coin`;
     - Qua ngày: `ui.turn`;
     - đóng ấn hoặc lĩnh mệnh: `ui.seal`.
   - Thêm `SimTBridge.playSfx` và nhận message `sfx` ở host, như giao ước.
4. **Cài đặt:**
   - Trong cửa sổ Cài đặt có sẵn (`DlcSettingsWindow`, mở từ nút "Cài đặt"), thêm khối "Âm thanh": 4 thanh trượt (Tổng, Nhạc, Hiệu ứng, Môi trường) và nút Tắt tiếng.
   - Dùng đúng kiểu control đang có trong cửa sổ đó.
   - Lưu lựa chọn theo người chơi, không theo ván, ở chỗ dự án đang lưu cài đặt máy. Nếu chưa có thì dùng một file JSON trong `%APPDATA%\SimT`.
   - Mức mặc định: nhạc 60%, môi trường 50%, hiệu ứng 80%.
5. **Kiểm tra:**
   - Test C# cho phần có logic: danh mục thiếu khóa thì lặng im; lưu và đọc lại âm lượng; chọn nhạc theo cảnh.
   - `check-scenes.mjs` kiểm: mọi file khai trong `audio.json` đều có thật; `SimTBridge.playSfx` tồn tại.

## Phần B — agy: tiếng chiêu thức tổng hợp và tiếng chợ (nhánh `feat/tq-14b-tieng-chieu`)

1. **Script tổng hợp:** viết `tools/audio/synth.py` (Python và numpy, đều có trên máy). Script sinh tiếng theo tham số, cố định theo seed, và xuất ra `assets/audio/sfx/*.wav`: 22050 Hz, mono, 16-bit. Mỗi tiếng dưới 1,5 giây. Riêng tiếng chợ sinh dạng lặp khoảng 8–12 giây, xuất ra `assets/audio/ambience/market.ogg` (dùng `ffmpeg`).
2. **Tiếng khớp với hiệu ứng pixel** trong `assets/scenes/vendor/pixelfx/src/fx/`. Đọc từng hiệu ứng để khớp độ dài và nhịp:
   - `fx.sword_slash`: tiếng xé gió, kim loại;
   - `fx.thrust`: đâm nhanh;
   - `fx.spear_sweep`: quét rộng;
   - `fx.arrow`: dây cung bật rồi tên rít;
   - `fx.hit_flash`: trúng đòn;
   - `fx.critical_hit`: trúng đòn chí mạng, nặng hơn;
   - `fx.crit_number`: tiếng sáng khi hiện số chí mạng;
   - `duel.start`: trống mở màn;
   - `duel.win`, `duel.lose`: chiêng thắng, trống trầm thua;
   - `debate.push`, `debate.counter`, `debate.win`, `debate.lose`: tiếng gõ mõ hoặc phách, tiếng chuông nhỏ;
   - `event.reveal`: tiếng khánh hoặc chiêng nhẹ khi mở cảnh sử biến.

   Âm sắc theo nhạc cụ cổ: phách, mõ, chiêng, khánh, dây gảy. Không dùng tiếng điện tử hay kiểu chiptune.
3. **Tiếng chợ:** tiếng rì rầm đông người bằng nhiễu lọc, có nhịp lên xuống, xen chuông nhỏ và tiếng bánh xe gỗ. Khóa là `ambience.market`, dùng cho Trong thành.
4. **Gắn vào cảnh:**
   - `duel-scene.js`: gọi `SimTBridge.playSfx?.(...)` đúng lúc mỗi hiệu ứng pixel bắt đầu.
   - `debate-scene.js`: gọi khi thanh thuyết phục đổi và khi kết thúc.
   - `event-scene.js`: gọi `event.reveal` khi mở cảnh.

   Thêm các khóa này vào `assets/audio/audio.json`. Nếu file chưa có thì tạo đúng cấu trúc giao ước; khi gộp, mình sẽ gộp với phần A.
5. **Kiểm tra:**
   - `check-scenes.mjs`: mỗi hiệu ứng pixel có khóa âm tương ứng; mọi file âm tồn tại; độ dài mỗi tiếng dưới 1,5 giây.
   - Xuất một trang nghe thử `D:\Hung DATA\Tool\_tq_shots\14\b\nghe-thu.html` liệt kê mọi tiếng kèm nút phát. Không commit trang này.

## Nghiệm thu mỗi phần

- Build Release sạch.
- `dotnet test SimT.sln -c Release` qua.
- `node tools/check-scenes.mjs` qua.
- `--smoke` chạy được.
- Golden replay vẫn `golden ok`.
- Báo cáo ngắn: danh sách file âm, tổng dung lượng, những gì chưa kiểm được bằng tai.
