# Prompt 09 — Nhân vật theo đúng thành, sửa logic cảnh, hành lý mang theo

Nhánh `fix/city-roster-luggage` (đã tạo sẵn). Mỗi phần A, B, C một commit riêng, không push. Mỗi file `.cs` không quá 400 dòng và không ép dòng dài để đạt giới hạn. Không đổi tính năng ngoài danh sách dưới.

## A. Nhân vật theo đúng thành

1. **Một cách xác định NPC đang ở đâu.** Thêm một hàm dùng chung trong `SimT.Contracts`: nơi NPC đang ở = `ISocietyChronicle.GetNpcActivity(id)?.CurrentCitadelId ?? npc.CitadelId`. Dùng nó ở `BuildCityNpcs`, `DialogueService` và mọi chỗ `npc.CitadelId != _player.CurrentCityId` trong `SocialService.cs` (các dòng ~238, 483, 633, 801, 908, 1498). Hiện cảnh Trong thành xét nơi đang ở còn lệnh trò chuyện, tặng quà xét quê quán, nên NPC đi xa tới thành người chơi vẫn bị báo khác thành. Giữ nguyên ngoại lệ gia thần (`GetRetainer`).
2. **Bỏ ghi cứng Quan Vũ, Trương Phi:** `MainWindow.Scenes.cs` `BuildCityNpcs`, `DialogueService.cs` dải người gần đây (~dòng 67–68), cờ `isVisitingHero` (~dòng 238).
3. **`BuildCityNpcs`:** tối đa 6 người đang ở thành hiện tại. Xếp tướng và người có `animPath` trước để Trong thành luôn có người đi lại toàn thân nếu thành có tướng, rồi tới người còn lại, thứ tự ổn định theo id. Thành không có ai thì cảnh vẫn chạy, không lỗi.
4. **Test:** người chơi ở Lạc Dương không thấy Quan Vũ; ở Trác Huyện thấy; NPC đang đi xa tới thành người chơi thì trò chuyện được.

## B. Sửa logic các cảnh

1. **Tặng quà chọn món.** `DialogueService.cs` ~dòng 160 và 212 đang tự lấy món đầu tiên. Bấm "Tặng quà" hiện danh sách đồ trong túi ở thành này dưới dạng lựa chọn `gift:<itemId>`, giá trị cao đứng trước, tối đa 5 món, kèm "Thôi". Dùng đúng khung lựa chọn của cảnh nói chuyện, không thêm hộp thoại WPF. Thêm test.
2. **Kiệt sức khi đấu.** `MilitaryService` trả 0 hiệp kèm lý do, còn `duel-scene.js` (`runReplay` ~dòng 176 và hàm kết thúc ~dòng 163) thoát sớm nên chỉ thấy "Trống trận vang rền...". Khi 0 hiệp: hiện lý do từ `combatSummaryVi`, không diễn, nút "Rời lôi đài" vẫn dùng được.
3. **Danh Sĩ ở thành khác.** Bái phỏng, So tài, Mở cảnh nói chuyện chỉ bấm được khi NPC đang ở thành người chơi hoặc là gia thần. Không thì khoá nút và ghi "Đang ở Thành Đô", dùng hàm ở A1. Máy chủ cũng phải chặn trong `HandleDossierSceneMessage`, không chỉ khoá ở giao diện.
4. **Tên vợ.** `LineageService.cs` `SpouseNames`: đổi thành `"Vương Thị", "Lưu Thị", "Tôn Thị", "Mã Thị", "Hà Thị"`. Không dùng tên NPC có thật trong danh sách 300 người. Save cũ giữ nguyên tên đã có.

## C. Hành lý mang theo

**Cách làm:** không tạo kho mới, không đổi schema save. `pl_bag` theo thành giữ nguyên ý nghĩa: đồ của người chơi tại thành đó. Khi Lên đường, một phần đồ được mang theo, tức chuyển từ túi thành đi sang túi thành đến, trong giới hạn của phương tiện. Phần còn lại ở nguyên thành cũ. Kho Bãi và Thương Đoàn giữ nguyên, Thương Đoàn vẫn là cách chở số lượng lớn.

1. **Sức mang theo phương tiện**, đặt thành hằng số một chỗ để dễ chỉnh: Bộ hành 40, Dịch mã 20, Tiêu cục 120 đơn vị. Thêm `CarryCapacity` vào `TravelQuote` và `MapTravelQuoteView`.
2. **Lệnh đi:** `travel:<thành>:<phương tiện>[:<itemId>=<số>,...]` trong `WorldMapCommandHandler.cs`.
   - Có danh sách: kiểm tra từng món đủ số lượng và tổng không quá sức mang. Sai thì báo lỗi và không đi, không trừ tiền.
   - Không có danh sách (bản đồ WPF, lối cũ): tự chọn món giá trị đơn vị cao trước cho đến khi đầy, kết quả phải xác định, không dùng số ngẫu nhiên.
   - Trừ lương thực như cũ trước, rồi mới tính phần mang theo. Chuyển hàng trong cùng giao dịch với trừ tiền và đổi thành. Không được mất hay sinh thêm đồ; thêm test bảo toàn tổng số lượng.
3. **Cảnh Bản đồ (`map-scene.js`):** dưới ba phương tiện thêm khối "Hành lý" ghi `Mang 40/40 · Để lại 102 món`. Bấm mở danh sách đồ ở thành hiện tại với nút −/+ từng món, mặc định theo cách tự chọn ở trên, đổi phương tiện thì tính lại. Nội dung không tràn, không cuộn ngang ở cửa sổ 1076. Khi chọn một thành có đồ để lại, panel ghi "Đồ của bạn ở đây: 82 Gạo, 4 Lụa".
4. **Thông báo khi đến nơi:** ghi đã mang gì và để lại gì ở đâu.
5. **Chợ:** đổi lời gợi ý trong `market-scene.js` ~dòng 49 và `bestArbitrageTip` ở `MainWindow.FeatureScenes.cs` ~dòng 247 cho đúng: đi bộ mang được ít, muốn chở nhiều dùng Thương Đoàn. Lợi nhuận gợi ý không được vượt số mang được khi người chơi tự đi.
6. **Kiểm tra lại:** cột mốc trong `PlayerDestinyService.cs` ~dòng 273 (đếm thành có đồ) và các chỗ khác đọc `pl_bag` vẫn đúng nghĩa. Nếu test golden đổi hash, không tự cập nhật: ghi rõ trong báo cáo lý do hash đổi.

## Nghiệm thu

Build sạch, `dotnet test` qua hết, `node tools/check-scenes.mjs`, `--smoke`. Bằng CDP: đi từ Lạc Dương sang Trác Huyện, thấy Quan Vũ ở Trác Huyện và không thấy ở Lạc Dương; chọn hành lý, đến nơi đúng số đã chọn, phần để lại còn ở Lạc Dương; quay lại lấy được. Tặng quà chọn món, đấu khi kiệt sức, Danh Sĩ ở thành khác. Báo cáo ngắn, kèm đường dẫn ảnh.
