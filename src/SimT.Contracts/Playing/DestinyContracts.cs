namespace SimT.Contracts.Playing;

/// <summary>
/// The six philosophical schools of thought and governance in the Three Kingdoms passive talent web.
/// </summary>
public enum DestinySchool
{
    Universal = 0,
    DaoChu = 1,    // Đào Chu Chi Thuật (Thương Mại & Đầu Cơ)
    BaDao = 2,     // Bá Đạo Pháp Gia (Đồn Điền & Kỷ Luật)
    VuongDao = 3,  // Vương Đạo Nho Gia (Nhân Tâm & Quan Trường)
    BinhGia = 4,   // Thiết Huyết Tiêu Cục (Vũ Lực & Sơn Trại)
    MacGia = 5,    // Bách Công Kỳ Xảo (Kho Vận & Kỹ Nghệ)
    DaoGia = 6     // Ẩn Sĩ Dưỡng Sinh (Y Thuật & Thiên Cơ)
}

/// <summary>
/// Tier classification for nodes on the passive talent web.
/// </summary>
public enum DestinyNodeTier
{
    Root = 0,
    Minor = 1,
    Notable = 2,
    Keystone = 3
}

/// <summary>
/// Static definition of a single node in the Bách Gia Thiên Mệnh passive skill tree.
/// </summary>
public sealed record DestinyNodeDef(
    string Id,
    DestinySchool School,
    DestinyNodeTier Tier,
    string NameVi,
    string SummaryVi,
    int PointCost,
    int RequiredSchoolPoints,
    IReadOnlyList<string> PrerequisiteIds,
    string ModCode,
    int ModValue,
    int GridX,
    int GridY);

/// <summary>
/// A one-time milestone achievement that grants Universal or School Resonance Destiny Points.
/// </summary>
public sealed record DestinyMilestoneDef(
    string Code,
    string Category,
    string NameVi,
    string DescriptionVi,
    int UniversalPoints,
    int ResonancePoints,
    DestinySchool ResonanceSchool,
    bool IsCompleted,
    int? CompletedDay);

/// <summary>
/// Runtime snapshot of the protagonist's learned destiny talents and available point budget.
/// </summary>
public sealed record DestinyState(
    int TotalPointsEarned,
    int PointsSpent,
    int UniversalPointsAvailable,
    IReadOnlyDictionary<DestinySchool, int> ResonancePointsAvailable,
    IReadOnlySet<string> LearnedNodeIds,
    IReadOnlyList<DestinyMilestoneDef> Milestones);

/// <summary>
/// Central contract for querying, allocating, and evaluating the Bách Gia Thiên Mệnh passive talent web.
/// </summary>
public interface IPlayerDestiny
{
    /// <summary>Current point budget, learned nodes, and milestone achievements.</summary>
    DestinyState State { get; }

    /// <summary>All nodes across all six schools in the passive graph.</summary>
    IReadOnlyList<DestinyNodeDef> AllNodes { get; }

    /// <summary>Validates if a node is currently eligible to be unlocked.</summary>
    bool CanLearnNode(string nodeId, out string? reasonVi);

    /// <summary>Unlocks a passive node if prerequisites and points are met.</summary>
    (bool Success, string MessageVi) LearnNode(string nodeId);

    /// <summary>Resets all allocated talent points back to available point pools.</summary>
    (bool Success, string MessageVi) RespecAll();

    /// <summary>Evaluates current campaign state against one-off milestones to grant destiny points.</summary>
    void EvaluateMilestones();

    /// <summary>Aggregates the total passive modifier value across all learned nodes for a given code.</summary>
    int GetModifier(string modCode);

    /// <summary>Checks whether a specific game-altering keystone is active.</summary>
    bool HasKeystone(string keystoneId);
}
