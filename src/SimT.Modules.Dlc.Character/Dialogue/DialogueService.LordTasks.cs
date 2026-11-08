using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Time;
namespace SimT.Modules.Dlc.Character;
internal sealed partial class DialogueService
{
    private TalkScene DecorateLordScene(TalkScene scene, string? feedbackVi)
    {
        if (!_context.Capabilities.TryGet<ILordTasks>(ILordTasks.CapabilityId,out var tasks) || tasks is null) return scene;
        if (tasks.CanEnvoy(scene.NpcId))
            return scene with { Choices = new[] { new SceneChoice("lord:envoy","Trao quốc thư","Hoàn thành việc đi sứ") }.Concat(scene.Choices).ToList() };
        if (!tasks.CanAudience(scene.NpcId)) return scene;
        var row = tasks.Current;
        var faction = _context.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice).Factions.First(f=>f.LeaderNpcId==scene.NpcId);
        var opening = faction.Code switch
        {
            "cao_wei" => "Thiên hạ chưa định, quân lương phải đủ. Ta trọng người làm được việc.",
            "shu_han" => "Vì bách tính và cơ nghiệp, ta giao khanh việc này.",
            "dong_wu" => "Giữ vững cơ nghiệp Giang Đông, khanh hãy tận tâm lĩnh việc.",
            _ => "Khanh đã đầu quân, hãy tận lực vì cơ nghiệp của ta."
        };
        var lines = row is null ? new List<string> { "Tháng này chưa có công vụ mới. Đầu tháng hãy lại bái kiến ta." }
            : new List<string> { opening, row.RequestVi, $"Hạn {GameDateCodec.ShortVi(row.Task.Deadline)}. Thưởng {row.Task.Merit} danh vọng, {row.Task.Bonus} quan.",row.ProgressVi };
        // Keep the full result in the conversation: the small feedback banner is limited to 90 characters.
        if (!string.IsNullOrWhiteSpace(feedbackVi))
            lines.InsertRange(0, feedbackVi.Split(". ", StringSplitOptions.RemoveEmptyEntries));
        var choices = new List<SceneChoice>();
        if (row is not null)
        {
            if (!row.Task.Acknowledged) choices.Add(new("lord:accept","Lĩnh mệnh","Ghi nhận quân lệnh"));
            else choices.Add(new("lord:report","Trình lên",row.ProgressVi));
        }
        choices.Add(new("lord:leave","Rời thế lực","Hủy công vụ đang làm"));
        choices.Add(new("bye","Cáo lui","Tiếp tục công vụ"));
        return scene with { Lines=lines,Choices=choices,RoleVi="Chúa công",RelationTierVi=$"Tín nhiệm {tasks.Trust}/100" };
    }
    private TalkChoiceOutcome HandleLordChoice(NpcDef npc,string choice)
    {
        if (!_context.Capabilities.TryGet<ILordTasks>(ILordTasks.CapabilityId,out var tasks) || tasks is null)
            return new(false,"Chưa có công vụ.");
        string message;
        if (choice == "lord:envoy") message=tasks.DeliverEnvoy(npc.Id);
        else if (!tasks.CanAudience(npc.Id)) return new(false,"Không phải chúa công của bạn.");
        else message=choice switch
        {
            "lord:accept" => tasks.Acknowledge(npc.Id),
            "lord:report" => tasks.Report(),
            "lord:leave" => _context.Capabilities.Get<IPoliticalCourt>(CapabilityIds.CourtOffice).LeaveFaction().Message,
            _ => "Hãy tận tâm làm việc."
        };
        return new(true,message,UpdatedScene:BuildTalkScene(npc.Id,message));
    }
}
