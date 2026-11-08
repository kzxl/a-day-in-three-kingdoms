using System.Text.Json;
using SimT.Contracts.Data;
using SimT.Contracts.Events;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using SimT.Core.Modules;
namespace SimT.Modules.Dlc.Tests;

public sealed class NpcWishTests : IDisposable
{
    private readonly DlcTestHost host=new();
    public void Dispose()=>host.Dispose();
    private static INpcWishes Wishes(SimKernel k)=>k.Capabilities.Get<INpcWishes>(INpcWishes.CapabilityId);
    private static INpcRegistry Registry(SimKernel k)=>k.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
    private static IEventPublisher Events(SimKernel k)=>k.Capabilities.Get<IEventPublisher>(CapabilityIds.EventPublisher);
    private static void Friend(SimKernel k,long id,int affinity=60)=>k.Db.Execute(
        "INSERT OR REPLACE INTO kz_relationship VALUES (@n,@a,70,'Tri kỷ',0,@d)",
        new DbArgs().Add("@n",id).Add("@a",affinity).Add("@d",GameDateCodec.Format(k.Today)));
    private static long Fixture(SimKernel k,string template="rare",string state="hidden",long target=0)
    {
        k.Db.Execute("DELETE FROM kz_npc_wish");
        var npc=Registry(k).All.First(n=>n.CitadelId==1);
        k.Db.Execute("INSERT INTO kz_npc_wish(npc_id,template,item_id,quantity,city_id,target_npc_id,opened,deadline,state) VALUES (@n,@t,12,2,1,@target,@d,@end,@s)",
            new DbArgs().Add("@n",npc.Id).Add("@t",template).Add("@target",target).Add("@d",GameDateCodec.Format(k.Today))
                .Add("@end",GameDateCodec.Format(k.Today.AddDays(90))).Add("@s",state));
        Friend(k,npc.Id);
        return npc.Id;
    }
    private static string[] Snapshot(SimKernel k)=>k.Db.Query(
        "SELECT npc_id||':'||template||':'||item_id||':'||quantity||':'||target_npc_id||':'||deadline||':'||state FROM kz_npc_wish ORDER BY id",r=>r.GetString(0)).ToArray();

    [Fact]
    public void Seeded_generation_is_bounded_repeatable_and_month_is_idempotent()
    {
        using var k=host.Boot();
        using var h2=new DlcTestHost(); using var second=h2.Boot();
        Assert.Equal(3,Snapshot(k).Length);
        Assert.Equal(Snapshot(k),Snapshot(second));
        for(int i=1;i<=6;i++)
        {
            var date=k.Today.AddDays(i*30);
            Events(k).Publish(new MonthClosed(date,date.AddDays(-1)));
            Events(second).Publish(new MonthClosed(date,date.AddDays(-1)));
            Assert.Equal(Snapshot(k),Snapshot(second));
            Assert.InRange(k.Db.ScalarLong("SELECT COUNT(*) FROM kz_npc_wish WHERE state IN ('hidden','revealed','accepted')"),0,NpcWishRules.OpenLimit);
            var before=Snapshot(k);
            Events(k).Publish(new MonthClosed(date,date.AddDays(-1)));
            Assert.Equal(before,Snapshot(k));
            Events(second).Publish(new MonthClosed(date,date.AddDays(-1)));
        }
        Assert.Equal(0,k.Db.ScalarLong("SELECT COUNT(*) FROM (SELECT npc_id FROM kz_npc_wish WHERE state IN ('hidden','revealed','accepted') GROUP BY npc_id HAVING COUNT(*)>1)"));
    }

