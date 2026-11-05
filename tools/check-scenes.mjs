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

console.log(`check-scenes: verified ${effects.length} pixelfx effects x 12 frames, narrative splitter, and manifest assets OK.`);
