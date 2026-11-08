using SimT.Contracts.Playing;
namespace SimT.Modules.Dlc.Power;
internal sealed partial class LordTaskService
{
    private sealed record Template(string Kind,long Item,int Quantity,long City,long Target=0,int Baseline=0);
    private List<Template> Templates(int rank, FactionView faction)
    {
        var result = new List<Template> { new("tribute",0,80+rank*40,faction.CapitalCityId) };
        var items = context.Capabilities.Get<IItemCatalog>(CapabilityIds.ItemCatalog);
        var grain = items.FindByCode("gao");
        var city = court.Governances.Where(g => g.FactionId == faction.Id)
            .OrderByDescending(g => g.UnrestLevel).ThenBy(g => g.DefenseStrength).ThenBy(g => g.CitadelId).FirstOrDefault();
        if (grain is not null && city is not null) result.Add(new("grain",grain.Id,10+rank*5,city.CitadelId));
        var horse = items.All.OrderBy(i => i.Id).FirstOrDefault(i => i.Code is "chien_ma" or "ngua_chien" or "war_horse");
        if (horse is not null && rank >= 1) result.Add(new("horse",horse.Id,1+rank/2,faction.CapitalCityId));
        if (rank >= 2 && context.Capabilities.TryGet<SimT.Contracts.Scenes.IDialogueModule>(CapabilityIds.Dialogue,out _))
        {
            var registry = context.Capabilities.Get<INpcRegistry>(CapabilityIds.NpcRegistry);
            var destination = court.Governances.OrderBy(g => g.CitadelId).FirstOrDefault(g => g.FactionId != 0
                && g.FactionId != faction.Id && registry.Find(g.GovernorNpcId) is not null);
            if (destination is not null) result.Add(new("envoy",0,1,destination.CitadelId,destination.GovernorNpcId));
        }
        if (rank >= 2 && context.Capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand,out var military))
        {
            var stronghold = military!.AllStrongholds().Where(s => s.LastClearedDate is null
                || context.Clock.Today.DayIndex-s.LastClearedDate.Value.DayIndex >= 30)
                .OrderBy(s => s.DangerLevel).ThenBy(s => s.Id).FirstOrDefault();
            if (stronghold is not null) result.Add(new("bandit",0,1,stronghold.CitadelId,stronghold.Id,stronghold.ClearedCount));
        }
        return result;
    }
}
