namespace SimT.Contracts.Playing;

/// <summary>Summary of player's overall fame, titles, tiers, and moral alignment.</summary>
public sealed record ReputationStanding(
    int Fame,
    int Virtue,
    int Credibility,
    string TitleVi,
    string RankVi,
    int CurrentTier,
    int NextTierFame,
    int ProgressToNextTierPercent,
    string PerkSummaryVi);

/// <summary>A historical deed or achievement that altered the player's reputation or moral alignment.</summary>
public sealed record ReputationDeed(
    long Id,
    int DayIndex,
    string DateText,
    string Category,
    int FameDelta,
    int VirtueDelta,
    int CredibilityDelta,
    string TitleVi,
    string NarrativeVi);

/// <summary>Reputation and influence in one of the five strategic regions.</summary>
public sealed record RegionalReputation(
    string RegionCode,
    string RegionNameVi,
    int Fame,
    string StandingTierVi,
    string InfluenceSummaryVi);

/// <summary>Leaderboard entry comparing prominent Three Kingdoms figures with the player.</summary>
public sealed record FameLeaderboardEntry(
    int Rank,
    long CharacterId,
    string NameVi,
    string RoleOrTitle,
    string LocationName,
    int Fame,
    string RenownTierVi,
    bool IsPlayer);

/// <summary>Result of an interactive reputation deed or event.</summary>
public sealed record ReputationActionResult(
    bool Success,
    string MessageVi,
    int FameGain,
    int VirtueGain,
    int CostCopper);

/// <summary>
/// Three Kingdoms Reputation & Prestige System owned by <c>base.player</c>.
/// Governs world renown, regional influence, moral standing, and deeds chronicle.
/// </summary>
public interface IPlayerReputation
{
    ReputationStanding Standing { get; }

    IReadOnlyList<ReputationDeed> RecentDeeds(int limit = 20);

    IReadOnlyList<RegionalReputation> RegionalStandings { get; }

    IReadOnlyList<FameLeaderboardEntry> GetLeaderboard(int top = 10);

    void GainFame(
        int amount,
        string category,
        string titleVi,
        string narrativeVi,
        string? regionCode = null,
        int virtueDelta = 0,
        int credDelta = 0);

    ReputationActionResult PerformCharity(int donationCopper);

    ReputationActionResult HostBanquet(int costCopper);

    ReputationActionResult ProclaimDeeds();
}
