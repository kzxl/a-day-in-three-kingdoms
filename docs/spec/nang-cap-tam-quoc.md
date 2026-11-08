# Spec — Nâng cấp lấy cảm hứng từ dòng Tam Quốc Chí

Nhánh nghiệm thu: `feat/tam-quoc-upgrades`, tách từ `main` ở commit `0b1d20a`.

- Mỗi đợt làm trên một nhánh con `feat/tq-NN-<tên>`, trong worktree riêng.
- Claude nghiệm thu rồi gộp nhánh con vào nhánh nghiệm thu. Nhánh nào không đạt thì không gộp.
- Không gộp vào `main`. `main` giữ nguyên các tính năng hiện có. Chỉ khi người chơi đã thử nhánh nghiệm thu và đồng ý thì mới mở PR vào `main`.

Nguồn tham khảo: Tam Quốc Chí 8 Remake (2024), XIII, XIV. Chỉ lấy ý tưởng cách chơi. Hình, tên giao diện, bố cục là của game mình, không chép của Koei.

## Theo dõi

| Đợt | Nội dung | Người làm | Đề bài | Trạng thái |
| --- | --- | --- | --- | --- |
| 1 | Bản đồ thế lực và mùa | agy | `prompts/13` | Xong (merge "batch 1") |
| 2 | Tin đồn tửu quán | Codex | `prompts/14` | Xong (`231b25b`) |
| 3 | Sử biến theo lịch | agy | `prompts/15` | Xong (merge "batch 3") |
| 4 | Lưới quan hệ thiên hạ | Codex | `prompts/16` | Xong (merge "batch 4") |
| 5 | Đặc tính nhân vật | agy | `prompts/17` | Xong (merge "batch 5") |
| 6 | Ước nguyện NPC | Codex | `prompts/18` | Xong (merge "batch 6") |
| 7 | Đầu quân chúa công | Codex (agy bận đợt 3) | `prompts/19` | Xong (merge "batch 7") |
| 8 | Người đi trên bản đồ | Codex | `prompts/20`, `prompts/25` (sprite xe) | Xong (merge "batch 8" và "pixel sprites") |
| 9 | Biện luận | agy | `prompts/21` | Xong (merge "batch 9") |
| 10 | Kịch bản khởi đầu | Codex | `prompts/22` | Xong (merge "batch 10") |
| 11 | Chân dung nhiều biểu cảm | Codex | `prompts/23` | Xong (merge "batch 11") |
| 12 | Võ tướng lục | agy | `prompts/24` | Xong (merge "batch 12") |

| 13 | Chân dung riêng (40 nhân vật sử) và biến thể chân dung dùng chung | Codex (A, B), agy (C) | `prompts/26` | Xong (merge "portraits part A", "part B", "variants") |

| 14 | Âm thanh: nhạc nền và âm nền CC0, tiếng giao diện Kenney, tiếng chiêu thức tổng hợp, cài đặt âm lượng | Codex (A), agy (B) | `prompts/27` | Xong (merge "audio part A", "audio part B"); chưa nghe kiểm bằng tai |
| 15 | Làm lại giao diện Võ tướng lục: một hàng tab, bộ lọc thả xuống, tường chân dung không khung, sử biến dạng dòng thời gian; hồ sơ dùng ảnh mặt ở danh sách và hiện đủ người ở khung lớn | Claude | — | Xong; chưa chơi thử |

**Lượt kiểm tra cuối:** build Release 0 cảnh báo, 619 test, `check-scenes`, `--smoke`, hai golden replay (57 bất biến) đều qua. Chưa chơi thử trong game.

Trạng thái: Chưa làm → Đang làm → Nghiệm thu → Xong (ghi commit gộp).

Thứ tự chạy:
- Hai đợt cùng hàng (1–2, 3–4, …, 11–12) chạy song song, vì đụng ít file chung.
- Đợt 4 không cần chờ ai, nên chạy sớm ngay khi Codex rảnh.

Đợt sau dùng kết quả đợt trước khi cần:

| Đợt | Dùng kết quả của |
| --- | --- |
| 3 | 2 (tin đồn) |
| 5 | 3 (sử biến) |
| 6 | 4 (quan hệ) |
| 8 | 1 (bản đồ) |
| 9 | 5 (đặc tính) |
| 10 | 3 (sử biến) và 1 (thế lực) |
| 11 | 3 (cảnh sử biến dùng chân dung) |
| 12 | 3 (sử biến) và 5 (đặc tính) |

