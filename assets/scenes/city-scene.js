import { SimTBridge } from './bridge.js';
import { stepCityWalkers, resolveStripBodyHeight } from './sprite-anim.js';
import { createFrame, clearFrame, composite } from './vendor/pixelfx/src/core/index.js';
import { safeCreateFx } from './duel-scene.js';
import * as cherryBlossoms from './vendor/pixelfx/src/fx/weather/cherry-blossoms.js';
import * as fallingLeaves from './vendor/pixelfx/src/fx/weather/falling-leaves.js';
import * as snowFx from './vendor/pixelfx/src/fx/weather/snow.js';

let activeScene = null;
let rafId = null;

function escapeHtml(str) {
  return String(str ?? '')
    .replace(/[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}\u{FE0F}]/gu, '')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function formatCityNarrative(text) {
  return String(text ?? '').split(/\n+/).map(escapeHtml).join('<br><br>');
}

function resolveWeatherModule(effectKey) {
  switch (String(effectKey || '').toLowerCase()) {
    case 'cherry-blossoms':
    case 'spring':
      return cherryBlossoms;
    case 'falling-leaves':
    case 'autumn':
      return fallingLeaves;
    case 'snow':
    case 'winter':
      return snowFx;
    default:
      return null;
  }
}

function computeTargetBodyPx(yPct, stageH) {
  const refScale = Math.max(0.75, Math.min(1.25, (stageH || 660) / 660));
  const depthT = Math.max(0, Math.min(1, (yPct - 72) / 16));
  return Math.round((115 + depthT * 35) * refScale);
}

