import argparse, json, pathlib, subprocess, sys
from array import array

ROOT = pathlib.Path(__file__).resolve().parents[2]
# Only these Kenney cues are used by Part A. Never execute source-folder files.
FILES = [
 ('music', 'city', 'biteme/chinese-game-music/Lotus Pond - Loop.wav', 'music/lotus_pond.ogg', 4),
 ('music', 'duel', 'biteme/chinese-game-music/Dragon Dance - Loop.wav', 'music/dragon_dance.ogg', 4),
 ('music', 'map', 'oga/asianoriental2.ogg', 'music/asian_oriental.ogg', 4),
 ('music', 'event', 'oga/orien.ogg', 'music/orien.ogg', 4),
 ('ambience', 'war', 'oga/horde_war_drums.wav', 'ambience/war_drums.ogg', 3),
 ('ambience', 'birds', 'oga/birds_and_wind_ambient.ogg', 'ambience/birds.ogg', 3),
 ('ambience', 'rain', 'oga/rain_on_window_loop.wav', 'ambience/rain.ogg', 3),
 ('ambience', 'wind', 'oga/wind_whoosh_loop.ogg', 'ambience/wind.ogg', 3),
 ('sfx', 'ui.click', 'kenney/ui-audio/Audio/click1.ogg', 'sfx/ui_click.ogg', 3),
 ('sfx', 'ui.book', 'kenney/rpg-audio/Audio/bookOpen.ogg', 'sfx/ui_book.ogg', 3),
 ('sfx', 'ui.turn', 'kenney/rpg-audio/Audio/bookFlip1.ogg', 'sfx/ui_turn.ogg', 3),
 ('sfx', 'ui.seal', 'kenney/rpg-audio/Audio/bookPlace1.ogg', 'sfx/ui_seal.ogg', 3),
 ('sfx', 'trade.coin', 'kenney/rpg-audio/Audio/handleCoins.ogg', 'sfx/trade_coin.ogg', 3),
]

def make_music_loop(source, dest, quality):
    # These two OGA songs are not supplied as loops. Decode with fresh timestamps,
    # trim only inaudible edges, then overlap the last/first two seconds.
    # Rotate to start at the end of the overlap so the final PCM boundary is continuous.
    decoded = subprocess.check_output(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-nostdin',
        '-i', str(source), '-af', 'asetpts=N/SR/TB', '-ar', '44100', '-ac', '2', '-f', 'f32le', 'pipe:1'])
    pcm = array('f')
    pcm.frombytes(decoded)
    if sys.byteorder != 'little': pcm.byteswap()
    first, last = 0, len(pcm)
    while first + 2 < last and max(abs(pcm[first]), abs(pcm[first + 1])) < .0001: first += 2
    while last - 2 > first and max(abs(pcm[last - 2]), abs(pcm[last - 1])) < .0001: last -= 2
    pcm = pcm[first:last]
    span = 2 * 44100 * 2
    if len(pcm) <= span * 2:
        raise RuntimeError(f'Music is too short for loop preparation: {source.name}')
    result = pcm[span:-span]
    frames = span // 2
    for i in range(span):
        alpha = (i // 2) / (frames - 1)
        result.append(pcm[len(pcm) - span + i] * (1 - alpha) + pcm[i] * alpha)
    if sys.byteorder != 'little': result.byteswap()
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-nostdin', '-y',
        '-f', 'f32le', '-ar', '44100', '-ac', '2', '-i', 'pipe:0', '-map_metadata', '-1',
        '-c:a', 'libvorbis', '-q:a', str(quality), str(dest)], input=result.tobytes(), check=True)


def main():
    parser = argparse.ArgumentParser(description='Import approved CC0 audio; merge the catalogue without removing Part B keys.')
    parser.add_argument('--source', type=pathlib.Path, default=pathlib.Path(r'D:\Hung DATA\Tool\_tq_audio\raw'))
    args = parser.parse_args()
    output = ROOT / 'assets/audio'
    catalogue_path = output / 'audio.json'
    catalogue = json.loads(catalogue_path.read_text(encoding='utf-8')) if catalogue_path.exists() else {}
    for group, key, source, target, quality in FILES:
        dest = output / target
        dest.parent.mkdir(parents=True, exist_ok=True)
        if group == 'music' and key in ('map', 'event'):
            make_music_loop(args.source / source, dest, quality)
        else:
            subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-nostdin', '-y',
                        '-i', str(args.source / source), '-map_metadata', '-1', '-vn',
                        '-af', 'asetpts=N/SR/TB', '-ar', '44100', '-ac', '2', '-c:a', 'libvorbis', '-q:a', str(quality), str(dest)], check=True)
        catalogue.setdefault(group, {})[key] = target
    catalogue_path.write_text(json.dumps(catalogue, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    size = sum(p.stat().st_size for p in output.rglob('*') if p.is_file())
    if size > 15 * 1024 * 1024:
        raise RuntimeError(f'Audio exceeds 15 MiB: {size} bytes')
    print(f'Imported {len(FILES)} files; {size} bytes')

if __name__ == '__main__':
    main()
