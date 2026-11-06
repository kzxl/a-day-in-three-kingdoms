using SimT.Contracts.Objects;

namespace SimT.Contracts.Scenes;

/// <summary>
/// A scene layer organizing game objects, rendering priority, and viewport camera bindings.
/// </summary>
public interface ISceneLayer
{
    string Name { get; }
    int ZIndex { get; set; }
    bool Visible { get; set; }
    float Opacity { get; set; }

    /// <summary>
    /// When true, items are rendered in World Space and transformed by Camera.
    /// When false, items are rendered in Screen Space (HUD, overlays).
    /// </summary>
    bool CameraBinding { get; set; }

    bool InputEnabled { get; set; }
    bool UpdateEnabled { get; set; }
    bool RenderEnabled { get; set; }

    IReadOnlyList<GameObject> GameObjects { get; }

    void AddGameObject(GameObject gameObject);
    bool RemoveGameObject(GameObject gameObject);
    void Clear();

    void Update(double deltaTime);
}

/// <summary>
/// Manages the hierarchy of scene layers, keeping them sorted by ZIndex.
/// </summary>
public interface ILayerManager
{
    IReadOnlyList<ISceneLayer> Layers { get; }

    ISceneLayer? GetLayer(string name);
    void AddLayer(ISceneLayer layer);
    bool RemoveLayer(string name);

    void Update(double deltaTime);
}
