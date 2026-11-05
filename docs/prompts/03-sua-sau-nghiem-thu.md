# Prompt 03 — Sửa sau nghiệm thu cảnh diễn

Cùng nhánh, cùng luật của prompt 01. Đây là lỗi tìm thấy khi chạy game thật có WebView2. Build và test qua không bắt được, vì không có test nào chạy JS của cảnh.

## Cách tự kiểm (bắt buộc dùng)

Chạy game với `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222`, rồi nối CDP tới `http://127.0.0.1:9222/json` để bấm nút, đọc lỗi console, chụp `Page.captureScreenshot`. Chỉ báo xong khi đã xem ảnh chụp thật và console không còn lỗi.

## Lỗi

**A. Cảnh đấu dừng ngay hiệp 1 (nghiêm trọng).** `impacts/hit-flash` ném `Cannot read properties of undefined (reading 'mask')` tại `create` (dòng 34), gọi từ `spawnRoundVfx` (`duel-scene.js:75`). Cả cảnh đứng ở "Hiệp 1 / 4", sprite không diễn, số HP không đổi.
- Đọc `meta.options` của từng hiệu ứng và truyền đúng tham số.
- Hiệu ứng lỗi không được làm dừng cảnh: bắt lỗi quanh `createFx`, ghi log, diễn tiếp.
- Thêm script `tools/check-scenes.mjs` (chỉ dùng Node) nạp và chạy vài khung mọi hiệu ứng mà `duel-scene.js`, `city-scene.js` dùng; nối vào `verify.cmd`.

**B. Mọi nhân vật đều rơi vào archetype `commoner`.** Hoa Hùng (Võ 92, Tây Lương Kiêu Tướng) hiện thành cô nông dân và nói "Dân đen chúng tôi…". Sửa cả chọn ảnh lẫn chọn thoại:
- Phân loại từ dữ liệu có thật, theo thứ tự: `manifest.npc` theo `code`; từ khoá trong `role` (Tướng, Quân, Soái → general; Thương, Lão Bản → merchant; Sĩ, Mưu, Thái Thú, Thừa Tướng → scholar; Khăn Vàng, Tặc → rebel); nữ theo dữ liệu giới tính nếu có; rồi chỉ số (Võ cao → general, Trí/Chính cao → scholar); cuối cùng mới là commoner.
- Đặt phân loại này ở một chỗ duy nhất, có test: Hoa Hùng là general, Trương Lão Bản là merchant, Điêu Thuyền là lady, Tư Mã Ý là scholar.

**C. Chữ thô lộ ra:** `VALIANT` ở cảnh nói chuyện, `trung_nguyen` ở màn thành, emoji 🛡️🏹 ở cảnh đấu (từ `TroopBranchHelper.Icon`). Dịch sang tiếng Việt và thay emoji bằng icon SVG trong cảnh.

**D. Màn "Trong thành":**
- Tên nhân vật đang hiện sẵn và đè lên nhau. Chỉ hiện khi rê chuột, đúng như prompt 01.
- Điểm "Cổng thành" không thấy, "Quán trọ" bị đè. Đặt lại vị trí, các điểm không chồng lên nhau và không chồng lên nhân vật.
- Rải nhân vật theo chiều sâu, người xa nhỏ hơn. Dùng sprite đúng archetype (đã có sprite mới cho thường dân, xem `manifest.json`).

**E. Cảnh đấu:**
- Sprite quá nhỏ so với sàn đấu. Vẽ đúng kích thước gốc (cao khoảng 120 trên khung 480×270), chân đặt trên sàn.
- Ở chiều rộng cửa sổ khoảng 1088px, nút "[ESC] Đóng" và cột lựa chọn bị tràn ra ngoài. Sửa layout co được từ 1024 trở lên.
- Lọc `favicon.ico` 404: thêm `<link rel="icon" href="data:,">`.

## Gộp `origin/main`

Sau khi sửa xong, `git merge origin/main` vào nhánh này (có 13+ commit mới: bản đồ, kho, du ngoạn, tách partial class). Không rebase, không ép.
- Giải quyết xung đột theo hướng giữ cả hai: tính năng mới của main và cảnh diễn, control của nhánh.
- Nếu main có cửa sổ du ngoạn riêng thì nối vào cảnh, không làm mất nó.
- Nếu các lỗi `Ink`, golden, seed đã được sửa trên main thì giữ bản của main.
- Sau khi gộp chạy lại toàn bộ nghiệm thu.

## Nghiệm thu

Build sạch, test qua hết, `--smoke` qua. Bằng CDP: đi đủ đường Trong thành → nói chuyện với Hoa Hùng → đấu → hết trận → về; console không lỗi. Không push. Báo cáo kèm đường dẫn ảnh chụp.
