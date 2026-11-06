using SimT.Contracts.Logging;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy.Banking;

public sealed record BankAccountDef(
    long CitadelId,
    long DepositBalance,
    long LoanBalance,
    int LoanDueDay);

public sealed record BankDraftDef(
    long Id,
    string SerialNumber,
    long FaceValue,
    int IssuedDay,
    long IssuedCitadelId,
    bool Redeemed);

public sealed record BankingOperationResult(bool Success, string Message, long Amount = 0);

/// <summary>
/// Ancient Banking & Credit Service (Tiền Trang & Ngân Hiệu Hối Đoái).
/// Manages deposits, commercial credit loans, and weightless silver drafts.
/// </summary>
public sealed class BankingService
{
    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly ILog _log;

    public const int DraftFeePercent = 2;
    public const int DepositMonthlyInterestPermille = 15; // 1.5% per month
    public const int LoanMonthlyInterestPermille = 50;     // 5.0% per month
    public const long BaseMaxCreditLimit = 2000;          // Base credit line in Quan

    public BankingService(IDbSession db, IGameClock clock, IWallet wallet, ILog log)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _log = log;
    }

    public BankAccountDef GetAccount(long citadelId)
    {
        var existing = _db.Query(
            """
            SELECT citadel_id, deposit_balance, loan_balance, loan_due_day
            FROM mk_bank_account
            WHERE citadel_id = @city
            """,
            row => new BankAccountDef(row.GetInt64(0), row.GetInt64(1), row.GetInt64(2), row.GetInt32(3)),
            DbArgs.One("@city", citadelId)).FirstOrDefault();

        if (existing is not null)
        {
            return existing;
        }

        _db.Execute(
            "INSERT OR IGNORE INTO mk_bank_account(citadel_id, deposit_balance, loan_balance, loan_due_day) VALUES(@city, 0, 0, 0)",
            DbArgs.One("@city", citadelId));

        return new BankAccountDef(citadelId, 0, 0, 0);
    }

    public BankingOperationResult Deposit(long citadelId, long amount)
    {
        if (amount <= 0) return new(false, "Số lượng ngân lượng gửi phải lớn hơn 0.");
        if (_wallet.Liquid < amount) return new(false, $"Ngân lượng trong tay không đủ (cần {amount} Quan, có {_wallet.Liquid} Quan).");

        _db.Transaction(() =>
        {
            _wallet.Charge(amount, "banking.deposit", $"Gửi {amount} Quan vào Tiền Trang.");
            _db.Execute(
                """
                INSERT INTO mk_bank_account(citadel_id, deposit_balance, loan_balance, loan_due_day)
                VALUES(@city, @amt, 0, 0)
                ON CONFLICT(citadel_id) DO UPDATE SET deposit_balance = deposit_balance + @amt
                """,
                new DbArgs().Add("@city", citadelId).Add("@amt", amount));
        });

        _log.Info($"Đã gửi {amount} Quan vào Tiền Trang.");
        return new(true, $"Đã gửi thành công {amount} Quan vào Tiền Trang. Lãi suất tiền gửi 1.5%/tháng.", amount);
    }

    public BankingOperationResult Withdraw(long citadelId, long amount)
    {
        if (amount <= 0) return new(false, "Số lượng ngân lượng rút phải lớn hơn 0.");
        var acc = GetAccount(citadelId);
        if (acc.DepositBalance < amount) return new(false, $"Số dư tiền gửi tại chi nhánh này không đủ (có {acc.DepositBalance} Quan).");

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE mk_bank_account SET deposit_balance = deposit_balance - @amt WHERE citadel_id = @city",
                new DbArgs().Add("@city", citadelId).Add("@amt", amount));
            _wallet.Pay(amount, "banking.withdraw", $"Rút {amount} Quan từ Tiền Trang.");
        });

        _log.Info($"Đã rút {amount} Quan từ Tiền Trang.");
        return new(true, $"Đã rút thành công {amount} Quan tiền mặt vào tay.", amount);
    }

    public BankingOperationResult IssueDraft(long citadelId, long faceValue)
    {
        if (faceValue <= 0) return new(false, "Mệnh giá ngân phiếu phải lớn hơn 0.");
        var fee = Math.Max(1, faceValue * DraftFeePercent / 100);
        var totalNeeded = faceValue + fee;

        if (_wallet.Liquid < totalNeeded)
        {
            return new(false, $"Cần {totalNeeded} Quan (gồm {faceValue} Quan mệnh giá + {fee} Quan lệ phí phát hành 2%), chỉ có {_wallet.Liquid} Quan.");
        }

        var serialNumber = $"NP-{_clock.Today.Year % 100}{_clock.Today.Month:00}{_clock.Today.Day:00}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        _db.Transaction(() =>
        {
            _wallet.Charge(totalNeeded, "banking.issue_draft", $"Phát hành Ngân Phiếu {serialNumber} mệnh giá {faceValue} Quan.");
            _db.Execute(
                """
                INSERT INTO mk_bank_draft(serial_number, face_value, issued_day, issued_citadel_id, redeemed)
                VALUES(@sn, @val, @day, @city, 0)
                """,
                new DbArgs()
                    .Add("@sn", serialNumber)
                    .Add("@val", faceValue)
                    .Add("@day", _clock.Today.DayIndex)
                    .Add("@city", citadelId));
        });

        _log.Info($"Đã phát hành Ngân Phiếu {serialNumber} trị giá {faceValue} Quan.");
        return new(true, $"Đã nhận [Ngân Phiếu {serialNumber}] mệnh giá {faceValue} Quan (không trọng lượng, không lo cướp bóc).", faceValue);
    }

    public BankingOperationResult RedeemDraft(long citadelId, string serialNumber)
    {
        var draft = _db.Query(
            "SELECT id, serial_number, face_value, issued_day, issued_citadel_id, redeemed FROM mk_bank_draft WHERE serial_number = @sn",
            row => new BankDraftDef(row.GetInt64(0), row.GetString(1), row.GetInt64(2), row.GetInt32(3), row.GetInt64(4), row.GetInt32(5) == 1),
            DbArgs.One("@sn", serialNumber)).FirstOrDefault();

        if (draft is null) return new(false, "Không tìm thấy Ngân Phiếu này.");
        if (draft.Redeemed) return new(false, "Ngân Phiếu này đã được thanh toán đổi tiền trước đó.");

        _db.Transaction(() =>
        {
            _db.Execute("UPDATE mk_bank_draft SET redeemed = 1 WHERE id = @id", DbArgs.One("@id", draft.Id));
            _wallet.Pay(draft.FaceValue, "banking.redeem_draft", $"Đổi Ngân Phiếu {draft.SerialNumber} lấy tiền mặt.");
        });

        _log.Info($"Đã thanh toán Ngân Phiếu {draft.SerialNumber} nhận {draft.FaceValue} Quan.");
        return new(true, $"Đổi thành công Ngân Phiếu {draft.SerialNumber}, nhận {draft.FaceValue} Quan tiền mặt vào hầu bao.", draft.FaceValue);
    }

    public BankingOperationResult BorrowLoan(long citadelId, long amount)
    {
        if (amount <= 0) return new(false, "Số lượng ngân lượng vay phải lớn hơn 0.");
        var acc = GetAccount(citadelId);
        var totalCurrentLoan = TotalActiveLoans();

        if (totalCurrentLoan + amount > BaseMaxCreditLimit)
        {
            return new(false, $"Vượt quá hạn mức tín dụng thương nghiệp! Hạn mức tối đa: {BaseMaxCreditLimit} Quan, hiện đang nợ: {totalCurrentLoan} Quan.");
        }

        var dueDay = _clock.Today.DayIndex + 60; // Hạn 60 ngày

        _db.Transaction(() =>
        {
            _db.Execute(
                """
                INSERT INTO mk_bank_account(citadel_id, deposit_balance, loan_balance, loan_due_day)
                VALUES(@city, 0, @amt, @due)
                ON CONFLICT(citadel_id) DO UPDATE SET loan_balance = loan_balance + @amt, loan_due_day = @due
                """,
                new DbArgs().Add("@city", citadelId).Add("@amt", amount).Add("@due", dueDay));
            _wallet.Pay(amount, "banking.loan", $"Vay tín dụng Tiền Trang {amount} Quan.");
        });

        _log.Info($"Đã vay {amount} Quan từ Tiền Trang.");
        return new(true, $"Đã nhận khoản vay {amount} Quan. Lãi suất 5%/tháng, kỳ hạn thanh toán trong 60 ngày.", amount);
    }

    public BankingOperationResult RepayLoan(long citadelId, long amount)
    {
        if (amount <= 0) return new(false, "Số tiền trả nợ phải lớn hơn 0.");
        var acc = GetAccount(citadelId);
        if (acc.LoanBalance <= 0) return new(false, "Bạn không có khoản nợ nào tại chi nhánh này.");

        var repayAmount = Math.Min(amount, acc.LoanBalance);
        if (_wallet.Liquid < repayAmount) return new(false, $"Ngân lượng trong tay không đủ (cần {repayAmount} Quan, có {_wallet.Liquid} Quan).");

        _db.Transaction(() =>
        {
            _wallet.Charge(repayAmount, "banking.repay", $"Thanh toán nợ Tiền Trang {repayAmount} Quan.");
            _db.Execute(
                "UPDATE mk_bank_account SET loan_balance = loan_balance - @amt WHERE citadel_id = @city",
                new DbArgs().Add("@city", citadelId).Add("@amt", repayAmount));
        });

        _log.Info($"Đã thanh toán nợ {repayAmount} Quan.");
        return new(true, $"Đã thanh toán nợ thành công {repayAmount} Quan cho Tiền Trang.", repayAmount);
    }

    public IReadOnlyList<BankDraftDef> GetActiveDrafts()
    {
        return _db.Query(
            """
            SELECT id, serial_number, face_value, issued_day, issued_citadel_id, redeemed
            FROM mk_bank_draft
            WHERE redeemed = 0
            ORDER BY id DESC
            """,
            row => new BankDraftDef(row.GetInt64(0), row.GetString(1), row.GetInt64(2), row.GetInt32(3), row.GetInt64(4), false));
    }

    public long TotalActiveLoans()
    {
        return _db.ScalarLong("SELECT COALESCE(SUM(loan_balance), 0) FROM mk_bank_account");
    }

    public long TotalDeposits()
    {
        return _db.ScalarLong("SELECT COALESCE(SUM(deposit_balance), 0) FROM mk_bank_account");
    }

    public void ProcessMonthlyInterest()
    {
        // 1. Pay deposit interest: +1.5%
        _db.Execute(
            $"""
            UPDATE mk_bank_account
            SET deposit_balance = deposit_balance + (deposit_balance * {DepositMonthlyInterestPermille} / 1000)
            WHERE deposit_balance > 0
            """);

        // 2. Charge loan interest: +5.0%
        _db.Execute(
            $"""
            UPDATE mk_bank_account
            SET loan_balance = loan_balance + (loan_balance * {LoanMonthlyInterestPermille} / 1000)
            WHERE loan_balance > 0
            """);
    }
}
