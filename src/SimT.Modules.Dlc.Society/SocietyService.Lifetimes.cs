using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService
{
    private readonly HashSet<long> _newlyArrivedNpcIds = new();

    private void RefreshNpcLifetimes()
    {
        // A maturing character may not have had activity rows at campaign initialization.
        var date = GameDateCodec.Format(_clock.Today);
        foreach (var npc in _registry.All)
        {
            var added = _db.Execute("INSERT OR IGNORE INTO so_npc_activity (npc_id,current_citadel_id,current_wealth,focus_path,current_action,stamina,reputation,updated_date) " +
                "VALUES (@id,@cit,100,'wanderer','Ở quê',100,50,@date)",
                new DbArgs().Add("@id",npc.Id).Add("@cit",npc.CitadelId).Add("@date",date));
            if (added > 0) _newlyArrivedNpcIds.Add(npc.Id);
            _db.Execute("INSERT OR IGNORE INTO so_npc_stat_delta (npc_id,updated_date) VALUES (@id,@date)",
                new DbArgs().Add("@id",npc.Id).Add("@date",date));
        }
        if (_broker is null || !_broker.TryGet<INpcLifetimes>(INpcLifetimes.CapabilityId, out var lifetimes) || lifetimes is null) return;
        foreach (var death in lifetimes.Deaths)
        {
            // Named historical deaths retain their existing event and chronicle entry.
            if (GetHistoricalEvents().Any(e => e.Impacts.Any(i => i.Kind == "npc_death" &&
                _registry.FindByCode(i.NpcCode ?? "")?.Id == death.NpcId))) continue;
            var key = $"lifetime:{death.NpcId}:{death.Year}";
            if (_db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE stat_impact=@key", DbArgs.One("@key",key)) > 0) continue;
            LogEvent(date, death.NpcId, death.NameVi, death.CitadelId,
                _map.Find(death.CitadelId)?.NameVi ?? "Thiên hạ", "history",
                $"{death.NameVi} qua đời", $"Qua đời năm {death.Year}. Sử quan ghi lại tin buồn.", key, _clock.Today.DayIndex);
            _db.Execute("UPDATE so_npc_activity SET current_action='Đã mất', stamina=0 WHERE npc_id=@id", DbArgs.One("@id",death.NpcId));
        }
    }
}
