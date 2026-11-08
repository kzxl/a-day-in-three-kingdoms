import { SimTBridge } from './bridge.js';
import { createPortraitAnimator } from './sprite-anim.js';

const HAN_NUMERALS = ['壹', '貳', '參', '肆', '伍', '陸'];

let currentScene = null;
let currentLineIdx = 0;
let keyHandler = null;
let moveHandler = null;
let rafId = null;
let portraitAnim = null;

function escapeHtml(str) {
  return String(str ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function advanceLine(container) {
  if (!currentScene || !Array.isArray(currentScene.lines) || currentScene.lines.length === 0) {
    return;
  }
  if (portraitAnim && portraitAnim.isTyping()) {
    portraitAnim.completeLine();
    return;
  }
  currentLineIdx = (currentLineIdx + 1) % currentScene.lines.length;
  const countEl = container.querySelector('#tk-talk-count');
  if (portraitAnim) {
    portraitAnim.setLine(currentScene.lines[currentLineIdx]);
  }
  if (countEl) {
    countEl.textContent = `${currentLineIdx + 1} / ${currentScene.lines.length}`;
  }
}

function renderTalkScene(container, scene, bridge) {
  unmountTalkScene();
  currentScene = scene;
  currentLineIdx = 0;

  const lines = Array.isArray(scene.lines) && scene.lines.length > 0
    ? scene.lines
    : ['Xin chào các hạ.'];
  const choices = Array.isArray(scene.choices) ? scene.choices : [];
  const nearby = Array.isArray(scene.nearbyNpcs) ? scene.nearbyNpcs : [];
  const sealChar = scene.nameHan ? String(scene.nameHan).trim().charAt(0) : '漢';
  const hasDistinctStyle = scene.styleNameVi && scene.styleNameVi.trim() !== String(scene.nameVi || '').trim();
  const stylePart = hasDistinctStyle ? `Tự ${scene.styleNameVi} · ` : '';
  const sign = scene.affinity >= 0 ? `+${scene.affinity}` : `${scene.affinity}`;

  const nearbyHtml = nearby.map((n) => `
    <button type="button"
            class="tk-npc-pill ${n.active ? 'active' : ''}"
            data-npc-nav="npc:${n.npcId}">
      <span class="tk-npc-thumb"><img src="${escapeHtml(n.facePath)}" alt="" /></span>
      <span class="tk-one">${escapeHtml(n.nameVi)}</span>
    </button>
  `).join('');

  const choicesHtml = choices.map((c, idx) => {
    const num = HAN_NUMERALS[idx] || String(idx + 1);
    const hint = c.hintVi
      ? `<span class="tk-choice-tag">${escapeHtml(c.hintVi)}</span>`
      : '';
    return `
      <button type="button"
              class="tk-choice"
              data-choice-id="${escapeHtml(c.id)}"
              ${c.enabled === false ? 'disabled' : ''}>
        <span class="tk-choice-num">${num}</span>
        <span class="tk-choice-label">${escapeHtml(c.labelVi)}</span>
        ${hint}
      </button>
    `;
  }).join('');

  const hasFrames = Boolean(scene.blinkPath && scene.talkPath);
  const portraitFramesHtml = hasFrames ? `
    <img class="tk-char-img tk-char-frame" id="tk-char-blink" src="${escapeHtml(scene.blinkPath)}" alt="" />
    <img class="tk-char-img tk-char-frame" id="tk-char-talk" src="${escapeHtml(scene.talkPath)}" alt="" />
  ` : '';

  const traits = Array.isArray(scene.traits) ? scene.traits : [];
  const traitsHtml = traits.length > 0 ? `
    <div class="tk-trait-row" aria-label="Đặc tính nhân vật">
      ${traits.map((t) => `
        <span class="tk-mood-pill ${t.hidden ? 'is-hidden' : ''}"
              title="${escapeHtml(t.descriptionVi || '')}"
              data-trait-code="${escapeHtml(t.code || '')}">${escapeHtml(t.nameVi || '???')}</span>
      `).join('')}
    </div>
  ` : '';

  container.innerHTML = `
    <div class="tk-talk-stage" id="tk-talk-stage">
      <div class="tk-layer tk-talk-bg" style="background-image:url('${escapeHtml(scene.backgroundPath)}')"></div>
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
          ${scene.archetypeCode ? `<div class="tk-char-halo tk-halo-${escapeHtml(scene.archetypeCode)}"></div>` : ''}
          <div class="tk-char-breath">
            <img class="tk-char-img" id="tk-char-base" src="${escapeHtml(scene.portraitFull)}" alt="${escapeHtml(scene.nameVi)}" />
            ${portraitFramesHtml}
          </div>
        </div>
      </div>
      <div class="tk-layer tk-talk-fg">
        <svg class="tk-branch-svg" viewBox="0 0 420 260">
          <path d="M420,20 Q300,55 210,115 Q130,165 40,190" stroke="#22150f" stroke-width="8" fill="none" stroke-linecap="round"/>
          <circle cx="280" cy="68" r="10" fill="#d97b73" opacity="0.86"/>
          <circle cx="210" cy="115" r="11" fill="#e69891" opacity="0.88"/>
          <circle cx="145" cy="152" r="9" fill="#d97b73" opacity="0.82"/>
          <circle cx="85" cy="176" r="8" fill="#e69891" opacity="0.78"/>
        </svg>
        <div class="tk-petal tk-p1"></div>
        <div class="tk-petal tk-p2"></div>
        <div class="tk-petal tk-p3"></div>
      </div>
      <div class="tk-vignette"></div>

      <header class="tk-talk-topbar">
        <div class="tk-loc-badge">
          <span class="tk-loc-dot"></span>
          <span class="tk-loc-vi tk-one">${escapeHtml(scene.locationNameVi)}</span>
          <span class="tk-loc-han">${escapeHtml(scene.locationHan)}</span>
        </div>
        <div class="tk-topbar-right">
          <div class="tk-npc-switcher">${nearbyHtml}</div>
          <button type="button" class="tk-close-btn" id="tk-talk-close">[ESC] Đóng</button>
        </div>
      </header>

      <footer class="tk-talk-dock">
        <div class="tk-dialog-paper" id="tk-talk-paper" title="Bấm hoặc nhấn Space để xem câu tiếp">
          <div class="tk-speaker-row">
            <div class="tk-speaker-left">
              <span class="tk-seal tk-seal-sm">${escapeHtml(sealChar)}</span>
              <div>
                <div class="tk-speaker-name tk-one">${escapeHtml(scene.nameVi)}</div>
                <div class="tk-speaker-sub tk-one">${escapeHtml(stylePart + scene.roleVi)}</div>
              </div>
              <span class="tk-mood-pill tk-one">${escapeHtml(scene.personalityVi || 'Trầm ổn')}</span>
              ${scene.archetypeBadgeVi ? `<span class="tk-archetype-pill tk-one tk-archetype-${escapeHtml(scene.archetypeCode || '')}">${escapeHtml(scene.archetypeBadgeVi)}</span>` : ''}
            </div>
            <div class="tk-affinity-badge tk-num">
              <span>Hảo cảm ${escapeHtml(sign)}</span>
              <span class="tk-affinity-tier">· ${escapeHtml(scene.relationTierVi)}</span>
            </div>
          </div>
          ${traitsHtml}

          <p class="tk-dialog-line" id="tk-talk-line">“${escapeHtml(lines[0])}”</p>

          <div class="tk-dialog-foot">
            <span class="tk-line-count tk-num" id="tk-talk-count">1 / ${lines.length}</span>
            <button type="button" class="tk-next-btn" id="tk-talk-next">[SPACE] Tiếp tục ▸</button>
          </div>
        </div>

        <div class="tk-choices-col">
          ${choicesHtml}
        </div>
      </footer>
    </div>
  `;

  const lineEl = container.querySelector('#tk-talk-line');
  portraitAnim = createPortraitAnimator({
    baseEl: container.querySelector('#tk-char-base'),
    blinkEl: container.querySelector('#tk-char-blink'),
    talkEl: container.querySelector('#tk-char-talk'),
    lineEl
  });
  portraitAnim.setLine(lines[0]);

  let prev = performance.now();
  const tick = (now) => {
    if (currentScene !== scene) return;
    const dt = Math.min(0.05, (now - prev) / 1000);
    prev = now;
    if (portraitAnim) portraitAnim.step(dt);
    rafId = requestAnimationFrame(tick);
  };
  rafId = requestAnimationFrame(tick);

  const stageEl = container.querySelector('#tk-talk-stage');
  moveHandler = (e) => {
    if (!stageEl) return;
    const rect = stageEl.getBoundingClientRect();
    const mx = ((e.clientX - rect.left) / Math.max(1, rect.width)) - 0.5;
    const my = ((e.clientY - rect.top) / Math.max(1, rect.height)) - 0.5;
    stageEl.style.setProperty('--mx', mx.toFixed(3));
    stageEl.style.setProperty('--my', my.toFixed(3));
  };
  window.addEventListener('mousemove', moveHandler);

  const paperEl = container.querySelector('#tk-talk-paper');
  if (paperEl) {
    paperEl.addEventListener('click', () => advanceLine(container));
  }

  const closeBtn = container.querySelector('#tk-talk-close');
  if (closeBtn) {
    closeBtn.addEventListener('click', () => bridge.sendClose(scene.sceneId));
  }

  container.querySelectorAll('[data-choice-id]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const id = btn.getAttribute('data-choice-id');
      if (id) bridge.sendChoice(scene.sceneId, id);
    });
  });

  container.querySelectorAll('[data-npc-nav]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const navId = btn.getAttribute('data-npc-nav');
      if (navId) bridge.sendNavigate(scene.sceneId, navId);
    });
  });

  keyHandler = (e) => {
    if (bridge.getActiveKind() !== 'talk' || !currentScene) return;
    if (e.code === 'Space') {
      e.preventDefault();
      advanceLine(container);
      return;
    }
    const digit = parseInt(e.key, 10);
    if (digit >= 1 && digit <= 6 && choices[digit - 1] && choices[digit - 1].enabled !== false) {
      e.preventDefault();
      bridge.sendChoice(currentScene.sceneId, choices[digit - 1].id);
    }
  };
  window.addEventListener('keydown', keyHandler);
}

function unmountTalkScene() {
  if (rafId) {
    cancelAnimationFrame(rafId);
    rafId = null;
  }
  if (keyHandler) {
    window.removeEventListener('keydown', keyHandler);
    keyHandler = null;
  }
  if (moveHandler) {
    window.removeEventListener('mousemove', moveHandler);
    moveHandler = null;
  }
  portraitAnim = null;
  currentScene = null;
}

SimTBridge.registerStage('talk', {
  render: renderTalkScene,
  unmount: unmountTalkScene
});
