using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class LordTaskService(ModuleContext context, CourtService court) : ILordTasks
{
    private IPlayerState Player => context.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState);
    private IWallet Wallet => context.Capabilities.Get<IWallet>(CapabilityIds.PlayerWallet);
    private IBag Bag => context.Capabilities.Get<IBag>(CapabilityIds.PlayerBag);
    private FactionView? Faction => court.Factions.FirstOrDefault(f => f.Id == court.AllegianceFactionId);
    private long Day => context.Clock.Today.DayIndex;
    private void EnsureTrust() => context.Db.Execute("INSERT OR IGNORE INTO po_lord_trust(faction_id) VALUES (@f)", DbArgs.One("@f", court.AllegianceFactionId));
    private long Value(string column)
    {
        if (Faction is null) return 0;
        EnsureTrust();
        return context.Db.ScalarLong($"SELECT {column} FROM po_lord_trust WHERE faction_id=@f", DbArgs.One("@f", Faction.Id));
    }
    public int Trust => (int)Value("trust");
    public int ConsecutiveFailures => (int)Value("failures");
    public string Notice => context.Db.ScalarString("SELECT notice FROM po_lord_trust WHERE faction_id=@f",
        DbArgs.One("@f",Faction?.Id ?? context.Db.ScalarLong("SELECT faction_id FROM po_lord_task ORDER BY id DESC LIMIT 1"))) ?? "";
    internal LordTask? Active => context.Db.Query(
        "SELECT id,faction_id,template,item_id,quantity,city_id,target_id,baseline,assigned_day,deadline_day,state,merit,bonus,evidence,acknowledged FROM po_lord_task WHERE state='active'",
        r => new LordTask(r.GetInt64(0),r.GetInt64(1),r.GetString(2),r.GetInt64(3),r.GetInt32(4),r.GetInt64(5),r.GetInt64(6),r.GetInt32(7),
            GameDate.FromDayIndex(r.GetInt64(8)),GameDate.FromDayIndex(r.GetInt64(9)),r.GetString(10),r.GetInt32(11),r.GetInt32(12),r.GetInt32(13)!=0,r.GetInt32(14)!=0)).FirstOrDefault();

    public LordTaskProgress? Current
    {
        get
        {
            Tick();
            var task = Active;
            if (task is null || task.FactionId != court.AllegianceFactionId) return null;
            var city = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap).Find(task.CityId)?.NameVi ?? "thành đích";
            var item = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog).All.FirstOrDefault(i => i.Id == task.ItemId)?.NameVi ?? "hàng";
            var strongholdName = task.Template == "bandit" && context.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand,out var military)
                ? military?.AllStrongholds().FirstOrDefault(s=>s.Id==task.TargetId)?.NameVi ?? "ổ phỉ" : "ổ phỉ";
            var count = task.Template == "tribute" ? Wallet.Liquid : Bag.Quantity(Player.CurrentCityId, task.ItemId);
            var ready = task.Template switch
            {
                "envoy" => task.Evidence,
                "bandit" => task.Evidence,
                _ => count >= task.Quantity
            };
            var request = task.Template switch
            {
                "grain" => $"Vận {task.Quantity} {item} tới {city}.",
                "horse" => $"Nộp {task.Quantity} {item} ở thủ phủ {city}.",
                "envoy" => $"Đi sứ {city}, nói chuyện với thái thú rồi về báo ở thủ phủ.",
                "bandit" => $"Thắng {strongholdName} tại {city} rồi về báo ở thủ phủ.",
                _ => $"Nộp cống {task.Quantity} quan tại {city}."
            };
            var destination = task.Template is "envoy" or "bandit" && ready ? Faction!.CapitalCityId : task.CityId;
            return new(task,request,ready ? "Đã đủ điều kiện — hãy Trình lên" : task.Template is "envoy" or "bandit" ? "Chưa có tin hoàn thành" : $"Đang có {count}/{task.Quantity}",destination,ready);
        }
    }

    internal void AssignMonth(GameDate date)
    {
        Tick();
        var faction = Faction;
        if (faction is null || Active is not null) return;
        var month = date.Year * 12 + date.Month;
        if (context.Db.ScalarLong("SELECT COUNT(*) FROM po_lord_task WHERE month_key=@m", DbArgs.One("@m",month)) > 0) return;
        EnsureTrust();
        var rank = court.Offices.FirstOrDefault(o => o.Id == court.OfficeId)?.Rank ?? 0;
        var options = Templates(rank, faction);
        var chosen = options[(int)((month + rank) % options.Count)];
        context.Db.Execute("""
            INSERT INTO po_lord_task(faction_id,month_key,template,item_id,quantity,city_id,target_id,baseline,assigned_day,deadline_day,merit,bonus)
            VALUES(@f,@m,@t,@i,@q,@c,@n,@b,@d,@end,@merit,@bonus)
            """, new DbArgs().Add("@f",faction.Id).Add("@m",month).Add("@t",chosen.Kind).Add("@i",chosen.Item)
            .Add("@q",chosen.Quantity).Add("@c",chosen.City).Add("@n",chosen.Target).Add("@b",chosen.Baseline)
            .Add("@d",date.DayIndex).Add("@end",date.AddDays(25).DayIndex).Add("@merit",6+rank*2).Add("@bonus",60+rank*20));
        Record($"{faction.NameVi} giao công vụ tháng {date.Month}; hạn 25 ngày.");
    }

    internal void Tick()
    {
        var task = Active;
        if (task is null) return;
        if (task.FactionId != court.AllegianceFactionId)
        {
            context.Db.Execute("UPDATE po_lord_task SET state='cancelled' WHERE id=@id",DbArgs.One("@id",task.Id));
            return;
        }
        if (Day > task.Deadline.DayIndex)
        {
            context.Db.Transaction(() =>
            {
                context.Db.Execute("UPDATE po_lord_task SET state='failed' WHERE id=@id",DbArgs.One("@id",task.Id));
                context.Db.Execute("UPDATE po_lord_trust SET trust=MAX(0,trust-15),failures=failures+1 WHERE faction_id=@f",DbArgs.One("@f",task.FactionId));
                var expelled = ConsecutiveFailures >= 3;
                Record(expelled ? "Trượt công vụ ba lần liên tiếp, chúa công đuổi khỏi thế lực." : "Quá hạn công vụ, tín nhiệm giảm 15.");
                if (expelled) court.LeaveFaction();
            });
            return;
        }
        if (task.Template == "bandit" && context.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand,out var military)
            && military!.AllStrongholds().Any(s => s.Id == task.TargetId && s.ClearedCount > task.Baseline
                && s.LastClearedDate?.DayIndex >= task.Assigned.DayIndex))
            context.Db.Execute("UPDATE po_lord_task SET evidence=1 WHERE id=@id",DbArgs.One("@id",task.Id));
    }

    public bool CanAudience(long npcId) => Faction is { } f && f.LeaderNpcId == npcId;
    public string Acknowledge(long npcId)
    {
        Tick();
        if (!CanAudience(npcId) || Active is not { } task) return "Chưa có công vụ tháng này; hãy đợi đầu tháng.";
        EnsureTrust();
        context.Db.Execute("UPDATE po_lord_task SET acknowledged=1 WHERE id=@id",DbArgs.One("@id",task.Id));
        return "Lĩnh mệnh! Công vụ đã ghi trong sổ nhiệm vụ.";
    }
    public bool CanEnvoy(long npcId) => Current is { Task.Template: "envoy", Task.Evidence: false, Task.Acknowledged: true } row
        && row.Task.TargetId == npcId && Player.CurrentCityId == row.Task.CityId;
    public string DeliverEnvoy(long npcId)
    {
        if (!CanEnvoy(npcId)) return "Không thể trao quốc thư tại đây.";
        context.Db.Execute("UPDATE po_lord_task SET evidence=1 WHERE state='active'");
        return "Thái thú đã nhận quốc thư. Hãy về thủ phủ báo chúa công.";
    }
    public string Report()
    {
        var row = Current;
        if (row is null || !row.Ready || !row.Task.Acknowledged || Player.CurrentCityId != row.DestinationCityId)
            return "Chưa đủ điều kiện trình lên; hãy lĩnh mệnh và tới đúng thành trong sổ nhiệm vụ.";
        var task = row.Task;
        var message = $"Hoàn thành công vụ: +{task.Merit} danh vọng, +{task.Bonus} quan, +10 tín nhiệm.";
        context.Db.Transaction(() =>
        {
            if (task.Template is "grain" or "horse" && !Bag.Remove(Player.CurrentCityId,task.ItemId,task.Quantity))
                throw new InvalidOperationException("Hàng không đủ để trình lên.");
            if (task.Template == "tribute") Wallet.Charge(task.Quantity,"politics.lord-task",row.RequestVi,$"lord:cost:{task.Id}");
            context.Db.Execute("UPDATE po_lord_task SET state='completed' WHERE id=@id",DbArgs.One("@id",task.Id));
            Wallet.Pay(task.Bonus,"politics.lord-task",message,$"lord:reward:{task.Id}");
            context.Db.Execute("UPDATE po_lord_trust SET trust=MIN(100,trust+10),failures=0 WHERE faction_id=@f",DbArgs.One("@f",task.FactionId));
            if (Trust >= 60) message += court.RewardLordTask(task.Merit);
            else court.AddLordMerit(task.Merit);
            Record(message);
        });
        return message;
    }
    private void Record(string message)
    {
        context.Db.Execute("UPDATE po_lord_trust SET notice=@n WHERE faction_id=@f",new DbArgs().Add("@n",message).Add("@f",court.AllegianceFactionId));
        if (context.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle,out var chronicle))
            chronicle?.RecordGovernanceEvent(context.Clock.Today,Faction?.LeaderNpcId ?? 0,Player.CurrentCityId,message);
    }
}
