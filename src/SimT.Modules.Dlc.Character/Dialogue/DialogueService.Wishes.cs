using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
namespace SimT.Modules.Dlc.Character;
internal sealed partial class DialogueService
{
    private void AddWishChoices(NpcDef npc,List<SceneChoice> choices)
    {
        if (!_context.Capabilities.TryGet<INpcWishes>(INpcWishes.CapabilityId,out var wishes)) return;
        var row=wishes?.ForNpc(npc.Id);
        if (row is null) return;
        if (row.Wish.State=="revealed") choices.Insert(0,new("wish:ask","Có điều gì muốn nhờ?","Lắng nghe ước nguyện"));
        else choices.Insert(0,new("wish:give","Trao",row.Ready?"Đã đủ điều kiện":row.ProgressVi));
    }
    private TalkChoiceOutcome HandleWishChoice(NpcDef npc,string choice)
    {
        if (!_context.Capabilities.TryGet<INpcWishes>(INpcWishes.CapabilityId,out var wishes) || wishes is null)
            return new(false,"Chưa có ước nguyện.");
        var row=wishes.ForNpc(npc.Id);
        if (row is null) return new(false,"Ước nguyện đã kết thúc.",UpdatedScene:BuildTalkScene(npc.Id));
        if (choice=="wish:ask")
        {
            var prompt=row.RequestVi + " Hạn: " + SimT.Contracts.Time.GameDateCodec.ShortVi(row.Wish.Deadline);
            return new(true,prompt,UpdatedScene:BuildTalkSceneCore(npc.Id,prompt,
                [new("wish:accept","Nhận lời","Ghi vào sổ nhiệm vụ"),new("wish:decline","Từ chối","Để dịp khác")]));
        }
        string message=choice switch
        {
            "wish:accept" => wishes.Accept(npc.Id),
            "wish:give" => wishes.Give(npc.Id),
            _ => "Ta hiểu, khi nào thuận tiện hãy quay lại."
        };
        return new(true,message,UpdatedScene:BuildTalkScene(npc.Id,message));
    }
}
