using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SimT.Contracts;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Cli;

/// <summary>
/// Headless harness for the slice. It is the golden-test oracle: same seed + same action
/// script must produce the same ledger hash, so a refactor cannot silently change the numbers.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0)
        {
            PrintUsage();
            return 2;
        }

        try
        {
            return args[0] switch
            {
                "replay" => Replay(args, expectPath: null),
                "golden" => Replay(args, expectPath: args.Length > 2 ? args[2] : null),
                "info" => Info(),
                _ => Unknown(args[0])
            };
        }
        catch (SimException error)
        {
            Console.Error.WriteLine($"FAILED [{error.Code}] {error.Message}");
            return 1;
        }
    }

    private static int Replay(string[] args, string? expectPath)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("replay needs a scenario path.");
            return 2;
        }

        if (args[0] == "golden" && expectPath is null)
        {
            Console.Error.WriteLine("golden needs an expectation file.");
            return 2;
        }

        var scenarioPath = Path.GetFullPath(args[1]);
        using var document = JsonDocument.Parse(File.ReadAllText(scenarioPath));
        var actions = document.RootElement.GetProperty("actions");

        var root = AppContext.BaseDirectory;
        var workDir = Path.Combine(Path.GetTempPath(), "simt-replay", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        var options = new KernelOptions(
            DatabasePath: Path.Combine(workDir, "campaign.db"),
            ModulesDirectory: Path.Combine(root, "Modules"),
            ProductPath: Path.Combine(root, "assets", "products", "merchant-slice.json"),
            ConstantsPath: Path.Combine(root, "assets", "config", "game-constants.json"));

        using var kernel = SimKernel.Open(options);

        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var warehouse = kernel.Capabilities.Get<IMarketWarehouse>(CapabilityIds.MarketWarehouse);
        var player = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        var catalog = kernel.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);

        foreach (var action in actions.EnumerateArray())
        {
            var type = action.GetProperty("type").GetString();
            switch (type)
            {
                case "advance":
                    kernel.AdvanceDays(action.GetProperty("days").GetInt32());
                    break;

                case "buy":
                    Console.WriteLine($"  buy   -> {trade.Buy(player.CurrentCityId, ItemId(catalog, action), Quantity(action)).Message}");
                    break;

                case "sell":
                    Console.WriteLine($"  sell  -> {trade.Sell(player.CurrentCityId, ItemId(catalog, action), Quantity(action)).Message}");
                    break;

                case "rent":
                    warehouse.Rent(player.CurrentCityId, out var rentMessage);
                    Console.WriteLine($"  rent  -> {rentMessage}");
                    break;

                case "store":
                    warehouse.Store(player.CurrentCityId, ItemId(catalog, action), Quantity(action), out var storeMessage);
                    Console.WriteLine($"  store -> {storeMessage}");
                    break;

                case "take":
                    warehouse.Take(player.CurrentCityId, ItemId(catalog, action), Quantity(action), out var takeMessage);
                    Console.WriteLine($"  take  -> {takeMessage}");
                    break;

                case "command":
                {
                    // Generic DLC command: the CLI must be able to drive any registered screen
                    // without knowing which module owns it.
                    var screen = action.GetProperty("screen").GetString() ?? string.Empty;
                    var id = action.GetProperty("id").GetString() ?? string.Empty;
                    if (!kernel.TryGetCommandHandler(screen, out var handler) || handler is null)
                    {
                        throw new SimException("cli.unknown_screen", $"Scenario targets screen '{screen}', which no enabled module registers.");
                    }

                    Console.WriteLine($"  {screen}:{id} -> {handler.Execute(id)}");
                    break;
                }

                default:
                    throw new SimException("cli.unknown_action", $"Scenario action '{type}' is not supported.");
            }
        }

        kernel.Save();

        var failures = kernel.RunInvariants();
        var hash = LedgerHash(kernel);
        Console.WriteLine($"date        {kernel.Today}");
        Console.WriteLine($"balance     {kernel.Ledger.Balance}");
        Console.WriteLine($"ledgerHash  {hash}");
        Console.WriteLine($"invariants  {(failures.Count == 0 ? "ok" : "FAILED")} ({kernel.Invariants.Count} rules)");
        Console.WriteLine($"save        {options.DatabasePath}");

        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"INVARIANT {failure.InvariantId}: {failure.Message}");
        }

        if (failures.Count > 0)
        {
            return 1;
        }

        if (expectPath is null)
        {
            return 0;
        }

        var expected = File.ReadAllText(expectPath).Trim();
        if (!string.Equals(expected, hash, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"GOLDEN MISMATCH: expected {expected}, got {hash}");
            return 1;
        }

        Console.WriteLine("golden ok");
        return 0;
    }

    private static int Info()
    {
        var root = AppContext.BaseDirectory;
        var modulesDir = Path.Combine(root, "Modules");
        var modules = Directory.Exists(modulesDir) ? Directory.GetFiles(modulesDir, "*.dll").Length : 0;

        Console.WriteLine($"base     {root}");
        Console.WriteLine($"modules  {modules} dll(s) in {modulesDir}");
        return 0;
    }

    private static string LedgerHash(SimKernel kernel)
    {
        var builder = new StringBuilder();
        foreach (var line in kernel.Ledger.Recent(100_000))
        {
            builder.Append(line.Date).Append('|')
                .Append(line.JournalCode).Append('|')
                .Append((int)line.Direction).Append('|')
                .Append(line.Amount).Append('|')
                .Append(line.BalanceAfter).Append('\n');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static long ItemId(IItemCatalog catalog, JsonElement action)
    {
        var code = action.GetProperty("item").GetString() ?? string.Empty;
        var item = catalog.FindByCode(code)
                   ?? throw new SimException("cli.unknown_item", $"Scenario references unknown item '{code}'.");
        return item.Id;
    }

    private static int Quantity(JsonElement action) =>
        action.TryGetProperty("qty", out var qty) ? qty.GetInt32() : 1;

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("SimT.Cli <command>");
        Console.WriteLine("  replay <scenario.json>              run a script and print the ledger hash");
        Console.WriteLine("  golden <scenario.json> <expect.txt> run and compare with a recorded hash");
        Console.WriteLine("  info                                show where modules and assets are resolved");
    }
}
