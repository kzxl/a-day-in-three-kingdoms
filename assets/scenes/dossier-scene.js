import { SimTBridge } from './bridge.js';

const AXIS_SHORT = ['Võ', 'Soái', 'Trí', 'Chính', 'Giao', 'Thương', 'Hậu cần', 'Nghệ'];
const AXIS_FULL = ['Võ Lực', 'Thống Soái', 'Trí Tuệ', 'Chính Trị', 'Ngoại Giao', 'Thương Mại', 'Hậu Cần', 'Kỹ Nghệ'];
const INITIAL_EAGER_PORTRAITS = 48;

const state = {
  scene: null,
  selectedNpcId: 0,
  query: '',
  mode: 'profile', // 'profile' | 'roster'
  vtlTab: 'characters', // 'characters' | 'history'
  filterFaction: 'all',
  filterCity: 'all',
  filterRole: 'all',
  lazyObserver: null
};

export function shortDossierName(nameVi) {
  return String(nameVi || '').replace(/\s*\([^)]*\)\s*/g, '').trim() || String(nameVi || '');
}

export function cleanCitadelName(cityNameVi) {
  return String(cityNameVi || '').replace(/\s*\(Đã mất\)\s*/gi, '').trim() || 'Trung Nguyên';
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

function classifyNpcRole(n) {
  if (n.roleCategoryVi) return n.roleCategoryVi;
  const role = String(n.roleVi || '');
  if (/thương|phú|chưởng/i.test(role) || ((n.com || 0) >= 72 && (n.com || 0) >= (n.war || 0))) return 'Thương nhân';
  if ((n.war || 0) >= 75 && (n.war || 0) >= (n.intStat || 0)) return 'Võ tướng';
  if ((n.intStat || 0) >= 75) return 'Mưu sĩ';
  if ((n.pol || 0) >= 70 || /thái thú|châu mục|thượng thư|thừa tướng/i.test(role)) return 'Quan văn';
  return 'Danh sĩ';
}

function filteredRosterNpcs(npcs) {
  return npcs.filter((n) => {
    if (state.filterFaction !== 'all' && (n.factionVi || 'Quần Hùng') !== state.filterFaction) {
      return false;
    }
    if (state.filterCity !== 'all' && cleanCitadelName(n.cityNameVi) !== state.filterCity) {
      return false;
    }
    if (state.filterRole !== 'all' && classifyNpcRole(n) !== state.filterRole) {
      return false;
    }
    return true;
  });
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

  const initMode = String(scene.initialMode || '').toLowerCase();
  if (initMode === 'roster' || initMode === 'characters') {
    state.mode = 'roster';
    state.vtlTab = 'characters';
  } else if (initMode === 'history' && scene.societyEnabled !== false) {
    state.mode = 'roster';
    state.vtlTab = 'history';
  } else if (initMode === 'profile') {
    state.mode = 'profile';
  }

  if (scene.societyEnabled === false && state.vtlTab === 'history') {
    state.vtlTab = 'characters';
  }

  paint(container, bridge);
}

function paint(root, bridge) {
  if (!root || !state.scene) return;
  const s = state.scene;
  const allNpcs = Array.isArray(s.npcs) ? s.npcs : [];
  const metCount = typeof s.metCount === 'number'
    ? s.metCount
    : allNpcs.filter((n) => Boolean(n.hasMet)).length;
  const totalCount = typeof s.totalCount === 'number' ? s.totalCount : allNpcs.length;

  if (!root.dataset.initialized) {
    root.dataset.initialized = '1';
    root.innerHTML =
      '<div class="dossier-head">' +
        '<div class="dossier-head-left">' +
          '<div>' +
            '<h1>Danh Sĩ</h1>' +
            '<div class="dossier-head-sub">Mỗi nhân vật có ảnh riêng. Bấm tên để xem hồ sơ.</div>' +
          '</div>' +
          '<div class="dossier-mode-tabs" role="tablist" aria-label="Chế độ xem Danh Sĩ">' +
            '<button type="button" class="dossier-btn dossier-tab-btn" id="btn-mode-profile" data-mode="profile">Hồ sơ danh sĩ</button>' +
            '<button type="button" class="dossier-btn dossier-tab-btn" id="btn-mode-roster" data-mode="roster">Võ tướng lục</button>' +
          '</div>' +
        '</div>' +
        '<div class="dossier-head-meta" id="dossier-head-meta"></div>' +
      '</div>' +
      '<div class="dossier-rec-bar" id="dossier-rec-bar"></div>' +
      '<div class="dossier-body" id="dossier-profile-view">' +
        '<div class="dossier-list-col">' +
          '<input type="text" class="dossier-search" id="dossier-search-input" placeholder="Tìm danh sĩ, tự, chức danh hoặc thành trì..." />' +
          '<div class="dossier-list-scroll" id="dossier-list-scroll"></div>' +
        '</div>' +
        '<div class="dossier-card" id="dossier-card"></div>' +
      '</div>' +
      '<div class="dossier-vtl-view" id="dossier-vtl-view" hidden>' +
        '<div class="dossier-vtl-top">' +
          '<div class="dossier-mode-tabs" id="dossier-vtl-subtabs" role="tablist" aria-label="Võ tướng lục"></div>' +
          '<div class="dossier-vtl-counter" id="dossier-vtl-counter"></div>' +
        '</div>' +
        '<div class="dossier-vtl-characters" id="dossier-vtl-characters">' +
          '<div class="dossier-vtl-filters" id="dossier-vtl-filters"></div>' +
          '<div class="dossier-vtl-scroll" id="dossier-vtl-scroll">' +
            '<div class="dossier-vtl-grid" id="dossier-vtl-grid"></div>' +
          '</div>' +
        '</div>' +
        '<div class="dossier-vtl-history" id="dossier-vtl-history" hidden>' +
          '<div class="dossier-vtl-history-scroll" id="dossier-vtl-history-scroll"></div>' +
        '</div>' +
      '</div>';

    const searchInput = root.querySelector('#dossier-search-input');
    if (searchInput) {
      searchInput.addEventListener('input', (e) => {
        state.query = e.target.value || '';
        paintListAndCard(root, bridge);
      });
    }

    const modeProfileBtn = root.querySelector('#btn-mode-profile');
    if (modeProfileBtn) {
      modeProfileBtn.addEventListener('click', () => {
        state.mode = 'profile';
        paint(root, bridge);
      });
    }

    const modeRosterBtn = root.querySelector('#btn-mode-roster');
    if (modeRosterBtn) {
      modeRosterBtn.addEventListener('click', () => {
        state.mode = 'roster';
        paint(root, bridge);
      });
    }
  }

  const metaEl = root.querySelector('#dossier-head-meta');
  if (metaEl) {
    metaEl.innerHTML =
      '<span>Đang tại <b>' + esc(s.currentCityName) + '</b></span>' +
      '<span>Đã gặp <b>' + esc(metCount) + ' / ' + esc(totalCount) + '</b></span>' +
      '<span>Tuệ Nhãn <b>' + esc(s.perceptionText) + '</b></span>';
  }

  const recEl = root.querySelector('#dossier-rec-bar');
  if (recEl) {
    if (state.mode === 'roster') {
      recEl.innerHTML = '<b>VÕ TƯỚNG LỤC</b>Đã gặp ' + esc(metCount) + ' / ' + esc(totalCount) + ' nhân vật. Các mốc sưu tầm (10, 50, 100 danh sĩ và đủ mọi sử biến) thưởng Danh vọng và ghi vào biên niên Thế Thái.';
    } else {
      recEl.innerHTML = '<b>NÊN LÀM</b>' + esc(s.recommendation || 'Chọn một danh sĩ ở cột trái để xem bát diện năng lực, đặc kỹ và tiểu sử.');
    }
  }

  const modeProfileBtn = root.querySelector('#btn-mode-profile');
  const modeRosterBtn = root.querySelector('#btn-mode-roster');
  if (modeProfileBtn) modeProfileBtn.classList.toggle('pri', state.mode === 'profile');
  if (modeRosterBtn) modeRosterBtn.classList.toggle('pri', state.mode === 'roster');

  const profileView = root.querySelector('#dossier-profile-view');
  const vtlView = root.querySelector('#dossier-vtl-view');
  if (state.mode === 'roster') {
    if (profileView) profileView.hidden = true;
    if (vtlView) vtlView.hidden = false;
    paintVoTuongLuc(root, bridge, metCount, totalCount);
  } else {
    if (profileView) profileView.hidden = false;
    if (vtlView) vtlView.hidden = true;
    paintListAndCard(root, bridge);
  }
}

function paintVoTuongLuc(root, bridge, metCount, totalCount) {
  const s = state.scene;
  if (!s) return;
  const societyEnabled = s.societyEnabled !== false;
  if (!societyEnabled && state.vtlTab === 'history') {
    state.vtlTab = 'characters';
  }

  const subtabsEl = root.querySelector('#dossier-vtl-subtabs');
  if (subtabsEl) {
    subtabsEl.innerHTML =
      '<button type="button" class="dossier-btn dossier-tab-btn' + (state.vtlTab === 'characters' ? ' pri' : '') + '" id="btn-vtl-tab-characters" data-vtl-tab="characters">Nhân vật</button>' +
      (societyEnabled
        ? '<button type="button" class="dossier-btn dossier-tab-btn' + (state.vtlTab === 'history' ? ' pri' : '') + '" id="btn-vtl-tab-history" data-vtl-tab="history">Sử biến</button>'
        : '');

    subtabsEl.querySelectorAll('[data-vtl-tab]').forEach((btn) => {
      btn.addEventListener('click', () => {
        state.vtlTab = btn.getAttribute('data-vtl-tab') || 'characters';
        paintVoTuongLuc(root, bridge, metCount, totalCount);
      });
    });
  }

  const counterEl = root.querySelector('#dossier-vtl-counter');
  if (counterEl) {
    if (state.vtlTab === 'history' && societyEnabled) {
      const evts = Array.isArray(s.historicalEvents) ? s.historicalEvents : [];
      const viewedCount = evts.filter((e) => e.status === 'viewed').length;
      counterEl.innerHTML = 'Đã gặp <b>' + esc(metCount) + ' / ' + esc(totalCount) + '</b> · Chứng kiến <b>' + esc(viewedCount) + ' / ' + esc(evts.length) + '</b>';
    } else {
      counterEl.innerHTML = 'Đã gặp <b>' + esc(metCount) + ' / ' + esc(totalCount) + '</b>';
    }
  }

  const charPane = root.querySelector('#dossier-vtl-characters');
  const histPane = root.querySelector('#dossier-vtl-history');
  if (state.vtlTab === 'history' && societyEnabled) {
    if (charPane) charPane.hidden = true;
    if (histPane) histPane.hidden = false;
    paintHistoryTab(root, bridge);
  } else {
    if (charPane) charPane.hidden = false;
    if (histPane) histPane.hidden = true;
    paintCharactersTab(root, bridge);
  }
}

function buildFilterChipsHtml(groupKey, labelVi, options, currentVal) {
  const chips = ['all', ...options].map((opt) => {
    const isAll = opt === 'all';
    const text = isAll ? 'Tất cả' : opt;
    const activeCls = currentVal === opt ? ' active' : '';
    return (
      '<button type="button" class="dossier-chip dossier-filter-btn' + activeCls + '" ' +
        'data-filter-group="' + esc(groupKey) + '" ' +
        'data-filter-val="' + esc(opt) + '">' +
        esc(text) +
      '</button>'
    );
  }).join('');

  return (
    '<div class="dossier-filter-row">' +
      '<span class="dossier-filter-lbl">' + esc(labelVi) + '</span>' +
      '<div class="dossier-filter-strip">' + chips + '</div>' +
    '</div>'
  );
}

function paintCharactersTab(root, bridge) {
  const s = state.scene;
  if (!s) return;
  const allNpcs = Array.isArray(s.npcs) ? s.npcs : [];

  const factions = Array.from(new Set(allNpcs.map((n) => n.factionVi || 'Quần Hùng'))).sort((a, b) => a.localeCompare(b, 'vi'));
  const cities = Array.from(new Set(allNpcs.map((n) => cleanCitadelName(n.cityNameVi)))).sort((a, b) => a.localeCompare(b, 'vi'));
  const roles = ['Võ tướng', 'Mưu sĩ', 'Quan văn', 'Thương nhân', 'Danh sĩ'].filter((r) =>
    allNpcs.some((n) => classifyNpcRole(n) === r)
  );

  if (state.filterFaction !== 'all' && !factions.includes(state.filterFaction)) state.filterFaction = 'all';
  if (state.filterCity !== 'all' && !cities.includes(state.filterCity)) state.filterCity = 'all';
  if (state.filterRole !== 'all' && !roles.includes(state.filterRole)) state.filterRole = 'all';

  const filtersEl = root.querySelector('#dossier-vtl-filters');
  if (filtersEl) {
    filtersEl.innerHTML =
      buildFilterChipsHtml('faction', 'Thế lực', factions, state.filterFaction) +
      buildFilterChipsHtml('city', 'Thành', cities, state.filterCity) +
      buildFilterChipsHtml('role', 'Loại người', roles, state.filterRole);

    filtersEl.querySelectorAll('.dossier-filter-btn').forEach((btn) => {
      btn.addEventListener('click', () => {
        const group = btn.getAttribute('data-filter-group');
        const val = btn.getAttribute('data-filter-val') || 'all';
        if (group === 'faction') state.filterFaction = val;
        else if (group === 'city') state.filterCity = val;
        else if (group === 'role') state.filterRole = val;
        paintCharactersTab(root, bridge);
      });
    });
  }

  const visible = filteredRosterNpcs(allNpcs);
  const gridEl = root.querySelector('#dossier-vtl-grid');
  const scrollEl = root.querySelector('#dossier-vtl-scroll');
  if (!gridEl) return;

  if (state.lazyObserver) {
    state.lazyObserver.disconnect();
    state.lazyObserver = null;
  }

  gridEl.innerHTML = visible.map((n, idx) => {
    const hasMet = Boolean(n.hasMet);
    const face = n.facePath || n.profilePath || 'art/portraits/scholar/face.webp';
    const displayName = hasMet ? shortDossierName(n.nameVi) : '???';
    const subText = hasMet ? cleanCitadelName(n.cityNameVi) : 'Chưa gặp';
    const eager = idx < INITIAL_EAGER_PORTRAITS;
    const imgAttr = eager
      ? 'src="' + esc(face) + '"'
      : 'data-src="' + esc(face) + '"';

    if (hasMet) {
      return (
        '<button type="button" class="dossier-vtl-cell is-met" data-npc-id="' + n.id + '">' +
          '<span class="dossier-vtl-avatar">' +
            '<img class="dossier-vtl-img" loading="lazy" decoding="async" ' + imgAttr + ' alt="' + esc(displayName) + '" />' +
          '</span>' +
          '<span class="dossier-vtl-name">' + esc(displayName) + '</span>' +
          '<span class="dossier-vtl-sub">' + esc(subText) + '</span>' +
        '</button>'
      );
    }

    return (
      '<div class="dossier-vtl-cell is-unmet" data-npc-id="' + n.id + '">' +
        '<span class="dossier-vtl-avatar">' +
          '<img class="dossier-vtl-img is-silhouette" loading="lazy" decoding="async" ' + imgAttr + ' alt="Chưa gặp" />' +
          '<span class="dossier-vtl-qmark">?</span>' +
        '</span>' +
        '<span class="dossier-vtl-name">' + esc(displayName) + '</span>' +
        '<span class="dossier-vtl-sub">' + esc(subText) + '</span>' +
      '</div>'
    );
  }).join('');

  gridEl.querySelectorAll('.dossier-vtl-cell.is-met').forEach((btn) => {
    btn.addEventListener('click', () => {
      const id = parseInt(btn.getAttribute('data-npc-id') || '0', 10);
      if (id) {
        state.selectedNpcId = id;
        state.mode = 'profile';
        paint(root, bridge);
      }
    });
  });

  if (typeof IntersectionObserver !== 'undefined' && scrollEl) {
    state.lazyObserver = new IntersectionObserver((entries, obs) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        const img = entry.target;
        const deferredSrc = img.getAttribute('data-src');
        if (deferredSrc) {
          img.setAttribute('src', deferredSrc);
          img.removeAttribute('data-src');
        }
        obs.unobserve(img);
      });
    }, {
      root: scrollEl,
      rootMargin: '180px 0px'
    });

    gridEl.querySelectorAll('img[data-src]').forEach((img) => {
      state.lazyObserver.observe(img);
    });
  } else {
    gridEl.querySelectorAll('img[data-src]').forEach((img) => {
      img.setAttribute('src', img.getAttribute('data-src') || '');
      img.removeAttribute('data-src');
    });
  }
}

