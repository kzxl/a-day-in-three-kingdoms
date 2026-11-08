# Prompt 23 — Đợt 11: Chân dung nhiều biểu cảm

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" và phần "Đợt 11".

Làm trong worktree đã tạo sẵn, nhánh `feat/tq-11-bieu-cam`.
- Commit trên nhánh này, không push.
- Không sửa kernel.
- Ảnh nháp và file QA để ở `D:\Hung DATA\Tool\_tq_shots\11\`, không commit vào repo.
- Chỉ commit ảnh đã duyệt vào `assets/`.

## Mục tiêu

Nhân vật đổi nét mặt theo lời nói. Thân người, trang phục và khung hình phải giữ nguyên tuyệt đối.

## Vẽ (dùng công cụ tạo ảnh của bạn)

1. **Đối tượng:** mọi thư mục trong `assets/scenes/art/portraits/`, gồm nhân vật sử (cao_cao, guan_yu, liu_bei, lu_bu, zhang_fei, zhuge_liang) và các kiểu dân (commoner, elder, farmer_woman, general, lady, merchant, scholar, swordswoman, player).
2. **Biểu cảm:** với mỗi `full.webp`, làm thêm 3 bản `happy`, `angry`, `sad`.
3. **Cách làm để thân không đổi:**
   - Sửa ảnh, dùng chính `full.webp` làm ảnh vào, chỉ yêu cầu đổi nét mặt.
   - Sau đó ghép bằng script: lấy vùng đầu của ảnh mới (khung chữ nhật quanh đầu, mép mờ vài pixel), dán lên đúng vị trí trên `full.webp` gốc.
   - Kết quả: ngoài vùng đầu, ảnh giống hệt bản gốc từng pixel.
   - Vùng đầu xác định một lần cho mỗi nhân vật. Ghi tọa độ vào một file JSON để làm lại được.
4. **Kiểm tra từng ảnh:**
   - Đo khác biệt pixel ngoài vùng đầu phải bằng 0.
   - Xem bằng mắt: đúng người, đúng tuổi, không lệch nét vẽ.
   - Nét mặt rõ nhưng không lố. Đây là tranh nhân vật nghiêm túc, không theo kiểu chibi.
   - Ảnh nào không đạt thì vẽ lại. Ghi lại ảnh đã vẽ lại và lý do.
5. **Lưu:** `assets/scenes/art/portraits/<id>/full_happy.webp`, `full_angry.webp`, `full_sad.webp`. Cùng kích thước và định dạng với `full.webp`. Giữ dung lượng gần bằng bản gốc.

## Dữ liệu và cảnh

1. **Manifest:** `manifest.json` thêm biểu cảm cho từng chân dung. `SceneArtCatalog` trả về đường dẫn biểu cảm. Thiếu biểu cảm nào thì dùng `full.webp`.
2. **Gắn biểu cảm cho lời thoại:**
   - Trong `TalkScene`, mỗi dòng có thể kèm biểu cảm. Thêm trường danh sách song song với `Lines`, mặc định để trống.
   - `DialogueService` đặt biểu cảm theo ngữ cảnh:
     - thiện cảm tăng, quà hợp ý: `happy`;
     - bị từ chối, bị khiêu khích, quà bị chê: `angry`;
     - tin dữ, ước nguyện thất bại: `sad`.
     Đặt ở chỗ đã có sẵn nhánh logic, không cần phủ mọi câu.
   - Cảnh sử biến (`event-scene.js`): mỗi biến cố có thể khai biểu cảm cho từng nhân vật. Đổi dữ liệu bằng migration mới, không sửa migration cũ.
3. **Cảnh trò chuyện (`talk-scene.js`):**
   - Chuyển dòng thì đổi ảnh `tk-char-base`, chuyển mờ ngắn khoảng 150ms.
   - Hoạt ảnh chớp mắt và nói (`blinkPath`, `talkPath`) chỉ chạy khi đang ở biểu cảm thường. Đổi sang biểu cảm khác thì tạm tắt, tránh ghép lệch.

## Kiểm tra

- Test C#: thiếu biểu cảm thì dùng `full.webp`; `DialogueService` gắn đúng biểu cảm cho câu tặng quà hợp ý và cho câu bị từ chối.
- `check-scenes.mjs`: mọi file biểu cảm khai trong manifest đều tồn tại.

## Nghiệm thu

- Build Release sạch, `dotnet test SimT.sln -c Release` qua, `node tools/check-scenes.mjs` qua, `--smoke` chạy.
- Ảnh tổng hợp: một lưới mỗi nhân vật × 4 biểu cảm (thường, vui, giận, buồn).
- Chụp cảnh trò chuyện ở 3 nét mặt.
- Báo cáo ngắn, kèm số pixel khác ngoài vùng đầu của từng ảnh. Số này phải bằng 0.