function renderCityScene(container, scene, bridge) {
  unmountCityScene();
  activeScene = scene;

  const hotspots = Array.isArray(scene.hotspots) ? scene.hotspots : [];
  const npcs = Array.isArray(scene.npcs) ? scene.npcs.slice(0, 6) : [];
  const choices = Array.isArray(scene.choices) ? scene.choices : [];
  const ev = scene.eventCard;
  const estStageH = Math.max(480, (container.clientHeight || 790) - 130);

  const hotspotsHtml = hotspots.map((h) => `
    <button type="button"
            class="tk-hotspot tk-one"
            style="left:${h.xPercent}%; top:${h.yPercent}%"
            data-nav-target="${escapeHtml(h.targetScreenId)}">
      <span class="tk-hotspot-dot"></span>
      <span>${escapeHtml(h.labelVi)}</span>
    </button>
  `).join('');

  let walkerIdx = 0;
  const npcsHtml = npcs.map((n, idx) => {
    if (n.animPath) {
      const wIdx = walkerIdx++;
      const yPct = wIdx === 0 ? 86 : wIdx === 1 ? 88 : Number(n.yPercent || 75);
      const bodyPx = computeTargetBodyPx(yPct, estStageH);
      const boxW = Math.round(bodyPx * 0.68);
      const stripBodyH = resolveStripBodyHeight(n.animPath);
      const stripScale = bodyPx / stripBodyH;
      const zIdx = 10 + Math.round(yPct);
      const initDir = wIdx % 2 === 0 ? 1 : -1;
      return `
        <button type="button"
                class="tk-city-walker"
                style="top:${yPct}%; width:${boxW}px; height:${bodyPx}px; z-index:${zIdx}"
                data-walker-idx="${wIdx}"
                data-walker-scale="${stripScale.toFixed(4)}"
                data-walker-boxw="${boxW}"
                data-walker-xpct="${Number(n.xPercent || 50)}"
                data-npc-idx="${idx}"
                data-nav-target="npc:${n.npcId}"
                title="Diện kiến ${escapeHtml(n.nameVi)} (${escapeHtml(n.roleVi)})">
          <div class="tk-city-walker-sh"></div>
          <div class="tk-city-walker-sp" data-walker-sp style="background-image:url('${escapeHtml(n.animPath)}'); transform:scale(${(initDir * stripScale).toFixed(3)}, ${stripScale.toFixed(3)})"></div>
          <div class="tk-city-walker-tag tk-one">
            <span>${escapeHtml(n.nameVi)}</span>
            <span class="tk-city-npc-role tk-one">${escapeHtml(n.roleVi)}</span>
          </div>
        </button>
      `;
    }
    const yPct = Number(n.yPercent || 76);
    const bodyPx = computeTargetBodyPx(yPct, estStageH);
    const zIdx = 10 + Math.round(yPct);
    return `
      <button type="button"
              class="tk-city-npc"
              style="left:${n.xPercent}%; top:${yPct}%; --npc-body-h:${bodyPx}px; z-index:${zIdx}"
              data-nav-target="npc:${n.npcId}"
              title="Diện kiến ${escapeHtml(n.nameVi)} (${escapeHtml(n.roleVi)})">
        <div class="tk-city-npc-label tk-one">
          <span>${escapeHtml(n.nameVi)}</span>
          <span class="tk-city-npc-role tk-one">${escapeHtml(n.roleVi)}</span>
        </div>
        <img class="tk-city-npc-sprite" src="${escapeHtml(n.spritePath)}" alt="${escapeHtml(n.nameVi)}" />
      </button>
    `;
  }).join('');

  const eventHtml = ev ? `
    <aside class="tk-city-event-card" id="tk-city-event-card">
      <div class="tk-event-head">
        <span class="tk-event-title tk-one">${escapeHtml(ev.titleVi)}</span>
        <span class="tk-event-loc tk-one">${escapeHtml(ev.locationVi)}</span>
      </div>
      <p class="tk-event-body">${formatCityNarrative(ev.narrativeVi)}</p>
      <div class="tk-event-reward tk-one">${escapeHtml(ev.rewardVi)}</div>
    </aside>
  ` : '';

  const choicesHtml = choices.map((c) => `
    <button type="button"
            class="tk-city-btn tk-one"
            data-choice-id="${escapeHtml(c.id)}"
            ${c.enabled === false ? 'disabled' : ''}>
      ${escapeHtml(c.labelVi)}
    </button>
  `).join('');

  container.innerHTML = `
    <div class="tk-city-shell">
      <header class="tk-city-topbar">
        <div class="tk-city-title-group">
          <span class="tk-seal tk-seal-sm">${escapeHtml((scene.cityNameHan || '城').charAt(0))}</span>
          <span class="tk-city-name tk-one">${escapeHtml(scene.cityNameVi)}</span>
          <span class="tk-city-han">${escapeHtml(scene.cityNameHan || '')}</span>
          <span class="tk-city-meta tk-one">${escapeHtml(scene.regionVi)}</span>
        </div>
        <div class="tk-city-title-group">
          <span class="tk-city-meta tk-num tk-one">${escapeHtml(scene.dateVi)} · ${escapeHtml(scene.season)}</span>
        </div>
      </header>

      <div class="tk-city-stage-wrap" id="tk-city-stage-wrap">
        <div class="tk-city-bg" style="background-image:url('${escapeHtml(scene.backgroundPath)}')"></div>
        <canvas class="tk-city-weather-canvas" id="tk-city-weather-canvas" width="480" height="270"></canvas>
        <div class="tk-city-vignette"></div>
        ${hotspotsHtml}
        ${npcsHtml}
        ${eventHtml}
      </div>

      <footer class="tk-city-dock">
        <span class="tk-city-hint tk-one">
          Bấm vào địa điểm để mở sổ quản lý, hoặc bấm vào nhân sĩ trên phố để trò chuyện trực tiếp.
        </span>
        <div class="tk-city-actions">
          ${choicesHtml}
        </div>
      </footer>
    </div>
  `;

  container.querySelectorAll('[data-nav-target]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const target = btn.getAttribute('data-nav-target');
      if (target) bridge.sendNavigate(scene.sceneId, target);
    });
  });

  container.querySelectorAll('[data-choice-id]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const id = btn.getAttribute('data-choice-id');
      if (id) bridge.sendChoice(scene.sceneId, id);
    });
  });

  const stageWrap = container.querySelector('#tk-city-stage-wrap');
  const stageW = Math.max(820, stageWrap?.clientWidth || 1080);
  const walkers = [];
  container.querySelectorAll('.tk-city-walker').forEach((el, i) => {
    const spEl = el.querySelector('[data-walker-sp]');
    const scale = parseFloat(el.getAttribute('data-walker-scale') || '1') || 1;
    const boxW = parseFloat(el.getAttribute('data-walker-boxw') || '96') || 96;
    const halfW = Math.round(boxW / 2);
    const xPct = parseFloat(el.getAttribute('data-walker-xpct') || '50') || 50;
    const canWalk = i < 2;
    const isFirst = i === 0;
    const min = isFirst ? Math.round(stageW * 0.22) : Math.round(stageW * 0.58);
    const max = isFirst ? Math.round(stageW * 0.42) : Math.round(stageW * 0.78);
    const startX = canWalk
      ? (isFirst ? Math.round(stageW * 0.30) : Math.round(stageW * 0.68))
      : Math.round(stageW * (xPct / 100));
    el.style.left = `${startX - halfW}px`;
    walkers.push({
      el,
      spEl,
      hasStrip: true,
      walk: canWalk,
      scale,
      halfW,
      x: startX,
      min,
      max,
      dir: i % 2 === 0 ? 1 : -1,
      wait: 0,
      dist: 0,
      fr: canWalk ? 4 : 0,
      it: i * 0.5
    });
  });

  const weatherMod = resolveWeatherModule(scene.weatherEffect);
  const canvas = container.querySelector('#tk-city-weather-canvas');
  const ctx = weatherMod && canvas ? canvas.getContext('2d') : null;
  const frame = ctx ? createFrame(480, 270) : null;
  const imgData = ctx ? ctx.createImageData(480, 270) : null;
  const fxA = ctx ? safeCreateFx(weatherMod, { x: 140, y: 135, seed: 11 }) : null;
  const fxB = ctx ? safeCreateFx(weatherMod, { x: 340, y: 135, seed: 29 }) : null;
  if (fxA) fxA.update(1.5);
  if (fxB) fxB.update(2.4);

  let prev = performance.now();
  const tick = (now) => {
    if (activeScene !== scene) return;
    const dt = Math.min(0.05, (now - prev) / 1000);
    prev = now;
    if (walkers.length > 0) stepCityWalkers(walkers, dt);
    if (ctx && frame && imgData) {
      clearFrame(frame);
      if (fxA) { fxA.update(dt); fxA.draw(frame); }
      if (fxB) { fxB.update(dt); fxB.draw(frame); }
      composite(frame, imgData.data, null, 1);
      ctx.putImageData(imgData, 0, 0);
    }
    rafId = requestAnimationFrame(tick);
  };
  rafId = requestAnimationFrame(tick);
}

function unmountCityScene() {
  if (rafId) {
    cancelAnimationFrame(rafId);
    rafId = null;
  }
  activeScene = null;
}

SimTBridge.registerStage('city', {
  render: renderCityScene,
  unmount: unmountCityScene
});
