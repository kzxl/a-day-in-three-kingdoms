import { SimTBridge } from './bridge.js';

let animHandle = 0;
let seasonAnimHandle = 0;
let resizeObs = null;
let visChangeHandler = null;

export const MAP_WIDTH = 800;
export const MAP_HEIGHT = 640;
export const FACTION_GRID_DIV = 8;
export const FACTION_MAX_RADIUS = 105;
export const SEASON_MAX_PARTICLES = 60;
const FACTION_STORAGE_KEY = 'simt.map.showFactions';

export const SEASON_NAMES_VI = {
  spring: 'Mùa Xuân',
  summer: 'Mùa Hạ',
  autumn: 'Mùa Thu',
  winter: 'Mùa Đông'
};

export const LABEL_POS = {
  1: 'top', 2: 'top', 3: 'right', 4: 'left', 5: 'left',
  6: 'right', 7: 'right', 8: 'bottom', 9: 'top', 10: 'left',
  11: 'bottom', 12: 'left', 13: 'right', 14: 'left', 15: 'bottom',
  16: 'left', 17: 'bottom', 18: 'left', 19: 'left', 20: 'bottom'
};

function stopTravelAnim() {
  if (animHandle) { cancelAnimationFrame(animHandle); animHandle = 0; }
  if (seasonAnimHandle) { cancelAnimationFrame(seasonAnimHandle); seasonAnimHandle = 0; }
  if (resizeObs) { resizeObs.disconnect(); resizeObs = null; }
  if (visChangeHandler && typeof document !== 'undefined' && typeof document.removeEventListener === 'function') {
    document.removeEventListener('visibilitychange', visChangeHandler);
    visChangeHandler = null;
  }
}

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined && text !== null) node.textContent = String(text);
  return node;
}

function loadShowFactions() {
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      const raw = window.localStorage.getItem(FACTION_STORAGE_KEY);
      if (raw !== null) return raw !== '0' && raw !== 'false';
    }
  } catch {
    // Ignore storage access errors
  }
  return true;
}

function saveShowFactions(val) {
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.setItem(FACTION_STORAGE_KEY, val ? '1' : '0');
    }
  } catch {
    // Ignore storage access errors
  }
}

