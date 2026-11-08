using SimT.Contracts.Scenes;

namespace SimT.Contracts.Playing;

/// <summary>
/// Canonical debate contexts supported by Batch 9 (Đợt 9 — Biện luận).
/// </summary>
public static class DebateTopics
{
    public const string Haggle = "haggle";
    public const string Recruit = "recruit";
    public const string Tax = "tax";

    public static string Normalize(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "recruit" or "retainer" or "gia_than" => Recruit,
        "tax" or "thue" or "giam_thue" => Tax,
        _ => Haggle
    };

    public static string TitleVi(string? topic) => Normalize(topic) switch
    {
        Recruit => "Thuyết phục làm gia thần",
        Tax => "Xin quan phủ giảm thuế",
        _ => "Mặc cả lô hàng"
    };
}

/// <summary>
/// Four argument categories drawn via <c>IRng</c> each turn in a debate.
/// </summary>
public enum DebateArgumentKind
{
    Ly = 1,
    Tinh = 2,
    Loi = 3,
    Uy = 4
}

/// <summary>
/// Outcome state of a 5-turn persuasion debate (0..100 bar, 20/80 thresholds).
/// </summary>
public enum DebateOutcomeKind
{
    Ongoing = 0,
    MajorWin = 1,
    MinorWin = 2,
    MinorLoss = 3,
    MajorLoss = 4,
    Conceded = 5
}

/// <summary>
/// One argument option presented to the player on a debate turn.
/// </summary>
public sealed record DebateArgumentView(
    string Id,
    DebateArgumentKind Kind,
    string KindLabelVi,
    string LineVi,
    string TagVi,
    int CostCopper,
    long CostItemId = 0,
    int CostItemQty = 0,
    bool Enabled = true,
    string? DisabledReasonVi = null);

/// <summary>
/// Snapshot of an ongoing or completed debate session.
/// </summary>
public sealed record DebateSessionView(
    string SessionId,
    long NpcId,
    string NpcNameVi,
    string Topic,
    string TopicTitleVi,
    long CityId,
    long TargetItemId,
    int TargetQuantity,
    int Turn,
    int MaxTurns,
    int Persuasion,
    DebateOutcomeKind Outcome,
    bool IsFinished,
    string LatestSpeakerVi,
    string LatestLineVi,
    string? PlayerLastLineVi,
    string? OpponentLastReplyVi,
    int LastPlayerPush,
    int LastOpponentCounter,
    double LastTraitMultiplier,
    bool OpponentDispleased,
    string? OutcomeTitleVi,
    string? ConsequenceVi,
    int PriceModifierPercent,
    int TaxReliefPercent,
    int RecruitFeeDiscountPercent,
    IReadOnlyList<DebateArgumentView> Arguments,
    IReadOnlyList<NpcTraitView> Traits);

/// <summary>
/// Capability exposed by <c>dlc.character</c> (<see cref="CapabilityIds.Debate"/>)
/// for running persuasion debates and querying their active economic/political effects.
/// </summary>
public interface IDebateService
{
    public const string CapabilityId = CapabilityIds.Debate;
    public const int CooldownDays = 7;
    public const int MaxTurns = 5;
    public const int StartPersuasion = 50;
    public const int WinThreshold = 80;
    public const int LossThreshold = 20;
    public const int TaxReliefDurationDays = 30;
    public const int MinHaggleLotQuantity = 20;

    DebateSessionView? ActiveSession { get; }

    bool CanDebate(long npcId, string topic, out string reasonVi);

    int DaysUntilDebateReady(long npcId, string topic);

    DebateSessionView StartDebate(long npcId, string topic, long itemId = 0, int quantity = MinHaggleLotQuantity);

    DebateSessionView StepDebate(string choiceId);

    DebateSessionView ChooseArgument(DebateArgumentKind kind);

    DebateSessionView Concede();

    DebateScene? BuildDebateScene(DebateSessionView? session = null);

    int GetHagglePriceModifierPercent(long cityId, long npcId, long itemId = 0);

    int GetCityBulkHaggleModifierPercent(long cityId, long itemId = 0);

    int GetRecruitFeeDiscountPercent(long npcId);

    bool HasPersuadedRetainer(long npcId);

    int GetTaxReliefPercent(long cityId);
}
