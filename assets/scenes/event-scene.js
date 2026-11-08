import { SimTBridge } from './bridge.js';

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

export function computeEventLayout(winWidth, portraitCount) {
  const clampedCount = Math.max(0, Math.min(2, Number(portraitCount) || 0));
  const totalW = Math.min(1240, Math.max(320, Number(winWidth) || 1076));
  const padX = 40;
  const colW = clampedCount > 0 ? 185 : 0;
  const gaps = clampedCount === 2 ? 28 : clampedCount === 1 ? 14 : 0;
  const centerW = totalW - padX - colW * clampedCount - gaps;
  return {
    totalWidth: totalW,
    portraitCount: clampedCount,
    portraitWidth: colW,
    centerWidth: centerW,
    hasHorizontalOverflow: totalW > (Number(winWidth) || 1076) || centerW < 420
  };
}

let activeTimer = null;
let activeKeyHandler = null;

function clearEventTimers() {
  if (activeTimer !== null) {
    clearInterval(activeTimer);
    activeTimer = null;
  }
  if (activeKeyHandler !== null && typeof window !== 'undefined') {
    window.removeEventListener('keydown', activeKeyHandler);
    activeKeyHandler = null;
  }
}

function prefersReducedMotion() {
  if (typeof window !== 'undefined' && typeof window.matchMedia === 'function') {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  }
  return false;
}

function renderPortraitCard(p) {
  if (!p) return '';
  return `
    <div class="tk-event-portrait-card">
      <img class="tk-event-portrait-img" src="${escapeEventHtml(p.portraitPath)}" alt="${escapeEventHtml(p.nameVi)}" />
      <div class="tk-event-portrait-caption">
        <div class="tk-event-portrait-name">${escapeEventHtml(p.nameVi)}</div>
        <div class="tk-event-portrait-role">${escapeEventHtml(p.roleVi || '')}</div>
      </div>
    </div>
  `;
}

