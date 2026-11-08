# Prompt 14 — Đợt 2: Tin đồn tửu quán

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" và "Đợt 2".

Làm trong worktree đã tạo sẵn, nhánh `feat/tq-02-tin-don`.
- Commit trên nhánh này, không push, không đụng `main`.
- File `.cs` mới không quá 400 dòng, không ép dòng dài.
- `SocietyService.cs` đã hơn 1100 dòng: thêm `partial` file mới (ví dụ `SocietyService.Rumors.cs`), không nhồi vào file cũ.

## Mục tiêu

Tửu quán là chỗ nghe tin. Tin đồn giúp đi buôn và báo trước biến cố, nhưng không phải lúc nào cũng đúng.

## Dữ liệu và luật (module `dlc.society`)

1. **Bảng `so_rumor`.** Viết migration mới trong `src/SimT.Modules.Dlc.Society/migrations/`, chạy lại được. Cột:
   - id, thành nghe, ngày nghe, ngày hết hạn;
   - loại (`price`, `event`, `whereabouts`, `danger`);
   - tiêu đề, lời kể tiếng Việt;
   - đúng hay sai;
   - đã kiểm chứng chưa;
   - khóa tham chiếu tùy loại: item, thành đích, npc, karma seed.
   Khai báo bảng trong `module.json` của society như các bảng khác.
2. **Nghe tin.** Thêm lệnh "Uống rượu nghe chuyện" ở tửu quán của thành.
   - Chi phí: trả 20 quan qua ví (lý do `society.tavern`) và tốn nửa buổi theo bảng chi phí hành động có sẵn. Đặt cả hai số thành hằng số một chỗ.
   - Kết quả: 3 tin cho thành đó. Trong vòng 10 ngày, nghe lại thì ra đúng 3 tin cũ và không trừ tiền lần nữa.
3. **Nguồn tin.** Chỉ lấy từ dữ liệu thật đang có, không bịa:
   - `price`: mặt hàng có giá ở một thành khác lệch nhiều nhất so với giá gốc, khan hoặc ế. Dùng capability chợ đang có (xem `MarketShockEngine.ScanArbitrageCorridors` và capability giá).
   - `event`: karma seed sắp chín hoặc cú sốc chợ đang chạy hay sắp chạy. Để sẵn chỗ cho sử biến của đợt 3: một interface nguồn tin nhỏ để đợt sau cắm vào. Không làm sử biến ở đây.
   - `whereabouts`: một danh sĩ đang ở thành nào, theo `NpcLocation.CurrentCitadelId`.
   - `danger`: tuyến đường đang có thổ phỉ hoặc nguy hiểm cao, theo dữ liệu quân sự/thế giới đang có.
4. **Tin sai.** Khoảng 20%, đặt thành hằng số. Dùng `IRng` của kernel để kết quả xác định theo seed.
   - Tin sai lấy dữ liệu gần đúng: ví dụ nói khan hàng ở một thành khác, hoặc nói sai thành của danh sĩ.
   - Lời kể bắt đầu bằng "Nghe nói…" cho mọi tin. Người chơi không biết tin nào sai.
5. **Kiểm chứng.** Khi người chơi tới đúng thành của tin `price` hoặc `whereabouts`, hoặc khi biến cố xảy ra hay hết hạn, đánh dấu đã kiểm chứng và ghi đúng/sai.
6. **Biên niên.** Thêm mục lọc "Tin đồn" trong biên niên của society: các tin đã nghe, trạng thái "Chưa rõ / Đúng / Sai", tin hết hạn mờ đi. Cách thêm bộ lọc làm giống bộ lọc di cư vừa thêm ở commit `0b1d20a`.

## Cảnh Trong thành

- Điểm hotspot dựng từ sublocation của thành (`MainWindow.Scenes.cs`, `BuildCityHotspots`).
- Tìm sublocation loại tửu quán hoặc quán trọ. Thành nào không có thì thêm hotspot "Tửu quán" bằng dữ liệu, không ghi cứng theo tên thành.
- Bấm vào thì hiện thẻ sự kiện của cảnh thành (`CityEventCard`) kèm lựa chọn "Uống rượu nghe chuyện (20 quan)". Nghe xong thẻ hiện 3 tin.
- Dùng khung có sẵn của `city-scene.js`. Nếu cần sửa JS thì giữ gọn, không cuộn ngang ở 1076.

## Kiểm tra

Viết test DLC cho:
- tin `price` đúng khớp dữ liệu chợ;
- nghe lại trong 10 ngày không trừ tiền và không đổi tin;
- tin hết hạn không còn hiện ở thẻ;
- tỉ lệ tin sai xấp xỉ hằng số trên mẫu lớn, với seed cố định;
- kiểm chứng khi tới thành;
- tắt `dlc.society` thì cảnh thành không có lệnh nghe tin và không lỗi.

## Nghiệm thu

- Build Release sạch, `dotnet test SimT.sln -c Release` qua hết, `node tools/check-scenes.mjs`, `--smoke`.
- Chụp: cảnh thành có tửu quán, thẻ 3 tin, biên niên mục Tin đồn. Lưu vào `D:\Hung DATA\Tool\_tq_shots\02\`.
- Báo cáo ngắn: các file đã đổi, ví dụ 3 tin thật, đường dẫn ảnh, điểm chưa ổn.
