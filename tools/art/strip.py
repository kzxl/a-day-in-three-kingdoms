import sys
from PIL import Image
import numpy as np
from scipy import ndimage
# usage: strip.py <prefix gy|zf> <key b|m> <out.png> <scale>
pre, keyc, out, SCALE = sys.argv[1], sys.argv[2], sys.argv[3], float(sys.argv[4])
CW, CH = 340, 170

def cells(path):
    im = np.array(Image.open(path).convert('RGB')).astype(int)
    H, W, _ = im.shape
    r, g, b = im[..., 0], im[..., 1], im[..., 2]
    key = {'b': (b > 150) & (b - r > 90) & (b - g > 70), 'g': (g > 150) & (g - r > 90) & (g - b > 70)}.get(keyc, (r - g > 46) & (b - g > 46))
    border = ndimage.binary_dilation(key, iterations=3)
    fg = ~key  # no opening: thin spear shafts are one or two pixels wide
    spill = fg & border & {'b': b > r + 30, 'g': g > r + 30}.get(keyc, (b > g + 14) & (r > g + 14))
    im[spill] = (im[spill] * 0.25).astype(int)
    res = []
    lab, n = ndimage.label(fg)
    for i in range(8):
        x0, y0 = (i % 4) * (W // 4), (i // 4) * (H // 2)
        sub = lab[y0:y0 + H // 2, x0:x0 + W // 4]
        ids, cnt = np.unique(sub[sub > 0], return_counts=True)
        best = ids[np.argmax(cnt)]
        keep = [best] + [k for k, c in zip(ids, cnt) if c > 150 and k != best and (lab == k).sum() == c]
        res.append((im.astype('uint8'), np.isin(lab, keep)))
    return res

def body_h(frame):
    a = frame[1]
    rows = np.nonzero(a.sum(axis=1) >= 30)[0]
    return rows.max() - rows.min()

def place(frame, sheet, idx, sc=None):
    scale = sc or SCALE
    rgb, a = frame
    ys, xs = np.nonzero(a)
    top, bot, l, r = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    h = bot - top
    band = a[top + int(h * .30):top + int(h * .70)]
    fx = int(np.median(np.nonzero(band)[1]))  # torso, ignores the weapon tip
    f = Image.fromarray(np.dstack([rgb, (a * 255).astype('uint8')]), 'RGBA').crop((l, top, r, bot))
    w2, h2 = max(1, round(f.width * scale)), max(1, round(f.height * scale))
    c = f.convert('RGB').resize((w2, h2), Image.BOX).quantize(colors=48, method=Image.MEDIANCUT, dither=Image.NONE).convert('RGB')
    c.putalpha(f.getchannel('A').resize((w2, h2), Image.BOX).point(lambda v: 255 if v > 120 else 0))
    px = idx * CW + CW // 2 - round((fx - l) * scale)
    cell = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
    cell.alpha_composite(c, (0, 0)) if False else None
    tmp = Image.new('RGBA', (CW * 3, CH), (0, 0, 0, 0))
    tmp.alpha_composite(c, (CW + CW // 2 - round((fx - l) * scale), max(0, CH - h2 - 2)))
    sheet.alpha_composite(tmp.crop((CW, 0, CW * 2, CH)), (idx * CW, 0))

import os
D1, D2 = ('g4', 'g4') if os.path.exists(f'g4/{pre}_misc.png') else ('g2', 'g3')
misc = cells(f'{D1}/{pre}_misc.png'); walk = cells(f'{D2}/{pre}_walk.png'); atk = cells(f'{D1}/{pre}_atk.png')
idle = [misc[0], misc[1], misc[3], misc[1]] if pre == 'gy' else misc[:4]
if pre == 'zf':
    atk = [atk[0], atk[1], atk[2], atk[2], atk[4], atk[5], atk[6], atk[7]]
seq = idle + walk + atk + misc[4:]
sheet = Image.new('RGBA', (CW * len(seq), CH), (0, 0, 0, 0))
wsc = SCALE * np.median([body_h(f) for f in idle]) / np.median([body_h(f) for f in walk])
print('idle body', np.median([body_h(f) for f in idle]), 'walk body', np.median([body_h(f) for f in walk]), 'walk scale', round(float(wsc), 3))
for i, f in enumerate(seq):
    place(f, sheet, i, wsc if 4 <= i <= 11 else None)
sheet.save(out, optimize=True)
print(out, sheet.size, len(seq))
