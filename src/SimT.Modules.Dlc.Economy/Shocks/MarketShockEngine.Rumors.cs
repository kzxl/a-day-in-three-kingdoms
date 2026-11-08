using SimT.Contracts.Data;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Economy.Shocks;

public sealed partial class MarketShockEngine
{
    public IReadOnlyList<RumorEventHint> Upcoming(long day, int horizonDays) => _db.Query(
        """
        SELECT id,citadel_id,start_day,note_vi FROM mk_city_shock
        WHERE end_day>=@day AND start_day<=@until ORDER BY start_day,id
        """, r => new RumorEventHint($"shock:{r.GetInt64(0)}", r.GetInt64(1), r.GetInt64(2),
            "Chợ sắp biến động", r.GetString(3)),
        new DbArgs().Add("@day", day).Add("@until", day + horizonDays));

    public bool HasOccurred(string eventKey, long day) => eventKey.StartsWith("shock:", StringComparison.Ordinal)
        && long.TryParse(eventKey[6..], out var id)
        && _db.ScalarLong("SELECT COUNT(*) FROM mk_city_shock WHERE id=@id AND start_day<=@day",
            new DbArgs().Add("@id", id).Add("@day", day)) > 0;
}
