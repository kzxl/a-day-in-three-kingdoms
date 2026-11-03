using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Time;

namespace SimT.Core.Ledger;

/// <summary>
/// K3 — the single cash writer. Every module charges and pays through this; the architecture
/// gate rejects any <c>INSERT INTO core_ledger</c> outside this file.
/// </summary>
public sealed class SqliteLedger : ILedger
{
    private const string Cash = "cash";
    private const string Vault = "vault";

    private readonly IDbSession _db;

    public SqliteLedger(IDbSession db) => _db = db;

    public long Balance => Liquid + Safe;

    public long Liquid => _db.ScalarLong("SELECT COALESCE(SUM(direction * amount), 0) FROM core_ledger WHERE account=@account", DbArgs.One("@account", Cash));

    public long Safe => _db.ScalarLong("SELECT COALESCE(SUM(direction * amount), 0) FROM core_ledger WHERE account=@account", DbArgs.One("@account", Vault));

    public long Post(GameDate date, LedgerEntry entry) => PostTo(date, entry, Cash);

    public long Move(GameDate date, long amount, bool toSafe, string journalCode, string description)
    {
        if (amount <= 0)
        {
            throw new SimException("ledger.invalid_amount", $"Vault move must be positive, got {amount}.");
        }

        long balance = 0;
        _db.Transaction(() =>
        {
            PostTo(date, new LedgerEntry(journalCode, toSafe ? LedgerDirection.Out : LedgerDirection.In, amount, description), Cash);
            balance = PostTo(date, new LedgerEntry(journalCode, toSafe ? LedgerDirection.In : LedgerDirection.Out, amount, description), Vault);
        });

        return balance;
    }

    public IReadOnlyList<LedgerLine> Recent(int limit = 50)
    {
        var rows = _db.Query(
            """
            SELECT id, game_date, journal_code, direction, amount, balance_after, description, module_id
            FROM core_ledger
            ORDER BY id DESC
            LIMIT @limit
            """,
            Map,
            DbArgs.One("@limit", Math.Max(1, limit)));

        rows.Reverse();
        return rows;
    }

    public IReadOnlyList<LedgerLine> Query(
        GameDate from,
        GameDate to,
        string? journalCode = null,
        string? moduleId = null,
        int limit = 500)
    {
        var sql = new System.Text.StringBuilder(
            """
            SELECT id, game_date, journal_code, direction, amount, balance_after, description, module_id
            FROM core_ledger
            WHERE day_index BETWEEN @from AND @to
            """);

        var args = new DbArgs().Add("@from", from.DayIndex).Add("@to", to.DayIndex).Add("@limit", Math.Max(1, limit));

        if (journalCode is not null)
        {
            sql.Append(" AND journal_code = @journal");
            args.Add("@journal", journalCode);
        }

        if (moduleId is not null)
        {
            sql.Append(" AND module_id = @module");
            args.Add("@module", moduleId);
        }

        sql.Append(" ORDER BY id ASC LIMIT @limit");
        return _db.Query(sql.ToString(), Map, args);
    }

    private static readonly Func<DbRecord, LedgerLine> Map = row => new LedgerLine(
        row.GetInt64(0),
        ParseDate(row.GetString(1)),
        row.GetString(2),
        row.GetInt64(3) > 0 ? LedgerDirection.In : LedgerDirection.Out,
        row.GetInt64(4),
        row.GetInt64(5),
        row.GetString(6),
        row.GetString(7));

    private long PostTo(GameDate date, LedgerEntry entry, string account)
    {
        var moduleId = JournalCodes.ModuleOf(entry.JournalCode);
        if (entry.Amount <= 0)
        {
            throw new SimException("ledger.invalid_amount", $"Journal '{entry.JournalCode}' posts amount {entry.Amount}; amounts must be positive.");
        }

        // Last line of defence: a module that forgets to check its own funds still cannot push
        // the ledger into a negative balance.
        if (entry.Direction == LedgerDirection.Out)
        {
            var available = account == Vault ? Safe : Liquid;
            if (available < entry.Amount)
            {
                throw new SimException(
                    "ledger.overdraft",
                    $"Journal '{entry.JournalCode}' would spend {entry.Amount} from {account} but only {available} is available.");
            }
        }

        var balanceAfter = LastBalance() + entry.Signed;

        _db.Execute(
            """
            INSERT INTO core_ledger(day_index, game_date, account, journal_code, direction, amount, balance_after, description, module_id)
            VALUES(@day, @date, @account, @journal, @direction, @amount, @balance, @description, @module)
            """,
            new DbArgs()
                .Add("@day", date.DayIndex)
                .Add("@date", date.ToString())
                .Add("@account", account)
                .Add("@journal", entry.JournalCode)
                .Add("@direction", (int)entry.Direction)
                .Add("@amount", entry.Amount)
                .Add("@balance", balanceAfter)
                .Add("@description", entry.Description)
                .Add("@module", moduleId));

        return balanceAfter;
    }

    private long LastBalance() =>
        _db.ScalarLong("SELECT COALESCE((SELECT balance_after FROM core_ledger ORDER BY id DESC LIMIT 1), 0)");

    private static GameDate ParseDate(string stored)
    {
        var parts = stored.Split('-');
        return new GameDate(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }
}
