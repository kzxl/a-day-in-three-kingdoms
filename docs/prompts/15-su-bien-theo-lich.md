# Prompt 15 — Đợt 3: Sử biến theo lịch

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" và "Đợt 3".

Worktree đã tạo sẵn, nhánh `feat/tq-03-su-bien`. Nhánh này đã có đợt 1 (thế lực trên bản đồ) và đợt 2 (tin đồn). Commit, không push. File `.cs` mới không quá 400 dòng. File cũ dài thì thêm `partial`.

## Mục tiêu

Thiên hạ chuyển động theo sử. Người chơi sống giữa các biến cố và chịu tác động của chúng.

## Dữ liệu

1. **Danh sách biến cố.** Khoảng 12 biến cố năm 184–220, đúng như danh sách trong spec.
   - Mỗi biến cố có: mã, ngày (năm, tháng, ngày), tiêu đề, lời kể 3–6 câu, các thành liên quan, nhân vật liên quan (mã NPC có trong danh sách 300; không có thì bỏ qua), và danh sách tác động.
   - Đặt dữ liệu theo cách dự án đang làm: dữ liệu NPC đang là migration SQL seed của module. Chọn SQL seed hay file trong `assets/config`, và ghi lý do trong báo cáo.
   - Viết lời kể theo sử, ngắn gọn, giọng như biên niên.
2. **Tác động.** Mỗi tác động là một trong các loại sau:
   - `price_shock`: thành, mặt hàng hoặc nhóm, mức, số ngày. Dùng `MarketShockEngine.TriggerShock`.
   - `route_danger`: tuyến, mức nguy hiểm, số ngày. `route_block` là đường không đi được.
   - `city_owner`: thành chuyển sang thế lực khác. Đi qua dữ liệu chính trị, để bản đồ của đợt 1 tự đổi màu.
   - `npc_move` và `npc_allegiance`: NPC đổi nơi ở hoặc đổi phe.
   - `npc_death`: chỉ dùng khi nhân vật chết trong sử, ví dụ Đổng Trác năm 192. NPC đó biến khỏi các cảnh, nhưng hồ sơ ghi "Đã mất". Không xoá dòng dữ liệu.
   Tác động nào cần capability mà DLC không bật thì bỏ qua, không lỗi.
3. **Lưu trạng thái.** Mỗi biến cố có một bảng trạng thái của module sở hữu: chờ / đã xảy ra / đã xem. Nên đặt ở `dlc.society` cạnh biên niên.

## Luật

- Mỗi ngày qua (`DayAdvanced`), biến cố đến ngày thì áp dụng đúng một lần, kể cả khi người chơi qua nhiều ngày một lúc. Ghi vào biên niên với `EventKind = "history"` và thêm bộ lọc "Sử biến".
- Ván bắt đầu sau ngày của biến cố: chỉ áp dụng trạng thái cuối, gồm chủ thành, NPC chết, NPC đổi phe. Không chiếu cảnh, không tạo cú sốc giá.
- Save cũ: khi mở lần đầu sau bản này, áp dụng giống ván mới theo ngày hiện tại của save.
- Tin đồn: cắm biến cố vào nguồn tin `event` mà đợt 2 đã để sẵn. Khoảng 30 ngày trước biến cố, tửu quán ở các thành liên quan có thể có tin báo trước.

## Cảnh sử biến (`event`)

- **Khi nào hiện.** Biến cố xảy ra khi người chơi ở một thành liên quan thì mở cảnh. Ở thành khác thì chỉ có một dòng tin trên thanh trạng thái và biên niên.
- **Giao diện.** Cảnh WebView2 mới `assets/scenes/event-scene.js` và `.css`, đăng ký như các cảnh khác (`bridge.js`, `SceneOverlayControl`, `check-scenes.mjs`). Gồm:
  - tranh nền theo loại biến cố: dùng nền có sẵn (`gate`, `hall`, `field`…) qua `SceneArtCatalog`;
  - năm và niên hiệu (`GameDateCodec.EraVi`), tiêu đề lớn;
  - chân dung lớn (`full.webp`) của tối đa 2 nhân vật liên quan;
  - lời kể hiện dần từng câu, bấm để hiện nhanh.
- **Lựa chọn.** 1–2 lựa chọn tùy biến cố, có tác động nhỏ và thật. Ví dụ:
  - quyên lương: trừ hàng, cộng danh vọng;
  - rời thành ngay: đi sang thành kề, không tốn ngày;
  - tích trữ hàng chiến: mua trước giá tăng.
  Luôn có "Tiếp tục".
- Biến cố đã xem có thể xem lại từ biên niên.

## Kiểm tra

Viết test cho các trường hợp sau:
- biến cố áp dụng đúng ngày, đúng một lần, kể cả khi bỏ qua nhiều ngày;
- ván bắt đầu sau biến cố chỉ có trạng thái cuối;
- save cũ mở lên áp dụng đúng;
- `city_owner` đổi màu thành trên payload bản đồ;
- `npc_death` làm NPC không còn trong `BuildCityNpcs`;
- tin đồn báo trước có xuất hiện.

## Nghiệm thu

- Build Release sạch, `dotnet test SimT.sln -c Release` qua hết, `node tools/check-scenes.mjs`, `--smoke`.
- Chụp một cảnh sử biến, ví dụ Đổng Trác vào Lạc Dương, và biên niên mục Sử biến. Lưu ảnh vào `D:\Hung DATA\Tool\_tq_shots\03\`.
- Báo cáo ngắn kèm danh sách 12 biến cố và tác động của từng cái.
