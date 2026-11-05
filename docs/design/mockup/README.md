# Mockup đã duyệt — bản gốc để đối chiếu

Đây là mã nguồn của mockup giao diện mà người chơi đã duyệt. Khi dựng client, **bám theo bố cục, kích thước, màu, chữ và chuyển động trong các file này**, không tự thiết kế lại.

Mỗi board có khung 1440×900. Ảnh trỏ thẳng tới `/assets/characters/...` trong repo.

| Board | File | Màn trong game |
|---|---|---|
| 1 · Màn mở đầu | `Main.dc.html` | `screens/title.js` |
| 2 · Tạo nhân vật | `CharacterCreate.dc.html` | màn tạo nhân vật (toàn màn hình, không phải modal) |
| 3 · Hội thoại 2.5D | `Dialogue.dc.html` | `screens/dialogue.js` + `parallax-stage.js` |
| 4 · Giao việc đầu tháng | `MonthPlan.dc.html` | màn giao việc (toàn màn hình) |
| 5 · Đơn đấu | `Duel.dc.html` | `screens/duel.js` |
| 6 · Bản đồ 13 châu | `Map.dc.html` | `screens/map.js` |
| 7 · Hồ sơ tướng | `Dossier.dc.html` | `screens/dossier.js` |
| HUD chung | `Hud.dc.html` | `components/hud.js` |

## Cách đọc file `.dc.html`

Đây là template của công cụ mockup, không chạy trực tiếp trong trình duyệt được. Đọc theo các quy ước sau:

- `<x-dc>`: gốc của board. `<helmet>` chứa `<style>` và font.
- `{{ten}}`: giá trị lấy từ `renderVals()` trong class `Component` ở cuối file.
- `<sc-for each="..." as="x">`: lặp danh sách. `<sc-if value="...">`: hiện khi giá trị đúng.
- `<dc-import name="Hud" active="map">`: nhúng HUD từ `Hud.dc.html`, với tab `active` được tô sáng.
- `href="CharacterCreate.dc.html"`: chuyển sang màn tương ứng.
- Logic trong `class Component` (state, công thức đơn đấu ở `Duel.dc.html`, dự báo thu chi ở `MonthPlan.dc.html`) **chỉ để minh hoạ**. Số liệu thật lấy từ server và `server/config/balance.json`.
