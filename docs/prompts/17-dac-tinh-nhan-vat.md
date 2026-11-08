# Prompt 17 — Đợt 5: Đặc tính nhân vật

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" và "Đợt 5".

Worktree đã tạo sẵn, nhánh `feat/tq-05-dac-tinh`. Commit, không push.

Giới hạn:
- File `.cs` mới không quá 400 dòng.
- File cũ dài thì thêm `partial`.
- Không sửa kernel.
- Ảnh QA ghi vào `D:\Hung DATA\Tool\_tq_shots\05\`, không commit.

## Mục tiêu

Mỗi người có cá tính thấy được, và cá tính đổi cách người chơi phải đối xử với họ.

## Đã có, phải dùng lại

- `CharacterArchetype` (4 kiểu) và `NpcPsycheProfile` (`Ambition`, `Integrity`, `Materialism`) trong `src/SimT.Contracts/Playing/SocialContracts.cs`.
- `ISocialNetwork.GetPsycheProfile`, phần tính nằm trong `SocialService.cs`.

Đặc tính là lớp chi tiết hơn nằm trên 4 kiểu này. Không mâu thuẫn với chúng. Ví dụ: Tham tài chỉ có ở người `Materialism` cao, Thanh liêm chỉ có ở người `Integrity` cao.

## Dữ liệu (module `dlc.character`)

1. **Bảng đặc tính.** Đặt một chỗ, gồm 16 đặc tính như spec. Mỗi đặc tính có:
   - mã, tên tiếng Việt, mô tả một câu;
   - điều kiện theo hồ sơ tâm lý;
   - các cặp loại trừ nhau (Tham tài–Thanh liêm, Hào sảng–Keo kiệt, Nóng nảy–Điềm tĩnh, Kiêu ngạo–Khiêm cung).
2. **Gán đặc tính.** Mỗi NPC có 1–3 đặc tính.
   - Khoảng 30 nhân vật sử nổi tiếng có đặc tính cố định, đúng hình ảnh trong truyện. Ví dụ:
     - Quan Vũ: Trung nghĩa, Kiêu ngạo;
     - Trương Phi: Nóng nảy, Mê rượu;
     - Tào Tháo: Đa nghi, Xảo quyệt;
     - Lưu Bị: Nhân hậu;
     - Gia Cát Lượng: Điềm tĩnh, Hiếu học.
   - Người khác sinh theo seed bằng `IRng`, hoặc tính xác định từ id và hồ sơ tâm lý, và thỏa điều kiện.
   - Lưu vào bảng mới, hoặc tính lại khi cần nếu kết quả luôn giống nhau. Chọn cách nào thì ghi lý do.
3. **Capability đọc.** Thêm capability `GetTraits(npcId)` trong `SimT.Contracts` để đợt 9 (biện luận) dùng.

## Tác động

Mỗi tác động đặt số ở một chỗ.

1. **Mua bán** với NPC bán hàng (`INpcMarket`, xem `MarketService.cs` phần NPC stall):
   - Tham tài: bán đắt hơn, mua rẻ hơn.
   - Hào sảng: ngược lại.
   - Keo kiệt: ít hàng.
   - Trọng chữ tín: giá ổn định.
2. **Quà.**
   - Mê rượu: thích rượu.
   - Hiếu học: thích sách và trà.
   - Mê võ: thích binh khí.
   - Thanh liêm: quà đắt bị giảm hiệu quả (coi là hối lộ).
   Cộng hoặc trừ trên hệ số quà đang có, không thay thế.
3. **Lời thoại.** `DialogueService` thêm vài câu chào hoặc đáp theo đặc tính. Mỗi đặc tính ít nhất 2 câu.
4. **Đấu võ.**
   - Nóng nảy: công cao, thủ thấp.
   - Điềm tĩnh: ngược lại.
   - Mê võ: thắng thì thiện cảm cộng thêm.
   Sửa nhẹ, không phá cân bằng hiện tại.

## Hiển thị

- **Nhãn đặc tính.** Hồ sơ Danh sĩ (`dossier-scene.js`) và cảnh trò chuyện (`talk-scene.js`) hiện đặc tính thành nhãn nhỏ; rê chuột thì hiện mô tả.
- **Che theo thiện cảm.** Chưa đủ thân thì một phần hiện "???".
  - Lần đầu gặp: thấy 1 đặc tính.
  - Thân hơn: thấy hết.
  Ngưỡng dùng các bậc thiện cảm đang có.
- **Kích thước.** Không cuộn ngang ở 1076.

## Kiểm tra

Viết test cho:
- đặc tính ổn định theo seed;
- không có cặp loại trừ trên cùng một người;
- nhân vật sử có đúng đặc tính cố định;
- giá mua bán đổi đúng chiều với Tham tài và Hào sảng;
- quà rượu cho người Mê rượu được cộng thêm;
- đặc tính bị che đúng theo thiện cảm.

## Nghiệm thu

- Build Release sạch.
- `dotnet test SimT.sln -c Release` qua hết.
- `node tools/check-scenes.mjs` qua.
- `--smoke` chạy được.
- Có ảnh nhãn đặc tính trong hồ sơ và trong cảnh trò chuyện.
- Báo cáo ngắn, kèm danh sách đặc tính đã gán cố định cho nhân vật sử.
