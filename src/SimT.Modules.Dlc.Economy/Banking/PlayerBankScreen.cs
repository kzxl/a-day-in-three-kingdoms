using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Economy.Banking;

public sealed class PlayerBankScreen : IScreenContribution
{
    private readonly PlayerBankService _service;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly IWallet _wallet;
    private readonly IGameClock _clock;

    public PlayerBankScreen(
        PlayerBankService service,
        IWorldMap map,
        IPlayerState player,
        IWallet wallet,
        IGameClock clock)
    {
        _service = service;
        _map = map;
        _player = player;
        _wallet = wallet;
        _clock = clock;
    }

    public string ScreenId => "market.player_bank";
    public string Title => "Đại Tiền Trang Tư Doanh";
    public int Order => 29;

    public ScreenModel Render()
    {
        var cityId = _player.CurrentCityId;
        var city = _map.Find(cityId);
        var cityName = city?.NameVi ?? "Đô Thành";

        var bank = _service.GetBank();

        if (bank is null)
        {
            var emptyMetrics = new List<ScreenMetric>
            {
                new("Ngân quỹ ví", $"{_wallet.Liquid:N0}", "quan", MetricTone.Positive),
                new("Trạng thái", "Chưa thành lập", "cần 5.000 quan", MetricTone.Neutral),
                new("Thành trì", cityName, "khả dụng hội sở", MetricTone.Neutral)
            };

            var emptyCommands = new List<ScreenCommand>
            {
                new("establish", "Khai Nghiệp Đại Tiền Trang (5.000 Quan)", CommandKind.Primary)
            };

            var emptySections = new List<ScreenSection>
            {
                new("Hội Sở Ngân Hiệu", ["Quy Chế", "Yêu Cầu", "Đặc Quyền"],
                [
                    new ScreenRow(["Vốn Pháp Định Khởi Điểm", "5.000 Quan Bạc", "Khai trương Hội sở và két bạc trung ương"], []),
                    new ScreenRow(["Mạng Lưới Chi Nhánh", "2.000 Quan / Thành", "Mở rộng phân hiệu huy động tiền gửi và cho vay"], []),
                    new ScreenRow(["Tiêu Cục Áp Ngân", "Tự do điều phối", "Chuyển bạc thỏi an toàn giữa các đô thị"], [])
                ])
            };

            return new ScreenModel(
                ScreenId,
                $"{Title} — Khởi Sự Doanh Thương",
                "Bạn chưa sáng lập ngân hiệu tư doanh. Hãy chuẩn bị tối thiểu 5.000 Quan vốn pháp định để mở Tổng Hội Sở Tiền Trang!",
                emptySections,
                emptyCommands,
                emptyMetrics,
                Array.Empty<ScreenAlert>());
        }

        var branches = _service.GetBranches(bank.Id);
        var convoys = _service.GetConvoys(bank.Id);

        var totalVaultCash = branches.Sum(b => b.VaultCash);
        var totalNpcDeposits = branches.Sum(b => b.NpcDeposits);
        var totalNpcLoans = branches.Sum(b => b.NpcLoans);

        var metrics = new List<ScreenMetric>
        {
            new("Ngân hiệu", bank.BankName, $"Uy tín: {bank.Reputation}/100", MetricTone.Positive),
            new("Tổng Kim Khố", $"{totalVaultCash:N0}", "bạc thỏi toàn mạng lưới", MetricTone.Positive),
            new("Tiền gửi NPC", $"{totalNpcDeposits:N0}", "huy động dân chúng", MetricTone.Neutral),
            new("Cho NPC vay", $"{totalNpcLoans:N0}", "dư nợ thương nhân/lãnh chúa", MetricTone.Neutral),
            new("Chi nhánh", $"{branches.Count}", "thành trì", MetricTone.Neutral)
        };

        var sections = new List<ScreenSection>();

        // 1. Branch table
        var branchColumns = new[] { "Chi Nhánh", "Thành Trì", "Kim Khố Bạc Thỏi", "Dân Gửi Bạc", "Cho Vay Nợ", "Trạng Thái", "Hành Động" };
        var branchRows = new List<ScreenRow>();

        foreach (var b in branches)
        {
            var cit = _map.Find(b.CitadelId);
            var cName = cit?.NameVi ?? $"Thành #{b.CitadelId}";
            var statusVi = b.Status == "ACTIVE" ? "Hoạt động vững chắc" : "Bị rút vốn hoảng loạn!";

            var rowCommands = new List<ScreenCommand>
            {
                new($"vault_deposit:{b.Id}:1000", "Nạp 1.000q"),
                new($"vault_withdraw:{b.Id}:1000", "Rút 1.000q")
            };

            branchRows.Add(new ScreenRow(
                [
                    b.BranchName,
                    cName,
                    $"{b.VaultCash:N0} quan",
                    $"{b.NpcDeposits:N0} (Lãi {b.DepositRatePct}%)",
                    $"{b.NpcLoans:N0} (Lãi {b.LoanRatePct}%)",
                    statusVi,
                    "Điều chuyển kim khố"
                ],
                rowCommands,
                Title: b.BranchName,
                Details:
                [
                    new ScreenDetail("Thành trì", cName),
                    new ScreenDetail("Trữ lượng bạc", $"{b.VaultCash:N0} quan"),
                    new ScreenDetail("Dân cư ký thác", $"{b.NpcDeposits:N0} quan"),
                    new ScreenDetail("Cho vay thương gia", $"{b.NpcLoans:N0} quan")
                ]));
        }

        sections.Add(new ScreenSection($"🏛️ Mạng Lưới Chi Nhánh Tiền Trang ({branches.Count} đô thành)", branchColumns, branchRows));

        // 2. Convoys table
        if (convoys.Count > 0)
        {
            var convoyColumns = new[] { "Tuyến Đường", "Bạc Áp Tải", "Lực Lượng Tiêu Sư", "Tình Trạng", "Hành Động" };
            var convoyRows = new List<ScreenRow>();

            foreach (var c in convoys.Take(6))
            {
                var orig = _map.Find(c.OriginCitadelId)?.NameVi ?? $"#{c.OriginCitadelId}";
                var dest = _map.Find(c.DestCitadelId)?.NameVi ?? $"#{c.DestCitadelId}";
                var statusVi = c.Status == "DELIVERED" ? "Đã giao két an toàn" : $"Đang áp tải (Đến ngày {c.EtaDay})";

                convoyRows.Add(new ScreenRow(
                    [
                        $"{orig} ➔ {dest}",
                        $"{c.BullionAmount:N0} quan",
                        $"Bảo an cấp {c.EscortStrength}",
                        statusVi,
                        "Hộ tống"
                    ],
                    [],
                    Title: $"Tiêu Cục #{c.Id}",
                    Details:
                    [
                        new ScreenDetail("Nơi xuất phát", orig),
                        new ScreenDetail("Nơi tiếp nhận", dest),
                        new ScreenDetail("Số lượng bạc", $"{c.BullionAmount:N0} quan")
                    ]));
            }

            sections.Add(new ScreenSection($"🛡️ Đoàn Tiêu Cục Áp Tải Ngân Lượng ({convoys.Count} lượt vận hành)", convoyColumns, convoyRows));
        }

        var commands = new List<ScreenCommand>();
        var currentBranch = branches.FirstOrDefault(b => b.CitadelId == cityId);

        if (currentBranch is null)
        {
            commands.Add(new("open_branch:2000", $"Khai Trương Phân Hiệu Tại {cityName} (2.000 Quan)", CommandKind.Primary));
        }

        // Add dispatch convoy command if at least 2 branches exist
        if (branches.Count >= 2 && currentBranch != null && currentBranch.VaultCash >= 1000)
        {
            var otherBranch = branches.First(b => b.Id != currentBranch.Id);
            var otherCit = _map.Find(otherBranch.CitadelId)?.NameVi ?? "Phân Hiệu Khác";
            commands.Add(new($"dispatch:{currentBranch.Id}:{otherBranch.Id}:1000", $"Áp Tải 1.000 Quan Sang {otherCit}"));
        }

        return new ScreenModel(
            ScreenId,
            $"{Title} — {bank.BankName}",
            $"Ngân hiệu tư nhân do bạn sáng lập. Nắm giữ kim khố {totalVaultCash:N0} Quan, kiểm soát thanh khoản và điều phối dòng bạc thỏi khắp thiên hạ.",
            sections,
            commands,
            metrics,
            Array.Empty<ScreenAlert>());
    }
}

