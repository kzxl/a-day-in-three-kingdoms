using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.Player;

/// <summary>
/// Handles user commands issued on the "player.reputation" screen.
/// </summary>
internal sealed class PlayerReputationCommandHandler : IScreenCommandHandler
{
    private readonly IPlayerReputation _reputation;

    public PlayerReputationCommandHandler(IPlayerReputation reputation)
    {
        _reputation = reputation;
    }

    public string ScreenId => "player.reputation";

    public string? Execute(string commandId)
    {
        return commandId switch
        {
            "rep.charity_small" => HandleCharity(50),
            "rep.charity_large" => HandleCharity(200),
            "rep.banquet" => HandleBanquet(100),
            "rep.proclaim" => HandleProclaim(),
            _ => null
        };
    }

    private string HandleCharity(int copper)
    {
        var result = _reputation.PerformCharity(copper);
        return result.MessageVi;
    }

    private string HandleBanquet(int copper)
    {
        var result = _reputation.HostBanquet(copper);
        return result.MessageVi;
    }

    private string HandleProclaim()
    {
        var result = _reputation.ProclaimDeeds();
        return result.MessageVi;
    }
}
