const NS = 'http://www.w3.org/2000/svg';
export const TRAVELLER_LABELS = { rival: 'Thương nhân', caravan: 'Thương đoàn', npc: 'Danh sĩ', army: 'Quân binh' };
export const TRAVELLER_ROWS = { rival: 0, caravan: 1, npc: 2, army: 3 };
export const TRAVELLER_SHEET = 'art/map/travellers.png';
let flagId = 0;

// Shortest connected itinerary keeps remote NPCs and freight on the road network.
export function travellerPath(traveller, cities, routes) {
  const byId = new Map(cities.map(c => [c.id, c]));
  const dist = new Map([[traveller.fromId, 0]]), prev = new Map(), done = new Set();
  while (true) {
    const next = [...dist].filter(([id]) => !done.has(id)).sort((a, b) => a[1] - b[1])[0];
    if (!next) return [];
    const [id, cost] = next;
    if (id === traveller.toId) break;
    done.add(id);
    for (const r of routes) {
      if (r.blockedReasonVi && traveller.kind !== 'army') continue;
      const to = r.fromId === id ? r.toId : r.toId === id ? r.fromId : null;
      if (to === null) continue;
      const value = cost + Math.max(1, r.days || 1);
      if (value < (dist.get(to) ?? Infinity)) { dist.set(to, value); prev.set(to, id); }
    }
  }
  const ids = [traveller.toId];
  while (ids[0] !== traveller.fromId) ids.unshift(prev.get(ids[0]));
  return ids.map(id => byId.get(id)).filter(Boolean);
}

export function travellerPosition(path, progress) {
  if (path.length < 2) return null;
  const lengths = path.slice(1).map((p, i) => Math.hypot(p.x - path[i].x, p.y - path[i].y));
  let distance = Math.max(0, Math.min(1, progress)) * lengths.reduce((a, b) => a + b, 0);
  for (let i = 0; i < lengths.length; i++) {
    if (distance <= lengths[i] || i === lengths.length - 1) {
      const t = Math.max(0.14, Math.min(0.86, lengths[i] ? distance / lengths[i] : 0));
      return { x: path[i].x + (path[i + 1].x - path[i].x) * t,
        y: path[i].y + (path[i + 1].y - path[i].y) * t,
        t, dx: (path[i + 1].x - path[i].x) / (lengths[i] || 1),
        dy: (path[i + 1].y - path[i].y) / (lengths[i] || 1) };
    }
    distance -= lengths[i];
  }
}

const overlaps = (a, b) => a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
export function travellerBox(p, width) {
  return { left: p.x - width / 2, right: p.x + width / 2, top: p.y - width * 2 / 3, bottom: p.y + 2 };
}

// All distances supplied by the caller are in map coordinates, including screen-pixel clearances.
export function placeTravellers(entries, obstacles = [], width = 54, scale = 1) {
  const placed = [];
  return entries.map(({ path, progress }, index) => {
    const base = travellerPosition(path, progress);
    if (!base) return null;
    const sign = index % 2 ? -1 : 1;
    let result = null;
    for (let attempt = 0; attempt < 161; attempt++) {
      const lane = attempt === 0 ? 0 : Math.ceil(attempt / 2) * (attempt % 2 ? sign : -sign);
      const offset = lane * 10 / scale;
      const p = { ...base, x: base.x - base.dy * offset, y: base.y + base.dx * offset };
      const box = travellerBox(p, width);
      if (box.left < 0 || box.right > 800 || box.top < 0 || box.bottom > 640) continue;
      if (obstacles.some(b => overlaps(box, b))) continue;
      if (placed.some(q => Math.hypot(q.x - p.x, q.y - p.y) * scale < 24 || overlaps(box, travellerBox(q, width)))) continue;
      result = p; break;
    }
    // Omit a marker only if the entire perpendicular corridor is occupied; never obscure a city.
    if (result) placed.push(result);
    return result;
  });
}

function svgNode(tag, attrs = {}) {
  const node = document.createElementNS(NS, tag);
  for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, String(value));
  return node;
}

export function setTravellerFrame(marker, frame = 0, left = false, width = 54) {
  const row = TRAVELLER_ROWS[marker.getAttribute('data-kind')] ?? 2;
  const sprite = marker.querySelector('.tk-map-traveller-sprite');
  sprite.setAttribute('viewBox', `${frame * 96} ${row * 64} 96 64`);
  sprite.setAttribute('x', String(-width / 2));
  sprite.setAttribute('y', String(-width * 2 / 3 - frame));
  sprite.setAttribute('width', String(width));
  sprite.setAttribute('height', String(width * 2 / 3));
  sprite.setAttribute('transform', `scale(${left ? -1 : 1} 1)`);
  const hit = marker.querySelector('.tk-map-traveller-hit');
  hit.setAttribute('x', String(-width / 2));
  hit.setAttribute('y', String(-width * 2 / 3 - 1));
  hit.setAttribute('width', String(width));
  hit.setAttribute('height', String(width * 2 / 3 + 3));
}

export function createTravellerMarker(traveller, routeLabel) {
  const g = svgNode('g', { class: 'tk-map-traveller', tabindex: 0, role: 'img', 'data-kind': traveller.kind });
  const label = `${traveller.labelVi} · ${routeLabel}`;
  g.setAttribute('aria-label', label);
  const title = svgNode('title'); title.textContent = label;
  const shadow = svgNode('ellipse', { cx: 0, cy: 0, rx: 21, ry: 3, fill: '#241a13', opacity: 0.22 });
  const hit = svgNode('rect', { class: 'tk-map-traveller-hit', x: -27, y: -36, width: 54, height: 40, fill: 'transparent' });
  const sprite = svgNode('svg', { class: 'tk-map-traveller-sprite', x: -27, y: -36, width: 54, height: 36,
    viewBox: `0 ${(TRAVELLER_ROWS[traveller.kind] ?? 2) * 64} 96 64` });
  sprite.append(svgNode('image', { href: TRAVELLER_SHEET, width: 192, height: 256 }));
  if (traveller.kind === 'army') {
    const id = `traveller-flag-${++flagId}`;
    const mask = svgNode('mask', { id, maskUnits: 'userSpaceOnUse', x: 0, y: 0, width: 192, height: 256 });
    mask.append(svgNode('image', { href: 'art/map/travellers_army_flag.png', width: 192, height: 256 }));
    const defs = svgNode('defs'); defs.append(mask);
    sprite.append(defs, svgNode('rect', { width: 192, height: 256, fill: traveller.color || '#e8dec4', mask: `url(#${id})` }));
  }
  g.append(title, shadow, hit, sprite);
  g.addEventListener('click', () => { g.classList.toggle('is-open'); });
  const tooltip = svgNode('text', { y: -42, 'text-anchor': 'middle' }); tooltip.textContent = label;
  g.append(tooltip);
  return g;
}
