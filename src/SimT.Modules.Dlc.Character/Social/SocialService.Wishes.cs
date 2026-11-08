using SimT.Contracts.Data;
namespace SimT.Modules.Dlc.Character;
internal sealed partial class SocialService
{
    private bool HasCompletedWish(long npcId) => _db.ScalarLong(
        "SELECT COUNT(*) FROM kz_npc_wish WHERE npc_id=@id AND state='completed'",DbArgs.One("@id",npcId))>0;
}
