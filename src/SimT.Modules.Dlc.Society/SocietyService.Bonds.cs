using SimT.Contracts.Playing;
using SimT.Contracts.Time;
namespace SimT.Modules.Dlc.Society;
internal sealed partial class SocietyService
{
    public void ReturnNpcHome(long npcId, GameDate date)
    {
        var npc = _registry.Find(npcId);
        if (npc is null) return;
        _db.Execute("UPDATE so_npc_activity SET current_citadel_id=@city, current_action='Về quê', updated_date=@date WHERE npc_id=@npc",
            new SimT.Contracts.Data.DbArgs().Add("@city",npc.CitadelId).Add("@date",GameDateCodec.Format(date)).Add("@npc",npcId));
    }
    public void RecordRelationshipEvent(GameDate date, long npcId, string headlineVi)
    {
        var npc = _registry.Find(npcId);
        if (npc is null) return;
        LogEvent(GameDateCodec.Format(date), npc.Id, npc.NameVi, npc.CurrentCitadelId(this), _map.Find(npc.CurrentCitadelId(this))?.NameVi ?? "", "relationship", headlineVi, headlineVi, "", 0);
    }

    public void RecordMilestoneEvent(GameDate date, string eventKind, string headlineVi, string detailVi, string statImpact)
    {
        var citId = _player.CurrentCityId;
        var citName = _map.Find(citId)?.NameVi ?? "Thiên hạ";
        LogEvent(GameDateCodec.Format(date), 0, _player.Name, citId, citName, eventKind, headlineVi, detailVi, statImpact, date.DayIndex);
    }
}
