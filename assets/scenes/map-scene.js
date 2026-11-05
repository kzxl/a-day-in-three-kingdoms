import { SimTBridge } from './bridge.js';

let animHandle = 0;

function stopTravelAnim() {
  if (animHandle) {
    cancelAnimationFrame(animHandle);
    animHandle = 0;
  }
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
    panY: 0
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

  function applyTransform() {
    canvas.style.transform = `translate(${state.panX}px, ${state.panY}px) scale(${state.zoom})`;
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

  function startTravelAnimation(pathIds, quote, targetCityId) {
    const pts = pathIds.map((id) => byId.get(id)).filter(Boolean);
    if (pts.length < 2) {
      bridge.sendChoice('map', `travel:${targetCityId}:${quote.modeKey}`);
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
        bridge.sendChoice('map', `travel:${targetCityId}:${quote.modeKey}`);
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
      const btn = el('button', 'tk-map-city');
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

    tokenImg.style.left = `${state.px}px`;
    tokenImg.style.top = `${state.py}px`;

    bottomBar.replaceChildren();
    const layers = [
      ['city', 'Thành trì'],
      ['price', 'Giá hàng'],
      ['danger', 'Độ hiểm trở']
    ];
    for (const [key, label] of layers) {
      const btn = el('button', `tk-map-lay${state.layer === key ? ' is-active' : ''}`, label);
      btn.type = 'button';
      btn.addEventListener('click', () => {
        state.layer = key;
        redrawAll();
      });
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
    headBox.appendChild(el('div', 'tk-map-sel-name', selCity.nameVi));
    headBox.appendChild(el('div', 'tk-map-sel-biome', selCity.biomeVi));
    rightCol.appendChild(headBox);

    const chips = el('div', 'tk-map-chips');
    const p = pricePct(selCity);
    const dangerLabel = ['', 'thấp', 'vừa', 'cao'][Math.max(1, Math.min(3, selCity.dangerLevel || 1))];
    chips.appendChild(el('span', 'tk-map-chip', `Giá hàng ${p > 0 ? '+' : ''}${p}%`));
    chips.appendChild(el('span', 'tk-map-chip', `Hiểm trở ${dangerLabel}`));
    rightCol.appendChild(chips);

    const fTopo = el('div', 'tk-map-field');
    fTopo.append(el('div', 'tk-map-field-label', 'Địa thế'), el('div', '', selCity.topographyVi));
    const fClim = el('div', 'tk-map-field');
    fClim.append(el('div', 'tk-map-field-label', 'Khí hậu'), el('div', '', selCity.climateVi));
    const fSpec = el('div', 'tk-map-field');
    fSpec.append(
      el('div', 'tk-map-field-label', 'Sản vật'),
      el('div', 'tk-map-spec', selCity.primarySpecialtyVi),
      el('div', 'tk-map-spec', selCity.secondarySpecialtyVi)
    );
    rightCol.append(fTopo, fClim, fSpec, el('div', 'tk-map-divider'));

    if (isHere) {
      rightCol.appendChild(el('div', 'tk-map-field', 'Bạn đang ở đây. Chọn thành khác để lên đường.'));
      return;
    }

    const routeNames = pathIds.map((id) => byId.get(id)?.nameVi || `#${id}`).join(' → ');
    const routeBox = el('div', 'tk-map-route');
    const routeLabel = el('span', 'tk-map-field-label', 'Lộ trình: ');
    routeBox.append(routeLabel, document.createTextNode(routeNames));
    rightCol.appendChild(routeBox);

    for (const q of selCity.quotes || []) {
      const mBtn = el('button', `tk-map-mode${q.modeKey === state.mode ? ' is-active' : ''}`);
      mBtn.type = 'button';
      const leftSpan = el('span');
      const mTitle = el('span', '', q.nameVi);
      mTitle.style.display = 'block';
      mTitle.style.fontWeight = '600';
      const mNote = el('span', 'tk-map-field-label', q.noteVi);
      mNote.style.display = 'block';
      mNote.style.fontSize = '12px';
      leftSpan.append(mTitle, mNote);

      const rightSpan = el('span');
      rightSpan.style.textAlign = 'right';
      const mDays = el('span', '', `${q.actualDays} ngày`);
      mDays.style.display = 'block';
      mDays.style.fontWeight = '600';
      const mFee = el('span', 'tk-map-field-label', q.fee > 0 ? `${q.fee.toLocaleString('vi-VN')} quan` : 'Miễn phí');
      mFee.style.display = 'block';
      mFee.style.fontSize = '12px';
      rightSpan.append(mDays, mFee);

      mBtn.append(leftSpan, rightSpan);
      mBtn.addEventListener('click', () => {
        if (state.moving) return;
        state.mode = q.modeKey;
        redrawAll();
      });
      rightCol.appendChild(mBtn);
    }

    if (activeQuote) {
      const canAfford = (activeQuote.fee || 0) <= (scene.cash || 0);
      const goBtn = el(
        'button',
        'tk-map-go',
        state.moving
          ? 'Đang đi đường...'
          : !canAfford
            ? 'Không đủ tiền'
            : `Lên đường · ${activeQuote.actualDays} ngày`
      );
      goBtn.type = 'button';
      goBtn.disabled = state.moving || !canAfford;
      goBtn.addEventListener('click', () => {
        if (state.moving || !canAfford) return;
        startTravelAnimation(pathIds, activeQuote, selCity.id);
      });
      rightCol.appendChild(goBtn);
    }
  }

  redrawAll();
}

SimTBridge.registerStage('map', {
  render: renderMapScene,
  unmount: stopTravelAnim
});
