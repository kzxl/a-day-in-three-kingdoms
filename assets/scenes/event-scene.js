import { SimTBridge } from './bridge.js';

// The historical event scene reuses the talk scene's stage, dock, paper and choice styles (scene.css)
// so it reads as part of the same game; event-scene.css only adds the two-figure cast.

const HAN_NUMERALS = ['壹', '貳', '參', '肆', '伍', '陸'];

export function escapeEventHtml(str) {
  return String(str ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function splitEventSentences(narrative) {
  const raw = String(narrative ?? '').trim();
  if (!raw) return ['Thiên hạ đại biến, sử quan ghi chép vào biên niên.'];
  const parts = raw
    .split(/(?<=[.!?…])\s+/u)
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
  return parts.length > 0 ? parts : [raw];
}

let keyHandler = null;

function unmountEventScene() {
  if (keyHandler) {
    window.removeEventListener('keydown', keyHandler);
    keyHandler = null;
  }
}

function figureHtml(p) {
  return `
    <figure class="tk-event-figure">
      <div class="tk-char-rig">
        <div class="tk-char-breath">
          <img class="tk-char-img" src="${escapeEventHtml(p.portraitPath)}" alt="${escapeEventHtml(p.nameVi)}" />
        </div>
      </div>
      <figcaption class="tk-event-nameplate">
        <span class="tk-event-nameplate-name tk-one">${escapeEventHtml(p.nameVi)}</span>
        <span class="tk-event-nameplate-role tk-one">${escapeEventHtml(p.roleVi || '')}</span>
      </figcaption>
    </figure>
  `;
}

function renderEventScene(container, scene, bridge) {
  unmountEventScene();
  if (!container || !scene) return;

  const sentences = Array.isArray(scene.sentencesVi) && scene.sentencesVi.length > 0
    ? scene.sentencesVi
    : splitEventSentences(scene.narrativeVi);
  const portraits = Array.isArray(scene.portraits) ? scene.portraits.slice(0, 2) : [];
  const impacts = Array.isArray(scene.impactBadges) ? scene.impactBadges : [];
  const choices = Array.isArray(scene.choices) && scene.choices.length > 0
    ? scene.choices
    : [{ id: 'continue', labelVi: 'Tiếp tục', hintVi: 'Khép lại trang sử' }];
  // One page per sentence, then a last "Hệ quả" page, so the paper keeps one height throughout.
  const pageCount = sentences.length + (impacts.length > 0 ? 1 : 0);
  let page = 0;
  // "Đổng Trác tiến kinh — Thâu tóm triều cương": the head is the name on the paper, the tail its subtitle.
  const [titleHead, ...titleTail] = String(scene.titleVi || 'Thiên hạ đại biến').split(/\s+[—–-]\s+/);
  const subtitle = [titleTail.join(' — '), `Sử biến · ${scene.locationLabelVi || ''}`].filter(Boolean).join(' · ');

  const choicesHtml = choices.map((c, idx) => `
    <button type="button" class="tk-choice" data-choice-id="${escapeEventHtml(c.id)}" ${c.enabled === false ? 'disabled' : ''}>
      <span class="tk-choice-num">${HAN_NUMERALS[idx] || idx + 1}</span>
      <span class="tk-choice-label">${escapeEventHtml(c.labelVi)}</span>
      ${c.hintVi ? `<span class="tk-choice-tag">${escapeEventHtml(c.hintVi)}</span>` : ''}
    </button>
  `).join('');

  const effectsHtml = impacts.map((i) => `<li>${escapeEventHtml(i)}</li>`).join('');

  container.innerHTML = `
    <div class="tk-talk-stage tk-event-stage">
      <div class="tk-layer tk-talk-bg" style="background-image:url('${escapeEventHtml(scene.backgroundArtPath)}')"></div>
      <div class="tk-layer tk-event-shade"></div>
      <div class="tk-mist"></div>
      <div class="tk-layer tk-event-cast tk-event-cast-${portraits.length}">
        ${portraits.map(figureHtml).join('')}
      </div>
      <div class="tk-vignette"></div>

      <header class="tk-talk-topbar">
        <div class="tk-loc-badge">
          <span class="tk-loc-dot"></span>
          <span class="tk-loc-vi tk-one">${escapeEventHtml(scene.locationLabelVi)}</span>
          <span class="tk-loc-han">史變</span>
        </div>
        <div class="tk-topbar-right">
          <span class="tk-event-era tk-one">${escapeEventHtml(scene.eraLabelVi)}</span>
          ${scene.isReplay ? '<button type="button" class="tk-close-btn" id="tk-event-close">[ESC] Đóng</button>' : ''}
        </div>
      </header>

      <footer class="tk-talk-dock">
        <div class="tk-dialog-paper" id="tk-event-paper" title="Bấm hoặc nhấn Space để đọc tiếp">
          <div class="tk-speaker-row">
            <div class="tk-speaker-left">
              <span class="tk-seal tk-seal-sm">史</span>
              <div>
                <div class="tk-speaker-name tk-one">${escapeEventHtml(titleHead)}</div>
                <div class="tk-speaker-sub tk-one">${escapeEventHtml(subtitle)}</div>
              </div>
            </div>
            ${scene.isReplay ? '<span class="tk-mood-pill tk-one">Xem lại</span>' : ''}
          </div>
          <div class="tk-dialog-line tk-event-line" id="tk-event-line"></div>
          <div class="tk-dialog-foot">
            <span class="tk-line-count tk-num" id="tk-event-count"></span>
            <button type="button" class="tk-next-btn" id="tk-event-next"></button>
          </div>
        </div>
        <div class="tk-choices-col tk-event-choices">${choicesHtml}</div>
      </footer>
    </div>
  `;

  const lineEl = container.querySelector('#tk-event-line');
  const countEl = container.querySelector('#tk-event-count');
  const nextEl = container.querySelector('#tk-event-next');

  const showPage = () => {
    const onEffects = page >= sentences.length;
    lineEl.classList.remove('is-in');
    if (onEffects) {
      lineEl.innerHTML = `<ul class="tk-event-effects">${effectsHtml}</ul>`;
    } else {
      lineEl.textContent = sentences[page];
    }
    void lineEl.offsetWidth;
    lineEl.classList.add('is-in');
    countEl.textContent = onEffects ? 'Hệ quả' : `${page + 1} / ${sentences.length}`;
    nextEl.textContent = page >= pageCount - 1 ? 'Chọn cách ứng xử ▸' : '[SPACE] Tiếp tục ▸';
  };
  const advance = () => {
    if (page < pageCount - 1) {
      page += 1;
      showPage();
    }
  };
  showPage();

  container.querySelector('#tk-event-paper').addEventListener('click', advance);
  container.querySelectorAll('[data-choice-id]').forEach((btn) => {
    btn.addEventListener('click', () => bridge.sendChoice(scene.sceneId, btn.getAttribute('data-choice-id')));
  });
  const closeBtn = container.querySelector('#tk-event-close');
  if (closeBtn) closeBtn.addEventListener('click', () => bridge.sendChoice(scene.sceneId, 'continue'));

  keyHandler = (e) => {
    if (bridge.getActiveKind() !== 'event') return;
    if (e.code === 'Space') {
      e.preventDefault();
      advance();
      return;
    }
    if (e.key === 'Escape' && scene.isReplay) {
      e.preventDefault();
      bridge.sendChoice(scene.sceneId, 'continue');
      return;
    }
    const digit = parseInt(e.key, 10);
    if (digit >= 1 && digit <= 6 && choices[digit - 1] && choices[digit - 1].enabled !== false) {
      e.preventDefault();
      bridge.sendChoice(scene.sceneId, choices[digit - 1].id);
    }
  };
  window.addEventListener('keydown', keyHandler);
}

SimTBridge.registerStage('event', {
  render: renderEventScene,
  unmount: unmountEventScene
});
