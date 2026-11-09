using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;
using SimT.Modules.Dlc.Economy.Shocks;

namespace SimT.Modules.Dlc.Economy.Banking;

public sealed class BankScreen : IScreenContribution
{
    private readonly BankingService _banking;
    private readonly MarketShockEngine _shocks;
    private readonly IWorldMap _map;
    private readonly IItemCatalog _items;
    private readonly IMarketTrade _trade;
    private readonly IPlayerState _player;
    private readonly IWallet _wallet;
    private readonly IGameClock _clock;
    private long _cachedRadarDay = -1;
    private IReadOnlyList<ScreenRow>? _cachedRadarRows;

    public BankScreen(
        BankingService banking,
        MarketShockEngine shocks,
        IWorldMap map,
        IItemCatalog items,
        IMarketTrade trade,
        IPlayerState player,
        IWallet wallet,
        IGameClock clock)
    {
        _banking = banking;
        _shocks = shocks;
        _map = map;
        _items = items;
        _trade = trade;
        _player = player;
        _wallet = wallet;
        _clock = clock;
    }

    public string ScreenId => "market.bank";
    public string Title => "Tiền Trang Hối Đoái";
    public int Order => 28;

    public ScreenModel Render()
    {
        var cityId = _player.CurrentCityId;
        var city = _map.Find(cityId);
        var cityName = city?.NameVi ?? "Đô Thành";

        var account = _banking.GetAccount(cityId);
        var totalDeposits = _banking.TotalDeposits();
        var totalLoans = _banking.TotalActiveLoans();
        var drafts = _banking.GetActiveDrafts();

        var metrics = new List<ScreenMetric>
        {
            new("Tiền mặt", $"{_wallet.Liquid:N0}", "quan", MetricTone.Positive),
            new("Tiền gửi tại đây", $"{account.DepositBalance:N0}", "lãi 1.5%/tháng", MetricTone.Neutral),
            new("Dư nợ tín dụng", $"{account.LoanBalance:N0}", account.LoanBalance > 0 ? "lãi 5%/tháng" : "không nợ", account.LoanBalance > 0 ? MetricTone.Negative : MetricTone.Neutral),
            new("Tổng gửi toàn quốc", $"{totalDeposits:N0}", "quan", MetricTone.Neutral)
        };

        var alerts = new List<ScreenAlert>();
        if (account.LoanBalance > 0)
        {
            var daysLeft = Math.Max(0, account.LoanDueDay - _clock.Today.DayIndex);
            alerts.Add(new ScreenAlert($"Khoản vay tại {cityName} còn nợ {account.LoanBalance} Quan, hạn trả còn {daysLeft} ngày.", daysLeft < 10 ? AlertLevel.Critical : AlertLevel.Warning));
        }

        // Global commands
        var commands = new List<ScreenCommand>
        {
            new("deposit:100", "Gửi 100 Quan"),
            new("deposit:500", "Gửi 500 Quan"),
            new("withdraw:100", "Rút 100 Quan"),
            new("draft:500", "Phát hành Phiếu 500 Quan"),
            new("draft:2000", "Phát hành Phiếu 2.000 Quan"),
            new("borrow:500", "Vay 500 Quan"),
            new("repay:500", "Trả nợ 500 Quan")
        };

        var sections = new List<ScreenSection>();

        // 1. Bank Drafts held
        var draftColumns = new[] { "Số Hiệu Ngân Phiếu", "Mệnh Giá", "Nơi Phát Hành", "Ngày Lập", "Hành Động" };
        var draftRows = new List<ScreenRow>();
        foreach (var d in drafts)
        {
            var issuedCity = _map.Find(d.IssuedCitadelId)?.NameVi ?? "Kỳ Thành";
            draftRows.Add(new ScreenRow(
                [
                    d.SerialNumber,
                    $"{d.FaceValue:N0} quan",
                    issuedCity,
                    $"Ngày {d.IssuedDay}",
                    "Có thể đổi tiền mặt"
                ],
                [
                    new ScreenCommand($"redeem:{d.SerialNumber}", "Đổi Tiền Mặt", CommandKind.Primary)
                ],
                Title: d.SerialNumber,
                Details:
                [
                    new ScreenDetail("Mệnh giá", $"{d.FaceValue:N0} quan"),
                    new ScreenDetail("Nơi lập phiếu", issuedCity),
                    new ScreenDetail("Tính chất", "Vô trọng lượng, an toàn tuyệt đối khi du hành liên thành.")
                ]));
        }

        sections.Add(new ScreenSection($"📜 Ngân Phiếu Đang Sở Hữu ({drafts.Count} phiếu lưu thông)", draftColumns, draftRows, SectionViewMode.TreasuryVault));

        // 2. Arbitrage Intelligence Radar (computed once per game day)
        var todayIndex = _clock.Today.DayIndex;
        if (_cachedRadarRows is null || _cachedRadarDay != todayIndex)
        {
            var arbitrage = _shocks.ScanArbitrageCorridors(_map, _items, _trade, 6, _clock.Today);
            var radarRows = new List<ScreenRow>(arbitrage.Count);
            foreach (var opp in arbitrage)
            {
                radarRows.Add(new ScreenRow(
                    [
                        opp.ItemNameVi,
                        $"{opp.SourceCitadelName} ({opp.BuyPriceAtSource}q)",
                        $"{opp.TargetCitadelName} ({opp.SellPriceAtTarget}q)",
                        $"+{opp.ProfitPerUnit:N0} quan",
                        $"+{opp.ProfitMarginPercent}%",
                        opp.StrategicAdvice
                    ],
                    [],
                    Title: $"{opp.ItemNameVi}: {opp.SourceCitadelName} ➔ {opp.TargetCitadelName}",
                    Details:
                    [
                        new ScreenDetail("Mặt hàng", opp.ItemNameVi),
                        new ScreenDetail("Thành thu mua giá rẻ", $"{opp.SourceCitadelName} (Giá: {opp.BuyPriceAtSource} Quan)"),
                        new ScreenDetail("Thành tiêu thụ giá cao", $"{opp.TargetCitadelName} (Giá: {opp.SellPriceAtTarget} Quan)"),
                        new ScreenDetail("Lợi nhuận ròng", $"+{opp.ProfitPerUnit} Quan mỗi đơn vị hàng hóa"),
                        new ScreenDetail("Chiến lược đề xuất", opp.StrategicAdvice)
                    ]));
            }

            _cachedRadarDay = todayIndex;
            _cachedRadarRows = radarRows;
        }

        var radarColumns = new[] { "Hàng Hóa", "Thành Xuất Xứ (Giá Mua)", "Thành Tiêu Thụ (Giá Bán)", "Lợi Nhuận/Đvị", "Biên Lợi Nhuận", "Khuyến Nghị" };
        sections.Add(new ScreenSection($"📡 Thương Báo Tam Quốc — Radar Tuyến Buôn Chênh Lệch Lời Nhất", radarColumns, _cachedRadarRows, SectionViewMode.TreasuryVault));

        return new ScreenModel(
            ScreenId,
            $"{Title} — {cityName}",
            "Hệ thống tiền trang hối đoái liên thành: gửi tiết kiệm sinh lãi, phát hành ngân phiếu không sợ cướp bóc, vay tín dụng thương nghiệp và theo dõi mạng lưới tin tức chênh lệch giá toàn thiên hạ.",
            sections,
            commands,
            metrics,
            alerts);
    }
}

