# Prompt 06 — Sửa sau nghiệm thu prompt 05

Cùng nhánh `feat/animation-feature-ui`, cùng luật: commit riêng, không push, không ép, mỗi file không quá 400 dòng. Không đụng nội dung ngoài danh sách dưới.

1. **Sai địa điểm khi nói chuyện và đấu.** Đang ở Lạc Dương, bấm Quan Vũ thì đầu cảnh ghi "Trác Huyện", cảnh đấu cũng vậy. `DialogueService.BuildTalkScene` lấy `npc.CitadelId`.
   - Địa điểm phải là thành người chơi đang đứng (`playerState.CurrentCityId`).
   - Dải người gần đây trong cảnh nói chuyện lấy người đang ở thành đó, cùng cách lọc với `BuildCityNpcs` (`ISocietyChronicle.GetNpcActivity(...).CurrentCitadelId ?? npc.CitadelId`). Vẫn giữ Quan Vũ, Trương Phi trong dải như `BuildCityNpcs` đang làm.
   - Thêm test: người chơi ở thành A, NPC quê ở thành B, cảnh nói chuyện ghi thành A.
2. **Nút kết quả hiện sớm khi đấu.** Trong lúc đang diễn "Hiệp 1/3" đã thấy "Tái đấu hiệp mới" và "Trò chuyện sau trận". Hai nút này chỉ hiện khi diễn xong hoặc bấm "Bỏ qua". Khi đang diễn chỉ để "Rời lôi đài".
3. **Bản đồ: điểm thành che nhãn.** Điểm tròn Quảng Tông đè lên nhãn "Nghiệp Thành". Chỉnh bảng hướng nhãn cho cụm Nghiệp Thành, Quảng Tông, Bình Nguyên, Thượng Đảng. Kiểm tra tự động phải tính cả nhãn đè điểm thành, không chỉ nhãn đè nhãn, ở cả cửa sổ 1076 và 1440.
4. **Nhân Duyên: người hảo cảm cao đè lên "Bạn".** Trương Lão Bản hảo cảm 100 chạm ảnh người chơi ở giữa. Đặt bán kính nhỏ nhất để ảnh và nhãn không chạm tâm. Nhãn "Âu Dương Phong Đúc" cũng không được đè nhãn vòng "Thân thiết · 60".
5. **Chữ Hán sai ở Chợ.** Gỗ, Vải thô, Than đá, Đồng thau đều đang ra 鐵; Lúa mì ra 米. `MainWindow.FeatureScenes.cs` ~dòng 275. Sửa: gỗ 木, vải thô 布, than đá 炭, đồng thau 銅, lúa mì 麥, muối 鹽 giữ nguyên. Hàng khác nhìn lại xem có chữ nào sai nghĩa.
6. **Thanh cuộn trắng** ở danh sách hàng Chợ và danh sách Danh Sĩ. Làm thanh cuộn tối theo màu cảnh, đặt một chỗ trong `scene.css`.
7. **Tên bị cắt ở danh sách Danh Sĩ** ("Biện Phu N...", "Tào Chươn...", "Âu Dương ..."). Danh sách dùng tên ngắn, bỏ phần trong ngoặc. Bỏ nhãn "Đồng thành" khỏi dòng tên, thay bằng dấu chấm vàng nhỏ cạnh ảnh có `title` là "Đồng thành". Tên đầy đủ vẫn hiện trong hồ sơ bên phải.

## Nghiệm thu

Build sạch, `dotnet test` qua hết, `node tools/check-scenes.mjs`, `--smoke`. Tự xem ảnh chụp thật qua CDP từng mục ở cửa sổ mặc định và 1076 rộng. Báo cáo ngắn, kèm đường dẫn ảnh.
