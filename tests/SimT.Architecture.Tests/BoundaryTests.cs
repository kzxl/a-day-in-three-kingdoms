using System.Reflection;
using System.Text.Json;
using SimT.Contracts.Config;
using SimT.Contracts.Modules;
using SimT.Contracts.Products;
using SimT.Contracts.Time;
using SimT.Core.Config;
using SimT.Core.Data;
using SimT.Core.Kernel;
using SimT.Core.Modules;

namespace SimT.Architecture.Tests;

/// <summary>
/// The gate. These tests are the reason the folder layout is an architecture and not a
/// suggestion: every rule from the design is machine-checked here.
/// </summary>
public sealed class BoundaryTests
{
    private static readonly Assembly Contracts = typeof(SimT.Contracts.SimException).Assembly;
    private static readonly Assembly Core = typeof(SimKernel).Assembly;

    [Fact]
    public void Contracts_has_no_solution_references()
    {
        Assert.Empty(SimTReferences(Contracts));
    }

    [Fact]
    public void Core_references_contracts_only()
    {
        Assert.Equal(["SimT.Contracts"], SimTReferences(Core));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_references_contracts_only(Assembly module)
    {
        // Core is not even referenced by modules: they consume kernel services through
        // ModuleContext, which is declared in Contracts.
        Assert.Equal(["SimT.Contracts"], SimTReferences(module));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_declares_a_valid_manifest(Assembly module)
    {
        var manifest = Manifest(module);

        Assert.False(string.IsNullOrWhiteSpace(manifest.Id));
        Assert.Equal(ModuleManifest.CurrentApiVersion, manifest.ApiVersion);
        Assert.Contains(manifest.Kind, ["base", "dlc"]);
        Assert.False(string.IsNullOrWhiteSpace(module.GetName().Name));
        Assert.NotEmpty(manifest.Owns.Tables);
    }

    [Fact]
    public void Table_ownership_is_unique_across_modules()
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in RepoLayout.ModuleAssemblies())
        {
            var manifest = Manifest(module);
            foreach (var table in manifest.Owns.Tables)
            {
                Assert.False(owners.TryGetValue(table, out var owner), $"Table '{table}' is claimed by '{owner}' and '{manifest.Id}'.");
                owners[table] = manifest.Id;
            }
        }
    }