## Luật chung cho mọi đợt

- Không push, không gộp `main`. Commit trên nhánh con, kết thúc bằng dòng `Co-Authored-By` như các commit trước.
- File `.cs` mới không quá 400 dòng. File cũ đã dài (ví dụ `SocialService.cs`, `SocietyService.cs`) thì thêm `partial` file mới, không nhồi thêm.
- Bảng mới mang tiền tố của module sở hữu, migration chạy lại được nhiều lần. Save cũ mở được, thiếu dữ liệu mới thì tự sinh khi mở.
- Tắt DLC sở hữu tính năng thì game vẫn chạy, phần đó ẩn đi, không lỗi.
- Mọi số ngẫu nhiên dùng `IRng` của kernel để golden replay vẫn xác định. Nếu hash golden đổi thì không tự cập nhật, ghi lý do trong báo cáo.
- Không sửa kernel (`SimT.Core`, `SimT.Contracts/Time`) trừ khi đề bài cho phép rõ. Thời gian chỉ tính theo ngày nguyên.
- Không sửa migration đã có; luôn thêm file mới với số chưa dùng.
- Biên niên `so_journey_log` đã nhận các `event_kind`: `martial`, `commerce`, `governance`, `scholarly`, `travel`, `relationship`, `history`. Dùng các loại này, không dựng lại bảng.
- Ảnh và file QA để ở `D:\Hung DATA\Tool\_tq_shots\NN\`, không commit vào repo.
- Chữ trong game là tiếng Việt, tên người và thành dùng đúng dữ liệu hiện có.
- **Giao diện:** cảnh và màn mới dùng lại các thành phần đã có trong `assets/scenes/scene.css`, gồm:
  - khung cảnh trò chuyện `tk-talk-stage`;
  - thanh trên `tk-loc-badge`;
  - thẻ giấy `tk-dialog-paper` có ấn `tk-seal`;
  - lựa chọn `tk-choice` đánh số 壹 貳 參.

  CSS riêng chỉ để bố trí những gì thật sự mới. Không dùng emoji trong giao diện, không gradient tự chế, không pill/chip trang trí. Mẫu đúng: cảnh sử biến `event-scene.js` sau khi làm lại.
- Cảnh WebView2 không cuộn ngang ở cửa sổ 1076, hiệu ứng tắt khi `prefers-reduced-motion`.
- Nghiệm thu mỗi đợt: build Release sạch, `dotnet test SimT.sln -c Release` qua hết, `node tools/check-scenes.mjs`, `--smoke`, ảnh chụp các màn mới, báo cáo ngắn.

## Đợt 1 — Bản đồ thế lực và mùa

**Mục tiêu:** nhìn bản đồ là biết thiên hạ đang chia thế nào và đang mùa gì.

- `MapCityView` thêm thế lực đang giữ thành (id, tên, màu) và thái thú. Lấy từ dữ liệu chính trị hiện có (`FactionView.ColorHex`, `CityGovernanceView.FactionId`). Không có DLC chính trị thì để trống, bản đồ không tô.
- Cảnh bản đồ tô vùng ảnh hưởng: mỗi điểm trên bản đồ thuộc thành gần nhất trong một bán kính giới hạn, tô màu thế lực mờ, kẻ biên ở chỗ hai thế lực giáp nhau. Vẽ trên canvas độ phân giải thấp rồi phóng, không làm chậm bản đồ.
- Chú thích thế lực (tên, màu, số thành), nút bật tắt lớp thế lực. Panel thành ghi "Thuộc … · Thái thú …".
- `MapScene` thêm mùa. Bản đồ đổi tông màu theo mùa, có lớp hạt nhẹ: hoa xuân, lá thu, tuyết đông, hè không hạt. Tối đa khoảng 60 hạt, dừng khi cảnh ẩn.
- **Xong khi:** ảnh bản đồ bốn mùa, ảnh bật và tắt lớp thế lực, có test payload bản đồ có thế lực.

## Đợt 2 — Tin đồn tửu quán

**Mục tiêu:** có lý do ghé tửu quán; tin đồn giúp đi buôn và dẫn tới biến cố.

- Cảnh Trong thành có điểm "Tửu quán". Vào đó mua một bình rượu (20 quan, không tốn ngày) thì nghe 3 tin đồn. Mỗi thành làm mới tin sau 10 ngày.
- Loại tin, lấy từ dữ liệu thật:
  - giá: mặt hàng nào đang khan hoặc ế ở thành nào;
  - biến cố sắp tới: từ karma, cú sốc chợ, và sử biến khi đợt 3 có;
  - tung tích: danh sĩ đang ở đâu;
  - đường nguy hiểm: thổ phỉ, chiến sự.
- Có tỉ lệ tin sai cố định (khoảng 20%), ghi "Nghe nói…". Khi biết thật hay sai thì sổ ghi lại.
- Bảng `so_rumor` thuộc `dlc.society`, lưu ngày nghe và ngày hết hạn. Biên niên có thêm mục "Sổ tin đồn".
- **Xong khi:** test tin giá khớp dữ liệu chợ khi tin đúng; tin hết hạn không còn hiện; ảnh tửu quán.

## Đợt 3 — Sử biến theo lịch

**Mục tiêu:** thiên hạ chuyển động theo sử, người chơi sống giữa các biến cố.

- Dữ liệu `assets/content/historical-events.json`, khoảng 12 biến cố từ năm 184 tới 220:
  - 184: Khăn Vàng nổi dậy.
  - 189: Hà Tiến bị giết, Đổng Trác vào Lạc Dương.
  - 190: Chư hầu thảo Đổng; dời đô về Trường An.
  - 192: Lã Bố giết Đổng Trác.
  - 196: Tào Tháo đón thiên tử về Hứa Xương.
  - 200: Quan Độ.
  - 207: Tam cố thảo lư.
  - 208: Xích Bích.
  - 214: Lưu Bị vào Thục.
  - 220: Tào Phi xưng đế.
- Mỗi biến cố có ngày, thành liên quan, nhân vật, lời kể và tác động:
  - cú sốc giá qua `MarketShockEngine`;
  - đường nguy hiểm hoặc bị chặn trong N ngày;
  - đổi thế lực giữ thành;
  - NPC đổi nơi ở hoặc phe.
- Đến ngày thì áp dụng và ghi biên niên mục "Sử biến". Người chơi đang ở thành liên quan thì xem cảnh sử biến:
  - cảnh mới `event` trong WebView2: nền tranh, chân dung lớn của nhân vật, lời kể hiện dần;
  - 1–2 lựa chọn nhỏ cho người chơi, ví dụ quyên lương, chạy khỏi thành, buôn hàng chiến.
- Biến cố đã qua trước ngày bắt đầu ván chỉ áp dụng trạng thái cuối, không chiếu cảnh. Tin đồn đợt 2 báo trước biến cố khoảng 1 tháng.
- **Xong khi:** test áp dụng đúng ngày và đúng một lần, test save cũ; ảnh một cảnh sử biến.

## Đợt 4 — Lưới quan hệ thiên hạ

**Mục tiêu:** NPC có quan hệ với nhau, không chỉ với người chơi.

- Bảng quan hệ NPC–NPC trong `dlc.character`, gồm các loại: kết nghĩa, vợ chồng, cha con, huynh đệ, chủ–tôi, thầy–trò, bạn, thù.
- Dữ liệu sử cho những người có trong danh sách 300, ví dụ Lưu–Quan–Trương kết nghĩa, Tôn Sách–Chu Du, Lã Bố–Đổng Trác. Thêm quan hệ sinh theo seed cho người khác.
- Mô phỏng hằng tháng có thể sinh hoặc đứt quan hệ và ghi biên niên.
- Cảnh Nhân Duyên thêm chế độ "Thiên hạ": chọn một người thì thấy mạng quan hệ 2 bậc. Đường màu theo loại, nút là chân dung, bấm vào nút thì chuyển tâm sang người đó.
- Tác động lên người chơi:
  - Thân với một người thì được giới thiệu với bạn của họ: thiện cảm khởi điểm cao hơn, ghi "Bạn của …".
  - Kết thân với kẻ thù của bạn thân thì bạn thân giảm thiện cảm.
- **Xong khi:** test dữ liệu sử, test giới thiệu; ảnh chế độ Thiên hạ.

## Đợt 5 — Đặc tính nhân vật

**Mục tiêu:** mỗi người có cá tính thấy được, và cá tính ảnh hưởng cách chơi.

- Danh sách khoảng 16 đặc tính: Tham tài, Thanh liêm, Trung nghĩa, Đa nghi, Hào sảng, Keo kiệt, Hiếu học, Nóng nảy, Điềm tĩnh, Kiêu ngạo, Khiêm cung, Mê rượu, Mê võ, Trọng chữ tín, Xảo quyệt, Nhân hậu.
- Mỗi NPC có 1–3 đặc tính: nhân vật sử có đặc tính cố định, người khác sinh theo seed. Không mâu thuẫn với 4 kiểu tâm lý đã có; nếu được thì suy ra từ chúng.
- Tác động:
  - giá khi mua bán với NPC đó;
  - quà ưa thích, ví dụ Mê rượu thích rượu;
  - lời thoại;
  - đấu võ, ví dụ Nóng nảy đánh mạnh nhưng hở thủ.
- Hiện thành nhãn trong hồ sơ và cảnh trò chuyện. Chưa đủ thân thì hiện "???".
- **Xong khi:** test đặc tính ổn định theo seed và đổi giá đúng chiều; ảnh nhãn trong hồ sơ và cảnh trò chuyện.

## Đợt 6 — Ước nguyện NPC

**Mục tiêu:** NPC có việc muốn làm, người chơi giúp được và có kết quả.

- NPC có tối đa một ước nguyện đang mở, chọn từ mẫu:
  - mở tiệm, cần vốn;
  - tìm món hàng hiếm;
  - báo thù kẻ thù (dùng quan hệ đợt 4);
  - được làm quan;
  - tìm thầy;
  - lập gia đình;
  - về quê.
- Hiện khi thiện cảm đủ mức. Người chơi nhận thì ước nguyện vào sổ nhiệm vụ cùng chỗ với chuỗi nhiệm vụ khởi đầu.
- Có hạn. Giúp xong được thiện cảm lớn, quà, có thể mời làm gia thần, và ghi biên niên. Quá hạn thì NPC tự xoay xở theo mô phỏng.
- **Xong khi:** test nhận → xong → thưởng, test quá hạn; ảnh sổ nhiệm vụ.

## Đợt 7 — Đầu quân chúa công

**Mục tiêu:** vào một thế lực thì có việc thật, có đường thăng tiến.

- Đã đầu quân thì đầu mỗi tháng chúa công giao một nhiệm vụ hợp cấp bậc. Mẫu:
  - vận N lương tới thành X trước ngày Y;
  - mua N chiến mã;
  - đi sứ thành X;
  - dẹp phỉ;
  - nộp cống bạc.
- Nhận nhiệm vụ trong cảnh trò chuyện với chúa công (chân dung, lời giao việc).
- Xong thì được công trạng, bổng lộc, thăng cấp theo bậc đã có. Trượt thì giảm tín nhiệm; trượt 3 lần liền thì bị đuổi.
- **Xong khi:** test giao việc theo tháng, hoàn thành, thăng cấp, bị đuổi; ảnh cảnh giao việc.

## Đợt 8 — Người đi trên bản đồ

**Mục tiêu:** bản đồ sống, thấy thiên hạ đi lại.

- `MapScene` thêm danh sách người đang đi: thương đoàn đối thủ, thương đoàn của người chơi, NPC đang đi xa, quân đội khi có sử biến. Mỗi mục có từ đâu, đến đâu, tiến độ, loại, tên.
- Cảnh bản đồ vẽ biểu tượng nhỏ di chuyển dọc tuyến đường, rê chuột thì hiện tên.
- Tuyến đường có chiến sự hoặc thổ phỉ kẻ đỏ. Báo giá đi đường đó tăng nguy hiểm; đường bị chặn thì không cho đi và ghi lý do.
- **Xong khi:** test payload có người đi từ dữ liệu thật, test đường bị chặn; ảnh bản đồ có người đi.

## Đợt 9 — Biện luận

**Mục tiêu:** cuộc đấu trí khác hẳn đấu võ, dùng cho buôn bán và quan trường.

- Cảnh mới `debate`, có thanh thuyết phục 0–100 bắt đầu ở 50.
- Mỗi lượt người chơi chọn một trong 3 lý lẽ rút ngẫu nhiên từ bốn loại:
  - Lý: dựa vào trí;
  - Tình: dựa vào ngoại giao;
  - Lợi: hứa tiền hoặc hàng, có tốn thật;
  - Uy: dựa vào danh vọng.
- Đặc tính của đối phương (đợt 5) quyết định loại nào hiệu quả. Ví dụ Tham tài yếu trước Lợi; Trung nghĩa không ăn Lợi nhưng mềm trước Tình; Đa nghi cứng trước Tình nhưng yếu trước Lý.
- Đối phương đáp lại bằng lời và có thể phản công. Tối đa 5 lượt, có nút nhượng bộ. Không phải oẳn tù tì như đấu võ.
- Dùng khi:
  - mặc cả lô hàng lớn với thương nhân NPC;
  - xin quan phủ giảm thuế hoặc cấp phép;
  - thuyết phục NPC làm gia thần;
  - lựa chọn trong sử biến.
- **Xong khi:** test kết quả xác định theo seed, test tác động đúng chiều; ảnh cảnh biện luận.

## Đợt 10 — Kịch bản khởi đầu

**Mục tiêu:** chọn thời điểm bước vào thiên hạ.

- Tạo ván mới có chọn kịch bản: 184 Khăn Vàng, 190 Chư hầu thảo Đổng, 200 Quan Độ, 208 Xích Bích.
- Mỗi kịch bản đặt:
  - ngày bắt đầu;
  - thế lực giữ từng thành trong 20 thành;
  - NPC còn sống, tuổi, nơi ở, phe;
  - hệ số giá chợ;
  - sử biến còn chờ.
- Thêm năm sinh và năm mất cho nhân vật sử nếu dữ liệu chưa có.
- Save cũ coi như kịch bản hiện tại, không đổi gì.
- **Xong khi:** test mỗi kịch bản khởi tạo đúng thế lực và ngày, test save cũ; ảnh màn chọn kịch bản.

## Đợt 11 — Chân dung nhiều biểu cảm

**Mục tiêu:** nhân vật đổi nét mặt theo lời nói, cảnh trò chuyện và sử biến có cảm xúc.

- **Vẽ:** Codex tự vẽ bằng công cụ tạo ảnh.
  - Ai: các nhân vật sử đang có chân dung riêng trong `assets/scenes/art/portraits/`, cùng các kiểu dân phố.
  - Vẽ gì: thêm 3 nét mặt cho mỗi người: vui, giận, buồn hoặc lo.
  - Yêu cầu: cùng phong cách, cùng khung, cùng ánh sáng với `face.webp` và `full.webp` hiện có. Đổi nét mặt, không đổi người, không đổi trang phục.
- **Dữ liệu:** thêm biểu cảm vào `manifest.json`. Thiếu biểu cảm nào thì dùng ảnh thường.
- **Lời thoại:** mỗi dòng có thể kèm biểu cảm.
  - Đặt theo ngữ cảnh: thiện cảm tăng thì vui, bị từ chối hoặc khiêu khích thì giận, tin dữ thì buồn.
  - Cảnh trò chuyện và cảnh sử biến đổi chân dung theo dòng, có chuyển mờ ngắn.
- **Xong khi:** đủ ảnh cho các nhân vật trên, đã soát bằng mắt, không lệch người; ảnh cảnh trò chuyện ở ba nét mặt.

## Đợt 12 — Võ tướng lục

**Mục tiêu:** có sổ sưu tầm, cho người chơi lý do đi gặp người và chứng kiến sử.

- Ghi lại người đã gặp, tức đã trò chuyện ít nhất một lần, và sử biến đã chứng kiến. Lưu trong module sở hữu, save cũ suy ra từ dữ liệu quan hệ đang có.
- Sổ mở từ hồ sơ Danh sĩ, có hai tab.
  - **Nhân vật:**
    - lưới chân dung, chưa gặp thì là bóng đen kèm "?";
    - lọc theo thế lực, thành, loại người;
    - tỉ lệ sưu tầm.
  - **Sử biến:**
    - các biến cố đã chứng kiến, bấm vào thì xem lại cảnh;
    - biến cố chưa xảy ra hoặc lỡ mất thì hiện mờ.
- Mốc sưu tầm (10, 50, 100 người, đủ 12 biến cố) cho danh vọng và ghi biên niên.
- **Xong khi:** test ghi nhận khi gặp, test save cũ, test mốc; ảnh hai tab.

## Ngoài phạm vi (để sau)

- Chơi nhiều nhân vật cùng lúc, chơi nhiều người.
