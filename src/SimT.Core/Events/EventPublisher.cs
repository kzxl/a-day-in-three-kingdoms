using SimT.Contracts.Events;

namespace SimT.Core.Events;

/// <summary>
/// K6a — in-process publisher with explicit subscription. No assembly scanning, no statics:
/// the kernel owns the instance and passes it through the module context.
/// </summary>
public sealed class EventPublisher : IEventPublisher
{
    private readonly Dictionary<Type, List<object>> _handlers = new();

    public void Publish<T>(T evt)
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers) || handlers.Count == 0)
        {
            return;
        }

        // Copy so a handler may subscribe/unsubscribe while a publication is in flight.
        var snapshot = handlers.ToArray();
        foreach (var handler in snapshot)
        {
            ((Action<T>)handler)(evt);
        }
    }

    public IDisposable Subscribe<T>(Action<T> handler)
    {
        if (!_handlers.TryGetValue(typeof(T), out var handlers))
        {
            handlers = new List<object>();
            _handlers[typeof(T)] = handlers;
        }

        handlers.Add(handler);
        return new Subscription(() => handlers.Remove(handler));
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            var action = _dispose;
            _dispose = null;
            action?.Invoke();
        }
    }
}
