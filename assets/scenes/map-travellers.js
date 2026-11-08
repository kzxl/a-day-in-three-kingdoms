const NS = 'http://www.w3.org/2000/svg';
export const TRAVELLER_LABELS = { rival: 'Thương nhân', caravan: 'Thương đoàn', npc: 'Danh sĩ', army: 'Quân binh' };
export const TRAVELLER_SHAPES = {
  rival: 'M-9 2h14v-7H-7z M-5-5v-4h7v4 M5 2l5-5 M-5 3a2 2 0 1 0 0 .1 M4 3a2 2 0 1 0 0 .1',
  caravan: 'M-10 1H8v-8H-10z M-7-7v-3H4v3 M8 1l4-4 M-6 3a2 2 0 1 0 0 .1 M5 3a2 2 0 1 0 0 .1',
  npc: 'M-8 0l3-5h6l3-5 4 1v6H4L2 1v6 M-5 0l-2 7 M-1-5v-5 M-8-1l-3-3',
  army: 'M-5 9V-11 M-4-10H9L5-5 9 0H-4 M-9 9h9'
};

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
      const t = lengths[i] ? distance / lengths[i] : 0;
      return { x: path[i].x + (path[i + 1].x - path[i].x) * t,
        y: path[i].y + (path[i + 1].y - path[i].y) * t };
    }
    distance -= lengths[i];
  }
}

export function createTravellerMarker(traveller, routeLabel) {
  const g = document.createElementNS(NS, 'g');
  g.setAttribute('class', 'tk-map-traveller');
  g.setAttribute('tabindex', '0');
  g.setAttribute('role', 'img');
  g.setAttribute('data-kind', traveller.kind);
  const label = `${traveller.labelVi} · ${routeLabel}`;
  g.setAttribute('aria-label', label);
  const title = document.createElementNS(NS, 'title'); title.textContent = label;
  const hit = document.createElementNS(NS, 'circle');
  hit.setAttribute('r', '13'); hit.setAttribute('fill', 'transparent'); hit.setAttribute('stroke', 'none');
  const shape = document.createElementNS(NS, 'path');
  shape.setAttribute('d', TRAVELLER_SHAPES[traveller.kind] || TRAVELLER_SHAPES.npc);
  shape.setAttribute('fill', '#eadfc6'); shape.setAttribute('stroke', traveller.color || '#6a4f2e');
  shape.setAttribute('stroke-width', '2'); shape.setAttribute('stroke-linejoin', 'round');
  g.append(title, hit, shape);
  g.addEventListener('click', () => { g.classList.toggle('is-open'); });
  const tooltip = document.createElementNS(NS, 'text');
  tooltip.setAttribute('y', '-18'); tooltip.setAttribute('text-anchor', 'middle'); tooltip.textContent = label;
  g.append(tooltip);
  return g;
}