    [Fact]
    public void Reveal_accept_give_rewards_once_and_never_consumes_on_read_or_decline()
    {
        using var k=host.Boot(); var id=Fixture(k);
        Friend(k,id,NpcWishRules.RevealAffinity-1);
        Assert.Null(Wishes(k).ForNpc(id));
        Assert.DoesNotContain(k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue).BuildTalkScene(id).Choices,c=>c.Id=="wish:ask");
        Friend(k,id);
        var dialogue=k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        Assert.Contains(dialogue.BuildTalkScene(id).Choices,c=>c.Id=="wish:ask");
        Assert.Contains(dialogue.HandleChoice(id,"wish:ask").UpdatedScene!.Choices,c=>c.Id=="wish:accept");
        dialogue.HandleChoice(id,"wish:decline");
        Assert.Empty(Wishes(k).Journal());
        dialogue.HandleChoice(id,"wish:accept");
        var bag=k.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
        Assert.False(Assert.Single(Wishes(k).Journal()).Ready);
        Wishes(k).Give(id); Assert.Equal(0,bag.Quantity(1,12));
        bag.Add(1,12,2);
        Assert.True(Assert.Single(Wishes(k).Journal()).Ready);
        Assert.Equal(2,bag.Quantity(1,12));
        Assert.Contains("Đã giúp",Wishes(k).Give(id));
        Assert.Empty(Wishes(k).Journal());
        Assert.Equal(85,k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork).GetRelationship(id).Affinity);
        Assert.Equal(0,bag.Quantity(1,12));
        var count=bag.TotalQuantity(1);
        Wishes(k).Give(id);
        Assert.Equal(count,bag.TotalQuantity(1));
        Assert.Equal(0,k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork).GetRecruitRequirement(id).SigningFee);
        Assert.Contains(k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle).RecentEvents(100,"relationship"),e=>e.HeadlineVi.Contains("ước nguyện"));
    }

    [Fact]
    public void Shop_uses_wallet_only_on_explicit_give_and_requires_presence()
    {
        using var k=host.Boot(); var id=Fixture(k,"shop");
        Wishes(k).Accept(id);
        var wallet=k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        long before=wallet.Liquid;
        Assert.True(Assert.Single(Wishes(k).Journal()).Ready);
        Assert.Equal(before,wallet.Liquid);
        var player=k.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        player.MoveTo(2,k.Today);
        Assert.False(Assert.Single(Wishes(k).Journal()).Ready);
        Wishes(k).Give(id); Assert.Equal(before,wallet.Liquid);
        player.MoveTo(1,k.Today); Wishes(k).Give(id);
        Assert.Equal(before-2,wallet.Liquid);
        Wishes(k).Give(id); Assert.Equal(before-2,wallet.Liquid);
    }

    [Fact]
    public void Completed_wish_allows_recruitment_with_no_signing_cash()
    {
        using var k=host.Boot(); var id=Fixture(k);
        Wishes(k).Accept(id);
        k.Capabilities.Get<IBag>(CapabilityIds.PlayerBag).Add(1,12,2);
        Wishes(k).Give(id);
        var attrs=k.Capabilities.Get<IPlayerAttributes>(CapabilityIds.PlayerAttributes);
        attrs.GainFame(2000,"QA chiêu hiền");
        var wallet=k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
        wallet.Charge(wallet.Liquid,"qa","Hết tiền sính lễ");
        var social=k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        Assert.True(social.GetRecruitRequirement(id).CanRecruit);
        social.RecruitRetainer(id,"accountant",50);
        Assert.NotNull(social.GetRetainer(id));
        Assert.Equal(0,wallet.Liquid);
    }

    [Fact]
    public void Expiry_penalizes_accepted_only_once_and_simulates_unaccepted_outcomes()
    {
        using var k=host.Boot(); var id=Fixture(k,state:"accepted");
        Events(k).Publish(new DayAdvanced(k.Today.AddDays(90)));
        Assert.NotEmpty(Wishes(k).Journal());
        Events(k).Publish(new DayAdvanced(k.Today.AddDays(91)));
        Assert.Equal(55,k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork).GetRelationship(id).Affinity);
        Events(k).Publish(new DayAdvanced(k.Today.AddDays(91)));
        Assert.Equal(55,k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork).GetRelationship(id).Affinity);
        Assert.Contains(k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork).GetMemories(id),m=>m.Topic=="wish" && m.SummaryVi.Contains("quá hạn"));
        for(int i=0;i<20;i++)
        {
            id=Fixture(k);
            Events(k).Publish(new DayAdvanced(k.Today.AddDays(91)));
            Assert.Equal(60,k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork).GetRelationship(id).Affinity);
        }
        var events=k.Capabilities.Get<ISocietyChronicle>(CapabilityIds.SocietyChronicle).RecentEvents(100,"relationship");
        Assert.Contains(events,e=>e.HeadlineVi.Contains("tự hoàn thành"));
        Assert.Contains(events,e=>e.HeadlineVi.Contains("quá hạn"));
    }

    [Fact]
    public void Revenge_requires_rival_and_only_a_new_victory_counts()
    {
        using var k=host.Boot();
        for(int i=0;i<12;i++) Events(k).Publish(new MonthClosed(k.Today.AddDays(i*30+30),k.Today));
        var rows=k.Db.Query("SELECT npc_id,target_npc_id FROM kz_npc_wish WHERE template='revenge'",r=>(Npc:r.GetInt64(0),Target:r.GetInt64(1)));
        Assert.NotEmpty(rows);
        foreach(var r in rows) Assert.Contains(k.Capabilities.Get<INpcBonds>(INpcBonds.CapabilityId).ForNpc(r.Npc),b=>b.Kind=="rival" && b.Other(r.Npc)==r.Target);
        var rival=Registry(k).All.First(n=>n.Attributes.War>=30).Id;
        var id=Fixture(k,"revenge",target:rival);
        Events(k).Publish(new DuelCompletedEvent(rival,true,"Lạc Dương",k.Today));
        Wishes(k).Accept(id);
        Assert.False(Assert.Single(Wishes(k).Journal()).Ready);
        Events(k).Publish(new DuelCompletedEvent(rival,false,"Lạc Dương",k.Today));
        Assert.False(Assert.Single(Wishes(k).Journal()).Ready);
        Events(k).Publish(new DuelCompletedEvent(rival,true,"Lạc Dương",k.Today));
        Assert.True(Assert.Single(Wishes(k).Journal()).Ready);
        Assert.Contains("Đã giúp",Wishes(k).Give(id));
    }

    [Fact]
    public void No_rivals_means_no_revenge_wishes()
    {
        using var k=host.Boot();
        k.Db.Execute("DELETE FROM kz_npc_bond WHERE kind='rival'; DELETE FROM kz_npc_wish");
        for(int i=1;i<=8;i++) Events(k).Publish(new MonthClosed(k.Today.AddDays(i*30),k.Today));
        Assert.Equal(0,k.Db.ScalarLong("SELECT COUNT(*) FROM kz_npc_wish WHERE template='revenge'"));
    }

    [Fact]
    public void Mentor_introduction_requires_contact_and_creates_directed_bond()
    {
        using var k=host.Boot();
        long id=Fixture(k,"mentor");
        var npc=Registry(k).Find(id)!;
        var social=k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        var teacher=Registry(k).All.First(n=>n.Id!=id && social.GetNpcRank(n.Id)<=social.GetNpcRank(id) && n.Skills.Any(s=>s.Level>(npc.Skills.FirstOrDefault(x=>x.Code==s.Code)?.Level??0)));
        var skill=teacher.Skills.First(s=>s.Level>(npc.Skills.FirstOrDefault(x=>x.Code==s.Code)?.Level??0));
        k.Db.Execute("UPDATE kz_npc_wish SET skill_code=@s",DbArgs.One("@s",skill.Code));
        Wishes(k).Accept(id);
        Assert.Contains(skill.NameVi,Wishes(k).ForNpc(id)!.RequestVi);
        Assert.DoesNotContain(teacher.NameVi,Wishes(k).ForNpc(id)!.RequestVi);
        Assert.DoesNotContain("Đã giúp",Wishes(k).Introduce(id,teacher.Id));
        Friend(k,teacher.Id);
        Assert.True(Assert.Single(Wishes(k).Journal()).Ready);
        var dialogue=k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue);
        var choices=dialogue.HandleChoice(id,"wish:give").UpdatedScene!.Choices;
        Assert.InRange(choices.Count,2,6);
        Assert.Contains(choices,c=>c.Id=="wish:cancel" && c.LabelVi=="Thôi");
        Assert.Contains("Đã giúp",dialogue.HandleChoice(id,$"wish:teacher:{teacher.Id}").MessageVi);
        Assert.Contains(k.Capabilities.Get<INpcBonds>(INpcBonds.CapabilityId).ForNpc(id),b=>b.Kind=="mentor" && b.NpcA==teacher.Id && b.NpcB==id);
    }

    [Fact]
    public void Mentor_rejects_every_lower_rank_or_non_superior_skill_even_when_met()
    {
        using var k=host.Boot();
        var reg=Registry(k); var social=k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        foreach(var n in reg.All) Friend(k,n.Id);
        int lowerRank=0, nonSuperior=0;
        foreach(var npc in reg.All.Where(n=>n.CitadelId==1))
        foreach(var skill in reg.All.SelectMany(n=>n.Skills).DistinctBy(s=>s.Code))
        {
            Fixture(k,"mentor",state:"accepted");
            k.Db.Execute("UPDATE kz_npc_wish SET npc_id=@n,skill_code=@s",new DbArgs().Add("@n",npc.Id).Add("@s",skill.Code));
            var candidates=Wishes(k).MentorCandidates(npc.Id);
            Assert.InRange(candidates.Count,0,5);
            foreach(var teacher in reg.All.GroupBy(n=>(Self:n.Id==npc.Id,Lower:social.GetNpcRank(n.Id)>social.GetNpcRank(npc.Id),Superior:n.Skills.Any(s=>s.Code==skill.Code && s.Level>(npc.Skills.FirstOrDefault(x=>x.Code==skill.Code)?.Level??0)))).Select(g=>g.First()))
            {
                bool lower=social.GetNpcRank(teacher.Id)>social.GetNpcRank(npc.Id);
                bool superior=teacher.Skills.Any(s=>s.Code==skill.Code && s.Level>(npc.Skills.FirstOrDefault(s=>s.Code==skill.Code)?.Level??0));
                if (teacher.Id!=npc.Id && !lower && superior) continue;
                if(lower && superior) lowerRank++;
                if(!superior) nonSuperior++;
                Assert.DoesNotContain(candidates,n=>n.Id==teacher.Id);
                Assert.DoesNotContain("Đã giúp",Wishes(k).Introduce(npc.Id,teacher.Id));
                Assert.Equal("accepted",k.Db.Query("SELECT state FROM kz_npc_wish",r=>r.GetString(0)).Single());
            }
        }
        Assert.True(lowerRank>0); Assert.True(nonSuperior>0);
    }

    [Fact]
    public void Generated_mentors_always_have_a_real_skill_and_eligible_teacher()
    {
        using var k=host.Boot(); var reg=Registry(k);
        var social=k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
        for(int i=1;i<=12;i++) Events(k).Publish(new MonthClosed(k.Today.AddDays(i*30),k.Today));
        var rows=k.Db.Query("SELECT npc_id,skill_code,target_npc_id FROM kz_npc_wish WHERE template='mentor'",r=>(Npc:r.GetInt64(0),Skill:r.GetString(1),Target:r.GetInt64(2)));
        Assert.NotEmpty(rows);
        foreach(var row in rows)
        {
            var npc=reg.Find(row.Npc)!;
            Assert.Equal(0,row.Target);
            Assert.Contains(reg.All.SelectMany(n=>n.Skills),s=>s.Code==row.Skill);
            Assert.Contains(reg.All,n=>n.Id!=npc.Id && social.GetNpcRank(n.Id)<=social.GetNpcRank(npc.Id) && n.Skills.Any(s=>s.Code==row.Skill && s.Level>(npc.Skills.FirstOrDefault(x=>x.Code==s.Code)?.Level??0)));
        }
    }

    [Fact]
    public void Home_is_only_generated_away_from_registry_hometown()
    {
        using var k=host.Boot();
        Assert.Equal(0,k.Db.ScalarLong("SELECT COUNT(*) FROM kz_npc_wish WHERE template='home'"));
        k.Db.Execute("DELETE FROM kz_npc_wish; UPDATE so_npc_activity SET current_citadel_id=CASE WHEN current_citadel_id=1 THEN 2 ELSE 1 END");
        bool generated=false;
        for(int i=1;i<=15;i++)
        {
            k.Db.Execute("DELETE FROM kz_npc_wish");
            Events(k).Publish(new MonthClosed(k.Today.AddDays(i*30),k.Today));
            foreach(var row in k.Db.Query("SELECT npc_id,city_id FROM kz_npc_wish WHERE template='home'",r=>(Npc:r.GetInt64(0),City:r.GetInt64(1))))
            {
                generated=true; var npc=Registry(k).Find(row.Npc)!;
                Assert.Equal(npc.CitadelId,row.City);
                Assert.NotEqual(npc.CitadelId,npc.CurrentCitadelId(k.Capabilities));
            }
        }
        Assert.True(generated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Home_completes_once_by_fare_or_retainer_escort(bool escort)
    {
        using var k=host.Boot(); long id=Fixture(k,"home");
        k.Db.Execute("UPDATE so_npc_activity SET current_citadel_id=2 WHERE npc_id=@n",DbArgs.One("@n",id));
        var player=k.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
        player.MoveTo(2,k.Today); Wishes(k).Accept(id);
        var wallet=k.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet); long before=wallet.Liquid;
        Assert.Single(Wishes(k).Journal()); Assert.Equal(before,wallet.Liquid);
        if(escort)
        {
            k.Db.Execute("INSERT INTO kz_retainer(npc_id,role_assigned,monthly_stipend,loyalty,recruited_date) VALUES (@n,'accountant',50,70,@d)",new DbArgs().Add("@n",id).Add("@d",GameDateCodec.Format(k.Today)));
            Events(k).Publish(new DayAdvanced(k.Today));
            Assert.Single(Wishes(k).Journal());
            player.MoveTo(1,k.Today);
            Events(k).Publish(new DayAdvanced(k.Today));
        }
        else
        {
            player.MoveTo(3,k.Today);
            Assert.DoesNotContain("Đã giúp",Wishes(k).PayHomeFare(id));
            Assert.Equal(before,wallet.Liquid);
            player.MoveTo(2,k.Today);
            wallet.Charge(wallet.Liquid-NpcWishRules.HomeFare+1,"qa","Thiếu một quan");
            long poor=wallet.Liquid;
            Assert.DoesNotContain("Đã giúp",Wishes(k).PayHomeFare(id)); Assert.Equal(poor,wallet.Liquid);
            wallet.Pay(before-poor,"qa","Hoàn tiền QA");
            Assert.Contains("Đã giúp",Wishes(k).PayHomeFare(id));
        }
        Assert.Empty(Wishes(k).Journal());
        Assert.Equal(1,Registry(k).Find(id)!.CurrentCitadelId(k.Capabilities));
        Assert.Equal(before-(escort?0:NpcWishRules.HomeFare),wallet.Liquid);
        Wishes(k).PayHomeFare(id); Events(k).Publish(new DayAdvanced(k.Today));
        Assert.Equal(before-(escort?0:NpcWishRules.HomeFare),wallet.Liquid);
        Assert.Equal(85,k.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork).GetRelationship(id).Affinity);
    }

    [Fact]
    public void Legacy_save_and_repeated_migration_preserve_progress()
    {
        using(var k=host.Boot())
        {
            k.Db.Execute("DROP TABLE kz_npc_wish; DROP TABLE kz_wish_state; DELETE FROM core_migration WHERE (migration_id LIKE '%0404-npc-wishes%' OR migration_id LIKE '%0405-wish-home-mentor%')"); k.Save();
        }
        string[] before;
        using(var k=host.Boot())
        {
            Assert.Equal(3,Snapshot(k).Length);
            var migration=File.ReadAllText(Path.Combine(DlcTestHost.RepoRoot,"src/SimT.Modules.Dlc.Character/migrations/0404-npc-wishes.sql"));
            var id=k.Db.ScalarLong("SELECT npc_id FROM kz_npc_wish ORDER BY id LIMIT 1");
            Friend(k,id);
            k.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).MoveTo(Registry(k).Find(id)!.CurrentCitadelId(k.Capabilities),k.Today);
            Wishes(k).Accept(id);
            Assert.Single(Wishes(k).Journal());
            before=Snapshot(k); k.Db.Execute(migration); k.Db.Execute(migration); Assert.Equal(before,Snapshot(k)); k.Save();
        }
        using var reopened=host.Boot(); Assert.Equal(before,Snapshot(reopened)); Assert.Single(Wishes(reopened).Journal());
    }

    [Fact]
    public void Upgrade_preserves_accepted_legacy_mentor_and_replaces_fixed_target_with_skill()
    {
        long id;
        using(var k=host.Boot())
        {
            id=Fixture(k,"mentor",state:"accepted",target:Registry(k).All.Last().Id);
            k.Db.Execute("ALTER TABLE kz_npc_wish DROP COLUMN skill_code; DELETE FROM core_migration WHERE migration_id LIKE '%0405-wish-home-mentor%'");
            k.Save();
        }
        using var upgraded=host.Boot();
        var row=Wishes(upgraded).ForNpc(id)!;
        Assert.Equal("accepted",row.Wish.State);
        Assert.NotEmpty(row.Wish.SkillCode);
        Assert.Equal(0,row.Wish.TargetNpcId);
        Assert.Contains("Ta muốn tìm một bậc thầy về",row.RequestVi);
        Assert.Equal(1,upgraded.Db.ScalarLong("SELECT COUNT(*) FROM kz_npc_wish"));
    }

    [Fact]
    public void Missing_catalog_skills_and_military_prevent_unsupported_templates_without_society()
    {
        var product=Path.Combine(Path.GetDirectoryName(host.DatabasePath)!,"wish-product.json");
        File.WriteAllText(product,JsonSerializer.Serialize(new { id="wish",name="Wish",seed=1840502,startLocation="lac_duong",startingCash=2000,
            modules=ModuleLoader.Discover(AppContext.BaseDirectory).Select(m=>m.Manifest.Id).Where(id=>id is not "dlc.society" and not "dlc.power").ToArray() }));
        SimKernel Boot()=>SimKernel.Open(new KernelOptions(host.DatabasePath,AppContext.BaseDirectory,product,SimT.Testing.TestHost.ConstantsPath));
        using(var k=Boot())
        {
            var id=Fixture(k,state:"accepted");
            k.Capabilities.Get<IBag>(CapabilityIds.PlayerBag).Add(1,12,2);
            Assert.Contains("Đã giúp",Wishes(k).Give(id));
            Fixture(k); Events(k).Publish(new DayAdvanced(k.Today.AddDays(91)));
            k.Db.Execute("DELETE FROM kz_npc_wish; DELETE FROM kz_wish_state; UPDATE ct_item SET category='food'; DELETE FROM np_skill"); k.Save();
        }
        using var second=Boot();
        Assert.NotEmpty(Snapshot(second));
        Assert.Equal(0,second.Db.ScalarLong("SELECT COUNT(*) FROM kz_npc_wish WHERE template!='shop'"));
    }

    [Fact]
    public void Export_real_new_campaign_examples_for_external_qa()
    {
        using var k=host.Boot();
        var qa=Environment.GetEnvironmentVariable("SIMT_WISH_QA");
        if(string.IsNullOrWhiteSpace(qa)) return;
        Directory.CreateDirectory(qa);
        var ids=k.Db.Query("SELECT npc_id FROM kz_npc_wish ORDER BY id",r=>r.GetInt64(0));
        foreach(var id in ids) Friend(k,id);
        var rows=ids.Select(id=>Wishes(k).ForNpc(id)!).ToArray();
        File.WriteAllText(Path.Combine(qa,"new-campaign-examples.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions { WriteIndented=true }));
        var npc=rows.First(r=>r.Wish.Template=="mentor").Wish.NpcId;
        // QA travels to the actual NPC; no wish parameters are fabricated.
        k.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).MoveTo(Registry(k).Find(npc)!.CurrentCitadelId(k.Capabilities),k.Today);
        var scene=k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue).HandleChoice(npc,"wish:ask").UpdatedScene;
        File.WriteAllText(Path.Combine(qa,"talk.json"),JsonSerializer.Serialize(scene,new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase }));
        Wishes(k).Accept(npc);
        foreach(var candidate in Registry(k).All) Friend(k,candidate.Id);
        var picker=k.Capabilities.Get<IDialogueModule>(CapabilityIds.Dialogue).HandleChoice(npc,"wish:give").UpdatedScene;
        File.WriteAllText(Path.Combine(qa,"teacher-picker.json"),JsonSerializer.Serialize(picker,new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase }));
        k.Save();
        k.Db.Execute("PRAGMA wal_checkpoint(FULL)");
        File.Copy(host.DatabasePath,Path.Combine(qa,"campaign.db"),true);
    }
}
