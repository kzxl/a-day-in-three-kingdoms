using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class EndgameAmbitionsTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void EndgameAmbitions_capability_is_registered_and_evaluates_all_5_branches()
    {
        using var kernel = _host.Boot();
        var ambitions = kernel.Capabilities.Get<IEndgameAmbitions>(CapabilityIds.EndgameAmbitions);

        Assert.NotNull(ambitions);

        var state = ambitions.Evaluate();
        Assert.NotNull(state);
        Assert.Equal(5, state.Branches.Count);

        var categories = state.Branches.Select(b => b.Category).ToHashSet();
        Assert.Contains(AmbitionCategory.Kingmaker, categories);
        Assert.Contains(AmbitionCategory.MerchantSovereign, categories);
        Assert.Contains(AmbitionCategory.DynastyPatriarch, categories);
        Assert.Contains(AmbitionCategory.GrandMartialMaster, categories);
        Assert.Contains(AmbitionCategory.RenownedSage, categories);

        foreach (var branch in state.Branches)
        {
            Assert.False(string.IsNullOrWhiteSpace(branch.TitleVi));
            Assert.False(string.IsNullOrWhiteSpace(branch.TitleEn));
            Assert.False(string.IsNullOrWhiteSpace(branch.EpilogueTitleVi));
            Assert.False(string.IsNullOrWhiteSpace(branch.EpilogueNarrativeVi));
            Assert.True(branch.Percent >= 0 && branch.Percent <= 100);
            Assert.NotEmpty(branch.Requirements);

            foreach (var req in branch.Requirements)
            {
                Assert.False(string.IsNullOrWhiteSpace(req.Code));
                Assert.False(string.IsNullOrWhiteSpace(req.LabelVi));
                Assert.True(req.TargetValue > 0);
            }
        }
    }

    [Fact]
    public void EndgameAmbitions_tracks_merchant_progress_when_wealth_increases()
    {
        using var kernel = _host.Boot();
        var ambitions = kernel.Capabilities.Get<IEndgameAmbitions>(CapabilityIds.EndgameAmbitions);
        var initialMerchant = ambitions.Evaluate().Branches.First(b => b.Category == AmbitionCategory.MerchantSovereign);

        // Inject funds into wallet
        var wallet = kernel.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        wallet.Pay(60000, "test.ambition", "Test ambition injection");

        var afterMerchant = ambitions.Evaluate().Branches.First(b => b.Category == AmbitionCategory.MerchantSovereign);
        var wealthReq = afterMerchant.Requirements.First(r => r.Code == "ms_wealth");

        Assert.True(wealthReq.IsMet);
        Assert.True(afterMerchant.Percent > initialMerchant.Percent);
    }
}