function paintHistoryTab(root, bridge) {
  const s = state.scene;
  const listEl = root.querySelector('#dossier-vtl-history-scroll');
  if (!s || !listEl) return;

  const events = Array.isArray(s.historicalEvents) ? s.historicalEvents : [];
  if (events.length === 0) {
    listEl.innerHTML = '<div class="dossier-history-empty">Chưa có biến cố lịch sử nào được ghi nhận.</div>';
    return;
  }

  listEl.innerHTML = events.map((evt) => {
    const status = String(evt.status || 'pending').toLowerCase();
    const yearLabel = 'Năm ' + esc(evt.year);
    if (status === 'viewed') {
      return (
        '<button type="button" class="dossier-row dossier-history-row is-viewed" data-event-code="' + esc(evt.code) + '">' +
          '<span class="dossier-history-year">' + yearLabel + '<small>' + esc(evt.dateText) + '</small></span>' +
          '<span class="dossier-row-meta">' +
            '<span class="dossier-row-name">' + esc(evt.titleVi) + '</span>' +
            '<span class="dossier-row-sub">' + esc(evt.citadelNameVi) + ' · ' + esc(evt.summaryVi) + '</span>' +
          '</span>' +
          '<span class="dossier-row-tag is-viewed">Xem lại cảnh</span>' +
        '</button>'
      );
    }

    if (status === 'occurred') {
      return (
        '<div class="dossier-row dossier-history-row is-missed" data-event-code="' + esc(evt.code) + '">' +
          '<span class="dossier-history-year">' + yearLabel + '<small>' + esc(evt.dateText) + '</small></span>' +
          '<span class="dossier-row-meta">' +
            '<span class="dossier-row-name">' + esc(evt.titleVi) + '</span>' +
            '<span class="dossier-row-sub">' + esc(evt.citadelNameVi) + '</span>' +
          '</span>' +
          '<span class="dossier-row-tag is-missed">Không có mặt</span>' +
        '</div>'
      );
    }

    return (
      '<div class="dossier-row dossier-history-row is-pending" data-event-code="' + esc(evt.code) + '">' +
        '<span class="dossier-history-year">' + yearLabel + '</span>' +
        '<span class="dossier-row-meta">' +
          '<span class="dossier-row-name">???</span>' +
          '<span class="dossier-row-sub">Chưa xảy ra</span>' +
        '</span>' +
        '<span class="dossier-row-tag is-pending">???</span>' +
      '</div>'
    );
  }).join('');

  listEl.querySelectorAll('.dossier-history-row.is-viewed').forEach((btn) => {
    btn.addEventListener('click', () => {
      const code = btn.getAttribute('data-event-code');
      if (code && bridge) {
        bridge.sendChoice('dossier', 'replay:' + code);
      }
    });
  });
}

