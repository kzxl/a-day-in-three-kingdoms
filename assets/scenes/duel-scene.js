import { SimTBridge } from './bridge.js';
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
  const lines = splitNarrativeSentences(str, maxLen);
  return lines.map((line) => `<span class="tk-duel-narrative-line">${escapeHtml(line)}</span>`).join('');
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
  } else if (mod === thrustFx) {
    pushFx(thrustFx, { x, y, flip, palette: pal7, sparks: SPARKS_GOLD, reach: 66, seed });
  } else if (mod === spearSweep) {
    pushFx(spearSweep, { x, y, flip, palette: pal7, sparks: SPARKS_GOLD, stone: '#4a3f69', seed });
  } else {
    pushFx(swordSlash, { x, y, flip, palette: pal7, sparks: SPARKS_GOLD, radius: 42, seed });
  }
}

function spawnRoundVfx(round, playerBranch, oppBranch) {
  const pAct = String(round.playerAction || '').toLowerCase();
  const oAct = String(round.opponentAction || '').toLowerCase();
  const pDmg = round.playerDamageDealt || 0;
  const oDmg = round.opponentDamageDealt || 0;
  const rn = round.roundNumber || 1;

  if (pDmg > 0 || pAct === 'attack' || pAct === 'specialstrike') {
    spawnWeaponFx(chooseWeaponMod(playerBranch, round.playerAction), 332, 196, false, true, rn * 7 + 1);
    if (round.isCritical || pAct === 'specialstrike') {
      pushFx(criticalHit, { x: 344, y: 176, palette: PAL_GOLD_8, height: 26, aim: 0.12, seed: rn * 11 });
      if (pDmg > 0) pushFx(critNumber, { x: 346, y: 145, palette: PAL_GOLD_8, text: `-${pDmg}!`, label: 'BAO', seed: rn });
    } else if (pDmg > 0) {
      pushFx(damageNumber, { x: 346, y: 150, palette: PAL_GOLD_8, text: `-${pDmg}`, seed: rn });
    }
  }
  if (round.isParried || oAct === 'defend') {
    pushFx(hitFlash, { x: 336, y: 178, palette: PAL_GOLD_7, lines: 7, side: -1, actor: DEFAULT_HIT_ACTOR, seed: rn });
  }

  if (oDmg > 0 || oAct === 'attack' || oAct === 'specialstrike') {
    spawnWeaponFx(chooseWeaponMod(oppBranch, round.opponentAction), 148, 196, true, false, rn * 13 + 3);
    if (round.isCritical || oAct === 'specialstrike') {
      pushFx(criticalHit, { x: 136, y: 176, palette: PAL_CRIMSON_8, height: 26, aim: -0.12, seed: rn * 17 });
      if (oDmg > 0) pushFx(critNumber, { x: 134, y: 145, palette: PAL_CRIMSON_8, text: `-${oDmg}!`, label: 'BAO', seed: rn + 5 });
    } else if (oDmg > 0) {
      pushFx(damageNumber, { x: 134, y: 150, palette: PAL_CRIMSON_8, text: `-${oDmg}`, seed: rn + 5 });
    }
  }
  if (round.isParried || pAct === 'defend') {
    pushFx(hitFlash, { x: 144, y: 178, palette: PAL_CRIMSON_7, lines: 7, side: 1, actor: DEFAULT_HIT_ACTOR, seed: rn + 9 });
  }
}

function fitIntegerStage(container) {
  const arena = container.querySelector('#tk-duel-arena');
  const box = container.querySelector('#tk-duel-stage-box');
  if (!arena || !box) return;
  const rect = arena.getBoundingClientRect();
  const rawScale = Math.min(Math.max(320, rect.width - 12) / 480, Math.max(180, rect.height - 8) / 270);
  const scale = rawScale >= 2 ? Math.min(4, Math.floor(rawScale)) : Math.max(1, rawScale);
  box.style.width = `${Math.round(480 * scale)}px`;
  box.style.height = `${Math.round(270 * scale)}px`;
  box.style.setProperty('--stage-scale', scale.toFixed(3));
}

