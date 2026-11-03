using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Lineage;

public sealed record FamilyMember(long Id, string Name, string Role, int DayIndex, bool IsHeir);

/// <summary>
/// Household and succession. Marriage and childbirth are paid events, upkeep is a real monthly
/// cashflow, and exactly one heir may be designated — the database enforces the last one.
/// </summary>
internal sealed class LineageService : ILineageHouse
{
    public const long DowryCost = 300;
    public const long ChildCost = 50;
    public const long SpouseUpkeep = 40;
    public const long ChildUpkeep = 20;

    private static readonly string[] SpouseNames =
        ["Nguyễn Thị Lan", "Trần Thị Hoa", "Lê Thị Mai", "Phạm Thị Ngọc", "Đỗ Thị Thu"];

    private static readonly string[] ChildNames =
        ["An", "Bình", "Chi", "Dũng", "Hạnh", "Khang", "Linh", "Minh", "Nguyên", "Phúc"];

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IRng _rng;
    private readonly IIdGenerator _ids;
    private readonly ILog _log;

    public LineageService(IDbSession db, IGameClock clock, IWallet wallet, IRng rng, IIdGenerator ids, ILog log)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _rng = rng;
        _ids = ids;
        _log = log;
    }

    public bool IsMarried => _db.ScalarLong("SELECT COUNT(*) FROM ln_spouse") > 0;

    public string? SpouseName =>
        _db.ScalarString("SELECT name_vi FROM ln_spouse ORDER BY id LIMIT 1");

    public int ChildCount => (int)_db.ScalarLong("SELECT COUNT(*) FROM ln_child");

    public string? HeirName =>
        _db.ScalarString("SELECT name_vi FROM ln_child WHERE is_heir = 1 LIMIT 1");

    public long MonthlyUpkeep => IsMarried ? SpouseUpkeep + (ChildCount * ChildUpkeep) : 0;

    public IReadOnlyList<FamilyMember> Members()
    {
        var members = new List<FamilyMember>();

        var spouse = _db.QueryOne(
            "SELECT id, name_vi, married_day_index FROM ln_spouse ORDER BY id LIMIT 1",
            row => new FamilyMember(row.GetInt64(0), row.GetString(1), "Chính thất", row.GetInt32(2), false));

        if (spouse is not null)
        {
            members.Add(spouse);
        }

        members.AddRange(_db.Query(
            "SELECT id, name_vi, born_day_index, is_heir FROM ln_child ORDER BY born_day_index, id",
            row => new FamilyMember(
                row.GetInt64(0),
                row.GetString(1),
                "Tử tức",
                row.GetInt32(2),
                row.GetInt64(3) != 0)));

        return members;
    }

    public LineageResult Marry()
    {
        if (IsMarried)
        {
            return LineageResult.Failure("Đã có chính thất.");
        }

        if (_wallet.Liquid < DowryCost)
        {
            return LineageResult.Failure($"Cần {DowryCost} Quan sính lễ, chỉ có {_wallet.Liquid}.");
        }

        var name = SpouseNames[_rng.Next(0, SpouseNames.Length)];
        var day = _clock.Today.DayIndex;

        _db.Transaction(() =>
        {
            _wallet.Charge(DowryCost, JournalCodes.Dowry, $"Sính lễ cưới {name}");
            _db.Execute(
                "INSERT INTO ln_spouse(id, name_vi, dowry_paid, married_day_index) VALUES(@id, @name, @dowry, @day)",
                new DbArgs()
                    .Add("@id", _ids.Next("lineage.spouse"))
                    .Add("@name", name)
                    .Add("@dowry", DowryCost)
                    .Add("@day", day));
        });

        _log.Info($"married {name} for {DowryCost}");
        return LineageResult.Success($"Thành hôn với {name}, sính lễ {DowryCost} Quan.");
    }

    public LineageResult Beget()
    {
        if (!IsMarried)
        {
            return LineageResult.Failure("Chưa có chính thất thì chưa tính chuyện con cái.");
        }

        if (_wallet.Liquid < ChildCost)
        {
            return LineageResult.Failure($"Cần {ChildCost} Quan lo sinh nở, chỉ có {_wallet.Liquid}.");
        }

        var name = ChildNames[_rng.Next(0, ChildNames.Length)];
        var day = _clock.Today.DayIndex;

        _db.Transaction(() =>
        {
            _wallet.Charge(ChildCost, JournalCodes.FamilyUpkeep, $"Lo sinh nở cho {NameOfSpouse()}");
            _db.Execute(
                "INSERT INTO ln_child(id, name_vi, born_day_index, is_heir) VALUES(@id, @name, @day, 0)",
                new DbArgs().Add("@id", _ids.Next("lineage.child")).Add("@name", name).Add("@day", day));
        });

        _log.Info($"child {name} born on day {day}");
        return LineageResult.Success($"{name} chào đời, chi phí {ChildCost} Quan.");
    }

    public LineageResult DesignateHeir(long childId)
    {
        var exists = _db.ScalarLong("SELECT COUNT(*) FROM ln_child WHERE id=@id", DbArgs.One("@id", childId));
        if (exists == 0)
        {
            return LineageResult.Failure("Không có người con này trong gia phả.");
        }

        var name = _db.ScalarString("SELECT name_vi FROM ln_child WHERE id=@id", DbArgs.One("@id", childId));

        _db.Transaction(() =>
        {
            _db.Execute("UPDATE ln_child SET is_heir = 0 WHERE is_heir = 1");
            _db.Execute("UPDATE ln_child SET is_heir = 1 WHERE id=@id", DbArgs.One("@id", childId));
        });

        return LineageResult.Success($"{name} được lập làm người kế vị.");
    }

    /// <summary>Monthly household upkeep. Unaffordable months are logged, never silently free.</summary>
    public void ChargeMonthly(GameDate date)
    {
        var upkeep = MonthlyUpkeep;
        if (upkeep <= 0)
        {
            return;
        }

        if (_wallet.Liquid < upkeep)
        {
            _log.Warn($"Chưa trả được phí gia đình {upkeep} Quan ngày {date}.");
            return;
        }

        _wallet.Charge(upkeep, JournalCodes.FamilyUpkeep, $"Phí gia đình tháng {date.Month:00}/{date.Year}");
    }

    private string NameOfSpouse() => SpouseName ?? "chính thất";
}

public readonly record struct LineageResult(bool Ok, string Message)
{
    public static LineageResult Success(string message) => new(true, message);

    public static LineageResult Failure(string message) => new(false, message);
}
