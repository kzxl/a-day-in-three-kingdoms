using SimT.Contracts.Modules;

namespace SimT.Core.Modules;

/// <summary>
/// Enforces module boundaries at runtime: a module may only provide capabilities declared
/// in its manifest's <c>provides</c>, and may only consume capabilities declared in its
/// <c>consumes</c> (or provided by itself).
/// </summary>
public sealed class ModuleScopedCapabilityBroker : ICapabilityBroker
{
    private readonly ICapabilityBroker _root;
    private readonly ModuleManifest _manifest;
    private readonly HashSet<string> _allowedToConsume;
    private readonly HashSet<string> _allowedToProvide;

    public ModuleScopedCapabilityBroker(ICapabilityBroker root, ModuleManifest manifest)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));

        _allowedToProvide = new HashSet<string>(manifest.Provides, StringComparer.Ordinal);
        _allowedToConsume = new HashSet<string>(manifest.Consumes, StringComparer.Ordinal);

        // A module is always permitted to consume capabilities it provides itself.
        foreach (var provided in manifest.Provides)
        {
            _allowedToConsume.Add(provided);
        }
    }

    public void Provide(string capabilityId, object provider)
    {
        if (!_allowedToProvide.Contains(capabilityId))
        {
            throw new CapabilityAccessDeniedException(_manifest.Id, capabilityId, "provide");
        }

        _root.Provide(capabilityId, provider);
    }

    public bool IsProvided(string capabilityId)
    {
        if (!_allowedToConsume.Contains(capabilityId))
        {
            throw new CapabilityAccessDeniedException(_manifest.Id, capabilityId, "consume");
        }

        return _root.IsProvided(capabilityId);
    }

    public T Get<T>(string capabilityId)
        where T : class
    {
        if (!_allowedToConsume.Contains(capabilityId))
        {
            throw new CapabilityAccessDeniedException(_manifest.Id, capabilityId, "consume");
        }

        return _root.Get<T>(capabilityId);
    }

    public bool TryGet<T>(string capabilityId, out T? provider)
        where T : class
    {
        if (!_allowedToConsume.Contains(capabilityId))
        {
            throw new CapabilityAccessDeniedException(_manifest.Id, capabilityId, "consume");
        }

        return _root.TryGet<T>(capabilityId, out provider);
    }

    public ICapabilityBroker CreateScope(ModuleManifest manifest) => _root.CreateScope(manifest);
}
