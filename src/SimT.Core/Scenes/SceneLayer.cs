using SimT.Contracts.Objects;
using SimT.Contracts.Scenes;

namespace SimT.Core.Scenes;

/// <summary>
/// Sovereign implementation of <see cref="ISceneLayer"/>.
/// Holds a collection of game objects and exposes execution flags.
/// </summary>
public sealed class SceneLayer : ISceneLayer
{
    private readonly List<GameObject> _gameObjects = new();

    public string Name { get; }
    public int ZIndex { get; set; }
    public bool Visible { get; set; } = true;
    public float Opacity { get; set; } = 1f;
    public bool CameraBinding { get; set; } = true;
    public bool InputEnabled { get; set; } = true;
    public bool UpdateEnabled { get; set; } = true;
    public bool RenderEnabled { get; set; } = true;

    public IReadOnlyList<GameObject> GameObjects => _gameObjects;

    public SceneLayer(string name, int zIndex = 0, bool cameraBinding = true)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ZIndex = zIndex;
        CameraBinding = cameraBinding;
    }

    public void AddGameObject(GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        if (!_gameObjects.Contains(gameObject))
        {
            _gameObjects.Add(gameObject);
        }
    }

    public bool RemoveGameObject(GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        return _gameObjects.Remove(gameObject);
    }

    public void Clear()
    {
        _gameObjects.Clear();
    }

    public void Update(double deltaTime)
    {
        if (!UpdateEnabled) return;

        for (var i = 0; i < _gameObjects.Count; i++)
        {
            var go = _gameObjects[i];
            if (go.Active)
            {
                go.Update(deltaTime);
            }
        }
    }
}

/// <summary>
/// Sovereign implementation of <see cref="ILayerManager"/>.
/// Maintains sorted order of layers and provides default Three Kingdoms layer templates.
/// </summary>
public sealed class LayerManager : ILayerManager
{
    private readonly List<ISceneLayer> _layers = new();

    public IReadOnlyList<ISceneLayer> Layers => _layers;

    public static LayerManager CreateDefaultThreeKingdomsSetup()
    {
        var manager = new LayerManager();
        manager.AddLayer(new SceneLayer("BackgroundLayer", zIndex: 0, cameraBinding: true));
        manager.AddLayer(new SceneLayer("MapLayer", zIndex: 10, cameraBinding: true));
        manager.AddLayer(new SceneLayer("BuildingLayer", zIndex: 20, cameraBinding: true));
        manager.AddLayer(new SceneLayer("CharacterLayer", zIndex: 30, cameraBinding: true));
        manager.AddLayer(new SceneLayer("EffectLayer", zIndex: 40, cameraBinding: true) { InputEnabled = false });
        manager.AddLayer(new SceneLayer("ScreenHudLayer", zIndex: 100, cameraBinding: false));
        return manager;
    }

    public ISceneLayer? GetLayer(string name)
    {
        for (var i = 0; i < _layers.Count; i++)
        {
            if (string.Equals(_layers[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return _layers[i];
            }
        }
        return null;
    }

    public void AddLayer(ISceneLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        _layers.Add(layer);
        _layers.Sort((a, b) => a.ZIndex.CompareTo(b.ZIndex));
    }

    public bool RemoveLayer(string name)
    {
        for (var i = 0; i < _layers.Count; i++)
        {
            if (string.Equals(_layers[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                _layers.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public void Update(double deltaTime)
    {
        for (var i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            if (layer.UpdateEnabled)
            {
                layer.Update(deltaTime);
            }
        }
    }
}
