namespace SimT.Contracts.Modules;

/// <summary>How a campaign starts, resolved from the product manifest by the kernel.</summary>
public sealed record CampaignStart(long Seed, string StartLocationCode, long StartingCash);
