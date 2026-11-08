# Prompt 19 — Đợt 7: Đầu quân chúa công

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" và "Đợt 7".

Worktree đã tạo sẵn, nhánh `feat/tq-07-chua-cong`.
- Commit trên nhánh này, không push.
- File `.cs` mới không quá 400 dòng. File cũ dài thì thêm `partial`.
- Không sửa kernel.
- Ảnh QA ghi vào `D:\Hung DATA\Tool\_tq_shots\07\`, không commit vào repo.
- Biên niên: không dựng lại bảng `so_journey_log`. Dùng các `event_kind` đã có (`governance`), ghi ở chỗ của module society qua capability.

## Mục tiêu

Đầu quân vào một thế lực thì có việc thật mỗi tháng và có đường thăng tiến.

## Đã có, phải dùng lại

- `IPoliticalCourt` (`src/SimT.Contracts/Playing/PoliticsContracts.cs`): `JoinFaction`, `LeaveFaction`, `AllegianceFactionId`, `Offices` (có `Rank`, bổng lộc, danh vọng cần), `Reputation`, `Factions` (có `LeaderNpcId`, `CapitalCityId`).
- Phần cài đặt nằm ở `src/SimT.Modules.Dlc.Power/Politics/`.
- Thăng cấp đi theo bậc chức quan đang có. Không đặt hệ bậc mới.

## Dữ liệu (module `dlc.power`)

1. **Bảng nhiệm vụ của chúa công.** Migration mới, chạy lại được. Lưu:
   - thế lực, mẫu nhiệm vụ, tham số;
   - ngày giao, hạn chót;
   - trạng thái (đang làm / xong / trượt);
   - phần thưởng.
2. **Bảng tín nhiệm.** Lưu tín nhiệm với chúa công (0–100) và số lần trượt liên tiếp. Thêm cột vào bảng của module hoặc tạo bảng nhỏ.
3. **Mẫu nhiệm vụ.** Tham số lấy từ dữ liệu thật của ván. Mẫu nào thiếu capability thì không giao.

   | Mẫu | Việc | Điều kiện / tham số |
   | --- | --- | --- |
   | Vận lương | Mang N gạo hoặc lương thực tới thành X trước hạn | X là thành của thế lực, ưu tiên thành nguy hiểm |
   | Mua chiến mã | Nộp N chiến mã ở thủ phủ | — |
   | Đi sứ | Tới thành X của thế lực khác, nói chuyện với thái thú (có lựa chọn mới trong cảnh trò chuyện), rồi về báo | — |
   | Dẹp phỉ | Thắng một ổ thổ phỉ đang có | Dùng phần quân sự có sẵn |
   | Nộp cống | Nộp N quan | Mẫu dễ, cho người mới |

   N tăng theo cấp bậc.

## Luật

- **Giao việc.** Đầu mỗi tháng (`MonthClosed`), người đã đầu quân và chưa có nhiệm vụ đang làm thì nhận một nhiệm vụ hợp cấp. Hạn khoảng 20–30 ngày.
- **Hoàn thành.** Khi đủ điều kiện thì phát hiện tự động, nhưng chỉ trừ hàng hoặc tiền khi người chơi bấm "Trình lên" ở thủ phủ hoặc đúng thành đích.
- **Thưởng.** Công trạng (cộng vào danh vọng chính trị đang có), bổng lộc thêm, tín nhiệm +.
- **Thăng chức.** Đủ danh vọng cần và tín nhiệm từ 60 trở lên thì chúa công thăng lên chức kế tiếp, có cảnh báo tin.
- **Trượt.** Quá hạn thì tín nhiệm −. Trượt 3 lần liền thì bị đuổi khỏi thế lực (gọi `LeaveFaction`), ghi biên niên.
- **Rời thế lực** (tự rời hoặc bị đuổi): nhiệm vụ đang làm bị hủy.

## Giao diện

- **Cảnh giao việc.** Tới thủ phủ, hoặc mở "Quan phủ" khi đã đầu quân, thì có lựa chọn "Bái kiến chúa công".
  - Mở cảnh trò chuyện có sẵn với chân dung chúa công (`LeaderNpcId`).
  - Chúa công giao việc bằng lời của mình, hợp với nhân vật.
  - Có lựa chọn "Lĩnh mệnh". Không có lựa chọn từ chối; muốn thoát thì rời thế lực.
- **Sổ nhiệm vụ.** Nhiệm vụ đang làm hiện trong sổ nhiệm vụ cùng chỗ với ước nguyện NPC, gồm: việc, tiến độ, hạn, nút "Đi tới".
- **Bảng chính trị.** Màn chính trị hiện tín nhiệm và số lần trượt.

## Kiểm tra

Viết test cho:
- giao việc đúng một lần mỗi tháng và đúng cấp;
- không giao khi chưa đầu quân;
- vận lương: trình đủ thì xong, thiếu thì không trừ;
- thăng chức khi đủ điều kiện;
- trượt 3 lần liền thì bị đuổi;
- rời thế lực thì nhiệm vụ bị hủy;
- save cũ mở được;
- tắt `dlc.society` vẫn chạy.

## Nghiệm thu

- Build Release sạch, `dotnet test SimT.sln -c Release` qua hết, `node tools/check-scenes.mjs`, `--smoke`.
- Ảnh: cảnh chúa công giao việc, và sổ nhiệm vụ có nhiệm vụ chúa công.
- Báo cáo ngắn: ví dụ một tháng giao việc thật ở thế lực của Tào Tháo.