function renderEventStage(container, scene, bridge) {
  clearEventTimers();
  if (!container || !scene) return;

  const portraits = Array.isArray(scene.portraits) ? scene.portraits.slice(0, 2) : [];
  const sentences = splitEventSentences(scene.narrativeVi);
  const impacts = Array.isArray(scene.impactBadges) ? scene.impactBadges : [];
  const choices = Array.isArray(scene.choices) && scene.choices.length > 0
    ? scene.choices
    : [{ id: 'continue', labelVi: 'Tiếp tục', subtitleVi: 'Khép lại trang sử' }];

  const instantReveal = prefersReducedMotion() || Boolean(scene.isReplay) || sentences.length <= 1;
  let revealedCount = instantReveal ? sentences.length : 1;

  const layoutClass = portraits.length >= 2
    ? 'is-two-portraits'
    : portraits.length === 1
      ? 'is-single-portrait'
      : 'is-no-portrait';

  const leftColHtml = portraits.length >= 1
    ? `<aside class="tk-event-portrait-col">${renderPortraitCard(portraits[0])}</aside>`
    : '';
  const rightColHtml = portraits.length >= 2
    ? `<aside class="tk-event-portrait-col">${renderPortraitCard(portraits[1])}</aside>`
    : '';

  const sentencesHtml = sentences
    .map((line, idx) => `<p class="tk-event-sentence${idx >= revealedCount ? ' is-hidden' : ''}" data-sentence-idx="${idx}">${escapeEventHtml(line)}</p>`)
    .join('');

  const impactsHtml = impacts
    .map((badge) => `<span class="tk-event-impact-pill">${escapeEventHtml(badge)}</span>`)
    .join('');

  const choicesHtml = choices
    .map((ch) => {
      const isCont = String(ch.id) === 'continue';
      const subText = ch.hintVi || ch.subtitleVi || '';
      const subHtml = subText
        ? `<span class="tk-event-choice-sub">${escapeEventHtml(subText)}</span>`
        : '';
      return `
        <button type="button" class="tk-event-choice-btn${isCont ? ' is-continue' : ''}" data-choice-id="${escapeEventHtml(ch.id)}">
          <span class="tk-event-choice-label">${escapeEventHtml(ch.labelVi)}</span>
          ${subHtml}
        </button>
      `;
    })
    .join('');

  const replayBadgeHtml = scene.isReplay
    ? `<span class="tk-event-replay-badge">ĐÃ XẢY RA · XEM LẠI</span>`
    : '';

  container.innerHTML = `
    <div class="tk-event-backdrop" style="background-image: url('${escapeEventHtml(scene.backgroundArtPath || 'art/backgrounds/hall.webp')}')"></div>
    <div class="tk-event-vignette"></div>
    <div class="tk-event-layout ${layoutClass}">
      ${leftColHtml}
      <div class="tk-event-center">
        <header class="tk-event-header">
          <div class="tk-event-meta-row">
            <span class="tk-event-era-badge">📜 ${escapeEventHtml(scene.eraLabelVi || '')}</span>
            <span class="tk-event-location-badge">📍 ${escapeEventHtml(scene.locationLabelVi || 'Trung Nguyên')}</span>
            ${replayBadgeHtml}
          </div>
          <h1 class="tk-event-title">${escapeEventHtml(scene.titleVi || 'Thiên hạ đại biến')}</h1>
        </header>

        <article class="tk-event-scroll" id="tk-event-scroll-box" title="Nhấp chuột hoặc nhấn Space để mở tiếp câu chuyện">
          <div class="tk-event-sentences" id="tk-event-sentences-list">
            ${sentencesHtml}
          </div>
          <footer class="tk-event-scroll-footer">
            <div class="tk-event-impacts">${impactsHtml}</div>
            <span id="tk-event-progress-hint">${revealedCount < sentences.length ? `Câu ${revealedCount}/${sentences.length} · Nhấp hoặc Space để đọc nhanh` : `Trọn vẹn ${sentences.length} câu sử ký`}</span>
          </footer>
        </article>

        <nav class="tk-event-choices" aria-label="Quyết định trước sử biến">
          ${choicesHtml}
        </nav>
      </div>
      ${rightColHtml}
    </div>
  `;

  const updateSentenceVisibility = () => {
    const nodes = container.querySelectorAll('.tk-event-sentence');
    nodes.forEach((node, idx) => {
      if (idx < revealedCount) {
        node.classList.remove('is-hidden');
      }
    });
    const hint = container.querySelector('#tk-event-progress-hint');
    if (hint) {
      hint.textContent = revealedCount < sentences.length
        ? `Câu ${revealedCount}/${sentences.length} · Nhấp hoặc Space để đọc nhanh`
        : `Trọn vẹn ${sentences.length} câu sử ký`;
    }
    if (revealedCount >= sentences.length && activeTimer !== null) {
      clearInterval(activeTimer);
      activeTimer = null;
    }
  };

  const advanceNarrative = () => {
    if (revealedCount < sentences.length) {
      revealedCount = sentences.length;
      updateSentenceVisibility();
    }
  };

  const scrollBox = container.querySelector('#tk-event-scroll-box');
  if (scrollBox) {
    scrollBox.addEventListener('click', advanceNarrative);
  }

  if (!instantReveal && typeof setInterval === 'function') {
    activeTimer = setInterval(() => {
      if (revealedCount < sentences.length) {
        revealedCount += 1;
        updateSentenceVisibility();
      } else {
        clearEventTimers();
      }
    }, 850);
  }

  activeKeyHandler = (ev) => {
    if (ev.code === 'Space' || ev.key === ' ') {
      if (revealedCount < sentences.length) {
        ev.preventDefault();
        advanceNarrative();
      }
    }
  };
  if (typeof window !== 'undefined') {
    window.addEventListener('keydown', activeKeyHandler);
    window.SimTEventScene = { skipReveal: advanceNarrative };
  }

  const buttons = container.querySelectorAll('.tk-event-choice-btn');
  buttons.forEach((btn) => {
    btn.addEventListener('click', () => {
      const choiceId = btn.getAttribute('data-choice-id') || 'continue';
      clearEventTimers();
      bridge.sendChoice(scene.sceneId, choiceId);
    });
  });
}

SimTBridge.registerStage('event', {
  render: renderEventStage,
  unmount: clearEventTimers
});
