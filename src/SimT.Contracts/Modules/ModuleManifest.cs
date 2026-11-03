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
    IReadOnlyList<string> Screens)
{
    public const string CurrentApiVersion = "1";
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
    public ModuleContext(
        ModuleManifest manifest,
        CampaignStart campaign,
        Data.IDbSession db,
        Time.IGameClock clock,
        Ledger.ILedger ledger,
        Identity.IIdGenerator ids,
        Identity.IRng rng,
        Events.IEventPublisher events,
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

    public ICapabilityBroker Capabilities { get; }

    public Config.GameConstants Constants { get; }

    public Logging.ILog Log { get; }

    public Ui.IUiRegistry Ui { get; }
}
