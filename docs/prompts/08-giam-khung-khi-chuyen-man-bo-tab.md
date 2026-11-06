# Prompt 08 — Hết khựng khi bấm menu, bỏ tab trùng

Nhánh `fix/nav-lag-remove-tabs` (đã tạo sẵn từ `main`). Commit riêng, không push. Mỗi file `.cs` không quá 400 dòng và không ép dòng dài để đạt giới hạn. Không đổi tính năng ngoài danh sách dưới. `MainWindow.Rendering.cs` có thể tách thêm nếu cần.

## Số đo hiện tại (ms, từ lúc gọi `SelectScreen` đến khi WPF vẽ xong)

| Màn | Tổng |
| --- | --- |
| `market.stall` | 482–1046 |
| `npc.directory` | 588–986 |
| `social.network` | 643–913 |
| `world.map` | 481–782 |
| `professions.trades` | 467–725 |
| `market.bank` | 481–488, trong đó `Render()` 401 |
| `politics.court` | 315 |

## 1. Bốn màn có cảnh WebView2 đang dựng hai lần (nguyên nhân chính)

`RenderSelected` mở cảnh (`TryOpenMapScene`, `TryOpenSocialScene`, `TryOpenMarketScene`, `TryOpenDossierScene`) rồi vẫn dựng toàn bộ KPI, lệnh, bảng WPF nằm dưới cảnh; `world.map` còn dựng cả `GameWorldMapControl`. Người chơi không nhìn thấy phần đó.

Sửa: mở được cảnh thì bỏ qua toàn bộ phần dựng WPF của màn đó, và đặt nội dung trang WPF về rỗng để không tốn layout. Chỉ khi `TryOpen...` trả về `false` (WebView2 không có, `--smoke`) mới dựng WPF như cũ. Trình tự cho `world.map`: thử cảnh trước, thất bại mới gọi `RenderWorldMapScreen`. Các lối đang dựa vào phần WPF dưới cảnh (`SelectFirstRow`, `_firstGrid`, `--row`, `--act`) phải vẫn hoạt động ở chế độ không có cảnh.

## 2. `BankScreen.Render()` mất 400ms

`src/SimT.Modules.Dlc.Economy/Banking/BankScreen.cs`, phần radar chênh lệch giá (~dòng 111). Vì `RefreshBadges` gọi `Render()` của mọi màn, 400ms này cộng vào mỗi lần huy hiệu bẩn, tức lần bấm đầu sau mỗi hành động và mỗi lần Qua Ngày.

Tìm đúng phép tính chậm. Tính radar một lần cho mỗi ngày trong game, hoặc chỉ tính khi màn đang mở. Mục tiêu dưới 20ms. Không đổi kết quả hiển thị; thêm test nếu đổi logic. Đo lại `Render()` của mọi màn, màn nào trên 20ms thì xử lý tương tự.

## 3. Menu phải đổi trạng thái ngay khi bấm

Trong `StrategicRibbon.ItemSelected`: đặt ngay `SelectedItemId` và `SelectedSubItemId` của mục vừa bấm, cho ribbon vẽ trước, rồi mới dựng màn qua `Dispatcher.BeginInvoke` mức `Background`. Bấm liên tiếp nhiều mục thì chỉ dựng mục cuối. Bấm lại đúng màn đang đứng thì không dựng lại.

## 4. Giảm phần còn lại

Sau mục 1–3, đo lại và xử lý các màn WPF còn trên 300ms, bắt đầu từ `professions.trades`, `politics.court`. Tìm nguyên nhân thật (ví dụ `DataGrid` không ảo hoá, dựng lại style từng ô) rồi sửa gốc. Ghi số đo trước và sau vào báo cáo. Không che bằng hiệu ứng hay ảnh giữ chỗ.

## 5. Bỏ hàng tab trùng với menu

Xoá `ScreenGroupTabs` trong `MainWindow.xaml` và mọi code đi kèm trong `MainWindow.Strategic.cs` (bộ xử lý `SelectionChanged`, `SetTabs` trong `SyncStrategicNavigation`). Sửa lại `Grid.Row` để không còn khoảng trống. Giữ `GameTabs` và các tab lọc trong nội dung màn ("Tất cả", "Ngân phiếu..."), vì đó không phải điều hướng. Nếu `GameTabs` có phần chỉ phục vụ hàng tab này thì xoá luôn.

## Nghiệm thu

Build sạch, `dotnet test` qua hết, `node tools/check-scenes.mjs`, `--smoke`. Bằng CDP mở lại Bản đồ, Nhân Duyên, Chợ, Danh Sĩ và thoát bằng ESC: phải về Trong thành, không lộ trang trống. Chụp một màn WPF (Tiền Trang) để thấy không còn hàng tab. Báo cáo ngắn, kèm bảng số đo trước và sau.
