namespace SimT.Contracts.Playing;

/// <summary>Read surface of <c>dlc.professions</c>, provided as the <c>professions.guild</c> capability.</summary>
public interface IProfessionGuild
{
    IReadOnlyList<TradeDefinition> Trades { get; }

    int Tier(long tradeId);

    int PracticeCount(long tradeId);
}

public sealed record TradeDefinition(
    long Id,
    string Code,
    string NameVi,
    string NameEn,
    long OutputItemId,
    int DaysPerBatch,
    int BaseQuantity,
    long ToolFee);
