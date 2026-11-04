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
        return items.OrderByDescending(meta => meta.LastPlayedUtc, StringComparer.Ordinal).ToList();
    }

    public CampaignMeta Create(string name)
    {
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(_root, id);
        Directory.CreateDirectory(directory);

        var product = Product();
        var meta = CampaignMeta.New(id, name, product);

        using (var kernel = SimKernel.Open(Options(directory)))
        {
            kernel.Save();
        }

        Upsert(meta);
        return meta;
    }

    public SimKernel Open(CampaignMeta meta) => SimKernel.Open(Options(Path.Combine(_root, meta.CampaignId)));

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

    private static ProductManifest Product()
    {
        var constants = SimT.Core.Config.GameConstantsLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "assets", "config", "game-constants.json"));

        return ProductManifestLoader.Load(
            Path.Combine(AppContext.BaseDirectory, "assets", "products", "merchant-slice.json"),
            SimT.Contracts.Time.GameDateCodec.Parse(constants.FallbackDate));
    }

    private static KernelOptions Options(string campaignDirectory) => new(
        DatabasePath: Path.Combine(campaignDirectory, "campaign.db"),
        ModulesDirectory: Path.Combine(AppContext.BaseDirectory, "Modules"),
        ProductPath: Path.Combine(AppContext.BaseDirectory, "assets", "products", "merchant-slice.json"),
        ConstantsPath: Path.Combine(AppContext.BaseDirectory, "assets", "config", "game-constants.json"),
        BackupDirectory: Path.Combine(campaignDirectory, "backups"));
}
