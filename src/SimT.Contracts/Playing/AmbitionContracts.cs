namespace SimT.Contracts.Playing;

/// <summary>
/// The 5 grand ambition archetypes for endgame victory in the Three Kingdoms sandbox.
/// </summary>
public enum AmbitionCategory
{
    /// <summary>Kingmaker: Serve an emperor/warlord to achieve hegemony (Triều chính & Bá nghiệp).</summary>
    Kingmaker = 1,

    /// <summary>Merchant Sovereign: Monopolize interstate commerce and amass boundless fortune (Thương đạo & Phú hào).</summary>
    MerchantSovereign = 2,

    /// <summary>Dynasty Patriarch: Establish an illustrious aristocratic bloodline and expansive estate (Gia tộc & Điền trang).</summary>
    DynastyPatriarch = 3,

    /// <summary>Grand Martial Master: Legendary warlord and arena champion who cleanses the realm (Võ đạo & Sơn trại).</summary>
    GrandMartialMaster = 4,

    /// <summary>Renowned Sage: Revered philosopher, scholar, and diplomat loved by all (Bách gia & Danh sĩ).</summary>
    RenownedSage = 5
}

/// <summary>
/// A quantifiable requirement towards completing an ambition branch.
/// </summary>
public sealed record AmbitionRequirement(
    string Code,
    string LabelVi,
    string LabelEn,
    long CurrentValue,
    long TargetValue,
    string UnitVi,
    bool IsMet);

/// <summary>
/// Progress snapshot of a single ambition branch.
/// </summary>
public sealed record AmbitionBranch(
    AmbitionCategory Category,
    string TitleVi,
    string TitleEn,
    string SubtitleVi,
    string Icon,
    int Percent,
    bool IsFulfilled,
    IReadOnlyList<AmbitionRequirement> Requirements,
    string EpilogueTitleVi,
    string EpilogueNarrativeVi);

/// <summary>
/// Global state of all 5 endgame ambitions.
/// </summary>
public sealed record EndgameAmbitionState(
    IReadOnlyList<AmbitionBranch> Branches,
    int CompletedCount,
    bool HasAnyVictory);

/// <summary>
/// Contract for evaluating endgame ambitions and victory conditions.
/// </summary>
public interface IEndgameAmbitions
{
    EndgameAmbitionState Evaluate();
}
