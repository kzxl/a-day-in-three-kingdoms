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

/// <summary>Read & action surface of <c>dlc.lineage</c>, provided as the <c>lineage.house</c> capability.</summary>
public interface ILineageHouse
{
    bool IsMarried { get; }

    string? SpouseName { get; }

    int ChildCount { get; }

    string? HeirName { get; }

    long MonthlyUpkeep { get; }

    int ClanPrestige { get; }

    int ShrineLevel { get; }

    IReadOnlyList<ChildDef> Children { get; }

    LineageResult Marry();

    LineageResult Beget();

    LineageResult DesignateHeir(long childId);

    LineageResult UpgradeShrine();

    LineageResult HoldOffering();

    LineageResult AssignTutor(long childId, string mentorName, string focusStat);

    LineageResult TeachChild(long childId);
}
