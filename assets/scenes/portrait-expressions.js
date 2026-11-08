// A small shared portrait swapper for talk lines and historical narrative pages.
export function expressionForLine(scene, index) {
  const value = scene?.lineExpressions?.[index];
  return ['happy', 'angry', 'sad'].includes(value) ? value : 'normal';
}

export function portraitForLine(scene, index, normalPath) {
  return scene?.expressionPaths?.[expressionForLine(scene, index)] || normalPath;
}

export function createPortraitSwapper(baseEl) {
  let generation = 0;
  let overlay = null;
  let timer = null;
  let disposed = false;
  const clear = () => {
    if (timer) clearTimeout(timer);
    timer = null;
    overlay?.remove();
    overlay = null;
  };
  return {
    set(path, fallback = path) {
      const token = ++generation;
      if (!baseEl || disposed) return;
      clear();
      if (baseEl.getAttribute('src') === path) return;
      const loader = new Image();
      loader.onload = () => {
        if (disposed || token !== generation) return;
        const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true;
        if (!reduced) {
          overlay = baseEl.cloneNode(false);
          overlay.removeAttribute('id');
          overlay.setAttribute('alt', '');
          overlay.setAttribute('aria-hidden', 'true');
          overlay.classList.add('tk-portrait-outgoing');
          overlay.style.opacity = '1';
          baseEl.parentElement.appendChild(overlay);
          timer = setTimeout(clear, 160);
        }
        baseEl.src = path;
      };
      loader.onerror = () => {
        if (!disposed && token === generation) baseEl.src = fallback;
      };
      loader.src = path;
    },
    dispose() { disposed = true; generation++; clear(); }
  };
}
