# Prompt 12 — Hoạt ảnh đi lại cho dân phố

Hiện chỉ Quan Vũ, Trương Phi, quan binh (`han_officer`) và lữ khách có dải hoạt ảnh. Mọi kiểu người khác trong thành chỉ có sprite tĩnh. Việc này vẽ dải hoạt ảnh cho 9 kiểu còn lại, chia cho hai người làm song song:

| Người làm | Thư mục làm việc | Kiểu nhân vật |
| --- | --- | --- |
| agy | `D:\Hung DATA\Tool\_tk_art\agy` | `scholar`, `merchant`, `elder`, `commoner` |
| Codex | `D:\Hung DATA\Tool\_tk_art\codex` | `lady`, `swordswoman`, `archer`, `rebel`, `farmer_woman` |

Chỉ làm đúng các kiểu của mình. Không sửa code, không sửa `manifest.json`, không commit, không đụng file nào khác trong repo ngoài các file dải hoạt ảnh ghi ở bước 3.

## Tài liệu có sẵn

- `<thư mục làm việc>\g4\ref\<id>_sprite.png`: sprite tĩnh hiện có trong game, phóng to 6 lần, nền xám. Đây là chuẩn nhân vật: giữ nguyên trang phục, màu, đồ cầm tay, dáng người, tỉ lệ cơ thể.
- `<id>_portrait.png` (nếu có): chân dung cùng kiểu, chỉ để tham khảo mặt và màu áo.
- `D:\Hung DATA\Tool\_tk_art\walk_guide.png`: 8 pha bước đi bằng người que (chân đỏ là chân gần, chân xanh là chân xa, tay xám vung).
- `D:\Hung DATA\Tool\_tk_art\example_strip_han_officer.png`: dải mẫu đã dùng trong game.
- `D:\Hung DATA\Tool\_tk_art\strip.py`: ghép 3 ảnh lưới thành một dải.

## 1. Vẽ 3 ảnh cho mỗi kiểu

Tạo bằng công cụ tạo ảnh của bạn, lưu vào `<thư mục làm việc>\g4\`. Làm tuần tự từng ảnh, đưa ảnh sprite tham chiếu vào mỗi lần tạo.

Mọi ảnh: pixel art, lưới 4 cột × 2 hàng, 8 ô, khoảng 1376 × 768, nhìn nghiêng hoàn toàn quay sang phải, chân trên cùng đường nền trong mỗi ô, không chồng sang ô bên cạnh, không bóng đổ, không viền ô, không chữ. Nền một màu phẳng: magenta `#FF00FF`, riêng nhân vật có màu hồng tím trên áo thì dùng xanh lá `#00FF00`. Đầu, mặt, trang phục giữ y nguyên giữa các ô.

1. `g4/<id>_walk.png`: 8 pha bước đi khớp đúng từng ô của `walk_guide.png`. Cùng độ cao hông và đầu, cùng vị trí gối và bàn chân. Đồ cầm tay giữ cố định. Không vẽ người que.
2. `g4/<id>_atk.png`: một đòn 8 ô bằng đúng đồ đang cầm (quạt, gậy, bàn tính, cuốc, cung, kiếm...); không cầm gì thì đẩy tay: thủ thế, bắt đầu kéo về, kéo hết ra sau, bắt đầu vung, vung nửa đường, chạm mục tiêu toàn thân lao tới, theo đà, thu về thủ thế.
3. `g4/<id>_misc.png`: 4 ô đầu đứng thở lặp liền mạch (hít, hít hết, thở, thở hết); 4 ô sau: trúng đòn ngửa người, loạng choạng lùi, đứng lại, khuỵu một gối.

## 2. Ghép dải

Chạy trong thư mục làm việc: `python ..\strip.py <id> <m|g> <id>_strip.png <scale>`. `m` cho nền magenta, `g` cho nền xanh lá. Chạy thử một lần với scale 0.42, đọc dòng `idle body` in ra, rồi chạy lại với scale = 145 / idle body để người cao khoảng 145px như dải mẫu.

## 3. Tự kiểm rồi chép vào game

Mở `<id>_strip.png` xem bằng mắt (ghép lên nền tối để thấy viền). Đạt khi:
- Đúng nhân vật như sprite tham chiếu, cả 24 ô cùng một người, cùng cỡ.
- Không còn viền hay đốm màu nền, không bị cắt mất đầu, chân hay đồ cầm tay.
- Ô 4–11 bước chân nối nhau thành vòng đi liền mạch; ô 0–3 không giật.

Ô nào hỏng thì vẽ lại ảnh lưới đó (tối đa 2 lần mỗi ảnh). Đạt thì chép thành `D:\Hung DATA\Tool\a-day-in-three-kingdoms\assets\scenes\art\anim\<id>.png`. Không đạt sau 2 lần thì không chép, ghi lý do.

Nếu hết hạn mức tạo ảnh thì dừng và báo kiểu nào đã xong, ảnh nào còn thiếu.

## Báo cáo

Danh sách dải đã chép vào game, scale đã dùng, kiểu nào chưa xong và vì sao. Ngắn gọn.
