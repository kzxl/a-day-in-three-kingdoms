import { SimTBridge } from './bridge.js';

let animHandle = 0;
let resizeObs = null;

export const LABEL_POS = {
  1: 'top', 2: 'top', 3: 'right', 4: 'left', 5: 'left',
  6: 'right', 7: 'right', 8: 'bottom', 9: 'top', 10: 'left',
  11: 'bottom', 12: 'left', 13: 'right', 14: 'left', 15: 'bottom',
  16: 'left', 17: 'bottom', 18: 'left', 19: 'left', 20: 'bottom'
};

function stopTravelAnim() {
  if (animHandle) { cancelAnimationFrame(animHandle); animHandle = 0; }
  if (resizeObs) { resizeObs.disconnect(); resizeObs = null; }
}

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined && text !== null) node.textContent = String(text);
  return node;
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

function renderMapScene(container, scene, bridge) {
  stopTravelAnim();
  if (!container || !scene) return;
  container.replaceChildren();

  const cities = Array.isArray(scene.cities) ? scene.cities : [];
  const routes = Array.isArray(scene.routes) ? scene.routes : [];
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
    `Đang ở ${scene.currentCityName || curCity.nameVi} · ${scene.dateText || ''} · ${(scene.cash || 0).toLocaleString('vi-VN')} quan · Mệt ${scene.fatigue || 0}%`
  );
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
  const viewport = el('div', 'tk-map-viewport');
  const canvas = el('div', 'tk-map-canvas');
  canvas.style.backgroundImage = `url("${scene.mapImagePath || 'art/map/thien_ha.webp'}")`;

  const svgNS = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(svgNS, 'svg');
  svg.setAttribute('viewBox', '0 0 800 640');
  svg.setAttribute('class', 'tk-map-svg');
  canvas.appendChild(svg);

  const tagsLayer = el('div');
  const citiesLayer = el('div');
  const tokenImg = el('img', 'tk-map-token');
  tokenImg.src = scene.playerPortraitPath || 'art/portraits/player/face.webp';
  tokenImg.alt = 'Vị trí của bạn';
  canvas.append(tagsLayer, citiesLayer, tokenImg);
  viewport.appendChild(canvas);

  const bottomBar = el('div', 'tk-map-bar');
  const rightCol = el('div', 'tk-map-right');

  leftCol.append(viewport, bottomBar);
  body.append(leftCol, rightCol);
  shell.appendChild(body);
  container.appendChild(shell);

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
    if (e.target.closest('button')) return;
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

  redrawAll();
}

SimTBridge.registerStage('map', { render: renderMapScene, unmount: stopTravelAnim });
