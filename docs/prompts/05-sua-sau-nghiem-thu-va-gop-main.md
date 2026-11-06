# Prompt 05 — Sửa sau nghiệm thu prompt 04 và gộp main

Cùng nhánh `feat/animation-feature-ui`, cùng luật của prompt 04. Commit riêng, không push, không ép. Tự kiểm bằng CDP và xem ảnh chụp thật ở cửa sổ khoảng 1076 và 1440 rộng trước khi báo xong.

## A. Lỗi cần sửa

**A1. Hình nhân vật trong trận đấu bị gán sai (nghiêm trọng).** `MainWindow.Scenes.cs` (~dòng 199–210) cho người chơi mượn hình Quan Vũ hoặc Trương Phi, và đối thủ không có khung thì rơi về `anim/zhang_fei.png`. Kết quả: người chơi mang hình Trương Phi, Trương Lão Bản cũng thành Trương Phi. Xoá hoàn toàn cách này.
- Người chơi dùng `anim/traveler.png`.
- Thêm vào manifest bảng `anim` theo archetype: `general` dùng `anim/han_officer.png`. Danh tướng có khung riêng thì dùng khung riêng.
- Nhân vật không có khung nào: dùng đúng sprite tĩnh của họ, diễn bằng code như trước prompt 04 (lấy đà, lao tới, khựng, văng lùi). Tuyệt đối không mượn hình người khác. Thêm test: Trương Lão Bản không nhận khung Trương Phi; người chơi nhận `traveler`.

**A2. Trong thành sai tỉ lệ và nhãn đè nhau.** Quan Vũ, Trương Phi đang to gấp 3–4 lần các nhân vật khác, nhãn tên hiện sẵn và chồng lên nhau, che cả điểm bấm.
- Mọi nhân vật dùng chung một thang theo chiều sâu: người gần chiều cao khoảng 150px ở cửa sổ cao 840, người xa nhỏ dần. Tính theo chiều cao thân thực trong khung, không theo kích thước ô 340×170.
- Nhãn chỉ hiện khi rê chuột hoặc focus, kể cả người đi lại. Bấm vào nhân vật ở đâu cũng được.
- Người đi lại đi trong dải đường, không đi xuyên điểm bấm, không chồng lên nhau.

**A3. Bản đồ.** Ở cửa sổ 1076, Tây Lương và Kiến Nghiệp bị cắt, nhãn Quảng Tông, Nghiệp Thành, Thượng Đảng đè nhau.
- Mức 100% phải thấy đủ 20 thành trong khung.
- Với cụm gần nhau, đặt nhãn lệch theo bảng cho từng thành (trái, phải, trên, dưới) để không chồng.
- Token vị trí không che tên thành đang đứng.

**A4. Nhân Duyên.** Tất cả hảo cảm 0 nên 20 người xếp hết ở vòng ngoài, nhãn bị cắt ("Trần Quần (Trườn", "Tô Song (Đại M"), Hoa Hùng đè Thái Diễm.
- Nhãn chỉ dùng tên ngắn, bỏ phần trong ngoặc. Không nhãn nào ra ngoài khung.
- Góc rải đều theo số người. Đông quá thì xếp 2 vòng xen kẽ theo hảo cảm.
- Tab Tuyệt kỹ, Thư từ, Gia thần chạy thử với dữ liệu thật.

**A5. Hồ sơ.** Ở cửa sổ 1076 có thanh cuộn ngang, cột chỉ số bị cắt ("Thương Mại 4"). Biểu đồ và cột chỉ số phải co vừa, không cuộn ngang.

**A6. Chợ.** Kiểm `mk_price_sample` có truyền đúng không: mọi đường giá đang phẳng. Nếu giá 10 ngày thật sự bằng nhau thì ghi "Giá ổn định" thay vì vẽ đường phẳng; nếu sai dữ liệu thì sửa.

## B. Gộp origin/main

`git merge origin/main` (24 commit: gộp DLC thành `dlc.economy`, `dlc.character`, `dlc.power`, ribbon chiến lược, Time HUD, ngân hàng, đa ngôn ngữ, viewport 2D). Không rebase. Xung đột ở `README.md`, `MainWindow.xaml`.

- **Khung màn hình giữ theo main:** thanh ribbon chiến lược, ấn Qua Ngày, Time HUD. Bỏ cột 5 nhóm của prompt 04 giai đoạn 2, nhưng giữ giá trị của nó: mỗi mục con trong ribbon có một dòng mô tả và huy hiệu việc cần làm.
- Giữ thanh "Nên làm".
- Cảnh Bản đồ, Nhân Duyên, Chợ, Danh Sĩ, đấu, Trong thành phải chạy trong khung mới, kể cả các module đã đổi tên và các màn mới của main.
- Nếu main có `SceneViewportControl` trùng việc với `SceneOverlayControl` thì đừng gộp hai cái; dùng overlay cho cảnh WebView2 và ghi rõ trong báo cáo.

## Nghiệm thu

Build sạch, `dotnet test` qua hết, `node tools/check-scenes.mjs`, `--smoke`. Bằng CDP đi đủ: Trong thành → nói chuyện → đấu với cả danh tướng lẫn thương nhân → Bản đồ → Lên đường → Nhân Duyên → Chợ → Danh Sĩ, console không lỗi. Báo cáo kèm đường dẫn ảnh chụp.
