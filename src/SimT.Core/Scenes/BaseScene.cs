using SimT.Contracts.Scenes;

namespace SimT.Core.Scenes;

/// <summary>
/// Abstract foundational implementation of a game scene with standardized lifecycle guards.
/// </summary>
public abstract class BaseScene : IScene
{
    private SceneLifecycleState _state = SceneLifecycleState.Inactive;
    protected SceneRuntimeContext? Context { get; private set; }

    public abstract string SceneId { get; }
    public abstract string Title { get; }

    public SceneLifecycleState State => _state;

    public virtual void OnEnter(SceneRuntimeContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _state = SceneLifecycleState.Active;
    }

    public virtual void OnUpdate(double deltaTimeSeconds)
    {
    }

    public virtual void OnSuspend()
    {
        _state = SceneLifecycleState.Suspended;
    }

    public virtual void OnResume()
    {
        _state = SceneLifecycleState.Active;
    }

    public virtual void OnExit()
    {
        _state = SceneLifecycleState.Inactive;
        Context = null;
    }

    public virtual void Dispose()
    {
        if (_state != SceneLifecycleState.Inactive)
        {
            OnExit();
        }
        GC.SuppressFinalize(this);
    }
}
