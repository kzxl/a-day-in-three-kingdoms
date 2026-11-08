"""Composite generated expression heads over decoded originals; verify saved WebP pixels.

Requires Pillow, numpy, opencv-python-headless. Raw input and QA stay outside the repo:
python tools/art/portrait-expressions.py --qa "D:/Hung DATA/Tool/_tq_shots/11"
Run with --verify-only to audit committed images without generated drafts.
The rectangle is [left, top, right, bottom), feathering stays strictly inside it.
"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'assets/scenes/art/portraits'
CONFIG = Path(__file__).with_name('portrait-head-regions.json')
MOODS = ('happy', 'angry', 'sad')


def align(original, generated):
    import cv2
    candidate = np.array(generated.resize(original.size, Image.Resampling.LANCZOS))
    base = np.array(original)
    # Match unchanged costume/hat details; facial landmarks intentionally differ.
    sift = cv2.SIFT_create(nfeatures=3500, contrastThreshold=.02)
    gray_a = cv2.cvtColor(base[:, :, :3], cv2.COLOR_RGB2GRAY)
    gray_b = cv2.cvtColor(candidate[:, :, :3], cv2.COLOR_RGB2GRAY)
    ka, da = sift.detectAndCompute(gray_a, base[:, :, 3])
    kb, db = sift.detectAndCompute(gray_b, candidate[:, :, 3])
    if da is None or db is None:
        raise ValueError('Not enough features to align generated portrait')
    matches = cv2.BFMatcher().knnMatch(db, da, k=2)
    good = [m for m, n in matches if m.distance < .75 * n.distance]
    if len(good) < 6:
        raise ValueError('Too few reliable alignment matches; review/repaint this draft')
    src = np.float32([kb[m.queryIdx].pt for m in good])
    dst = np.float32([ka[m.trainIdx].pt for m in good])
    cv2.setRNGSeed(11)
    matrix, inliers = cv2.estimateAffinePartial2D(src, dst, method=cv2.RANSAC, ransacReprojThreshold=3)
    if matrix is None or int(inliers.sum()) < 5:
        raise ValueError('Alignment failed; review/repaint this draft')
    aligned = cv2.warpAffine(candidate, matrix, original.size, flags=cv2.INTER_LANCZOS4)
    return Image.fromarray(aligned), matrix.tolist(), int(inliers.sum())


def composite(original, aligned, box, feather):
    x0, y0, x1, y1 = box
    h, w = y1-y0, x1-x0
    yy, xx = np.mgrid[:h, :w]
    edge = np.minimum.reduce([xx, yy, w-1-xx, h-1-yy])
    mask = np.uint8(np.clip(edge / feather, 0, 1) * 255)
    # Keep original silhouette and exact alpha; the edit rectangle is inside the face.
    source = aligned.crop(box).copy()
    source.putalpha(original.crop(box).getchannel('A'))
    result = original.copy()
    result.paste(source, (x0, y0), Image.fromarray(mask))
    result.putalpha(original.getchannel('A'))
    return result


def verify(original, final, box):
    a, b = np.array(original), np.array(final)
    if a.shape != b.shape:
        raise ValueError('Dimensions changed')
    changed = np.any(a != b, axis=2)
    outside = changed.copy()
    x0, y0, x1, y1 = box
    outside[y0:y1, x0:x1] = False
    count = int(outside.sum())
    if count:
        raise ValueError(f'{count} changed pixels outside head')
    inside = int(changed.sum())
    if inside < 100:
        raise ValueError('Expression is empty or unchanged')
    return count, inside


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--qa', type=Path, required=True)
    parser.add_argument('--verify-only', action='store_true')
    parser.add_argument('--available-only', action='store_true', help='Process only drafts currently present')
    args = parser.parse_args()
    args.qa.mkdir(parents=True, exist_ok=True)
    config = json.loads(CONFIG.read_text(encoding='utf-8'))
    report = []
    sheet = Image.new('RGB', (800, len(config['regions']) * 256), '#ddd6c8')
    draw = ImageDraw.Draw(sheet)
    faces = Image.new('RGB', (880, len(config['regions']) * 180), '#ddd6c8')
    fd = ImageDraw.Draw(faces)
    for row, (key, box) in enumerate(config['regions'].items()):
        original = Image.open(ART/key/'full.webp').convert('RGBA')
        variants = [original]
        for mood in MOODS:
            dest = ART/key/f'full_{mood}.webp'
            raw = args.qa/'raw'/f'{key}_{mood}.png'
            if args.available_only and not raw.exists():
                variants.append(original)
                continue
            metadata = {}
            if not args.verify_only:
                generated = Image.open(raw).convert('RGBA')
                aligned, matrix, inliers = align(original, generated)
                final = composite(original, aligned, box, config['feather'])
                # Lossy re-encoding would alter the body. exact preserves hidden RGB too.
                final.save(dest, 'WEBP', lossless=True, exact=True, method=6)
                metadata = {'alignment': matrix, 'inliers': inliers}
            final = Image.open(dest).convert('RGBA')
            outside, inside = verify(original, final, box)
            report.append({'character': key, 'expression': mood, 'outside_head_pixels': outside,
                           'inside_head_pixels': inside, 'bytes': dest.stat().st_size,
                           'original_bytes': (ART/key/'full.webp').stat().st_size, **metadata})
            variants.append(final)
        for col, picture in enumerate(variants):
            thumb = picture.copy(); thumb.thumbnail((190, 228))
            sheet.paste(thumb, (col*200+(200-thumb.width)//2, row*256+25), thumb)
            draw.text((col*200+8, row*256+6), key+' / '+('normal',*MOODS)[col], fill='#27221e')
            face = picture.crop(box); face.thumbnail((170, 145))
            # Scale faces to inspect expressions and seams at a readable size.
            face = picture.crop(box).resize((170,145), Image.Resampling.LANCZOS)
            faces.paste(face, (col*220+20,row*180+25),face)
            fd.text((col*220+8,row*180+6),key+' / '+('normal',*MOODS)[col],fill='#27221e')
    sheet.save(args.qa/'portrait-grid.jpg',quality=93)
    faces.save(args.qa/'face-grid.jpg',quality=95)
    (args.qa/'pixel-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(f'{len(report)} expressions verified; outside-head differences = 0 for every image')


if __name__ == '__main__':
    main()
