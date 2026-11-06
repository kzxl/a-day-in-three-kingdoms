using SimT.Contracts.Scenes;

namespace SimT.Core.Scenes;

/// <summary>
/// Sovereign Scene Manager & State Machine.
/// Orchestrates scene transitions, hierarchical scene stack (e.g. Dialogue on top of City),
/// and continuous update ticks.
/// </summary>
public sealed class SceneManager : ISceneManager
{
    private readonly List<IScene> _sceneStack = new();
    private readonly SceneRuntimeContext _context;
    private IScene? _pendingScene;
    private SceneTransitionType _currentTransition = SceneTransitionType.None;
    private double _transitionTimer;
    private double _transitionDuration = 0.3; // Default 300ms transition
    private bool _isTransitioning;

    public IScene? CurrentScene => _sceneStack.Count > 0 ? _sceneStack[^1] : null;

    public IReadOnlyList<IScene> SceneStack => _sceneStack;

    public bool IsInTransition => _isTransitioning;

    public double TransitionProgress { get; private set; } = 1.0;

    public event Action<IScene?, IScene, SceneTransitionType>? SceneChanged;
    public event Action<double>? TransitionProgressChanged;

    public SceneManager(SceneRuntimeContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void ChangeScene(IScene scene, SceneTransitionType transition = SceneTransitionType.Fade)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var prev = CurrentScene;
        if (transition == SceneTransitionType.None)
        {
            ApplySceneChange(scene);
            SceneChanged?.Invoke(prev, scene, transition);
            return;
        }

        _pendingScene = scene;
        _currentTransition = transition;
        _isTransitioning = true;
        _transitionTimer = 0.0;
        TransitionProgress = 0.0;
        TransitionProgressChanged?.Invoke(0.0);
    }

    public void PushScene(IScene scene, SceneTransitionType transition = SceneTransitionType.Fade)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var prev = CurrentScene;
        prev?.OnSuspend();

        _sceneStack.Add(scene);
        scene.OnEnter(_context);

        SceneChanged?.Invoke(prev, scene, transition);
    }

    public bool PopScene(SceneTransitionType transition = SceneTransitionType.Fade)
    {
        if (_sceneStack.Count <= 1)
        {
            return false;
        }

        var popped = _sceneStack[^1];
        _sceneStack.RemoveAt(_sceneStack.Count - 1);
        popped.OnExit();
        popped.Dispose();

        var resumed = CurrentScene;
        resumed?.OnResume();

        if (resumed != null)
        {
            SceneChanged?.Invoke(popped, resumed, transition);
        }

        return true;
    }

    public void Update(double deltaTimeSeconds)
    {
        if (_isTransitioning)
        {
            _transitionTimer += deltaTimeSeconds;
            TransitionProgress = Math.Clamp(_transitionTimer / _transitionDuration, 0.0, 1.0);
            TransitionProgressChanged?.Invoke(TransitionProgress);

            if (TransitionProgress >= 0.5 && _pendingScene != null)
            {
                var target = _pendingScene;
                _pendingScene = null;
                var prev = CurrentScene;
                ApplySceneChange(target);
                SceneChanged?.Invoke(prev, target, _currentTransition);
            }

            if (_transitionTimer >= _transitionDuration)
            {
                _isTransitioning = false;
                TransitionProgress = 1.0;
                TransitionProgressChanged?.Invoke(1.0);
            }
        }

        CurrentScene?.OnUpdate(deltaTimeSeconds);
    }

    private void ApplySceneChange(IScene nextScene)
    {
        while (_sceneStack.Count > 0)
        {
            var top = _sceneStack[^1];
            _sceneStack.RemoveAt(_sceneStack.Count - 1);
            top.OnExit();
            top.Dispose();
        }

        _sceneStack.Add(nextScene);
        nextScene.OnEnter(_context);
    }
}
