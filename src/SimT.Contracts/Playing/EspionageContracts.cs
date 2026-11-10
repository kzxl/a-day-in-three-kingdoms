namespace SimT.Contracts.Playing;

/// <summary>
/// Covert intelligence node (Tế Tác) stationed inside a citadel.
/// </summary>
public sealed record SpyNetworkNodeDef(
    long CitadelId,
    string CitadelNameVi,
    int IntelLevel,
    int InfiltratedDayIndex,
    string ActiveOperation,
    int SabotageCount);

/// <summary>
/// Ancient treatise or secret manual guarded by a Three Kingdoms hermit.
/// </summary>
public sealed record HermitTomeDef(
    string Code,
    string NameVi,
    string HermitNameVi,
    long CitadelId,
    string CitadelNameVi,
    string StatCode,
    int StatBonus,
    string SkillCode,
    bool IsAcquired,
    int AcquiredDayIndex);

/// <summary>
/// Covert intelligence network (Tế Tác) and hermit tome discovery surface owned by <c>dlc.society</c>.
/// </summary>
public interface IEspionageNetwork
{
    IReadOnlyList<SpyNetworkNodeDef> GetSpyNodes();

    SpyNetworkNodeDef GetSpyNode(long citadelId);

    string DeploySpyCell(long citadelId);

    string ExecuteCovertWhisper(long citadelId, string operationKind);

    IReadOnlyList<HermitTomeDef> GetHermitTomes();

    string SeekHermitTome(string tomeCode);
}
