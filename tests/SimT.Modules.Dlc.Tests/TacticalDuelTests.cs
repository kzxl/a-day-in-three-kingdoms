using SimT.Contracts.Playing;
using SimT.Core.Kernel;
using SimT.Modules.Dlc.Military;

namespace SimT.Modules.Dlc.Tests;

public sealed class TacticalDuelTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Troop_branch_counter_affinities_form_canonical_triangle()
    {
        // Cavalry > Infantry > Archers > Cavalry
        Assert.True(TroopBranchHelper.Counters(TroopBranch.Cavalry, TroopBranch.Infantry));
        Assert.True(TroopBranchHelper.Counters(TroopBranch.Infantry, TroopBranch.Archers));
        Assert.True(TroopBranchHelper.Counters(TroopBranch.Archers, TroopBranch.Cavalry));

        // Inverses do not counter
        Assert.False(TroopBranchHelper.Counters(TroopBranch.Infantry, TroopBranch.Cavalry));
        Assert.False(TroopBranchHelper.Counters(TroopBranch.Archers, TroopBranch.Infantry));
        Assert.False(TroopBranchHelper.Counters(TroopBranch.Cavalry, TroopBranch.Archers));

        // Symmetric do not counter
        Assert.False(TroopBranchHelper.Counters(TroopBranch.Cavalry, TroopBranch.Cavalry));
        Assert.False(TroopBranchHelper.Counters(TroopBranch.Infantry, TroopBranch.Infantry));
        Assert.False(TroopBranchHelper.Counters(TroopBranch.Archers, TroopBranch.Archers));
    }

    [Fact]
    public void Npc_branch_affinity_resolved_iconically()
    {
        using var kernel = _host.Boot();
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);

        // Quan Vũ (npc_0003) has vo_thanh -> Cavalry
        var guanYu = registry.FindByCode("npc_0003");
        Assert.NotNull(guanYu);
        var gyBranch = military.ResolveNpcBranch(guanYu.Id);
        Assert.Equal(TroopBranch.Cavalry, gyBranch);

        // Zhang Fei (npc_0002) has dau_than -> Cavalry
        var zhangFei = registry.FindByCode("npc_0002");
        Assert.NotNull(zhangFei);
        var zfBranch = military.ResolveNpcBranch(zhangFei.Id);
        Assert.Equal(TroopBranch.Cavalry, zfBranch);

        // Zhuge Liang (npc_0025) has than_toan -> Archers
        var zhugeLiang = registry.FindByCode("npc_0025");
        Assert.NotNull(zhugeLiang);
        var zlBranch = military.ResolveNpcBranch(zhugeLiang.Id);
        Assert.Equal(TroopBranch.Archers, zlBranch);
    }

    [Fact]
    public void Simulated_duel_produces_structured_round_logs_and_narratives()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);
        var registry = kernel.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
        var attributes = kernel.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);

        var opponent = registry.FindByCode("npc_0002"); // Zhang Fei (Cavalry)
        Assert.NotNull(opponent);

        // Test with counter branch: Archers counter Cavalry (Zhang Fei)
        var result = military.ChallengeArena(opponent.Id, TroopBranch.Archers);

        Assert.NotNull(result);
        Assert.Equal(TroopBranch.Archers, result.PlayerBranch);
        Assert.Equal(TroopBranch.Cavalry, result.OpponentBranch);
        Assert.True(result.Rounds >= 1 && result.Rounds <= 5);
        Assert.NotNull(result.RoundLogs);
        Assert.NotEmpty(result.RoundLogs);
        Assert.Equal(result.Rounds, result.RoundLogs.Count);

        foreach (var log in result.RoundLogs)
        {
            Assert.True(log.RoundNumber >= 1);
            Assert.NotEmpty(log.NarrativeVi);
            Assert.True(log.PlayerHpAfter >= 0);
            Assert.True(log.OpponentHpAfter >= 0);
            Assert.InRange(log.PlayerSpiritAfter, 0, 100);
            Assert.InRange(log.OpponentSpiritAfter, 0, 100);
        }

        // Final HP matches the last round log
        var lastLog = result.RoundLogs[^1];
        Assert.Equal(lastLog.PlayerHpAfter, result.FinalPlayerHp);
        Assert.Equal(lastLog.OpponentHpAfter, result.FinalOpponentHp);

        var failures = kernel.RunInvariants();
        Assert.Empty(failures);
    }

    [Fact]
    public void Stronghold_records_carry_troop_branch_affinity()
    {
        using var kernel = _host.Boot();
        var military = kernel.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand);

        var strongholds = military.AllStrongholds();
        Assert.NotEmpty(strongholds);

        foreach (var sh in strongholds)
        {
            Assert.True(Enum.IsDefined(typeof(TroopBranch), sh.Branch));
        }
    }
}
