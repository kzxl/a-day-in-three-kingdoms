using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Professions;

/// <summary>
/// Day labour. A batch costs the trade's tool fee, consumes its days through the kernel clock
/// and yields goods into the current city's bag. Practice raises the tier, which raises yield,
/// so working a trade is an investment rather than a flat loop.
/// </summary>
internal sealed class ProfessionService : IProfessionGuild
{
    public const int PracticesPerTier = 4;
    public const int MaxTier = 5;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly IItemCatalog _items;
    private readonly ILog _log;

    public ProfessionService(IDbSession db, IGameClock clock, IWallet wallet, IBag bag, IItemCatalog items, ILog log)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _items = items;
        _log = log;
        Trades = LoadTrades(db);
    }

    public IReadOnlyList<TradeDefinition> Trades { get; }

    /// <summary>Boot-time validation: every output must exist in the shared catalog.</summary>
    public void ValidateCatalog()
    {
        foreach (var trade in Trades)
        {
            if (_items.Find(trade.OutputItemId) is null)
            {
                throw new SimException(
                    "professions.output_missing",
                    $"Trade '{trade.Code}' yields item {trade.OutputItemId}, which is not in the catalog.");
            }
        }
    }

    public int Tier(long tradeId) =>
        (int)_db.ScalarLong("SELECT COALESCE(tier, 0) FROM pf_skill WHERE trade_id=@trade", DbArgs.One("@trade", tradeId));

    public int PracticeCount(long tradeId) =>
        (int)_db.ScalarLong("SELECT COALESCE(practice_count, 0) FROM pf_skill WHERE trade_id=@trade", DbArgs.One("@trade", tradeId));

    public WorkResult Work(long cityId, long tradeId)
    {
        var trade = Trades.FirstOrDefault(candidate => candidate.Id == tradeId);
        if (trade is null)
        {
            return WorkResult.Failure("Nghề này không tồn tại.");
        }

        if (_wallet.Liquid < trade.ToolFee)
        {
            return WorkResult.Failure($"Cần {trade.ToolFee} Quan tiền dụng cụ, chỉ có {_wallet.Liquid}.");
        }

        var output = _items.Find(trade.OutputItemId);
        if (output is null)
        {
            throw new SimException("professions.output_missing", $"Trade '{trade.Code}' has no catalog entry.");
        }

        var tierBefore = Tier(tradeId);
        var quantity = trade.BaseQuantity + tierBefore;
        var tierAfter = 0;

        _db.Transaction(() =>
        {
            _wallet.Charge(trade.ToolFee, JournalCodes.ToolFee, $"Dụng cụ nghề {trade.NameVi}");
            _clock.Advance(trade.DaysPerBatch);
            _bag.Add(cityId, trade.OutputItemId, quantity);

            var practice = PracticeCount(tradeId) + 1;
            tierAfter = Math.Min(MaxTier, practice / PracticesPerTier);

            _db.Execute(
                """
                INSERT INTO pf_skill(trade_id, practice_count, tier) VALUES(@trade, @practice, @tier)
                ON CONFLICT(trade_id) DO UPDATE SET practice_count = excluded.practice_count, tier = excluded.tier
                """,
                new DbArgs().Add("@trade", tradeId).Add("@practice", practice).Add("@tier", tierAfter));
        });

        _log.Info($"worked {trade.Code}: +{quantity} {output.Code}, tier {tierBefore} -> {tierAfter}");

        var tierNote = tierAfter > tierBefore ? $", tay nghề lên bậc {tierAfter}" : string.Empty;
        return WorkResult.Success(
            $"{trade.DaysPerBatch} ngày làm {trade.NameVi}: +{quantity} {output.NameVi} (phí {trade.ToolFee} Quan){tierNote}.",
            quantity,
            trade.DaysPerBatch);
    }

    private static IReadOnlyList<TradeDefinition> LoadTrades(IDbSession db) =>
        db.Query(
            """
            SELECT id, code, name_vi, name_en, output_item_id, days_per_batch, base_quantity, tool_fee
            FROM pf_trade
            ORDER BY id
            """,
            row => new TradeDefinition(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt64(7)));
}

public readonly record struct WorkResult(bool Ok, string Message, int Quantity, int Days)
{
    public static WorkResult Success(string message, int quantity, int days) => new(true, message, quantity, days);

    public static WorkResult Failure(string message) => new(false, message, 0, 0);
}
