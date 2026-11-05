// Shared real-time requestAnimationFrame sprite strip & portrait animator
// Used by duel-scene.js, city-scene.js, and talk-scene.js.

export const STRIP_CELL_W = 340;
export const STRIP_CELL_H = 170;
export const STRIP_FRAMES = 24;
export const SCALE_DUEL = 2.85;
export const STEP_DUEL = 25.6;
export const SCALE_CITY = 2;
export const STEP_CITY = 18;
export const DUEL_SPEED = 380;
export const CITY_SPEED = 165;

export const ATK_DURATIONS = [0.07, 0.08, 0.13, 0.06, 0.05, 0.10, 0.10, 0.12];
export const ATK_FRAMES = {
  guan_yu: [12, 14, 16, 16, 15, 17, 18, 19],
  zhang_fei: [12, 13, 14, 15, 16, 17, 18, 19],
  default: [12, 13, 14, 15, 16, 17, 18, 19]
};

export function computeWalkFrame(dist, step, reverse = false) {
  const safeStep = step > 0 ? step : STEP_DUEL;
  const i = Math.floor(Math.max(0, dist) / safeStep) % 8;
  return 4 + (reverse ? 7 - i : i);
}

export function computeAttackLunge(t, dir) {
  const t0 = 0.34;
  const t1 = 0.44;
  if (t > t0 && t < t1) {
    const k = (t - t0) / (t1 - t0);
    return dir * 58 * (k * k * (3 - 2 * k));
  }
  if (t >= t1) {
    return dir * Math.max(0, 58 * (1 - (t - t1) / 0.26));
  }
  return 0;
}

export function createDuelFighterState(cfg, index) {
  const isLeft = index === 0;
  const home = cfg.home ?? (isLeft ? 300 : 724);
  const dir = isLeft ? 1 : -1;
  const key = String(cfg.animKey || '').toLowerCase();
  const atk = ATK_FRAMES[key] || ATK_FRAMES.default;
  return {
    el: cfg.el || null,
    spEl: cfg.spEl || null,
    hasStrip: Boolean(cfg.animPath),
    animPath: cfg.animPath || null,
    spritePath: cfg.spritePath || '',
    atk,
    x: home,
    home,
    dir,
    hp: cfg.hp ?? 100,
    max: cfg.maxHp ?? 100,
    st: 'idle',
    t: 0,
    fr: 0,
    it: isLeft ? 0 : 0.4,
    dist: 0,
    tx: home,
    hit: false,
    fl: 0,
    kb: 0,
    back: false,
    pendingImpact: null
  };
}

export function stepDuelState(state, rawDt) {
  const dt = Math.max(0, Math.min(0.05, rawDt));
  if (state.stop > 0) {
    state.stop = Math.max(0, state.stop - dt);
    return;
  }
  state.sh = Math.max(0, state.sh - dt * 4.5);

  for (let idx = 0; idx < state.fighters.length; idx++) {
    const f = state.fighters[idx];
    f.t += dt;
    f.it += dt;
    f.fl = Math.max(0, f.fl - dt);

    if (f.st === 'idle') {
      f.fr = Math.floor(f.it / 0.26) % 4;
      f.x += (f.home - f.x) * Math.min(1, dt * 8);
    } else if (f.st === 'run') {
      const d = f.tx - f.x;
      const sp = f.back ? DUEL_SPEED * 0.7 : DUEL_SPEED;
      const stepDist = Math.min(Math.abs(d), sp * dt);
      f.x += Math.sign(d) * stepDist;
      f.dist += stepDist;
      f.fr = computeWalkFrame(f.dist, STEP_DUEL, f.back);
      if (Math.abs(d) < 4) {
        f.x = f.tx;
        f.t = 0;
        if (f.back) {
          f.st = 'idle';
          f.back = false;
        } else {
          f.st = 'atk';
        }
      }
    } else if (f.st === 'atk') {
      let acc = 0;
      let i = 7;
      for (let k = 0; k < ATK_DURATIONS.length; k++) {
        acc += ATK_DURATIONS[k];
        if (f.t <= acc) {
          i = k;
          break;
        }
      }
      f.fr = f.atk[i];
      if (i >= 5 && !f.hit) {
        f.hit = true;
        triggerDuelImpact(state, idx);
      }
      if (f.t >= 0.71) {
        f.st = 'run';
        f.tx = f.home;
        f.t = 0;
        f.back = true;
      }
    } else if (f.st === 'hit') {
      const k = Math.min(1, f.t / 0.42);
      f.x += -f.dir * f.kb * (1 - k) * dt;
      f.fr = f.t < 0.14 ? 20 : f.t < 0.34 ? 21 : 22;
      if (f.t >= 0.56) {
        if (f.hp <= 0) {
          f.st = 'ko';
          f.fr = 23;
        } else {
          f.st = 'run';
          f.tx = f.home;
          f.t = 0;
          f.back = true;
        }
      }
    } else if (f.st === 'ko') {
      f.fr = 23;
    }
  }

  if (state.waitResolve && state.fighters.every((f) => f.st === 'idle' || f.st === 'ko')) {
    const resolve = state.waitResolve;
    state.waitResolve = null;
    resolve();
  }
}

