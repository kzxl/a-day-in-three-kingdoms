using SimT.Contracts.Data;
using SimT.Contracts.Playing;
namespace SimT.Modules.Dlc.Character;
internal sealed partial class NpcWishService
{
    // NpcRank is ordered S, A, B, C: smaller values mean higher rank.
    private IEnumerable<NpcDef> EligibleTeachers(NpcDef npc,string skillCode) => Registry.All.Where(n=>
        n.Id!=npc.Id && Social.GetNpcRank(n.Id)<=Social.GetNpcRank(npc.Id) &&
        n.Skills.Any(s=>s.Code==skillCode && s.Level>(npc.Skills.FirstOrDefault(x=>x.Code==skillCode)?.Level??0)));
    private string SkillName(string code) => Registry.All.SelectMany(n=>n.Skills).FirstOrDefault(s=>s.Code==code)?.NameVi??code;
    public IReadOnlyList<NpcDef> MentorCandidates(long npcId)
    {
        var wish=All().FirstOrDefault(w=>w.NpcId==npcId && Open(w) && w.Template=="mentor");
        var npc=Registry.Find(npcId);
        return wish is null || npc is null ? [] : EligibleTeachers(npc,wish.SkillCode).Where(n=>Social.HasMet(n.Id)).OrderBy(n=>n.Id).Take(5).ToArray();
    }
    public string Introduce(long npcId,long teacherId)
    {
        var row=ForNpc(npcId);
        if (row is null || row.Wish.State!="accepted" || row.Wish.Template!="mentor" || !Nearby(npcId) ||
            !Social.HasMet(teacherId) || !EligibleTeachers(Registry.Find(npcId)!,row.Wish.SkillCode).Any(n=>n.Id==teacherId))
            return "Người được giới thiệu chưa đủ điều kiện.";
        return Complete(row with { Wish=row.Wish with { TargetNpcId=teacherId } });
    }
    private bool HomeReached(NpcWish wish) => Registry.Find(wish.NpcId) is {} npc &&
        (npc.CurrentCitadelId(context.Capabilities)==npc.CitadelId || (Social.GetRetainer(npc.Id) is not null && Player.CurrentCityId==npc.CitadelId));
    private void ReturnHome(NpcWish wish)
    {
        if (context.Capabilities.TryGet<ISocietyChronicle>(CapabilityIds.SocietyChronicle,out var society))
            society?.ReturnNpcHome(wish.NpcId,context.Clock.Today);
    }
    public string PayHomeFare(long npcId)
    {
        var row=ForNpc(npcId);
        if (row is null || row.Wish.Template!="home" || row.Wish.State!="accepted" || !row.Ready)
            return "Chưa đủ điều kiện trả lộ phí.";
        return Complete(row);
    }
}
