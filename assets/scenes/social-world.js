const kinds = {
  sworn: ['Kết nghĩa', '#e5b85b'], spouse: ['Vợ chồng', '#ee91b4'],
  parent: ['Cha mẹ → con', '#86caaa'], sibling: ['Huynh đệ', '#bd9ce4'],
  lord: ['Chủ → tôi', '#82b7e8'], mentor: ['Thầy → trò', '#b3cc72'],
  friend: ['Bạn', '#57c9bd'], rival: ['Thù', '#eb776b']
};
const make = (tag, cls, text) => {
  const n = document.createElement(tag); n.className = cls;
  if (text) n.textContent = text; return n;
};
export function renderBondWorld(body, world, bridge) {
  body.replaceChildren();
  const pane = make('div', 'tk-world');
  const toolbar = make('form', 'tk-world-tools');
  const input = make('input', 'tk-world-search');
  input.type = 'search'; input.maxLength = 80; input.placeholder = 'Tìm tên nhân sĩ…'; input.setAttribute('aria-label', 'Tìm tên nhân sĩ');
  const submit = make('button', 'tk-soc-tab', 'Tìm'); submit.type = 'submit';
  toolbar.append(input, submit, make('span', '', 'Hai bậc nhân duyên · Bấm chân dung để chuyển tâm'));
  toolbar.addEventListener('submit', e => { e.preventDefault(); bridge.sendChoice('social', `search:${input.value.trim()}`); });
  pane.append(toolbar);
  const results = make('div', 'tk-world-results');
  for (const n of world.searchResults || []) {
    const b = make('button', 'tk-soc-tab', n.nameVi); b.type = 'button';
    b.onclick = () => bridge.sendChoice('social', `world:${n.npcId}`); results.append(b);
  }
  pane.append(results);
  const map = make('div', 'tk-world-map');
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 1000 600'); svg.setAttribute('preserveAspectRatio', 'none'); svg.setAttribute('aria-hidden', 'true');
  const positions = new Map();
  const firstCount = world.nodes.filter(n => n.depth === 1).length;
  for (const depth of [0, 1, 2]) {
    const nodes = world.nodes.filter(n => n.depth === depth);
    nodes.forEach((n, i) => {
      const angle = -Math.PI / 2 + 2 * Math.PI * (i + (depth === 1 ? 0.5 : 0)) / nodes.length;
      const rx = depth === 1 ? (firstCount > 16 ? 395 : firstCount > 10 ? 310 : 235) : 430;
      const ry = depth === 1 ? (firstCount > 16 ? 225 : firstCount > 10 ? 185 : 148) : 245;
      positions.set(n.npcId, depth === 0 ? [500,300] : [500 + rx * Math.cos(angle),300 + ry * Math.sin(angle)]);
    });
  }
  const defs = document.createElementNS(svg.namespaceURI, 'defs');
  for (const [key, [,color]] of Object.entries(kinds)) {
    const marker = document.createElementNS(svg.namespaceURI, 'marker');
    for (const [k,v] of Object.entries({id:`bond-arrow-${key}`,viewBox:'0 0 10 10',refX:9,refY:5,markerWidth:5,markerHeight:5,orient:'auto-start-reverse'})) marker.setAttribute(k,String(v));
    const arrow = document.createElementNS(svg.namespaceURI, 'path'); arrow.setAttribute('d','M 0 0 L 10 5 L 0 10 z'); arrow.setAttribute('fill',color);
    marker.append(arrow); defs.append(marker);
  }
  svg.append(defs);
  for (const e of world.edges) {
    const a = positions.get(e.npcA), b = positions.get(e.npcB); if (!a || !b) continue;
    const siblings = world.edges.filter(x => (x.npcA === e.npcA && x.npcB === e.npcB) || (x.npcA === e.npcB && x.npcB === e.npcA));
    const offset = (siblings.indexOf(e) - (siblings.length-1)/2) * 35;
    const dx = b[0]-a[0], dy=b[1]-a[1], length=Math.hypot(dx,dy) || 1;
    const end = [b[0]-dx/length*28,b[1]-dy/length*28];
    const line = document.createElementNS(svg.namespaceURI, 'path');
    line.setAttribute('d',`M ${a[0]} ${a[1]} Q ${(a[0]+b[0])/2-dy/length*offset} ${(a[1]+b[1])/2+dx/length*offset} ${end[0]} ${end[1]}`);
    line.setAttribute('fill','none'); line.setAttribute('stroke',kinds[e.kind]?.[1] || '#aaa'); line.setAttribute('stroke-width','2');
    if (['parent','lord','mentor'].includes(e.kind)) line.setAttribute('marker-end',`url(#bond-arrow-${e.kind})`);
    if (e.kind === 'rival') line.setAttribute('stroke-dasharray','7 5');
    const title = document.createElementNS(svg.namespaceURI, 'title'); title.textContent = `${kinds[e.kind]?.[0]} · ${e.strength}/100`; line.append(title); svg.append(line);
  }
  map.append(svg);
  for (const n of world.nodes) {
    const p = positions.get(n.npcId), btn = make('button', `tk-world-node depth-${n.depth}`);
    btn.type = 'button'; btn.style.left = `${p[0]/10}%`; btn.style.top = `${p[1]/6}%`;
    btn.title = n.nameVi; btn.setAttribute('aria-label', `Chọn ${n.nameVi} làm tâm`);
    const img = make('img', ''); img.src = n.facePath; img.alt = '';
    btn.append(img, make('span','',n.nameVi));
    btn.onclick = () => bridge.sendChoice('social', `world:${n.npcId}`); map.append(btn);
  }
  pane.append(map);
  const legend = make('div','tk-world-legend');
  for (const [key,[name,color]] of Object.entries(kinds)) {
    const item = make('span','',name), swatch = make('i',''); swatch.style.borderTop = `2px ${key === 'rival' ? 'dashed' : 'solid'} ${color}`;
    item.prepend(swatch); legend.append(item);
  }
  pane.append(legend); body.append(pane);
}