function isReducedMotionPreferred() {
  try {
    if (typeof window !== 'undefined' && typeof window.matchMedia === 'function') {
      return Boolean(window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    }
  } catch {
    // Ignore matchMedia errors
  }
  return false;
}

export function parseHexRgb(hex) {
  const clean = String(hex || '').trim().replace(/^#/, '');
  if (clean.length === 3) {
    const r = parseInt(clean[0] + clean[0], 16);
    const g = parseInt(clean[1] + clean[1], 16);
    const b = parseInt(clean[2] + clean[2], 16);
    if (!Number.isNaN(r) && !Number.isNaN(g) && !Number.isNaN(b)) return [r, g, b];
  }
  if (clean.length >= 6) {
    const r = parseInt(clean.slice(0, 2), 16);
    const g = parseInt(clean.slice(2, 4), 16);
    const b = parseInt(clean.slice(4, 6), 16);
    if (!Number.isNaN(r) && !Number.isNaN(g) && !Number.isNaN(b)) return [r, g, b];
  }
  return [196, 160, 98];
}

export function computeFactionGrid(
  cities,
  mapW = MAP_WIDTH,
  mapH = MAP_HEIGHT,
  gridDiv = FACTION_GRID_DIV,
  maxRadius = FACTION_MAX_RADIUS
) {
  const gridW = Math.max(1, Math.round(mapW / gridDiv));
  const gridH = Math.max(1, Math.round(mapH / gridDiv));
  const cellW = mapW / gridW;
  const cellH = mapH / gridH;
  const maxDistSq = maxRadius * maxRadius;

  const validCities = Array.isArray(cities) ? cities : [];
  const rgbByFaction = new Map();
  for (const c of validCities) {
    if (c.factionId && c.factionColor && !rgbByFaction.has(c.factionId)) {
      rgbByFaction.set(c.factionId, parseHexRgb(c.factionColor));
    }
  }

  const totalCells = gridW * gridH;
  const cellFactionIds = new Array(totalCells).fill(0);

  for (let gy = 0; gy < gridH; gy += 1) {
    const cy = (gy + 0.5) * cellH;
    const rowOff = gy * gridW;
    for (let gx = 0; gx < gridW; gx += 1) {
      const cx = (gx + 0.5) * cellW;
      let bestDistSq = maxDistSq;
      let nearestCity = null;
      for (let i = 0; i < validCities.length; i += 1) {
        const c = validCities[i];
        const dx = cx - c.x;
        const dy = cy - c.y;
        const dSq = dx * dx + dy * dy;
        if (dSq <= bestDistSq) {
          bestDistSq = dSq;
          nearestCity = c;
        }
      }
      if (nearestCity && nearestCity.factionId && nearestCity.factionColor) {
        cellFactionIds[rowOff + gx] = nearestCity.factionId;
      }
    }
  }

  const borderMask = new Uint8Array(totalCells);
  const rgba = new Uint8ClampedArray(totalCells * 4);
  let paintedCount = 0;
  let borderCount = 0;

  for (let gy = 0; gy < gridH; gy += 1) {
    const rowOff = gy * gridW;
    for (let gx = 0; gx < gridW; gx += 1) {
      const idx = rowOff + gx;
      const fid = cellFactionIds[idx];
      if (!fid) continue;

      paintedCount += 1;
      let isBorder = false;
      const neighbors = [
        gx > 0 ? cellFactionIds[idx - 1] : 0,
        gx + 1 < gridW ? cellFactionIds[idx + 1] : 0,
        gy > 0 ? cellFactionIds[idx - gridW] : 0,
        gy + 1 < gridH ? cellFactionIds[idx + gridW] : 0
      ];
      for (let n = 0; n < 4; n += 1) {
        const nfid = neighbors[n];
        if (nfid && nfid !== fid) {
          isBorder = true;
          break;
        }
      }

      const rgb = rgbByFaction.get(fid) || [196, 160, 98];
      const p = idx * 4;
      rgba[p] = rgb[0];
      rgba[p + 1] = rgb[1];
      rgba[p + 2] = rgb[2];
      if (isBorder) {
        borderMask[idx] = 1;
        borderCount += 1;
        rgba[p + 3] = 166; // ~0.65 alpha at inter-faction borders
      } else {
        rgba[p + 3] = 51; // ~0.20 alpha for territory fill
      }
    }
  }

  return { gridW, gridH, cellFactionIds, borderMask, rgba, paintedCount, borderCount };
}

export function paintFactionCanvas(factionCanvas, cities, showFactions = true) {
  if (!factionCanvas) return { paintedCount: 0, borderCount: 0 };
  const w = factionCanvas.width || MAP_WIDTH;
  const h = factionCanvas.height || MAP_HEIGHT;
  const ctx = typeof factionCanvas.getContext === 'function' ? factionCanvas.getContext('2d') : null;

  if (!showFactions) {
    if (ctx && typeof ctx.clearRect === 'function') ctx.clearRect(0, 0, w, h);
    if (factionCanvas.dataset) {
      factionCanvas.dataset.visible = '0';
      factionCanvas.dataset.paintedCells = '0';
      factionCanvas.dataset.borderCells = '0';
    }
    factionCanvas._lastGrid = null;
    return { paintedCount: 0, borderCount: 0 };
  }

  const grid = computeFactionGrid(cities, w, h, FACTION_GRID_DIV, FACTION_MAX_RADIUS);
  factionCanvas._lastGrid = grid;
  if (factionCanvas.dataset) {
    factionCanvas.dataset.visible = '1';
    factionCanvas.dataset.paintedCells = String(grid.paintedCount);
    factionCanvas.dataset.borderCells = String(grid.borderCount);
  }

  if (ctx) {
    if (typeof ctx.clearRect === 'function') ctx.clearRect(0, 0, w, h);
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';

    let drewScaled = false;
    if (typeof document !== 'undefined' && typeof document.createElement === 'function') {
      const off = document.createElement('canvas');
      off.width = grid.gridW;
      off.height = grid.gridH;
      const offCtx = typeof off.getContext === 'function' ? off.getContext('2d') : null;
      if (offCtx && typeof offCtx.createImageData === 'function' && typeof offCtx.putImageData === 'function' && typeof ctx.drawImage === 'function') {
        const imgData = offCtx.createImageData(grid.gridW, grid.gridH);
        imgData.data.set(grid.rgba);
        offCtx.putImageData(imgData, 0, 0);
        ctx.drawImage(off, 0, 0, w, h);
        drewScaled = true;
      }
    }

    if (!drewScaled && typeof ctx.putImageData === 'function') {
      ctx.putImageData({ data: grid.rgba, width: grid.gridW, height: grid.gridH }, 0, 0);
    }
  }

  return grid;
}

export function resolveSceneFactions(scene, cities) {
  if (scene && Array.isArray(scene.factions) && scene.factions.length > 0) {
    return scene.factions.filter((f) => f && f.cityCount > 0);
  }
  const map = new Map();
  for (const c of Array.isArray(cities) ? cities : []) {
    if (!c.factionId || !c.factionNameVi || !c.factionColor) continue;
    const prev = map.get(c.factionId);
    if (prev) {
      prev.cityCount += 1;
    } else {
      map.set(c.factionId, {
        id: c.factionId,
        nameVi: c.factionNameVi,
        color: c.factionColor,
        cityCount: 1
      });
    }
  }
  return Array.from(map.values());
}

export function createSeasonParticles(season, width = MAP_WIDTH, height = MAP_HEIGHT, maxCount = SEASON_MAX_PARTICLES) {
  const key = String(season || 'spring').toLowerCase();
  if (key === 'summer' || (key !== 'spring' && key !== 'autumn' && key !== 'winter')) {
    return [];
  }
  const count = Math.min(SEASON_MAX_PARTICLES, Math.max(0, maxCount));
  const targetCount = Math.min(count, key === 'winter' ? 48 : 40);
  const list = [];
  for (let i = 0; i < targetCount; i += 1) {
    const seed = (i * 9301 + 49297) % 233280;
    const norm = seed / 233280;
    const seed2 = ((i + 7) * 4391 + 19381) % 233280;
    const norm2 = seed2 / 233280;
    list.push({
      x: norm * width,
      y: norm2 * height,
      vx: key === 'winter' ? (norm - 0.5) * 14 : 12 + norm * 18,
      vy: key === 'winter' ? 22 + norm2 * 20 : 18 + norm2 * 22,
      size: key === 'winter' ? 2 + (i % 3) : 3.5 + (i % 3) * 1.2,
      phase: norm * Math.PI * 2,
      colorIdx: i % 3
    });
  }
  return list;
}

export function stepSeasonParticles(particles, season, dt, width = MAP_WIDTH, height = MAP_HEIGHT) {
  if (!Array.isArray(particles) || particles.length === 0) return;
  const stepDt = Math.max(0, Math.min(0.08, dt || 0.016));
  for (let i = 0; i < particles.length; i += 1) {
    const p = particles[i];
    p.phase += stepDt * 2.2;
    p.x += (p.vx + Math.sin(p.phase) * 10) * stepDt;
    p.y += p.vy * stepDt;
    if (p.y > height + 8) {
      p.y = -6;
      p.x = ((p.x + width * 0.37) % width + width) % width;
    }
    if (p.x > width + 8) p.x -= width + 12;
    else if (p.x < -8) p.x += width + 12;
  }
}

function drawSeasonParticles(ctx, particles, season, width, height) {
  if (!ctx || typeof ctx.clearRect !== 'function') return;
  ctx.clearRect(0, 0, width, height);
  if (!Array.isArray(particles) || particles.length === 0) return;

  const palettes = {
    spring: ['rgba(255, 198, 214, 0.82)', 'rgba(245, 158, 184, 0.78)', 'rgba(255, 224, 232, 0.85)'],
    autumn: ['rgba(217, 119, 36, 0.82)', 'rgba(200, 75, 32, 0.78)', 'rgba(226, 168, 50, 0.82)'],
    winter: ['rgba(240, 246, 252, 0.86)', 'rgba(219, 234, 254, 0.80)', 'rgba(255, 255, 255, 0.90)']
  };
  const colors = palettes[season] || palettes.spring;

  for (let i = 0; i < particles.length; i += 1) {
    const p = particles[i];
    ctx.fillStyle = colors[p.colorIdx % colors.length];
    ctx.beginPath();
    if (season === 'winter') {
      ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
    } else if (typeof ctx.ellipse === 'function') {
      ctx.ellipse(p.x, p.y, p.size, p.size * 0.6, p.phase * 0.5, 0, Math.PI * 2);
    } else {
      ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
    }
    ctx.fill();
  }
}

export function computeMapLayoutBoxes(winW = 1076, factionCount = 12) {
  const shellPadX = 22;
  const bodyGap = 14;
  const rightW = Math.min(336, Math.max(276, Math.round((winW - shellPadX * 2) * 0.31)));
  const leftW = Math.max(480, winW - shellPadX * 2 - rightW - bodyGap);
  const viewportH = winW <= 1076 ? 430 : 510;

  const viewportBox = { left: shellPadX, top: 96, right: shellPadX + leftW, bottom: 96 + viewportH };
  const rightPanelBox = { left: viewportBox.right + bodyGap, top: 96, right: viewportBox.right + bodyGap + rightW, bottom: 96 + viewportH + 48 };
  const goButtonBox = {
    left: rightPanelBox.left + 14,
    top: rightPanelBox.bottom - 14 - 44,
    right: rightPanelBox.right - 14,
    bottom: rightPanelBox.bottom - 14
  };

  const rows = Math.ceil(Math.max(1, factionCount) / 2);
  const legendW = 208;
  const legendH = 28 + rows * 16 + 8;
  const factionLegendBox = {
    left: viewportBox.right - 8 - legendW,
    top: viewportBox.bottom - 8 - legendH,
    right: viewportBox.right - 8,
    bottom: viewportBox.bottom - 8
  };

  return { viewportBox, factionLegendBox, rightPanelBox, goButtonBox };
}

function pricePct(city) {
  return Math.round(((city.priceFactorPermille || 1000) - 1000) / 10);
}

function cityColor(city, currentCityId, layer) {
  if (layer === 'price') {
    const pf = city.priceFactorPermille || 1000;
    return pf >= 1100 ? '#a8321f' : pf >= 1010 ? '#c9962e' : '#3f8f73';
  }
  if (layer === 'danger') {
    const d = Math.max(1, Math.min(3, city.dangerLevel || 1));
    return ['', '#3f8f73', '#c9962e', '#a8321f'][d];
  }
  return city.id === currentCityId ? '#a8321f' : '#ecdfc2';
}

function cityLabel(city, layer) {
  if (layer === 'price') {
    const p = pricePct(city);
    return `${city.nameVi} ${p > 0 ? '+' : ''}${p}%`;
  }
  return city.nameVi;
}

export function computeAvailableMapBag(currentBag, rationsNeeded = 0) {
  const rows = Array.isArray(currentBag) ? currentBag : [];
  const gaoRow = rows.find((r) => r.itemId === 1);
  const meatRow = rows.find((r) => r.itemId === 8);
  let needed = Math.max(0, rationsNeeded || 0);
  const usedGao = Math.min(needed, Math.max(0, gaoRow ? gaoRow.quantity : 0));
  needed -= usedGao;
  const usedMeat = Math.min(needed, Math.max(0, meatRow ? meatRow.quantity : 0));

  return rows
    .map((r) => {
      const deducted = r.itemId === 1 ? usedGao : r.itemId === 8 ? usedMeat : 0;
      return {
        itemId: r.itemId,
        nameVi: r.nameVi,
        basePrice: r.basePrice || 1,
        availableQty: Math.max(0, (r.quantity || 0) - deducted)
      };
    })
    .filter((r) => r.availableQty > 0)
    .sort((a, b) => (b.basePrice - a.basePrice) || (a.itemId - b.itemId));
}

export function computeDefaultMapLuggage(currentBag, rationsNeeded = 0, carryCapacity = 40) {
  const avail = computeAvailableMapBag(currentBag, rationsNeeded);
  const selected = new Map();
  let rem = Math.max(0, carryCapacity || 0);
  for (const item of avail) {
    const take = Math.min(item.availableQty, rem);
    selected.set(item.itemId, take);
    rem -= take;
  }
  return { avail, selected };
}

export function renderMapScene(container, scene, bridge) {
  stopTravelAnim();
  if (!container || !scene) return;
  container.replaceChildren();

  const cities = Array.isArray(scene.cities) ? scene.cities : [];
  const routes = Array.isArray(scene.routes) ? scene.routes : [];
  const factions = resolveSceneFactions(scene, cities);
  const seasonKey = ['spring', 'summer', 'autumn', 'winter'].includes(String(scene.season || '').toLowerCase())
    ? String(scene.season).toLowerCase()
    : 'spring';
  const seasonLabelVi = SEASON_NAMES_VI[seasonKey] || 'Mùa Xuân';

  const byId = new Map();
  for (const c of cities) byId.set(c.id, c);

  const curCity = byId.get(scene.currentCityId) || cities[0];
  if (!curCity) return;

  const firstTarget = cities.find((c) => c.id !== curCity.id) || curCity;
  const state = {
    selectedCityId: firstTarget.id,
    layer: 'city',
    mode: 'walk',
    moving: false,
    px: curCity.x,
    py: curCity.y,
    zoom: 1,
    panX: 0,
    panY: 0,
    showFactions: loadShowFactions(),
    luggageOpen: false,
    luggageKey: '',
    luggageSelected: new Map(),
    luggageAvail: []
  };

  const shell = el('div', 'tk-map-shell');
  const header = el('div', 'tk-map-header');
  const titleWrap = el('div');
  titleWrap.appendChild(el('h1', 'tk-map-title', 'Bản Đồ Thiên Hạ'));
  titleWrap.appendChild(el('div', 'tk-map-sub', 'Bấm một thành để xem đường đi, chi phí và sản vật, rồi lên đường.'));
  const statusEl = el(
    'div',
    'tk-map-status',
    `Đang ở ${scene.currentCityName || curCity.nameVi} · ${scene.dateText || ''} · ${seasonLabelVi} · ${(scene.cash || 0).toLocaleString('vi-VN')} quan · Mệt ${scene.fatigue || 0}%`
  );
  statusEl.setAttribute('data-season', seasonKey);
  header.append(titleWrap, statusEl);
  shell.appendChild(header);

  if (scene.recommendation) {
    const rec = el('div', 'tk-map-rec');
    rec.appendChild(el('span', 'tk-map-rec-tag', 'NÊN LÀM'));
    rec.appendChild(el('span', '', scene.recommendation));
    shell.appendChild(rec);
  }

  const body = el('div', 'tk-map-body');
  const leftCol = el('div', 'tk-map-left');
  const viewport = el('div', `tk-map-viewport season-${seasonKey}`);
  const canvas = el('div', `tk-map-canvas season-${seasonKey}`);
  canvas.setAttribute('data-season', seasonKey);

  const bgLayer = el('div', `tk-map-bg season-${seasonKey}`);
  bgLayer.style.backgroundImage = `url("${scene.mapImagePath || 'art/map/thien_ha.webp'}")`;

  const factionCanvas = el('canvas', 'tk-map-faction-canvas');
  factionCanvas.width = MAP_WIDTH;
  factionCanvas.height = MAP_HEIGHT;

  const svgNS = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(svgNS, 'svg');
  svg.setAttribute('viewBox', '0 0 800 640');
  svg.setAttribute('class', 'tk-map-svg');

  const tagsLayer = el('div');
  const citiesLayer = el('div');
  const tokenImg = el('img', 'tk-map-token');
  tokenImg.src = scene.playerPortraitPath || 'art/portraits/player/face.webp';
  tokenImg.alt = 'Vị trí của bạn';

  canvas.append(bgLayer, factionCanvas, svg, tagsLayer, citiesLayer, tokenImg);

  const seasonCanvas = el('canvas', 'tk-map-season-canvas');
  seasonCanvas.width = MAP_WIDTH;
  seasonCanvas.height = MAP_HEIGHT;

  const factionBox = el('aside', 'tk-map-faction-box');
  viewport.append(canvas, seasonCanvas, factionBox);

  const bottomBar = el('div', 'tk-map-bar');
  const rightCol = el('div', 'tk-map-right');

  leftCol.append(viewport, bottomBar);
  body.append(leftCol, rightCol);
  shell.appendChild(body);
  container.appendChild(shell);

  let lastFactionPaintKey = '';
  function ensureFactionCanvasPainted() {
    const sig = `${state.showFactions ? 1 : 0}:${cities.map((c) => `${c.id}-${c.factionId || 0}-${c.factionColor || ''}`).join('|')}`;
    if (sig === lastFactionPaintKey) return;
    lastFactionPaintKey = sig;
    paintFactionCanvas(factionCanvas, cities, state.showFactions);
  }

  function renderFactionLegend() {
    factionBox.replaceChildren();
    if (factions.length === 0) {
      factionBox.hidden = true;
      return;
    }
    factionBox.hidden = false;

    const head = el('div', 'tk-map-faction-head');
    head.appendChild(el('span', 'tk-map-faction-title', 'Chư hầu cát cứ'));
    const toggleBtn = el(
      'button',
      `tk-map-faction-toggle${state.showFactions ? ' is-active' : ''}`,
      'Thế lực'
    );
    toggleBtn.type = 'button';
    toggleBtn.setAttribute('aria-pressed', state.showFactions ? 'true' : 'false');
    toggleBtn.addEventListener('click', () => {
      state.showFactions = !state.showFactions;
      saveShowFactions(state.showFactions);
      ensureFactionCanvasPainted();
      renderFactionLegend();
    });
    head.appendChild(toggleBtn);
    factionBox.appendChild(head);

    const list = el('div', `tk-map-faction-list${state.showFactions ? '' : ' is-muted'}`);
    for (const f of factions) {
      const item = el('div', 'tk-map-faction-item');
      const sw = el('span', 'tk-map-faction-swatch');
      sw.style.background = f.color || '#c4a062';
      const txt = el('span', 'tk-map-faction-name', `${f.nameVi} (${f.cityCount})`);
      item.append(sw, txt);
      list.appendChild(item);
    }
    factionBox.appendChild(list);
  }

  function setupSeasonParticles() {
    const ctx = typeof seasonCanvas.getContext === 'function' ? seasonCanvas.getContext('2d') : null;
    const particles = isReducedMotionPreferred()
      ? []
      : createSeasonParticles(seasonKey, MAP_WIDTH, MAP_HEIGHT, SEASON_MAX_PARTICLES);
    seasonCanvas.dataset.particleCount = String(particles.length);

    if (!ctx || particles.length === 0) {
      if (ctx && typeof ctx.clearRect === 'function') ctx.clearRect(0, 0, MAP_WIDTH, MAP_HEIGHT);
      return;
    }

    drawSeasonParticles(ctx, particles, seasonKey, MAP_WIDTH, MAP_HEIGHT);

    let lastNow = typeof performance !== 'undefined' ? performance.now() : 0;
    function canAnimateSeason() {
      if (isReducedMotionPreferred()) return false;
      if (typeof document !== 'undefined' && document.hidden) return false;
      if (container.hidden) return false;
      return particles.length > 0;
    }

    function stepSeason(now) {
      seasonAnimHandle = 0;
      if (!canAnimateSeason()) return;
      const dt = Math.min(0.05, Math.max(0.001, (now - lastNow) / 1000));
      lastNow = now;
      stepSeasonParticles(particles, seasonKey, dt, MAP_WIDTH, MAP_HEIGHT);
      drawSeasonParticles(ctx, particles, seasonKey, MAP_WIDTH, MAP_HEIGHT);
      if (typeof requestAnimationFrame === 'function') {
        seasonAnimHandle = requestAnimationFrame(stepSeason);
      }
    }

    visChangeHandler = () => {
      if (!canAnimateSeason()) {
        if (seasonAnimHandle) {
          cancelAnimationFrame(seasonAnimHandle);
          seasonAnimHandle = 0;
        }
      } else if (!seasonAnimHandle && typeof requestAnimationFrame === 'function') {
        lastNow = typeof performance !== 'undefined' ? performance.now() : 0;
        seasonAnimHandle = requestAnimationFrame(stepSeason);
      }
    };

    if (typeof document !== 'undefined' && typeof document.addEventListener === 'function') {
      document.addEventListener('visibilitychange', visChangeHandler);
    }

    if (canAnimateSeason() && typeof requestAnimationFrame === 'function') {
      seasonAnimHandle = requestAnimationFrame(stepSeason);
    }
  }

  function getFitScale() {
    const vw = viewport.clientWidth || 520;
    const vh = viewport.clientHeight || 480;
    return Math.min(1.15, Math.max(0.52, Math.min((vw - 12) / 800, (vh - 12) / 640)));
  }

  function applyTransform() {
    const effScale = getFitScale() * state.zoom;
    canvas.style.transform = `translate(${state.panX.toFixed(1)}px, ${state.panY.toFixed(1)}px) scale(${effScale.toFixed(4)})`;
  }

  if (typeof ResizeObserver !== 'undefined') {
    resizeObs = new ResizeObserver(() => applyTransform());
    resizeObs.observe(viewport);
  }

  let dragStart = null;
  viewport.addEventListener('wheel', (e) => {
    e.preventDefault();
    const factor = e.deltaY < 0 ? 1.12 : 0.89;
    state.zoom = Math.max(0.85, Math.min(2.4, state.zoom * factor));
    applyTransform();
  }, { passive: false });

  viewport.addEventListener('pointerdown', (e) => {
    if (e.target.closest('button') || e.target.closest('.tk-map-faction-box')) return;
    dragStart = { x: e.clientX - state.panX, y: e.clientY - state.panY };
    viewport.classList.add('is-dragging');
    viewport.setPointerCapture(e.pointerId);
  });

  viewport.addEventListener('pointermove', (e) => {
    if (!dragStart) return;
    state.panX = e.clientX - dragStart.x;
    state.panY = e.clientY - dragStart.y;
    applyTransform();
  });

  viewport.addEventListener('pointerup', () => {
    dragStart = null;
    viewport.classList.remove('is-dragging');
  });

  function getSelectedQuote(selCity) {
    if (!selCity || !Array.isArray(selCity.quotes) || selCity.quotes.length === 0) return null;
    return selCity.quotes.find((q) => q.modeKey === state.mode) || selCity.quotes[0];
  }

  function findRouteDays(a, b) {
    const r = routes.find((x) => (x.fromId === a && x.toId === b) || (x.fromId === b && x.toId === a));
    return r ? r.days : 2;
  }

  function syncLuggageForQuote(quote) {
    const cap = quote?.carryCapacity || (state.mode === 'horse' ? 20 : state.mode === 'caravan' ? 120 : 40);
    const rations = quote?.rations || 0;
    const key = `${state.mode}:${cap}:${rations}`;
    const { avail, selected } = computeDefaultMapLuggage(scene.currentBag, rations, cap);
    state.luggageAvail = avail;
    if (state.luggageKey !== key) {
      state.luggageKey = key;
      state.luggageSelected = selected;
    } else {
      for (const item of avail) {
        const cur = state.luggageSelected.get(item.itemId) ?? 0;
        state.luggageSelected.set(item.itemId, Math.max(0, Math.min(item.availableQty, cur)));
      }
    }
  }

  function buildTravelCommand(targetCityId, quote) {
    const spec = state.luggageAvail
      .map((it) => [it.itemId, state.luggageSelected.get(it.itemId) || 0])
      .filter(([, q]) => q > 0)
      .map(([id, q]) => `${id}=${q}`)
      .join(',');
    return `travel:${targetCityId}:${quote.modeKey}:${spec}`;
  }

  function startTravelAnimation(pathIds, quote, targetCityId) {
    const travelCmd = buildTravelCommand(targetCityId, quote);
    const pts = pathIds.map((id) => byId.get(id)).filter(Boolean);
    if (pts.length < 2) {
      bridge.sendChoice('map', travelCmd);
      return;
    }

    const segs = [];
    let totalDist = 0;
    for (let i = 0; i < pts.length - 1; i += 1) {
      const len = Math.hypot(pts[i + 1].x - pts[i].x, pts[i + 1].y - pts[i].y);
      segs.push(len);
      totalDist += len;
    }

    state.moving = true;
    redrawAll();

    const duration = 700 + Math.min(6, quote.actualDays || 2) * 180;
    const t0 = performance.now();

    function step(now) {
      const k = Math.min(1, (now - t0) / duration);
      const ease = k < 0.5 ? 2 * k * k : 1 - Math.pow(-2 * k + 2, 2) / 2;
      let dist = ease * totalDist;
      let idx = 0;
      while (idx < segs.length - 1 && dist > segs[idx]) {
        dist -= segs[idx];
        idx += 1;
      }
      const ratio = segs[idx] > 0 ? dist / segs[idx] : 1;
      const a = pts[idx];
      const b = pts[idx + 1];
      state.px = a.x + (b.x - a.x) * ratio;
      state.py = a.y + (b.y - a.y) * ratio;
      tokenImg.style.left = `${state.px}px`;
      tokenImg.style.top = `${state.py}px`;

      if (k < 1) {
        animHandle = requestAnimationFrame(step);
      } else {
        animHandle = 0;
        bridge.sendChoice('map', travelCmd);
      }
    }

    animHandle = requestAnimationFrame(step);
  }

  function redrawAll() {
    ensureFactionCanvasPainted();

    const selCity = byId.get(state.selectedCityId) || curCity;
    const isHere = selCity.id === curCity.id;
    const activeQuote = isHere ? null : getSelectedQuote(selCity);
    const pathIds = activeQuote && Array.isArray(activeQuote.pathNodeIds) ? activeQuote.pathNodeIds : [];

    const onPathEdges = new Set();
    for (let i = 0; i < pathIds.length - 1; i += 1) {
      const a = Math.min(pathIds[i], pathIds[i + 1]);
      const b = Math.max(pathIds[i], pathIds[i + 1]);
      onPathEdges.add(`${a}-${b}`);
    }

    svg.replaceChildren();
    for (const r of routes) {
      const a = byId.get(r.fromId);
      const b = byId.get(r.toId);
      if (!a || !b) continue;
      const key = `${Math.min(r.fromId, r.toId)}-${Math.max(r.fromId, r.toId)}`;
      const isAct = onPathEdges.has(key);
      const line = document.createElementNS(svgNS, 'line');
      line.setAttribute('x1', String(a.x));
      line.setAttribute('y1', String(a.y));
      line.setAttribute('x2', String(b.x));
      line.setAttribute('y2', String(b.y));
      line.setAttribute('stroke', isAct ? '#a8321f' : '#2a1f16');
      line.setAttribute('stroke-width', isAct ? '4' : '1.4');
      if (!isAct) line.setAttribute('stroke-dasharray', '5 5');
      line.setAttribute('stroke-opacity', isAct ? '1' : '0.55');
      svg.appendChild(line);
    }

    tagsLayer.replaceChildren();
    for (let i = 0; i < pathIds.length - 1; i += 1) {
      const a = byId.get(pathIds[i]);
      const b = byId.get(pathIds[i + 1]);
      if (!a || !b) continue;
      const tag = el('div', 'tk-map-day-tag', `${findRouteDays(a.id, b.id)}n`);
      tag.style.left = `${(a.x + b.x) / 2}px`;
      tag.style.top = `${(a.y + b.y) / 2}px`;
      tagsLayer.appendChild(tag);
    }

    citiesLayer.replaceChildren();
    for (const c of cities) {
      const pos = LABEL_POS[c.id] || 'bottom';
      const btn = el('button', `tk-map-city pos-${pos}`);
      btn.type = 'button';
      if (c.id === selCity.id) btn.classList.add('is-selected');
      if (c.id === curCity.id) btn.classList.add('is-current');
      btn.style.left = `${c.x}px`;
      btn.style.top = `${c.y}px`;
      btn.setAttribute('aria-label', c.nameVi);

      const dot = el('span', 'tk-map-dot');
      dot.style.background = cityColor(c, curCity.id, state.layer);
      if (c.factionColor) {
        dot.style.borderColor = c.factionColor;
      }
      const nm = el('span', 'tk-map-nm', cityLabel(c, state.layer));
      btn.append(dot, nm);
      btn.addEventListener('click', () => {
        if (state.moving) return;
        state.selectedCityId = c.id;
        redrawAll();
      });
      citiesLayer.appendChild(btn);
    }

    const curPos = LABEL_POS[curCity.id] || 'bottom';
    tokenImg.classList.toggle('token-below', !state.moving && curPos === 'top');
    tokenImg.style.left = `${state.px}px`;
    tokenImg.style.top = `${state.py}px`;
    applyTransform();

    bottomBar.replaceChildren();
    for (const [key, label] of [['city', 'Thành trì'], ['price', 'Giá hàng'], ['danger', 'Độ hiểm trở']]) {
      const btn = el('button', `tk-map-lay${state.layer === key ? ' is-active' : ''}`, label);
      btn.type = 'button';
      btn.addEventListener('click', () => { state.layer = key; redrawAll(); });
      bottomBar.appendChild(btn);
    }

    const resetBtn = el('button', 'tk-map-lay', 'Góc nhìn 100%');
    resetBtn.type = 'button';
    resetBtn.addEventListener('click', () => {
      state.zoom = 1;
      state.panX = 0;
      state.panY = 0;
      applyTransform();
    });
    bottomBar.appendChild(resetBtn);

    const legendText = state.layer === 'price'
      ? 'Xanh rẻ · Vàng thường · Đỏ đắt'
      : state.layer === 'danger'
        ? 'Xanh an toàn · Vàng có cướp · Đỏ rất hiểm'
        : 'Đường đỏ là lộ trình đang chọn · Cuộn chuột để thu phóng';
    bottomBar.appendChild(el('div', 'tk-map-legend', legendText));

    rightCol.replaceChildren();
    const headBox = el('div');
    headBox.append(el('div', 'tk-map-sel-name', selCity.nameVi), el('div', 'tk-map-sel-biome', selCity.biomeVi));
    if (selCity.factionNameVi && selCity.governorNameVi) {
      const govLine = el('div', 'tk-map-sel-gov', `Thuộc ${selCity.factionNameVi} · Thái thú ${selCity.governorNameVi}`);
      if (selCity.factionColor) govLine.style.borderLeftColor = selCity.factionColor;
      headBox.appendChild(govLine);
    } else if (selCity.factionNameVi) {
      const govLine = el('div', 'tk-map-sel-gov', `Thuộc ${selCity.factionNameVi}`);
      if (selCity.factionColor) govLine.style.borderLeftColor = selCity.factionColor;
      headBox.appendChild(govLine);
    }
    rightCol.appendChild(headBox);

    const chips = el('div', 'tk-map-chips');
    const p = pricePct(selCity);
    const dangerLabel = ['', 'thấp', 'vừa', 'cao'][Math.max(1, Math.min(3, selCity.dangerLevel || 1))];
    chips.append(el('span', 'tk-map-chip', `Giá hàng ${p > 0 ? '+' : ''}${p}%`), el('span', 'tk-map-chip', `Hiểm trở ${dangerLabel}`));
    rightCol.appendChild(chips);

    const fTopo = el('div', 'tk-map-field');
    fTopo.append(el('div', 'tk-map-field-label', 'Địa thế'), el('div', '', selCity.topographyVi));
    const fClim = el('div', 'tk-map-field');
    fClim.append(el('div', 'tk-map-field-label', 'Khí hậu'), el('div', '', selCity.climateVi));
    const fSpec = el('div', 'tk-map-field');
    fSpec.append(el('div', 'tk-map-field-label', 'Sản vật'), el('div', 'tk-map-spec', selCity.primarySpecialtyVi), el('div', 'tk-map-spec', selCity.secondarySpecialtyVi));
    rightCol.append(fTopo, fClim, fSpec);

    if (selCity.storedItemsSummaryVi) {
      const storedBox = el('div', 'tk-map-stored', `Đồ của bạn ở đây: ${selCity.storedItemsSummaryVi}`);
      rightCol.appendChild(storedBox);
    }

    rightCol.appendChild(el('div', 'tk-map-divider'));

    if (isHere) {
      rightCol.appendChild(el('div', 'tk-map-field', 'Bạn đang ở đây. Chọn thành khác để lên đường.'));
      return;
    }

    const routeNames = pathIds.map((id) => byId.get(id)?.nameVi || `#${id}`).join(' → ');
    const routeBox = el('div', 'tk-map-route');
    routeBox.append(el('span', 'tk-map-field-label', 'Lộ trình: '), document.createTextNode(routeNames));
    rightCol.appendChild(routeBox);

    for (const q of selCity.quotes || []) {
      const mBtn = el('button', `tk-map-mode${q.modeKey === state.mode ? ' is-active' : ''}`);
      mBtn.type = 'button';
      const leftSpan = el('span');
      const mTitle = el('span', '', q.nameVi);
      mTitle.style.cssText = 'display:block;font-weight:600';
      const mNote = el('span', 'tk-map-field-label', q.noteVi);
      mNote.style.cssText = 'display:block;font-size:12px';
      leftSpan.append(mTitle, mNote);

      const rightSpan = el('span');
      rightSpan.style.textAlign = 'right';
      const mDays = el('span', '', `${q.actualDays} ngày`);
      mDays.style.cssText = 'display:block;font-weight:600';
      const mFee = el('span', 'tk-map-field-label', q.fee > 0 ? `${q.fee.toLocaleString('vi-VN')} quan` : 'Miễn phí');
      mFee.style.cssText = 'display:block;font-size:12px';
      rightSpan.append(mDays, mFee);

      mBtn.append(leftSpan, rightSpan);
      mBtn.addEventListener('click', () => {
        if (!state.moving) {
          state.mode = q.modeKey;
          state.luggageKey = '';
          redrawAll();
        }
      });
      rightCol.appendChild(mBtn);
    }

    if (activeQuote) {
      syncLuggageForQuote(activeQuote);
      const cap = activeQuote.carryCapacity || (state.mode === 'horse' ? 20 : state.mode === 'caravan' ? 120 : 40);
      const totalAvail = state.luggageAvail.reduce((s, it) => s + it.availableQty, 0);
      const totalCarried = state.luggageAvail.reduce((s, it) => s + (state.luggageSelected.get(it.itemId) || 0), 0);
      const totalLeft = Math.max(0, totalAvail - totalCarried);

      const lugWrap = el('div', 'tk-map-luggage');
      const lugBtn = el('button', 'tk-map-lug-btn');
      lugBtn.type = 'button';
      lugBtn.id = 'tk-map-luggage-toggle';
      lugBtn.append(
        el('span', 'tk-map-lug-title', 'Hành lý'),
        el('span', 'tk-map-lug-summary', `Mang ${totalCarried}/${cap} · Để lại ${totalLeft} món ${state.luggageOpen ? '▴' : '▾'}`)
      );
      lugBtn.addEventListener('click', () => {
        if (!state.moving) {
          state.luggageOpen = !state.luggageOpen;
          redrawAll();
        }
      });
      lugWrap.appendChild(lugBtn);

      if (state.luggageOpen) {
        const listBox = el('div', 'tk-map-lug-list');
        if (state.luggageAvail.length === 0) {
          listBox.appendChild(el('div', 'tk-map-lug-empty', 'Không có hàng hóa tại thành hiện tại.'));
        } else {
          for (const item of state.luggageAvail) {
            const curQty = state.luggageSelected.get(item.itemId) || 0;
            const row = el('div', 'tk-map-lug-row');
            const info = el('div', 'tk-map-lug-info');
            info.append(
              el('span', 'tk-map-lug-name', `${item.nameVi} (còn ${item.availableQty})`),
              el('span', 'tk-map-lug-price', `${item.basePrice} quan/đv`)
            );
            const ctrl = el('div', 'tk-map-lug-ctrl');
            const dec = el('button', 'tk-map-lug-step', '−');
            dec.type = 'button';
            dec.setAttribute('data-lug-dec', String(item.itemId));
            dec.disabled = state.moving || curQty <= 0;
            dec.addEventListener('click', () => {
              if (state.moving || curQty <= 0) return;
              state.luggageSelected.set(item.itemId, curQty - 1);
              redrawAll();
            });
            const val = el('span', 'tk-map-lug-val', String(curQty));
            const inc = el('button', 'tk-map-lug-step', '+');
            inc.type = 'button';
            inc.setAttribute('data-lug-inc', String(item.itemId));
            inc.disabled = state.moving || curQty >= item.availableQty || totalCarried >= cap;
            inc.addEventListener('click', () => {
              if (state.moving || curQty >= item.availableQty || totalCarried >= cap) return;
              state.luggageSelected.set(item.itemId, curQty + 1);
              redrawAll();
            });
            ctrl.append(dec, val, inc);
            row.append(info, ctrl);
            listBox.appendChild(row);
          }
        }
        lugWrap.appendChild(listBox);
      }
      rightCol.appendChild(lugWrap);

      const canAfford = (activeQuote.fee || 0) <= (scene.cash || 0);
      const goBtn = el('button', 'tk-map-go', state.moving ? 'Đang đi đường...' : !canAfford ? 'Không đủ tiền' : `Lên đường · ${activeQuote.actualDays} ngày`);
      goBtn.type = 'button';
      goBtn.disabled = state.moving || !canAfford;
      goBtn.addEventListener('click', () => { if (!state.moving && canAfford) startTravelAnimation(pathIds, activeQuote, selCity.id); });
      rightCol.appendChild(goBtn);
    }
  }

  renderFactionLegend();
  redrawAll();
  setupSeasonParticles();
}

SimTBridge.registerStage('map', { render: renderMapScene, unmount: stopTravelAnim });
