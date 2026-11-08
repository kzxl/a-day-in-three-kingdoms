using System.Text.Json;
using SimT.Contracts.Data;
using SimT.Contracts.Events;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using SimT.Core.Modules;

namespace SimT.Modules.Dlc.Tests;

public sealed class NpcBondTests : IDisposable
{
    private readonly DlcTestHost _host = new();
    public void Dispose() => _host.Dispose();
    private static INpcBonds Bonds(SimKernel k) => k.Capabilities.Get<INpcBonds>(INpcBonds.CapabilityId);
    private static INpcRegistry Registry(SimKernel k) => k.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
    private static long Id(SimKernel k, string code) => Registry(k).All.Single(n => n.Code == code).Id;
    private static void Befriend(SimKernel k, long id) => k.Db.Execute(
        "INSERT OR REPLACE INTO kz_relationship VALUES (@id,80,70,'Thân thiết',0,@d)",
        new DbArgs().Add("@id",id).Add("@d",GameDateCodec.Format(k.Today)));

    [Fact]
    public void Historical_bonds_are_canonical_directed_and_repeatable()
    {
        using var k = _host.Boot();
        long liu = Id(k,"npc_0001"), guan = Id(k,"npc_0003"), zhang = Id(k,"npc_0002");
        foreach (var pair in new[] { (liu,guan), (liu,zhang), (guan,zhang) })
            Assert.Contains(Bonds(k).ForNpc(pair.Item1), b => b.Other(pair.Item1) == pair.Item2 && b.Kind == "sworn" && b.Source == "history");
        Assert.Contains(Bonds(k).ForNpc(liu), b => b.NpcA == liu && b.NpcB == Id(k,"npc_0025") && b.Kind == "lord");
        var count = k.Db.ScalarLong("SELECT COUNT(*) FROM kz_npc_bond WHERE source='history'");
        Assert.True(count >= 40);
        k.Db.Execute(File.ReadAllText(Path.Combine(DlcTestHost.RepoRoot,"src/SimT.Modules.Dlc.Character/migrations/0403-npc-bonds.sql")));
        Assert.Equal(count,k.Db.ScalarLong("SELECT COUNT(*) FROM kz_npc_bond WHERE source='history'"));
        Assert.Equal(0,k.Db.ScalarLong("SELECT COUNT(*) FROM kz_npc_bond WHERE kind NOT IN ('parent','lord','mentor') AND npc_a>=npc_b"));
    }

    [Fact]
    public void Seeded_bonds_repeat_and_every_nonhistorical_npc_has_one_to_three()
    {
        using var k = _host.Boot();
        using var secondHost = new DlcTestHost();
        using var second = secondHost.Boot();
        foreach (var npc in Registry(k).All)
        {
            var first = Bonds(k).ForNpc(npc.Id);
            Assert.Equal(first,Bonds(second).ForNpc(npc.Id));
            if (first.All(b => b.Source != "history")) Assert.InRange(first.Count,1,3);
        }
    }

    [Fact]
    public void Legacy_save_migrates_then_reopening_keeps_exact_bonds()
    {
        using (var k = _host.Boot())
        {
            k.Db.Execute("DROP TABLE kz_npc_bond; DROP TABLE kz_bond_state; DELETE FROM core_migration WHERE migration_id LIKE '%0403-npc-bonds%'");
            k.Save();
        }
        NpcBond[] before;
        using (var k = _host.Boot())
        {
            Assert.True(k.Db.ScalarLong("SELECT COUNT(*) FROM kz_npc_bond WHERE source='sim'") > 0);
            before = Registry(k).All.SelectMany(n => Bonds(k).ForNpc(n.Id)).ToArray(); k.Save();
        }
        using var reopened = _host.Boot();
        Assert.Equal(before,Registry(reopened).All.SelectMany(n => Bonds(reopened).ForNpc(n.Id)));
    }

    [Fact]
    public void Introduction_adds_bonus_once_and_appears_in_conversation()
    {
        using var k = _host.Boot();
        var social = k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        long liu = Id(k,"npc_0001"), guan = Id(k,"npc_0003");
        Registry(k).UpdateCitadel(guan,1);
        k.Db.Execute("UPDATE so_npc_activity SET current_citadel_id=1 WHERE npc_id=@id",DbArgs.One("@id",guan));
        Befriend(k,liu);
        Assert.False(social.HasMet(guan));
        Assert.Equal(NpcBondRules.IntroductionBonus,social.GetRelationship(guan).Affinity);
        var dialogue = k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        Assert.Contains(dialogue.BuildTalkScene(guan).Lines,l => l.StartsWith("Bạn của "));
        var msg = social.ChatWithNpc(guan);
        Assert.Contains("Bạn của ",msg);
        var affinity = social.GetRelationship(guan).Affinity;
        Assert.Equal(NpcBondRules.IntroductionBonus + 1,affinity);
        social.ChatWithNpc(guan);
        Assert.Equal(affinity+1,social.GetRelationship(guan).Affinity);
        Assert.Contains(dialogue.BuildTalkScene(guan).Lines,l => l.StartsWith("Bạn của "));
    }

    [Fact]
    public void Befriending_rival_reduces_close_ally_affinity_and_reports_it()
    {
        using var k = _host.Boot();
        long ally = Id(k,"npc_0001"), target = Registry(k).All.First(n => n.CitadelId == 1).Id;
        k.Db.Execute("INSERT OR REPLACE INTO kz_npc_bond VALUES (@a,@b,'rival',75,@d,'sim')",
            new DbArgs().Add("@a",Math.Min(ally,target)).Add("@b",Math.Max(ally,target)).Add("@d",GameDateCodec.Format(k.Today)));
        Befriend(k,ally);
        var social = k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        Assert.Contains("không vui",social.ChatWithNpc(target));
        Assert.True(social.GetRelationship(ally).Affinity < 80);
        Assert.Contains(social.GetMemories(ally),m => m.Topic == "rival" && m.AffinityDelta < 0);
    }

