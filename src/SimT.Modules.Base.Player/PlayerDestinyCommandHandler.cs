using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Base.Player;

/// <summary>
/// Handles user commands issued on the "player.destiny" screen (allocating talents, respec, evaluating).
/// </summary>
internal sealed class PlayerDestinyCommandHandler : IScreenCommandHandler
{
    private readonly IPlayerDestiny _destiny;

    public PlayerDestinyCommandHandler(IPlayerDestiny destiny)
    {
        _destiny = destiny;
    }

    public string ScreenId => "player.destiny";

    public string? Execute(string commandId)
    {
        if (commandId.StartsWith("destiny:learn:", StringComparison.OrdinalIgnoreCase))
        {
            var nodeId = commandId["destiny:learn:".Length..];
            var (success, msg) = _destiny.LearnNode(nodeId);
            return msg;
        }

        if (commandId.Equals("destiny:respec", StringComparison.OrdinalIgnoreCase))
        {
            var (success, msg) = _destiny.RespecAll();
            return msg;
        }

        if (commandId.Equals("destiny:evaluate", StringComparison.OrdinalIgnoreCase))
        {
            _destiny.EvaluateMilestones();
            return "Đã cập nhật toàn bộ mốc kỳ ngộ và kiểm toán lại ngân sách Điểm Thiên Mệnh.";
        }

        return null;
    }
}