public sealed class BankCommandHandler : IScreenCommandHandler
{
    private readonly BankingService _banking;
    private readonly IPlayerState _player;

    public BankCommandHandler(BankingService banking, IPlayerState player)
    {
        _banking = banking;
        _player = player;
    }

    public string ScreenId => "market.bank";

    public string? Execute(string commandId)
    {
        var cityId = _player.CurrentCityId;
        var parts = commandId.Split(':');
        var action = parts[0].ToLowerInvariant();

        switch (action)
        {
            case "deposit":
                if (parts.Length > 1 && long.TryParse(parts[1], out var depAmt))
                {
                    return _banking.Deposit(cityId, depAmt).Message;
                }
                return "Cần chỉ định số tiền muốn gửi.";

            case "withdraw":
                if (parts.Length > 1 && long.TryParse(parts[1], out var withAmt))
                {
                    return _banking.Withdraw(cityId, withAmt).Message;
                }
                return "Cần chỉ định số tiền muốn rút.";

            case "draft":
                if (parts.Length > 1 && long.TryParse(parts[1], out var faceVal))
                {
                    return _banking.IssueDraft(cityId, faceVal).Message;
                }
                return "Cần chỉ định mệnh giá ngân phiếu.";

            case "redeem":
                if (parts.Length > 1)
                {
                    return _banking.RedeemDraft(cityId, parts[1]).Message;
                }
                return "Cần chỉ định số hiệu ngân phiếu cần đổi.";

            case "borrow":
                if (parts.Length > 1 && long.TryParse(parts[1], out var borrowAmt))
                {
                    return _banking.BorrowLoan(cityId, borrowAmt).Message;
                }
                return "Cần chỉ định số tiền muốn vay.";

            case "repay":
                if (parts.Length > 1 && long.TryParse(parts[1], out var repayAmt))
                {
                    return _banking.RepayLoan(cityId, repayAmt).Message;
                }
                return "Cần chỉ định số tiền muốn trả nợ.";

            default:
                return $"Hành động '{action}' không hợp lệ tại Tiền Trang.";
        }
    }
}
