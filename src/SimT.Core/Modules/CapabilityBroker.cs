using SimT.Contracts;
using SimT.Contracts.Modules;

namespace SimT.Core.Modules;

/// <summary>
/// K5 — capability exchange. Duplicate providers are a hard error: two modules claiming the
/// same contract is exactly the "two writers" bug the architecture exists to prevent.
/// </summary>
public sealed class CapabilityBroker : ICapabilityBroker
{
    private readonly Dictionary<string, object> _providers = new(StringComparer.Ordinal);

    public void Provide(string capabilityId, object provider)
    {
        if (!_providers.TryAdd(capabilityId, provider))
        {
            throw new SimException("capability.duplicate", $"Capability '{capabilityId}' is already provided by {_providers[capabilityId].GetType().Name}.");
        }
    }

    public bool IsProvided(string capabilityId) => _providers.ContainsKey(capabilityId);

    public T Get<T>(string capabilityId)
        where T : class
    {
        if (!_providers.TryGetValue(capabilityId, out var provider))
        {
            throw new MissingCapabilityException(capabilityId);
        }

        return provider as T
               ?? throw new SimException(
                   "capability.type_mismatch",
                   $"Capability '{capabilityId}' is provided as {provider.GetType().Name}, not {typeof(T).Name}.");
    }

    public bool TryGet<T>(string capabilityId, out T? provider)
        where T : class
    {
        if (_providers.TryGetValue(capabilityId, out var value) && value is T typed)
        {
            provider = typed;
            return true;
        }

        provider = null;
        return false;
    }

    public ICapabilityBroker CreateScope(ModuleManifest manifest) =>
        new ModuleScopedCapabilityBroker(this, manifest);

    public IReadOnlyCollection<string> Provided => _providers.Keys;
}
