import { SimTBridge } from './bridge.js';
import { createDuelAnimator } from './sprite-anim.js';
import { createFrame, clearFrame, createFx, composite } from './vendor/pixelfx/src/core/index.js';
import * as swordSlash from './vendor/pixelfx/src/fx/melee/sword-slash.js';
import * as thrustFx from './vendor/pixelfx/src/fx/melee/thrust.js';
import * as spearSweep from './vendor/pixelfx/src/fx/melee/spear-sweep.js';
import * as arrowFx from './vendor/pixelfx/src/fx/projectiles/arrow.js';
import * as criticalHit from './vendor/pixelfx/src/fx/impacts/critical-hit.js';
import * as hitFlash from './vendor/pixelfx/src/fx/impacts/hit-flash.js';
import * as damageNumber from './vendor/pixelfx/src/fx/ui/damage-number.js';
import * as critNumber from './vendor/pixelfx/src/fx/ui/crit-number.js';

const HAN_NUMERALS = ['壹', '貳', '參', '肆'];
const PAL_GOLD_7 = ['#2b1708', '#5e3210', '#965419', '#c48431', '#e3b464', '#f7dfa8', '#ffffff'];
const PAL_GOLD_8 = ['#2b1708', '#4c280d', '#7a4315', '#b06b25', '#d99b48', '#f0c97d', '#faecc0', '#ffffff'];
const PAL_CRIMSON_7 = ['#240608', '#570d12', '#8f1920', '#c72c30', '#f05d4f', '#ffa596', '#ffffff'];
const PAL_CRIMSON_8 = ['#1a0508', '#4a0b12', '#8c1420', '#d42a2e', '#ff5a4a', '#ff9a86', '#ffd8cc', '#ffffff'];
const SPARKS_GOLD = ['#fff3a6', '#ffe26b', '#ff8a2a', '#f2561d'];
const DEFAULT_HIT_ACTOR = {
  x: -8, y: -36, chest: [0, -20],
  mask: { w: 1, h: 1, data: new Uint8Array(1) },
  pixels: new Uint32Array(1)
};

let activeScene = null;
let rafId = null;
let resizeHandler = null;
let liveFx = [];
let speedMult = 1;
let stopReplayFlag = false;
let duelAnim = null;

