using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy.Banking;

public sealed record PlayerBankDef(
    long Id,
    long PlayerId,
    string BankName,
    long HeadquartersCitadelId,
    int Reputation,
    int ReserveRatioPct,
    long TotalIssuedDrafts,
    GameDate CreatedDate);

public sealed record PlayerBranchDef(
    long Id,
    long BankId,
    long CitadelId,
    string BranchName,
    long VaultCash,
    long NpcDeposits,
    long NpcLoans,
    long? ManagerNpcId,
    double DepositRatePct,
    double LoanRatePct,
    string Status,
    GameDate OpenedDate);

public sealed record PlayerConvoyDef(
    long Id,
    long BankId,
    long OriginCitadelId,
    long DestCitadelId,
    long BullionAmount,
    int DepartDay,
    int EtaDay,
    int EscortStrength,
    string Status,
    GameDate CreatedDate);

public sealed record PlayerBankResult(bool Success, string MessageVi, long Amount = 0);

/// <summary>
/// Autonomous Player Bank Network & Vault Logistics (Đại Tiền Trang & Mạng Lưới Chi Nhánh).
/// Governs capital reserves, credit pricing, branch expansion, and bullion convoys.
/// </summary>
public sealed class PlayerBankService
{
    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly ILog _log;

    public const long MinimumHqCapital = 5000;
    public const long MinimumBranchCapital = 2000;

