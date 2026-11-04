namespace SimT.Contracts.Products;

/// <summary>
/// A product composition: which modules are enabled for this installation and how a new
/// campaign starts. Adding a DLC = appending its id here. Removing it disables the module
/// while its tables and data stay in existing saves.
/// </summary>
public sealed record ProductManifest(
    string Id,
    string Name,
    IReadOnlyList<string> Modules,
    long Seed,
    string StartLocationCode,
    long StartingCash,
    Time.GameDate StartDate);

/// <summary>Per-campaign save metadata, stored next to the database file.</summary>
public sealed record CampaignMeta(
    string CampaignId,
    string Name,
    string CreatedUtc,
    string LastPlayedUtc,
    string GameDate,
    string SchemaVersion,
    IReadOnlyList<string> Modules)
{
    public static CampaignMeta New(string campaignId, string name, ProductManifest product) => new(
        campaignId,
        name,
        DateTime.UtcNow.ToString("O"),
        DateTime.UtcNow.ToString("O"),
        Time.GameDateCodec.Format(product.StartDate),
        "1",
        product.Modules);
}
