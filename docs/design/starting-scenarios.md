# Kịch bản khởi đầu — đợt 10

Bốn mốc dùng lịch ngày nguyên sẵn có: 184-05-02 (giữ ngày mặc định), 190-01-01,
200-07-01 và 208-07-01. `StartingScenarios` là danh mục dùng chung; host ghi manifest
`product.json` riêng trong thư mục ván mới. Mở lại và nhân bản giữ nguyên manifest;
save không có manifest riêng vẫn dùng đường mở cũ, coi là kịch bản 184.

## Thế cục và tương thích

- Migration mới dựng nền Hán triều (19 thành) và Khăn Vàng (Quảng Tông). Các chuyển giao
  còn thiếu được bổ sung vào `so_historical_event`, dùng cùng bộ máy sử biến để áp dụng
  theo thứ tự ngày. Không duy trì bốn bản sao thế giới riêng biệt.
- Các mốc chuyển giao bổ sung chỉ có độ chính xác theo năm; ngày trong năm là mốc mô phỏng
  để sắp thứ tự, không khẳng định ngày lịch sử. Không đặt thêm ngày sinh hoặc ngày mất.
- Năm 200 Nghiệp Thành còn thuộc Viên Thiệu; việc chiếm Nghiệp Thành được chuyển sang
  sự biến năm 204. Năm 208 giữa năm Kinh Châu còn thuộc Lưu Biểu; Xích Bích cuối năm
  vẫn là sử biến đang chờ. Giữ cách xử lý cuối trạng thái của sử biến khi tạo ván muộn:
  không phát lại cảnh, cú sốc giá hoặc nguy hiểm đường đã hết thời hạn.
- Chọn bảng `po_faction_name` (mốc năm, tên, chủ): gọn hơn việc thêm nhiều cảnh sử biến
  chỉ để đổi tên; truy vấn theo năm hiện tại nên mọi màn dùng `IPoliticalCourt` có cùng
  tên. Ngụy từ 220, Thục từ 221, Ngô từ 229; Tôn Kiên → Tôn Sách → Tôn Quyền.
- Các migration chỉ khởi tạo thế cục mới khi chưa có ngày đã lưu. Save cũ nhận bảng tra
  tên và dữ liệu tiểu sử, nhưng không đổi chủ thành, chỗ ở, cờ tử vong hay danh sách đang
  có mặt. Cờ `np_lifetime_campaign` giữ chính sách này sau các lần mở lại.
- Giao diện chọn kịch bản là WPF trong `TitleWindow`: tái dùng màu, chữ và nút của màn
  tiêu đề; không tạo cảnh WebView/CSS riêng, emoji, gradient hoặc chip trang trí.
  Bốn thẻ hỗ trợ bàn phím, Escape trở lại và cuộn dọc ở cửa sổ thấp.

## Nhân vật và độ chắc chắn

Danh mục 300 bản ghi vẫn nguyên. `All`, `FindByCitadel` và `IsPresent` phản ánh danh sách
đã xuất hiện; `Find`/`FindByCode` là tra cứu hồ sơ, cần giữ cả người chưa xuất hiện để
sử biến và quan hệ có thể tham chiếu. Màn danh sĩ, mạng quan hệ và tương tác lọc theo
sự hiện diện. Nhân vật đủ 15 tuổi xuất hiện ở quê đã ánh xạ lên thành gần nhất trong
20 thành của bản đồ (không khẳng định đó chính là huyện sinh). Lượt tháng đầu không
cho NPC vừa xuất hiện di cư ngay khỏi quê. Ví dụ Lang Nha → Hạ Phì; Thiên Thủy → Tây Lương;
Phù Phong → Trường An; Lư Giang → Thọ Xuân; Nhữ Nam → Hứa Xương.

Năm mất đã qua thì dùng `MarkDeceased`; Society ghi biên niên loại `history` đúng một
lần nếu không có sự biến tử vong riêng. Không có Society vẫn áp dụng tuổi và tử vong;
không có Power vẫn tạo được kịch bản và bản đồ không có lớp thế lực.

Không dùng lại các năm 160/165/… lấp sẵn trong seed như dữ kiện sử. Migration mới xóa
năm sinh chưa được xác nhận và chỉ ghi các mục dưới đây. `—` là SQL NULL: không đoán
năm sinh từ ngoại hình, vai trò hoặc quan hệ cha con. Quan Vũ, Trương Phi, Triệu Vân…
không được gán năm sinh. Tôn Kiên có tranh luận niên đại nên không thêm năm sinh/mất;
sự biến riêng có sẵn vẫn xử lý ông. Những người khác ngoài bảng cũng để trống.

