import { SimTBridge } from './bridge.js';

const AXIS_SHORT = ['Võ', 'Soái', 'Trí', 'Chính', 'Giao', 'Thương', 'Hậu cần', 'Nghệ'];
const AXIS_FULL = ['Võ Lực', 'Thống Soái', 'Trí Tuệ', 'Chính Trị', 'Ngoại Giao', 'Thương Mại', 'Hậu Cần', 'Kỹ Nghệ'];

const state = {
  scene: null,
  selectedNpcId: 0,
  query: ''
};

export function shortDossierName(nameVi) {
  return String(nameVi || '').replace(/\s*\([^)]*\)\s*/g, '').trim() || String(nameVi || '');
}

function esc(str) {
  return String(str == null ? '' : str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function radarPoint(index, radius) {
  const angle = -Math.PI / 2 + index * (Math.PI / 4);
  return [Math.round(radius * Math.cos(angle)), Math.round(radius * Math.sin(angle))];
}

function radarRingPoints(factor) {
  return AXIS_SHORT.map((_, i) => radarPoint(i, 92 * factor).join(',')).join(' ');
}

function filteredNpcs(npcs) {
  const q = (state.query || '').trim().toLowerCase();
  if (!q) return npcs;
  return npcs.filter((n) =>
    (n.nameVi && n.nameVi.toLowerCase().includes(q)) ||
    (n.courtesyVi && n.courtesyVi.toLowerCase().includes(q)) ||
    (n.roleVi && n.roleVi.toLowerCase().includes(q)) ||
    (n.cityNameVi && n.cityNameVi.toLowerCase().includes(q))
  );
}

function renderDossierScene(container, scene, bridge) {
  if (!container || !scene) return;
  state.scene = scene;
  const npcs = Array.isArray(scene.npcs) ? scene.npcs : [];
  if (scene.selectedNpcId && npcs.some((n) => n.id === scene.selectedNpcId)) {
    state.selectedNpcId = scene.selectedNpcId;
  } else if (!state.selectedNpcId || !npcs.some((n) => n.id === state.selectedNpcId)) {
    state.selectedNpcId = npcs.length > 0 ? npcs[0].id : 0;
  }
  paint(container, bridge);
}

function paint(root, bridge) {
  if (!root || !state.scene) return;
  const s = state.scene;

  if (!root.dataset.initialized) {
    root.dataset.initialized = '1';
    root.innerHTML =
      '<div class="dossier-head">' +
        '<div>' +
          '<h1>Danh Sĩ</h1>' +
          '<div class="dossier-head-sub">Mỗi nhân vật có ảnh riêng. Bấm tên để xem hồ sơ.</div>' +
        '</div>' +
        '<div class="dossier-head-meta" id="dossier-head-meta"></div>' +
      '</div>' +
      '<div class="dossier-rec-bar" id="dossier-rec-bar"></div>' +
      '<div class="dossier-body">' +
        '<div class="dossier-list-col">' +
          '<input type="text" class="dossier-search" id="dossier-search-input" placeholder="Tìm danh sĩ, tự, chức danh hoặc thành trì..." />' +
          '<div class="dossier-list-scroll" id="dossier-list-scroll"></div>' +
        '</div>' +
        '<div class="dossier-card" id="dossier-card"></div>' +
      '</div>';

    const searchInput = document.getElementById('dossier-search-input');
    if (searchInput) {
      searchInput.addEventListener('input', (e) => {
        state.query = e.target.value || '';
        paintListAndCard(bridge);
      });
    }
  }

  const metaEl = document.getElementById('dossier-head-meta');
  if (metaEl) {
    metaEl.innerHTML =
      '<span>Đang tại <b>' + esc(s.currentCityName) + '</b></span>' +
      '<span>Đã diện kiến <b>' + esc(s.metCount) + '/' + esc(s.totalCount) + '</b></span>' +
      '<span>Tuệ Nhãn <b>' + esc(s.perceptionText) + '</b></span>';
  }

  const recEl = document.getElementById('dossier-rec-bar');
  if (recEl) {
    recEl.innerHTML = '<b>NÊN LÀM</b>' + esc(s.recommendation || 'Chọn một danh sĩ ở cột trái để xem bát diện năng lực, đặc kỹ và tiểu sử.');
  }

  paintListAndCard(bridge);
}

function paintListAndCard(bridge) {
  const s = state.scene;
  if (!s) return;
  const allNpcs = Array.isArray(s.npcs) ? s.npcs : [];
  const visibleNpcs = filteredNpcs(allNpcs);
  const current = allNpcs.find((n) => n.id === state.selectedNpcId) || visibleNpcs[0] || allNpcs[0];

  const listEl = document.getElementById('dossier-list-scroll');
  if (listEl) {
    listEl.innerHTML = visibleNpcs.map((n) => {
      const activeCls = current && n.id === current.id ? ' active' : '';
      const portrait = n.profilePath || n.facePath || 'art/portraits/scholar/face.webp';
      const displayName = shortDossierName(n.nameVi);
      const localDot = n.isLocal ? '<span class="dossier-local-dot" title="Đồng thành"></span>' : '';
      const subText = !n.isLocal && n.cityNameVi ? n.cityNameVi + ' · ' + (n.roleVi || '') : (n.roleVi || '');
      return (
        '<button type="button" class="dossier-row' + activeCls + '" data-npc-id="' + n.id + '">' +
          '<span class="dossier-row-avatar">' +
            '<img src="' + esc(portrait) + '" alt="' + esc(displayName) + '" />' +
            localDot +
          '</span>' +
          '<span class="dossier-row-meta">' +
            '<span class="dossier-row-name">' + esc(displayName) + '</span>' +
            '<span class="dossier-row-sub">' + esc(subText) + '</span>' +
          '</span>' +
        '</button>'
      );
    }).join('');

    listEl.querySelectorAll('.dossier-row').forEach((btn) => {
      btn.addEventListener('click', () => {
        const id = parseInt(btn.getAttribute('data-npc-id') || '0', 10);
        if (id) {
          state.selectedNpcId = id;
          paintListAndCard(bridge);
        }
      });
    });
  }

  const cardEl = document.getElementById('dossier-card');
  if (!cardEl || !current) return;

  const vals = [
    current.war || 0,
    current.ldr || 0,
    current.intStat || 0,
    current.pol || 0,
    current.dip || 0,
    current.com || 0,
    current.logStat || 0,
    current.cra || 0
  ];

  const polyPoints = vals.map((v, i) => radarPoint(i, (92 * Math.max(8, Math.min(100, v))) / 100).join(',')).join(' ');
  const axisLabelsSvg = AXIS_SHORT.map((label, i) => {
    const pt = radarPoint(i, 108);
    return '<text x="' + pt[0] + '" y="' + pt[1] + '" text-anchor="middle" dominant-baseline="middle" fill="#b9aa8c" font-size="12" font-family="Be Vietnam Pro, sans-serif">' + esc(label) + '</text>';
  }).join('');

  const statBarsHtml = AXIS_FULL.map((label, i) => {
    const val = vals[i];
    const pct = Math.max(0, Math.min(100, val));
    return (
      '<div class="dossier-stat-line">' +
        '<span class="dossier-stat-lbl">' + esc(label) + '</span>' +
        '<div class="dossier-bar"><i style="width:' + pct + '%"></i></div>' +
        '<span class="dossier-stat-val">' + val + '</span>' +
      '</div>'
    );
  }).join('');

  const affPct = Math.max(0, Math.min(100, current.affinity || 0));
  const portraitSrc = current.profilePath || current.facePath || 'art/portraits/scholar/face.webp';

  cardEl.innerHTML =
    '<div class="dossier-left-pane">' +
      '<div class="dossier-portrait-frame">' +
        '<img src="' + esc(portraitSrc) + '" alt="' + esc(current.nameVi) + '" />' +
      '</div>' +
      '<div class="dossier-aff-row"><span style="color:#b9aa8c">Hảo cảm</span><span>' + (current.affinity || 0) + ' / 100</span></div>' +
      '<div class="dossier-bar" style="flex:none"><i style="width:' + affPct + '%"></i></div>' +
      '<div class="dossier-btn-row">' +
        '<button type="button" class="dossier-btn pri" id="btn-dossier-visit">Bái phỏng</button>' +
        '<button type="button" class="dossier-btn" id="btn-dossier-spar">So tài</button>' +
      '</div>' +
      '<button type="button" class="dossier-btn full" id="btn-dossier-talk">Mở cảnh nói chuyện</button>' +
      '<div class="dossier-skill-box">' +
        '<div class="dossier-skill-label">ĐẶC KỸ</div>' +
        '<div class="dossier-skill-name">' + esc(current.skillNameVi || 'Chưa khai mở') + '</div>' +
        '<div class="dossier-skill-desc">' + esc(current.skillDescVi || 'Gặp gỡ và đàm đạo để tìm hiểu tuyệt kỹ sở trường.') + '</div>' +
      '</div>' +
    '</div>' +
    '<div class="dossier-right-pane">' +
      '<div class="dossier-title-row">' +
        '<div class="dossier-name">' + esc(current.nameVi) + '</div>' +
        '<div class="dossier-han">' + esc(current.hanziName || '') + '</div>' +
      '</div>' +
      '<div class="dossier-chips">' +
        '<span class="dossier-chip">' + esc(current.roleVi) + '</span>' +
        '<span class="dossier-chip">Tự ' + esc(current.courtesyVi || '—') + '</span>' +
        '<span class="dossier-chip">Khí chất ' + esc(current.moodVi || 'Điềm đạm') + '</span>' +
        '<span class="dossier-chip">Ngụ tại ' + esc(current.cityNameVi) + '</span>' +
      '</div>' +
      '<div class="dossier-radar-row">' +
        '<svg class="dossier-radar-svg" viewBox="-140 -122 280 244" role="img" aria-label="Biểu đồ tám năng lực">' +
          '<polygon points="' + radarRingPoints(0.33) + '" fill="none" stroke="#3a2c20"></polygon>' +
          '<polygon points="' + radarRingPoints(0.66) + '" fill="none" stroke="#3a2c20"></polygon>' +
          '<polygon points="' + radarRingPoints(1.0) + '" fill="none" stroke="#4a3a2a"></polygon>' +
          '<polygon points="' + polyPoints + '" fill="rgba(196,160,98,.32)" stroke="#e8b75c" stroke-width="2"></polygon>' +
          axisLabelsSvg +
        '</svg>' +
        '<div class="dossier-stats-list">' + statBarsHtml + '</div>' +
      '</div>' +
      '<div class="dossier-bio-title">TIỂU SỬ</div>' +
      '<div class="dossier-bio-text">' + esc(current.bioVi) + '</div>' +
    '</div>';

  const visitBtn = document.getElementById('btn-dossier-visit');
  if (visitBtn) {
    visitBtn.addEventListener('click', () => bridge.sendChoice('dossier', `visit:${current.id}`));
  }
  const sparBtn = document.getElementById('btn-dossier-spar');
  if (sparBtn) {
    sparBtn.addEventListener('click', () => bridge.sendChoice('dossier', `spar:${current.id}`));
  }
  const talkBtn = document.getElementById('btn-dossier-talk');
  if (talkBtn) {
    talkBtn.addEventListener('click', () => bridge.sendNavigate('dossier', `npc:${current.id}`));
  }
}

SimTBridge.registerStage('dossier', {
  render: renderDossierScene
});
