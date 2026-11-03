using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Core.Data;
using SimT.Core.Kernel;
using SimT.Core.Modules;

namespace SimT.Core.Tests;

/// <summary>
/// Contract-level tests for the pieces that decide what a save is allowed to load. They are the
/// rules a new DLC will hit first, so they are pinned here rather than discovered in a campaign.
/// </summary>
public sealed class ModuleGraphTests
{
    [Fact]
    public void Dependencies_start_before_the_modules_that_need_them()
    {
        var a = Module("a");
        var b = Module("b", requires: ["a"]);

        var ordered = ModuleGraph.Resolve([a, b], ["b", "a"]);

        Assert.Equal(["a", "b"], ordered.Select(module => module.Manifest.Id));
    }

    [Fact]
    public void A_dependency_cycle_is_rejected()
    {
        var a = Module("a", requires: ["b"]);
        var b = Module("b", requires: ["a"]);

        var error = Assert.Throws<SimException>(() => ModuleGraph.Resolve([a, b], ["a", "b"]));
        Assert.Equal("module.cycle", error.Code);
    }

    [Fact]
    public void Two_artefacts_with_the_same_id_are_rejected()
    {
        var error = Assert.Throws<SimException>(() => ModuleGraph.Resolve([Module("a"), Module("a")], ["a"]));
        Assert.Equal("module.duplicate_id", error.Code);
    }

    [Fact]
    public void Enabling_a_module_that_is_not_present_is_rejected()
    {
        var error = Assert.Throws<SimException>(() => ModuleGraph.Resolve([Module("a")], ["a", "ghost"]));
        Assert.Equal("module.not_found", error.Code);
    }

    [Fact]
    public void Enabling_a_module_without_its_dependency_is_rejected()
    {
        var error = Assert.Throws<SimException>(() => ModuleGraph.Resolve([Module("a"), Module("b", requires: ["a"])], ["b"]));
        Assert.Equal("module.missing_dependency", error.Code);
    }

    [Fact]
    public void Two_modules_cannot_own_the_same_table()
    {
        var a = Module("a", tables: ["shared"]);
        var b = Module("b", tables: ["shared"]);

        var error = Assert.Throws<SimException>(() => ModuleGraph.Resolve([a, b], ["a", "b"]));
        Assert.Equal("module.table_ownership", error.Code);
    }

    [Fact]
    public void Two_modules_cannot_provide_the_same_capability()
    {
        var a = Module("a", provides: ["thing"]);
        var b = Module("b", provides: ["thing"]);

        var error = Assert.Throws<SimException>(() => ModuleGraph.Resolve([a, b], ["a", "b"]));
        Assert.Equal("module.capability_ownership", error.Code);
    }

    [Fact]
    public void An_api_version_mismatch_is_rejected()
    {
        var error = Assert.Throws<SimException>(() => ModuleGraph.Resolve([Module("a", api: "2")], ["a"]));
        Assert.Equal("module.api_mismatch", error.Code);
    }

    private static ModuleDescriptor Module(
        string id,
        string[]? requires = null,
        string[]? provides = null,
        string[]? tables = null,
        string api = ModuleManifest.CurrentApiVersion) =>
        new(
            new ModuleManifest(
                id,
                "1.0.0",
                api,
                "dlc",
                requires ?? [],
                provides ?? [],
                [],
                new ModuleOwnership(tables ?? [$"{id}_table"], ["migrations"]),
                []),
            typeof(object).Assembly,
            typeof(object));
}

public sealed class CapabilityBrokerTests
{
    [Fact]
    public void A_provider_can_be_resolved_by_contract()
    {
        var broker = new CapabilityBroker();
        var provider = new Provider();

        broker.Provide("thing", provider);

        Assert.True(broker.IsProvided("thing"));
        Assert.Same(provider, broker.Get<IProvider>("thing"));
    }

    [Fact]
    public void Providing_the_same_capability_twice_is_rejected()
    {
        var broker = new CapabilityBroker();
        broker.Provide("thing", new Provider());

        var error = Assert.Throws<SimException>(() => broker.Provide("thing", new Provider()));
        Assert.Equal("capability.duplicate", error.Code);
    }

    [Fact]
    public void Asking_for_a_missing_capability_names_it()
    {
        var broker = new CapabilityBroker();

        var error = Assert.Throws<MissingCapabilityException>(() => broker.Get<IProvider>("ghost"));
        Assert.Equal("ghost", error.CapabilityId);
    }

