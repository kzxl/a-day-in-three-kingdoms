using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class MilitaryService
{
    public NavalFleetDef GetNavalFleet(long citadelId)
    {
        EnsureNavalRow(citadelId);
        return _db.QueryOne(
            """
            SELECT citadel_id, mong_xung_count, dau_ham_count, lau_thuyen_count,
                   naval_power, patrol_victory_count, last_patrol_date
            FROM ml_naval_shipyard
            WHERE citadel_id = @citId
            """,
            r => new NavalFleetDef(
                r.GetInt64(0),
                r.GetInt32(1),
                r.GetInt32(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.GetInt32(5),
                r.IsDbNull(6) ? null : GameDateCodec.Parse(r.GetString(6))),
            DbArgs.One("@citId", citadelId))
            ?? new NavalFleetDef(citadelId, 0, 0, 0, 0, 0, null);
    }

    public string CommissionWarship(long citadelId, NavalShipKind shipKind)
    {
        EnsureNavalRow(citadelId);

        var (costGold, powerGain, column, shipNameVi) = shipKind switch
        {
            NavalShipKind.MongXung => (120L, 15, "mong_xung_count", "Mông Xung Xung Kích Hạm"),
            NavalShipKind.DauHam => (260L, 35, "dau_ham_count", "Đấu Hạm Chiến Thuyền"),
            NavalShipKind.LauThuyen => (500L, 75, "lau_thuyen_count", "Lâu Thuyền Đại Soái Hạm"),
            _ => (120L, 15, "mong_xung_count", "Mông Xung Xung Kích Hạm")
        };

        if (_wallet.Balance < costGold)
        {
            throw new SimException("military.insufficient_funds",
                $"Không đủ ngân lượng đóng {shipNameVi} (cần {costGold} Quan, hiện có {_wallet.Balance} Quan).");
        }

        var today = GameDateCodec.Format(_clock.Today);
        var citadelName = _map.Find(citadelId)?.NameVi ?? $"Thành #{citadelId}";

        _db.Transaction(() =>
        {
            _wallet.Charge(costGold, "military.shipyard", $"Đóng chiến thuyền {shipNameVi} tại {citadelName}", $"shipyard:{citadelId}:{shipKind}:{today}");
            _db.Execute(
                $"UPDATE ml_naval_shipyard SET {column} = {column} + 1, naval_power = naval_power + @p WHERE citadel_id = @citId",
                new DbArgs().Add("@p", powerGain).Add("@citId", citadelId));

            _attributes.AddExp("LDR", 15);
            _attributes.AddExp("CRA", 12);
        });

        var fleet = GetNavalFleet(citadelId);
        _log.Info($"Commissioned warship {shipKind} at citadel {citadelId}: naval_power={fleet.NavalPower}");
        return $"[Thủy Quân Đô Đốc Phủ] Đã hạ thủy 1 chiếc {shipNameVi} tại bến {citadelName} (-{costGold} Quan). Thủy lực hạm đội đạt {fleet.NavalPower} điểm!";
    }

    public string LaunchNavalCampaign(long citadelId)
    {
        var fleet = GetNavalFleet(citadelId);
        var citadelName = _map.Find(citadelId)?.NameVi ?? $"Thành #{citadelId}";

        if (fleet.TotalShips <= 0 || fleet.NavalPower < 15)
        {
            throw new SimException("military.no_naval_fleet",
                $"Bến thuyền {citadelName} chưa có đủ chiến thuyền xuất kích (cần tối thiểu 1 chiến thuyền / 15 điểm Thủy Lực).");
        }

        var pAttr = _attributes.Attributes;
        var combatScore = fleet.NavalPower + pAttr.Ldr * 2 + pAttr.War;
        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        // Calculate bounty based on fleet composition and commander prowess
        var baseBounty = 160L + Math.Min(450L, fleet.NavalPower * 2L) + (fleet.LauThuyenCount * 80L);
        _attributes.ApplyFatigue(15);

        _db.Transaction(() =>
        {
            _wallet.Pay(baseBounty, "military.naval_loot", $"Chiến lợi phẩm bình định Thủy Tặc Trường Giang tại {citadelName}", $"naval:{citadelId}:{formattedDate}:{fleet.PatrolVictoryCount + 1}");

            _db.Execute(
                """
                UPDATE ml_naval_shipyard
                SET patrol_victory_count = patrol_victory_count + 1,
                    last_patrol_date = @date
                WHERE citadel_id = @citId
                """,
                new DbArgs().Add("@date", formattedDate).Add("@citId", citadelId));

            _db.Execute(
                """
                UPDATE po_city_governance
                SET defense_strength = MIN(100, defense_strength + 5),
                    unrest_level = MAX(0, unrest_level - 4)
                WHERE citadel_id = @citId
                """,
                DbArgs.One("@citId", citadelId));

            _attributes.AddExp("LDR", 25);
            _attributes.AddExp("WAR", 20);
            _attributes.GainFame(20, $"Chỉ huy thủy quân bình định Cẩm Phàm Thủy Tặc trên bến {citadelName}.", virtueDelta: 4);
        });

        _log.Info($"Launched naval campaign at {citadelId}: combatScore={combatScore}, bounty={baseBounty}");
        return $"[Thủy Chiến Đại捷] Hạm đội ({fleet.TotalShips} chiến thuyền · Thủy lực {fleet.NavalPower}) dàn trận trên sông lớn quanh {citadelName}, phóng hỏa thiêu rụi thủy trại giặc sông! Thu chiến lợi phẩm +{baseBounty} Quan, phòng ngự thủy đạo +5, Uy Danh +20!";
    }

    private void EnsureNavalRow(long citadelId)
    {
        _db.Execute(
            "INSERT OR IGNORE INTO ml_naval_shipyard (citadel_id, mong_xung_count, dau_ham_count, lau_thuyen_count, naval_power, patrol_victory_count, last_patrol_date) VALUES (@citId, 0, 0, 0, 0, 0, NULL)",
            DbArgs.One("@citId", citadelId));
    }
}