function triggerDuelImpact(state, atkIdx) {
  const a = state.fighters[atkIdx];
  const defIdx = atkIdx === 0 ? 1 : 0;
  const d = state.fighters[defIdx];
  const info = a.pendingImpact || {};
  a.pendingImpact = null;

  state.stop = 0.1;
  state.sh = 1;
  if (typeof info.defenderHpAfter === 'number') {
    d.hp = Math.max(0, info.defenderHpAfter);
  } else if (typeof info.damage === 'number' && info.damage > 0) {
    d.hp = Math.max(0, d.hp - info.damage);
  }
  d.fl = 0.09;
  d.kb = 340;
  d.st = 'hit';
  d.t = 0;
  d.back = false;

  if (typeof info.onImpact === 'function') {
    info.onImpact({ attacker: a, defender: d, damage: info.damage || 0 });
  }
}

export function drawDuelState(state) {
  const { stageEl, stageScale = 1 } = state;
  if (stageEl) {
    const sx = state.sh > 0 ? Math.sin(performance.now() * 1.1) * 7 * state.sh : 0;
    const sy = state.sh > 0 ? Math.cos(performance.now() * 1.5) * 4 * state.sh : 0;
    stageEl.style.transform = `translate(${sx.toFixed(1)}px, ${sy.toFixed(1)}px) scale(${stageScale.toFixed(3)})`;
  }

  for (const f of state.fighters) {
    if (!f.el || !f.spEl) continue;
    const lunge = f.st === 'atk' ? computeAttackLunge(f.t, f.dir) : 0;
    f.el.style.left = `${(f.x - STRIP_CELL_W / 2).toFixed(1)}px`;
    if (f.hasStrip) {
      f.spEl.style.backgroundPosition = `-${f.fr * STRIP_CELL_W}px 0`;
      f.spEl.style.transform = `translate3d(${lunge.toFixed(1)}px, 0px, 0) scale(${(f.dir * SCALE_DUEL).toFixed(3)}, ${SCALE_DUEL})`;
    } else {
      const koPose = f.st === 'ko' ? ' rotate(-72deg) translateY(22px)' : '';
      f.spEl.style.transform = `translate3d(${lunge.toFixed(1)}px, 0px, 0) scaleX(${f.dir})${koPose}`;
    }
    f.spEl.style.filter = f.fl > 0
      ? 'brightness(2.6) drop-shadow(0 0 12px #fff)'
      : 'drop-shadow(0 6px 8px rgba(0,0,0,.55))';
  }
}

export function createDuelAnimator({ stageEl, fighters }) {
  const state = {
    stageEl,
    stageScale: 1,
    stop: 0,
    sh: 0,
    waitResolve: null,
    fighters: fighters.map((cfg, i) => createDuelFighterState(cfg, i))
  };

  return {
    state,
    setStageScale(scale) {
      state.stageScale = scale;
      drawDuelState(state);
    },
    step(dt) {
      stepDuelState(state, dt);
      drawDuelState(state);
    },
    performStrike(atkIdx, defIdx, opts = {}) {
      const a = state.fighters[atkIdx];
      const b = state.fighters[defIdx];
      if (!a || !b || a.st === 'ko') return Promise.resolve();
      if (state.waitResolve) {
        const prev = state.waitResolve;
        state.waitResolve = null;
        prev();
      }
      a.st = 'run';
      a.tx = b.x - a.dir * 210;
      a.t = 0;
      a.hit = false;
      a.back = false;
      a.pendingImpact = opts;
      return new Promise((resolve) => {
        state.waitResolve = resolve;
      });
    },
    snapToEnd(pHp, oHp) {
      state.stop = 0;
      state.sh = 0;
      if (state.waitResolve) {
        const prev = state.waitResolve;
        state.waitResolve = null;
        prev();
      }
      const [p, o] = state.fighters;
      if (p) {
        p.hp = pHp;
        p.x = p.home;
        p.back = false;
        p.fl = 0;
        p.st = pHp <= 0 ? 'ko' : 'idle';
        p.fr = pHp <= 0 ? 23 : 0;
      }
      if (o) {
        o.hp = oHp;
        o.x = o.home;
        o.back = false;
        o.fl = 0;
        o.st = oHp <= 0 ? 'ko' : 'idle';
        o.fr = oHp <= 0 ? 23 : 0;
      }
      drawDuelState(state);
    }
  };
}