Nguồn là truyện tương ứng trong Tam Quốc Chí (Trần Thọ, chú Bùi Tùng Chi), Hậu Hán Thư
và Tấn Thư, bản chép trên Wikisource. Năm sinh tính từ năm mất và tuổi ghi trong truyện
khi niên đại rõ (tuổi mụ: năm mất − tuổi + 1); không suy từ tuổi không xác định.
Danh sách này không tuyên bố đã xác định được niên đại cho toàn bộ 300 người.

## Danh sách năm đã thêm hoặc xác nhận lại

| Nhân vật | Sinh | Mất | Truyện đối chiếu |
| --- | ---: | ---: | --- |
| Lưu Bị | 161 | 223 | [三國志/卷32](https://zh.wikisource.org/wiki/三國志/卷32) |
| Trương Phi | — | 221 | [三國志/卷36](https://zh.wikisource.org/wiki/三國志/卷36) |
| Quan Vũ | — | 219 | [三國志/卷36](https://zh.wikisource.org/wiki/三國志/卷36) |
| Trương Giác | — | 184 | [後漢書/卷71](https://zh.wikisource.org/wiki/後漢書/卷71) |
| Lư Thực | — | 192 | [後漢書/卷64](https://zh.wikisource.org/wiki/後漢書/卷64) |
| Vương Doãn | — | 192 | [後漢書/卷66](https://zh.wikisource.org/wiki/後漢書/卷66) |
| Lưu Biểu | — | 208 | [三國志/卷06](https://zh.wikisource.org/wiki/三國志/卷06) |
| Chu Du | 175 | 210 | [三國志/卷54](https://zh.wikisource.org/wiki/三國志/卷54) |
| Viên Thiệu | — | 202 | [三國志/卷06](https://zh.wikisource.org/wiki/三國志/卷06) |
| Tôn Quyền | 182 | 252 | [三國志/卷47](https://zh.wikisource.org/wiki/三國志/卷47) |
| Lưu Chương | — | 219 | [三國志/卷31](https://zh.wikisource.org/wiki/三國志/卷31) |
| Tào Tháo | 155 | 220 | [三國志/卷01](https://zh.wikisource.org/wiki/三國志/卷01) |
| Đổng Trác | — | 192 | [三國志/卷06](https://zh.wikisource.org/wiki/三國志/卷06) |
| Triệu Vân | — | 229 | [三國志/卷36](https://zh.wikisource.org/wiki/三國志/卷36) |
| Gia Cát Lượng | 181 | 234 | [三國志/卷35](https://zh.wikisource.org/wiki/三國志/卷35) |
| Bàng Thống | 179 | 214 | [三國志/卷37](https://zh.wikisource.org/wiki/三國志/卷37) |
| Pháp Chính | 176 | 220 | [三國志/卷37](https://zh.wikisource.org/wiki/三國志/卷37) |
| Hoàng Trung | — | 220 | [三國志/卷36](https://zh.wikisource.org/wiki/三國志/卷36) |
| Mã Siêu | 176 | 222 | [三國志/卷36](https://zh.wikisource.org/wiki/三國志/卷36) |
| Ngụy Diên | — | 234 | [三國志/卷40](https://zh.wikisource.org/wiki/三國志/卷40) |
| Khương Duy | 202 | 264 | [三國志/卷44](https://zh.wikisource.org/wiki/三國志/卷44) |
| Mi Trúc | — | 220 | [三國志/卷38](https://zh.wikisource.org/wiki/三國志/卷38) |
| Tuân Úc | 163 | 212 | [三國志/卷10](https://zh.wikisource.org/wiki/三國志/卷10) |
| Tuân Du | 157 | 214 | [三國志/卷10](https://zh.wikisource.org/wiki/三國志/卷10) |
| Quách Gia | 170 | 207 | [三國志/卷14](https://zh.wikisource.org/wiki/三國志/卷14) |
| Giả Hủ | 147 | 223 | [三國志/卷10](https://zh.wikisource.org/wiki/三國志/卷10) |
| Tư Mã Ý | 179 | 251 | [晉書/卷001](https://zh.wikisource.org/wiki/晉書/卷001) |
| Trương Liêu | 169 | 222 | [三國志/卷17](https://zh.wikisource.org/wiki/三國志/卷17) |
| Trương Cáp | — | 231 | [三國志/卷17](https://zh.wikisource.org/wiki/三國志/卷17) |
| Từ Hoảng | — | 227 | [三國志/卷17](https://zh.wikisource.org/wiki/三國志/卷17) |
| Tôn Sách | 175 | 200 | [三國志/卷46](https://zh.wikisource.org/wiki/三國志/卷46) |
| Lỗ Túc | 172 | 217 | [三國志/卷54](https://zh.wikisource.org/wiki/三國志/卷54) |
| Lữ Mông | 178 | 220 | [三國志/卷54](https://zh.wikisource.org/wiki/三國志/卷54) |
| Lục Tốn | 183 | 245 | [三國志/卷58](https://zh.wikisource.org/wiki/三國志/卷58) |
| Thái Sử Từ | — | 206 | [三國志/卷49](https://zh.wikisource.org/wiki/三國志/卷49) |
| Lữ Bố | — | 199 | [三國志/卷07](https://zh.wikisource.org/wiki/三國志/卷07) |
| Trần Cung | — | 199 | [三國志/卷07](https://zh.wikisource.org/wiki/三國志/卷07) |
| Viên Thuật | — | 199 | [三國志/卷06](https://zh.wikisource.org/wiki/三國志/卷06) |
| Công Tôn Toản | — | 199 | [三國志/卷08](https://zh.wikisource.org/wiki/三國志/卷08) |
| Tào Phi | 187 | 226 | [三國志/卷02](https://zh.wikisource.org/wiki/三國志/卷02) |
| Đặng Ngải | 197 | 264 | [三國志/卷28](https://zh.wikisource.org/wiki/三國志/卷28) |
| Chung Hội | 225 | 264 | [三國志/卷28](https://zh.wikisource.org/wiki/三國志/卷28) |
| Vu Cấm | — | 221 | [三國志/卷17](https://zh.wikisource.org/wiki/三國志/卷17) |
| Nhạc Tiến | — | 218 | [三國志/卷17](https://zh.wikisource.org/wiki/三國志/卷17) |
| Tào Thực | 192 | 232 | [三國志/卷19](https://zh.wikisource.org/wiki/三國志/卷19) |
| Hoa Hân | 157 | 232 | [三國志/卷13](https://zh.wikisource.org/wiki/三國志/卷13) |
| Vương Lãng | — | 228 | [三國志/卷13](https://zh.wikisource.org/wiki/三國志/卷13) |
| Trần Quần | — | 237 | [三國志/卷22](https://zh.wikisource.org/wiki/三國志/卷22) |
| Tư Mã Sư | 208 | 255 | [晉書/卷002](https://zh.wikisource.org/wiki/晉書/卷002) |
| Tư Mã Chiêu | 211 | 265 | [晉書/卷002](https://zh.wikisource.org/wiki/晉書/卷002) |
| Mã Đằng | — | 212 | [三國志/卷36](https://zh.wikisource.org/wiki/三國志/卷36) |
| Hàn Toại | — | 215 | [三國志/卷36](https://zh.wikisource.org/wiki/三國志/卷36) |
| Lưu Thiền | 207 | 271 | [三國志/卷33](https://zh.wikisource.org/wiki/三國志/卷33) |
| Gia Cát Cẩn | 174 | 241 | [三國志/卷52](https://zh.wikisource.org/wiki/三國志/卷52) |
| Gia Cát Khác | 203 | 253 | [三國志/卷64](https://zh.wikisource.org/wiki/三國志/卷64) |

## Kiểm tra

`StartingScenarioTests` kiểm tra ngày và mở lại cả bốn kịch bản, thế cục 184/200/208,
Gia Cát Lượng theo Lưu Bị, nhân vật chưa sinh/chưa đủ tuổi, xuất hiện ở quê, tử vong
lặng lẽ không ghi trùng, save cũ, tên theo năm và tắt từng DLC tùy chọn.
Các test cũ về 300 hồ sơ vẫn kiểm tra số bản ghi trong DB; danh sách xuất hiện không
còn bị buộc có đủ 300 người hoặc chia đều 20 người mỗi thành ở năm 184.
Test đơn hàng tìm trên cả 20 thành vì lọc tuổi làm thay đổi thành được NPC đặt hàng;
test ước nguyện so với nơi ở trước lượt tháng vì mô phỏng có thể di cư sau lúc phát sinh.

Ảnh và log nghiệm thu lưu riêng ở `D:\Hung DATA\Tool\_tq_shots\10\`, không đưa vào commit.
