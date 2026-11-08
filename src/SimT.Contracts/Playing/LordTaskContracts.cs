using SimT.Contracts.Time;
namespace SimT.Contracts.Playing;

public sealed record LordTask(long Id, long FactionId, string Template, long ItemId, int Quantity,
    long CityId, long TargetId, int Baseline, GameDate Assigned, GameDate Deadline, string State,
    int Merit, int Bonus, bool Evidence, bool Acknowledged);
public sealed record LordTaskProgress(LordTask Task, string RequestVi, string ProgressVi,
    long DestinationCityId, bool Ready);
public interface ILordTasks
{
    const string CapabilityId = "politics.lord-tasks";
    int Trust { get; }
    int ConsecutiveFailures { get; }
    string Notice { get; }
    LordTaskProgress? Current { get; }
    string Acknowledge(long npcId);
    string Report();
    bool CanAudience(long npcId);
    bool CanEnvoy(long npcId);
    string DeliverEnvoy(long npcId);
}