export function stepCityWalkers(walkers, rawDt) {
  const dt = Math.max(0, Math.min(0.05, rawDt));
  for (const n of walkers) {
    n.it = (n.it || 0) + dt;
    if (!n.walk || !n.hasStrip) continue;
    if (n.wait > 0) {
      n.wait -= dt;
      n.fr = Math.floor(n.it / 0.3) % 4;
      if (n.wait <= 0) n.dir *= -1;
    } else {
      const stepDist = CITY_SPEED * dt;
      n.x += n.dir * stepDist;
      n.dist = (n.dist || 0) + stepDist;
      // STEP = 18 at scale 2, no vertical bobbing added in code
      n.fr = computeWalkFrame(n.dist, STEP_CITY, false);
      if ((n.dir > 0 && n.x >= n.max) || (n.dir < 0 && n.x <= n.min)) {
        n.x = Math.max(n.min, Math.min(n.max, n.x));
        n.wait = 2.2;
      }
    }
    if (n.el && n.spEl) {
      n.el.style.left = `${(n.x - STRIP_CELL_W / 2).toFixed(1)}px`;
      n.spEl.style.backgroundPosition = `-${n.fr * STRIP_CELL_W}px 0`;
      n.spEl.style.transform = `scale(${n.dir * SCALE_CITY}, ${SCALE_CITY})`;
    }
  }
}

export function createPortraitAnimator({ baseEl, blinkEl, talkEl, lineEl }) {
  const st = {
    fullText: '',
    typed: 0,
    charTimer: 0,
    speaking: false,
    it: 0,
    bt: 1.8 + Math.random() * 1.6,
    blink: 0
  };

  function syncFrames() {
    if (!blinkEl || !talkEl || !baseEl) return;
    const mouth = st.speaking && Math.floor(st.it / 0.11) % 2 === 0;
    const isBlink = st.blink > 0;
    blinkEl.style.opacity = isBlink ? '1' : '0';
    talkEl.style.opacity = !isBlink && mouth ? '1' : '0';
    baseEl.style.opacity = isBlink || mouth ? '0' : '1';
  }

  return {
    setLine(text) {
      st.fullText = String(text ?? '');
      st.typed = 0;
      st.charTimer = 0;
      st.speaking = st.fullText.length > 0;
      if (lineEl) lineEl.textContent = '“”';
      syncFrames();
    },
     isTyping() {
      return st.speaking && st.typed < st.fullText.length;
    },
    completeLine() {
      st.typed = st.fullText.length;
      st.speaking = false;
      if (lineEl) lineEl.textContent = `“${st.fullText}”`;
      syncFrames();
    },
    step(rawDt) {
      const dt = Math.max(0, Math.min(0.05, rawDt));
      st.it += dt;
      if (st.speaking) {
        st.charTimer += dt;
        while (st.charTimer >= 0.045 && st.typed < st.fullText.length) {
          st.charTimer -= 0.045;
          st.typed++;
          if (lineEl) lineEl.textContent = `“${st.fullText.slice(0, st.typed)}”`;
        }
        if (st.typed >= st.fullText.length) st.speaking = false;
      }
      st.bt -= dt;
      if (st.bt <= 0) {
        st.blink = 0.13;
        st.bt = 2.4 + Math.random() * 2.0;
      }
      if (st.blink > 0) st.blink = Math.max(0, st.blink - dt);
      syncFrames();
    }
  };
}
