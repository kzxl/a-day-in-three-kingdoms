using System.IO;
using System.Text.Json;
using SimT.Contracts.Products;
using SimT.Core.Config;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

/// <summary>
/// Save management for an offline single-player game: one directory per campaign, one SQLite
/// file inside it, one index.json listing them. No leases, no locks, no registry database —
/// the operating system already arbitrates a single user on a single file.
/// </summary>
public sealed class SaveManager
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _root;

    public SaveManager()
    {
        _root = Environment.GetEnvironmentVariable("SIMT_SAVES")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimT", "saves");
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public IReadOnlyList<CampaignMeta> List()
    {
        var indexPath = Path.Combine(_root, "index.json");
        if (!File.Exists(indexPath))
        {
            return Array.Empty<CampaignMeta>();
        }

        var items = JsonSerializer.Deserialize<List<CampaignMeta>>(File.ReadAllText(indexPath), Json) ?? [];
        return items.Select(meta => meta with { Modules = NormalizeModules(meta.Modules) })
                    .OrderByDescending(meta => meta.LastPlayedUtc, StringComparer.Ordinal).ToList();
    }

    private static IReadOnlyList<string> NormalizeModules(IReadOnlyList<string> modules)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in modules)
        {
            switch (mod.ToLowerInvariant())
            {
                case "dlc.market":
                case "dlc.caravan":
                case "dlc.cultivation":
                case "dlc.professions":
                    result.Add("dlc.economy");
                    break;
                case "dlc.social":
                case "dlc.dialogue":
                case "dlc.lineage":
                    result.Add("dlc.character");
                    break;
                case "dlc.politics":
                case "dlc.military":
                    result.Add("dlc.power");
                    break;
                default:
                    result.Add(mod);
                    break;
            }
        }
        return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public CampaignMeta Create(string name, Action<string>? logSink = null, StartingScenario? scenario = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(_root, id);
        Directory.CreateDirectory(directory);

        var product = PrepareScenario(directory, scenario ?? StartingScenarios.All[0]);
        var meta = CampaignMeta.New(id, name, product);

        using (var kernel = SimKernel.Open(Options(directory, logSink)))
        {
            kernel.Save();
        }

        Upsert(meta);
        return meta;
    }

    public (CampaignMeta Meta, SimKernel Kernel) CreateAndOpen(string name, Action<string>? logSink = null, StartingScenario? scenario = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(_root, id);
        Directory.CreateDirectory(directory);

        var product = PrepareScenario(directory, scenario ?? StartingScenarios.All[0]);
        var meta = CampaignMeta.New(id, name, product);

        var kernel = SimKernel.Open(Options(directory, logSink));
        try
        {
            kernel.Save();
            Upsert(meta);
            return (meta, kernel);
        }
        catch
        {
            kernel.Dispose();
            throw;
        }
    }

    public SimKernel Open(CampaignMeta meta, Action<string>? logSink = null) =>
        SimKernel.Open(Options(Path.Combine(_root, meta.CampaignId), logSink));

    public void Touch(CampaignMeta meta, string gameDate)
    {
        Upsert(meta with
        {
            LastPlayedUtc = DateTime.UtcNow.ToString("O"),
            GameDate = gameDate
        });
    }

    public void Delete(string campaignId)
    {
        var directory = Path.Combine(_root, campaignId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        var remaining = List().Where(meta => meta.CampaignId != campaignId).ToList();
        File.WriteAllText(Path.Combine(_root, "index.json"), JsonSerializer.Serialize(remaining, Json));
    }

    public CampaignMeta Duplicate(CampaignMeta source)
    {
        var id = Guid.NewGuid().ToString("N");
        var from = Path.Combine(_root, source.CampaignId);
        var to = Path.Combine(_root, id);
        Directory.CreateDirectory(to);

        foreach (var file in Directory.GetFiles(from))
        {
            // The lock file belongs to a running session, never to the saved state.
            if (file.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        }

        var copy = source with
        {
            CampaignId = id,
            Name = source.Name + " (bản sao)",
            CreatedUtc = DateTime.UtcNow.ToString("O"),
            LastPlayedUtc = DateTime.UtcNow.ToString("O")
        };

        Upsert(copy);
        return copy;
    }

    private void Upsert(CampaignMeta meta)
    {
        var items = List().Where(existing => existing.CampaignId != meta.CampaignId).Append(meta).ToList();
        File.WriteAllText(Path.Combine(_root, "index.json"), JsonSerializer.Serialize(items, Json));
    }

    private static ProductManifest PrepareScenario(string directory, StartingScenario scenario)
    {
        var product = Product() with { StartDate = scenario.StartDate, StartLocationCode = scenario.SuggestedCityCode };
        var payload = new { id = product.Id, name = product.Name, modules = product.Modules,
            seed = product.Seed, startLocation = product.StartLocationCode, startingCash = product.StartingCash,
            startDate = SimT.Contracts.Time.GameDateCodec.Format(product.StartDate), scenario = scenario.Code };
        File.WriteAllText(Path.Combine(directory, "product.json"), JsonSerializer.Serialize(payload, Json));
        return product;
    }

    private static ProductManifest Product()
    {
        var constants = SimT.Core.Config.GameConstantsLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "assets", "config", "game-constants.json"));

        return ProductManifestLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "assets", "products", "merchant-slice.json"),
            SimT.Contracts.Time.GameDateCodec.Parse(constants.FallbackDate));
    }

    private static KernelOptions Options(string campaignDirectory, Action<string>? logSink = null) => new(
        DatabasePath: Path.Combine(campaignDirectory, "campaign.db"),
        ModulesDirectory: Path.Combine(AppContext.BaseDirectory, "Modules"),
        ProductPath: File.Exists(Path.Combine(campaignDirectory, "product.json"))
            ? Path.Combine(campaignDirectory, "product.json")
            : Path.Combine(AppContext.BaseDirectory, "assets", "products", "merchant-slice.json"),
        ConstantsPath: Path.Combine(AppContext.BaseDirectory, "assets", "config", "game-constants.json"),
        BackupDirectory: Path.Combine(campaignDirectory, "backups"),
        LogSink: logSink);
}
