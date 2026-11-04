namespace SimT.Contracts.Modules;

/// <summary>
/// Declarative module descriptor, read from the <c>module.json</c> embedded in the module
/// assembly. This is what makes a DLC installable: drop the DLL, add its id to the product
/// manifest, and the kernel wires it.
/// </summary>
public sealed record ModuleManifest(
    string Id,
    string Version,
    string ApiVersion,
    string Kind,
    IReadOnlyList<string> Requires,
    IReadOnlyList<string> Provides,
    IReadOnlyList<string> Consumes,
    ModuleOwnership Owns,
    IReadOnlyList<string> Screens,
    IReadOnlyDictionary<string, string>? Config = null)
{
    public const string CurrentApiVersion = "1";

    /// <summary>Flat scalar settings from <c>module.json</c>, defaults supplied by the module.</summary>
    public ModuleConfig Settings { get; } = new(Config, Id);
}

/// <summary>Tables and migration folders a module is allowed to touch.</summary>
public sealed record ModuleOwnership(IReadOnlyList<string> Tables, IReadOnlyList<string> MigrationFolders);

/// <summary>A module's entry point. Registration happens in <see cref="Start"/>.</summary>
public interface ISimModule
{
    void Start(ModuleContext context);
}

/// <summary>Everything a module is allowed to reach, injected per module.</summary>
public sealed class ModuleContext
{
    private readonly List<Diagnostics.IInvariant> _invariants = new();

    public ModuleContext(
        ModuleManifest manifest,
        CampaignStart campaign,
        Data.IDbSession db,
        Time.IGameClock clock,
        Ledger.ILedger ledger,
        Identity.IIdGenerator ids,
        Identity.IRng rng,
        Events.IEventPublisher events,
        Time.IActionTime actionTime,
        ICapabilityBroker capabilities,
        Config.GameConstants constants,
        Logging.ILog log,
        Ui.IUiRegistry ui)
    {
        Manifest = manifest;
        Campaign = campaign;
        Db = db;
        Clock = clock;
        Ledger = ledger;
        Ids = ids;
        Rng = rng;
        Events = events;
        ActionTime = actionTime;
        Capabilities = capabilities;
        Constants = constants;
        Log = log;
        Ui = ui;
    }

    public ModuleManifest Manifest { get; }

    public CampaignStart Campaign { get; }

    public Data.IDbSession Db { get; }

    public Time.IGameClock Clock { get; }

    public Ledger.ILedger Ledger { get; }

    public Identity.IIdGenerator Ids { get; }

    public Identity.IRng Rng { get; }

    public Events.IEventPublisher Events { get; }

    public Time.IActionTime ActionTime { get; }

    public ICapabilityBroker Capabilities { get; }

    public Config.GameConstants Constants { get; }

    public Logging.ILog Log { get; }

    public Ui.IUiRegistry Ui { get; }

    /// <summary>Typed settings this module declared in its manifest.</summary>
    public ModuleConfig Config => Manifest.Settings;

    public IReadOnlyList<Diagnostics.IInvariant> Invariants => _invariants;

    /// <summary>Registers a consistency rule the kernel runs after each scenario or on demand.</summary>
    public void AddInvariant(Diagnostics.IInvariant invariant)
    {
        if (_invariants.Any(existing => string.Equals(existing.Id, invariant.Id, StringComparison.Ordinal)))
        {
            throw new SimException("invariant.duplicate", $"Invariant '{invariant.Id}' is registered twice by '{Manifest.Id}'.");
        }

        _invariants.Add(invariant);
    }
}
