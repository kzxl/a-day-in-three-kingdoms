using SimT.Contracts.Playing;
using SimT.Contracts.Time;
namespace SimT.Modules.Dlc.Society;
internal sealed partial class SocietyService
{
    public void RecordRelationshipEvent(GameDate date, long npcId, string headlineVi)
    {
        var npc = _registry.Find(npcId);
        if (npc is null) return;
        LogEvent(GameDateCodec.Format(date), npc.Id, npc.NameVi, npc.CurrentCitadelId(this), _map.Find(npc.CurrentCitadelId(this))?.NameVi ?? "", "relationship", headlineVi, headlineVi, "", 0);
    }
}
