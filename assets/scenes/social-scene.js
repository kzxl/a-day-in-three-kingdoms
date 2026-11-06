import { SimTBridge } from './bridge.js';

let activeTabKey = 'rel';
let rememberedNpcId = 0;

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined && text !== null) node.textContent = String(text);
  return node;
}

function shortName(nameVi) {
  return String(nameVi || '').replace(/\s*\([^)]*\)\s*/g, '').trim() || String(nameVi || '');
}

function ringRadiusPct(affinity) {
  const clamped = Math.max(0, Math.min(100, affinity || 0));
  return 41 - clamped * 0.24;
}

function nodeRadiusPct(affinity, idx, total) {
  const clamped = Math.max(0, Math.min(100, affinity || 0));
  if (clamped < 35 && total > 6) {
    return idx % 2 === 0 ? 40 - clamped * 0.20 : 35.5 - clamped * 0.18;
  }
  return ringRadiusPct(clamped);
}

const SLOT_RULES = ['S', 'A', 'A', 'BC', 'BC', 'BC'];
const SLOT_RULE_LABELS = { S: 'Ô phẩm S', A: 'Ô phẩm A', BC: 'Ô phẩm B, C' };

function fitsSlotRule(skillRank, slotIndex) {
  const rule = SLOT_RULES[slotIndex];
  if (rule === 'BC') return skillRank === 'B' || skillRank === 'C';
  return skillRank === rule;
}

