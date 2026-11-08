# Prompt 13 — Đợt 1: Bản đồ thế lực và mùa

Spec: `docs/spec/nang-cap-tam-quoc.md`, mục "Luật chung" và "Đợt 1". Đọc cả hai trước khi làm.

Làm trong worktree đã tạo sẵn, nhánh `feat/tq-01-ban-do`. Commit trên nhánh này, không push, không đụng `main`. File `.cs` mới không quá 400 dòng, không ép dòng dài.

## Mục tiêu

Nhìn bản đồ là biết thiên hạ đang chia cho thế lực nào và đang mùa gì.

## Dữ liệu

1. `MapCityView` (`src/SimT.Contracts/Scenes/FeatureSceneContracts.cs`) thêm các trường có giá trị mặc định để không vỡ chỗ gọi cũ:
   - `factionId`, `factionNameVi`, `factionColor`;
   - `governorNameVi`.
2. Nguồn dữ liệu là `IPoliticalCourt` qua `CapabilityIds.CourtOffice`:
   - `Factions` cho tên và `ColorHex`;
   - thông tin cai quản thành (`CityGovernanceView`) cho thế lực và thái thú.
   Không có capability thì để trống.
3. `MapScene` thêm:
   - `season` với giá trị `spring | summer | autumn | winter`, theo tháng của `GameDateCodec`;
   - `factions`: danh sách `{id, nameVi, color, cityCount}` cho chú thích.
4. Chỗ dựng payload là `MainWindow.FeatureScenes.cs` (~dòng 68). Đưa phần dựng thế lực và mùa sang file partial mới nếu file đó vượt 400 dòng.

## Cảnh bản đồ (`assets/scenes/map-scene.js`, `map-scene.css`)

1. **Lớp thế lực.** Vẽ trên một canvas đặt dưới chấm thành và trên ảnh nền.
   - Chia ô: tính ở lưới thô, khoảng 1/8 kích thước bản đồ. Mỗi ô thuộc thành gần nhất nếu cách thành đó không quá một bán kính giới hạn (chọn sao cho vùng biển và vùng hoang không bị tô). Ô ngoài bán kính để trống.
   - Tô: màu thế lực, độ mờ khoảng 0,2. Kẻ biên đậm hơn ở chỗ hai ô kề nhau khác thế lực.
   - Vẽ: phóng canvas lên mượt (`imageSmoothingEnabled`). Chỉ vẽ lại khi kích thước bản đồ hoặc dữ liệu đổi, không vẽ mỗi khung hình.
   - Thành không thuộc thế lực nào thì không tô.
2. **Chấm thành.** Viền chấm theo màu thế lực.
3. **Panel thành.** Thêm dòng "Thuộc {thế lực} · Thái thú {tên}"; thiếu dữ liệu thì bỏ dòng.
4. **Chú thích.** Một khối nhỏ ở góc, liệt kê màu, tên, số thành của từng thế lực, kèm nút bật/tắt "Thế lực". Lưu lựa chọn bật/tắt bằng `localStorage`, bọc `try/catch`. Khối này không được che nút Lên đường và panel thành ở cửa sổ 1076.
5. **Mùa.**
   - Tông màu: đổi tông ảnh nền bằng CSS `filter`. Xuân hơi xanh tươi, hạ ấm, thu vàng cam, đông lạnh và nhạt.
   - Hạt: một canvas phủ trên cùng, không bắt chuột. Xuân là cánh hoa, thu là lá, đông là tuyết, hạ không có hạt.
   - Giới hạn: tối đa 60 hạt, `requestAnimationFrame`. Dừng khi tab hoặc cảnh ẩn (`visibilitychange`) và khi `prefers-reduced-motion`.
   - Hiện tên mùa cạnh ngày tháng trên thanh trên của bản đồ.

## Kiểm tra

- **Test C#:** payload bản đồ có thế lực và màu khi DLC chính trị bật; trường trống khi không có capability; `season` đúng theo tháng.
- **`tools/check-scenes.mjs`:** thêm kiểm tra payload mẫu có thế lực thì canvas thế lực được vẽ, và ở 1076px chú thích không đè panel thành.
- **Hiệu năng:** đo thời gian mở cảnh bản đồ trước và sau thay đổi. Không được chậm hơn quá 30 ms.

## Nghiệm thu

- Build Release sạch, `dotnet test SimT.sln -c Release` qua hết, `node tools/check-scenes.mjs`, `--smoke`.
- Chụp bản đồ bốn mùa (đổi tháng bằng cách nào tiện nhất, ví dụ payload mẫu), chụp lớp thế lực bật và tắt, chụp panel thành có dòng thế lực. Lưu ảnh vào `D:\Hung DATA\Tool\_tq_shots\01\`.
- Báo cáo ngắn: đã đổi file nào, số đo hiệu năng, đường dẫn ảnh, điểm còn chưa ổn.
