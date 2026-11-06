namespace SimT.Contracts.Assets;

/// <summary>
/// Smart resource handle providing reference-counted access to cached assets.
/// Automatically releases its reference on disposal.
/// </summary>
public interface IResourceHandle<out T> : IDisposable where T : class
{
    string AssetPath { get; }
    T Resource { get; }
    int RefCount { get; }
    bool IsDisposed { get; }
}

/// <summary>
/// Centralized asset management service providing reference counting,
/// caching, and unified synchronous/asynchronous asset loading.
/// </summary>
public interface IAssetManager
{
    int CachedResourceCount { get; }

    IResourceHandle<T> Load<T>(string relativePath, Func<string, T> loader) where T : class;

    Task<IResourceHandle<T>> LoadAsync<T>(
        string relativePath,
        Func<string, CancellationToken, Task<T>> loader,
        CancellationToken cancellationToken = default) where T : class;

    int UnloadUnused();

    void Clear();
}
