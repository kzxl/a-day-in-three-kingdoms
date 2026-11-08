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
        else choices.Insert(0,new("wish:give",row.Wish.Template=="mentor"?"Giới thiệu thầy":row.Wish.Template=="home"?"Trả lộ phí":"Trao",row.Ready?"Đã đủ điều kiện":row.ProgressVi));
    }
    private TalkChoiceOutcome HandleWishChoice(NpcDef npc,string choice)
    {
        if (!_context.Capabilities.TryGet<INpcWishes>(INpcWishes.CapabilityId,out var wishes) || wishes is null)
            return new(false,"Chưa có ước nguyện.");
        var row=wishes.ForNpc(npc.Id);
        if (row is null)
        {
            const string ended = "Ước nguyện đã kết thúc.";
            return new(false,ended,UpdatedScene:WithFeedbackExpression(BuildTalkScene(npc.Id,ended),"sad"));
        }
        if (choice=="wish:ask")
        {
            var prompt=row.RequestVi + " Hạn: " + SimT.Contracts.Time.GameDateCodec.ShortVi(row.Wish.Deadline);
            return new(true,prompt,UpdatedScene:BuildTalkSceneCore(npc.Id,prompt,
                [new("wish:accept","Nhận lời","Ghi vào sổ nhiệm vụ"),new("wish:decline","Từ chối","Để dịp khác")]));
        }
        if (choice=="wish:give" && row.Wish.Template=="mentor")
        {
            var choices=wishes.MentorCandidates(npc.Id).Select(n=>new SceneChoice($"wish:teacher:{n.Id}",n.NameVi,"Giới thiệu làm thầy")).ToList();
            choices.Add(new("wish:cancel","Thôi","Để dịp khác"));
            return new(true,row.RequestVi,UpdatedScene:BuildTalkSceneCore(npc.Id,row.RequestVi,choices));
        }
        if (choice.StartsWith("wish:teacher:") && long.TryParse(choice[13..],out var teacherId))
        {
            var result=wishes.Introduce(npc.Id,teacherId);
            return new(true,result,UpdatedScene:BuildTalkScene(npc.Id,result));
        }
        string message=choice switch
        {
            "wish:accept" => wishes.Accept(npc.Id),
            "wish:give" => wishes.Give(npc.Id),
            _ => "Ta hiểu, khi nào thuận tiện hãy quay lại."
        };
        var expression = choice == "wish:decline" ? "sad" : choice == "wish:accept" ? "happy" : "normal";
        return new(true,message,UpdatedScene:WithFeedbackExpression(BuildTalkScene(npc.Id,message),expression));
    }
}