public sealed class PlayerBankCommandHandler : IScreenCommandHandler
{
    private readonly PlayerBankService _service;
    private readonly IPlayerState _player;

    public PlayerBankCommandHandler(PlayerBankService service, IPlayerState player)
    {
        _service = service;
        _player = player;
    }

    public string ScreenId => "market.player_bank";

    public string? Execute(string commandId)
    {
        var parts = commandId.Split(':');
        var action = parts[0].ToLowerInvariant();

        switch (action)
        {
            case "establish":
            {
                var res = _service.EstablishBank("Thiên Kim Ngân Hiệu", _player.CurrentCityId, 5000);
                return res.MessageVi;
            }

            case "open_branch":
            {
                var capital = parts.Length > 1 && long.TryParse(parts[1], out var c) ? c : 2000;
                var res = _service.OpenBranch(_player.CurrentCityId, "Phân Hiệu", capital);
                return res.MessageVi;
            }

            case "vault_deposit":
            {
                if (parts.Length > 2 && long.TryParse(parts[1], out var branchId) && long.TryParse(parts[2], out var amt))
                {
                    return _service.DepositToBranchVault(branchId, amt).MessageVi;
                }
                return "Lệnh nạp bạc vào két không đúng định dạng.";
            }

            case "vault_withdraw":
            {
                if (parts.Length > 2 && long.TryParse(parts[1], out var branchId) && long.TryParse(parts[2], out var amt))
                {
                    return _service.WithdrawFromBranchVault(branchId, amt).MessageVi;
                }
                return "Lệnh rút bạc từ két không đúng định dạng.";
            }

            case "dispatch":
            {
                if (parts.Length > 3 && long.TryParse(parts[1], out var origId) &&
                    long.TryParse(parts[2], out var destId) && long.TryParse(parts[3], out var amt))
                {
                    return _service.DispatchConvoy(origId, destId, amt).MessageVi;
                }
                return "Lệnh điều động tiêu cục không đúng định dạng.";
            }

            default:
                return $"Hành động '{action}' không được hỗ trợ tại Đại Tiền Trang.";
        }
    }
}
