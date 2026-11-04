using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Ui;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class PlayerReputationTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static IPlayerReputation GetReputation(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerReputation>(CapabilityIds.PlayerReputation);

    private static IPlayerAttributes GetAttrs(SimKernel kernel) =>
        kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

    private static IWallet GetWallet(SimKernel kernel) =>
        kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);

    [Fact]
    public void Protagonist_starts_with_tier_1_fame_and_canonical_standing()
    {
        using var kernel = _host.Boot();
        var rep = GetReputation(kernel);

        var standing = rep.Standing;
        Assert.Equal(100, standing.Fame);
        Assert.Equal(50, standing.Virtue);
        Assert.Equal(50, standing.Credibility);
        Assert.Equal(1, standing.CurrentTier);
        Assert.Equal("Hào Kiệt Mới Nổi", standing.TitleVi);
        Assert.Equal(300, standing.NextTierFame);
        Assert.True(standing.ProgressToNextTierPercent >= 0);

        var deeds = rep.RecentDeeds();
        Assert.NotEmpty(deeds);
        Assert.Equal("milestone", deeds[0].Category);
    }

    [Fact]
    public void GainFame_increments_fame_and_inserts_chronicle_deed()
    {
        using var kernel = _host.Boot();
        var rep = GetReputation(kernel);

        var initialCount = rep.RecentDeeds().Count;
        rep.GainFame(120, "trade", "Đại Hội Giao Thương", "Hoàn tất hợp đồng lớn", "trung_nguyen", virtueDelta: 5, credDelta: 10);

        var standing = rep.Standing;
        Assert.Equal(220, standing.Fame);
        Assert.Equal(55, standing.Virtue);
        Assert.Equal(60, standing.Credibility);

        var deeds = rep.RecentDeeds();
        Assert.Equal(initialCount + 1, deeds.Count);
        Assert.Equal("trade", deeds[0].Category);
        Assert.Equal(120, deeds[0].FameDelta);
        Assert.Equal("Đại Hội Giao Thương", deeds[0].TitleVi);
    }

    [Fact]
    public void Fame_ladder_advances_tier_and_perks()
    {
        using var kernel = _host.Boot();
        var rep = GetReputation(kernel);

        // Advance to Tier 2 (300+ fame)
        rep.GainFame(250, "military", "Bình Định Hà Lạc", "Tiêu trừ cường đạo");
        Assert.Equal(350, rep.Standing.Fame);
        Assert.Equal(2, rep.Standing.CurrentTier);
        Assert.Equal("Châu Huyện Chi Danh", rep.Standing.TitleVi);

        // Advance to Tier 3 (600+ fame)
        rep.GainFame(300, "court", "Triều Đình Trọng Đãi", "Được thăng phẩm hàm");
        Assert.Equal(650, rep.Standing.Fame);
        Assert.Equal(3, rep.Standing.CurrentTier);
        Assert.Equal("Danh Chấn Một Vùng", rep.Standing.TitleVi);
    }

    [Fact]
    public void PerformCharity_deducts_money_and_awards_virtue_and_fame()
    {
        using var kernel = _host.Boot();
        var rep = GetReputation(kernel);
        var wallet = GetWallet(kernel);

        var startBalance = wallet.Balance;
        var startFame = rep.Standing.Fame;
        var startVirtue = rep.Standing.Virtue;

        var result = rep.PerformCharity(50);
        Assert.True(result.Success);
        Assert.Equal(50, result.CostCopper);
        Assert.True(result.FameGain > 0);
        Assert.True(result.VirtueGain > 0);

        Assert.Equal(startBalance - 50, wallet.Balance);
        Assert.Equal(startFame + result.FameGain, rep.Standing.Fame);
        Assert.Equal(startVirtue + result.VirtueGain, rep.Standing.Virtue);
    }

    [Fact]
    public void HostBanquet_boosts_fame_and_regional_reputation()
    {
        using var kernel = _host.Boot();
        var rep = GetReputation(kernel);
        var wallet = GetWallet(kernel);

        var startBalance = wallet.Balance;
        var startFame = rep.Standing.Fame;

        var result = rep.HostBanquet(100);
        Assert.True(result.Success);
        Assert.Equal(startBalance - 100, wallet.Balance);
        Assert.Equal(startFame + result.FameGain, rep.Standing.Fame);

        var regions = rep.RegionalStandings;
        var tn = regions.First(r => r.RegionCode == "trung_nguyen");
        Assert.True(tn.Fame > 60);
    }

    [Fact]
    public void ProclaimDeeds_consumes_stamina_and_awards_fame()
    {
        using var kernel = _host.Boot();
        var rep = GetReputation(kernel);
        var attrs = GetAttrs(kernel);

        var startStamina = attrs.Vitals.CurrentStamina;
        var startFame = rep.Standing.Fame;

        var result = rep.ProclaimDeeds();
        Assert.True(result.Success);
        Assert.Equal(startFame + result.FameGain, rep.Standing.Fame);
        Assert.Equal(startStamina - 20, attrs.Vitals.CurrentStamina);
    }

    [Fact]
    public void Leaderboard_returns_notables_with_player_ranked()
    {
        using var kernel = _host.Boot();
        var rep = GetReputation(kernel);

        var board = rep.GetLeaderboard(10);
        Assert.NotEmpty(board);

        // Player or top notables exist
        var player = board.FirstOrDefault(e => e.IsPlayer);
        Assert.NotNull(player);
        Assert.Equal(100, player.Fame);
        Assert.True(player.Rank > 0);
    }

    [Fact]
    public void ReputationScreen_renders_all_four_sections_and_commands()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "player.reputation");
        Assert.NotNull(screen);

        var model = screen.Render();
        Assert.Equal("player.reputation", model.ScreenId);
        Assert.Equal("Uy Danh", model.Title);
        Assert.Equal(4, model.Headline.Count);
        Assert.Equal(4, model.Sections.Count);
        Assert.Equal(4, model.Commands.Count);

        Assert.Equal("CẤP BẬC DANH VỌNG & ĐẶC QUYỀN MỞ KHÓA", model.Sections[0].Heading);
        Assert.Equal("UY DANH THEO 5 ĐẠI KHU VỰC CHIẾN LƯỢC", model.Sections[1].Heading);
        Assert.Equal("BẢNG PHONG THẦN · XẾP HẠNG UY DANH THIÊN HẠ", model.Sections[2].Heading);
        Assert.Equal("NHẬT KÝ CHIẾN TÍCH & SỰ KIỆN RẠNG DANH", model.Sections[3].Heading);
    }

    [Fact]
    public void ReputationCommandHandler_executes_actions()
    {
        using var kernel = _host.Boot();
        Assert.True(kernel.Ui.TryGetCommandHandler("player.reputation", out var handler));
        Assert.NotNull(handler);

        var msg1 = handler.Execute("rep.charity_small");
        Assert.NotNull(msg1);
        Assert.Contains("Đã phát chẩn", msg1);

        var msg2 = handler.Execute("rep.banquet");
        Assert.NotNull(msg2);
        Assert.Contains("Đã tổ chức yến tiệc", msg2);

        var msg3 = handler.Execute("rep.proclaim");
        Assert.NotNull(msg3);
        Assert.Contains("Đã đăng đàn", msg3);
    }
}
