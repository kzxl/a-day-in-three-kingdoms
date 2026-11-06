using System.Collections.Concurrent;
using SimT.Contracts.Assets;

namespace SimT.Core.Assets;

/// <summary>
/// Sovereign Reference-Counted Asset Manager.
/// Prevents redundant loads: 100 sprites using the same texture share a single resource instance.
/// </summary>
public sealed class AssetManager : IAssetManager
{
    private sealed class ResourceEntry
    {
        public readonly object Resource;
        public readonly string AssetPath;
        private int _refCount;

        public int RefCount => Volatile.Read(ref _refCount);

        public ResourceEntry(string assetPath, object resource)
        {
            AssetPath = assetPath;
            Resource = resource;
            _refCount = 0;
        }

        public int AddRef() => Interlocked.Increment(ref _refCount);

        public int Release() => Interlocked.Decrement(ref _refCount);
    }

    private sealed class ResourceHandleImpl<T> : IResourceHandle<T> where T : class
    {
        private readonly ResourceEntry _entry;
        private readonly AssetManager _owner;
        private int _disposed;

        public string AssetPath => _entry.AssetPath;
        public T Resource => (T)_entry.Resource;
        public int RefCount => _entry.RefCount;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public ResourceHandleImpl(ResourceEntry entry, AssetManager owner)
        {
            _entry = entry;
            _owner = owner;
            _entry.AddRef();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _entry.Release();
            }
        }
    }

    private readonly ConcurrentDictionary<string, ResourceEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public int CachedResourceCount => _cache.Count;

    public IResourceHandle<T> Load<T>(string relativePath, Func<string, T> loader) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(loader);

        var entry = _cache.GetOrAdd(relativePath, path =>
        {
            var res = loader(path);
            return new ResourceEntry(path, res);
        });

        return new ResourceHandleImpl<T>(entry, this);
    }

    public async Task<IResourceHandle<T>> LoadAsync<T>(
        string relativePath,
        Func<string, CancellationToken, Task<T>> loader,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(loader);

        if (_cache.TryGetValue(relativePath, out var existing))
        {
            return new ResourceHandleImpl<T>(existing, this);
        }

        var res = await loader(relativePath, cancellationToken).ConfigureAwait(false);
        var entry = _cache.GetOrAdd(relativePath, path => new ResourceEntry(path, res));

        return new ResourceHandleImpl<T>(entry, this);
    }

    public int UnloadUnused()
    {
        var evicted = 0;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.RefCount <= 0)
            {
                if (_cache.TryRemove(kvp.Key, out var removed))
                {
                    if (removed.Resource is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    evicted++;
                }
            }
        }
        return evicted;
    }

    public void Clear()
    {
        foreach (var kvp in _cache)
        {
            if (kvp.Value.Resource is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _cache.Clear();
    }
}
