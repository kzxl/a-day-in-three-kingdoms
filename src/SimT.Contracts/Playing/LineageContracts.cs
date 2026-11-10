namespace SimT.Contracts.Playing;

public readonly record struct LineageResult(bool Ok, string Message)
{
    public static LineageResult Success(string message) => new(true, message);
    public static LineageResult Failure(string message) => new(false, message);
}

public sealed record ChildDef(
    long Id,
    string NameVi,
    int BornDay,
    int Age,
    bool IsAdult,
    bool IsHeir,
    string? MentorName,
    string? FocusStat,
    int StatBonus);

public sealed record PoliticalMarriageDef(
    long ChildId,
    string ChildNameVi,
    long FactionId,
    string FactionNameVi,
    string SpouseTitleVi,
    int MarriedDayIndex,
    int TariffDiscountPermille,
    int PrestigeBonus);

public sealed record GenerationRecordDef(
    int GenerationNumber,
    string PatriarchNameVi,
    long HeirChildId,
    int AscendedDayIndex,
    int InheritedPrestige,
    string ChronicleVi);

/// <summary>Read & action surface of <c>dlc.lineage</c>, provided as the <c>lineage.house</c> capability.</summary>
public interface ILineageHouse
{
    bool IsMarried { get; }

    string? SpouseName { get; }

    long? SpouseNpcId { get; }

    int ChildCount { get; }

    string? HeirName { get; }

    long MonthlyUpkeep { get; }

    int ClanPrestige { get; }

    int ShrineLevel { get; }

    IReadOnlyList<ChildDef> Children { get; }

    int CurrentGeneration => 1;

    int TotalTariffDiscountPermille => 0;

    IReadOnlyList<PoliticalMarriageDef> PoliticalMarriages => Array.Empty<PoliticalMarriageDef>();

    IReadOnlyList<GenerationRecordDef> GenerationHistory => Array.Empty<GenerationRecordDef>();

    LineageResult Marry();

    LineageResult Marry(long? npcId);

    LineageResult Beget();

    LineageResult DesignateHeir(long childId);

    LineageResult UpgradeShrine();

    LineageResult HoldOffering();

    LineageResult AssignTutor(long childId, string mentorName, string focusStat);

    LineageResult TeachChild(long childId);

    LineageResult ArrangePoliticalMarriage(long childId, long factionId) => LineageResult.Failure("Chưa hỗ trợ.");

    LineageResult PassTorchToHeir() => LineageResult.Failure("Chưa hỗ trợ.");
}
