using SimT.Contracts.Scenes;
using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class NpcWishService(ModuleContext context) : INpcWishes
{
    private INpcRegistry Registry => context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
    private IPlayerState Player => context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
    private ISocialNetwork Social => context.Capabilities.Get<ISocialNetwork>(CapabilityIds.SocialNetwork);
    private IItemCatalog Items => context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
    private IWallet Wallet => context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
    private IBag Bag => context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
    private IReadOnlyList<NpcWish> All() => context.Db.Query(
        "SELECT id,npc_id,template,item_id,quantity,city_id,target_npc_id,opened,deadline,state,evidence,skill_code FROM kz_npc_wish ORDER BY id",
        r => new NpcWish(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetInt64(3),r.GetInt32(4),r.GetInt64(5),r.GetInt64(6),
            GameDateCodec.Parse(r.GetString(7)),GameDateCodec.Parse(r.GetString(8)),r.GetString(9),r.GetInt32(10)!=0,r.GetString(11)));
    private static bool Open(NpcWish w) => w.State is "hidden" or "revealed" or "accepted";
    private DbArgs Args(NpcWish w) => new DbArgs().Add("@id",w.Id).Add("@npc",w.NpcId).Add("@d",GameDateCodec.Format(context.Clock.Today));

    public void Initialize()
    {
        if (context.Db.ScalarLong("SELECT COUNT(*) FROM kz_wish_state WHERE key='initialized'") != 0) return;
        // Isolate new-save draws from established simulation draws, as for NPC bonds.
        var state = context.Rng.State;
        context.Rng.Reseed(state ^ 0x574953484E504353UL);
        try
        {
            context.Db.Transaction(() =>
            {
                Generate(context.Clock.Today);
                context.Db.Execute("INSERT INTO kz_wish_state VALUES ('initialized')");
            });
        }
        finally { context.Rng.Reseed(state); }
    }

    public void OnMonthClosed(GameDate date)
    {
        string key = "month:" + GameDateCodec.Format(date);
        if (context.Db.ScalarLong("SELECT COUNT(*) FROM kz_wish_state WHERE key=@k",DbArgs.One("@k",key)) != 0) return;
        context.Db.Transaction(() =>
        {
            Refresh(date);
            Generate(date);
            context.Db.Execute("INSERT INTO kz_wish_state VALUES (@k)",DbArgs.One("@k",key));
        });
    }

    private void Generate(GameDate date)
    {
        var active = All().Where(Open).ToArray();
        int slots = Math.Min(NpcWishRules.MonthlyLimit,NpcWishRules.OpenLimit-active.Length);
        var pool = Registry.All.Where(n => !active.Any(w => w.NpcId==n.Id)).OrderBy(n=>n.Id).ToList();
        while (slots > 0 && pool.Count > 0)
        {
            int index = context.Rng.Next(0,pool.Count);
            var npc = pool[index]; pool.RemoveAt(index);
            long city = npc.CurrentCitadelId(context.Capabilities);
            if (context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap).Find(city) is null) continue;
            var candidates = new List<(string Kind,long Item,int Quantity,long Target)>();
            if (RoleTitleVi.ToArchetypeKey(npc.Role)=="merchant") candidates.Add(("shop",0,context.Rng.Next(300,801),0));
            // Catalog luxury goods with a high base price are the existing rare trade goods.
            foreach (var item in Items.All.Where(i=>i.Category=="luxury" && i.BasePrice>=150).OrderBy(i=>i.Id))
                candidates.Add(("rare",item.Id,2,0));
            if (context.Capabilities.IsProvided(CapabilityIds.MilitaryCommand))
            foreach (var bond in context.Capabilities.Get<INpcBonds>(INpcBonds.CapabilityId).ForNpc(npc.Id).Where(b=>b.Kind=="rival"))
            {
                var rival = Registry.Find(bond.Other(npc.Id));
                if (rival is not null && rival.Attributes.War>=30) candidates.Add(("revenge",0,0,rival.Id));
            }
            var mentorSkills=Registry.All.SelectMany(n=>n.Skills).DistinctBy(s=>s.Code).OrderBy(s=>s.Code)
                .Where(s=>EligibleTeachers(npc,s.Code).Any()).ToArray();
            if (mentorSkills.Length>0) candidates.Add(("mentor",0,0,0));
            if (city!=npc.CitadelId) candidates.Add(("home",0,0,0));
            if (candidates.Count==0) continue;
            var chosen = candidates[context.Rng.Next(0,candidates.Count)];
            string skillCode = chosen.Kind=="mentor" ? mentorSkills[context.Rng.Next(0,mentorSkills.Length)].Code : "";
            context.Db.Execute("INSERT INTO kz_npc_wish(npc_id,template,item_id,quantity,city_id,target_npc_id,opened,deadline,state,skill_code) VALUES (@n,@t,@i,@q,@c,@target,@d,@end,'hidden',@skill)",
                new DbArgs().Add("@n",npc.Id).Add("@t",chosen.Kind).Add("@i",chosen.Item).Add("@q",chosen.Quantity).Add("@c",chosen.Kind=="home"?npc.CitadelId:city)
                    .Add("@skill",skillCode).Add("@target",chosen.Target).Add("@d",GameDateCodec.Format(date)).Add("@end",GameDateCodec.Format(date.AddDays(90))));
            slots--;
        }
    }

    public void Refresh(GameDate date)
    {
        foreach (var old in All().Where(w=>Open(w) && w.Template=="mentor" && w.SkillCode==""))
        {
            var npc=Registry.Find(old.NpcId)!;
            var skill=Registry.All.SelectMany(n=>n.Skills).OrderBy(s=>s.Code).FirstOrDefault(s=>EligibleTeachers(npc,s.Code).Any());
            context.Db.Execute("UPDATE kz_npc_wish SET skill_code=@skill,target_npc_id=0,state=@state WHERE id=@id",
                new DbArgs().Add("@skill",skill?.Code??"").Add("@state",skill is null?"expired":old.State).Add("@id",old.Id));
        }
        foreach (var wish in All().Where(Open))
        {
            if (date > wish.Deadline)
            {
                bool self = context.Rng.Next(0,100)<35;
                if (self && wish.Template=="home") ReturnHome(wish);
                context.Db.Execute("UPDATE kz_npc_wish SET state=@s WHERE id=@id",new DbArgs().Add("@s",self?"self_completed":"expired").Add("@id",wish.Id));
                string message = self ? $"{Registry.Find(wish.NpcId)?.NameVi} tự hoàn thành ước nguyện sau khi quá hạn." : $"Ước nguyện của {Registry.Find(wish.NpcId)?.NameVi} quá hạn.";
                if (wish.State=="accepted")
                {
                    message += $" Bạn lỡ hẹn (-{NpcWishRules.ExpiryPenalty} thiện cảm).";
                    ChangeAffinity(wish,-NpcWishRules.ExpiryPenalty,message,date);
                }
                Record(wish,message,date);
            }
            else if (wish.Template=="home" && wish.State=="accepted" && HomeReached(wish))
            {
                Complete(Progress(wish));
            }
            else if (wish.State=="hidden" && Social.GetRelationship(wish.NpcId).Affinity>=NpcWishRules.RevealAffinity)
                context.Db.Execute("UPDATE kz_npc_wish SET state='revealed' WHERE id=@id",DbArgs.One("@id",wish.Id));
        }
    }

    public IReadOnlyList<NpcWishProgress> Journal()
    {
        Refresh(context.Clock.Today);
        return All().Where(w=>w.State=="accepted").Select(Progress).ToArray();
    }
    public NpcWishProgress? ForNpc(long npcId)
    {
        Refresh(context.Clock.Today);
        var wish = All().FirstOrDefault(w=>w.NpcId==npcId && w.State is "revealed" or "accepted");
        return wish is null ? null : Progress(wish);
    }
    private bool Nearby(long npcId) => Registry.Find(npcId) is { } npc &&
        (npc.CurrentCitadelId(context.Capabilities)==Player.CurrentCityId || Social.GetRetainer(npcId) is not null);
    private NpcWishProgress Progress(NpcWish w)
    {
        var npc = Registry.Find(w.NpcId)!;
        string target = Registry.Find(w.TargetNpcId)?.NameVi ?? "người ấy";
        string request = w.Template switch
        {
            "shop" => $"Ta muốn mở tiệm, mong được góp {w.Quantity} quan.",
            "rare" => $"Ta đang tìm {w.Quantity} {Items.Find(w.ItemId)?.NameVi}.",
            "revenge" => $"Xin giúp ta thắng {target} trên lôi đài.",
            "home" => $"Ta muốn về quê ở {context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap).Find(npc.CitadelId)?.NameVi}. Bạn có thể đưa ta về hoặc giúp {NpcWishRules.HomeFare} quan lộ phí.",
            _ => $"Ta muốn tìm một bậc thầy về {SkillName(w.SkillCode)}."
        };
        bool ready = Nearby(w.NpcId);
        string progress;
        long destination = npc.CurrentCitadelId(context.Capabilities);
        switch(w.Template)
        {
            case "shop": ready &= Wallet.Liquid>=w.Quantity; progress=$"Vốn: {Wallet.Liquid}/{w.Quantity} quan"; break;
            case "rare":
                int held=Bag.Quantity(Player.CurrentCityId,w.ItemId);
                ready &= held>=w.Quantity; progress=$"Hành trang: {held}/{w.Quantity}"; break;
            case "revenge":
                ready &= w.Evidence; progress=w.Evidence?"Đã thắng đối thủ — về báo tin":"Chưa thắng đối thủ";
                if (!w.Evidence && Registry.Find(w.TargetNpcId) is {} rival) destination=rival.CurrentCitadelId(context.Capabilities);
                break;
            case "home":
                destination=npc.CitadelId;
                ready &= Wallet.Liquid>=NpcWishRules.HomeFare;
                progress=$"Lộ phí: {Wallet.Liquid}/{NpcWishRules.HomeFare} quan; hoặc cùng gia thần tới quê";
                break;
            default:
                bool met=MentorCandidates(w.NpcId).Count>0;
                ready &= met; progress=met?"Đã quen người đủ điều kiện — hãy chọn thầy":"Hãy làm quen người có kỹ năng cao hơn, phẩm không thấp hơn";
                break;
        }
        return new(w,npc.NameVi,request,progress,destination,ready);
    }
    public string Accept(long npcId)
    {
        var row=ForNpc(npcId);
        if (row is null || row.Wish.State!="revealed" || !Nearby(npcId)) return "Chưa thể nhận ước nguyện này.";
        context.Db.Execute("UPDATE kz_npc_wish SET state='accepted' WHERE id=@id",DbArgs.One("@id",row.Wish.Id));
        return "Đã nhận ước nguyện. Xem tiến độ và hạn trong Nhật Trình.";
    }
    public void OnDuel(long npcId, bool won, GameDate date)
    {
        Refresh(date);
        if (won) context.Db.Execute("UPDATE kz_npc_wish SET evidence=1 WHERE template='revenge' AND state='accepted' AND target_npc_id=@npc AND opened<=@d",
            new DbArgs().Add("@npc",npcId).Add("@d",GameDateCodec.Format(date)));
    }
    private void ChangeAffinity(NpcWish w,int delta,string message,GameDate date)
    {
        context.Db.Execute("INSERT INTO kz_relationship(npc_id,affinity,last_interaction_date) VALUES (@npc,0,@d) ON CONFLICT(npc_id) DO NOTHING",Args(w));
        context.Db.Execute("UPDATE kz_relationship SET affinity=MIN(100,MAX(-100,affinity+@delta)) WHERE npc_id=@npc",Args(w).Add("@delta",delta));
        context.Db.Execute("INSERT INTO kz_memory(npc_id,date,topic,sentiment,affinity_delta,summary_vi) VALUES (@npc,@date,'wish',@sentiment,@delta,@msg)",
            Args(w).Add("@date",GameDateCodec.Format(date)).Add("@sentiment",delta>0?"positive":"negative").Add("@delta",delta).Add("@msg",message));
    }
    private void Record(NpcWish w,string message,GameDate date)
    {
        if (context.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle,out var chronicle))
            chronicle?.RecordRelationshipEvent(date,w.NpcId,message);
    }
}
