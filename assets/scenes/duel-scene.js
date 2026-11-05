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
const PAL_GOLD = ['#2b1708', '#5e3210', '#965419', '#c48431', '#e3b464', '#f7dfa8', '#ffffff'];
const PAL_CRIMSON = ['#240608', '#570d12', '#8f1920', '#c72c30', '#f05d4f', '#ffa596', '#ffffff'];

let activeScene = null;
let rafId = null;
let resizeHandler = null;
let liveFx = [];
let speedMult = 1;
let stopReplayFlag = false;

function escapeHtml(str) {
  return String(str ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
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

function clamp90(str) {
  const s = String(str || '').replace(/\s+/g, ' ').trim();
  return s.length <= 90 ? s : `${s.slice(0, 89)}…`;
}

function chooseWeaponMod(branch, action) {
  const act = String(action || '').toLowerCase();
  const br = String(branch || '').toLowerCase();
  if (act === 'specialstrike') return spearSweep;
  if (br === 'archers') return arrowFx;
  if (br === 'cavalry') return thrustFx;
  return swordSlash;
}

function spawnRoundVfx(round, playerBranch, oppBranch) {
  const pAct = String(round.playerAction || '').toLowerCase();
  const oAct = String(round.opponentAction || '').toLowerCase();
  const pDmg = round.playerDamageDealt || 0;
  const oDmg = round.opponentDamageDealt || 0;

  // Player strike at Opponent (right side x=332, y=196)
  if (pDmg > 0 || pAct === 'attack' || pAct === 'specialstrike') {
    const mod = chooseWeaponMod(playerBranch, round.playerAction);
    liveFx.push(createFx(mod, { x: 332, y: 196, flip: false, palette: PAL_GOLD, seed: round.roundNumber * 7 + 1 }));
    if (round.isCritical || pAct === 'specialstrike') {
      liveFx.push(createFx(criticalHit, { x: 344, y: 176, palette: PAL_GOLD, seed: round.roundNumber * 11 }));
      if (pDmg > 0) {
        liveFx.push(createFx(critNumber, { x: 346, y: 145, text: `-${pDmg}`, seed: round.roundNumber }));
      }
    } else if (pDmg > 0) {
      liveFx.push(createFx(damageNumber, { x: 346, y: 150, text: `-${pDmg}`, palette: PAL_GOLD, seed: round.roundNumber }));
    }
  }
  if (round.isParried || oAct === 'defend') {
    liveFx.push(createFx(hitFlash, { x: 336, y: 178, palette: PAL_GOLD, seed: round.roundNumber }));
  }

  // Opponent strike at Player (left side x=148, y=196)
  if (oDmg > 0 || oAct === 'attack' || oAct === 'specialstrike') {
    const mod = chooseWeaponMod(oppBranch, round.opponentAction);
    liveFx.push(createFx(mod, { x: 148, y: 196, flip: true, palette: PAL_CRIMSON, seed: round.roundNumber * 13 + 3 }));
    if (round.isCritical || oAct === 'specialstrike') {
      liveFx.push(createFx(criticalHit, { x: 136, y: 176, palette: PAL_CRIMSON, seed: round.roundNumber * 17 }));
      if (oDmg > 0) {
        liveFx.push(createFx(critNumber, { x: 134, y: 145, text: `-${oDmg}`, seed: round.roundNumber + 5 }));
      }
    } else if (oDmg > 0) {
      liveFx.push(createFx(damageNumber, { x: 134, y: 150, text: `-${oDmg}`, palette: PAL_CRIMSON, seed: round.roundNumber + 5 }));
    }
  }
  if (round.isParried || pAct === 'defend') {
    liveFx.push(createFx(hitFlash, { x: 144, y: 178, palette: PAL_CRIMSON, seed: round.roundNumber + 9 }));
  }
}

function fitIntegerStage(container) {
  const arena = container.querySelector('#tk-duel-arena');
  const box = container.querySelector('#tk-duel-stage-box');
  if (!arena || !box) return;
  const rect = arena.getBoundingClientRect();
  const scale = Math.max(1, Math.min(4, Math.floor(Math.min(rect.width / 480, rect.height / 270))));
  box.style.width = `${480 * scale}px`;
  box.style.height = `${270 * scale}px`;
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
      actRow.innerHTML = `
        <span>${escapeHtml(scene.player.nameVi)}: ${actionLabelVi(r.playerAction)} (-${r.playerDamageDealt} HP)</span>
        <span>${escapeHtml(scene.opponent.nameVi)}: ${actionLabelVi(r.opponentAction)} (-${r.opponentDamageDealt} HP)</span>
      `;
    }
    if (narEl) narEl.textContent = clamp90(r.narrativeVi);
    if (noteEl) {
      noteEl.textContent = r.isCritical
        ? 'Đòn chí mạng kinh thiên động địa trên lôi đài!'
        : r.isParried
          ? 'Đỡ gạt chuẩn xác, hóa giải thế công!'
          : scene.matchupSummaryVi;
    }

    // 1. Wind-up (lấy đà)
    if (leftActor) leftActor.style.transform = 'translateX(-14px)';
    if (rightActor) rightActor.style.transform = 'translateX(14px)';
    await waitMs(140);
    if (stopReplayFlag || activeScene !== scene) break;

    // 2. Dash forward (lao tới)
    if (leftActor) leftActor.style.transform = 'translateX(86px)';
    if (rightActor) rightActor.style.transform = 'translateX(-86px)';
    await waitMs(120);
    if (stopReplayFlag || activeScene !== scene) break;

    // 3. Impact + pixelfx + hit-stop (0.12s) + shake + white flash + knockback
    spawnRoundVfx(r, scene.player.branch, scene.opponent.branch);
    if (stageBox) {
      stageBox.classList.remove('tk-shake');
      void stageBox.offsetWidth;
      stageBox.classList.add('tk-shake');
    }
    if (r.opponentDamageDealt > 0 && leftActor) {
      leftActor.classList.add('tk-flash');
      leftActor.style.transform = 'translateX(-22px)';
    }
    if (r.playerDamageDealt > 0 && rightActor) {
      rightActor.classList.add('tk-flash');
      rightActor.style.transform = 'translateX(22px)';
    }
    updateBars(
      container,
      r.playerHpAfter, scene.player.maxHp, r.playerSpiritAfter, scene.player.maxSpirit,
      r.opponentHpAfter, scene.opponent.maxHp, r.opponentSpiritAfter, scene.opponent.maxSpirit
    );
    await waitMs(130);

    if (leftActor) leftActor.classList.remove('tk-flash');
    if (rightActor) rightActor.classList.remove('tk-flash');

    // 4. Return to guard
    if (leftActor) leftActor.style.transform = 'translateX(0)';
    if (rightActor) rightActor.style.transform = 'translateX(0)';
    await waitMs(520);
  }

  if (activeScene !== scene) return;
  const last = rounds[rounds.length - 1];
  updateBars(
    container,
    last.playerHpAfter, scene.player.maxHp, last.playerSpiritAfter, scene.player.maxSpirit,
    last.opponentHpAfter, scene.opponent.maxHp, last.opponentSpiritAfter, scene.opponent.maxSpirit
  );
  if (leftActor) { leftActor.classList.remove('tk-flash'); leftActor.style.transform = 'translateX(0)'; }
  if (rightActor) { rightActor.classList.remove('tk-flash'); rightActor.style.transform = 'translateX(0)'; }

  const banner = container.querySelector('#tk-duel-banner');
  if (banner) {
    banner.hidden = false;
  }
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

  const stampText = !hasResult ? '' : scene.result.won ? 'ĐẠI THẮNG LÔI ĐÀI' : 'THẤT BẠI ĐÁNG TIẾC';
  const rewardVi = hasResult && scene.result.goldReward > 0
    ? `Phần thưởng lôi đài: +${scene.result.goldReward} Quan`
    : hasResult ? 'Đã ghi nhận kết quả giao đấu.' : '';

  const choicesHtml = choices.map((c, idx) => `
    <button type="button" class="tk-choice" data-choice-id="${escapeHtml(c.id)}" ${c.enabled === false ? 'disabled' : ''}>
      <span class="tk-choice-num">${HAN_NUMERALS[idx] || (idx + 1)}</span>
      <span class="tk-choice-label tk-one">${escapeHtml(c.labelVi)}</span>
      ${c.hintVi ? `<span class="tk-choice-tag tk-one">${escapeHtml(c.hintVi)}</span>` : ''}
    </button>
  `).join('');

  container.innerHTML = `
    <div class="tk-duel-shell">
      <header class="tk-duel-topbar">
        <div class="tk-duel-title-wrap">
          <span class="tk-duel-title tk-one">${escapeHtml(scene.citadelNameVi)} · ${escapeHtml(scene.titleVi)}</span>
          <span class="tk-duel-matchup tk-one">${escapeHtml(scene.matchupSummaryVi)}</span>
        </div>
        <div class="tk-duel-controls">
          <span class="tk-round-pill tk-num" id="tk-round-pill">${hasResult ? `Hiệp 1 / ${scene.result.rounds}` : 'Chọn Binh Chủng'}</span>
          ${hasResult ? `
            <button type="button" class="tk-speed-btn active" data-speed="1">1x</button>
            <button type="button" class="tk-speed-btn" data-speed="2">2x</button>
            <button type="button" class="tk-speed-btn" data-speed="4">4x</button>
            <button type="button" class="tk-skip-btn" id="tk-duel-skip">Bỏ qua</button>
          ` : ''}
          <button type="button" class="tk-close-btn" id="tk-duel-close">[ESC] Đóng</button>
        </div>
      </header>

      <div class="tk-duel-arena" id="tk-duel-arena">
        <div class="tk-duel-stage-box" id="tk-duel-stage-box" style="background-image:url('${escapeHtml(scene.backgroundPath)}')">
          <div class="tk-duel-hud">
            <div class="tk-fcard">
              <div class="tk-fcard-face"><img src="${escapeHtml(p.facePath)}" alt="" /></div>
              <div>
                <div class="tk-fcard-name tk-one">${escapeHtml(p.nameVi)}</div>
                <div class="tk-fcard-sub tk-one">${escapeHtml(p.subVi)}</div>
                <div class="tk-bar-row"><span>HP</span><div class="tk-bar-track"><div class="tk-bar-fill tk-bar-hp" id="tk-p-hp-bar" style="width:100%"></div></div><span class="tk-num" id="tk-p-hp-txt">${p.hp}/${p.maxHp}</span></div>
                <div class="tk-bar-row"><span>Khí</span><div class="tk-bar-track"><div class="tk-bar-fill tk-bar-sp" id="tk-p-sp-bar" style="width:10%"></div></div><span class="tk-num" id="tk-p-sp-txt">${p.spirit}</span></div>
              </div>
            </div>

            <div class="tk-fcard tk-fcard-right">
              <div>
                <div class="tk-fcard-name tk-one">${escapeHtml(o.nameVi)}</div>
                <div class="tk-fcard-sub tk-one">${escapeHtml(o.subVi)}</div>
                <div class="tk-bar-row"><span>HP</span><div class="tk-bar-track"><div class="tk-bar-fill tk-bar-hp" id="tk-o-hp-bar" style="width:100%"></div></div><span class="tk-num" id="tk-o-hp-txt">${o.hp}/${o.maxHp}</span></div>
                <div class="tk-bar-row"><span>Khí</span><div class="tk-bar-track"><div class="tk-bar-fill tk-bar-sp" id="tk-o-sp-bar" style="width:10%"></div></div><span class="tk-num" id="tk-o-sp-txt">${o.spirit}</span></div>
              </div>
              <div class="tk-fcard-face"><img src="${escapeHtml(o.facePath)}" alt="" /></div>
            </div>
          </div>

          <div class="tk-fighter-actor tk-fighter-left" id="tk-actor-left">
            <img class="tk-fighter-sprite" src="${escapeHtml(p.spritePath)}" alt="${escapeHtml(p.nameVi)}" />
          </div>
          <div class="tk-fighter-actor tk-fighter-right" id="tk-actor-right">
            <img class="tk-fighter-sprite" src="${escapeHtml(o.spritePath)}" alt="${escapeHtml(o.nameVi)}" />
          </div>

          <canvas class="tk-duel-canvas" id="tk-duel-canvas" width="480" height="270"></canvas>

          <div class="tk-duel-banner" id="tk-duel-banner" hidden>
            <div class="tk-banner-stamp">${escapeHtml(stampText)}</div>
            <div class="tk-banner-sub tk-one">${escapeHtml(hasResult ? clamp90(scene.result.combatSummaryVi) : '')}</div>
            <div class="tk-banner-reward tk-one">${escapeHtml(rewardVi)}</div>
          </div>
        </div>
      </div>

      <footer class="tk-duel-dock">
        <div class="tk-duel-logbox">
          <div class="tk-duel-actions-row" id="tk-duel-actions">
            <span>${escapeHtml(p.nameVi)} chuẩn bị xuất chiêu</span>
            <span>${escapeHtml(o.nameVi)} thủ thế nghênh địch</span>
          </div>
          <p class="tk-duel-narrative tk-one" id="tk-duel-narrative">
            ${escapeHtml(hasResult ? 'Trống trận vang rền, hai võ sĩ lao vào giao phong!' : 'Hãy chọn binh chủng xuất chiến để bắt đầu tỷ võ lôi đài.')}
          </p>
          <div class="tk-duel-Note tk-one" id="tk-duel-note">${escapeHtml(scene.matchupSummaryVi)}</div>
        </div>
        <div class="tk-choices-col">${choicesHtml}</div>
      </footer>
    </div>
  `;

  fitIntegerStage(container);
  resizeHandler = () => fitIntegerStage(container);
  window.addEventListener('resize', resizeHandler);

  container.querySelector('#tk-duel-close')?.addEventListener('click', () => bridge.sendClose(scene.sceneId));
  container.querySelectorAll('[data-choice-id]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const id = btn.getAttribute('data-choice-id');
      if (id) bridge.sendChoice(scene.sceneId, id);
    });
  });

  container.querySelectorAll('[data-speed]').forEach((btn) => {
    btn.addEventListener('click', () => {
      speedMult = parseInt(btn.getAttribute('data-speed') || '1', 10) || 1;
      container.querySelectorAll('[data-speed]').forEach((b) => b.classList.toggle('active', b === btn));
    });
  });

  container.querySelector('#tk-duel-skip')?.addEventListener('click', () => {
    stopReplayFlag = true;
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
      if (liveFx.length > 0) {
        clearFrame(frame);
        for (const fx of liveFx) {
          fx.update(dt);
          fx.draw(frame);
        }
        liveFx = liveFx.filter((fx) => !fx.done());
        composite(frame, imgData.data, null, 1);
        ctx.putImageData(imgData, 0, 0);
      } else {
        ctx.clearRect(0, 0, 480, 270);
      }
      rafId = requestAnimationFrame(tick);
    };
    rafId = requestAnimationFrame(tick);
  }

  if (hasResult) {
    runReplay(container, scene);
  }
}

function unmountDuelScene() {
  stopReplayFlag = true;
  if (rafId) {
    cancelAnimationFrame(rafId);
    rafId = null;
  }
  if (resizeHandler) {
    window.removeEventListener('resize', resizeHandler);
    resizeHandler = null;
  }
  liveFx = [];
  activeScene = null;
}

SimTBridge.registerStage('duel', {
  render: renderDuelScene,
  unmount: unmountDuelScene
});
