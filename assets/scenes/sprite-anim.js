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
const DEFAULT_ATK = [12, 13, 14, 15, 16, 17, 18, 19];
export const ATK_FRAMES = {
  guan_yu: [12, 14, 16, 16, 15, 17, 18, 19],
  zhang_fei: DEFAULT_ATK,
  traveler: DEFAULT_ATK,
  han_officer: DEFAULT_ATK,
  general: DEFAULT_ATK,
  default: DEFAULT_ATK
};
export const STRIP_BODY_H = { guan_yu: 98, zhang_fei: 98, traveler: 132, han_officer: 136, general: 136, default: 115 };

export function resolveStripBodyHeight(animPath) {
  const p = String(animPath || '').toLowerCase();
  if (p.includes('han_officer')) return STRIP_BODY_H.han_officer;
  if (p.includes('traveler')) return STRIP_BODY_H.traveler;
  if (p.includes('guan_yu')) return STRIP_BODY_H.guan_yu;
  if (p.includes('zhang_fei')) return STRIP_BODY_H.zhang_fei;
  return STRIP_BODY_H.default;
}

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
  return t >= t1 ? dir * Math.max(0, 58 * (1 - (t - t1) / 0.26)) : 0;
}

export function createDuelFighterState(cfg, index) {
  const isLeft = index === 0;
  const home = cfg.home ?? (isLeft ? 300 : 724);
  const dir = isLeft ? 1 : -1;
  const key = String(cfg.animKey || '').toLowerCase();
  const atk = ATK_FRAMES[key] || ATK_FRAMES.default;
  return {
    el: cfg.el || null, spEl: cfg.spEl || null,
    hasStrip: Boolean(cfg.animPath), animPath: cfg.animPath || null, spritePath: cfg.spritePath || '',
    atk, x: home, home, dir, hp: cfg.hp ?? 100, max: cfg.maxHp ?? 100,
    st: 'idle', t: 0, fr: 0, it: isLeft ? 0 : 0.4, dist: 0, tx: home,
    hit: false, fl: 0, kb: 0, back: false, pendingImpact: null
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
      const stripScale = (SCALE_DUEL * 98) / resolveStripBodyHeight(f.animPath);
      f.spEl.style.backgroundPosition = `-${f.fr * STRIP_CELL_W}px 0`;
      f.spEl.style.transform = `translate3d(${lunge.toFixed(1)}px, 0px, 0) scale(${(f.dir * stripScale).toFixed(3)}, ${stripScale.toFixed(3)})`;
    } else {
      let tx = lunge;
      let ty = 0;
      let rot = 0;
      let sx = 1;
      let sy = 1;
      if (f.st === 'idle') {
        ty = Math.sin(f.it * 3.2) * 3;
        sy = 1 + Math.sin(f.it * 3.2) * 0.014;
      } else if (f.st === 'run') {
        ty = -Math.abs(Math.sin(f.dist * 0.075)) * 8;
        rot = f.back ? -7 : 10;
      } else if (f.st === 'atk') {
        if (f.t < 0.28) {
          const k = f.t / 0.28;
          tx = -f.dir * 26 * k;
          rot = -12 * k;
          sx = 1 + 0.04 * k;
          sy = 1 - 0.06 * k;
        } else if (f.t < 0.48) {
          tx = f.dir * 66;
          rot = 16;
          sx = 1.07;
          sy = 1.03;
        } else {
          const k = Math.max(0, 1 - (f.t - 0.48) / 0.23);
          tx = f.dir * 66 * k;
          rot = 16 * k;
        }
      } else if (f.st === 'hit') {
        const k = Math.max(0, 1 - f.t / 0.48);
        rot = -18 * k;
        sx = 1 + 0.06 * k;
        sy = 1 - 0.08 * k;
      } else if (f.st === 'ko') {
        rot = -72;
        ty = 22;
      }
      f.spEl.style.transform = `translate3d(${tx.toFixed(1)}px, ${ty.toFixed(1)}px, 0) scaleX(${f.dir}) rotate(${rot.toFixed(1)}deg) scale(${sx.toFixed(3)}, ${sy.toFixed(3)})`;
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
    if (!n.hasStrip) continue;
    const scale = n.scale || SCALE_CITY;
    const halfW = n.halfW || (STRIP_CELL_W / 2);
    if (!n.walk) {
      n.fr = Math.floor(n.it / 0.3) % 4;
    } else if (n.wait > 0) {
      n.wait -= dt;
      n.fr = Math.floor(n.it / 0.3) % 4;
      if (n.wait <= 0) n.dir *= -1;
    } else {
      const speed = n.speed || CITY_SPEED * (scale / SCALE_CITY);
      const step = n.step || STEP_CITY * (scale / SCALE_CITY);
      const stepDist = speed * dt;
      n.x += n.dir * stepDist;
      n.dist = (n.dist || 0) + stepDist;
      n.fr = computeWalkFrame(n.dist, step, false);
      if ((n.dir > 0 && n.x >= n.max) || (n.dir < 0 && n.x <= n.min)) {
        n.x = Math.max(n.min, Math.min(n.max, n.x));
        n.wait = 2.2;
      }
    }
    if (n.el && n.spEl) {
      n.el.style.left = `${(n.x - halfW).toFixed(1)}px`;
      n.spEl.style.backgroundPosition = `-${n.fr * STRIP_CELL_W}px 0`;
      n.spEl.style.transform = `scale(${(n.dir * scale).toFixed(3)}, ${scale.toFixed(3)})`;
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