    public PlayerBankService(IDbSession db, IGameClock clock, IWallet wallet, ILog log)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _log = log;
    }

    public PlayerBankDef? GetBank(long playerId = 1)
    {
        return _db.Query(
            """
            SELECT id, player_id, bank_name, headquarters_citadel_id, reputation, reserve_ratio_pct, total_issued_drafts, created_date
            FROM bk_player_bank
            WHERE player_id = @pid
            """,
            row => new PlayerBankDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetString(2),
                row.GetInt64(3),
                row.GetInt32(4),
                row.GetInt32(5),
                row.GetInt64(6),
                GameDateCodec.Parse(row.GetString(7))),
            DbArgs.One("@pid", playerId)).FirstOrDefault();
    }

    public IReadOnlyList<PlayerBranchDef> GetBranches(long bankId)
    {
        return _db.Query(
            """
            SELECT id, bank_id, citadel_id, branch_name, vault_cash, npc_deposits, npc_loans, manager_npc_id,
                   deposit_rate_pct, loan_rate_pct, status, opened_date
            FROM bk_player_branch
            WHERE bank_id = @bid
            ORDER BY id ASC
            """,
            row => new PlayerBranchDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt64(5),
                row.GetInt64(6),
                row.IsDbNull(7) ? null : row.GetInt64(7),
                row.GetDouble(8),
                row.GetDouble(9),
                row.GetString(10),
                GameDateCodec.Parse(row.GetString(11))),
            DbArgs.One("@bid", bankId));
    }

    public PlayerBranchDef? GetBranch(long bankId, long citadelId)
    {
        return _db.Query(
            """
            SELECT id, bank_id, citadel_id, branch_name, vault_cash, npc_deposits, npc_loans, manager_npc_id,
                   deposit_rate_pct, loan_rate_pct, status, opened_date
            FROM bk_player_branch
            WHERE bank_id = @bid AND citadel_id = @city
            """,
            row => new PlayerBranchDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt64(5),
                row.GetInt64(6),
                row.IsDbNull(7) ? null : row.GetInt64(7),
                row.GetDouble(8),
                row.GetDouble(9),
                row.GetString(10),
                GameDateCodec.Parse(row.GetString(11))),
            new DbArgs().Add("@bid", bankId).Add("@city", citadelId)).FirstOrDefault();
    }

    public IReadOnlyList<PlayerConvoyDef> GetConvoys(long bankId)
    {
        return _db.Query(
            """
            SELECT id, bank_id, origin_citadel_id, dest_citadel_id, bullion_amount, depart_day, eta_day, escort_strength, status, created_date
            FROM bk_player_convoy
            WHERE bank_id = @bid
            ORDER BY id DESC
            """,
            row => new PlayerConvoyDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetString(8),
                GameDateCodec.Parse(row.GetString(9))),
            DbArgs.One("@bid", bankId));
    }

    public PlayerBankResult EstablishBank(string bankName, long hqCitadelId, long initialVaultCash, long playerId = 1)
    {
        if (string.IsNullOrWhiteSpace(bankName))
        {
            return new PlayerBankResult(false, "Tên ngân hiệu không được để trống.");
        }

        if (initialVaultCash < MinimumHqCapital)
        {
            return new PlayerBankResult(false, $"Vốn pháp định khởi điểm tối thiểu là {MinimumHqCapital:N0} Quan.");
        }

        var existing = GetBank(playerId);
        if (existing is not null)
        {
            return new PlayerBankResult(false, $"Bạn đã sáng lập ngân hiệu '{existing.BankName}' rồi.");
        }

        if (_wallet.Liquid < initialVaultCash)
        {
            return new PlayerBankResult(false, $"Bạn không đủ ngân lượng ({_wallet.Liquid:N0}/{initialVaultCash:N0} Quan).");
        }

        var today = _clock.Today;
        var dateStr = GameDateCodec.Format(today);

        _wallet.Charge(initialVaultCash, "player_bank.establish", $"Vốn pháp định sáng lập ngân hiệu {bankName}.");

        _db.Execute(
            """
            INSERT INTO bk_player_bank(player_id, bank_name, headquarters_citadel_id, reputation, reserve_ratio_pct, total_issued_drafts, created_date)
            VALUES(@pid, @name, @city, 60, 40, 0, @date)
            """,
            new DbArgs()
                .Add("@pid", playerId)
                .Add("@name", bankName.Trim())
                .Add("@city", hqCitadelId)
                .Add("@date", dateStr));

        var bankId = _db.ScalarLong("SELECT last_insert_rowid()");

        // Create Headquarters branch
        _db.Execute(
            """
            INSERT INTO bk_player_branch(bank_id, citadel_id, branch_name, vault_cash, npc_deposits, npc_loans, manager_npc_id, deposit_rate_pct, loan_rate_pct, status, opened_date)
            VALUES(@bid, @city, @bname, @vault, 0, 0, NULL, 1.5, 4.0, 'ACTIVE', @date)
            """,
            new DbArgs()
                .Add("@bid", bankId)
                .Add("@city", hqCitadelId)
                .Add("@bname", $"{bankName} - Tổng Hiệu")
                .Add("@vault", initialVaultCash)
                .Add("@date", dateStr));

        _log.Info($"Thành lập Ngân Hiệu '{bankName}' tại thành #{hqCitadelId} với vốn {initialVaultCash:N0} Quan.");
        return new PlayerBankResult(true, $"Đại khai môn đình! Ngân hiệu '{bankName}' chính thức khai trương hội sở chính!", initialVaultCash);
    }

    public PlayerBankResult OpenBranch(long citadelId, string branchName, long initialCash, long? managerNpcId = null, long playerId = 1)
    {
        var bank = GetBank(playerId);
        if (bank is null)
        {
            return new PlayerBankResult(false, "Bạn chưa thành lập ngân hiệu.");
        }

        if (initialCash < MinimumBranchCapital)
        {
            return new PlayerBankResult(false, $"Vốn điều lệ mở chi nhánh tối thiểu là {MinimumBranchCapital:N0} Quan.");
        }

        var existingBranch = GetBranch(bank.Id, citadelId);
        if (existingBranch is not null)
        {
            return new PlayerBankResult(false, $"Thành trì này đã có chi nhánh '{existingBranch.BranchName}' đang hoạt động.");
        }

        if (_wallet.Liquid < initialCash)
        {
            return new PlayerBankResult(false, $"Ví của bạn không đủ ngân lượng ({_wallet.Liquid:N0}/{initialCash:N0} Quan).");
        }

        var today = _clock.Today;
        var dateStr = GameDateCodec.Format(today);

        _wallet.Charge(initialCash, "player_bank.open_branch", $"Vốn điều lệ mở phân hiệu tại thành #{citadelId}.");

        _db.Execute(
            """
            INSERT INTO bk_player_branch(bank_id, citadel_id, branch_name, vault_cash, npc_deposits, npc_loans, manager_npc_id, deposit_rate_pct, loan_rate_pct, status, opened_date)
            VALUES(@bid, @city, @bname, @vault, 0, 0, @mgr, 1.5, 4.0, 'ACTIVE', @date)
            """,
            new DbArgs()
                .Add("@bid", bank.Id)
                .Add("@city", citadelId)
                .Add("@bname", string.IsNullOrWhiteSpace(branchName) ? $"{bank.BankName} - Phân Hiệu" : branchName.Trim())
                .Add("@vault", initialCash)
                .Add("@mgr", managerNpcId)
                .Add("@date", dateStr));

        _log.Info($"Mở chi nhánh mới tại thành #{citadelId} với kim khố {initialCash:N0} Quan.");
        return new PlayerBankResult(true, $"Khai trương phân hiệu mới thành công tại thành #{citadelId}!", initialCash);
    }

    public PlayerBankResult DepositToBranchVault(long branchId, long amount)
    {
        if (amount <= 0) return new PlayerBankResult(false, "Số lượng ngân lượng gửi vào phải lớn hơn 0.");
        if (_wallet.Liquid < amount) return new PlayerBankResult(false, $"Ví không đủ tiền ({_wallet.Liquid:N0}/{amount:N0} Quan).");

        var branch = _db.Query(
            "SELECT id, vault_cash FROM bk_player_branch WHERE id = @id",
            r => (Id: r.GetInt64(0), Cash: r.GetInt64(1)),
            DbArgs.One("@id", branchId)).FirstOrDefault();

        if (branch.Id == 0) return new PlayerBankResult(false, "Chi nhánh không tồn tại.");

        _wallet.Charge(amount, "player_bank.vault_deposit", $"Nạp {amount} Quan vào kim khố chi nhánh.");
        _db.Execute("UPDATE bk_player_branch SET vault_cash = vault_cash + @amt WHERE id = @id",
            new DbArgs().Add("@amt", amount).Add("@id", branchId));

        return new PlayerBankResult(true, $"Đã nạp {amount:N0} Quan vào kim khố chi nhánh.", amount);
    }

    public PlayerBankResult WithdrawFromBranchVault(long branchId, long amount)
    {
        if (amount <= 0) return new PlayerBankResult(false, "Số lượng ngân lượng rút ra phải lớn hơn 0.");

        var branch = _db.Query(
            "SELECT id, vault_cash FROM bk_player_branch WHERE id = @id",
            r => (Id: r.GetInt64(0), Cash: r.GetInt64(1)),
            DbArgs.One("@id", branchId)).FirstOrDefault();

        if (branch.Id == 0) return new PlayerBankResult(false, "Chi nhánh không tồn tại.");
        if (branch.Cash < amount) return new PlayerBankResult(false, $"Kim khố chi nhánh không đủ bạc thỏi ({branch.Cash:N0}/{amount:N0} Quan).");

        _db.Execute("UPDATE bk_player_branch SET vault_cash = vault_cash - @amt WHERE id = @id",
            new DbArgs().Add("@amt", amount).Add("@id", branchId));
        _wallet.Pay(amount, "player_bank.vault_withdraw", $"Rút {amount} Quan từ kim khố chi nhánh về ví.");

        return new PlayerBankResult(true, $"Đã rút {amount:N0} Quan từ kim khố về ví của bạn.", amount);
    }

    public PlayerBankResult DispatchConvoy(long originBranchId, long destBranchId, long amount, int escortStrength = 50)
    {
        if (originBranchId == destBranchId) return new PlayerBankResult(false, "Không thể chuyển bạc đến cùng chi nhánh.");
        if (amount <= 0) return new PlayerBankResult(false, "Số bạc thỏi vận chuyển phải lớn hơn 0.");

        var origin = _db.Query(
            "SELECT id, bank_id, citadel_id, vault_cash FROM bk_player_branch WHERE id = @id",
            r => (Id: r.GetInt64(0), BankId: r.GetInt64(1), CitadelId: r.GetInt64(2), Cash: r.GetInt64(3)),
            DbArgs.One("@id", originBranchId)).FirstOrDefault();

        var dest = _db.Query(
            "SELECT id, bank_id, citadel_id FROM bk_player_branch WHERE id = @id",
            r => (Id: r.GetInt64(0), BankId: r.GetInt64(1), CitadelId: r.GetInt64(2)),
            DbArgs.One("@id", destBranchId)).FirstOrDefault();

        if (origin.Id == 0 || dest.Id == 0) return new PlayerBankResult(false, "Chi nhánh gửi hoặc nhận không hợp lệ.");
        if (origin.BankId != dest.BankId) return new PlayerBankResult(false, "Hai chi nhánh không cùng hệ thống ngân hiệu.");
        if (origin.Cash < amount) return new PlayerBankResult(false, $"Kim khố chi nhánh gốc không đủ bạc thỏi ({origin.Cash:N0}/{amount:N0} Quan).");

        var today = _clock.Today;
        var departDay = today.DayIndex;
        var travelDays = 3; // Standard inter-citadel logistics duration
        var etaDay = departDay + travelDays;

        _db.Execute("UPDATE bk_player_branch SET vault_cash = vault_cash - @amt WHERE id = @id",
            new DbArgs().Add("@amt", amount).Add("@id", originBranchId));

        _db.Execute(
            """
            INSERT INTO bk_player_convoy(bank_id, origin_citadel_id, dest_citadel_id, bullion_amount, depart_day, eta_day, escort_strength, status, created_date)
            VALUES(@bid, @orig, @dest, @amt, @dep, @eta, @esc, 'IN_TRANSIT', @date)
            """,
            new DbArgs()
                .Add("@bid", origin.BankId)
                .Add("@orig", origin.CitadelId)
                .Add("@dest", dest.CitadelId)
                .Add("@amt", amount)
                .Add("@dep", departDay)
                .Add("@eta", etaDay)
                .Add("@esc", escortStrength)
                .Add("@date", GameDateCodec.Format(today)));

        _log.Info($"Đoàn Tiêu Cục khởi hành từ thành #{origin.CitadelId} sang #{dest.CitadelId} áp tải {amount:N0} Quan bạc thỏi.");
        return new PlayerBankResult(true, $"Đoàn Tiêu Cục đã lên đường áp tải {amount:N0} Quan (Dự kiến đến sau {travelDays} ngày).", amount);
    }

    public void SimulateMonthlyTurn(GameDate date)
    {
        var banks = _db.Query(
            "SELECT id, reputation, reserve_ratio_pct FROM bk_player_bank",
            r => (Id: r.GetInt64(0), Rep: r.GetInt32(1), ReserveRatio: r.GetInt32(2)));

        var currentDay = date.DayIndex;

        foreach (var b in banks)
        {
            // 1. Deliver pending convoys
            var pendingConvoys = _db.Query(
                """
                SELECT id, dest_citadel_id, bullion_amount
                FROM bk_player_convoy
                WHERE bank_id = @bid AND status = 'IN_TRANSIT' AND @day >= eta_day
                """,
                r => (Id: r.GetInt64(0), DestCity: r.GetInt64(1), Amount: r.GetInt64(2)),
                new DbArgs().Add("@bid", b.Id).Add("@day", currentDay));

            foreach (var conv in pendingConvoys)
            {
                _db.Execute(
                    "UPDATE bk_player_branch SET vault_cash = vault_cash + @amt WHERE bank_id = @bid AND citadel_id = @city",
                    new DbArgs().Add("@amt", conv.Amount).Add("@bid", b.Id).Add("@city", conv.DestCity));

                _db.Execute("UPDATE bk_player_convoy SET status = 'DELIVERED' WHERE id = @id",
                    DbArgs.One("@id", conv.Id));
            }

            // 2. Process branch earnings & interest
            var branches = GetBranches(b.Id);
            foreach (var br in branches)
            {
                // Earn loan interest from NPCs
                var earnedLoanInterest = (long)(br.NpcLoans * (br.LoanRatePct / 100.0));
                // Pay deposit interest to NPCs
                var paidDepositInterest = (long)(br.NpcDeposits * (br.DepositRatePct / 100.0));
                var netInterest = earnedLoanInterest - paidDepositInterest;

                var newVault = br.VaultCash + netInterest;

                // Organic customer deposit & loan growth if branch is in good standing
                var newDeposits = br.NpcDeposits;
                var newLoans = br.NpcLoans;
                var status = br.Status;

                if (newVault > 0 && b.Rep >= 40)
                {
                    // Inflow of new local NPC deposits
                    var depositGrowth = Math.Max(100, (long)(newVault * 0.05));
                    newDeposits += depositGrowth;
                    newVault += depositGrowth;

                    // Commercial lending out to merchants & commanders
                    var loanGrowth = Math.Max(50, (long)(depositGrowth * 0.6));
                    if (newVault > loanGrowth)
                    {
                        newLoans += loanGrowth;
                        newVault -= loanGrowth;
                    }

                    status = "ACTIVE";
                }
                else if (newVault < 0)
                {
                    // Liquidity crunch / Bank run
                    status = "RUN_ON_BANK";
                    _db.Execute("UPDATE bk_player_bank SET reputation = MAX(0, reputation - 10) WHERE id = @bid",
                        DbArgs.One("@bid", b.Id));
                }

                _db.Execute(
                    """
                    UPDATE bk_player_branch
                    SET vault_cash = @vault, npc_deposits = @dep, npc_loans = @loan, status = @status
                    WHERE id = @id
                    """,
                    new DbArgs()
                        .Add("@vault", newVault)
                        .Add("@dep", newDeposits)
                        .Add("@loan", newLoans)
                        .Add("@status", status)
                        .Add("@id", br.Id));
            }
        }
    }
}
