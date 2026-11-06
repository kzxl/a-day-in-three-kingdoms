import { SimTBridge } from './bridge.js';

let rememberedItemId = 0;
let currentQty = 5;

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined && text !== null) node.textContent = String(text);
  return node;
}

function buildSparklinePoints(history, fallbackPrice) {
  const arr = Array.isArray(history) && history.length >= 2
    ? history
    : [fallbackPrice, fallbackPrice, fallbackPrice, fallbackPrice, fallbackPrice];
  const lo = Math.min(...arr);
  const hi = Math.max(...arr);
  const span = hi - lo || 1;
  const n = arr.length - 1;
  return arr
    .map((v, i) => `${Math.round((i * 200) / n)},${Math.round(30 - ((v - lo) / span) * 24)}`)
    .join(' ');
}

function renderMarketScene(container, scene, bridge) {
  if (!container || !scene) return;
  container.replaceChildren();

  const items = Array.isArray(scene.items) ? scene.items : [];
  if (items.length === 0) return;

  if (scene.selectedItemId && items.some((x) => x.itemId === scene.selectedItemId)) {
    rememberedItemId = scene.selectedItemId;
  } else if (!items.some((x) => x.itemId === rememberedItemId)) {
    rememberedItemId = items[0].itemId;
  }

  let statusMsg = '';

  const shell = el('div', 'tk-mkt-shell');

  const banner = el('div', 'tk-mkt-banner');
  banner.style.backgroundImage = `url("${scene.bannerImagePath || 'art/backgrounds/market.png'}")`;
  banner.appendChild(el('div', 'tk-mkt-banner-scrim'));
  const bannerRow = el('div', 'tk-mkt-banner-row');
  const titleWrap = el('div');
  titleWrap.appendChild(el('h1', 'tk-mkt-title', `Chợ ${scene.currentCityName || 'Lạc Dương'}`));
  titleWrap.appendChild(el('div', 'tk-mkt-sub', 'Mua rẻ ở đây, chở sang thành giá cao để bán lãi.'));
  const cashEl = el('div', 'tk-mkt-cash', `${(scene.cash || 0).toLocaleString('vi-VN')} quan`);
  bannerRow.append(titleWrap, cashEl);
  banner.appendChild(bannerRow);
  shell.appendChild(banner);

  const main = el('div', 'tk-mkt-main');
  const recBar = el('div', 'tk-mkt-rec');
  recBar.appendChild(el('span', 'tk-mkt-rec-tag', 'NÊN LÀM'));
  recBar.appendChild(el('span', 'tk-mkt-rec-text', scene.recommendation || 'Mua mặt hàng đang rẻ tại chợ rồi chở sang thành có giá thu mua cao hơn.'));
  main.appendChild(recBar);

  const body = el('div', 'tk-mkt-body');
  const grid = el('div', 'tk-mkt-grid');
  const right = el('div', 'tk-mkt-right');
  body.append(grid, right);
  main.appendChild(body);
  shell.appendChild(main);
  container.appendChild(shell);

  const svgNS = 'http://www.w3.org/2000/svg';

  function redrawAll() {
    const selItem = items.find((x) => x.itemId === rememberedItemId) || items[0];
    grid.replaceChildren();

    for (const g of items) {
      const hist = Array.isArray(g.historyPrices) && g.historyPrices.length > 0 ? g.historyPrices : [g.buyPrice];
      const lo = Math.min(...hist);
      const hi = Math.max(...hist);
      const hasVariation = hist.length >= 2 && hi > lo;
      const delta = hist[hist.length - 1] - hist[0];
      const col = delta > 0 ? '#e8927c' : delta < 0 ? '#7fc7a2' : '#b9aa8c';
      const trend = delta > 0 ? 'Đang lên' : delta < 0 ? 'Đang rẻ' : 'Giá ổn định';

      const card = el('button', `tk-mkt-good${g.itemId === selItem.itemId ? ' is-selected' : ''}`);
      card.type = 'button';

      const topSpan = el('span');
      topSpan.style.display = 'flex';
      topSpan.style.alignItems = 'center';
      topSpan.style.gap = '10px';
      const ic = el('span', 'tk-mkt-ic', g.hanzi || '貨');
      const nameBox = el('span');
      nameBox.style.minWidth = '0';
      const nm = el('span', '', g.nameVi);
      nm.style.display = 'block';
      nm.style.fontSize = '14.5px';
      nm.style.fontWeight = '600';
      const sub = el('span', '', `Trong túi ${g.held} · Sạp ${g.stock}`);
      sub.style.display = 'block';
      sub.style.fontSize = '11.5px';
      sub.style.color = '#b9aa8c';
      nameBox.append(nm, sub);
      topSpan.append(ic, nameBox);

      let midNode;
      if (hasVariation) {
        const svg = document.createElementNS(svgNS, 'svg');
        svg.setAttribute('width', '100%');
        svg.setAttribute('height', '32');
        svg.setAttribute('viewBox', '0 0 200 34');
        svg.setAttribute('preserveAspectRatio', 'none');
        const poly = document.createElementNS(svgNS, 'polyline');
        poly.setAttribute('points', buildSparklinePoints(hist, g.buyPrice));
        poly.setAttribute('fill', 'none');
        poly.setAttribute('stroke', col);
        poly.setAttribute('stroke-width', '2');
        svg.appendChild(poly);
        midNode = svg;
      } else {
        midNode = el('span', 'tk-mkt-stable', 'Giá ổn định trong 10 ngày qua');
      }

      const botSpan = el('span');
      botSpan.style.display = 'flex';
      botSpan.style.justifyContent = 'space-between';
      botSpan.style.fontSize = '12.5px';
      const priceText = el('span', '', `Mua ${g.buyPrice} · Bán ${g.sellPrice}`);
      const trendEl = el('span', '', trend);
      trendEl.style.color = col;
      botSpan.append(priceText, trendEl);

      card.append(topSpan, midNode, botSpan);
      card.addEventListener('click', () => {
        rememberedItemId = g.itemId;
        statusMsg = '';
        redrawAll();
      });
      grid.appendChild(card);
    }

    right.replaceChildren();
    const head = el('div');
    head.style.display = 'flex';
    head.style.gap = '12px';
    head.style.alignItems = 'center';
    const bigIc = el('span', 'tk-mkt-ic', selItem.hanzi || '貨');
    bigIc.style.width = '50px';
    bigIc.style.height = '50px';
    bigIc.style.fontSize = '28px';
    const headText = el('span');
    const hName = el('span', '', selItem.nameVi);
    hName.style.display = 'block';
    hName.style.font = '700 18px "Noto Serif", serif';
    hName.style.color = '#f3e6cc';
    const hSub = el('span', '', `Sạp còn ${selItem.stock} · trong túi ${selItem.held}`);
    hSub.style.display = 'block';
    hSub.style.fontSize = '12.5px';
    hSub.style.color = '#b9aa8c';
    headText.append(hName, hSub);
    head.append(bigIc, headText);
    right.appendChild(head);

    const arbBox = el('div');
    arbBox.appendChild(el('div', 'tk-mkt-sec', 'BÁN Ở ĐÂU LỜI NHẤT'));
    const bestCities = Array.isArray(selItem.bestSellCities) ? selItem.bestSellCities : [];
    for (const c of bestCities) {
      const row = el('div', 'tk-mkt-city-row');
      row.appendChild(el('span', '', `${c.cityNameVi} · ${c.travelDays} ngày`));
      const gainStr = `${c.estimatedSellPrice} quan (${c.diffPerUnit >= 0 ? '+' : ''}${c.diffPerUnit})`;
      const gainEl = el('span', '', gainStr);
      gainEl.style.fontWeight = '600';
      gainEl.style.color = c.diffPerUnit > 0 ? '#7fc7a2' : '#b9aa8c';
      row.appendChild(gainEl);
      arbBox.appendChild(row);
    }
    right.appendChild(arbBox);

    const qtyBox = el('div');
    qtyBox.appendChild(el('div', 'tk-mkt-sec', 'SỐ LƯỢNG'));
    const qtyRow = el('div', 'tk-mkt-qty-row');
    const decBtn = el('button', 'tk-mkt-sq', '−');
    decBtn.type = 'button';
    decBtn.addEventListener('click', () => {
      currentQty = Math.max(1, currentQty - 1);
      statusMsg = '';
      redrawAll();
    });
    const qtyVal = el('div', 'tk-mkt-qty-val', String(currentQty));
    const incBtn = el('button', 'tk-mkt-sq', '+');
    incBtn.type = 'button';
    incBtn.addEventListener('click', () => {
      currentQty = Math.min(99, currentQty + 1);
      statusMsg = '';
      redrawAll();
    });
    const maxBtn = el('button', 'tk-mkt-sq', 'Tối đa');
    maxBtn.type = 'button';
    maxBtn.style.width = '64px';
    maxBtn.style.fontSize = '12.5px';
    maxBtn.addEventListener('click', () => {
      const maxBuy = selItem.buyPrice > 0 ? Math.floor((scene.cash || 0) / selItem.buyPrice) : 1;
      const cap = Math.min(selItem.stock || 0, maxBuy);
      currentQty = Math.max(1, Math.min(99, cap > 0 ? cap : selItem.held || 1));
      statusMsg = '';
      redrawAll();
    });
    qtyRow.append(decBtn, qtyVal, incBtn, maxBtn);
    qtyBox.appendChild(qtyRow);
    right.appendChild(qtyBox);

    const totalCost = currentQty * (selItem.buyPrice || 0);
    const rTotal = el('div', 'tk-mkt-stat-row');
    rTotal.append(
      el('span', '', 'Tổng tiền mua'),
      el('span', '', `${totalCost.toLocaleString('vi-VN')} quan`)
    );

    const topDiff = bestCities.length > 0 ? Math.max(0, bestCities[0].diffPerUnit) : 0;
    const estProfit = currentQty * topDiff;
    const rProfit = el('div', 'tk-mkt-stat-row');
    const profVal = el('span', '', `lãi ${estProfit.toLocaleString('vi-VN')} quan`);
    profVal.style.color = '#7fc7a2';
    profVal.style.fontWeight = '600';
    rProfit.append(el('span', '', 'Nếu bán ở thành tốt nhất'), profVal);
    right.append(rTotal, rProfit);

    const actRow = el('div', 'tk-mkt-actions');
    const buyBtn = el('button', 'tk-mkt-btn tk-mkt-btn-buy', `Mua ${currentQty}`);
    buyBtn.type = 'button';
    buyBtn.addEventListener('click', () => {
      if (totalCost > (scene.cash || 0)) {
        statusMsg = 'Không đủ tiền mặt trong túi.';
        redrawAll();
        return;
      }
      if (currentQty > (selItem.stock || 0)) {
        statusMsg = 'Sạp không đủ số lượng hàng.';
        redrawAll();
        return;
      }
      bridge.sendChoice('market', `buy:${selItem.itemId}:${currentQty}`);
    });

    const sellBtn = el('button', 'tk-mkt-btn tk-mkt-btn-sell', `Bán ${currentQty}`);
    sellBtn.type = 'button';
    sellBtn.addEventListener('click', () => {
      if (currentQty > (selItem.held || 0)) {
        statusMsg = 'Trong túi không đủ số lượng để bán.';
        redrawAll();
        return;
      }
      bridge.sendChoice('market', `sell:${selItem.itemId}:${currentQty}`);
    });
    actRow.append(buyBtn, sellBtn);
    right.appendChild(actRow);

    right.appendChild(el('div', 'tk-mkt-msg', statusMsg));
  }

  redrawAll();
}

SimTBridge.registerStage('market', {
  render: renderMarketScene,
  unmount() {}
});