function paintListAndCard(root, bridge) {
  const s = state.scene;
  if (!s) return;
  const allNpcs = Array.isArray(s.npcs) ? s.npcs : [];
  const visibleNpcs = filteredNpcs(allNpcs);
  const current = allNpcs.find((n) => n.id === state.selectedNpcId) || visibleNpcs[0] || allNpcs[0];

  const listEl = root.querySelector('#dossier-list-scroll');
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
            '<img loading="lazy" src="' + esc(portrait) + '" alt="' + esc(displayName) + '" />' +
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
          paintListAndCard(root, bridge);
        }
      });
    });
  }

  const cardEl = root.querySelector('#dossier-card');
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
  const isLocal = Boolean(current.isLocal);
  const remoteLabel = 'Đang ở ' + (current.cityNameVi || 'thành khác');
  const disabledAttr = isLocal ? '' : ' disabled';
  const visitLabel = isLocal ? 'Bái phỏng' : remoteLabel;
  const sparLabel = isLocal ? 'So tài' : remoteLabel;
  const talkLabel = isLocal ? 'Mở cảnh nói chuyện' : remoteLabel;
  const traits = Array.isArray(current.traits) ? current.traits : [];
  const traitsHtml = traits.length > 0
    ? '<div class="dossier-traits" aria-label="Đặc tính nhân vật">' +
        traits.map((t) => {
          const hiddenCls = t.hidden ? ' is-hidden' : '';
          const desc = t.descriptionVi || '';
          return (
            '<span class="dossier-chip is-trait' + hiddenCls + '" ' +
              'title="' + esc(desc) + '" ' +
              'data-trait-code="' + esc(t.code || '') + '">' +
              esc(t.nameVi || '???') +
            '</span>'
          );
        }).join('') +
      '</div>'
    : '';

  cardEl.innerHTML =
    '<div class="dossier-left-pane">' +
      '<div class="dossier-portrait-frame">' +
        '<img src="' + esc(portraitSrc) + '" alt="' + esc(current.nameVi) + '" />' +
      '</div>' +
      '<div class="dossier-aff-row"><span style="color:#b9aa8c">Hảo cảm</span><span>' + (current.affinity || 0) + ' / 100</span></div>' +
      '<div class="dossier-bar" style="flex:none"><i style="width:' + affPct + '%"></i></div>' +
      '<div class="dossier-btn-row">' +
        '<button type="button" class="dossier-btn pri" id="btn-dossier-visit"' + disabledAttr + '>' + esc(visitLabel) + '</button>' +
        '<button type="button" class="dossier-btn" id="btn-dossier-spar"' + disabledAttr + '>' + esc(sparLabel) + '</button>' +
      '</div>' +
      '<button type="button" class="dossier-btn full" id="btn-dossier-talk"' + disabledAttr + '>' + esc(talkLabel) + '</button>' +
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
      traitsHtml +
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

  const visitBtn = root.querySelector('#btn-dossier-visit');
  if (visitBtn && isLocal) {
    visitBtn.addEventListener('click', () => bridge.sendChoice('dossier', `visit:${current.id}`));
  }
  const sparBtn = root.querySelector('#btn-dossier-spar');
  if (sparBtn && isLocal) {
    sparBtn.addEventListener('click', () => bridge.sendChoice('dossier', `spar:${current.id}`));
  }
  const talkBtn = root.querySelector('#btn-dossier-talk');
  if (talkBtn && isLocal) {
    talkBtn.addEventListener('click', () => bridge.sendNavigate('dossier', `npc:${current.id}`));
  }
}

SimTBridge.registerStage('dossier', {
  render: renderDossierScene
});
