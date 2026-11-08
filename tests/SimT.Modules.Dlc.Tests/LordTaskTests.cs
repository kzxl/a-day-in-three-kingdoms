using System.Text.Json;
using SimT.Contracts.Data;
using SimT.Contracts.Events;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using SimT.Core.Modules;
namespace SimT.Modules.Dlc.Tests;

public sealed class LordTaskTests : IDisposable
{
    private readonly DlcTestHost host = new();
    public void Dispose() => host.Dispose();
    private static ILordTasks Tasks(SimKernel k) => k.Capabilities.Get<ILordTasks>(ILordTasks.CapabilityId);
    private static IPoliticalCourt Court(SimKernel k) => k.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice);
    private static IPlayerState Player(SimKernel k) => k.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
    private static void Join(SimKernel k,int rank=0)
    {
        k.Db.Execute("UPDATE po_state SET value=100 WHERE key='reputation'");
        k.Db.Execute("UPDATE po_state SET value=@rank WHERE key='office_id'",DbArgs.One("@rank",rank));
        Assert.True(Court(k).JoinFaction(1).Ok);
    }
    private static void Month(SimKernel k,GameDate date) => k.Capabilities.Get<IEventPublisher>(CapabilityIds.EventPublisher)
        .Publish(new MonthClosed(date,date.AddDays(-1)));
    private static void Fixture(SimKernel k,string kind="grain",int quantity=10)
    {
        Join(k);
        k.Db.Execute("""
            INSERT INTO po_lord_task(faction_id,month_key,template,item_id,quantity,city_id,assigned_day,deadline_day,merit,bonus)
            VALUES(1,1,@t,1,@q,1,@d,@end,6,60)
            """,new DbArgs().Add("@t",kind).Add("@q",quantity).Add("@d",k.Today.DayIndex).Add("@end",k.Today.AddDays(25).DayIndex));
        Tasks(k).Acknowledge(Court(k).Factions.First(f=>f.Id==1).LeaderNpcId);
    }

    [Fact]
    public void Unaffiliated_player_gets_no_task()
    {
        using var k=host.Boot(); Month(k,k.Today.AddDays(30));
        Assert.Null(Tasks(k).Current);
        Assert.Equal(0,k.Db.ScalarLong("SELECT COUNT(*) FROM po_lord_task"));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(4)] [InlineData(9)]
    public void Monthly_assignment_is_once_rank_scaled_and_uses_real_targets(int rank)
    {
        using var k=host.Boot(); Join(k,rank);
        var date=k.Today.AddDays(30);
        Month(k,date); var task=Tasks(k).Current!.Task;
        Month(k,date);
        Assert.Equal(1,k.Db.ScalarLong("SELECT COUNT(*) FROM po_lord_task"));
        Assert.Equal(6+rank*2,task.Merit);
        Assert.Equal(25,task.Deadline.DayIndex-task.Assigned.DayIndex);
        Assert.NotNull(k.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap).Find(task.CityId));
        if (rank==0) Assert.Contains(task.Template,new[]{"grain","tribute"});
        if (task.Template=="grain")
        {
            Assert.Equal(10+rank*5,task.Quantity);
            Assert.Contains(Court(k).Governances,g=>g.CitadelId==task.CityId && g.FactionId==1);
        }
        if (task.Template=="tribute") Assert.Equal(80+rank*40,task.Quantity);
        if (task.Template=="envoy")
        {
            Assert.True(rank>=2);
            Assert.Contains(Court(k).Governances,g=>g.CitadelId==task.CityId && g.FactionId!=1 && g.GovernorNpcId==task.TargetId);
        }
        k.Db.Execute("UPDATE po_lord_task SET state='completed'");
        Month(k,date); Assert.Equal(1,k.Db.ScalarLong("SELECT COUNT(*) FROM po_lord_task"));
    }
    [Fact]
    public void Grain_requires_full_stock_acknowledgement_and_correct_city_then_rewards_once()
    {
        using var k=host.Boot(); Fixture(k);
        var bag=k.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        Player(k).MoveTo(1,k.Today); bag.Add(1,1,9);
        var cash=k.Ledger.Liquid;
        Assert.False(Tasks(k).Current!.Ready); Tasks(k).Report();
        Assert.Equal(9,bag.Quantity(1,1)); Assert.Equal(cash,k.Ledger.Liquid);
        bag.Add(1,1,1); Assert.True(Tasks(k).Current!.Ready);
        Assert.Equal(10,bag.Quantity(1,1));
        k.Db.Execute("UPDATE po_lord_task SET acknowledged=0"); Tasks(k).Report(); Assert.Equal(10,bag.Quantity(1,1));
        Tasks(k).Acknowledge(Court(k).Factions[0].LeaderNpcId);
        Player(k).MoveTo(2,k.Today); bag.Add(2,1,10); Tasks(k).Report(); Assert.Equal(10,bag.Quantity(2,1));
        Player(k).MoveTo(1,k.Today);
        var message=Tasks(k).Report(); Assert.Contains("Hoàn thành",message);
        Assert.Equal(0,bag.Quantity(1,1)); Assert.Equal(cash+60,k.Ledger.Liquid);
        Assert.Equal(60,Tasks(k).Trust); Assert.Equal(106,Court(k).Reputation);
        Assert.Equal(1,Court(k).OfficeId); Assert.Contains("thăng chức",message);
        Tasks(k).Report(); Assert.Equal(cash+60,k.Ledger.Liquid);
        Assert.Contains(k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle).RecentEvents(100,"governance"),e=>e.HeadlineVi.Contains("Hoàn thành công vụ"));
    }
    [Fact]
    public void Tribute_only_charges_on_explicit_report_at_capital()
    {
        using var k=host.Boot(); Fixture(k,"tribute",80);
        var cash=k.Ledger.Liquid; Assert.True(Tasks(k).Current!.Ready); Assert.Equal(cash,k.Ledger.Liquid);
        Player(k).MoveTo(1,k.Today); Tasks(k).Report();
        Assert.Equal(cash-80+60,k.Ledger.Liquid);
    }
    [Fact]
    public void Low_trust_blocks_promotion_and_success_resets_failure_streak()
    {
        using var k=host.Boot(); Fixture(k,"tribute");
        k.Db.Execute("UPDATE po_lord_trust SET trust=10,failures=2");
        Player(k).MoveTo(1,k.Today); Tasks(k).Report();
        Assert.Equal(0,Court(k).OfficeId); Assert.Equal(0,Tasks(k).ConsecutiveFailures); Assert.Equal(20,Tasks(k).Trust);
    }
    [Fact]
    public void Three_consecutive_expiries_expel_and_record_governance()
    {
        using var k=host.Boot(); Join(k);
        for(var i=0;i<3;i++)
        {
            k.AdvanceDays(31-k.Today.Day);
            Assert.NotNull(Tasks(k).Current);
            k.AdvanceDays(26);
            if(i<2) Assert.Equal(i+1,Tasks(k).ConsecutiveFailures);
        }
        Assert.Equal(0,Court(k).AllegianceFactionId); Assert.Null(Tasks(k).Current);
        Assert.Equal(3,k.Db.ScalarLong("SELECT COUNT(*) FROM po_lord_task WHERE state='failed'"));
        Assert.Contains(k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle).RecentEvents(200,"governance"),e=>e.HeadlineVi.Contains("đuổi khỏi"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Leaving_or_switching_cancels_current_task(bool switchFaction)
    {
        using var k=host.Boot(); Fixture(k);
        if(switchFaction) Court(k).JoinFaction(2); else Court(k).LeaveFaction();
        Assert.Null(Tasks(k).Current);
        Assert.Equal("cancelled",k.Db.ScalarString("SELECT state FROM po_lord_task"));
    }
    [Fact]
    public void Lord_scene_has_real_portrait_orders_acceptance_and_no_refusal()
    {
        using var k=host.Boot(); Fixture(k);
        k.Db.Execute("UPDATE po_lord_task SET acknowledged=0");
        var leader=Court(k).Factions[0].LeaderNpcId;
        var dialogue=k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var scene=dialogue.BuildTalkScene(leader);
        Assert.Equal(leader,scene.NpcId); Assert.NotEmpty(scene.PortraitFull);
        Assert.Contains(scene.Choices,c=>c.Id=="lord:accept");
        Assert.DoesNotContain(scene.Choices,c=>c.LabelVi.Contains("Từ chối"));
        Assert.Contains(dialogue.HandleChoice(leader,"lord:accept").UpdatedScene!.Choices,c=>c.Id=="lord:report");
    }
    [Fact]
    public void Reporting_in_dialogue_shows_the_full_promotion_announcement()
    {
        using var k = host.Boot();
        Fixture(k, "tribute");
        Player(k).MoveTo(1, k.Today);
        var leader = Court(k).Factions[0].LeaderNpcId;
        var dialogue = k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var outcome = dialogue.HandleChoice(leader, "lord:report");
        Assert.Contains("thăng chức", outcome.MessageVi);
        Assert.Contains(outcome.UpdatedScene!.Lines, line => line.Contains("thăng chức"));
        Assert.Equal(1, Court(k).OfficeId);
    }
    [Fact]
    public void Envoy_requires_governor_at_destination_and_report_back()
    {
        using var k=host.Boot(); Fixture(k,"envoy");
        var gov=Court(k).Governances.First(g=>g.FactionId!=1 && g.GovernorNpcId!=0);
        k.Db.Execute("UPDATE po_lord_task SET city_id=@c,target_id=@n",new DbArgs().Add("@c",gov.CitadelId).Add("@n",gov.GovernorNpcId));
        Assert.False(Tasks(k).CanEnvoy(gov.GovernorNpcId));
        Player(k).MoveTo(gov.CitadelId,k.Today);
        k.Db.Execute("UPDATE po_lord_task SET acknowledged=0");
        Assert.False(Tasks(k).CanEnvoy(gov.GovernorNpcId));
        Tasks(k).DeliverEnvoy(gov.GovernorNpcId);
        Assert.False(Tasks(k).Current!.Ready);
        Tasks(k).Acknowledge(Court(k).Factions[0].LeaderNpcId);
        Assert.False(Tasks(k).CanEnvoy(gov.GovernorNpcId+1));
        var dialogue=k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        Assert.Contains(dialogue.BuildTalkScene(gov.GovernorNpcId).Choices,c=>c.Id=="lord:envoy");
        dialogue.HandleChoice(gov.GovernorNpcId,"lord:envoy");
        Assert.True(Tasks(k).Current!.Ready); Assert.Equal(Court(k).Factions[0].CapitalCityId,Tasks(k).Current!.DestinationCityId);
        Tasks(k).Report(); Assert.NotNull(Tasks(k).Current);
        Player(k).MoveTo(Court(k).Factions[0].CapitalCityId,k.Today); Tasks(k).Report(); Assert.Null(Tasks(k).Current);
    }
    [Fact]
    public void Old_save_migrates_idempotently_and_active_task_survives_reopen()
    {
        long id;
        using(var k=host.Boot())
        {
            k.Db.Execute("DROP TABLE po_lord_task"); k.Db.Execute("DROP TABLE po_lord_trust");
            k.Db.Execute("DELETE FROM core_migration WHERE module_id='dlc.power' AND migration_id LIKE '%0703%'");
        }
        using(var k=host.Boot())
        {
            Fixture(k); id=Tasks(k).Current!.Task.Id;
            var sql=File.ReadAllText(Path.Combine(DlcTestHost.RepoRoot,"src/SimT.Modules.Dlc.Power/migrations/0703-lord-tasks.sql"));
            k.Db.Execute(sql); k.Db.Execute(sql); Assert.Equal(id,Tasks(k).Current!.Task.Id); k.Save();
        }
        using(var k=host.Boot()) { Assert.Equal(id,Tasks(k).Current!.Task.Id); Assert.Equal(50,Tasks(k).Trust); }
    }
    [Theory]
    [InlineData("dlc.society")]
    [InlineData("dlc.power")]
    public void Optional_dlc_can_be_disabled(string disabled)
    {
        var product=Path.Combine(Path.GetDirectoryName(host.DatabasePath)!,"lord-product.json");
        File.WriteAllText(product,JsonSerializer.Serialize(new { id="lord",name="Lord",seed=1840502,startLocation="lac_duong",startingCash=2000,
            modules=ModuleLoader.Discover(AppContext.BaseDirectory).Select(m=>m.Manifest.Id).Where(id=>id!=disabled).ToArray() }));
        using var k=SimKernel.Open(new KernelOptions(host.DatabasePath,AppContext.BaseDirectory,product,SimT.Testing.TestHost.ConstantsPath));
        if(disabled=="dlc.power")
        {
            Assert.False(k.Capabilities.IsProvided(ILordTasks.CapabilityId));
            var npc=k.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry).All[0];
            Assert.DoesNotContain(k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue).BuildTalkScene(npc.Id).Choices,c=>c.Id.StartsWith("lord:"));
        }
        else
        {
            Join(k); k.AdvanceDays(31-k.Today.Day);
            var row=Tasks(k).Current!;
            Assert.Equal("tribute",row.Task.Template);
            Tasks(k).Acknowledge(Court(k).Factions[0].LeaderNpcId);
            Player(k).MoveTo(row.DestinationCityId,k.Today);
            Assert.Contains("Hoàn thành",Tasks(k).Report());
            Assert.Equal(60,Tasks(k).Trust);
        }
        Assert.Empty(k.RunInvariants());
    }
    [Fact]
    public void Bandit_evidence_requires_a_new_victory_after_assignment()
    {
        using var k=host.Boot(); Fixture(k,"bandit");
        var stronghold=k.Capabilities.Get<IMilitaryCommand>(CapabilityIds.MilitaryCommand).AllStrongholds()[0];
        k.Db.Execute("UPDATE po_lord_task SET target_id=@n,baseline=@b",new DbArgs().Add("@n",stronghold.Id).Add("@b",stronghold.ClearedCount));
        Assert.False(Tasks(k).Current!.Ready);
        k.Db.Execute("UPDATE ml_stronghold SET cleared_count=cleared_count+1,last_cleared_date=@d WHERE id=@id",
            new DbArgs().Add("@id",stronghold.Id).Add("@d",GameDateCodec.Format(k.Today.AddDays(-1))));
        Assert.False(Tasks(k).Current!.Ready);
        k.Db.Execute("UPDATE ml_stronghold SET last_cleared_date=@d WHERE id=@id",new DbArgs().Add("@id",stronghold.Id).Add("@d",GameDateCodec.Format(k.Today)));
        Assert.True(Tasks(k).Current!.Ready);
    }
}
