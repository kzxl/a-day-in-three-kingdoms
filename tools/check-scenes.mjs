import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

globalThis.window = globalThis.window || {
  addEventListener() {},
  chrome: {}
};
globalThis.document = globalThis.document || {
  readyState: 'complete',
  getElementById() { return null; },
  addEventListener() {}
};

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, '..');
const scenesDir = path.join(rootDir, 'assets', 'scenes');

const { createFrame, clearFrame, createFx, composite } = await import('../assets/scenes/vendor/pixelfx/src/core/index.js');
const { buildEffectOptions, safeCreateFx, splitNarrativeSentences } = await import('../assets/scenes/duel-scene.js');
const { STEP_DUEL, STEP_CITY, SCALE_DUEL, SCALE_CITY, computeWalkFrame, createDuelFighterState, stepDuelState, stepCityWalkers } = await import('../assets/scenes/sprite-anim.js');
const { LABEL_POS, computeDefaultMapLuggage } = await import('../assets/scenes/map-scene.js');
const { MIN_NODE_RADIUS_PCT, ringRadiusPct, computeSocialNodePos } = await import('../assets/scenes/social-scene.js');
const { shortDossierName } = await import('../assets/scenes/dossier-scene.js');

import * as swordSlash from '../assets/scenes/vendor/pixelfx/src/fx/melee/sword-slash.js';
import * as thrustFx from '../assets/scenes/vendor/pixelfx/src/fx/melee/thrust.js';
import * as spearSweep from '../assets/scenes/vendor/pixelfx/src/fx/melee/spear-sweep.js';
import * as arrowFx from '../assets/scenes/vendor/pixelfx/src/fx/projectiles/arrow.js';
import * as criticalHit from '../assets/scenes/vendor/pixelfx/src/fx/impacts/critical-hit.js';
import * as hitFlash from '../assets/scenes/vendor/pixelfx/src/fx/impacts/hit-flash.js';
import * as damageNumber from '../assets/scenes/vendor/pixelfx/src/fx/ui/damage-number.js';
import * as critNumber from '../assets/scenes/vendor/pixelfx/src/fx/ui/crit-number.js';
import * as cherryBlossoms from '../assets/scenes/vendor/pixelfx/src/fx/weather/cherry-blossoms.js';
import * as fallingLeaves from '../assets/scenes/vendor/pixelfx/src/fx/weather/falling-leaves.js';
import * as snowFx from '../assets/scenes/vendor/pixelfx/src/fx/weather/snow.js';

const effects = [
  { name: 'weapons/sword-slash', mod: swordSlash, opts: { x: 240, y: 158, seed: 101 } },
  { name: 'weapons/thrust', mod: thrustFx, opts: { x: 240, y: 158, seed: 102 } },
  { name: 'weapons/spear-sweep', mod: spearSweep, opts: { x: 240, y: 158, seed: 103 } },
  { name: 'weapons/arrow', mod: arrowFx, opts: { x: 240, y: 158, seed: 104 } },
  { name: 'impacts/critical-hit', mod: criticalHit, opts: { x: 326, y: 162, seed: 105 } },
  { name: 'impacts/hit-flash (default)', mod: hitFlash, opts: { x: 326, y: 162, seed: 106 } },
  { name: 'impacts/hit-flash (no actor)', mod: hitFlash, opts: {} },
  { name: 'ui/damage-number', mod: damageNumber, opts: { x: 326, y: 118, value: 28, seed: 107 } },
  { name: 'ui/crit-number', mod: critNumber, opts: { x: 326, y: 114, value: 45, seed: 108 } },
  { name: 'weather/cherry-blossoms', mod: cherryBlossoms, opts: { x: 140, y: 135, seed: 11 } },
  { name: 'weather/falling-leaves', mod: fallingLeaves, opts: { x: 240, y: 135, seed: 29 } },
  { name: 'weather/snow', mod: snowFx, opts: { x: 340, y: 135, seed: 47 } }
];

const frame = createFrame(480, 270);
const outRgba = new Uint8ClampedArray(480 * 270 * 4);

