# Prompt 20 — Đợt 8: Người đi trên bản đồ

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" và "Đợt 8".

Worktree đã tạo sẵn, nhánh `feat/tq-08-nguoi-di`. Nhánh đã có đợt 1 (lớp thế lực trên bản đồ) và đợt 3 (sử biến, có `route_danger` và `route_block`).

Ràng buộc:
- Commit trên nhánh này, không push.
- File `.cs` mới không quá 400 dòng.
- Không sửa kernel.
- Ảnh QA ghi vào `D:\Hung DATA\Tool\_tq_shots\08\`, không commit vào repo.

## Mục tiêu

Bản đồ sống: thấy người trong thiên hạ đi lại, và thấy đường nào đang nguy hiểm.

## Dữ liệu

1. **`MapScene` thêm `travellers`.** Thêm trường mặc định rỗng. Mỗi mục gồm:
   - `id`, `kind` (`rival` | `caravan` | `npc` | `army`);
   - `labelVi`, `fromId`, `toId`;
   - `progress` (0–1), `color`, có thể lấy theo thế lực.

   Nguồn dữ liệu, chỉ lấy dữ liệu thật:
   - **`rival`:** thương nhân đối thủ đang đi (`MerchantRivalDef`: `CurrentCitadelId` → `TargetCitadelId`). Tiến độ tính từ dữ liệu sẵn có. Nếu không có ngày đi thì dùng một giá trị xác định theo id và ngày, không ngẫu nhiên mỗi lần mở.
   - **`caravan`:** thương đoàn của người chơi đang chạy (`ICaravanGuild`, `CaravanRunSummary`).
   - **`npc`:** danh sĩ đang đi xa, tức nơi ở khác quê. Lấy tối đa 8 người nổi tiếng nhất.
   - **`army`:** quân của sử biến đang diễn ra quanh các thành liên quan (đợt 3). Không có thì bỏ.

   Tổng cộng tối đa khoảng 30 mục.

2. **Tuyến đường thêm trạng thái.** `MapRouteView` thêm `danger` (0–3) và `blockedReasonVi`. Lấy từ:
   - sử biến (đợt 3);
   - thổ phỉ;
   - mức nguy hiểm thành ở hai đầu.

3. **Báo giá đi đường** (`QuoteTravel` và `WorldMapCommandHandler`):
   - Đi qua tuyến bị chặn: không cho đi, trả lý do.
   - Tìm đường (Dijkstra hiện có) phải tránh tuyến bị chặn nếu còn đường khác.
   - Đi qua tuyến nguy hiểm thì phần báo giá ghi rõ mức nguy hiểm.
   - Không đổi công thức giá hay số ngày nếu không cần.

## Cảnh bản đồ (`map-scene.js`)

- **Người đi.** Biểu tượng nhỏ di chuyển chậm dọc tuyến đường, tiến dần quanh vị trí `progress`. Dùng một `requestAnimationFrame` chung với lớp hạt mùa của đợt 1. Dừng khi cảnh ẩn hoặc `prefers-reduced-motion`.
  - Dùng SVG hoặc canvas, mỗi loại một hình riêng: xe hàng, ngựa, cờ quân.
  - Rê chuột hoặc chạm thì hiện tên và lộ trình.
- **Tuyến nguy hiểm.** Kẻ đỏ, đậm theo mức. Tuyến bị chặn dùng nét đứt đỏ, có biểu tượng chặn ở giữa.
- **Chú thích và nút tắt.** Thêm dòng chú thích loại người đi vào khối chú thích có sẵn, và nút bật/tắt "Người đi".
- **Kích thước.** Không cuộn ngang ở 1076. Không che panel thành hay nút Lên đường.
- **Hiệu năng.** Đo lại thời gian mở bản đồ, không được chậm hơn quá 30 ms.

## Kiểm tra

Viết test cho:
- payload có thương nhân đối thủ thật;
- tiến độ xác định, mở hai lần cùng ngày cho cùng kết quả;
- tuyến bị chặn: không đi được, và Dijkstra đi vòng;
- tắt `dlc.society` thì `travellers` rỗng và không lỗi.

`check-scenes.mjs` kiểm thêm: người đi được vẽ, và chú thích không đè.

## Nghiệm thu

- Build Release sạch.
- `dotnet test SimT.sln -c Release` qua hết.
- `node tools/check-scenes.mjs` qua.
- `--smoke` chạy được.
- Ảnh: bản đồ có người đi, và một tuyến bị chặn có báo lỗi khi chọn đi qua.
- Báo cáo ngắn kèm số đo hiệu năng.
