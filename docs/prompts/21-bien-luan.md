# Prompt 21 — Đợt 9: Biện luận

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" (nhất là luật Giao diện) và "Đợt 9".

Làm trong worktree đã tạo sẵn, nhánh `feat/tq-09-bien-luan`. Nhánh đã có đặc tính nhân vật của đợt 5, dùng qua capability `GetTraits`.

Ràng buộc:
- Commit, không push.
- File `.cs` mới không quá 400 dòng.
- Không sửa kernel, không sửa migration cũ.
- Ảnh QA ghi vào `D:\Hung DATA\Tool\_tq_shots\09\`, không commit.

## Mục tiêu

Có một cuộc đấu trí khác hẳn đấu võ, dùng cho buôn bán và quan trường. Không được là oẳn tù tì đổi tên.

## Luật (module `dlc.character`, phần Dialogue)

1. **Thanh thuyết phục** 0–100, bắt đầu ở 50. Đạt 80 trở lên là thắng, 20 trở xuống là thua. Hết 5 lượt mà chưa ngã ngũ thì tính theo vị trí thanh: từ 50 trở lên là thắng nhỏ, dưới 50 là thua nhỏ.
2. **Mỗi lượt** người chơi được rút 3 lý lẽ từ bốn loại, rút bằng `IRng`:

   | Loại | Dựa vào | Ghi chú |
   | --- | --- | --- |
   | Lý | INT | |
   | Tình | DIP | |
   | Lợi | Tiền hoặc hàng | Trừ thật khi dùng. Số tiền ghi rõ trên lý lẽ. |
   | Uy | Danh vọng | |

   Mỗi lý lẽ có một câu nói cụ thể, sinh từ mẫu theo ngữ cảnh của cuộc biện luận. Ví dụ ngữ cảnh mặc cả: "Lụa Thục năm nay được mùa, giá ấy đã quá cao."
3. **Sức đẩy** của một lý lẽ = chỉ số của người chơi × hệ số theo đặc tính của đối phương. Ví dụ bảng hệ số (đặt một chỗ, dễ chỉnh):

   | Đặc tính | Lợi | Tình | Lý | Uy |
   | --- | --- | --- | --- | --- |
   | Tham tài | ×1.5 | | | |
   | Thanh liêm | ×0.3, và làm đối phương phật ý | | | |
   | Trung nghĩa | ×0.5 | ×1.4 | | |
   | Đa nghi | | ×0.6 | ×1.4 | |
   | Kiêu ngạo | | | | ×0.4 |
   | Khiêm cung | | | | ×1.3 |
   | Nóng nảy | | | | phản tác dụng 50% |

   Ô trống là ×1. Đặc tính đã lộ thì người chơi thấy, nên đọc được điểm yếu. Đặc tính còn "???" thì phải đoán.
4. **Đối phương đáp lại** mỗi lượt bằng một câu, và có thể phản công kéo thanh về phía mình. Mức phản công theo chỉ số INT hoặc DIP của đối phương.
5. **Nhượng bộ:** người chơi có thể nhượng bộ bất cứ lúc nào. Kết thúc ngay với kết quả hòa nhẹ, không mất gì thêm.
6. **Kết quả xác định theo seed.** Cùng save và cùng lựa chọn thì ra cùng kết quả.

## Dùng ở đâu

Làm cả ba chỗ:

| Chỗ dùng | Thắng | Thua |
| --- | --- | --- |
| **Mặc cả lô hàng** với thương nhân NPC, từ 20 đơn vị trở lên | Giảm giá 5–15% theo mức thắng | Tăng giá 5% |
| **Thuyết phục làm gia thần** khi chưa đủ thiện cảm | Chiêu mộ được với phí giảm | Thiện cảm giảm nhẹ |
| **Xin quan phủ giảm thuế** ở thành đang ở, nếu có dữ liệu thuế của `CityGovernanceView` | Giảm thuế người chơi trong 30 ngày | Mất danh vọng nhỏ |

Mỗi NPC chỉ biện luận được 1 lần mỗi 7 ngày cho mỗi loại.

## Cảnh `debate`

Thêm `assets/scenes/debate-scene.js` và một file CSS nhỏ. Đăng ký cảnh ở `bridge.js`, `index.html`, `SceneOverlayControl` và `check-scenes.mjs`.

**Dựng trên khung cảnh trò chuyện, giống cách `event-scene.js` đang làm.** Dùng lại `tk-talk-stage`, `tk-loc-badge`, `tk-dialog-paper` có `tk-seal`, `tk-choice`.

- **Đối phương:** đứng toàn thân như cảnh trò chuyện. Đặc tính đã lộ hiện bằng `tk-mood-pill` có sẵn.
- **Thẻ giấy:** hiện câu nói mới nhất (của người chơi hoặc đối phương). Ngay dưới là thanh thuyết phục, kẻ mảnh trên nền giấy:
  - phần nghiêng về người chơi màu `--jade`, phần nghiêng về đối phương màu `--vermilion`;
  - vạch mốc ở 20 và 80;
  - số lượt "Lượt 2 / 5".
- **Ba lý lẽ:** là ba `tk-choice` đánh số 壹 貳 參. Nhãn là câu nói. Tag bên phải ghi loại và chi phí, ví dụ "Lợi · 40 quan". Thêm một `tk-choice` "Nhượng bộ".
- **Kết thúc:** thẻ giấy ghi kết quả và hệ quả. Lựa chọn duy nhất là "Rời đi".
- **Cấm:** không emoji, không gradient tự chế, không thẻ bài lật. Đối chiếu ảnh với cảnh trò chuyện trước khi nộp.

## Kiểm tra

Viết test cho:
- kết quả xác định theo seed;
- hệ số đặc tính đổi sức đẩy đúng chiều: Tham tài gặp Lợi mạnh hơn, Thanh liêm gặp Lợi bị phạt;
- Lợi trừ tiền thật;
- thắng mặc cả thì giá giảm đúng;
- giới hạn 7 ngày;
- nhượng bộ không mất thêm gì.

## Nghiệm thu

- Build Release sạch, `dotnet test SimT.sln -c Release`, `node tools/check-scenes.mjs`, `--smoke`.
- Ảnh cảnh biện luận ở lượt giữa và lúc kết thúc, kích thước 840 và 1440.
- Báo cáo ngắn.
