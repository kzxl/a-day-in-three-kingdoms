using SimT.Contracts;
using SimT.Contracts.Commands;
using SimT.Contracts.Localization;
using SimT.Contracts.Simulation;
using SimT.Core.Commands;
using SimT.Core.Localization;
using SimT.Core.Simulation;
using SimT.Contracts.Config;
using SimT.Contracts.Data;
using SimT.Contracts.Events;
using SimT.Contracts.Diagnostics;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Products;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Core.Config;
using SimT.Core.Data;
using SimT.Core.Diagnostics;
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
    Action<string>? LogSink = null,
    string? BackupDirectory = null);

/// <summary>
/// K1–K9 assembled. Opening a campaign applies the core migrations, resolves the enabled
/// module set from the product manifest, migrates each module from its own embedded
/// resources, and starts it in dependency order.
/// </summary>
public sealed class SimKernel : IDisposable
{
    /// <summary>Bumped when a kernel-owned table changes shape; a newer save refuses to open.</summary>
    public const string SchemaVersion = "1";

    private readonly SqliteSession _db;
    private readonly SplitMix64Rng _rng;
    private readonly UiRegistry _ui;
    private readonly CapabilityBroker _broker;
    private readonly EventPublisher _events;
    private readonly SimClock _clock;
    private readonly SqliteLedger _ledger;
    private readonly InvariantRunner _invariants;
    private readonly List<ModuleContext> _contexts = new();
    private readonly List<ISimModule> _started = new();
    private readonly CampaignLock _lock;
    private readonly CommandDispatcher _commands = new();
    private readonly SimulationRunner _simulation;
    private readonly CheckpointManager _checkpoints;
    private readonly LocalizationService _localization = new();
    private bool _disposed;

    private SimKernel(
        SqliteSession db,
        SplitMix64Rng rng,
        UiRegistry ui,
        CapabilityBroker broker,
        EventPublisher events,
        SimClock clock,
        SqliteLedger ledger,
        GameConstants constants,
        ProductManifest product,
        CampaignLock campaignLock)
    {
        _db = db;
        _rng = rng;
        _ui = ui;
        _broker = broker;
        _events = events;
        _clock = clock;
        _ledger = ledger;
        Constants = constants;
        Product = product;
        _lock = campaignLock;
        var dbPath = db.DatabasePath;
        var baseDir = Path.GetDirectoryName(Path.GetFullPath(string.IsNullOrEmpty(dbPath) ? "campaign.db" : dbPath)) ?? string.Empty;
        _checkpoints = new CheckpointManager(db, dbPath, Path.Combine(baseDir, "checkpoints"));
        _simulation = new SimulationRunner(this);

        _invariants = new InvariantRunner(new KernelInvariantContext(db, clock, ledger));
        foreach (var invariant in KernelInvariants.All())
        {
            _invariants.Register(invariant);
        }
    }

    public GameConstants Constants { get; }

    public ProductManifest Product { get; }

    public IDbSession Db => _db;

    public ILedger Ledger => _ledger;

    public IGameClock Clock => _clock;

    public IActionTime ActionTime { get; private init; } = null!;

    public IEventPublisher Events => _events;

    public IUiRegistry Ui => _ui;

    public ICapabilityBroker Capabilities => _broker;

    public IReadOnlyList<ModuleContext> Modules => _contexts;

    public IReadOnlyList<IInvariant> Invariants => _invariants.Invariants;

    public ICommandDispatcher Commands => _commands;

    public ISimulationRunner Simulation => _simulation;

    public ICheckpointManager Checkpoints => _checkpoints;

    public ILocalizationService Localization => _localization;

    public GameDate Today => _clock.Today;

    /// <summary>
    /// Month index since the campaign started, 1-based: the "lượt" the old game showed. Turn 1 is
    /// the month the campaign opens in, and it ticks once per closed month.
    /// </summary>
    public int Turn
    {
        get
        {
            var start = Product.StartDate;
            var today = _clock.Today;
            return ((today.Year - start.Year) * GameDate.MonthsPerYear) + (today.Month - start.Month) + 1;
        }
    }

    public static SimKernel Open(KernelOptions options)
    {
        var existedBefore = File.Exists(Path.GetFullPath(options.DatabasePath));
        var campaignLock = CampaignLock.Acquire(options.DatabasePath);

        SqliteSession? db = null;
        try
        {
            db = SqliteSession.Open(options.DatabasePath);
            var runner = new MigrationRunner(db);
            var log = new TextLog("kernel", options.LogSink);

            var constants = GameConstantsLoader.Load(options.ConstantsPath);
            var product = ProductManifestLoader.Load(
                options.ProductPath,
                GameDateCodec.Parse(constants.FallbackDate));
            var discovered = ModuleLoader.Discover(options.ModulesDirectory);
            var ordered = ModuleGraph.Resolve(discovered, product.Modules);

            BackupIfMigrationsPending(runner, ordered, options, db, existedBefore, log);

            runner.Apply(typeof(SimKernel).Assembly, "core");
            EnsureSchemaVersion(db);

            var events = new EventPublisher();
            var clock = new SimClock(db, events, product.StartDate, constants.EraEndYear);
            var actionTime = new ActionTime(clock, LoadActionCosts(options.ConstantsPath), events);
            var ledger = new SqliteLedger(db);
            var ids = new SequenceIdGenerator(db);

            var storedRng = db.ScalarString("SELECT value FROM core_state WHERE key='rng.state'");
            var rng = new SplitMix64Rng(storedRng is null ? (ulong)product.Seed : ulong.Parse(storedRng));

            var broker = new CapabilityBroker();
            broker.Provide("core.events", events);
            var ui = new UiRegistry();
            var kernel = new SimKernel(db, rng, ui, broker, events, clock, ledger, constants, product, campaignLock)
            {
                ActionTime = actionTime
            };

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
                    actionTime,
                    broker,
                    constants,
                    new TextLog(descriptor.Manifest.Id, options.LogSink),
                    ui);

                var module = (ISimModule)Activator.CreateInstance(descriptor.EntryPoint)!;
                module.Start(context);

                foreach (var invariant in context.Invariants)
                {
                    kernel._invariants.Register(invariant);
                }

                db.Execute(
                    "INSERT INTO core_module_state(module_id, version, enabled) VALUES(@id, @version, 1) " +
                    "ON CONFLICT(module_id) DO UPDATE SET version=excluded.version, enabled=1",
                    new DbArgs().Add("@id", descriptor.Manifest.Id).Add("@version", descriptor.Manifest.Version));

                kernel._contexts.Add(context);
                kernel._started.Add(module);
            }

