using SimT.Contracts.Localization;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Time;

namespace SimT.Contracts.Scenes;

/// <summary>
/// Transition visual animations between scenes.
/// </summary>
public enum SceneTransitionType
{
    None = 0,
    Fade = 1,
    CrossFade = 2,
    SlideLeft = 3,
    SlideRight = 4
}

/// <summary>
/// Lifecycle state of an active or suspended scene.
/// </summary>
public enum SceneLifecycleState
{
    Inactive = 0,
    Entering = 1,
    Active = 2,
    Suspended = 3,
    Exiting = 4
}

/// <summary>
/// Runtime context provided to a scene during lifecycle transitions.
/// </summary>
public sealed record SceneRuntimeContext(
    ICapabilityBroker Capabilities,
    IGameClock Clock,
    ILog Log,
    ILocalizationService Localization,
    Action<string, IReadOnlyDictionary<string, string>?> ExecuteCommand,
    Action<string> NavigateScreen);

/// <summary>
/// Core contract representing a distinct self-contained game stage/scene
/// (e.g. City Hub, World Map, Tactical Duel, Dialogue, Banquet).
/// </summary>
public interface IScene : IDisposable
{
    string SceneId { get; }
    string Title { get; }
    SceneLifecycleState State { get; }

    void OnEnter(SceneRuntimeContext context);
    void OnUpdate(double deltaTimeSeconds);
    void OnSuspend();
    void OnResume();
    void OnExit();
}

/// <summary>
/// Optional contract for scenes supporting hardware-accelerated 2D canvas rendering.
/// </summary>
public interface IScene2DRenderer
{
    void RenderBackground(object drawingContext, double width, double height);
    void RenderEntities(object drawingContext, double width, double height);
    void RenderWeatherFx(object drawingContext, double width, double height);
    void RenderForeground(object drawingContext, double width, double height);
}

/// <summary>
/// Central Scene Management State Machine.
/// Coordinates scene stack, transitions, 60 FPS update loops, and view lifecycle.
/// </summary>
public interface ISceneManager
{
    IScene? CurrentScene { get; }
    IReadOnlyList<IScene> SceneStack { get; }
    bool IsInTransition { get; }
    double TransitionProgress { get; }

    void ChangeScene(IScene scene, SceneTransitionType transition = SceneTransitionType.Fade);
    void PushScene(IScene scene, SceneTransitionType transition = SceneTransitionType.Fade);
    bool PopScene(SceneTransitionType transition = SceneTransitionType.Fade);

    void Update(double deltaTimeSeconds);

    event Action<IScene?, IScene, SceneTransitionType>? SceneChanged;
    event Action<double>? TransitionProgressChanged;
}
