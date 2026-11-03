namespace SimT.Contracts.Events;

/// <summary>
/// In-process publisher owned by the kernel (decision K6a). Subscribers are registered
/// explicitly through the module context; there is no assembly scanning for handlers.
/// </summary>
public interface IEventPublisher
{
    void Publish<T>(T evt);

    IDisposable Subscribe<T>(Action<T> handler);
}
