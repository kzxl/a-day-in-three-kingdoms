# Prompt 26 — Chân dung riêng cho nhân vật sử, và biến thể cho chân dung dùng chung

Đọc trước:
- Luật chung trong `docs/spec/nang-cap-tam-quoc.md`.
- `docs/prompts/23-chan-dung-bieu-cam.md`: dùng chung cách vẽ và cách soát ảnh.

Quy ước:
- Mỗi phần làm trong worktree riêng đã tạo sẵn. Commit, không push.
- Ảnh nháp và ảnh QA để ở `D:\Hung DATA\Tool\_tq_shots\13\`, không commit.

## Vấn đề

300 NPC nhưng chỉ có 6 chân dung riêng: Lưu Bị, Quan Vũ, Trương Phi, Tào Tháo, Gia Cát Lượng, Lữ Bố. Phần còn lại dùng chung 8 chân dung theo kiểu người, nên Triệu Vân, Chu Du, Tôn Quyền, Viên Thiệu… cùng mang một mặt "tướng". Võ tướng lục xếp 300 người cạnh nhau, nên chỗ trùng lộ ra hết.

## Định dạng (giống hệt chân dung hiện có)

- `full.webp`: toàn thân, đứng, nền trong suốt RGBA, cao đúng 900px, rộng 520–640px, chân chạm đáy khung.
- `face.webp`: 160×160 RGB, cắt đầu và vai từ chính `full` của người đó, nền tối giống các `face.webp` đang có.
- **Phong cách:** cùng nét vẽ, ánh sáng, độ đậm viền, tỉ lệ cơ thể với `assets/scenes/art/portraits/guan_yu`, `zhuge_liang`, `cao_cao`. Đặt chúng cạnh nhau phải thấy như một bộ. Mở các ảnh đó ra xem trước khi vẽ.
- **Đúng hình tượng Tam Quốc đã quen:**
  - Triệu Vân: áo giáp bạc trắng, thương.
  - Chu Du: trẻ, nho nhã, giáp đỏ.
  - Hoàng Trung: lão tướng tóc bạc, cung.
  - Điển Vi: song kích.
  - Hạ Hầu Đôn: che một mắt.
  - Mã Siêu: giáp bạc, mũ sư tử.
  - Quách Gia: thư sinh gầy, quạt.
  - Hoa Đà: thầy thuốc.
  - Trương Giác: đạo sĩ khăn vàng.
  - Kiều, Tôn Thượng Hương: tiểu thư, Tôn Thượng Hương mang cung.

  Không nhân vật nào được giống một người đã có.
- **Tách nền:** vẽ trên nền phẳng, tách bằng script. Không còn viền màu nền: không có pixel nào alpha > 0 mà gần màu nền.
- **Lưu:** WebP lossy chất lượng 92 (`exact=True`), dung lượng gần bằng các file đang có.

## Phần A — Codex: 20 nhân vật (nhánh `feat/tq-13a-chan-dung`)

| Mã | Tên | Mã | Tên |
| --- | --- | --- | --- |
| npc_0024 | Triệu Vân | npc_0028 | Hoàng Trung |
| npc_0017 | Chu Du | npc_0026 | Bàng Thống |
| npc_0019 | Tôn Quyền | npc_0037 | Quách Gia |
| npc_0048 | Tôn Sách | npc_0035 | Tuân Úc |
| npc_0014 | Tôn Kiên | npc_0040 | Trương Liêu |
| npc_0039 | Tư Mã Ý | npc_0043 | Hạ Hầu Đôn |
| npc_0018 | Viên Thiệu | npc_0042 | Điển Vi |
| npc_0023 | Đổng Trác | npc_0041 | Hứa Chử |
| npc_0029 | Mã Siêu | npc_0051 | Lục Tốn |
| npc_0050 | Lữ Mông | npc_0053 | Cam Ninh |

## Phần B — Codex: 20 nhân vật (nhánh `feat/tq-13b-chan-dung`, chạy sau phần A)

| Mã | Tên | Mã | Tên |
| --- | --- | --- | --- |
| npc_0054 | Thái Sử Từ | npc_0030 | Ngụy Diên |
| npc_0049 | Lỗ Túc | npc_0031 | Khương Duy |
| npc_0052 | Hoàng Cái | npc_0058 | Trần Cung |
| npc_0038 | Giả Hủ | npc_0059 | Viên Thuật |
| npc_0036 | Tuân Du | npc_0060 | Công Tôn Toản |
| npc_0044 | Hạ Hầu Uyên | npc_0061 | Hoa Đà |
| npc_0045 | Tào Nhân | npc_0004 | Trương Giác |
| npc_0046 | Trương Cáp | npc_0073 | Tào Phi |
| npc_0047 | Từ Hoảng | npc_0087 | Tôn Thượng Hương |
| npc_0088 | Đại Kiều | npc_0089 | Tiểu Kiều |

**Cách làm chung cho phần A và B:**
- Mỗi người một thư mục `assets/scenes/art/portraits/<khóa>/` (khóa là tên Latin không dấu, ví dụ `zhao_yun`), có `full.webp` và `face.webp`.
- Thêm vào mục `npc` của `manifest.json` theo mã. Sprite trên phố giữ theo kiểu người như hiện tại (`general`, `scholar`…).
- Không vẽ biểu cảm cho người mới. Cảnh trò chuyện tự dùng `full.webp` khi thiếu biểu cảm.

## Phần C — agy: biến thể cho chân dung dùng chung (nhánh `feat/tq-13c-bien-the`)

**Ảnh.** Với mỗi kiểu `general`, `scholar`, `merchant`, `elder`, `commoner`, `lady`, `farmer_woman`, `swordswoman`, vẽ thêm 2 biến thể vào `portraits/<kiểu>_v2/` và `portraits/<kiểu>_v3/`:
- cùng kiểu người, cùng phong cách và khung như bản gốc;
- khác người rõ ràng: tuổi, râu tóc, khuôn mặt, màu áo, mũ;
- định dạng như mục "Định dạng" ở trên.

**Code.** `SceneArtCatalog.ResolveArchetypeKeys` (`src/SimT.Contracts/Scenes/SceneArtCatalog.cs`):
- NPC không có chân dung riêng và không có ảnh profile thì chọn bản gốc, `_v2` hoặc `_v3` theo mã NPC: `số trong mã % 3`. Kết quả phải ổn định, không ngẫu nhiên.
- Người chơi (`player`) giữ nguyên.
- Khai báo các biến thể trong `manifest.json`. Thiếu biến thể nào thì về bản gốc.

**Kiểm tra.**
- Test C#:
  - cùng mã luôn ra cùng ảnh;
  - ba NPC cùng kiểu với mã kề nhau ra ba ảnh khác nhau;
  - nhân vật có chân dung riêng không bị đổi.
- `check-scenes.mjs`:
  - mọi đường dẫn khai trong manifest đều có file thật;
  - kích thước `full` cao 900px, `face` 160×160.

## Nghiệm thu mỗi phần

- Build Release sạch, `dotnet test SimT.sln -c Release` qua, `node tools/check-scenes.mjs` qua, `--smoke` chạy được.
- Ảnh lưới tổng hợp: mỗi người mới đứng cạnh 2 chân dung cũ để so phong cách; có lưới `face`.
- Báo cáo ngắn: danh sách ảnh, ảnh nào phải vẽ lại và vì sao.
