# Prompt 24 — Đợt 12: Võ tướng lục

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" (nhất là luật Giao diện) và "Đợt 12".

Làm trong worktree đã tạo sẵn, nhánh `feat/tq-12-vo-tuong-luc`. Nhánh đã có sử biến (đợt 3) và đặc tính (đợt 5).

Ràng buộc:
- Commit, không push.
- File `.cs` mới không quá 400 dòng.
- Không sửa kernel, không sửa migration cũ.
- Ảnh QA ghi vào `D:\Hung DATA\Tool\_tq_shots\12\`, không commit.

## Mục tiêu

Có một sổ sưu tầm, để người chơi có lý do đi gặp người và chứng kiến sử biến.

## Dữ liệu (module `dlc.character`)

1. **Bảng "đã gặp".** Migration mới, gồm: npc, ngày gặp lần đầu, thành gặp.
   - Ghi khi người chơi trò chuyện lần đầu, hoặc khi có lần tương tác đầu tiên qua `SocialService`, `DialogueService`.
   - Save cũ: suy ra từ quan hệ đã có thiện cảm khác mặc định, hoặc có ký ức (`GetMemories`). Ngày gặp là ngày sớm nhất tìm được.
2. **Sử biến đã chứng kiến.** Lấy từ trạng thái của đợt 3: biến cố có cảnh đã xem (`viewed`) khi người chơi có mặt. Không lưu lại lần nữa.
3. **Mốc sưu tầm.** 10, 50, 100 người, và đủ mọi sử biến.
   - Mỗi mốc cho danh vọng một lần duy nhất và ghi biên niên. Dùng `event_kind` đã có, không dựng lại bảng biên niên.
   - Lưu mốc đã nhận để không cho lại.

## Giao diện

Sổ mở từ hồ sơ Danh sĩ (`dossier-scene.js`), qua một tab hoặc nút "Võ tướng lục" theo đúng kiểu tab đang có ở đó.

- **Tab Nhân vật**
  - Lưới chân dung mặt (`face.webp`). Người chưa gặp hiện bóng đen của chính chân dung (CSS `filter: brightness(0)`) kèm "?".
  - Bộ lọc theo thế lực, thành, loại người. Dùng kiểu nút lọc đang có trong các cảnh.
  - Tỉ lệ "Đã gặp 37 / 300".
  - Bấm vào người đã gặp thì mở hồ sơ người đó.
- **Tab Sử biến**
  - Danh sách các biến cố theo năm, theo kiểu dòng của biên niên hoặc danh sách trong dossier.
  - Đã chứng kiến: bấm để xem lại cảnh, dùng chức năng xem lại của đợt 3.
  - Đã xảy ra nhưng lỡ mất: hiện mờ, ghi "Không có mặt".
  - Chưa xảy ra: hiện "???" và năm.
- Không emoji, không gradient tự chế, không pill trang trí. Đối chiếu ảnh với hồ sơ Danh sĩ hiện có trước khi nộp.
- Không cuộn ngang ở 1076. Lưới 300 người phải cuộn mượt: lazy-load ảnh, không tải cả 300 ảnh một lúc.

## Kiểm tra

Viết test cho:
- trò chuyện lần đầu thì được ghi "đã gặp";
- save cũ suy ra đúng;
- mỗi mốc chỉ thưởng một lần;
- sử biến lỡ mất được phân biệt với sử biến đã chứng kiến;
- tắt `dlc.society` thì tab Sử biến ẩn và không lỗi.

## Nghiệm thu

- Build Release sạch.
- `dotnet test SimT.sln -c Release` qua.
- `node tools/check-scenes.mjs` qua.
- `--smoke` chạy được.
- Ảnh hai tab ở cỡ 840 và 1440.
- Báo cáo ngắn.
