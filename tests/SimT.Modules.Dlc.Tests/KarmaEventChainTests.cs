using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Ui;
using SimT.Core.Kernel;

namespace SimT.Modules.Dlc.Tests;

public sealed class KarmaEventChainTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Karma_seeds_initialized_with_starter_chains()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        Assert.NotNull(society.Karma);

        var activeSeeds = society.Karma.GetActiveSeeds();
        Assert.Equal(3, activeSeeds.Count);

        var famine = activeSeeds.FirstOrDefault(s => s.ChainCode == "CH_GRAIN_FAMINE");
        Assert.NotNull(famine);
        Assert.Equal(1, famine.CurrentStage);
        Assert.Equal(2, famine.MaxStage);
        Assert.False(famine.IsResolved);
        Assert.True(famine.IsActive);
        Assert.Equal(1, famine.CitadelId); // Lạc Dương

        var mohist = activeSeeds.FirstOrDefault(s => s.ChainCode == "CH_MOHIST_ENGINEER");
        Assert.NotNull(mohist);
        Assert.Equal(2, mohist.CitadelId); // Trường An

        var horseThief = activeSeeds.FirstOrDefault(s => s.ChainCode == "CH_HORSE_THIEF");
        Assert.NotNull(horseThief);
        Assert.Equal(4, horseThief.CitadelId); // Hứa Xương
    }

    [Fact]
    public void Karma_pending_events_respect_citadel_and_timing_delays()
    {
        using var kernel = _host.Boot();
        var karma = kernel.Capabilities.Get<IKarmaEngine>(CapabilityIds.KarmaEngine);
        var today = (int)kernel.Today.DayIndex;

        // CH_MOHIST_ENGINEER has trigger_after_day = today + 20 in Citadel 2.
        // It should NOT be pending before today + 20
        var pendingEarly = karma.CheckPendingEvents(2, currentDay: today + 10);
        Assert.Empty(pendingEarly);

        // At day today + 20, it should be pending in Citadel 2
        var pendingMatured = karma.CheckPendingEvents(2, currentDay: today + 20);
        Assert.Single(pendingMatured);
        Assert.Equal("CH_MOHIST_ENGINEER", pendingMatured[0].ChainCode);
        Assert.Equal(3, pendingMatured[0].Options.Count);

        // At another citadel (e.g. Citadel 3), it should not appear
        var pendingOtherCitadel = karma.CheckPendingEvents(3, currentDay: today + 20);
        Assert.Empty(pendingOtherCitadel);
    }

    [Fact]
    public void Karma_resolve_choice_virtue_path_and_fruition()
    {
        using var kernel = _host.Boot();
        var society = kernel.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        var karma = society.Karma;
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var baselineCash = wallet.Balance;
        var baselineVirtue = attrs.Social.Virtue;

        var famine = karma.GetActiveSeeds().First(s => s.ChainCode == "CH_GRAIN_FAMINE");

        // Resolve Stage 1 with "generous" choice
        var resStage1 = karma.ResolveChoice(famine.Id, "generous");
        Assert.True(resStage1.Success);
        Assert.Equal(-150, resStage1.MoneyChange);
        Assert.Equal(15, resStage1.VirtueChange);
        Assert.False(resStage1.ChainCompleted);

        // Verify wallet & attributes updated
        Assert.Equal(baselineCash - 150, wallet.Balance);
        Assert.Equal(baselineVirtue + 15, attrs.Social.Virtue);

        // Verify seed advanced to Stage 2 and trigger_after_day extended
        var updatedSeed = karma.GetAllSeeds().First(s => s.Id == famine.Id);
        Assert.Equal(2, updatedSeed.CurrentStage);
        Assert.Equal("virtue", updatedSeed.KarmaAlignment);
        Assert.Equal("generous", updatedSeed.BranchChoice);
        Assert.True(updatedSeed.TriggerAfterDay >= 25);

        // History recorded
        var history = karma.GetHistory(famine.Id);
        Assert.Single(history);
        Assert.Equal(1, history[0].Stage);
        Assert.Equal("generous", history[0].DecisionVi);

        // Check Stage 2 Pending Event
        var pendingStage2 = karma.CheckPendingEvents(1, currentDay: updatedSeed.TriggerAfterDay);
        Assert.Single(pendingStage2);
        Assert.Equal(2, pendingStage2[0].Stage);

        // Resolve Stage 2 climax
        var resStage2 = karma.ResolveChoice(famine.Id, "accept_steward");
        Assert.True(resStage2.Success);
        Assert.True(resStage2.ChainCompleted);
        Assert.Equal(1200, resStage2.MoneyChange);
        Assert.Equal(20, resStage2.VirtueChange);

        // Verify final wallet balance: baseline - 150 + 1200 = baseline + 1050
        Assert.Equal(baselineCash - 150 + 1200, wallet.Balance);

        // Verify seed is completed and resolved
        var finishedSeed = karma.GetAllSeeds().First(s => s.Id == famine.Id);
        Assert.True(finishedSeed.IsResolved);
        Assert.False(finishedSeed.IsActive);

        // Verify history has both stages
        var fullHistory = karma.GetHistory(famine.Id);
        Assert.Equal(2, fullHistory.Count);

        // Invariants pass
        Assert.Empty(kernel.RunInvariants());
    }

    [Fact]
    public void Karma_resolve_choice_ruthless_path_and_retribution()
    {
        using var kernel = _host.Boot();
        var karma = kernel.Capabilities.Get<IKarmaEngine>(CapabilityIds.KarmaEngine);
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        var attrs = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var baselineCash = wallet.Balance;
        var baselineVirtue = attrs.Social.Virtue;

        var famine = karma.GetActiveSeeds().First(s => s.ChainCode == "CH_GRAIN_FAMINE");

        // Resolve Stage 1 with "ruthless" choice (hoard grain)
        var resStage1 = karma.ResolveChoice(famine.Id, "ruthless");
        Assert.True(resStage1.Success);
        Assert.Equal(300, resStage1.MoneyChange);
        Assert.Equal(-20, resStage1.VirtueChange);

        Assert.Equal(baselineCash + 300, wallet.Balance);
        Assert.Equal(baselineVirtue - 20, attrs.Social.Virtue);

        var updatedSeed = karma.GetAllSeeds().First(s => s.Id == famine.Id);
        Assert.Equal("ruthless", updatedSeed.KarmaAlignment);

        // Climax Stage 2 Retribution
        var resStage2 = karma.ResolveChoice(famine.Id, "bribe_settle");
        Assert.True(resStage2.Success);
        Assert.True(resStage2.ChainCompleted);
        Assert.Equal(-600, resStage2.MoneyChange);
        Assert.Equal(-10, resStage2.FameChange);

        // Net result: 300 - 600 = -300 loss from karma retribution
        Assert.Equal(baselineCash - 300, wallet.Balance);
    }

    [Fact]
    public void City_stroll_triggers_pending_karma_fruition()
    {
        using var kernel = _host.Boot();
        var stroll = kernel.Capabilities.Get<ICityStroll>(CapabilityIds.CityStroll);
        var karma = kernel.Capabilities.Get<IKarmaEngine>(CapabilityIds.KarmaEngine);

        // Ensure CH_GRAIN_FAMINE is ready at citadel 1 by setting day advance
        kernel.AdvanceDays(5);

        // Stroll in Citadel 1 (Lạc Dương)
        var result = stroll.Stroll(1);
        Assert.True(result.Ok);
        Assert.Equal(StrollEventType.KarmaFruition, result.EventType);
        Assert.Contains("Nhân Duyên Khởi Điểm", result.Title);
        Assert.Contains("Căng thẳng", result.RewardSummary);

        // Verify that seed advanced from stage 1 to stage 2
        var famine = karma.GetAllSeeds().First(s => s.ChainCode == "CH_GRAIN_FAMINE");
        Assert.Equal(2, famine.CurrentStage);
    }

    [Fact]
    public void Society_screen_renders_karma_filter_and_commands()
    {
        using var kernel = _host.Boot();
        var screen = kernel.Ui.Screens.FirstOrDefault(s => s.ScreenId == "society.chronicle");
        Assert.NotNull(screen);

        Assert.True(kernel.TryGetCommandHandler("society.chronicle", out var handler));
        Assert.NotNull(handler);

        // Switch to karma filter
        var switchMsg = handler.Execute("filter:karma");
        Assert.Contains("karma", switchMsg);

        var model = screen.Render();
        Assert.Equal("society.chronicle", model.ScreenId);
        Assert.Contains(model.Sections, s => s.Heading.Contains("HẠT GIỐNG NHÂN QUẢ"));
        Assert.Contains(model.Headline, m => m.Label == "Hạt Giống Đang Ươm");
        Assert.Contains(model.Commands, c => c.Id == "filter:karma" && c.Kind == CommandKind.Primary);

        // Advance to trigger day and test resolve command
        kernel.AdvanceDays(5);
        var famine = kernel.Capabilities.Get<IKarmaEngine>(CapabilityIds.KarmaEngine)
            .GetAllSeeds().First(s => s.ChainCode == "CH_GRAIN_FAMINE");

        var resolveMsg = handler.Execute($"karma:resolve:{famine.Id}");
        Assert.NotNull(resolveMsg);
        Assert.Contains("Nhân Quả Ứng Nghiệm", resolveMsg);

        // Verify history section appears now
        var modelAfterResolve = screen.Render();
        Assert.Contains(modelAfterResolve.Sections, s => s.Heading.Contains("SỔ BÁO ỨNG"));
    }
}
