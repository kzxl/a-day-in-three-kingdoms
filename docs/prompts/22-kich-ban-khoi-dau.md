# Prompt 22 — Đợt 10: Kịch bản khởi đầu

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`: phần "Luật chung" (nhất là luật Giao diện) và phần "Đợt 10".

Worktree đã tạo sẵn, nhánh `feat/tq-10-kich-ban`. Nhánh đã có sử biến (đợt 3) và thế lực trên bản đồ (đợt 1).

Ràng buộc:
- Commit, không push.
- File `.cs` mới không quá 400 dòng.
- Không sửa kernel, không sửa migration cũ.
- Ảnh QA để ở `D:\Hung DATA\Tool\_tq_shots\10\`, không commit.

## Mục tiêu

Người chơi chọn thời điểm bước vào thiên hạ, và thế cục lúc đó phải đúng sử.

Hiện có lỗi cần sửa: năm 184 mà bản đồ đã hiện "Tào Ngụy", "Thục Hán", "Đông Ngô".

## Tận dụng cái đã có

Đợt 3 đã làm được việc này: ván bắt đầu sau ngày của một sử biến thì chỉ áp dụng trạng thái cuối của sử biến đó (chủ thành, người mất, đổi phe, đổi nơi ở).

Vì vậy một kịch bản chủ yếu gồm:
1. ngày bắt đầu;
2. trạng thái nền năm 184;
3. phần sử biến của đợt 3 tự áp dụng những gì đã qua.

Chỉ thêm dữ liệu riêng cho kịch bản khi sử biến chưa đủ để ra thế cục đúng.

## Dữ liệu

1. **Bốn kịch bản:**

   | Kịch bản | Ngày bắt đầu |
   | --- | --- |
   | Khăn Vàng nổi dậy | 184 |
   | Chư hầu thảo Đổng | đầu năm 190 |
   | Quan Độ | giữa năm 200 |
   | Xích Bích | giữa năm 208 |

   Mỗi kịch bản có tên, mô tả 2–3 câu, ngày bắt đầu, và thành bắt đầu gợi ý.

2. **Thế lực theo thời kỳ.**
   - Trước năm 220, các thế lực mang tên chủ: "Tào Tháo", "Lưu Bị", "Tôn Kiên" rồi "Tôn Sách", "Tôn Quyền" theo năm, "Viên Thiệu", v.v. Từ 220 trở đi mới gọi "Tào Ngụy"; "Thục Hán", "Đông Ngô" theo đúng năm xưng đế.
   - Năm 184 phần lớn thành thuộc "Hán triều". Nếu chưa có thì thêm thế lực này bằng migration mới, màu trung tính.
   - Tên hiển thị tính theo ngày hiện tại. Chọn cách lưu gọn nhất: bảng đổi tên theo năm, hoặc thêm sử biến đổi tên. Ghi lý do đã chọn.

3. **Nhân vật.**
   - Thêm năm sinh và năm mất cho các nhân vật sử có trong danh sách 300 (`src/SimT.Modules.Base.Npc/migrations/`), bằng migration mới.
   - Nhân vật chưa sinh, hoặc chưa trưởng thành dưới 15 tuổi vào ngày bắt đầu, thì chưa xuất hiện. Đến năm 15 tuổi thì xuất hiện ở quê.
   - Nhân vật quá năm mất mà không có sử biến riêng thì qua đời lặng lẽ (`MarkDeceased`), có ghi biên niên.
   - Năm nào không chắc thì để trống, khi đó nhân vật luôn có mặt. Không bịa.

4. **Save cũ:** coi là kịch bản 184. Không đổi gì ngoài phần sửa tên thế lực theo thời kỳ.

## Giao diện

- Màn tạo ván mới ở màn tiêu đề (`TitleWindow`) thêm bước chọn kịch bản: 4 thẻ theo đúng phong cách của màn tiêu đề hiện có (màu, chữ, viền). Mỗi thẻ có năm lớn, tên, mô tả, và 3–4 thế lực chính lúc đó kèm màu.
- Chọn xong thì tạo ván như hiện tại, với ngày và thế cục của kịch bản đó.
- Không emoji, không gradient tự chế.

## Kiểm tra

Viết test cho:
- mỗi kịch bản có đúng ngày bắt đầu;
- năm 184: Lạc Dương thuộc Hán triều, không có thế lực tên "Tào Ngụy";
- năm 200: Hứa Xương thuộc Tào Tháo, Đổng Trác đã mất;
- năm 208: Gia Cát Lượng có mặt và theo Lưu Bị;
- nhân vật chưa sinh không xuất hiện; đến tuổi thì xuất hiện;
- save cũ mở được, và tên thế lực đúng thời kỳ.

## Nghiệm thu

- Build Release sạch.
- `dotnet test SimT.sln -c Release` qua.
- `node tools/check-scenes.mjs` qua.
- `--smoke` chạy được.
- Chụp ảnh màn chọn kịch bản, và ảnh bản đồ thế lực năm 184 và năm 208.
- Báo cáo ngắn, kèm danh sách năm sinh và năm mất đã thêm.
