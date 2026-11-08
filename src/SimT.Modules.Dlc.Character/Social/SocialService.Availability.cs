using SimT.Contracts;
namespace SimT.Modules.Dlc.Character;
internal sealed partial class SocialService
{
    private void RequirePresentNpc(long npcId)
    {
        if (!_registry.IsPresent(npcId))
            throw new SimException("social.npc_unavailable", "Nhân sĩ chưa xuất hiện trong thời kỳ này.");
    }
}
