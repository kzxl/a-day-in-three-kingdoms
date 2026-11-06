# Prompt 07 — Hoàn tất gộp origin/main (cấu trúc mới)

Repo đang dở `git merge origin/main` trên nhánh `feat/animation-feature-ui`, có 5 file xung đột. Giải quyết rồi commit merge. Không rebase, không abort, không push. Mỗi file `.cs` tự viết không quá 400 dòng. Không đổi tính năng ngoài danh sách dưới.

**Nguyên tắc:** cấu trúc và tối ưu của main thắng, nhưng các cảnh WebView2 và mô tả một dòng của nhánh này phải còn nguyên.

## Từng xung đột

1. `SceneArtCatalog.cs`: lấy phần cache của main (`CachedScenesDirectory`, `CachedManifestPath`), giữ `ReadStringMap` của nhánh.
2. `GameCommandRibbon.cs`: lấy cách vẽ icon trong ô 18px của main. Mục con và huy hiệu của nhánh phải vẽ đúng với lề mới.
3. `MainWindow.Scenes.cs` ~dòng 244: gộp cả hai:
   `InvalidateBadges(); RefreshBadges();` rồi giữ nhánh `if (activeScene is MapScene or SocialScene or MarketScene or DossierScene) SelectScreen(CitySceneScreenId); else RenderSelected();`
4. `MainWindow.xaml`: lấy phần tiêu đề và `ScrollViewer` của main.
5. `MainWindow.xaml.cs`:
   - Lấy `_badgesDirty`, `_cachedBadgeCounts`, `_lastScreenPerGroup`, `InvalidateBadges`, `_isNavigating` của main.
   - `BuildStrategicRibbon`, `SyncStrategicNavigation`, `RefreshBadges` chỉ tồn tại một bản, đặt ở `MainWindow.Strategic.cs`. Xoá bản trong `xaml.cs` để không trùng.
   - `SelectScreen` theo main nhưng không được render hai lần khi đã đứng đúng màn.

## Thanh ribbon sau khi gộp

Dùng tên, thứ tự và id nhóm của main. Giữ `SubItems` có mô tả một dòng và huy hiệu của nhánh:
- `group.self` "Đại Bản Doanh": Trong Thành (`CitySceneScreenId`, mặc định), Nhật Trình (`CityHubScreenId`), Mệnh Cách, Danh Vọng, Đồ Giám, Sổ Cái.
- `group.map` "Thiên Hạ": `world.map`.
- `group.trade` "Thương Đạo": như nhánh, gồm cả `market.bank`, `market.player_bank`. Bản của main bỏ sót hai màn này khiến chúng rơi vào nhóm mặc định, phải sửa.
- `group.social`, `group.power`: như main, kèm mô tả của nhánh.

Huy hiệu nhóm và mục con đều đọc từ `_cachedBadgeCounts`, không render lại mọi màn khi đổi tab. `_lastScreenPerGroup` vẫn nhớ màn cuối của từng nhóm. Mọi `ScreenId` của mọi module phải thuộc đúng một nhóm; thêm test kiểm tra điều này nếu chưa có.

## Nghiệm thu

Build sạch, `dotnet test` qua hết, `node tools/check-scenes.mjs`, `--smoke`. Mở màn tiêu đề mới rồi vào game. Bằng CDP kiểm: Trong thành, nói chuyện, đấu, Bản đồ, Nhân Duyên, Chợ, Danh Sĩ. F3 bật được HUD debug và không che cảnh khi tắt. Báo cáo ngắn, kèm đường dẫn ảnh.
