import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { inflateSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';

globalThis.window = globalThis.window || {
  addEventListener() {},
  removeEventListener() {},
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
const {
  LABEL_POS,
  SEASON_MAX_PARTICLES,
  computeDefaultMapLuggage,
  computeFactionGrid,
  paintFactionCanvas,
  computeMapLayoutBoxes,
  createSeasonParticles
} = await import('../assets/scenes/map-scene.js');
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

// Verify manifest.json asset references exist on disk and portrait dimensions match spec
function readWebpDimensions(filePath) {
  const buf = fs.readFileSync(filePath);
  assert.equal(buf.toString('ascii', 0, 4), 'RIFF', `Expected RIFF header in ${filePath}`);
  assert.equal(buf.toString('ascii', 8, 12), 'WEBP', `Expected WEBP header in ${filePath}`);
  const chunk = buf.toString('ascii', 12, 16);
  if (chunk === 'VP8X') return { width: 1 + buf.readUIntLE(24, 3), height: 1 + buf.readUIntLE(27, 3) };
  if (chunk === 'VP8 ') return { width: buf.readUInt16LE(26) & 0x3fff, height: buf.readUInt16LE(28) & 0x3fff };
  if (chunk === 'VP8L') {
    const bits = buf.readUInt32LE(21);
    return { width: (bits & 0x3fff) + 1, height: ((bits >> 14) & 0x3fff) + 1 };
  }
  throw new Error(`Unsupported WEBP chunk ${chunk} in ${filePath}`);
}

function assertPortraitDir(portraitKey, isVariant = false) {
  const fullPath = path.join(scenesDir, 'art', 'portraits', portraitKey, 'full.webp');
  const facePath = path.join(scenesDir, 'art', 'portraits', portraitKey, 'face.webp');
  assert.ok(fs.existsSync(fullPath), `Missing portrait full for ${portraitKey}: ${fullPath}`);
  assert.ok(fs.existsSync(facePath), `Missing portrait face for ${portraitKey}: ${facePath}`);
  const fullDim = readWebpDimensions(fullPath);
  const faceDim = readWebpDimensions(facePath);
  assert.equal(fullDim.height, 900, `Expected full.webp height 900 for ${portraitKey}, got ${fullDim.width}x${fullDim.height}`);
  if (isVariant) {
    assert.ok(fullDim.width >= 520 && fullDim.width <= 640, `Expected variant ${portraitKey} full.webp width 520..640, got ${fullDim.width}`);
  }
  assert.equal(faceDim.width, 160, `Expected face.webp width 160 for ${portraitKey}, got ${faceDim.width}x${faceDim.height}`);
  assert.equal(faceDim.height, 160, `Expected face.webp height 160 for ${portraitKey}, got ${faceDim.width}x${faceDim.height}`);
}

const manifestPath = path.join(scenesDir, 'art', 'manifest.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
if (manifest.player) {
  assertPortraitDir(manifest.player.portrait);
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', 'sprites', `${manifest.player.sprite}.png`)), `Missing player sprite ${manifest.player.sprite}`);
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', manifest.player.anim)), `Missing player anim ${manifest.player.anim}`);
}
for (const [code, entry] of Object.entries(manifest.npc || {})) {
  assertPortraitDir(entry.portrait);
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', 'sprites', `${entry.sprite}.png`)), `Missing NPC sprite for ${code}: ${entry.sprite}`);
}
for (const [arch, entry] of Object.entries(manifest.archetype || {})) {
  assertPortraitDir(entry.portrait);
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', 'sprites', `${entry.sprite}.png`)), `Missing archetype sprite for ${arch}: ${entry.sprite}`);
  assert.ok(Array.isArray(entry.variants) && entry.variants.length === 3, `Expected 3 portrait variants on archetype ${arch}`);
  for (const vKey of entry.variants) {
    assertPortraitDir(vKey, vKey !== entry.portrait);
  }
}
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
for (const [key, variants] of Object.entries(manifest.portraitExpressions || {})) {
  for (const [expression, rel] of Object.entries(variants)) {
    assert.ok(['happy', 'angry', 'sad'].includes(expression), `Unknown portrait expression ${expression}`);
    assert.equal(rel, `portraits/${key}/full_${expression}.webp`);
    assert.ok(fs.existsSync(path.join(scenesDir, 'art', rel)), `Missing expression ${key}/${expression}: ${rel}`);
    const exprDim = readWebpDimensions(path.join(scenesDir, 'art', rel));
    assert.equal(exprDim.height, 900, `Expected expression ${rel} height 900, got ${exprDim.height}`);
  }
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
for (const [k, rel] of Object.entries(manifest.map || {})) {
  assert.ok(fs.existsSync(path.join(scenesDir, 'art', rel)), `Missing map asset ${k}: ${rel}`);
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

// Verify faction territory canvas painting on sample payload & 1076px legend non-overlap
const sampleFactionCities = MAP_CITIES.map((c, idx) => ({
  ...c,
  factionId: (idx % 3) + 1,
  factionNameVi: ['Tào Ngụy', 'Thục Hán', 'Đông Ngô'][idx % 3],
  factionColor: ['#ef4444', '#22c55e', '#3b82f6'][idx % 3],
  governorNameVi: 'Thái thú'
}));

let canvasPutCount = 0;
const mockFactionCanvas = {
  width: 800,
  height: 640,
  dataset: {},
  getContext() {
    return {
      imageSmoothingEnabled: false,
      imageSmoothingQuality: 'low',
      clearRect() {},
      putImageData(img) {
        if (img && img.data && img.data.some((v) => v > 0)) canvasPutCount += 1;
      },
      drawImage() {
        canvasPutCount += 1;
      }
    };
  }
};

const paintedGrid = paintFactionCanvas(mockFactionCanvas, sampleFactionCities, true);
assert.ok(paintedGrid.paintedCount > 500, 'Expected sample faction payload to paint territory cells');
assert.ok(paintedGrid.borderCount > 20, 'Expected adjacent faction territories to draw border cells');
assert.ok(canvasPutCount >= 1, 'Expected faction canvas 2D context to be drawn');
assert.equal(mockFactionCanvas.dataset.visible, '1');
assert.ok(
  paintedGrid.paintedCount < paintedGrid.gridW * paintedGrid.gridH * 0.75,
  'Expected distant sea and wilderness outside radius limit to remain unpainted'
);

const emptyGrid = computeFactionGrid(MAP_CITIES.map((c) => ({ ...c, factionId: 0, factionColor: '' })));
assert.equal(emptyGrid.paintedCount, 0, 'Expected cities without faction to leave territory canvas unpainted');

const layout1076 = computeMapLayoutBoxes(1076, 12);
assert.ok(
  !rectsOverlap(layout1076.factionLegendBox, layout1076.rightPanelBox),
  'Faction legend must not overlap city panel at 1076px'
);
assert.ok(
  !rectsOverlap(layout1076.factionLegendBox, layout1076.goButtonBox),
  'Faction legend must not overlap Go (Lên đường) button at 1076px'
);

for (const sKey of ['spring', 'autumn', 'winter']) {
  const pts = createSeasonParticles(sKey);
  assert.ok(pts.length > 0 && pts.length <= SEASON_MAX_PARTICLES, `Expected 1..60 particles for ${sKey}`);
}
assert.equal(createSeasonParticles('summer').length, 0, 'Expected 0 particles in summer');

// Event cards must show all three tavern rumors, escaping player-facing source text.
const { formatCityNarrative } = await import('../assets/scenes/city-scene.js');
assert.equal(formatCityNarrative('One\n\nTwo\nThree'), 'One<br><br>Two<br><br>Three');
assert.equal(formatCityNarrative('<script>'), '&lt;script&gt;');

const { splitEventSentences } = await import('../assets/scenes/event-scene.js');
const eventSentences = splitEventSentences('Thiên Công Tướng Quân Trương Giác phất cờ Thái Bình Đạo tại Quảng Tông. Hàng chục vạn nông dân đội khăn vàng nổi dậy khắp Ký Châu. Bắc Quân Trung Lang Tướng Lư Thực vây hãm thành Quảng Tông suốt nhiều tháng ròng.');
assert.equal(eventSentences.length, 3, 'Expected 3 chronicle sentences');

const { clampPersuasion, formatTurnCounter, renderDebateScene } = await import('../assets/scenes/debate-scene.js');
assert.equal(clampPersuasion(50), 50);
assert.equal(clampPersuasion(120), 100);
assert.equal(clampPersuasion(-15), 0);
assert.equal(formatTurnCounter(2, 5), 'Lượt 2 / 5');

const mockDebateContainer = {
  innerHTML: '',
  querySelector() { return null; },
  querySelectorAll() { return []; }
};
renderDebateScene(mockDebateContainer, {
  sceneId: 'debate:1:haggle',
  npcId: 1,
  npcNameVi: 'Trương Lão Bản',
  npcNameHan: '張',
  roleVi: 'Thương nhân',
  locationNameVi: 'Lạc Dương',
  locationHan: '洛',
  topicTitleVi: 'Mặc cả lô hàng',
  persuasion: 68,
  turn: 2,
  maxTurns: 5,
  isFinished: false,
  latestSpeakerVi: 'Trương Lão Bản',
  latestLineVi: 'Lụa Thục năm nay vận chuyển qua bao ải, bớt thế sao đặng.',
  traits: [
    { code: 'greedy', nameVi: 'Tham tài', hintVi: 'Lợi ×1.5', revealed: true },
    { code: 'hidden_1', nameVi: '???', hintVi: 'Đặc tính ẩn', revealed: false }
  ],
  choices: [
    { id: 'arg:Reason', labelVi: 'Lụa Thục năm nay được mùa, giá ấy đã quá cao.', hintVi: 'Lý · Mưu trí', enabled: true },
    { id: 'arg:Profit', labelVi: 'Ta đặt cọc thêm tiền mặt tại bàn, đôi bên cùng lợi.', hintVi: 'Lợi · 36 quan', enabled: true },
    { id: 'arg:Prestige', labelVi: 'Uy tín của ta khắp thành đều rõ, xin chớ câu nệ.', hintVi: 'Uy · Danh vọng', enabled: true },
    { id: 'concede', labelVi: 'Nhượng bộ', hintVi: 'Hòa nhẹ', enabled: true }
  ]
}, null);
assert.ok(mockDebateContainer.innerHTML.includes('tk-talk-stage') && mockDebateContainer.innerHTML.includes('tk-dialog-paper'), 'debate-scene must reuse tk-talk-stage and tk-dialog-paper');
assert.ok(mockDebateContainer.innerHTML.includes('tk-trait-row') && mockDebateContainer.innerHTML.includes('tk-mood-pill'), 'debate-scene must reuse tk-trait-row and tk-mood-pill');
assert.ok(mockDebateContainer.innerHTML.includes('Lượt 2 / 5'), 'debate-scene must render turn counter');

// Verify Batch 5 character trait pills in dossier-scene & talk-scene
const dossierJs = fs.readFileSync(path.join(scenesDir, 'dossier-scene.js'), 'utf8');
const dossierCss = fs.readFileSync(path.join(scenesDir, 'dossier-scene.css'), 'utf8');
const talkJs = fs.readFileSync(path.join(scenesDir, 'talk-scene.js'), 'utf8');
const sceneCss = fs.readFileSync(path.join(scenesDir, 'scene.css'), 'utf8');
const debateCss = fs.readFileSync(path.join(scenesDir, 'debate-scene.css'), 'utf8');
const indexHtml = fs.readFileSync(path.join(scenesDir, 'index.html'), 'utf8');
const bridgeJs = fs.readFileSync(path.join(scenesDir, 'bridge.js'), 'utf8');
assert.ok(dossierJs.includes('dossier-chip is-trait') && dossierJs.includes('title='), 'dossier-scene.js must render traits as dossier chips with a title');
assert.ok(dossierCss.includes('.dossier-traits') && dossierCss.includes('flex-wrap: wrap'), 'dossier-scene.css must wrap trait pills to prevent 1076px overflow');
assert.ok(!dossierCss.includes('gradient('), 'dossier-scene.css must not use custom gradients');
assert.ok(!/is-unmet[^`]*?<img/.test(dossierJs.slice(dossierJs.indexOf('is-unmet'), dossierJs.indexOf('is-unmet') + 400)), 'dossier-scene.js must not load portraits for unmet NPCs (they show only a ?)');
assert.ok(dossierJs.includes('Võ tướng lục') && dossierJs.includes('Sử biến') && dossierJs.includes('dossier-tab'), 'dossier-scene.js must include the Hồ sơ / Võ tướng lục / Sử biến tabs');
assert.ok(dossierJs.includes('loading="lazy"') && dossierJs.includes('data-src='), 'dossier-scene.js must lazy-load 300 portraits');
assert.ok(dossierJs.includes('Không có mặt') && dossierJs.includes('replay:'), 'dossier-scene.js must distinguish missed historical events and support replay');
assert.ok(talkJs.includes('tk-trait-row') && talkJs.includes('tk-mood-pill'), 'talk-scene.js must render traits as paper mood pills');
assert.ok(sceneCss.includes('.tk-trait-row') && sceneCss.includes('flex-wrap: wrap'), 'scene.css must wrap talk trait pills to prevent 1076px overflow');
assert.ok(!debateCss.includes('gradient('), 'debate-scene.css must not use custom gradients');
assert.ok(indexHtml.includes('debate-scene.css') && indexHtml.includes('debate-scene.js') && indexHtml.includes('stage-debate'), 'index.html must wire debate scene');
assert.ok(bridgeJs.includes("case 'scene:debate':"), 'bridge.js must handle scene:debate');

console.log(`check-scenes: verified ${effects.length} pixelfx effects, map dot/label/faction layout (1076 & 1440), social layout, luggage defaults, tavern rumor cards, historical event scene, character trait pills, and manifest assets OK.`);


// Travellers use connected roads, distinct SVG silhouettes, and stay around saved progress.
const { travellerPath, travellerPosition, TRAVELLER_ROWS, createTravellerMarker, placeTravellers, travellerBox } = await import('../assets/scenes/map-travellers.js');
const travellerCities = [{ id: 1, nameVi: 'A', x: 0, y: 0 }, { id: 2, nameVi: 'B', x: 100, y: 0 }, { id: 3, nameVi: 'C', x: 100, y: 100 }];
const travellerRoutes = [{ fromId: 1, toId: 3, days: 1, blockedReasonVi: 'Phong tỏa' }, { fromId: 1, toId: 2, days: 2 }, { fromId: 2, toId: 3, days: 2 }];
const traveller = { id: 'rival:1', kind: 'rival', fromId: 1, toId: 3, progress: .5, labelVi: 'Thương nhân' };
assert.deepEqual(travellerPath(traveller, travellerCities, travellerRoutes).map(c => c.id), [1, 2, 3]);
assert.deepEqual(travellerPath({ ...traveller, kind: 'army' }, travellerCities, travellerRoutes).map(c => c.id), [1, 3]);
assert.deepEqual(travellerPosition(travellerCities, .75), { x: 100, y: 50, t: .5, seg: '2-3', dx: 0, dy: 1 });
// Same kind on the same segment shares one marker with a count.
const grouped = placeTravellers([{ path: travellerCities, progress: .75, kind: 'rival' }, { path: travellerCities, progress: .8, kind: 'rival' }, { path: travellerCities, progress: .75, kind: 'npc' }]);
assert.equal(grouped[0].count, 2); assert.equal(grouped[1], null);
assert.equal(new Set(Object.values(TRAVELLER_ROWS)).size, 4);
const originalCreateNS = document.createElementNS;
document.createElementNS = (_, tag) => ({ tag, attributes: {}, children: [], setAttribute(k, v) { this.attributes[k] = v; }, append(...nodes) { this.children.push(...nodes); }, addEventListener() {} });
const marker = createTravellerMarker(traveller, 'A → B → C');
assert.equal(marker.attributes['data-kind'], 'rival');
assert.ok(marker.children.some(n => n.tag === 'svg' && n.children.some(c => c.tag === 'image' && c.attributes.href === 'art/map/travellers.png')));
assert.ok(marker.children.some(n => n.tag === 'title' && n.textContent.includes('A → B → C')));
document.createElementNS = originalCreateNS;
for (const width of [1076, 1440]) {
  const boxes = computeMapLayoutBoxes(width, 12);
  assert.ok(!rectsOverlap(boxes.factionLegendBox, boxes.rightPanelBox));
  assert.ok(!rectsOverlap(boxes.factionLegendBox, boxes.goButtonBox));
  assert.ok(boxes.factionLegendBox.top >= boxes.viewportBox.top);
}
// Decode the final RGBA PNGs, including PNG row filters, without an external dependency.
function readTravellerPng(rel) {
  const buf = fs.readFileSync(path.join(scenesDir, 'art', rel));
  assert.equal(buf.subarray(1, 4).toString(), 'PNG');
  assert.equal(buf.readUInt32BE(16), 192);
  assert.equal(buf.readUInt32BE(20), 256);
  assert.equal(buf[24], 8); assert.equal(buf[25], 6); assert.equal(buf[28], 0);
  const chunks = [];
  for (let p = 8; p < buf.length;) {
    const len = buf.readUInt32BE(p);
    if (buf.toString('ascii', p + 4, p + 8) === 'IDAT') chunks.push(buf.subarray(p + 8, p + 8 + len));
    p += len + 12;
  }
  const raw = inflateSync(Buffer.concat(chunks)), out = Buffer.alloc(192 * 256 * 4), stride = 192 * 4;
  for (let y = 0; y < 256; y++) {
    const filter = raw[y * (stride + 1)];
    for (let x = 0; x < stride; x++) {
      const i = y * stride + x, a = x >= 4 ? out[i - 4] : 0, b = y ? out[i - stride] : 0;
      const c = y && x >= 4 ? out[i - stride - 4] : 0;
      const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
      const predictor = [0, a, b, Math.floor((a + b) / 2), pa <= pb && pa <= pc ? a : pb <= pc ? b : c][filter];
      assert.ok(predictor !== undefined);
      out[i] = (raw[y * (stride + 1) + x + 1] + predictor) & 255;
    }
  }
  return out;
}
for (const key of ['travellers', 'travellers_army_flag']) {
  assert.ok(manifest.map[key], `Missing map manifest ${key}`);
  const pixels = readTravellerPng(manifest.map[key]);
  for (let i = 0; i < pixels.length; i += 4) {
    assert.ok(!pixels[i + 3] || !(pixels[i] - pixels[i + 1] > 40 && pixels[i + 2] - pixels[i + 1] > 40), 'Magenta fringe remains');
  }
  for (let row = key === 'travellers' ? 0 : 3; row < 4; row++) {
    for (let col = 0; col < 2; col++) {
      const occupied = [];
      for (let y = row * 64; y < (row + 1) * 64; y++) for (let x = col * 96; x < (col + 1) * 96; x++) {
        if (pixels[(y * 192 + x) * 4 + 3]) occupied.push({ x, y });
      }
      assert.ok(occupied.length > 10, 'Empty sprite or flag frame');
      if (key === 'travellers') assert.equal(Math.max(...occupied.map(p => p.y)) % 64, 59, 'Frames must share ground baseline');
    }
  }
}
for (const progress of [0, .01, .49, .5, .51, .99, 1]) {
  const p = travellerPosition(travellerCities, progress);
  assert.ok(p.t >= .14 && p.t <= .86, 'Traveller entered city endpoint margin');
}
const road = [{ x: 100, y: 180 }, { x: 650, y: 180 }];
const obstacles = [{ left: 350, right: 400, top: 145, bottom: 185 }];
const placements = placeTravellers(Array.from({ length: 8 }, () => ({ path: road, progress: .5 })), obstacles);
// A crowded road shows what fits and hides the rest; nothing may drift more than one lane off its road.
assert.ok(placements.filter(Boolean).length >= 4, 'A crowded road should still show several travellers');
placements.filter(Boolean).forEach(p => assert.ok(Math.abs(p.y - 180) <= 10.5, 'Traveller drifted off its road'));
placements.filter(Boolean).forEach((p, i, shown) => {
  assert.ok(!obstacles.some(b => rectsOverlap(travellerBox(p, 54), b)));
  shown.slice(0, i).forEach(q => {
    assert.ok(Math.hypot(p.x - q.x, p.y - q.y) >= 24);
    assert.ok(!rectsOverlap(travellerBox(p, 54), travellerBox(q, 54)));
  });
});
console.log('check-scenes: sprite dimensions, alpha/fringe, frames, connected roads, endpoint margins and collision separation OK.');

// Portrait expressions follow the current line and fall back for sparse/unknown values.
const { expressionForLine, portraitForLine, createPortraitSwapper } = await import('../assets/scenes/portrait-expressions.js');
const expressionScene = { lineExpressions: ['happy', '', 'angry', 'unknown'], expressionPaths: { happy: 'happy.webp' } };
assert.equal(expressionForLine(expressionScene, 0), 'happy');
assert.equal(expressionForLine(expressionScene, 1), 'normal');
assert.equal(expressionForLine(expressionScene, 3), 'normal');
assert.equal(portraitForLine(expressionScene, 0, 'full.webp'), 'happy.webp');
assert.equal(portraitForLine(expressionScene, 2, 'full.webp'), 'full.webp');
assert.equal(portraitForLine({}, 20, 'full.webp'), 'full.webp');
const { createPortraitAnimator } = await import('../assets/scenes/sprite-anim.js');
const base = { style: {} }, blink = { style: {} }, mouth = { style: {} }, line = {};
const animator = createPortraitAnimator({ baseEl: base, blinkEl: blink, talkEl: mouth, lineEl: line });
animator.setFramesEnabled(false);
animator.setLine('Tin dữ từ Kinh Châu.');
for (let i = 0; i < 120; i++) animator.step(.05);
assert.equal(base.style.opacity, '1');
assert.equal(blink.style.opacity, '0');
assert.equal(mouth.style.opacity, '0');
assert.equal(line.textContent, '“Tin dữ từ Kinh Châu.”');
animator.setFramesEnabled(true);
animator.setLine('Xin chào.');
assert.equal(mouth.style.opacity, '1');

// Late image loads must not overwrite a newer line or mutate an unmounted scene.
const originalImage = globalThis.Image;
const originalMatchMedia = window.matchMedia;
let reducedMotion = false;
window.matchMedia = () => ({ matches: reducedMotion });
const pendingImages = [];
globalThis.Image = class { constructor() { pendingImages.push(this); } };
const overlays = [];
const swapBase = {
  src: 'normal.webp',
  getAttribute() { return this.src; },
  cloneNode() { return { style: {}, classList: { add() {} }, removeAttribute() {}, setAttribute() {}, remove() { this.removed = true; } }; },
  parentElement: { appendChild(n) { overlays.push(n); } }
};
const swapper = createPortraitSwapper(swapBase);
swapper.set('happy.webp', 'normal.webp');
swapper.set('sad.webp', 'normal.webp');
pendingImages[0].onload();
assert.equal(swapBase.src, 'normal.webp');
pendingImages[1].onload();
assert.equal(swapBase.src, 'sad.webp');
assert.equal(overlays.length, 1);
swapper.set('angry.webp', 'normal.webp');
assert.equal(overlays[0].removed, true);
reducedMotion = true;
pendingImages[2].onload();
assert.equal(swapBase.src, 'angry.webp');
assert.equal(overlays.length, 1, 'Reduced motion must swap without an animated overlay');
swapper.set('missing.webp', 'normal.webp');
pendingImages[3].onerror();
assert.equal(swapBase.src, 'normal.webp');
swapper.set('late.webp');
swapper.dispose();
pendingImages[4].onload();
assert.equal(swapBase.src, 'normal.webp');
globalThis.Image = originalImage;
window.matchMedia = originalMatchMedia;
console.log('check-scenes: expression fallback, normal-only mouth/blink, image-load races, unmount and reduced motion OK.');

// Part A/B shared sound catalogue and WebView contract.
const audioRoot = path.join(rootDir, 'assets', 'audio');
const audioCatalog = JSON.parse(fs.readFileSync(path.join(audioRoot, 'audio.json'), 'utf8'));
for (const group of ['music', 'ambience', 'sfx']) {
  assert.ok(audioCatalog[group] && typeof audioCatalog[group] === 'object', `Missing audio group: ${group}`);
  for (const [key, relative] of Object.entries(audioCatalog[group])) {
    const full = path.resolve(audioRoot, relative);
    assert.ok(full.startsWith(audioRoot + path.sep), `Unsafe audio path: ${key}`);
    assert.ok(fs.existsSync(full), `Missing audio: ${key} -> ${relative}`);
    assert.ok(group === 'sfx' ? /\.(ogg|wav)$/i.test(relative) : /\.ogg$/i.test(relative), `Bad audio format: ${key}`);
  }
}
const { SimTBridge: audioBridge } = await import('../assets/scenes/bridge.js');
assert.equal(typeof audioBridge.playSfx, 'function');
const oldWebview = window.chrome.webview;
const soundMessages = [];
window.chrome.webview = { postMessage(message) { soundMessages.push(message); } };
audioBridge.playSfx('fx.sword_slash');
audioBridge.playSfx('');
audioBridge.playSfx(null);
audioBridge.playSfx('x'.repeat(129));
assert.deepEqual(soundMessages, [{ type: 'sfx', id: 'fx.sword_slash' }]);
window.chrome.webview = oldWebview;
console.log('check-scenes: audio catalogue files and SimTBridge.playSfx contract OK.');
{ // Part B checks keep their own names apart from the shared catalogue check above.
// Verify Prompt 27 Part B: pixel effects map to audio keys, all audio files exist, WAV SFX < 1.5s, and scenes emit playSfx.
const { SimTBridge } = await import('../assets/scenes/bridge.js');
const { FX_SFX_KEYS, sfxKeyForFx, pushFx, renderDuelScene } = await import('../assets/scenes/duel-scene.js');
const { renderEventScene } = await import('../assets/scenes/event-scene.js');

const audioDir = path.join(rootDir, 'assets', 'audio');
const audioCatalogPath = path.join(audioDir, 'audio.json');
assert.ok(fs.existsSync(audioCatalogPath), `Missing audio catalog: ${audioCatalogPath}`);
const audioCatalog = JSON.parse(fs.readFileSync(audioCatalogPath, 'utf8'));
assert.ok(audioCatalog && typeof audioCatalog === 'object', 'audio.json must be a JSON object');
assert.ok(audioCatalog.sfx && typeof audioCatalog.sfx === 'object', 'audio.json must contain sfx map');
assert.ok(audioCatalog.ambience && typeof audioCatalog.ambience === 'object', 'audio.json must contain ambience map');

function readWavMeta(filePath) {
  const buf = fs.readFileSync(filePath);
  assert.equal(buf.toString('ascii', 0, 4), 'RIFF', `Expected RIFF header in ${filePath}`);
  assert.equal(buf.toString('ascii', 8, 12), 'WAVE', `Expected WAVE header in ${filePath}`);
  let offset = 12;
  let fmt = null;
  let dataBytes = 0;
  let maxSample = 0;
  while (offset + 8 <= buf.length) {
    const id = buf.toString('ascii', offset, offset + 4);
    const size = buf.readUInt32LE(offset + 4);
    const body = offset + 8;
    if (id === 'fmt ') {
      fmt = {
        audioFormat: buf.readUInt16LE(body),
        channels: buf.readUInt16LE(body + 2),
        sampleRate: buf.readUInt32LE(body + 4),
        byteRate: buf.readUInt32LE(body + 8),
        bitsPerSample: buf.readUInt16LE(body + 14)
      };
    } else if (id === 'data') {
      dataBytes = size;
      const end = Math.min(buf.length, body + size);
      for (let p = body; p + 1 < end; p += 2) {
        const s = Math.abs(buf.readInt16LE(p));
        if (s > maxSample) maxSample = s;
      }
    }
    offset = body + size + (size % 2);
  }
  assert.ok(fmt, `Missing fmt chunk in ${filePath}`);
  assert.ok(dataBytes > 0, `Missing or empty data chunk in ${filePath}`);
  return { ...fmt, dataBytes, duration: dataBytes / fmt.byteRate, maxSample };
}

for (const section of ['music', 'ambience', 'sfx']) {
  for (const [key, rel] of Object.entries(audioCatalog[section] || {})) {
    const abs = path.join(audioDir, rel);
    assert.ok(fs.existsSync(abs), `Missing audio file for ${section}.${key}: ${abs}`);
    const stat = fs.statSync(abs);
    assert.ok(stat.size > 64, `Audio file too small for ${section}.${key}: ${abs}`);
  }
}

const combatPixelFx = [
  ['weapons/sword-slash', swordSlash, 'fx.sword_slash'],
  ['weapons/thrust', thrustFx, 'fx.thrust'],
  ['weapons/spear-sweep', spearSweep, 'fx.spear_sweep'],
  ['weapons/arrow', arrowFx, 'fx.arrow'],
  ['impacts/hit-flash', hitFlash, 'fx.hit_flash'],
  ['impacts/critical-hit', criticalHit, 'fx.critical_hit'],
  ['ui/crit-number', critNumber, 'fx.crit_number'],
  ['ui/damage-number', damageNumber, 'fx.hit_flash']
];
for (const [fxName, mod, expectedKey] of combatPixelFx) {
  const actualKey = sfxKeyForFx(mod);
  assert.equal(actualKey, expectedKey, `Pixel effect ${fxName} must map to ${expectedKey}, got ${actualKey}`);
  assert.ok(FX_SFX_KEYS.has(mod), `FX_SFX_KEYS must register ${fxName}`);
  assert.ok(audioCatalog.sfx[actualKey], `audio.json missing sfx key ${actualKey} for ${fxName}`);
}

const requiredSynthesizedKeys = [
  'fx.sword_slash', 'fx.thrust', 'fx.spear_sweep', 'fx.arrow',
  'fx.hit_flash', 'fx.critical_hit', 'fx.crit_number',
  'duel.start', 'duel.win', 'duel.lose',
  'debate.push', 'debate.counter', 'debate.win', 'debate.lose',
  'event.reveal'
];
for (const key of requiredSynthesizedKeys) {
  const rel = audioCatalog.sfx[key];
  assert.ok(rel, `Missing required synthesized key ${key} in audio.json`);
  const abs = path.join(audioDir, rel);
  assert.ok(fs.existsSync(abs), `Missing WAV file for ${key}: ${abs}`);
  if (abs.endsWith('.wav')) {
    const wav = readWavMeta(abs);
    assert.equal(wav.audioFormat, 1, `Expected PCM WAV for ${key}`);
    assert.equal(wav.channels, 1, `Expected mono WAV for ${key}`);
    assert.equal(wav.sampleRate, 22050, `Expected 22050 Hz WAV for ${key}`);
    assert.equal(wav.bitsPerSample, 16, `Expected 16-bit WAV for ${key}`);
    assert.ok(wav.duration > 0.1 && wav.duration < 1.5, `Expected ${key} duration < 1.5s, got ${wav.duration.toFixed(3)}s`);
    assert.ok(wav.maxSample > 1000, `Expected non-silent waveform for ${key}`);
  }
}

const marketRel = audioCatalog.ambience['ambience.market'] || audioCatalog.ambience.market;
assert.equal(marketRel, 'ambience/market.ogg', 'Expected market ambience mapped to ambience/market.ogg');
const marketOggBuf = fs.readFileSync(path.join(audioDir, marketRel));
assert.equal(marketOggBuf.toString('ascii', 0, 4), 'OggS', 'Expected OggS header in ambience/market.ogg');

const playedSfx = [];
const prevPlaySfx = SimTBridge.playSfx;
SimTBridge.playSfx = (k) => playedSfx.push(k);
try {
  for (const [, mod] of combatPixelFx.slice(0, 7)) {
    pushFx(mod, { x: 200, y: 150, seed: 7 });
  }
  for (const expected of ['fx.sword_slash', 'fx.thrust', 'fx.spear_sweep', 'fx.arrow', 'fx.hit_flash', 'fx.critical_hit', 'fx.crit_number']) {
    assert.ok(playedSfx.includes(expected), `Expected pushFx to play ${expected}`);
  }

  playedSfx.length = 0;
  const mockDuelEl = { innerHTML: '', querySelector() { return null; }, querySelectorAll() { return []; } };
  renderDuelScene(mockDuelEl, {
    sceneId: 'duel:test', citadelNameVi: 'Lạc Dương', titleVi: 'Lôi đài', matchupSummaryVi: 'Cân sức',
    backgroundPath: 'art/backgrounds/arena.png',
    player: { nameVi: 'Bạn', subVi: 'Bộ', facePath: '', spritePath: '', branch: 'Infantry', hp: 100, maxHp: 100, spirit: 10, maxSpirit: 100 },
    opponent: { nameVi: 'Địch', subVi: 'Kỵ', facePath: '', spritePath: '', branch: 'Cavalry', hp: 100, maxHp: 100, spirit: 10, maxSpirit: 100 },
    result: { won: true, roundLogs: [], combatSummaryVi: 'Thắng' }, choices: []
  }, SimTBridge);
  assert.ok(playedSfx.includes('duel.start') && playedSfx.includes('duel.win'), 'Expected duel-scene to play duel.start and duel.win');

  playedSfx.length = 0;
  renderDuelScene(mockDuelEl, {
    sceneId: 'duel:test2', citadelNameVi: 'Lạc Dương', titleVi: 'Lôi đài', matchupSummaryVi: 'Cân sức',
    backgroundPath: 'art/backgrounds/arena.png',
    player: { nameVi: 'Bạn', subVi: 'Bộ', facePath: '', spritePath: '', branch: 'Infantry', hp: 100, maxHp: 100, spirit: 10, maxSpirit: 100 },
    opponent: { nameVi: 'Địch', subVi: 'Kỵ', facePath: '', spritePath: '', branch: 'Cavalry', hp: 100, maxHp: 100, spirit: 10, maxSpirit: 100 },
    result: { won: false, roundLogs: [], combatSummaryVi: 'Thua' }, choices: []
  }, SimTBridge);
  assert.ok(playedSfx.includes('duel.lose'), 'Expected duel-scene to play duel.lose');

  playedSfx.length = 0;
  renderDebateScene(mockDebateContainer, {
    sceneId: 'debate:sfx:1', npcId: 1, npcNameVi: 'Trương Lão Bản', persuasion: 65, turn: 2, maxTurns: 5,
    isFinished: false, outcomeCode: 'ongoing', latestLineVi: 'Lý lẽ sắc bén.', traits: [], choices: []
  }, SimTBridge);
  renderDebateScene(mockDebateContainer, {
    sceneId: 'debate:sfx:1', npcId: 1, npcNameVi: 'Trương Lão Bản', persuasion: 48, turn: 3, maxTurns: 5,
    isFinished: false, outcomeCode: 'ongoing', latestLineVi: 'Phản biện.', traits: [], choices: []
  }, SimTBridge);
  renderDebateScene(mockDebateContainer, {
    sceneId: 'debate:sfx:1', npcId: 1, npcNameVi: 'Trương Lão Bản', persuasion: 82, turn: 4, maxTurns: 5,
    isFinished: true, outcomeCode: 'majorwin', latestLineVi: 'Tâm phục khẩu phục.', traits: [], choices: []
  }, SimTBridge);
  renderDebateScene(mockDebateContainer, {
    sceneId: 'debate:sfx:2', npcId: 1, npcNameVi: 'Trương Lão Bản', persuasion: 18, turn: 3, maxTurns: 5,
    isFinished: true, outcomeCode: 'majorloss', latestLineVi: 'Đuối lý.', traits: [], choices: []
  }, SimTBridge);
  for (const k of ['debate.push', 'debate.counter', 'debate.win', 'debate.lose']) {
    assert.ok(playedSfx.includes(k), `Expected debate-scene to play ${k}`);
  }

  playedSfx.length = 0;
  const mockEventEl = {
    innerHTML: '',
    querySelector() { return { classList: { remove() {}, add() {} }, addEventListener() {}, offsetWidth: 0, textContent: '', innerHTML: '' }; },
    querySelectorAll() { return []; }
  };
  renderEventScene(mockEventEl, {
    sceneId: 'event:sfx:1', titleVi: 'Khăn Vàng nổi dậy', locationLabelVi: 'Quảng Tông', eraLabelVi: '184',
    narrativeVi: 'Thiên hạ đại biến.', sentencesVi: ['Thiên hạ đại biến.'], backgroundArtPath: '', portraits: [], impactBadges: [], choices: []
  }, SimTBridge);
  assert.ok(playedSfx.includes('event.reveal'), 'Expected event-scene to play event.reveal');
} finally {
  if (prevPlaySfx === undefined) delete SimTBridge.playSfx;
  else SimTBridge.playSfx = prevPlaySfx;
}
console.log('check-scenes: synthesized audio catalog, <1.5s WAV durations, market.ogg ambience, and duel/debate/event SFX wiring OK.');
}

