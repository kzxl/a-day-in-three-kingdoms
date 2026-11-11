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
    string Description,
    string? IdempotencyKey = null)
{
    public long Signed => (long)Direction * Amount;
}

/// <summary>A persisted ledger line, including the running balance and the posting module.</summary>
public sealed record LedgerLine(
    long Id,
    GameDate Date,
    string JournalCode,
    LedgerDirection Direction,
    long Amount,
    long BalanceAfter,
    string Description,
    string ModuleId);

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

    /// <summary>
    /// Ledger lines in a date range, optionally narrowed to one journal code or one module.
    /// This is the read side reports and the ledger screen use; writing stays in <see cref="Post"/>.
    /// </summary>
    IReadOnlyList<LedgerLine> Query(
        GameDate from,
        GameDate to,
        string? journalCode = null,
        string? moduleId = null,
        int limit = 500);
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
    public const string StallRent = "MKT.STALL_RENT";
    public const string StallSale = "MKT.STALL_SALE";
    public const string CaravanFreight = "CV.FREIGHT";
    public const string CaravanReward = "CV.REWARD";
    public const string ToolFee = "PF.TOOL_FEE";
    public const string Dowry = "LN.DOWRY";
    public const string FamilyUpkeep = "LN.UPKEEP";
    public const string CourtService = "PO.SERVICE";
    public const string FarmRent = "FM.RENT";
    public const string FarmSeed = "FM.SEED";
    public const string CourtStipend = "PO.STIPEND";
    public const string ReputationCharity = "PL.CHARITY";
    public const string ReputationBanquet = "PL.BANQUET";
    public const string SocietySymposium = "SO.SYMPOSIUM";
    public const string SocietyTradeWar = "SO.TRADE_WAR";

    /// <summary>
    /// Which module a journal code belongs to. Attribution is by ownership of the code rather
    /// than by the caller, because every module pays through the same wallet — the useful
    /// question is "what did this feature cost", not "which object held the pen".
    /// </summary>
    public static string ModuleOf(string journalCode)
    {
        if (journalCode.StartsWith("CORE.START", StringComparison.Ordinal))
        {
            return "base.player";
        }

        if (journalCode.StartsWith("CORE.", StringComparison.Ordinal))
        {
            return "core";
        }

        return journalCode switch
        {
            var code when code.StartsWith("MKT.", StringComparison.Ordinal) => "dlc.economy",
            var code when code.StartsWith("CV.", StringComparison.Ordinal) => "dlc.economy",
            var code when code.StartsWith("PF.", StringComparison.Ordinal) => "dlc.economy",
            var code when code.StartsWith("FM.", StringComparison.Ordinal) => "dlc.economy",
            var code when code.StartsWith("LN.", StringComparison.Ordinal) => "dlc.character",
            var code when code.StartsWith("PO.", StringComparison.Ordinal) => "dlc.power",
            var code when code.StartsWith("SO.", StringComparison.Ordinal) => "dlc.society",
            var code when code.StartsWith("PL.", StringComparison.Ordinal) => "base.player",
            _ => "core"
        };
    }
}
