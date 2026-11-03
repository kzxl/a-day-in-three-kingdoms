using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Politics;

/// <summary>
/// Standing at court: service earns reputation, reputation unlocks an office, and an office
/// pays a monthly stipend. One office at a time — the appointee must earn the next one.
/// </summary>
internal sealed class CourtService : IPoliticalCourt
{
    public const int ServiceDays = 3;
    public const int ServiceReputation = 2;
    public const long ServiceCost = 5;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly ILog _log;

    public CourtService(IDbSession db, IGameClock clock, IWallet wallet, ILog log)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _log = log;
    }

    public int Reputation => (int)State("reputation");

    public long OfficeId => State("office_id");

    public string? OfficeName =>
        _db.ScalarString("SELECT name_vi FROM po_office WHERE id=@id", DbArgs.One("@id", OfficeId));

    public long MonthlyStipend =>
        _db.ScalarLong("SELECT COALESCE(monthly_stipend, 0) FROM po_office WHERE id=@id", DbArgs.One("@id", OfficeId));

    public IReadOnlyList<OfficeView> Offices =>
        _db.Query(
            """
            SELECT id, code, name_vi, name_en, rank, monthly_stipend, reputation_required
            FROM po_office ORDER BY rank
            """,
            row => new OfficeView(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetString(3),
                row.GetInt32(4),
                row.GetInt64(5),
                row.GetInt32(6)));

    public CourtResult Serve()
    {
        if (_wallet.Liquid < ServiceCost)
        {
            return CourtResult.Failure($"Cần {ServiceCost} Quan lễ nhậm, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(ServiceCost, JournalCodes.CourtService, "Lễ nhậm cung sự");
            _clock.Advance(ServiceDays);
            SetState("reputation", Reputation + ServiceReputation);
        });

        return CourtResult.Success(
            $"Cung sự {ServiceDays} ngày: uy tín {Reputation}, tốn {ServiceCost} Quan.");
    }

    public CourtResult Apply(long officeId)
    {
        var office = Offices.FirstOrDefault(candidate => candidate.Id == officeId);
        if (office is null)
        {
            return CourtResult.Failure("Không có chức vụ này.");
        }

        if (OfficeId == officeId)
        {
            return CourtResult.Failure($"Đã tại chức {office.NameVi}.");
        }

        if (OfficeId != 0)
        {
            return CourtResult.Failure("Đang giữ một chức vụ khác, phải từ chức mới nhận được.");
        }

        if (Reputation < office.ReputationRequired)
        {
            return CourtResult.Failure(
                $"Cần {office.ReputationRequired} uy tín để nhận {office.NameVi}, hiện có {Reputation}.");
        }

        _db.Transaction(() => SetState("office_id", officeId));

        _log.Info($"appointed to {office.Code} with reputation {Reputation}");
        return CourtResult.Success(
            $"Được bổ nhiệm {office.NameVi} (phẩm {office.Rank}), bổng {office.MonthlyStipend} Quan/tháng.");
    }

    public CourtResult Resign()
    {
        if (OfficeId == 0)
        {
            return CourtResult.Failure("Chưa tại chức.");
        }

        var name = OfficeName;
        _db.Transaction(() => SetState("office_id", 0));
        return CourtResult.Success($"Từ chức {name}.");
    }

    /// <summary>Monthly stipend while in office. Real cashflow, paid at the boundary.</summary>
    public void PayStipend(GameDate date)
    {
        var stipend = MonthlyStipend;
        if (stipend <= 0)
        {
            return;
        }

        _wallet.Pay(stipend, JournalCodes.CourtStipend, $"Bổng lộc tháng {date.Month:00}/{date.Year}");
        _log.Info($"stipend {stipend} paid on {date}");
    }

    private long State(string key)
    {
        var value = _db.ScalarString("SELECT value FROM po_state WHERE key=@key", DbArgs.One("@key", key));
        if (value is null)
        {
            throw new SimException("politics.state_missing", $"po_state has no '{key}' row; migration 0700 did not run.");
        }

        return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private void SetState(string key, long value) =>
        _db.Execute(
            "INSERT INTO po_state(key, value) VALUES(@key, @value) ON CONFLICT(key) DO UPDATE SET value=excluded.value",
            new DbArgs().Add("@key", key).Add("@value", value));
}

public readonly record struct CourtResult(bool Ok, string Message)
{
    public static CourtResult Success(string message) => new(true, message);

    public static CourtResult Failure(string message) => new(false, message);
}
