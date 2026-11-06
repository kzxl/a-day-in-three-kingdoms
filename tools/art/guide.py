import math
from PIL import Image, ImageDraw
import numpy as np
# side-view walk guide: 8 phases, red = near leg, blue = far leg
CW, CH, GROUND = 344, 384, 362
TH, SH = 92, 92
SW = 16  # thigh swing, degrees

def leg(t):
    th = SW * math.cos(2 * math.pi * t) + 2
    k = 6 + 50 * math.exp(-((t - .72) / .13) ** 2) + 14 * math.exp(-((t - .1) / .08) ** 2)
    return th, k

def pts(hip, t):
    th, k = leg(t % 1)
    a = math.radians(th)
    knee = (hip[0] + TH * math.sin(a), hip[1] + TH * math.cos(a))
    b = math.radians(th - k)
    foot = (knee[0] + SH * math.sin(b), knee[1] + SH * math.cos(b))
    return knee, foot

im = Image.new('RGB', (CW * 4, CH * 2), (235, 235, 235))
d = ImageDraw.Draw(im)
for i in range(8):
    t = i / 8
    cx, cy = (i % 4) * CW + CW // 2, (i // 4) * CH
    h0 = (0, 0)
    fr = pts(h0, t)[1]; fl = pts(h0, t + .5)[1]
    hipy = GROUND - max(fr[1], fl[1])
    hip = (cx - 10, cy + hipy)
    neck = (hip[0] + 8, hip[1] - 118)
    head = (neck[0] + 6, neck[1] - 34)
    for ph, col, w in ((t + .5, (40, 90, 220), 13), (t, (220, 40, 40), 15)):
        kn, ft = pts(hip, ph)
        d.line([hip, kn, ft], fill=col, width=w, joint='curve')
        d.line([ft, (ft[0] + 34, ft[1] + 2)], fill=col, width=w)
    d.line([hip, neck], fill=(30, 30, 30), width=20)
    d.ellipse([head[0] - 26, head[1] - 26, head[0] + 26, head[1] + 26], fill=(30, 30, 30))
    s = math.radians(14 * math.cos(2 * math.pi * (t + .5)))
    d.line([neck, (neck[0] + 96 * math.sin(s), neck[1] + 96 * math.cos(s))], fill=(120, 120, 120), width=11)
    d.line([(cx - 150, cy + GROUND + 4), (cx + 150, cy + GROUND + 4)], fill=(150, 150, 150), width=2)
im.save('walk_guide.png')
print('ok')