function updateBars(container, pHp, pMaxHp, pSp, pMaxSp, oHp, oMaxHp, oSp, oMaxSp) {
  const setBar = (id, val, max) => {
    const el = container.querySelector(id);
    if (el) el.style.width = `${Math.max(0, Math.min(100, Math.round((val / Math.max(1, max)) * 100)))}%`;
  };
  const setTxt = (id, txt) => {
    const el = container.querySelector(id);
    if (el) el.textContent = txt;
  };
  setBar('#tk-p-hp-bar', pHp, pMaxHp);
  setBar('#tk-p-sp-bar', pSp, pMaxSp);
  setBar('#tk-o-hp-bar', oHp, oMaxHp);
  setBar('#tk-o-sp-bar', oSp, oMaxSp);
  setTxt('#tk-p-hp-txt', `${pHp}/${pMaxHp}`);
  setTxt('#tk-p-sp-txt', `${pSp}`);
  setTxt('#tk-o-hp-txt', `${oHp}/${oMaxHp}`);
  setTxt('#tk-o-sp-txt', `${oSp}`);
}

const waitMs = (ms) => new Promise((r) => setTimeout(r, Math.max(16, ms / speedMult)));

async function runReplay(container, scene) {
  const result = scene.result;
  if (!result || !Array.isArray(result.roundLogs) || result.roundLogs.length === 0) return;

  const leftActor = container.querySelector('#tk-actor-left');
  const rightActor = container.querySelector('#tk-actor-right');
  const stageBox = container.querySelector('#tk-duel-stage-box');
  const roundPill = container.querySelector('#tk-round-pill');
  const actRow = container.querySelector('#tk-duel-actions');
  const narEl = container.querySelector('#tk-duel-narrative');
  const noteEl = container.querySelector('#tk-duel-note');
  const rounds = result.roundLogs;

  for (let i = 0; i < rounds.length; i++) {
    if (stopReplayFlag || activeScene !== scene) break;
    const r = rounds[i];
    if (roundPill) roundPill.textContent = `Hiệp ${r.roundNumber} / ${rounds.length}`;
    if (actRow) {
      actRow.innerHTML = `<span>${escapeHtml(scene.player.nameVi)}: ${actionLabelVi(r.playerAction)} (-${r.playerDamageDealt} HP)</span><span>${escapeHtml(scene.opponent.nameVi)}: ${actionLabelVi(r.opponentAction)} (-${r.opponentDamageDealt} HP)</span>`;
    }
    if (narEl) narEl.innerHTML = formatNarrativeHtml(r.narrativeVi, 90);
    if (noteEl) {
      noteEl.textContent = r.isCritical
        ? 'Đòn chí mạng kinh thiên động địa trên lôi đài!'
        : r.isParried ? 'Đỡ gạt chuẩn xác, hóa giải thế công!' : stripEmoji(scene.matchupSummaryVi);
    }

    if (leftActor) leftActor.style.transform = 'translateX(-14px)';
    if (rightActor) rightActor.style.transform = 'translateX(14px)';
    await waitMs(140);
    if (stopReplayFlag || activeScene !== scene) break;

    if (leftActor) leftActor.style.transform = 'translateX(76px)';
    if (rightActor) rightActor.style.transform = 'translateX(-76px)';
    await waitMs(120);
    if (stopReplayFlag || activeScene !== scene) break;

    spawnRoundVfx(r, scene.player.branch, scene.opponent.branch);
    if (stageBox) {
      stageBox.classList.remove('tk-shake');
      void stageBox.offsetWidth;
      stageBox.classList.add('tk-shake');
    }
    if (r.opponentDamageDealt > 0 && leftActor) { leftActor.classList.add('tk-flash'); leftActor.style.transform = 'translateX(-20px)'; }
    if (r.playerDamageDealt > 0 && rightActor) { rightActor.classList.add('tk-flash'); rightActor.style.transform = 'translateX(20px)'; }
    updateBars(container, r.playerHpAfter, scene.player.maxHp, r.playerSpiritAfter, scene.player.maxSpirit, r.opponentHpAfter, scene.opponent.maxHp, r.opponentSpiritAfter, scene.opponent.maxSpirit);
    await waitMs(130);

    if (leftActor) { leftActor.classList.remove('tk-flash'); leftActor.style.transform = 'translateX(0)'; }
    if (rightActor) { rightActor.classList.remove('tk-flash'); rightActor.style.transform = 'translateX(0)'; }
    await waitMs(480);
  }

  if (activeScene !== scene) return;
  const last = rounds[rounds.length - 1];
  updateBars(container, last.playerHpAfter, scene.player.maxHp, last.playerSpiritAfter, scene.player.maxSpirit, last.opponentHpAfter, scene.opponent.maxHp, last.opponentSpiritAfter, scene.opponent.maxSpirit);
  if (leftActor) { leftActor.classList.remove('tk-flash'); leftActor.style.transform = 'translateX(0)'; }
  if (rightActor) { rightActor.classList.remove('tk-flash'); rightActor.style.transform = 'translateX(0)'; }

  const banner = container.querySelector('#tk-duel-banner');
  if (banner) { banner.hidden = false; banner.classList.add('show'); }
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
  const choices = Array.isArray(scene.choices) ? scene.choices : [];
  const cleanMatchup = escapeHtml(scene.matchupSummaryVi);
  const stampText = !hasResult ? '' : scene.result.won ? 'ĐẠI THẮNG LÔI ĐÀI' : 'THẤT BẠI ĐÁNG TIẾC';
  const rewardVi = hasResult && scene.result.goldReward > 0
    ? `Phần thưởng lôi đài: +${scene.result.goldReward} Quan`
    : hasResult ? 'Đã ghi nhận kết quả giao đấu.' : '';

  const choicesHtml = choices.map((c, idx) => {
    const isBranch = String(c.id || '').startsWith('branch:');
    const iconHtml = isBranch ? branchSvgIcon(c.id) : `<span class="tk-choice-num">${HAN_NUMERALS[idx] || (idx + 1)}</span>`;
    return `<button type="button" class="tk-choice" data-choice-id="${escapeHtml(c.id)}" ${c.enabled === false ? 'disabled' : ''}>${iconHtml}<span class="tk-choice-label">${escapeHtml(c.labelVi)}</span>${c.hintVi ? `<span class="tk-choice-tag">${escapeHtml(c.hintVi)}</span>` : ''}</button>`;
  }).join('');

  container.innerHTML = `
    <div class="tk-duel-shell">
      <header class="tk-duel-topbar">
        <div class="tk-duel-title-wrap">
          <span class="tk-duel-title tk-one">${escapeHtml(scene.citadelNameVi)} · ${escapeHtml(scene.titleVi)}</span>
          <span class="tk-duel-matchup tk-one">${branchSvgIcon(p.branch)}<span>${cleanMatchup}</span>${branchSvgIcon(o.branch)}</span>
        </div>
        <div class="tk-duel-controls">
          <span class="tk-round-pill tk-num" id="tk-round-pill">${hasResult ? `Hiệp 1 / ${scene.result.rounds}` : 'Chọn Binh Chủng'}</span>
          ${hasResult ? '<button type="button" class="tk-speed-btn active" data-speed="1">1x</button><button type="button" class="tk-speed-btn" data-speed="2">2x</button><button type="button" class="tk-speed-btn" data-speed="4">4x</button><button type="button" class="tk-skip-btn" id="tk-duel-skip">Bỏ qua</button>' : ''}
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

          <div class="tk-fighter-actor tk-fighter-left" id="tk-actor-left"><img class="tk-fighter-sprite" src="${escapeHtml(p.spritePath)}" alt="${escapeHtml(p.nameVi)}" /></div>
          <div class="tk-fighter-actor tk-fighter-right" id="tk-actor-right"><img class="tk-fighter-sprite" src="${escapeHtml(o.spritePath)}" alt="${escapeHtml(o.nameVi)}" /></div>
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
          <div class="tk-duel-narrative" id="tk-duel-narrative">${formatNarrativeHtml(hasResult ? 'Trống trận vang rền, hai võ sĩ lao vào giao phong!' : 'Hãy chọn binh chủng xuất chiến để bắt đầu tỷ võ lôi đài.', 90)}</div>
          <div class="tk-duel-Note" id="tk-duel-note">${cleanMatchup}</div>
        </div>
        <div class="tk-choices-col tk-duel-choices">${choicesHtml}</div>
      </footer>
    </div>
  `;

  fitIntegerStage(container);
  resizeHandler = () => fitIntegerStage(container);
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
  container.querySelector('#tk-duel-skip')?.addEventListener('click', () => { stopReplayFlag = true; });

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
  activeScene = null;
}

SimTBridge.registerStage('duel', { render: renderDuelScene, unmount: unmountDuelScene });
