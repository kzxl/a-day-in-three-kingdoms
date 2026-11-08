using SimT.Contracts.Data;
namespace SimT.Modules.Dlc.Power;
internal sealed partial class CourtService
{
    internal SimT.Contracts.Playing.ILordTasks? LordTasks { get; set; }
    private void CancelLordTask() => _db.Execute("UPDATE po_lord_task SET state='cancelled' WHERE state='active'");
    internal string RewardLordTask(int merit)
    {
        SetState("reputation", Reputation + merit);
        var rank = Offices.FirstOrDefault(o => o.Id == OfficeId)?.Rank ?? 0;
        var next = Offices.FirstOrDefault(o => o.Rank > rank);
        if (next is null || Reputation < next.ReputationRequired) return "";
        SetState("office_id", next.Id);
        return $" Chúa công thăng chức {next.NameVi} (phẩm {next.Rank})!";
    }
    internal void AddLordMerit(int merit) => SetState("reputation", Reputation + merit);
}
