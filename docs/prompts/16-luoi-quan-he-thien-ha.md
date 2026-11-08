# Prompt 16 — Đợt 4: Lưới quan hệ thiên hạ

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" và "Đợt 4".

Worktree đã tạo sẵn, nhánh `feat/tq-04-quan-he`. Commit trên nhánh này, không push.

Giới hạn khi viết code:
- File `.cs` mới không quá 400 dòng.
- `SocialService.cs` đã gần 1800 dòng: thêm `partial` file mới hoặc service riêng trong `src/SimT.Modules.Dlc.Character/Social/`, không viết thêm vào file cũ.

## Mục tiêu

NPC có quan hệ với nhau, không chỉ với người chơi. Quan hệ đó có tác động lên người chơi.

## Dữ liệu (module `dlc.character`)

1. **Bảng `kz_npc_bond`.** Migration mới, chạy lại được. Khai báo trong `module.json`. Cột:
   - `npc_a`, `npc_b`, `kind`, `strength` 0–100;
   - `since_date`;
   - `source`: `history` hoặc `sim`.
   Mỗi cặp chỉ có một quan hệ cho mỗi loại. Lưu theo `npc_a < npc_b`, trừ các loại có hướng.
2. **Loại quan hệ.**
   - Có hướng: `parent` (a là cha/mẹ của b), `lord` (a là chủ của b), `mentor` (a là thầy của b).
   - Không hướng: `sworn` (kết nghĩa), `spouse` (vợ chồng), `sibling` (huynh đệ), `friend` (bạn), `rival` (thù).
3. **Dữ liệu sử.**
   - Chỉ cho NPC có trong danh sách 300. Dữ liệu NPC nằm ở `src/SimT.Modules.Base.Npc/migrations/0101-*.sql` và `0103-*.sql`; dò mã ở đó.
   - Ví dụ: Lưu Bị–Quan Vũ–Trương Phi `sworn`; Tôn Sách–Chu Du `sworn`; Tào Tháo–Hạ Hầu Đôn `sibling` (họ hàng); Lã Bố và Đinh Nguyên/Đổng Trác `lord` rồi `rival`; Lưu Bị–Gia Cát Lượng `lord`; Tư Mã Huy–Gia Cát Lượng `mentor`.
   - Làm ít nhất 40 quan hệ đúng sử. Không chắc thì bỏ, không bịa.
4. **Quan hệ sinh theo seed.**
   - Các NPC không có dữ liệu sử được sinh 1–3 quan hệ khi tạo ván hoặc khi mở save cũ lần đầu.
   - Chủ yếu là `friend` và `rival` với người cùng thành hoặc cùng nghề. Dùng `IRng` để kết quả xác định theo seed.
5. **Mô phỏng hằng tháng** (`MonthClosed`):
   - Hai NPC cùng thành có thể thành bạn. Quan hệ yếu có thể phai.
   - Số quan hệ mới hoặc mất mỗi tháng có giới hạn để không tràn biên niên.
   - Ghi biên niên mục quan hệ qua `ISocietyChronicle` nếu có capability. Không có capability thì vẫn chạy.
6. **Capability đọc.** Thêm một capability đọc quan hệ trong `SimT.Contracts`, để các đợt sau dùng (đợt 6 ước nguyện "báo thù").

## Cảnh Nhân Duyên (`assets/scenes/social-scene.js`)

1. **Chế độ "Thiên hạ".** Thêm nút chuyển giữa "Của tôi" (vòng hiện tại) và "Thiên hạ".
2. **Mạng quan hệ 2 bậc.** Ở chế độ Thiên hạ:
   - Chọn một NPC thì người đó ở giữa, quan hệ bậc 1 xếp quanh, bậc 2 xếp ngoài.
   - Tối đa khoảng 24 nút. Quá thì ưu tiên quan hệ mạnh.
   - Đường nối màu theo loại, có chú thích màu. `rival` dùng nét đứt.
   - Nút là chân dung tròn có tên.
   - Bấm một nút thì chuyển tâm sang người đó, có hiệu ứng chuyển ngắn.
   - Có ô tìm theo tên.
3. **Payload.** Dữ liệu mạng đưa qua payload `SocialScene` (thêm trường có mặc định) hoặc qua message yêu cầu theo NPC. Chọn cách nào nhẹ hơn, không gửi cả 300 người mỗi lần mở cảnh.
4. **Kích thước.** Không cuộn ngang ở 1076.

## Tác động lên người chơi

- **Được giới thiệu.** Người chơi có thiện cảm cao với A (ngưỡng ở hằng số). Khi gặp lần đầu một người B là `friend`, `sworn` hoặc `sibling` của A:
  - thiện cảm khởi điểm của B cộng thêm;
  - cảnh trò chuyện có dòng "Bạn của {A}".
- **Kết thân với kẻ thù của bạn thân.** Người chơi tăng thiện cảm với kẻ `rival` của một người thân với mình:
  - người thân đó giảm thiện cảm theo tỉ lệ nhỏ;
  - có dòng báo.
- Số cộng trừ đặt thành hằng số một chỗ.

## Kiểm tra

Viết test cho:
- dữ liệu sử có mặt và đúng loại (Lưu–Quan–Trương `sworn`);
- quan hệ sinh theo seed xác định;
- save cũ mở lên có quan hệ;
- giới thiệu cộng thiện cảm đúng;
- kết thân với kẻ thù bị trừ thiện cảm;
- tắt `dlc.society` thì mô phỏng tháng vẫn chạy.

## Nghiệm thu

- Build Release sạch, `dotnet test SimT.sln -c Release` qua hết, `node tools/check-scenes.mjs`, `--smoke`.
- Chụp chế độ Thiên hạ với tâm là Lưu Bị và với tâm là một NPC thường. Lưu ảnh vào `D:\Hung DATA\Tool\_tq_shots\04\`.
- Báo cáo ngắn, kèm danh sách các quan hệ sử đã thêm.