function renderSocialScene(container, scene, bridge) {
  if (!container || !scene) return;

  const nodes = Array.isArray(scene.nodes) ? scene.nodes : [];
  const skills = Array.isArray(scene.skills) ? scene.skills : [];
  const letters = Array.isArray(scene.letters) ? scene.letters : [];
  const retainers = Array.isArray(scene.retainers) ? scene.retainers : [];

  if (scene.selectedNpcId && nodes.some((n) => n.npcId === scene.selectedNpcId)) {
    rememberedNpcId = scene.selectedNpcId;
  } else if (!nodes.some((n) => n.npcId === rememberedNpcId) && nodes.length > 0) {
    rememberedNpcId = nodes[0].npcId;
  }

  let shell = container.querySelector('.tk-soc-shell');
  let mapBox = container.querySelector('.tk-soc-map');
  let inspectorBox = container.querySelector('.tk-soc-inspector');
  let bodyBox = container.querySelector('.tk-soc-body');
  let metaEl = container.querySelector('.tk-soc-meta');
  let recTextEl = container.querySelector('.tk-soc-rec-text');
  let toastEl = container.querySelector('.tk-soc-toast');
  let tabsBox = container.querySelector('.tk-soc-tabs');

  if (!shell) {
    container.replaceChildren();
    shell = el('div', 'tk-soc-shell');
    const header = el('div', 'tk-soc-header');
    const titleWrap = el('div');
    titleWrap.append(
      el('h1', 'tk-soc-title', 'Nhân Duyên'),
      el('div', 'tk-soc-sub', 'Kết giao người trong thành để chiêu mộ, kết nghĩa và học tuyệt kỹ.')
    );
    metaEl = el('div', 'tk-soc-meta');
    header.append(titleWrap, metaEl);

    const recBar = el('div', 'tk-soc-rec');
    recBar.appendChild(el('span', 'tk-soc-rec-tag', 'NÊN LÀM'));
    recTextEl = el('span', 'tk-soc-rec-text');
    toastEl = el('span', 'tk-soc-toast');
    recBar.append(recTextEl, toastEl);

    tabsBox = el('div', 'tk-soc-tabs');
    bodyBox = el('div', 'tk-soc-body');
    shell.append(header, recBar, tabsBox, bodyBox);
    container.appendChild(shell);
  }

  function flash(msg) {
    if (toastEl) toastEl.textContent = msg;
  }

  metaEl.textContent = `Tại ${scene.currentCityName || 'Lạc Dương'} · Danh vọng ${(scene.fame || 0).toLocaleString('vi-VN')} · ${(scene.cash || 0).toLocaleString('vi-VN')} quan`;
  recTextEl.textContent = scene.recommendation || 'Đàm đạo và tặng quà cho danh sĩ trong thành để chiêu mộ hoặc học tuyệt kỹ.';

  function redrawTabs() {
    tabsBox.replaceChildren();
    const tabDefs = [
      ['rel', 'Quan hệ'], ['skill', 'Tuyệt kỹ'],
      ['mail', `Thư từ${letters.length ? ` (${letters.length})` : ''}`],
      ['team', `Gia thần${retainers.length ? ` (${retainers.length})` : ''}`]
    ];
    for (const [key, label] of tabDefs) {
      const btn = el('button', `tk-soc-tab${activeTabKey === key ? ' is-active' : ''}`, label);
      btn.type = 'button';
      btn.addEventListener('click', () => { activeTabKey = key; redrawBody(); redrawTabs(); });
      tabsBox.appendChild(btn);
    }
  }

  function redrawInspector(cur) {
    if (!inspectorBox) return;
    inspectorBox.replaceChildren();
    if (!cur) { inspectorBox.appendChild(el('div', 'tk-soc-role', 'Chưa có nhân sĩ trong danh sách.')); return; }

    const head = el('div', 'tk-soc-head');
    const avatar = el('img');
    avatar.src = cur.facePath || 'art/portraits/scholar/face.webp';
    avatar.alt = shortName(cur.nameVi);
    const headInfo = el('div');
    headInfo.append(
      el('div', 'tk-soc-name', shortName(cur.nameVi)),
      el('div', 'tk-soc-role', `${cur.roleVi} · ${cur.cityNameVi}`),
      el('div', 'tk-soc-rank', `Phẩm ${cur.rankCode || 'B'} · ${cur.tierTitleVi}`)
    );
    head.append(avatar, headInfo);
    inspectorBox.appendChild(head);

    for (const [lbl, val, color] of [['Hảo cảm', cur.affinity, '#c4a062'], ['Tín nhiệm', cur.trust, '#3f8f73']]) {
      const wrap = el('div');
      const row = el('div', 'tk-soc-bar-row');
      row.append(el('span', '', lbl), el('span', '', `${val} / 100`));
      const bar = el('div', 'tk-soc-bar');
      const fill = el('i');
      fill.style.cssText = `width:${Math.max(0, Math.min(100, val))}%;background:${color}`;
      bar.appendChild(fill);
      wrap.append(row, bar);
      inspectorBox.appendChild(wrap);
    }

    inspectorBox.appendChild(el('div', 'tk-soc-sec', 'LÀM GÌ VỚI NGƯỜI NÀY'));
    const chatBtn = el('button', 'tk-soc-act is-primary');
    chatBtn.type = 'button';
    chatBtn.append(el('span', '', 'Đàm đạo'), el('em', '', '+hảo cảm · 1 ngày'));
    chatBtn.addEventListener('click', () => bridge.sendChoice('social', `chat:${cur.npcId}`));

    const giftBtn = el('button', 'tk-soc-act');
    giftBtn.type = 'button';
    const firstGift = (Array.isArray(scene.giftItems) ? scene.giftItems : [])[0];
    giftBtn.append(el('span', '', 'Biếu quà'), el('em', '', firstGift ? `Tặng ${firstGift.nameVi} (còn ${firstGift.heldQuantity})` : '+hảo cảm · cần quà trong túi'));
    giftBtn.addEventListener('click', () => bridge.sendChoice('social', firstGift ? `gift:${cur.npcId}:${firstGift.itemId}` : `gift:${cur.npcId}`));

    inspectorBox.append(chatBtn, giftBtn, el('div', 'tk-soc-sec', 'MỐC MỞ KHOÁ'));

    const recBtn = el('button', `tk-soc-act${cur.canRecruit && !cur.isRetainer ? ' is-primary' : ' is-disabled'}`);
    recBtn.type = 'button';
    recBtn.append(el('span', '', 'Chiêu mộ'), el('em', '', cur.isRetainer ? 'Đã chiêu mộ' : `Hảo cảm ${cur.affinity}/${cur.requiredAffinity} · ${cur.signingFee} quan`));
    recBtn.addEventListener('click', () => cur.isRetainer ? flash('Nhân sĩ này đã ở dưới trướng.') : bridge.sendChoice('social', `recruit:${cur.npcId}`));

    const canSwear = cur.affinity >= 80 && cur.trust >= 60 && !cur.isSworn;
    const swBtn = el('button', `tk-soc-act${canSwear ? ' is-primary' : ' is-disabled'}`);
    swBtn.type = 'button';
    swBtn.append(el('span', '', 'Kết nghĩa'), el('em', '', cur.isSworn ? 'Đã kết nghĩa' : `Hảo cảm ${cur.affinity}/80 · Tín ${cur.trust}/60`));
    swBtn.addEventListener('click', () => cur.isSworn ? flash('Đã kết nghĩa Kim Lan.') : bridge.sendChoice('social', `swear:${cur.npcId}`));

    const talkBtn = el('button', 'tk-soc-act');
    talkBtn.type = 'button';
    talkBtn.style.marginTop = 'auto';
    talkBtn.append(el('span', '', 'Mở cảnh nói chuyện'), el('em', '', '2.5D'));
    talkBtn.addEventListener('click', () => bridge.sendNavigate('social', `npc:${cur.npcId}`));
    inspectorBox.append(recBtn, swBtn, talkBtn);
  }

  function redrawRelationTab() {
    let existingMap = bodyBox.querySelector('.tk-soc-map');
    let existingInsp = bodyBox.querySelector('.tk-soc-inspector');

    if (!existingMap || !existingInsp) {
      bodyBox.replaceChildren();
      mapBox = el('div', 'tk-soc-map');
      inspectorBox = el('div', 'tk-soc-inspector');

      for (const [aff, label] of [[35, 'Quen biết · 35'], [60, 'Thân thiết · 60'], [80, 'Tri kỷ · 80']]) {
        const rPct = ringRadiusPct(aff);
        const ring = el('div', 'tk-soc-ring');
        ring.style.width = `${rPct * 2}%`;
        ring.style.height = `${rPct * 1.84}%`;
        const rl = el('div', 'tk-soc-ring-label', label);
        rl.style.top = `${50 - rPct * 0.92}%`;
        mapBox.append(ring, rl);
      }

      const center = el('div', 'tk-soc-center');
      const pImg = el('img');
      pImg.src = scene.playerPortraitPath || 'art/portraits/player/face.webp';
      pImg.alt = 'Bạn';
      center.append(pImg, el('div', '', 'Bạn'));
      mapBox.appendChild(center);
      bodyBox.append(mapBox, inspectorBox);
    } else {
      mapBox = existingMap;
      inspectorBox = existingInsp;
    }

    const existingBtns = new Map();
    for (const b of mapBox.querySelectorAll('.tk-soc-node')) {
      existingBtns.set(Number(b.dataset.npcId), b);
    }

    const seenIds = new Set();
    const total = Math.max(1, nodes.length);
    for (let idx = 0; idx < nodes.length; idx += 1) {
      const n = nodes[idx];
      seenIds.add(n.npcId);
      const rPct = nodeRadiusPct(n.affinity, idx, total);
      const deg = -90 + ((idx + 0.5) * 360) / total;
      const rad = (deg * Math.PI) / 180;
      const leftPct = Math.max(11, Math.min(89, 50 + rPct * Math.cos(rad)));
      const topPct = Math.max(12, Math.min(86, 50 + rPct * 0.86 * Math.sin(rad)));
      const labelText = shortName(n.nameVi);

      let btn = existingBtns.get(n.npcId);
      if (!btn) {
        btn = el('button', 'tk-soc-node');
        btn.type = 'button';
        btn.dataset.npcId = String(n.npcId);
        const img = el('img');
        img.src = n.facePath || 'art/portraits/scholar/face.webp';
        img.alt = labelText;
        const lbl = el('span', '', labelText);
        btn.append(img, lbl);
        btn.addEventListener('click', () => {
          rememberedNpcId = n.npcId;
          redrawRelationTab();
        });
        mapBox.appendChild(btn);
      } else {
        const lbl = btn.querySelector('span');
        if (lbl) lbl.textContent = labelText;
      }

      btn.classList.toggle('is-selected', n.npcId === rememberedNpcId);
      btn.style.left = `${leftPct.toFixed(2)}%`;
      btn.style.top = `${topPct.toFixed(2)}%`;
    }

    for (const [id, b] of existingBtns.entries()) {
      if (!seenIds.has(id)) b.remove();
    }

    const cur = nodes.find((n) => n.npcId === rememberedNpcId) || nodes[0] || null;
    redrawInspector(cur);
  }

  function redrawSkillsTab() {
    bodyBox.replaceChildren();
    const pane = el('div', 'tk-soc-skills-pane');
    const equippedCount = skills.filter((s) => s.equipped).length;

    const topRow = el('div');
    topRow.style.cssText = 'display:flex;justify-content:space-between;font-size:13.5px';
    topRow.append(
      el('span', '', 'Bấm tuyệt kỹ bên dưới để gắn vào ô. Bấm ô đã gắn để gỡ.'),
      el('span', 'tk-soc-rank', equippedCount ? `Đang gắn ${equippedCount} tuyệt kỹ` : 'Chưa gắn tuyệt kỹ nào')
    );
    pane.appendChild(topRow);

    const slotsGrid = el('div', 'tk-soc-slots');
    for (let i = 0; i < 6; i += 1) {
      const slotNum = i + 1;
      const eqSkill = skills.find((s) => s.equipped && s.equippedSlot === slotNum);
      const slotBtn = el('button', `tk-soc-slot${eqSkill ? ' is-full' : ''}`);
      slotBtn.type = 'button';
      const hanEl = el('span', '', eqSkill ? eqSkill.hanzi || '技' : '+');
      hanEl.style.cssText = 'font:26px "Noto Serif",serif;color:#c4a062';
      const nameEl = el('span', '', eqSkill ? eqSkill.nameVi : 'Còn trống');
      nameEl.style.fontWeight = '600';
      const ruleEl = el('span', 'tk-soc-role', SLOT_RULE_LABELS[SLOT_RULES[i]]);
      slotBtn.append(hanEl, nameEl, ruleEl);
      slotBtn.addEventListener('click', () => {
        if (eqSkill) bridge.sendChoice('social', `skill:unequip:${slotNum}`);
      });
      slotsGrid.appendChild(slotBtn);
    }
    pane.appendChild(slotsGrid);

    pane.appendChild(el('div', 'tk-soc-sec', 'TUYỆT KỸ ĐÃ HỌC VÀ CÁCH CÓ THÊM'));
    const libGrid = el('div', 'tk-soc-lib');
    for (const sk of skills) {
      const card = el('button', `tk-soc-card${sk.learned ? '' : ' is-locked'}`);
      card.type = 'button';
      const r1 = el('div');
      r1.style.cssText = 'display:flex;justify-content:space-between;font-weight:600';
      r1.append(el('span', '', sk.nameVi), el('span', 'tk-soc-rank', `Phẩm ${sk.rank}`));

      const r2 = el('div', 'tk-soc-role', sk.effectVi);
      r2.style.marginTop = '6px';

      const statusText = sk.equipped ? `Đang gắn (Ô ${sk.equippedSlot})` : sk.learned ? 'Bấm để gắn vào ô trống' : sk.needVi;
      const r3 = el('div', '', statusText);
      r3.style.cssText = `margin-top:6px;font-size:12px;color:${sk.equipped ? '#7fc7a2' : sk.learned ? '#e8b75c' : '#b9aa8c'}`;

      card.append(r1, r2, r3);
      card.addEventListener('click', () => {
        if (!sk.learned) {
          if (sk.teacherNpcId > 0) bridge.sendChoice('social', `learn:${sk.teacherNpcId}:${sk.skillCode}`);
          else flash(sk.needVi);
          return;
        }
        if (sk.equipped) {
          bridge.sendChoice('social', `skill:unequip:${sk.equippedSlot}`);
          return;
        }
        let targetSlot = -1;
        for (let i = 0; i < 6; i += 1) {
          const slotNum = i + 1;
          if (!skills.some((x) => x.equipped && x.equippedSlot === slotNum) && fitsSlotRule(sk.rank, i)) {
            targetSlot = slotNum;
            break;
          }
        }
        if (targetSlot < 0) {
          flash(`Hết ô trống phù hợp với phẩm ${sk.rank}`);
          return;
        }
        bridge.sendChoice('social', `skill:equip:${sk.skillCode}:${targetSlot}`);
      });
      libGrid.appendChild(card);
    }
    pane.appendChild(libGrid);
    bodyBox.appendChild(pane);
  }

  function redrawListTab(isMail) {
    bodyBox.replaceChildren();
    const list = isMail ? letters : retainers;
    if (list.length === 0) {
      const empty = el('div', 'tk-soc-empty');
      const han = el('div', '', isMail ? '書' : '臣');
      han.style.cssText = 'font:52px "Noto Serif",serif;color:#6a4f2e';
      empty.append(
        han,
        el('div', 'tk-soc-name', isMail ? 'Chưa có thư' : 'Chưa có gia thần'),
        el('div', 'tk-soc-role', isMail ? 'Khi hảo cảm từ 20 trở lên, bạn hữu ở xa sẽ gửi thư kèm quà.' : 'Chiêu mộ người đạt mốc hảo cảm, họ sẽ làm việc và lĩnh bổng hàng tháng.')
      );
      const backBtn = el('button', 'tk-soc-act is-primary', 'Sang tab Quan hệ');
      backBtn.type = 'button';
      backBtn.addEventListener('click', () => { activeTabKey = 'rel'; redrawBody(); redrawTabs(); });
      empty.appendChild(backBtn);
      bodyBox.appendChild(empty);
      return;
    }

    const pane = el('div', 'tk-soc-skills-pane');
    const grid = el('div', 'tk-soc-lib');
    if (isMail) {
      for (const l of letters) {
        const card = el('div', 'tk-soc-card');
        card.append(
          el('div', 'tk-soc-name', `${l.subject} · ${shortName(l.senderNameVi)}`),
          el('div', 'tk-soc-role', `${l.dateText} · ${l.attachmentSummary}`),
          el('div', 'tk-soc-role', l.body)
        );
        if (l.hasClaimableGift) {
          const claimBtn = el('button', 'tk-soc-act is-primary', 'Nhận quà đính kèm');
          claimBtn.type = 'button';
          claimBtn.style.marginTop = '8px';
          claimBtn.addEventListener('click', () => bridge.sendChoice('social', `letter:claim:${l.id}`));
          card.appendChild(claimBtn);
        }
        grid.appendChild(card);
      }
    } else {
      for (const r of retainers) {
        const card = el('div', 'tk-soc-card');
        card.append(
          el('div', 'tk-soc-name', `${shortName(r.nameVi)} (Phẩm ${r.rankCode})`),
          el('div', 'tk-soc-role', `${r.roleVi} · Bổng ${r.monthlyStipend} quan/tháng · Trung thành ${r.loyalty}/100`),
          el('div', 'tk-soc-rank', r.bonusSummaryVi)
        );
        grid.appendChild(card);
      }
    }
    pane.appendChild(grid);
    bodyBox.appendChild(pane);
  }

  function redrawBody() {
    if (activeTabKey === 'rel') redrawRelationTab();
    else if (activeTabKey === 'skill') redrawSkillsTab();
    else if (activeTabKey === 'mail') redrawListTab(true);
    else redrawListTab(false);
  }

  redrawTabs();
  redrawBody();
}

SimTBridge.registerStage('social', { render: renderSocialScene, unmount() {} });
