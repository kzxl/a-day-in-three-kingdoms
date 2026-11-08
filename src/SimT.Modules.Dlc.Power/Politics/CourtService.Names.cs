using SimT.Contracts.Data;
using SimT.Contracts.Playing;
namespace SimT.Modules.Dlc.Power;

internal sealed partial class CourtService
{
    private IReadOnlyList<FactionView> ReadFactionsForYear() =>
        _db.Query(
            """
            SELECT f.id, f.code,
              COALESCE(n.name_vi, f.name_vi), COALESCE(n.leader_npc_id, f.leader_npc_id),
              f.capital_city_id, f.ideal_vi, f.troop_count, f.treasury_copper, f.color_hex
            FROM po_faction f LEFT JOIN po_faction_name n ON n.faction_id=f.id AND n.from_year=
              (SELECT MAX(from_year) FROM po_faction_name WHERE faction_id=f.id AND from_year<=@year)
            ORDER BY f.id ASC
            """,
            row => new FactionView(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetString(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetString(8)), DbArgs.One("@year", _clock.Today.Year));

}