    [Fact]
    public void Asking_for_the_wrong_contract_is_rejected()
    {
        var broker = new CapabilityBroker();
        broker.Provide("thing", "not a provider");

        var error = Assert.Throws<SimException>(() => broker.Get<IProvider>("thing"));
        Assert.Equal("capability.type_mismatch", error.Code);
    }

    [Fact]
    public void TryGet_reports_absence_without_throwing()
    {
        var broker = new CapabilityBroker();

        Assert.False(broker.TryGet<IProvider>("ghost", out var provider));
        Assert.Null(provider);
    }

    private interface IProvider;

    private sealed class Provider : IProvider;
}

public sealed class MigrationRunnerTests
{
    [Fact]
    public void Every_embedded_core_migration_is_pending_on_a_fresh_save()
    {
        using var db = TempDatabase();
        var runner = new MigrationRunner(db);
        var expected = MigrationRunner.ListResources(typeof(SimKernel).Assembly).Count;

        Assert.Equal(expected, runner.Pending(typeof(SimKernel).Assembly, "core").Count);
        Assert.False(runner.HasMigrationTable());
    }

    [Fact]
    public void Applying_twice_is_idempotent()
    {
        using var db = TempDatabase();
        var runner = new MigrationRunner(db);

        var first = runner.Apply(typeof(SimKernel).Assembly, "core");
        var second = runner.Apply(typeof(SimKernel).Assembly, "core");

        Assert.True(first > 0);
        Assert.Equal(0, second);
        Assert.Empty(runner.Pending(typeof(SimKernel).Assembly, "core"));
    }

    [Fact]
    public void Migrations_are_listed_in_ordinal_order()
    {
        var ids = MigrationRunner.ListResources(typeof(SimKernel).Assembly).Select(resource => resource.Id).ToList();
        var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(sorted, ids);
    }

    private static SqliteSession TempDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), "simt-contract", Guid.NewGuid().ToString("N"), "campaign.db");
        return SqliteSession.Open(path);
    }
}

public sealed class ModuleConfigTests
{
    [Fact]
    public void Missing_settings_fall_back_to_the_code_default()
    {
        var config = new ModuleConfig(null, "dlc.test");

        Assert.Equal(8, config.GetLong("freightPerUnit", 8));
        Assert.Equal("fallback", config.GetString("name", "fallback"));
        Assert.False(config.Has("freightPerUnit"));
    }

    [Fact]
    public void Manifest_settings_override_the_default()
    {
        var config = new ModuleConfig(new Dictionary<string, string> { ["freightPerUnit"] = "12" }, "dlc.test");

        Assert.Equal(12, config.GetLong("freightPerUnit", 8));
        Assert.Equal(12, config.RequireLong("freightPerUnit"));
    }

    [Fact]
    public void A_required_setting_that_is_absent_is_a_boot_error()
    {
        var config = new ModuleConfig(null, "dlc.test");

        var error = Assert.Throws<SimException>(() => config.RequireLong("freightPerUnit"));
        Assert.Equal("module.config_missing", error.Code);
    }

    [Fact]
    public void A_malformed_setting_is_a_boot_error()
    {
        var config = new ModuleConfig(new Dictionary<string, string> { ["freightPerUnit"] = "eight" }, "dlc.test");

        var error = Assert.Throws<SimException>(() => config.RequireLong("freightPerUnit"));
        Assert.Equal("module.config_invalid", error.Code);
    }

    [Fact]
    public void The_loader_reads_scalar_settings_and_rejects_nested_ones()
    {
        var manifest = ModuleLoader.ParseManifest(
            """
            {
              "id": "dlc.test",
              "version": "1.0.0",
              "apiVersion": "1",
              "kind": "dlc",
              "config": { "freightPerUnit": 12, "label": "hello", "flag": true }
            }
            """,
            "dlc.test");

        Assert.Equal(12, manifest.Settings.RequireLong("freightPerUnit"));
        Assert.Equal("hello", manifest.Settings.GetString("label", ""));
        Assert.Equal("true", manifest.Settings.GetString("flag", ""));

        var error = Assert.Throws<SimException>(() => ModuleLoader.ParseManifest(
            """
            {
              "id": "dlc.bad",
              "version": "1.0.0",
              "apiVersion": "1",
              "kind": "dlc",
              "config": { "nested": { "a": 1 } }
            }
            """,
            "dlc.bad"));

        Assert.Equal("module.invalid_manifest", error.Code);
    }
}