for (const entry of effects) {
  assert.ok(
    entry.mod && (typeof entry.mod.create === 'function' || typeof entry.mod.draw === 'function'),
    `Missing create()/draw() in ${entry.name}`
  );
  const mergedOpts = buildEffectOptions(entry.mod, entry.opts);
  const rawFx = createFx(entry.mod, mergedOpts);
  const safeFx = safeCreateFx(entry.mod, entry.opts);
  assert.ok(rawFx, `createFx returned null for ${entry.name}`);
  assert.ok(safeFx, `safeCreateFx returned null for ${entry.name}`);

  for (let step = 0; step < 12; step++) {
    clearFrame(frame);
    rawFx.update(1 / 30);
    rawFx.draw(frame);
    safeFx.update(1 / 30);
    safeFx.draw(frame);
    composite(frame, outRgba, null, 1);
  }
}

// Verify manifest.json asset references exist on disk
const manifestPath = path.join(scenesDir, 'art', 'manifest.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
for (const [k, rel] of Object.entries(manifest.backgrounds || {})) {
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', rel)), `Missing background ${k}: ${rel}`);
}
for (const [k, rel] of Object.entries(manifest.sprites || {})) {
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', rel)), `Missing sprite ${k}: ${rel}`);
}
for (const [k, obj] of Object.entries(manifest.portraits || {})) {
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', obj.full)), `Missing portrait full ${k}: ${obj.full}`);
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', obj.face)), `Missing portrait face ${k}: ${obj.face}`);
}
for (const [k, rel] of Object.entries(manifest.anim || {})) {
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', rel)), `Missing anim strip ${k}: ${rel}`);
}
for (const [k, rel] of Object.entries(manifest.profile || {})) {
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', rel)), `Missing profile image ${k}: ${rel}`);
}
for (const [k, obj] of Object.entries(manifest.portraitFrames || {})) {
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', obj.blink)), `Missing portrait blink ${k}: ${obj.blink}`);
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', obj.talk)), `Missing portrait talk ${k}: ${obj.talk}`);
}

assert.equal(SCALE_DUEL, 2.85, 'Expected SCALE_DUEL = 2.85');
assert.equal(STEP_DUEL, 25.6, 'Expected STEP_DUEL = 25.6');
assert.equal(SCALE_CITY, 2, 'Expected SCALE_CITY = 2');
assert.equal(STEP_CITY, 18, 'Expected STEP_CITY = 18');
assert.equal(computeWalkFrame(0, STEP_DUEL, false), 4);
assert.equal(computeWalkFrame(STEP_DUEL * 2.1, STEP_DUEL, false), 6);
assert.equal(computeWalkFrame(0, STEP_DUEL, true), 11);

const testState = {
  stop: 0,
  sh: 0,
  waitResolve: null,
  fighters: [
    createDuelFighterState({ animPath: 'art/anim/guan_yu.png', animKey: 'guan_yu', hp: 100, maxHp: 100 }, 0),
    createDuelFighterState({ animPath: 'art/anim/zhang_fei.png', animKey: 'zhang_fei', hp: 100, maxHp: 100 }, 1)
  ]
};
testState.fighters[0].st = 'run';
testState.fighters[0].tx = 514;
for (let i = 0; i < 60; i++) stepDuelState(testState, 1 / 30);
assert.ok(testState.fighters[1].hp <= 100, 'Expected duel state machine to step cleanly');

const cityWalkers = [{ hasStrip: true, walk: true, x: 360, min: 230, max: 560, dir: 1, wait: 0, dist: 0, fr: 4, it: 0 }];
for (let i = 0; i < 4; i++) stepCityWalkers(cityWalkers, 1 / 30);
assert.equal(cityWalkers[0].fr, 5, 'Expected city walker to advance 1 walk frame after >= 18px');

const longNarrative = 'Hiệp 1: Lữ Khách vung thương đâm thẳng gây 28 sát thương! Hoa Hùng gầm lớn múa đại đao phản kích dữ dội gây 22 sát thương trên lôi đài Lạc Dương.';
const splitLines = splitNarrativeSentences(longNarrative, 90);
assert.ok(splitLines.length >= 2, 'Expected long narrative to split into >= 2 sentences');
for (const line of splitLines) {
  assert.ok(line.length <= 90, `Split sentence exceeds 90 chars (${line.length}): ${line}`);
  assert.ok(!line.includes('…'), `Split sentence must not truncate with ellipsis: ${line}`);
}

function rectsOverlap(a, b) {
  return a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
}

// Verify 20-citadel map labels do not overlap other labels OR city dots at 1076 and 1440 windows
const MAP_CITIES = [
  { id: 1, nameVi: 'Lạc Dương', x: 446.4, y: 236.0, pf: 1000 },
  { id: 2, nameVi: 'Trường An', x: 332.8, y: 247.2, pf: 1120 },
  { id: 3, nameVi: 'Nghiệp Thành', x: 507.2, y: 181.6, pf: 930 },
  { id: 4, nameVi: 'Trác Huyện', x: 559.2, y: 80.0, pf: 950 },
  { id: 5, nameVi: 'Quảng Tông', x: 532.8, y: 157.6, pf: 1050 },
  { id: 6, nameVi: 'Bình Nguyên', x: 573.6, y: 155.2, pf: 980 },
  { id: 7, nameVi: 'Hứa Xương', x: 491.2, y: 255.2, pf: 1020 },
  { id: 8, nameVi: 'Bắc Bình', x: 624.0, y: 67.2, pf: 1100 },
  { id: 9, nameVi: 'Hạ Phì', x: 623.2, y: 246.4, pf: 970 },
  { id: 10, nameVi: 'Tương Dương', x: 436.8, y: 318.4, pf: 1000 },
  { id: 11, nameVi: 'Xích Bích', x: 492.8, y: 388.8, pf: 1080 },
  { id: 12, nameVi: 'Giang Lăng', x: 438.4, y: 372.8, pf: 1030 },
  { id: 13, nameVi: 'Kiến Nghiệp', x: 648.8, y: 318.4, pf: 1150 },
  { id: 14, nameVi: 'Hán Trung', x: 272.8, y: 285.6, pf: 1060 },
  { id: 15, nameVi: 'Thành Đô', x: 178.4, y: 362.4, pf: 920 },
  { id: 16, nameVi: 'Uyển Thành', x: 448.8, y: 288.0, pf: 1020 },
  { id: 17, nameVi: 'Thọ Xuân', x: 585.6, y: 301.6, pf: 1060 },
  { id: 18, nameVi: 'Thượng Đảng', x: 467.2, y: 185.6, pf: 990 },
  { id: 19, nameVi: 'Tây Lương', x: 132.8, y: 130.4, pf: 1140 },
  { id: 20, nameVi: 'Giao Chỉ', x: 240.0, y: 592.0, pf: 1180 }
];

function mapLabelBox(c, layer, scale) {
  const pos = LABEL_POS[c.id] || 'bottom';
  const pct = Math.round(((c.pf || 1000) - 1000) / 10);
  const text = layer === 'price' ? `${c.nameVi} ${pct > 0 ? '+' : ''}${pct}%` : c.nameVi;
  const w = text.length * 7.2 + 10;
  const h = 17;
  let left = c.x - w / 2, top = c.y + 13;
  if (pos === 'top') { left = c.x - w / 2; top = c.y - 13 - h; }
  else if (pos === 'left') { left = c.x - 14 - w; top = c.y - h / 2; }
  else if (pos === 'right') { left = c.x + 14; top = c.y - h / 2; }
  return { left: left * scale, top: top * scale, right: (left + w) * scale, bottom: (top + h) * scale };
}

for (const winW of [1076, 1440]) {
  const vw = winW === 1076 ? 620 : 880;
  const vh = winW === 1076 ? 430 : 510;
  const scale = Math.min(1.15, Math.max(0.52, Math.min((vw - 12) / 800, (vh - 12) / 640)));
  for (const layer of ['city', 'price']) {
    const dots = MAP_CITIES.map((c) => ({
      city: c,
      box: { left: (c.x - 11) * scale, top: (c.y - 11) * scale, right: (c.x + 11) * scale, bottom: (c.y + 11) * scale }
    }));
    const labels = MAP_CITIES.map((c) => ({ city: c, box: mapLabelBox(c, layer, scale) }));
    for (let i = 0; i < labels.length; i++) {
      assert.ok(labels[i].box.left >= 0 && labels[i].box.right <= 800 * scale, `Map label out of canvas at ${winW}: ${labels[i].city.nameVi}`);
      for (let j = i + 1; j < labels.length; j++) {
        assert.ok(!rectsOverlap(labels[i].box, labels[j].box),
          `Map label overlap (${winW}, ${layer}): "${labels[i].city.nameVi}" vs "${labels[j].city.nameVi}"`);
      }
      for (let j = 0; j < dots.length; j++) {
        assert.ok(!rectsOverlap(labels[i].box, dots[j].box),
          `Map label overlaps city dot (${winW}, ${layer}): label "${labels[i].city.nameVi}" vs dot "${dots[j].city.nameVi}"`);
      }
    }
  }
}

// Verify Social scene: high-affinity node does not touch center "Bạn", "Âu Dương Phong Đúc" does not overlap ring labels, and nodes do not overlap each other
assert.ok(MIN_NODE_RADIUS_PCT >= 24, 'Expected MIN_NODE_RADIUS_PCT >= 24');
for (const [winW, mapW, mapH] of [[1076, 510, 480], [1440, 680, 540]]) {
  const centerBox = { left: mapW / 2 - 34, top: mapH / 2 - 34, right: mapW / 2 + 34, bottom: mapH / 2 + 54 };
  const ringLabels = [[35, 'Quen biết · 35'], [60, 'Thân thiết · 60'], [80, 'Tri kỷ · 80']].map(([aff, text]) => {
    const rPct = ringRadiusPct(aff);
    const cy = mapH * (0.5 - (rPct * 0.86) / 100);
    const w = text.length * 6.4 + 10;
    return { text, box: { left: mapW / 2 - w / 2, top: cy - 8, right: mapW / 2 + w / 2, bottom: cy + 8 } };
  });

  const sampleNodes = [
    { name: 'Trương Lão Bản', aff: 100 }, { name: 'Tả Phong', aff: 100 },
    { name: 'Tư Mã Ý', aff: 0 }, { name: 'Thái Diễm', aff: 0 },
    { name: 'Điêu Thuyền', aff: 0 }, { name: 'Trương Xuân Hoa', aff: 0 },
    { name: 'Tôn Diệu', aff: 0 }, { name: 'Âu Dương Phong Đúc', aff: 0 }
  ];
  const nodeBoxes = sampleNodes.map((n, idx) => {
    const { leftPct, topPct } = computeSocialNodePos(n.aff, idx, sampleNodes.length);
    const cx = (leftPct / 100) * mapW;
    const cy = (topPct / 100) * mapH;
    const lw = Math.max(54, n.name.length * 6.8);
    return {
      name: n.name,
      aff: n.aff,
      avatarBox: { left: cx - 27, top: cy - 27, right: cx + 27, bottom: cy + 27 },
      labelBox: { left: cx - lw / 2, top: cy + 29, right: cx + lw / 2, bottom: cy + 45 }
    };
  });
  for (let i = 0; i < nodeBoxes.length; i++) {
    const nb = nodeBoxes[i];
    assert.ok(!rectsOverlap(nb.avatarBox, centerBox) && !rectsOverlap(nb.labelBox, centerBox),
      `Social node "${nb.name}" (aff=${nb.aff}) touches center "Bạn" at ${winW}`);
    for (const rl of ringLabels) {
      assert.ok(!rectsOverlap(nb.avatarBox, rl.box) && !rectsOverlap(nb.labelBox, rl.box),
        `Social node "${nb.name}" overlaps ring label "${rl.text}" at ${winW}`);
    }
    for (let j = i + 1; j < nodeBoxes.length; j++) {
      const other = nodeBoxes[j];
      assert.ok(
        !rectsOverlap(nb.avatarBox, other.avatarBox) &&
        !rectsOverlap(nb.labelBox, other.avatarBox) &&
        !rectsOverlap(nb.avatarBox, other.labelBox) &&
        !rectsOverlap(nb.labelBox, other.labelBox),
        `Social node "${nb.name}" overlaps "${other.name}" at ${winW}`
      );
    }
  }
}

assert.equal(shortDossierName('Tào Chương (Tử Văn)'), 'Tào Chương');
assert.equal(shortDossierName('Biện Phu Nhân'), 'Biện Phu Nhân');
assert.equal(shortDossierName('Âu Dương Phong Đúc'), 'Âu Dương Phong Đúc');

const bagSample = [
  { itemId: 1, nameVi: 'Gạo', quantity: 100, basePrice: 12 },
  { itemId: 3, nameVi: 'Lụa', quantity: 24, basePrice: 85 }
];
const walkLug = computeDefaultMapLuggage(bagSample, 2, 40);
assert.equal(walkLug.selected.get(3), 24, 'Walk default should take all 24 high-value Lụa first');
assert.equal(walkLug.selected.get(1), 16, 'Walk default should fill remaining 16 capacity with Gạo');
const horseLug = computeDefaultMapLuggage(bagSample, 2, 20);
assert.equal(horseLug.selected.get(3), 20, 'Horse default (cap 20) should take 20 Lụa');
assert.equal(horseLug.selected.get(1), 0, 'Horse default (cap 20) should take 0 Gạo');

console.log(`check-scenes: verified ${effects.length} pixelfx effects, map dot/label layout (1076 & 1440), social layout, luggage defaults, and manifest assets OK.`);
