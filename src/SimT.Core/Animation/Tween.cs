using SimT.Contracts.Primitives;

namespace SimT.Core.Animation;

/// <summary>
/// Sovereign lightweight tweening handle for interpolating values smoothly over time.
/// </summary>
public interface ITweenHandle : IDisposable
{
    bool IsCompleted { get; }
    bool IsCancelled { get; }
    void Cancel();
}

/// <summary>
/// Sovereign zero-allocation Tween engine for smooth property transitions.
/// </summary>
public sealed class TweenRunner
{
    private abstract class TweenNode : ITweenHandle
    {
        public readonly double Duration;
        public double Elapsed;
        public Func<float, float> EaseFunc = Easing.EaseOutQuad;
        public Action? OnCompleted;
        public bool IsCompleted { get; private set; }
        public bool IsCancelled { get; private set; }

        protected TweenNode(double duration)
        {
            Duration = Math.Max(0.0001, duration);
        }

        public abstract void Step(float t);

        public void Update(double deltaTime)
        {
            if (IsCancelled || IsCompleted) return;

            Elapsed += deltaTime;
            var progress = (float)Math.Clamp(Elapsed / Duration, 0.0, 1.0);
            var easedProgress = EaseFunc(progress);

            Step(easedProgress);

            if (Elapsed >= Duration)
            {
                IsCompleted = true;
                OnCompleted?.Invoke();
            }
        }

        public void Cancel()
        {
            IsCancelled = true;
        }

        public void Dispose() => Cancel();
    }

    private sealed class FloatTween : TweenNode
    {
        private readonly float _from;
        private readonly float _to;
        private readonly Action<float> _setter;

        public FloatTween(float from, float to, double duration, Action<float> setter) : base(duration)
        {
            _from = from;
            _to = to;
            _setter = setter;
        }

        public override void Step(float t)
        {
            var value = _from + (_to - _from) * t;
            _setter(value);
        }
    }

    private sealed class VectorPoint2DTween : TweenNode
    {
        private readonly VectorPoint2D _from;
        private readonly VectorPoint2D _to;
        private readonly Action<VectorPoint2D> _setter;

        public VectorPoint2DTween(VectorPoint2D from, VectorPoint2D to, double duration, Action<VectorPoint2D> setter) : base(duration)
        {
            _from = from;
            _to = to;
            _setter = setter;
        }

        public override void Step(float t)
        {
            var x = _from.X + (_to.X - _from.X) * t;
            var y = _from.Y + (_to.Y - _from.Y) * t;
            _setter(new VectorPoint2D(x, y));
        }
    }

    private readonly List<TweenNode> _tweens = new();
    private readonly List<TweenNode> _pendingAdd = new();
    private bool _isUpdating;

    public ITweenHandle AnimateFloat(
        float from,
        float to,
        double duration,
        Action<float> setter,
        Func<float, float>? ease = null,
        Action? onComplete = null)
    {
        ArgumentNullException.ThrowIfNull(setter);
        var node = new FloatTween(from, to, duration, setter)
        {
            EaseFunc = ease ?? Easing.EaseOutQuad,
            OnCompleted = onComplete
        };

        if (_isUpdating) _pendingAdd.Add(node);
        else _tweens.Add(node);

        return node;
    }

    public ITweenHandle AnimatePosition(
        VectorPoint2D from,
        VectorPoint2D to,
        double duration,
        Action<VectorPoint2D> setter,
        Func<float, float>? ease = null,
        Action? onComplete = null)
    {
        ArgumentNullException.ThrowIfNull(setter);
        var node = new VectorPoint2DTween(from, to, duration, setter)
        {
            EaseFunc = ease ?? Easing.EaseOutQuad,
            OnCompleted = onComplete
        };

        if (_isUpdating) _pendingAdd.Add(node);
        else _tweens.Add(node);

        return node;
    }

    public void Update(double deltaTimeSeconds)
    {
        if (deltaTimeSeconds <= 0 || (_tweens.Count == 0 && _pendingAdd.Count == 0)) return;

        _isUpdating = true;
        try
        {
            for (var i = 0; i < _tweens.Count; i++)
            {
                var tween = _tweens[i];
                if (tween.IsCancelled || tween.IsCompleted) continue;
                tween.Update(deltaTimeSeconds);
            }
        }
        finally
        {
            _isUpdating = false;
            _tweens.RemoveAll(t => t.IsCancelled || t.IsCompleted);

            if (_pendingAdd.Count > 0)
            {
                _tweens.AddRange(_pendingAdd);
                _pendingAdd.Clear();
            }
        }
    }

    public void CancelAll()
    {
        foreach (var tween in _tweens) tween.Cancel();
        _tweens.Clear();
        foreach (var tween in _pendingAdd) tween.Cancel();
        _pendingAdd.Clear();
    }
}