    [Fact]
    public void Capped_affinity_does_not_penalize_an_ally_and_old_contacts_are_not_reintroduced()
    {
        using var k = _host.Boot();
        long ally = Id(k,"npc_0001"), target = Registry(k).All.First(n => n.CitadelId == 1).Id;
        k.Db.Execute("INSERT OR REPLACE INTO kz_npc_bond VALUES (@a,@b,'rival',75,@d,'sim')",
            new DbArgs().Add("@a",Math.Min(ally,target)).Add("@b",Math.Max(ally,target)).Add("@d",GameDateCodec.Format(k.Today)));
        Befriend(k,ally);
        Befriend(k,target);
        k.Db.Execute("UPDATE kz_relationship SET affinity=100 WHERE npc_id=@id",DbArgs.One("@id",target));
        var social = k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        Assert.DoesNotContain("không vui",social.ChatWithNpc(target));
        Assert.Equal(80,social.GetRelationship(ally).Affinity);
        Assert.DoesNotContain(social.GetMemories(target),m=>m.Topic=="introduction");
    }

    [Fact]
    public void Chronicle_migration_keeps_existing_rows_when_repeated()
    {
        using var k = _host.Boot();
        var chronicle = k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle);
        chronicle.RecordRelationshipEvent(k.Today,Id(k,"npc_0001"),"Lưu Bị kết giao bạn hữu");
        var migration = File.ReadAllText(Path.Combine(DlcTestHost.RepoRoot,"src/SimT.Modules.Dlc.Society/migrations/0304-relationship-chronicle.sql"));
        k.Db.Execute(migration);
        k.Db.Execute(migration);
        Assert.Single(chronicle.RecentEvents(100,"relationship"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Monthly_changes_are_bounded_idempotent_and_society_is_optional(bool society)
    {
        var product = Path.Combine(Path.GetDirectoryName(_host.DatabasePath)!,"bond-product.json");
        File.WriteAllText(product,JsonSerializer.Serialize(new {
            id="bonds",name="Bonds",seed=1840502,startLocation="lac_duong",startingCash=2000,
            modules=ModuleLoader.Discover(AppContext.BaseDirectory).Select(m=>m.Manifest.Id).Where(id=>society || id!="dlc.society").ToArray()
        }));
        using var k = SimKernel.Open(new KernelOptions(_host.DatabasePath,AppContext.BaseDirectory,product,
            SimT.Testing.TestHost.ConstantsPath,BackupDirectory:Path.Combine(Path.GetDirectoryName(product)!,"backups")));
        var events = k.Capabilities.Get<IEventPublisher>(CapabilityIds.EventPublisher);
        for (int month=1;month<=12;month++)
        {
            var date = new GameDate(185,month,1);
            var before = Registry(k).All.SelectMany(n=>Bonds(k).ForNpc(n.Id)).Distinct().ToHashSet();
            events.Publish(new MonthClosed(date,date.AddDays(-1)));
            var after = Registry(k).All.SelectMany(n=>Bonds(k).ForNpc(n.Id)).Distinct().ToHashSet();
            Assert.True(before.Except(after).Count(b=>!after.Any(a=>a.NpcA==b.NpcA && a.NpcB==b.NpcB && a.Kind==b.Kind)) + after.Except(before).Count(b=>!before.Any(a=>a.NpcA==b.NpcA && a.NpcB==b.NpcB && a.Kind==b.Kind)) <= NpcBondRules.MonthlyChangeLimit);
            events.Publish(new MonthClosed(date,date.AddDays(-1)));
            Assert.True(after.SetEquals(Registry(k).All.SelectMany(n=>Bonds(k).ForNpc(n.Id))));
        }
        if (society) Assert.NotEmpty(k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle).RecentEvents(100,"relationship"));
    }

    [Fact]
    public void Scene_is_bounded_to_two_hops_and_search_can_find_every_npc()
    {
        using var k = _host.Boot();
        var registry = Registry(k);
        var liu = NpcBondScene.Build(Bonds(k),registry,Id(k,"npc_0001"));
        Assert.InRange(liu.Nodes.Count,2,24);
        Assert.All(liu.Nodes,n=>Assert.InRange(n.Depth,0,2));
        Assert.Contains(liu.Nodes,n=>n.Depth==2);
        Assert.Contains(NpcBondScene.Build(Bonds(k),registry,liu.CenterId,"Lưu Bị").SearchResults,n=>n.NpcId==liu.CenterId);
        var ordinary = registry.All.First(n=>Bonds(k).ForNpc(n.Id).All(b=>b.Source=="sim"));
        var common = NpcBondScene.Build(Bonds(k),registry,ordinary.Id);
        // Optional external QA export uses the real bounded payload and real portrait paths.
        var qa = Environment.GetEnvironmentVariable("SIMT_BOND_QA");
        if (!string.IsNullOrWhiteSpace(qa))
        {
            Directory.CreateDirectory(qa);
            File.WriteAllText(Path.Combine(qa,"liu-bei.json"),JsonSerializer.Serialize(liu));
            File.WriteAllText(Path.Combine(qa,"ordinary.json"),JsonSerializer.Serialize(common));
        }
    }
}