function stripEmoji(str) {
  return String(str ?? '').replace(/[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}\u{FE0F}]/gu, '').replace(/\s+/g, ' ').trim();
}
function escapeHtml(str) {
  return stripEmoji(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
function branchSvgIcon(branch) {
  const b = String(branch || '').toLowerCase();
  if (b.includes('cavalry') || b.includes('kỵ')) {
    return '<svg class="tk-troop-icon" viewBox="0 0 16 16" aria-hidden="true"><path d="M2.5 13.5 L12.8 3.2 M9.2 2.5 H13.5 V6.8 M3 9.5 L6.5 13" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>';
  }
  if (b.includes('archer') || b.includes('cung')) {
    return '<svg class="tk-troop-icon" viewBox="0 0 16 16" aria-hidden="true"><path d="M4.5 2.2 C10.5 4.2 10.5 11.8 4.5 13.8 M4.5 2.2 V13.8 M2.2 8 H13.5 M10.8 5.5 L13.5 8 L10.8 10.5" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>';
  }
  return '<svg class="tk-troop-icon" viewBox="0 0 16 16" aria-hidden="true"><path d="M8 1.5 L13.5 3.8 V8.2 C13.5 11.4 11.1 13.8 8 14.8 C4.9 13.8 2.5 11.4 2.5 8.2 V3.8 Z" fill="none" stroke="currentColor" stroke-width="1.5"/><path d="M8 4.2 V11.8 M5.5 7.2 H10.5" stroke="currentColor" stroke-width="1.3"/></svg>';
}
function actionLabelVi(act) {
  switch (String(act || '').toLowerCase()) {
    case 'attack': return 'Tấn Công';
    case 'defend': return 'Phòng Thủ';
    case 'specialstrike': return 'Tuyệt Kỹ';
    case 'focusspirit': return 'Tụ Khí';
    default: return 'Giao Phong';
  }
}

export function splitNarrativeSentences(str, maxLen = 90) {
  const clean = stripEmoji(str);
  if (!clean) return [];
  if (clean.length <= maxLen) return [clean];
  const rawSentences = clean.split(/(?<=[.!?;。！？])\s+|\s+[·—]\s+/).map((s) => s.trim()).filter(Boolean);
  const out = [];
  for (const sentence of rawSentences) {
    if (sentence.length <= maxLen) { out.push(sentence); continue; }
    let rest = sentence;
    while (rest.length > maxLen) {
      let cut = rest.lastIndexOf(', ', maxLen - 1);
      if (cut < Math.floor(maxLen * 0.4)) cut = rest.lastIndexOf(' ', maxLen);
      if (cut <= 0) cut = maxLen;
      const head = rest.slice(0, cut + (rest[cut] === ',' ? 1 : 0)).trim();
      if (head) out.push(head);
      rest = rest.slice(cut + 1).trim();
    }
    if (rest) out.push(rest);
  }
  return out;
}

function formatNarrativeHtml(str, maxLen = 90) {
  return splitNarrativeSentences(str, maxLen).map((line) => `<span class="tk-duel-narrative-line">${escapeHtml(line)}</span>`).join('');
}
function chooseWeaponMod(branch, action) {
  const act = String(action || '').toLowerCase();
  const br = String(branch || '').toLowerCase();
  if (act === 'specialstrike') return spearSweep;
  if (br === 'archers') return arrowFx;
  if (br === 'cavalry') return thrustFx;
  return swordSlash;
}
export function buildEffectOptions(mod, overrides = {}) {
  const defaults = mod?.meta?.options ? { ...mod.meta.options } : {};
  const merged = { ...defaults, ...overrides };
  if (mod?.meta?.actor && !merged.actor) merged.actor = DEFAULT_HIT_ACTOR;
  return merged;
}
export function safeCreateFx(mod, overrides = {}) {
  try { return createFx(mod, buildEffectOptions(mod, overrides)); }
  catch (err) { console.warn('[DuelScene] Bỏ qua hiệu ứng lỗi:', mod?.meta?.name || 'unknown', err); return null; }
}
function pushFx(mod, overrides) {
  const fx = safeCreateFx(mod, overrides);
  if (fx) liveFx.push(fx);
}
function spawnWeaponFx(mod, x, y, flip, isGold, seed) {
  const pal7 = isGold ? PAL_GOLD_7 : PAL_CRIMSON_7;
  if (mod === arrowFx) {
    pushFx(arrowFx, {
      x, y, flip, seed,
      palette: isGold ? ['#7a4315', '#c48431', '#f7dfa8', '#ffffff'] : ['#7a1f2c', '#b8323c', '#e8e2d6', '#ffffff'],
      wood: ['#3a2414', '#6e4422', '#a8723c', '#d8a868'],
      steel: ['#2e3648', '#5a6680', '#9aa6ba', '#dfe7f2'],
      wind: '#b8c8e8'
    });
  } else if (mod === thrustFx) pushFx(thrustFx, { x, y, flip, palette: pal7, sparks: SPARKS_GOLD, reach: 66, seed });
  else if (mod === spearSweep) pushFx(spearSweep, { x, y, flip, palette: pal7, sparks: SPARKS_GOLD, stone: '#4a3f69', seed });
  else pushFx(swordSlash, { x, y, flip, palette: pal7, sparks: SPARKS_GOLD, radius: 42, seed });
}
function spawnHitVfx(targetIsRight, branch, action, dmg, isCrit, isParried, rn) {
  const x = targetIsRight ? 336 : 144;
  const pal7 = targetIsRight ? PAL_GOLD_7 : PAL_CRIMSON_7;
  const pal8 = targetIsRight ? PAL_GOLD_8 : PAL_CRIMSON_8;
  spawnWeaponFx(chooseWeaponMod(branch, action), x, 196, !targetIsRight, targetIsRight, rn * 7 + (targetIsRight ? 1 : 3));
  if (isCrit || String(action || '').toLowerCase() === 'specialstrike') {
    pushFx(criticalHit, { x, y: 176, palette: pal8, height: 26, aim: targetIsRight ? 0.12 : -0.12, seed: rn * 11 });
    if (dmg > 0) pushFx(critNumber, { x, y: 145, palette: pal8, text: `-${dmg}!`, label: 'BAO', seed: rn });
  } else if (dmg > 0) pushFx(damageNumber, { x, y: 150, palette: pal8, text: `-${dmg}`, seed: rn });
  if (isParried) pushFx(hitFlash, { x, y: 178, palette: pal7, lines: 7, side: targetIsRight ? -1 : 1, actor: DEFAULT_HIT_ACTOR, seed: rn });
}
function spawnDomPop(stageBox, x, dmg) {
  if (!stageBox || dmg <= 0) return;
  const pop = document.createElement('div');
  pop.className = 'tk-duel-pop';
  pop.style.left = `${x}px`;
  pop.style.bottom = '286px';
  pop.textContent = `-${dmg}`;
  stageBox.appendChild(pop);
  setTimeout(() => pop.remove(), 800);
}
function fitStageScale(container) {
  const arena = container.querySelector('#tk-duel-arena');
  const box = container.querySelector('#tk-duel-stage-box');
  if (!arena || !box) return;
  const rect = arena.getBoundingClientRect();
  const scale = Math.min(1.15, Math.max(0.55, Math.min((rect.width - 12) / 1024, (rect.height - 8) / 576)));
  box.style.setProperty('--stage-scale', scale.toFixed(3));
  if (duelAnim) duelAnim.setStageScale(scale);
}
function updateBars(container, pHp, pMaxHp, pSp, pMaxSp, oHp, oMaxHp, oSp, oMaxSp) {
  const setBar = (id, val, max) => { const el = container.querySelector(id); if (el) el.style.width = `${Math.max(0, Math.min(100, Math.round((val / Math.max(1, max)) * 100)))}%`; };
  const setTxt = (id, txt) => { const el = container.querySelector(id); if (el) el.textContent = txt; };
  setBar('#tk-p-hp-bar', pHp, pMaxHp); setBar('#tk-p-sp-bar', pSp, pMaxSp);
  setBar('#tk-o-hp-bar', oHp, oMaxHp); setBar('#tk-o-sp-bar', oSp, oMaxSp);
  setTxt('#tk-p-hp-txt', `${pHp}/${pMaxHp}`); setTxt('#tk-p-sp-txt', `${pSp}`);
  setTxt('#tk-o-hp-txt', `${oHp}/${oMaxHp}`); setTxt('#tk-o-sp-txt', `${oSp}`);
}

const waitMs = (ms) => new Promise((r) => setTimeout(r, Math.max(16, ms / speedMult)));

function revealDuelResult(container, scene) {
  if (!scene?.result) return;
  const rounds = scene.result.roundLogs;
  const roundPill = container.querySelector('#tk-round-pill');
  if (!Array.isArray(rounds) || rounds.length === 0) {
    if (roundPill) roundPill.textContent = '0 Hiệp';
    const narEl = container.querySelector('#tk-duel-narrative');
    if (narEl && scene.result.combatSummaryVi) narEl.innerHTML = formatNarrativeHtml(scene.result.combatSummaryVi, 90);
    const banner = container.querySelector('#tk-duel-banner');
    if (banner) { banner.hidden = false; banner.classList.add('show'); }
    container.querySelectorAll('[data-post-duel]').forEach((btn) => { btn.hidden = false; });
    return;
  }
  if (!duelAnim) return;
  const last = rounds[rounds.length - 1];
  if (roundPill) roundPill.textContent = `Hiệp ${rounds.length} / ${rounds.length}`;
  updateBars(container, last.playerHpAfter, scene.player.maxHp, last.playerSpiritAfter, scene.player.maxSpirit, last.opponentHpAfter, scene.opponent.maxHp, last.opponentSpiritAfter, scene.opponent.maxSpirit);
  duelAnim.snapToEnd(last.playerHpAfter, last.opponentHpAfter);
  const banner = container.querySelector('#tk-duel-banner');
  if (banner) { banner.hidden = false; banner.classList.add('show'); }
  container.querySelectorAll('[data-post-duel]').forEach((btn) => { btn.hidden = false; });
}

async function runReplay(container, scene) {
  const rounds = scene.result?.roundLogs;
  if (!Array.isArray(rounds) || rounds.length === 0) {
    revealDuelResult(container, scene);
    return;
  }
  if (!duelAnim) return;
  const stageBox = container.querySelector('#tk-duel-stage-box');
  const roundPill = container.querySelector('#tk-round-pill');
  const actRow = container.querySelector('#tk-duel-actions');
  const narEl = container.querySelector('#tk-duel-narrative');
  const noteEl = container.querySelector('#tk-duel-note');
  let curPHp = scene.player.hp, curPSp = scene.player.spirit, curOHp = scene.opponent.hp, curOSp = scene.opponent.spirit;

  for (let i = 0; i < rounds.length; i++) {
    if (stopReplayFlag || activeScene !== scene || !duelAnim) break;
    const r = rounds[i];
    const rn = r.roundNumber || (i + 1);
    if (roundPill) roundPill.textContent = `Hiệp ${rn} / ${rounds.length}`;
    if (actRow) actRow.innerHTML = `<span>${escapeHtml(scene.player.nameVi)}: ${actionLabelVi(r.playerAction)} (-${r.playerDamageDealt} HP)</span><span>${escapeHtml(scene.opponent.nameVi)}: ${actionLabelVi(r.opponentAction)} (-${r.opponentDamageDealt} HP)</span>`;
    if (narEl) narEl.innerHTML = formatNarrativeHtml(r.narrativeVi, 90);
    if (noteEl) noteEl.textContent = r.isCritical ? 'Đòn chí mạng kinh thiên động địa trên lôi đài!' : r.isParried ? 'Đỡ gạt chuẩn xác, hóa giải thế công!' : stripEmoji(scene.matchupSummaryVi);

    const pAttacks = r.playerDamageDealt > 0 || ['attack', 'specialstrike'].includes(String(r.playerAction || '').toLowerCase());
    const oAttacks = r.opponentDamageDealt > 0 || ['attack', 'specialstrike'].includes(String(r.opponentAction || '').toLowerCase());

    if (pAttacks && !stopReplayFlag && activeScene === scene) {
      await duelAnim.performStrike(0, 1, {
        damage: r.playerDamageDealt, defenderHpAfter: r.opponentHpAfter,
        onImpact: ({ defender }) => {
          curOHp = r.opponentHpAfter; curPSp = r.playerSpiritAfter;
          updateBars(container, curPHp, scene.player.maxHp, curPSp, scene.player.maxSpirit, curOHp, scene.opponent.maxHp, curOSp, scene.opponent.maxSpirit);
          spawnHitVfx(true, scene.player.branch, r.playerAction, r.playerDamageDealt, r.isCritical, r.isParried, rn);
          spawnDomPop(stageBox, defender.x, r.playerDamageDealt);
        }
      });
    }
    if (oAttacks && curOHp > 0 && !stopReplayFlag && activeScene === scene) {
      await duelAnim.performStrike(1, 0, {
        damage: r.opponentDamageDealt, defenderHpAfter: r.playerHpAfter,
        onImpact: ({ defender }) => {
          curPHp = r.playerHpAfter; curOSp = r.opponentSpiritAfter;
          updateBars(container, curPHp, scene.player.maxHp, curPSp, scene.player.maxSpirit, curOHp, scene.opponent.maxHp, curOSp, scene.opponent.maxSpirit);
          spawnHitVfx(false, scene.opponent.branch, r.opponentAction, r.opponentDamageDealt, r.isCritical, r.isParried, rn);
          spawnDomPop(stageBox, defender.x, r.opponentDamageDealt);
        }
      });
    }
    if (!pAttacks && !oAttacks) {
      curPHp = r.playerHpAfter; curPSp = r.playerSpiritAfter; curOHp = r.opponentHpAfter; curOSp = r.opponentSpiritAfter;
      updateBars(container, curPHp, scene.player.maxHp, curPSp, scene.player.maxSpirit, curOHp, scene.opponent.maxHp, curOSp, scene.opponent.maxSpirit);
      await waitMs(320);
    }
  }
  if (activeScene !== scene || !duelAnim) return;
  revealDuelResult(container, scene);
}

function renderFighterInnerHtml(f) {
  if (f.animPath) {
    return `<div class="tk-fighter-shadow"></div><div class="tk-fighter-strip" data-fighter-sp style="background-image:url('${escapeHtml(f.animPath)}')"></div>`;
  }
  return `<div class="tk-fighter-shadow"></div><img class="tk-fighter-sprite" data-fighter-sp src="${escapeHtml(f.spritePath)}" alt="${escapeHtml(f.nameVi)}" />`;
}

function renderDuelScene(container, scene, bridge) {
  unmountDuelScene();
  activeScene = scene;
  liveFx = [];
  speedMult = 1;
  stopReplayFlag = false;

  const p = scene.player;
  const o = scene.opponent;
  const hasResult = !!scene.result;
  const hasReplay = hasResult && Array.isArray(scene.result.roundLogs) && scene.result.roundLogs.length > 0;
  const choices = Array.isArray(scene.choices) ? scene.choices : [];
  const cleanMatchup = escapeHtml(scene.matchupSummaryVi);
  const stampText = !hasResult ? '' : !hasReplay ? 'CHƯA THỂ GIAO ĐẤU' : scene.result.won ? 'ĐẠI THẮNG LÔI ĐÀI' : 'THẤT BẠI ĐÁNG TIẾC';
  const rewardVi = hasResult && scene.result.goldReward > 0
    ? `Phần thưởng lôi đài: +${scene.result.goldReward} Quan`
    : hasReplay ? 'Đã ghi nhận kết quả giao đấu.' : '';

  const choicesHtml = choices.map((c, idx) => {
    const isBranch = String(c.id || '').startsWith('branch:');
    const hideWhilePlaying = hasReplay && c.id !== 'leave';
    const iconHtml = isBranch ? branchSvgIcon(c.id) : `<span class="tk-choice-num">${HAN_NUMERALS[idx] || (idx + 1)}</span>`;
    return `<button type="button" class="tk-choice" data-choice-id="${escapeHtml(c.id)}" ${hideWhilePlaying ? 'data-post-duel hidden' : ''} ${c.enabled === false ? 'disabled' : ''}>${iconHtml}<span class="tk-choice-label">${escapeHtml(c.labelVi)}</span>${c.hintVi ? `<span class="tk-choice-tag">${escapeHtml(c.hintVi)}</span>` : ''}</button>`;
  }).join('');

  container.innerHTML = `
    <div class="tk-duel-shell">
      <header class="tk-duel-topbar">
        <div class="tk-duel-title-wrap">
          <span class="tk-duel-title tk-one">${escapeHtml(scene.citadelNameVi)} · ${escapeHtml(scene.titleVi)}</span>
          <span class="tk-duel-matchup tk-one">${branchSvgIcon(p.branch)}<span>${cleanMatchup}</span>${branchSvgIcon(o.branch)}</span>
        </div>
        <div class="tk-duel-controls">
          <span class="tk-round-pill tk-num" id="tk-round-pill">${hasReplay ? `Hiệp 1 / ${scene.result.rounds}` : hasResult ? '0 Hiệp' : 'Chọn Binh Chủng'}</span>
          ${hasReplay ? '<button type="button" class="tk-speed-btn active" data-speed="1">1x</button><button type="button" class="tk-speed-btn" data-speed="2">2x</button><button type="button" class="tk-speed-btn" data-speed="4">4x</button><button type="button" class="tk-skip-btn" id="tk-duel-skip">Bỏ qua</button>' : ''}
          <button type="button" class="tk-close-btn" id="tk-duel-close">[ESC] Đóng</button>
        </div>
      </header>

      <div class="tk-duel-arena" id="tk-duel-arena">
        <div class="tk-duel-stage-box" id="tk-duel-stage-box" style="background-image:url('${escapeHtml(scene.backgroundPath)}')">
          <div class="tk-duel-hud">
            <div class="tk-fcard">
              <div class="tk-fcard-face"><img src="${escapeHtml(p.facePath)}" alt="" /></div>
              <div class="tk-fcard-body">
                <div class="tk-fcard-name tk-one">${escapeHtml(p.nameVi)}</div>
                <div class="tk-fcard-sub tk-one">${branchSvgIcon(p.branch)}<span>${escapeHtml(p.subVi)}</span></div>
                <div class="tk-bar-row"><span>HP</span><div class="tk-bar-track"><div class="tk-bar-fill tk-bar-hp" id="tk-p-hp-bar" style="width:100%"></div></div><span class="tk-num" id="tk-p-hp-txt">${p.hp}/${p.maxHp}</span></div>
                <div class="tk-bar-row"><span>Khí</span><div class="tk-bar-track"><div class="tk-bar-fill tk-bar-sp" id="tk-p-sp-bar" style="width:10%"></div></div><span class="tk-num" id="tk-p-sp-txt">${p.spirit}</span></div>
              </div>
            </div>
            <div class="tk-fcard tk-fcard-right">
              <div class="tk-fcard-body">
                <div class="tk-fcard-name tk-one">${escapeHtml(o.nameVi)}</div>
                <div class="tk-fcard-sub tk-one"><span>${escapeHtml(o.subVi)}</span>${branchSvgIcon(o.branch)}</div>
                <div class="tk-bar-row"><span>HP</span><div class="tk-bar-track"><div class="tk-bar-fill tk-bar-hp" id="tk-o-hp-bar" style="width:100%"></div></div><span class="tk-num" id="tk-o-hp-txt">${o.hp}/${o.maxHp}</span></div>
                <div class="tk-bar-row"><span>Khí</span><div class="tk-bar-track"><div class="tk-bar-fill tk-bar-sp" id="tk-o-sp-bar" style="width:10%"></div></div><span class="tk-num" id="tk-o-sp-txt">${o.spirit}</span></div>
              </div>
              <div class="tk-fcard-face"><img src="${escapeHtml(o.facePath)}" alt="" /></div>
            </div>
          </div>

          <div class="tk-fighter-actor tk-fighter-left" id="tk-actor-left">${renderFighterInnerHtml(p)}</div>
          <div class="tk-fighter-actor tk-fighter-right" id="tk-actor-right">${renderFighterInnerHtml(o)}</div>
          <canvas class="tk-duel-canvas" id="tk-duel-canvas" width="480" height="270"></canvas>
          <div class="tk-duel-banner" id="tk-duel-banner" hidden>
            <div class="tk-banner-stamp">${escapeHtml(stampText)}</div>
            <div class="tk-banner-sub">${hasResult ? formatNarrativeHtml(scene.result.combatSummaryVi, 90) : ''}</div>
            <div class="tk-banner-reward">${escapeHtml(rewardVi)}</div>
          </div>
        </div>
      </div>

      <footer class="tk-duel-dock">
        <div class="tk-duel-logbox">
          <div class="tk-duel-actions-row" id="tk-duel-actions"><span>${escapeHtml(p.nameVi)} chuẩn bị xuất chiêu</span><span>${escapeHtml(o.nameVi)} thủ thế nghênh địch</span></div>
          <div class="tk-duel-narrative" id="tk-duel-narrative">${formatNarrativeHtml(!hasResult ? 'Hãy chọn binh chủng xuất chiến để bắt đầu tỷ võ lôi đài.' : hasReplay ? 'Trống trận vang rền, hai võ sĩ lao vào giao phong!' : scene.result.combatSummaryVi, 90)}</div>
          <div class="tk-duel-Note" id="tk-duel-note">${cleanMatchup}</div>
        </div>
        <div class="tk-choices-col tk-duel-choices">${choicesHtml}</div>
      </footer>
    </div>
  `;

  const stageBox = container.querySelector('#tk-duel-stage-box');
  const leftEl = container.querySelector('#tk-actor-left');
  const rightEl = container.querySelector('#tk-actor-right');
  duelAnim = createDuelAnimator({
    stageEl: stageBox,
    fighters: [
      { el: leftEl, spEl: leftEl?.querySelector('[data-fighter-sp]'), animPath: p.animPath, animKey: p.animKey, spritePath: p.spritePath, hp: p.hp, maxHp: p.maxHp, home: 300 },
      { el: rightEl, spEl: rightEl?.querySelector('[data-fighter-sp]'), animPath: o.animPath, animKey: o.animKey, spritePath: o.spritePath, hp: o.hp, maxHp: o.maxHp, home: 724 }
    ]
  });

  fitStageScale(container);
  resizeHandler = () => fitStageScale(container);
  window.addEventListener('resize', resizeHandler);

  container.querySelector('#tk-duel-close')?.addEventListener('click', () => bridge.sendClose(scene.sceneId));
  container.querySelectorAll('[data-choice-id]').forEach((btn) => {
    btn.addEventListener('click', () => { const id = btn.getAttribute('data-choice-id'); if (id) bridge.sendChoice(scene.sceneId, id); });
  });
  container.querySelectorAll('[data-speed]').forEach((btn) => {
    btn.addEventListener('click', () => {
      speedMult = parseInt(btn.getAttribute('data-speed') || '1', 10) || 1;
      container.querySelectorAll('[data-speed]').forEach((b) => b.classList.toggle('active', b === btn));
    });
  });
  container.querySelector('#tk-duel-skip')?.addEventListener('click', () => {
    stopReplayFlag = true;
    revealDuelResult(container, scene);
  });

  const canvas = container.querySelector('#tk-duel-canvas');
  if (canvas) {
    const ctx = canvas.getContext('2d');
    const frame = createFrame(480, 270);
    const imgData = ctx.createImageData(480, 270);
    let prev = performance.now();
    const tick = (now) => {
      if (activeScene !== scene) return;
      const dt = Math.min(0.05, (now - prev) / 1000) * speedMult;
      prev = now;
      if (duelAnim) duelAnim.step(dt);
      if (liveFx.length > 0) {
        clearFrame(frame);
        const kept = [];
        for (const fx of liveFx) {
          try { fx.update(dt); fx.draw(frame); if (!fx.done()) kept.push(fx); }
          catch (err) { console.warn('[DuelScene] Lỗi vẽ hiệu ứng:', fx?.meta?.name || 'unknown', err); }
        }
        liveFx = kept;
        composite(frame, imgData.data, null, 1);
        ctx.putImageData(imgData, 0, 0);
      } else {
        ctx.clearRect(0, 0, 480, 270);
      }
      rafId = requestAnimationFrame(tick);
    };
    rafId = requestAnimationFrame(tick);
  }
  if (hasResult) runReplay(container, scene);
}

function unmountDuelScene() {
  stopReplayFlag = true;
  if (rafId) { cancelAnimationFrame(rafId); rafId = null; }
  if (resizeHandler) { window.removeEventListener('resize', resizeHandler); resizeHandler = null; }
  liveFx = [];
  duelAnim = null;
  activeScene = null;
}

SimTBridge.registerStage('duel', { render: renderDuelScene, unmount: unmountDuelScene });
