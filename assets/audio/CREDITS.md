# Âm thanh — Phần A

Các file phân phối dùng [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/).
Nguồn đã được duyệt trong `docs/prompts/27-am-thanh.md`; file gốc chỉ được đọc/chuyển đổi.

| File phân phối | File gốc | Tác giả, nguồn | Giấy phép |
| --- | --- | --- | --- |
| `music/lotus_pond.ogg` | `Lotus Pond - Loop.wav` | BiteMe Games — [Chinese Game Music, itch.io](https://bitemegames.itch.io/chinese-game-music) | CC0 1.0 |
| `music/dragon_dance.ogg` | `Dragon Dance - Loop.wav` | BiteMe Games — [Chinese Game Music, itch.io](https://bitemegames.itch.io/chinese-game-music) | CC0 1.0 |
| `music/asian_oriental.ogg` | `asianoriental2.ogg` | Tozan — [Asianoriental2, OpenGameArt](https://opengameart.org/content/asianoriental2) | CC0 1.0 |
| `music/orien.ogg` | `orien.ogg` | Tozan — [Orien, OpenGameArt](https://opengameart.org/content/orien) | CC0 1.0 |
| `ambience/war_drums.ogg` | `horde_war_drums.wav` | William Hector — [Horde War Drums loop, OpenGameArt](https://opengameart.org/content/horde-war-drums-loop) | CC0 1.0 |
| `ambience/birds.ogg` | `birds_and_wind_ambient.ogg` (Birds and Wind - Ambient) | Spring Spring; tiếng chim: isaiah658, syncopika, pauliuw — [OpenGameArt](https://opengameart.org/content/birds-and-wind-ambient-birds-wind-and-synth) | CC0 1.0 |
| `ambience/rain.ogg` | `rain_on_window_loop.wav` | alxl — [Rain on Window Loop, OpenGameArt](https://opengameart.org/content/rain-on-window-loop) | Chọn CC0 1.0 trong các giấy phép tác giả cung cấp |
| `ambience/wind.ogg` | `wind_whoosh_loop.ogg` | SketchMan3 — [wind whoosh loop, OpenGameArt](https://opengameart.org/content/wind-whoosh-loop) | CC0 1.0 |
| `sfx/ui_click.ogg` | `ui-audio/Audio/click1.ogg` | Kenney — [UI Audio](https://kenney.nl/assets/ui-audio) | CC0 1.0 |
| `sfx/ui_book.ogg` | `rpg-audio/Audio/bookOpen.ogg` | Kenney — [RPG Audio](https://kenney.nl/assets/rpg-audio) | CC0 1.0 |
| `sfx/ui_turn.ogg` | `rpg-audio/Audio/bookFlip1.ogg` | Kenney — [RPG Audio](https://kenney.nl/assets/rpg-audio) | CC0 1.0 |
| `sfx/ui_seal.ogg` | `rpg-audio/Audio/bookPlace1.ogg` | Kenney — [RPG Audio](https://kenney.nl/assets/rpg-audio) | CC0 1.0 |
| `sfx/trade_coin.ogg` | `rpg-audio/Audio/handleCoins.ogg` | Kenney — [RPG Audio](https://kenney.nl/assets/rpg-audio) | CC0 1.0 |

BiteMe: **Asset license: Creative Commons Zero v1.0 Universal** lấy từ trang itch.io nêu trên
(đối chiếu ngày 08/11/2026); `About.txt` trong gói không ghi giấy phép.

Chuyển đổi bằng `python tools/audio/import.py` và ffmpeg: OGG Vorbis stereo 44100 Hz,
nhạc q4, môi trường và hiệu ứng q3; bỏ metadata nguồn và chuẩn hóa timestamp.
Hai bài OGA (`asian_oriental`, `orien`) được bỏ khoảng lặng ở mép và hòa chồng đầu/cuối 2 giây để tạo vòng lặp;
hai bài BiteMe giữ nguyên bản Loop.
Chỉ nhập 5 tiếng Kenney đang dùng, không nhập tiếng quân reo cho chợ. Script giữ các khóa Phần B đã có.

Phần mềm phát âm ở host: [NAudio 2.2.1](https://github.com/naudio/NAudio/tree/v2.2.1)
và [NVorbis 0.10.5](https://github.com/NVorbis/NVorbis), đều MIT;
đây là giấy phép phần mềm, độc lập với CC0 của file âm.
