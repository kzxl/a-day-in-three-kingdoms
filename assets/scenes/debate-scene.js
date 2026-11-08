import { SimTBridge } from './bridge.js';

// Debate scene (Đợt 9 — Biện luận): built on the talk scene layout (tk-talk-stage, tk-loc-badge,
// tk-dialog-paper with tk-seal, tk-mood-pill, tk-choice) just like event-scene.js and talk-scene.js.

const HAN_NUMERALS = ['壹', '貳', '參', '肆', '伍', '陸'];

export function escapeDebateHtml(str) {
  return String(str ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function clampPersuasion(value) {
  const num = Number(value);
  if (!Number.isFinite(num)) return 50;
  return Math.max(0, Math.min(100, Math.round(num)));
}

export function formatTurnCounter(turn, maxTurns) {
  const t = Math.max(1, Number(turn) || 1);
  const m = Math.max(1, Number(maxTurns) || 5);
  return `Lượt ${t} / ${m}`;
}

let currentScene = null;
let keyHandler = null;
let moveHandler = null;

function unmountDebateScene() {
  if (keyHandler) {
    window.removeEventListener('keydown', keyHandler);
    keyHandler = null;
  }
  if (moveHandler) {
    window.removeEventListener('mousemove', moveHandler);
    moveHandler = null;
  }
  currentScene = null;
}

export function renderDebateScene(container, scene, bridge) {
  unmountDebateScene();
  if (!container || !scene) return;
  currentScene = scene;

  const persuasion = clampPersuasion(scene.persuasion);
  const oppShare = 100 - persuasion;
  const turnText = formatTurnCounter(scene.turn, scene.maxTurns);
  const sealChar = scene.npcNameHan ? String(scene.npcNameHan).trim().charAt(0) : '辯';
  const hasDistinctStyle = scene.styleNameVi && scene.styleNameVi.trim() !== String(scene.npcNameVi || '').trim();
  const stylePart = hasDistinctStyle ? `Tự ${scene.styleNameVi} · ` : '';
  const roleSub = `${stylePart}${scene.roleVi || 'Nhân sĩ'}`;
  const isFinished = Boolean(scene.isFinished);
  const outcomeCode = String(scene.outcomeCode || 'ongoing').toLowerCase();
  const isLossOutcome = outcomeCode.includes('loss');

  const traits = Array.isArray(scene.traits) ? scene.traits : [];
  const traitsHtml = traits.length > 0 ? `
    <div class="tk-trait-row" aria-label="Đặc tính nhân vật">
      ${traits.map((t) => `
        <span class="tk-mood-pill ${t.hidden ? 'is-hidden' : ''}"
              title="${escapeDebateHtml(t.descriptionVi || '')}"
              data-trait-code="${escapeDebateHtml(t.code || '')}">${escapeDebateHtml(t.nameVi || '???')}</span>
      `).join('')}
    </div>
  ` : '';

  const choices = Array.isArray(scene.choices) && scene.choices.length > 0
    ? scene.choices
    : (isFinished
      ? [{ id: 'leave', labelVi: 'Rời đi', hintVi: 'Khép lại cuộc biện luận', enabled: true }]
      : [{ id: 'concede', labelVi: 'Nhượng bộ', hintVi: 'Hòa nhẹ', enabled: true }]);

  const choicesHtml = choices.map((c, idx) => {
    const num = HAN_NUMERALS[idx] || String(idx + 1);
    const hint = c.hintVi
      ? `<span class="tk-choice-tag">${escapeDebateHtml(c.hintVi)}</span>`
      : '';
    return `
      <button type="button"
              class="tk-choice"
              data-choice-id="${escapeDebateHtml(c.id)}"
              ${c.enabled === false ? 'disabled' : ''}
              ${c.disabledReasonVi ? `title="${escapeDebateHtml(c.disabledReasonVi)}"` : ''}>
        <span class="tk-choice-num">${num}</span>
        <span class="tk-choice-label">${escapeDebateHtml(c.labelVi)}</span>
        ${hint}
      </button>
    `;
  }).join('');

  const consequenceHtml = isFinished && scene.consequenceVi
    ? `<div class="tk-debate-consequence ${isLossOutcome ? 'is-loss' : ''}" id="tk-debate-consequence">${escapeDebateHtml(scene.consequenceVi)}</div>`
    : '';

  const rightPillText = isFinished && scene.outcomeTitleVi
    ? scene.outcomeTitleVi
    : (scene.personalityVi || 'Trầm ổn');

  const footRightText = `Thuyết phục ${persuasion} / 100`;

  container.innerHTML = `
    <div class="tk-talk-stage tk-debate-stage" id="tk-debate-stage">
      <div class="tk-layer tk-talk-bg" style="background-image:url('${escapeDebateHtml(scene.backgroundPath)}')"></div>
      <div class="tk-layer tk-talk-sky"></div>
      <div class="tk-layer tk-talk-far">
        <div class="tk-sun"></div>
        <svg class="tk-mountains-far" viewBox="0 0 1440 420" preserveAspectRatio="none">
          <path d="M0,420 L0,250 Q180,140 360,230 T720,170 T1080,220 T1440,180 L1440,420 Z" fill="#2c1d18" opacity="0.72"/>
        </svg>
      </div>
      <div class="tk-layer tk-talk-mid">
        <svg class="tk-mountains-near" viewBox="0 0 1440 360" preserveAspectRatio="none">
          <path d="M0,360 L0,220 L160,150 L290,210 L480,130 L680,220 L920,160 L1190,210 L1440,140 L1440,360 Z" fill="#18120e" opacity="0.88"/>
        </svg>
      </div>
      <div class="tk-mist"></div>
      <div class="tk-layer tk-talk-char-layer">
        <div class="tk-char-rig">
          ${scene.archetypeCode ? `<div class="tk-char-halo tk-halo-${escapeDebateHtml(scene.archetypeCode)}"></div>` : ''}
          <div class="tk-char-breath">
            <img class="tk-char-img" id="tk-debate-char-base" src="${escapeDebateHtml(scene.portraitFull)}" alt="${escapeDebateHtml(scene.npcNameVi)}" />
          </div>
        </div>
      </div>
      <div class="tk-vignette"></div>

      <header class="tk-talk-topbar">
        <div class="tk-loc-badge">
          <span class="tk-loc-dot"></span>
          <span class="tk-loc-vi tk-one">${escapeDebateHtml(scene.locationNameVi)}</span>
          <span class="tk-loc-han">${escapeDebateHtml(scene.locationHan || '辯')}</span>
        </div>
        <div class="tk-topbar-right">
          <span class="tk-debate-topic tk-one">${escapeDebateHtml(scene.topicTitleVi || 'Biện luận')}</span>
          <button type="button" class="tk-close-btn" id="tk-debate-close">[ESC] Đóng</button>
        </div>
      </header>

      <footer class="tk-talk-dock">
        <div class="tk-dialog-paper" id="tk-debate-paper">
          <div class="tk-speaker-row">
            <div class="tk-speaker-left">
              <span class="tk-seal tk-seal-sm">${escapeDebateHtml(sealChar)}</span>
              <div>
                <div class="tk-speaker-name tk-one">${escapeDebateHtml(scene.npcNameVi)}</div>
                <div class="tk-speaker-sub tk-one">${escapeDebateHtml(roleSub)}</div>
              </div>
            </div>
            <span class="tk-mood-pill tk-one">${escapeDebateHtml(rightPillText)}</span>
          </div>
          ${traitsHtml}
          <p class="tk-dialog-line tk-debate-line" id="tk-debate-line">${escapeDebateHtml(scene.latestLineVi)}</p>
          ${consequenceHtml}

          <div class="tk-debate-meter" aria-label="Thanh thuyết phục">
            <div class="tk-debate-meter-labels tk-num">
              <span>Mốc 20 · Thua</span>
              <span>Mốc 80 · Thắng</span>
            </div>
            <div class="tk-debate-bar" id="tk-debate-bar">
              <div class="tk-debate-fill-player" style="width:${persuasion}%"></div>
              <div class="tk-debate-fill-opponent" style="width:${oppShare}%"></div>
              <span class="tk-debate-tick tk-debate-tick-20" data-tick="20" title="Mốc 20: Thua cuộc"></span>
              <span class="tk-debate-tick tk-debate-tick-80" data-tick="80" title="Mốc 80: Thắng cuộc"></span>
            </div>
          </div>

          <div class="tk-dialog-foot">
            <span class="tk-line-count tk-num" id="tk-debate-turn">${escapeDebateHtml(turnText)}</span>
            <span class="tk-debate-status tk-num">${escapeDebateHtml(footRightText)}</span>
          </div>
        </div>

        <div class="tk-choices-col tk-debate-choices">
          ${choicesHtml}
        </div>
      </footer>
    </div>
  `;

  const stageEl = container.querySelector('#tk-debate-stage');
  moveHandler = (e) => {
    if (!stageEl || window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return;
    const rect = stageEl.getBoundingClientRect();
    const mx = ((e.clientX - rect.left) / Math.max(1, rect.width)) - 0.5;
    const my = ((e.clientY - rect.top) / Math.max(1, rect.height)) - 0.5;
    stageEl.style.setProperty('--mx', mx.toFixed(3));
    stageEl.style.setProperty('--my', my.toFixed(3));
  };
  window.addEventListener('mousemove', moveHandler);

  const closeBtn = container.querySelector('#tk-debate-close');
  if (closeBtn) {
    closeBtn.addEventListener('click', () => {
      if (isFinished) bridge.sendChoice(scene.sceneId, 'leave');
      else bridge.sendClose(scene.sceneId);
    });
  }

  container.querySelectorAll('[data-choice-id]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const id = btn.getAttribute('data-choice-id');
      if (id) bridge.sendChoice(scene.sceneId, id);
    });
  });

  keyHandler = (e) => {
    if (bridge.getActiveKind() !== 'debate' || !currentScene) return;
    const digit = parseInt(e.key, 10);
    if (digit >= 1 && digit <= 6 && choices[digit - 1] && choices[digit - 1].enabled !== false) {
      e.preventDefault();
      bridge.sendChoice(currentScene.sceneId, choices[digit - 1].id);
    }
  };
  window.addEventListener('keydown', keyHandler);
}

SimTBridge.registerStage('debate', {
  render: renderDebateScene,
  unmount: unmountDebateScene
});
