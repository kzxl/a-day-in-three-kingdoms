const stageHandlers = {
  talk: null,
  duel: null,
  city: null,
  map: null,
  social: null,
  market: null,
  dossier: null,
  event: null
};

let activeKind = 'none';
let activeSceneId = null;
let activeSceneData = null;

function postToHost(message) {
  if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
    window.chrome.webview.postMessage(message);
  }
}

export const SimTBridge = {
  registerStage(kind, handler) {
    stageHandlers[kind] = handler;
    if (activeKind === kind && activeSceneData) {
      showStageSection(kind, activeSceneData);
    }
  },

  sendChoice(sceneId, id) {
    postToHost({ type: 'choice', sceneId: sceneId || activeSceneId, id: String(id) });
  },

  sendNavigate(sceneId, id) {
    postToHost({ type: 'navigate', sceneId: sceneId || activeSceneId, id: String(id) });
  },

  sendClose(sceneId) {
    postToHost({ type: 'close', sceneId: sceneId || activeSceneId });
  },

  getActiveKind() {
    return activeKind;
  }
};

window.SimTBridge = SimTBridge;

function showStageSection(kind, scene) {
  const root = document.getElementById('scene-root');
  if (root) {
    root.dataset.active = kind;
  }

  for (const key of ['talk', 'duel', 'city', 'map', 'social', 'market', 'dossier', 'event']) {
    const el = document.getElementById(`stage-${key}`);
    if (!el) continue;
    if (key === kind) {
      el.hidden = false;
    } else {
      el.hidden = true;
      if (stageHandlers[key] && typeof stageHandlers[key].unmount === 'function') {
        stageHandlers[key].unmount();
      }
    }
  }

  activeKind = kind;
  activeSceneData = scene;
  activeSceneId = scene ? scene.sceneId || kind : null;

  if (kind !== 'none' && stageHandlers[kind] && typeof stageHandlers[kind].render === 'function') {
    const container = document.getElementById(`stage-${kind}`);
    stageHandlers[kind].render(container, scene, SimTBridge);
    const paintedSceneId = activeSceneId;
    const notifyPainted = () => {
      if (activeKind === kind && activeSceneId === paintedSceneId) {
        postToHost({ type: 'painted', sceneId: paintedSceneId });
      }
    };
    if (typeof requestAnimationFrame === 'function') {
      requestAnimationFrame(() => {
        requestAnimationFrame(notifyPainted);
      });
    } else {
      notifyPainted();
    }
  }
}

function handleHostEnvelope(raw) {
  if (!raw) return;
  let data = raw;
  if (typeof raw === 'string') {
    try {
      data = JSON.parse(raw);
    } catch {
      return;
    }
  }
  if (!data || typeof data.type !== 'string') return;

  switch (data.type) {
    case 'scene:talk':
      showStageSection('talk', data.scene);
      break;
    case 'scene:duel':
      showStageSection('duel', data.scene);
      break;
    case 'scene:city':
      showStageSection('city', data.scene);
      break;
    case 'scene:map':
      showStageSection('map', data.scene);
      break;
    case 'scene:social':
      showStageSection('social', data.scene);
      break;
    case 'scene:market':
      showStageSection('market', data.scene);
      break;
    case 'scene:dossier':
      showStageSection('dossier', data.scene);
      break;
    case 'scene:event':
      showStageSection('event', data.scene);
      break;
    case 'scene:hide':
      showStageSection('none', null);
      break;
  }
}

if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener('message', (event) => {
    handleHostEnvelope(event.data);
  });
}

window.addEventListener('message', (event) => {
  handleHostEnvelope(event.data);
});

window.addEventListener('keydown', (event) => {
  if (event.key === 'Escape' && activeKind !== 'none' && activeKind !== 'city') {
    event.preventDefault();
    SimTBridge.sendClose(activeSceneId);
  }
});

window.addEventListener('DOMContentLoaded', () => {
  postToHost({ type: 'ready' });
});
