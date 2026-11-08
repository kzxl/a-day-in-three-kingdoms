# Prompt 18 — Đợt 6: Ước nguyện NPC

Đọc trước spec `docs/spec/nang-cap-tam-quoc.md`, phần "Luật chung" và "Đợt 6".

Làm trong worktree đã tạo sẵn, nhánh `feat/tq-06-uoc-nguyen`. Nhánh này đã có đợt 4 (quan hệ NPC–NPC). Commit trên nhánh này, không push.

Luật bắt buộc:
- File `.cs` mới không quá 400 dòng. File cũ dài thì thêm `partial` file mới.
- Không sửa kernel.
- Ảnh QA ghi vào `D:\Hung DATA\Tool\_tq_shots\06\`, không commit vào repo.

## Mục tiêu

NPC có điều muốn làm. Người chơi giúp được, và việc giúp có kết quả thấy rõ.

## Dữ liệu (module `dlc.character`)

1. Thêm bảng ước nguyện bằng migration mới, chạy lại được nhiều lần. Bảng ghi:
   - npc, mẫu ước nguyện, tham số (món hàng, số lượng, thành, npc đích);
   - ngày mở, hạn chót;
   - trạng thái: ẩn / đã lộ / đã nhận / xong / quá hạn / NPC tự xong.
2. Các mẫu ước nguyện, mỗi mẫu tham số hóa từ dữ liệu thật của ván:

   | Mẫu | Cần gì | Khi nào xong |
   | --- | --- | --- |
   | Mở tiệm | góp N quan | trả qua ví |
   | Tìm hàng hiếm | mang N món X tới | X là món hàng hiếm có trong danh mục |
   | Báo thù | NPC có quan hệ `rival` (đợt 4) | thắng đấu võ người đó, hoặc làm người đó mất danh vọng bằng hành động có sẵn |
   | Làm quan | — | người chơi có chức quan đủ cao và tiến cử, dùng dữ liệu chính trị có sẵn |
   | Tìm thầy | — | giới thiệu một NPC có kỹ năng cao hơn |
   | Lập gia đình | — | giới thiệu một NPC độc thân phù hợp |
   | Về quê | — | đưa NPC về thành quê; gia thần đi cùng thì tính |

   Mẫu nào thiếu dữ liệu, hoặc thiếu capability, thì không sinh.
3. Mỗi NPC có tối đa một ước nguyện chưa kết thúc. Mỗi tháng (`MonthClosed`), một số NPC được sinh ước nguyện mới:
   - có giới hạn tổng số ước nguyện đang mở;
   - dùng `IRng` để kết quả xác định theo seed.
4. Ước nguyện chuyển từ ẩn sang đã lộ khi thiện cảm với NPC đạt bậc đã có, ví dụ "tri kỷ". Ngưỡng đặt làm hằng số.
5. Quá hạn mà người chơi chưa giúp thì mô phỏng xét theo tỉ lệ: NPC tự xong hoặc thất bại. Cả hai trường hợp đều ghi vào biên niên nếu có `ISocietyChronicle`.

## Người chơi

1. **Nhận việc.** Cảnh trò chuyện có lựa chọn "Có điều gì muốn nhờ?" khi ước nguyện đã lộ. NPC nói ước nguyện bằng lời của mình, rồi người chơi chọn nhận hoặc từ chối.
2. **Sổ nhiệm vụ.**
   - Ước nguyện đã nhận hiện cùng chỗ với chuỗi nhiệm vụ khởi đầu (xem `ProgressionGatingContracts.cs`, `GameStrategicBriefingControl.cs` và `MainWindow.Strategic.cs`).
   - Mỗi ước nguyện ghi tên NPC, việc cần làm, tiến độ và hạn.
   - Có nút "Đi tới" mở bản đồ tới đúng thành nếu có.
3. **Hoàn thành.**
   - Phát hiện tự động khi đủ điều kiện, ví dụ đang ở đúng thành và có đủ hàng. Hàng hoặc tiền chỉ bị trừ khi người chơi bấm "Trao", không tự trừ.
   - Thưởng: thiện cảm lớn, một món quà theo đặc tính NPC (nếu đợt 5 chưa có thì quà theo nghề), có thể mời làm gia thần dù chưa đủ phí, ghi biên niên.
4. **Quá hạn sau khi đã nhận:** trừ thiện cảm nhẹ, có dòng báo.

## Kiểm tra

Viết test cho:
- sinh ước nguyện xác định theo seed và đúng giới hạn tổng số;
- không sinh mẫu thiếu dữ liệu;
- lộ ước nguyện đúng ngưỡng thiện cảm;
- nhận → trao → thưởng;
- quá hạn khi đã nhận và khi chưa nhận;
- "Báo thù" chỉ sinh khi NPC có quan hệ `rival`;
- save cũ mở lên không lỗi;
- tắt `dlc.society` thì không lỗi.

## Nghiệm thu

- Build Release sạch.
- `dotnet test SimT.sln -c Release` qua hết.
- `node tools/check-scenes.mjs` qua.
- `--smoke` qua.
- Có ảnh: cảnh trò chuyện có ước nguyện, sổ nhiệm vụ.
- Báo cáo ngắn, kèm ví dụ 3 ước nguyện thật sinh ra trong ván mới.