            kernel.MarkDisabledModules(product.Modules);
            return kernel;
        }
        catch
        {
            db?.Dispose();
            campaignLock.Dispose();
            throw;
        }
    }

    /// <summary>The day-advance chokepoint. One transaction per day keeps the save consistent.</summary>
    public GameDate AdvanceDay() => AdvanceDays(1);

    public GameDate AdvanceDays(int days)
    {
        var result = _clock.Today;
        _db.Transaction(() =>
        {
            result = _clock.Advance(days);
            PersistRng();
        });

        return result;
    }

    /// <summary>Runs every registered invariant; an empty list means stored state is coherent.</summary>
    public IReadOnlyList<InvariantFailure> RunInvariants() => _invariants.Run();

    /// <summary>Flushes everything a reload must reproduce: the date and the RNG stream position.</summary>
    public void Save()
    {
        _clock.Persist();
        PersistRng();
    }

    /// <summary>
    /// Writes the RNG position inside the caller's transaction. A crash after a committed roll
    /// must not leave the save with fresh data and a stale stream, or every later replay diverges.
    /// </summary>
    private void PersistRng() =>
        _db.Execute(
            "INSERT INTO core_state(key, value) VALUES('rng.state', @value) ON CONFLICT(key) DO UPDATE SET value=excluded.value",
            DbArgs.One("@value", _rng.State.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    public bool TryGetCommandHandler(string screenId, out IScreenCommandHandler? handler) =>
        _ui.TryGetCommandHandler(screenId, out handler);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        for (var index = _started.Count - 1; index >= 0; index--)
        {
            if (_started[index] is IStoppableModule stoppable)
            {
                stoppable.Stop();
            }
        }

        Save();
        _db.Dispose();
        _lock.Dispose();
    }

    /// <summary>
    /// The cost table lives beside the constants file. A missing table is a packaging error, not
    /// a reason to let actions cost nothing.
    /// </summary>
    private static IActionCostPolicy LoadActionCosts(string constantsPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(constantsPath)) ?? ".";
        return ActionCostPolicy.Load(Path.Combine(directory, "action-costs.json"));
    }

    private static void BackupIfMigrationsPending(
        MigrationRunner runner,
        IReadOnlyList<ModuleDescriptor> ordered,
        KernelOptions options,
        SqliteSession db,
        bool existedBefore,
        ILog log)
    {
        if (!existedBefore || options.BackupDirectory is null)
        {
            return;
        }

        var pending = runner.Pending(typeof(SimKernel).Assembly, "core").Count
                      + ordered.Sum(descriptor => runner.Pending(descriptor.Assembly, descriptor.Manifest.Id).Count);

        if (pending == 0)
        {
            return;
        }

        var path = DatabaseBackup.Create(db, options.DatabasePath, options.BackupDirectory);
        if (path is not null)
        {
            log.Info($"backed up save before {pending} pending migration(s): {path}");
        }
    }

    private static void EnsureSchemaVersion(SqliteSession db)
    {
        var stored = db.ScalarString("SELECT value FROM core_state WHERE key='schema.version'");
        if (stored is null)
        {
            db.Execute(
                "INSERT INTO core_state(key, value) VALUES('schema.version', @version)",
                DbArgs.One("@version", SchemaVersion));
            return;
        }

        if (string.CompareOrdinal(stored, SchemaVersion) > 0)
        {
            throw new SimException(
                "save.future_version",
                $"Save schema {stored} is newer than this build ({SchemaVersion}); update the game instead of downgrading the save.");
        }
    }

    /// <summary>
    /// Keeps the save's module ledger honest: modules present in the save but not enabled by
    /// this product are marked disabled, and their data is left untouched.
    /// </summary>
    private void MarkDisabledModules(IReadOnlyList<string> enabled)
    {
        var rows = _db.Query("SELECT module_id FROM core_module_state WHERE enabled = 1", row => row.GetString(0));
        foreach (var moduleId in rows)
        {
            if (enabled.Contains(moduleId, StringComparer.Ordinal))
            {
                continue;
            }

            _db.Execute("UPDATE core_module_state SET enabled = 0 WHERE module_id=@id", DbArgs.One("@id", moduleId));
        }
    }

    private sealed class KernelInvariantContext(IDbSession db, IGameClock clock, ILedger ledger) : IInvariantContext
    {
        public IDbSession Db => db;

        public IGameClock Clock => clock;

        public ILedger Ledger => ledger;
    }
}
