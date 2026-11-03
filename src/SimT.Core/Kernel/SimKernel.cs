using System.Reflection;
using SimT.Contracts;
using SimT.Contracts.Config;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Modules;
using SimT.Contracts.Products;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Core.Config;
using SimT.Core.Data;
using SimT.Core.Events;
using SimT.Core.Identity;
using SimT.Core.Ledger;
using SimT.Core.Logging;
using SimT.Core.Modules;
using SimT.Core.Time;

namespace SimT.Core.Kernel;

/// <summary>Everything the kernel needs to open one campaign.</summary>
public sealed record KernelOptions(
    string DatabasePath,
    string ModulesDirectory,
    string ProductPath,
    string ConstantsPath,
    Action<string>? LogSink = null);

/// <summary>
/// K1–K9 assembled. Opening a campaign applies the core migrations, resolves the enabled
/// module set from the product manifest, migrates each module from its own embedded
/// resources, and starts it in dependency order.
/// </summary>
public sealed class SimKernel : IDisposable
{
    private readonly SqliteSession _db;
    private readonly SplitMix64Rng _rng;
    private readonly UiRegistry _ui;
    private readonly CapabilityBroker _broker;
    private readonly SimClock _clock;
    private readonly SqliteLedger _ledger;
    private readonly List<ModuleContext> _contexts = new();
    private bool _disposed;

    private SimKernel(
        SqliteSession db,
        SplitMix64Rng rng,
        UiRegistry ui,
        CapabilityBroker broker,
        SimClock clock,
        SqliteLedger ledger,
        GameConstants constants,
        ProductManifest product)
    {
        _db = db;
        _rng = rng;
        _ui = ui;
        _broker = broker;
        _clock = clock;
        _ledger = ledger;
        Constants = constants;
        Product = product;
    }

    public GameConstants Constants { get; }

    public ProductManifest Product { get; }

    public IDbSession Db => _db;

    public ILedger Ledger => _ledger;

    public IGameClock Clock => _clock;

    public IUiRegistry Ui => _ui;

    public ICapabilityBroker Capabilities => _broker;

    public IReadOnlyList<ModuleContext> Modules => _contexts;

    public GameDate Today => _clock.Today;

    public static SimKernel Open(KernelOptions options)
    {
        var db = SqliteSession.Open(options.DatabasePath);
        try
        {
            var runner = new MigrationRunner(db);
            runner.Apply(typeof(SimKernel).Assembly, "core");

            var constants = GameConstantsLoader.Load(options.ConstantsPath);
            var product = ProductManifestLoader.Load(options.ProductPath);
            var events = new EventPublisher();
            var clock = new SimClock(db, events);
            var ledger = new SqliteLedger(db);
            var ids = new SequenceIdGenerator(db);

            var storedRng = db.ScalarString("SELECT value FROM core_state WHERE key='rng.state'");
            var rng = new SplitMix64Rng(storedRng is null ? (ulong)product.Seed : ulong.Parse(storedRng));

            var broker = new CapabilityBroker();
            var ui = new UiRegistry();
            var kernel = new SimKernel(db, rng, ui, broker, clock, ledger, constants, product);

            var discovered = ModuleLoader.Discover(options.ModulesDirectory);
            var ordered = ModuleGraph.Resolve(discovered, product.Modules);
            var campaign = new CampaignStart(product.Seed, product.StartLocationCode, product.StartingCash);

            foreach (var descriptor in ordered)
            {
                runner.Apply(descriptor.Assembly, descriptor.Manifest.Id);

                var context = new ModuleContext(
                    descriptor.Manifest,
                    campaign,
                    db,
                    clock,
                    ledger,
                    ids,
                    rng,
                    events,
                    broker,
                    constants,
                    new TextLog(descriptor.Manifest.Id, options.LogSink),
                    ui);

                var module = (ISimModule)Activator.CreateInstance(descriptor.EntryPoint)!;
                module.Start(context);

                db.Execute(
                    "INSERT INTO core_module_state(module_id, version, enabled) VALUES(@id, @version, 1) " +
                    "ON CONFLICT(module_id) DO UPDATE SET version=excluded.version, enabled=1",
                    new DbArgs().Add("@id", descriptor.Manifest.Id).Add("@version", descriptor.Manifest.Version));

                kernel._contexts.Add(context);
            }

            return kernel;
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }

    /// <summary>The day-advance chokepoint. One transaction per day keeps the save consistent.</summary>
    public GameDate AdvanceDay() => AdvanceDays(1);

    public GameDate AdvanceDays(int days)
    {
        var result = _clock.Today;
        _db.Transaction(() => result = _clock.Advance(days));
        return result;
    }

    /// <summary>Flushes everything a reload must reproduce: the date and the RNG stream position.</summary>
    public void Save()
    {
        _clock.Persist();
        _db.Execute(
            "INSERT INTO core_state(key, value) VALUES('rng.state', @value) ON CONFLICT(key) DO UPDATE SET value=excluded.value",
            DbArgs.One("@value", _rng.State.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    public bool TryGetCommandHandler(string screenId, out IScreenCommandHandler? handler) =>
        _ui.TryGetCommandHandler(screenId, out handler);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Save();
        _db.Dispose();
    }
}
