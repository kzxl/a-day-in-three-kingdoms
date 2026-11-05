# Prompt 02 — Bộ control chuyên dụng cho WPF

Làm sau prompt 01, cùng nhánh `feat/scenes-webview2`, cùng các luật ở đầu `docs/prompts/01-canh-dien-webview2.md`.

Vấn đề: giao diện toàn nút và lưới mặc định, trông đơn giản. Thay bằng bộ control riêng mang chất game Tam Quốc, dùng lại được ở mọi màn. Đặt trong `src/SimT.Host.Wpf/Controls/` cạnh các `Game*` có sẵn, style trong `Themes/`. Host vẫn render chung từ read model, không biết module nào.

## Control cần có

- **GameButton**: thay `Button` mặc định. Các loại: chính (chu sa), phụ (viền đồng), nguy hiểm, icon. Nền sơn mài, viền đồng mảnh, góc vát, có trạng thái rê chuột, nhấn lún xuống, tắt. Có thể hiện kèm giá hoặc số ngày tốn ("Mua · 14 q").
- **GameTable**: thay lưới bảng ở `MainWindow.Rendering.cs`. Hàng dạng thẻ thấp có zebra nhẹ, rê chuột sáng viền đồng, hàng chọn có vạch chu sa bên trái. Tiêu đề cột bấm được để sắp xếp. Cột số canh phải, có mũi tên tăng giảm màu ngọc/chu sa. Ô đầu hàng hiện icon vật phẩm hoặc mặt nhân vật (tra `assets/scenes/art/manifest.json`). Nút trong hàng hiện khi rê chuột để bảng đỡ rối. Giữ ảo hoá khi nhiều dòng.
- **GameItemSlot**: ô vật phẩm vuông có khung theo độ hiếm, số lượng ở góc, tooltip chi tiết. Dùng cho túi, kho, phần thưởng.
- **GameQuantityPicker**: chọn số lượng bằng `−` `+`, kéo thanh, nút Tối đa, hiện tổng tiền ngay. Thay ô nhập số ở inspector.
- **GameTabs**: tab dạng thẻ bài với gạch chân đồng, thay các nút lọc rời rạc.
- **GameProgressBar**: thanh có vạch mốc và số, dùng cho sinh lực, thể lực, hảo cảm, kinh nghiệm.
- **GameToast**: thông báo nổi góc phải, tự tắt, thay dòng chữ ở thanh trạng thái. Có loại lãi, lỗ, sự kiện.
- **GameConfirmDialog**: hộp xác nhận theo theme, thay `MessageBox`.
- **GameRichTooltip**: tooltip nhiều dòng có tiêu đề, chỉ số, mô tả.
- **GamePortraitCard**: thẻ nhân vật có mặt, tên, chức, thanh hảo cảm, nút gặp gỡ. Dùng ở Nhân Sĩ và Nhân Duyên thay cho hàng bảng khi màn hợp.

## Áp dụng

Thay ở `MainWindow`, inspector, các màn chung. Giữ read model hiện có; nếu cần thêm gợi ý hiển thị (icon, độ hiếm) thì thêm trường tuỳ chọn vào contract `Ui`, không phá module cũ. Không còn `Button` hay `DataGrid` kiểu mặc định ở màn chính.

Thêm một màn demo ẩn `--demo --screen ui.gallery` hiện đủ các control để kiểm tra.

## Nghiệm thu

Như prompt 01: build sạch, test qua hết kể cả test kiến trúc, `--smoke` qua. Commit riêng, không push. Báo cáo control nào đã thay ở đâu, chỗ nào còn dùng kiểu mặc định.
