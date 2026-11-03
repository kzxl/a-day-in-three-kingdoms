namespace SimT.Contracts.Playing;

/// <summary>Read surface of <c>dlc.lineage</c>, provided as the <c>lineage.house</c> capability.</summary>
public interface ILineageHouse
{
    bool IsMarried { get; }

    string? SpouseName { get; }

    int ChildCount { get; }

    string? HeirName { get; }

    long MonthlyUpkeep { get; }
}
