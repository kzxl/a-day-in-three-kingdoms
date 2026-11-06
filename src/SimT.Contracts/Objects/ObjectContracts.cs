using SimT.Contracts.Primitives;

namespace SimT.Contracts.Objects;

/// <summary>
/// Lifecycle interface for components attached to a <see cref="GameObject"/>.
/// </summary>
public interface IComponent
{
    GameObject? GameObject { get; }
    bool Enabled { get; set; }
    void OnAttach(GameObject owner);
    void OnDetach();
    void Update(double deltaTime);
}

/// <summary>
/// Base class providing default empty lifecycle hooks for components.
/// </summary>
public abstract class ComponentBase : IComponent
{
    public GameObject? GameObject { get; private set; }
    public bool Enabled { get; set; } = true;

    public virtual void OnAttach(GameObject owner)
    {
        GameObject = owner;
    }

    public virtual void OnDetach()
    {
        GameObject = null;
    }

    public virtual void Update(double deltaTime)
    {
    }
}

/// <summary>
/// Sovereign Game Object container.
/// Holds modular components, spatial transform, and parent-child hierarchy.
/// </summary>
public sealed class GameObject
{
    private readonly List<IComponent> _components = new();
    private readonly List<GameObject> _children = new();
    private Transform2D _transform;

    public string Id { get; }
    public string Name { get; set; }
    public string Tag { get; set; } = "Untagged";
    public bool Active { get; set; } = true;

    public Transform2D Transform => _transform;

    public GameObject? Parent { get; private set; }
    public IReadOnlyList<GameObject> Children => _children;

    public GameObject(string name, string? id = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Id = id ?? Guid.NewGuid().ToString("N");

        _transform = new Transform2D();
        AddComponent(_transform);
    }

    public T AddComponent<T>(T component) where T : class, IComponent
    {
        ArgumentNullException.ThrowIfNull(component);
        _components.Add(component);
        component.OnAttach(this);
        return component;
    }

    public T AddComponent<T>() where T : class, IComponent, new()
    {
        return AddComponent(new T());
    }

    public T? GetComponent<T>() where T : class
    {
        for (var i = 0; i < _components.Count; i++)
        {
            if (_components[i] is T match) return match;
        }
        return null;
    }

    public bool TryGetComponent<T>(out T? component) where T : class
    {
        component = GetComponent<T>();
        return component != null;
    }

    public bool RemoveComponent<T>() where T : class
    {
        for (var i = 0; i < _components.Count; i++)
        {
            if (_components[i] is T match)
            {
                _components.RemoveAt(i);
                if (match is IComponent comp) comp.OnDetach();
                return true;
            }
        }
        return false;
    }

    public IReadOnlyList<IComponent> GetAllComponents() => _components;

    public void AddChild(GameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child == this) throw new InvalidOperationException("Cannot add self as child.");

        child.Parent?._children.Remove(child);
        child.Parent = this;
        _children.Add(child);
    }

    public bool RemoveChild(GameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (_children.Remove(child))
        {
            child.Parent = null;
            return true;
        }
        return false;
    }

    public void Update(double deltaTime)
    {
        if (!Active) return;

        for (var i = 0; i < _components.Count; i++)
        {
            var comp = _components[i];
            if (comp.Enabled) comp.Update(deltaTime);
        }

        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].Update(deltaTime);
        }
    }
}
