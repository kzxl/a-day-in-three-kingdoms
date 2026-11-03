using SimT.Contracts.Time;

namespace SimT.Contracts.Ledger;

/// <summary>Direction of a cash movement, signed so that a sum is the balance delta.</summary>
public enum LedgerDirection
{
    Out = -1,
    In = 1
}

/// <summary>
/// A single cash movement. Journal codes are data, not an enum, so a DLC can add its own
/// code without touching the kernel.
/// </summary>
public readonly record struct LedgerEntry(
    string JournalCode,
    LedgerDirection Direction,
    long Amount,
    string Description)
{
    public long Signed => (long)Direction * Amount;
}

/// <summary>A persisted ledger line, including the running balance.</summary>
public sealed record LedgerLine(
    long Id,
    GameDate Date,
    string JournalCode,
    LedgerDirection Direction,
    long Amount,
    long BalanceAfter,
    string Description);

/// <summary>
/// The single writer for cash. Modules must never <c>INSERT</c> into the ledger table;
/// the architecture gate fails the build if they try.
/// </summary>
public interface ILedger
{
    /// <summary>Liquid cash + vault. The definition is fixed by the legacy invariant.</summary>
    long Balance { get; }

    long Liquid { get; }

    long Safe { get; }

    long Post(GameDate date, LedgerEntry entry);

    long Move(GameDate date, long amount, bool toSafe, string journalCode, string description);

    IReadOnlyList<LedgerLine> Recent(int limit = 50);
}

/// <summary>Journal codes owned by the kernel and the base modules.</summary>
public static class JournalCodes
{
    public const string CampaignStart = "CORE.START";
    public const string StartingCash = "CORE.STARTING_CASH";
    public const string SafeDeposit = "CORE.SAFE_DEPOSIT";
    public const string SafeWithdraw = "CORE.SAFE_WITHDRAW";
    public const string MarketBuy = "MKT.BUY";
    public const string MarketSell = "MKT.SELL";
    public const string WarehouseRent = "MKT.WH_LEASE";
    public const string CaravanFreight = "CV.FREIGHT";
    public const string ToolFee = "PF.TOOL_FEE";
    public const string Dowry = "LN.DOWRY";
    public const string FamilyUpkeep = "LN.UPKEEP";
    public const string FarmRent = "FM.RENT";
    public const string FarmSeed = "FM.SEED";
    public const string CourtStipend = "PO.STIPEND";
}
