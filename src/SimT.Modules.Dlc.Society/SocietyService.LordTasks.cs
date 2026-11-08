using SimT.Contracts.Time;
namespace SimT.Modules.Dlc.Society;
internal sealed partial class SocietyService
{
    public void RecordGovernanceEvent(GameDate date, long npcId, long cityId, string headlineVi)
    {
        var npc = _registry.Find(npcId);
        LogEvent(GameDateCodec.Format(date),npcId,npc?.NameVi ?? "Chúa công",cityId,
            _map.Find(cityId)?.NameVi ?? "","governance",headlineVi,headlineVi,"",0);
    }
}
