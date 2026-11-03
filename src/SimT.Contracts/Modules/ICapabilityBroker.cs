namespace SimT.Contracts.Modules;

/// <summary>
/// Runtime capability exchange. A DLC never references another module's assembly; it asks
/// the broker for a contract interface declared in <c>SimT.Contracts</c> (or in its own
/// contracts file) and the providing module answers.
/// </summary>
public interface ICapabilityBroker
{
    void Provide(string capabilityId, object provider);

    bool IsProvided(string capabilityId);

    T Get<T>(string capabilityId)
        where T : class;

    bool TryGet<T>(string capabilityId, out T? provider)
        where T : class;
}

/// <summary>Raised when a module asks for a capability nobody provides (or was disabled).</summary>
public sealed class MissingCapabilityException : SimException
{
    public MissingCapabilityException(string capabilityId)
        : base("capability.missing", $"Capability '{capabilityId}' is not provided by any enabled module.")
    {
        CapabilityId = capabilityId;
    }

    public string CapabilityId { get; }
}