    [Fact]
    public void Every_declared_table_is_created_by_its_own_migrations()
    {
        foreach (var module in RepoLayout.ModuleAssemblies())
        {
            var manifest = Manifest(module);
            var sql = string.Join('\n', MigrationRunner.ListResources(module).Select(resource => ReadResource(module, resource.ResourceName)));

            foreach (var table in manifest.Owns.Tables)
            {
                Assert.Contains($"CREATE TABLE IF NOT EXISTS {table}", sql, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Modules_do_not_read_foreign_tables()
    {
        // A module may only name tables it declares. This is the rule that keeps a DLC from
        // quietly joining against another module's data.
        foreach (var module in RepoLayout.ModuleAssemblies())
        {
            var manifest = Manifest(module);
            var owned = manifest.Owns.Tables.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sources = RepoLayout.Sources(".cs")
                .Where(path => path.Contains(Path.DirectorySeparatorChar + module.GetName().Name!, StringComparison.OrdinalIgnoreCase));

            foreach (var path in sources)
            {
                var text = File.ReadAllText(path);
                foreach (var table in KnownTables())
                {
                    if (owned.Contains(table))
                    {
                        continue;
                    }

                    Assert.DoesNotContain(table, text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public void No_create_table_outside_migrations()
    {
        // MigrationRunner is the single allowed exception: it must bootstrap core_migration
        // before it can read which migrations already ran.
        foreach (var path in RepoLayout.Sources(".cs"))
        {
            if (path.EndsWith("MigrationRunner.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            Assert.DoesNotContain("CREATE TABLE", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Ledger_has_a_single_writer()
    {
        // Reads are fine (the invariant runner verifies the ledger); writes are not.
        string[] writes = ["INSERT INTO core_ledger", "UPDATE core_ledger", "DELETE FROM core_ledger"];

        var offenders = RepoLayout.Sources(".cs")
            .Where(path => !path.EndsWith("SqliteLedger.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return writes.Any(write => text.Contains(write, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Module_code_never_calls_wall_clock()
    {
        var offenders = RepoLayout.Sources(".cs")
            .Where(path => path.Contains("SimT.Modules.", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.Contains("DateTime.Now", StringComparison.Ordinal)
                       || text.Contains("DateTime.Today", StringComparison.Ordinal)
                       || text.Contains("DateTime.UtcNow", StringComparison.Ordinal);
            })
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Core_has_no_business_nouns()
    {
        string[] banned = ["market", "warehouse", "citadel", "rice", "silk", "quest", "marriage", "faction"];
        var offenders = new List<string>();

        foreach (var path in RepoLayout.Sources(".cs").Where(path => path.Contains("SimT.Core", StringComparison.OrdinalIgnoreCase)))
        {
            var text = File.ReadAllText(path);
            foreach (var noun in banned)
            {
                if (text.Contains(noun, StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Path.GetFileName(path)} contains '{noun}'");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Calendar_constants_match_the_shared_json()
    {
        var constants = GameConstantsLoader.Load(Path.Combine(RepoLayout.Root, "assets", "config", "game-constants.json"));

        Assert.Equal(GameDate.DaysPerMonth, constants.DaysPerMonth);
        Assert.Equal(GameDate.MonthsPerYear, constants.MonthsPerYear);
        Assert.Equal("Quan", constants.CurrencyNameVi);
        Assert.Equal("Thạch", constants.GrainNameVi);
    }

    [Fact]
    public void Product_manifest_enables_every_module_and_satisfies_dependencies()
    {
        var product = ProductManifestLoader.Load(
            Path.Combine(RepoLayout.Root, "assets", "products", "merchant-slice.json"),
            GameDate.Start);
        var manifests = RepoLayout.ModuleAssemblies().Select(Manifest).ToDictionary(manifest => manifest.Id, StringComparer.Ordinal);

        foreach (var enabled in product.Modules)
        {
            Assert.True(manifests.ContainsKey(enabled), $"Product enables unknown module '{enabled}'.");
        }

        foreach (var enabled in product.Modules)
        {
            foreach (var required in manifests[enabled].Requires)
            {
                Assert.Contains(required, product.Modules);
            }
        }
    }

    [Fact]
    public void Capabilities_consumed_are_provided_somewhere()
    {
        var manifests = RepoLayout.ModuleAssemblies().Select(Manifest).ToList();
        var provided = manifests.SelectMany(manifest => manifest.Provides).ToHashSet(StringComparer.Ordinal);

        Assert.All(
            manifests.SelectMany(manifest => manifest.Consumes.Select(capability => (manifest.Id, Capability: capability))),
            pair => Assert.True(provided.Contains(pair.Capability), $"'{pair.Id}' consumes '{pair.Capability}' which nobody provides."));
    }

    [Fact]
    public void Host_source_never_mentions_module_types()
    {
        foreach (var project in new[] { "SimT.Cli", "SimT.Host.Wpf" })
        {
            var directory = Path.Combine(RepoLayout.Root, "src", project);
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
            {
                Assert.DoesNotContain("SimT.Modules.", File.ReadAllText(file), StringComparison.Ordinal);
            }

            var csproj = File.ReadAllText(Path.Combine(directory, project + ".csproj"));
            Assert.Contains("ReferenceOutputAssembly=\"false\"", csproj);
            Assert.Contains("CopyModules", csproj);
        }
    }

    public static TheoryData<Assembly> Modules()
    {
        var data = new TheoryData<Assembly>();
        foreach (var module in RepoLayout.ModuleAssemblies())
        {
            data.Add(module);
        }

        return data;
    }

    private static ModuleManifest Manifest(Assembly module)
    {
        using var stream = module.GetManifestResourceStream("module.json")
                           ?? throw new InvalidOperationException($"{module.GetName().Name} has no embedded module.json.");
        using var reader = new StreamReader(stream);
        return ModuleLoader.ParseManifest(reader.ReadToEnd(), module.GetName().Name ?? "module");
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> KnownTables() =>
        RepoLayout.ModuleAssemblies()
            .SelectMany(module => Manifest(module).Owns.Tables)
            .Concat(["core_ledger", "core_state", "core_sequence", "core_module_state", "core_migration"]);

    private static string[] SimTReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name => name.StartsWith("SimT.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
}
