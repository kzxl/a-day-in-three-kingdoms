using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class CourtService
{
    public long GovernorCityId => State("governor_city_id");

    public FiefStatusView? PlayerFief
    {
        get
        {
            var cityId = GovernorCityId;
            if (cityId <= 0) return null;

            var map = _capabilities?.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var m) == true ? m : null;
            var cityName = map?.Find(cityId)?.NameVi ?? $"Thành #{cityId}";

            return _db.Query(
                """
                SELECT citadel_id, agriculture_score, commerce_score, fortification_score, order_score, local_treasury, appointed_day
                FROM po_governor_fief WHERE citadel_id = @citId AND player_governor = 1
                """,
                row => new FiefStatusView(
                    row.GetInt64(0),
                    cityName,
                    row.GetInt32(1),
                    row.GetInt32(2),
                    row.GetInt32(3),
                    row.GetInt32(4),
                    row.GetInt64(5),
                    row.GetInt32(6)),
                DbArgs.One("@citId", cityId)).FirstOrDefault();
        }
    }

    public CourtResult AppointGovernor(long citadelId)
    {
        var office = Offices.FirstOrDefault(o => o.Id == OfficeId);
        var rank = office?.Rank ?? 0;
        if (rank < 3)
        {
            return CourtResult.Failure("Cần chức vị tối thiểu Phẩm 3 (Thái Thú hoặc Đô Úy) để được phong đất cai quản châu quận.");
        }

        if (AllegianceFactionId == 0)
        {
            return CourtResult.Failure("Phải quy thuận một thế lực chư hầu để được phong đất trấn thủ.");
        }

        var gov = Governances.FirstOrDefault(g => g.CitadelId == citadelId);
        if (gov is null || gov.FactionId != AllegianceFactionId)
        {
            return CourtResult.Failure("Thành trì này không thuộc quyền kiểm soát của thế lực bạn phụng sự.");
        }

        if (GovernorCityId > 0 && GovernorCityId != citadelId)
        {
            return CourtResult.Failure("Mỗi quan viên chỉ được cai quản một thành trì. Hãy từ nhiệm trước khi nhận phong ấp mới.");
        }

        var map = _capabilities?.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var m) == true ? m : null;
        var cityName = map?.Find(citadelId)?.NameVi ?? $"Thành #{citadelId}";

        _db.Transaction(() =>
        {
            _db.Execute(
                """
                INSERT INTO po_governor_fief (citadel_id, player_governor, appointed_day, agriculture_score, commerce_score, fortification_score, order_score, local_treasury, last_eval_day)
                VALUES (@citId, 1, @day, 50, 50, 50, 50, 500, @day)
                ON CONFLICT(citadel_id) DO UPDATE SET player_governor = 1, appointed_day = @day
                """,
                new DbArgs().Add("@citId", citadelId).Add("@day", _clock.Today.DayIndex));

            SetState("governor_city_id", citadelId);
            LogCareerChange(OfficeId, OfficeId, "APPOINTMENT", $"Được minh chủ sắc phong làm Thái Thú trấn nhậm {cityName}!", 15);
            AddMerits(15);
        });

        _log.Info($"Player appointed governor of citadel {citadelId} ({cityName})");
        return CourtResult.Success($"📜 ĐẮC PHONG THÁI THÚ: Bạn chính thức tiếp nhận quyền cai quản phong ấp {cityName}! Công trạng +15.");
    }

    public CourtResult ResignGovernor()
    {
        var cityId = GovernorCityId;
        if (cityId <= 0)
        {
            return CourtResult.Failure("Hiện tại bạn không cai quản phong ấp nào.");
        }

        var map = _capabilities?.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var m) == true ? m : null;
        var cityName = map?.Find(cityId)?.NameVi ?? $"Thành #{cityId}";

        _db.Transaction(() =>
        {
            _db.Execute("UPDATE po_governor_fief SET player_governor = 0 WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
            SetState("governor_city_id", 0);
            LogCareerChange(OfficeId, OfficeId, "DISMISSAL", $"Bàn giao ấn tín, rời chức vụ Thái Thú {cityName}.", 0);
        });

        return CourtResult.Success($"Đã dâng biểu bàn giao ấn tín, rời chức vụ Thái Thú {cityName}.");
    }

    public CourtResult DevelopFief(string domainCode)
    {
        var fief = PlayerFief;
        if (fief is null)
        {
            return CourtResult.Failure("Bạn chưa cai quản phong ấp nào để thực thi quốc sách địa phương.");
        }

        var citadelId = fief.CitadelId;
        var gov = Governances.FirstOrDefault(g => g.CitadelId == citadelId);

        const int actionDays = 4;
        const long actionCost = 65;

        if (_wallet.Liquid < actionCost)
        {
            return CourtResult.Failure($"Cần {actionCost} Quan từ ngân sách cá nhân để kiến thiết châu quận (hiện có {_wallet.Liquid}).");
        }

        switch (domainCode.ToLowerInvariant())
        {
            case "agriculture":
            {
                var newAgri = Math.Min(100, fief.Agriculture + 12);
                _db.Transaction(() =>
                {
                    _wallet.Charge(actionCost, JournalCodes.CourtService, "Khuyến nông thủy lợi phong ấp");
                    _clock.Advance(actionDays);
                    _db.Execute(
                        "UPDATE po_governor_fief SET agriculture_score = @val WHERE citadel_id = @citId",
                        new DbArgs().Add("@val", newAgri).Add("@citId", citadelId));
                    AddMerits(8);
                });
                return CourtResult.Success($"🌾 Khuyến nông đắp đê hoàn tất ({actionDays} ngày): Nông Khẩn tăng lên {newAgri}/100, Công trạng +8.");
            }

            case "commerce":
            {
                var newComm = Math.Min(100, fief.Commerce + 12);
                var addTreasury = 80;
                _db.Transaction(() =>
                {
                    _wallet.Charge(actionCost, JournalCodes.CourtService, "Mở rộng chợ phiên thương cảng");
                    _clock.Advance(actionDays);
                    _db.Execute(
                        "UPDATE po_governor_fief SET commerce_score = @val, local_treasury = local_treasury + @add WHERE citadel_id = @citId",
                        new DbArgs().Add("@val", newComm).Add("@add", addTreasury).Add("@citId", citadelId));
                    AddMerits(8);
                });
                return CourtResult.Success($"🏮 Mở trạm dịch thương thuyền ({actionDays} ngày): Thương Nghiệp tăng lên {newComm}/100, Nha Môn thu +{addTreasury} Quan, Công trạng +8.");
            }

            case "defense":
            {
                var newDef = Math.Min(100, fief.Fortification + 15);
                var cityDefense = (gov?.DefenseStrength ?? 80) + 10;
                _db.Transaction(() =>
                {
                    _wallet.Charge(actionCost, JournalCodes.CourtService, "Tu bổ thành quách hào lũy");
                    _clock.Advance(actionDays);
                    _db.Execute(
                        "UPDATE po_governor_fief SET fortification_score = @val WHERE citadel_id = @citId",
                        new DbArgs().Add("@val", newDef).Add("@citId", citadelId));
                    _db.Execute(
                        "UPDATE po_city_governance SET defense_strength = @def WHERE citadel_id = @citId",
                        new DbArgs().Add("@def", cityDefense).Add("@citId", citadelId));
                    AddMerits(8);
                });
                return CourtResult.Success($"🛡️ Gia cố quan ải thành lũy ({actionDays} ngày): Thành Phòng tăng lên {newDef}/100 (Phòng thủ đô thành: {cityDefense}), Công trạng +8.");
            }

            case "order":
            {
                var newOrder = Math.Min(100, fief.PublicOrder + 12);
                var newUnrest = Math.Max(0, (gov?.UnrestLevel ?? 20) - 8);
                _db.Transaction(() =>
                {
                    _wallet.Charge(actionCost, JournalCodes.CourtService, "Tuần phòng an dân giáo hóa");
                    _clock.Advance(actionDays);
                    _db.Execute(
                        "UPDATE po_governor_fief SET order_score = @val WHERE citadel_id = @citId",
                        new DbArgs().Add("@val", newOrder).Add("@citId", citadelId));
                    _db.Execute(
                        "UPDATE po_city_governance SET unrest_level = @unrest WHERE citadel_id = @citId",
                        new DbArgs().Add("@unrest", newUnrest).Add("@citId", citadelId));
                    AddMerits(8);
                });
                return CourtResult.Success($"⚖️ Tăng cường nha dịch tuần sát ({actionDays} ngày): Trị An tăng lên {newOrder}/100 (Bất ổn giảm còn {newUnrest}%), Công trạng +8.");
            }

            default:
                return CourtResult.Failure($"Lĩnh vực kiến thiết '{domainCode}' không hợp lệ.");
        }
    }

    public QuarterlyEvalResult? EvaluateQuarterlyGovernance()
    {
        var fief = PlayerFief;
        if (fief is null) return null;

        var map = _capabilities?.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var m) == true ? m : null;
        var cityName = map?.Find(fief.CitadelId)?.NameVi ?? $"Thành #{fief.CitadelId}";

        var avgScore = (fief.Agriculture + fief.Commerce + fief.Fortification + fief.PublicOrder) / 4;

        string grade;
        int meritDelta;
        long bonus;
        string rankAction;
        string narrative;

        if (avgScore >= 80)
        {
            grade = "Giáp (Xuất sắc)";
            meritDelta = 40;
            bonus = 300;
            rankAction = "THĂNG QUAN / KHEN THƯỞNG";
            narrative = "Thành trì trù phú, nhân dân an cư lạc nghiệp, kho lương đầy ắp. Minh chủ hạ hịch biểu dương công đức!";
        }
        else if (avgScore >= 65)
        {
            grade = "Ất (Khá)";
            meritDelta = 20;
            bonus = 150;
            rankAction = "BAN THƯỞNG";
            narrative = "Trấn thủ nghiêm cẩn, thương nông ổn định, bá quan đều ngợi khen.";
        }
        else if (avgScore >= 50)
        {
            grade = "Bính (Đạt)";
            meritDelta = 5;
            bonus = 50;
            rankAction = "GIỮ NGUYÊN";
            narrative = "Chánh vụ duy trì ở mức bình thường, cần tích cực khuyến nông phòng thủ hơn nữa.";
        }
        else
        {
            grade = "Đinh (Yếu kém)";
            meritDelta = -30;
            bonus = 0;
            rankAction = "KHIỂN TRÁCH / GIÁNG CẤP";
            narrative = "Dân chúng oán thán, trị an lỏng lẻo, ngân khố hao hụt. Chúa công hạ chiếu nghiêm khắc khiển trách!";
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE po_governor_fief SET last_eval_day = @day WHERE citadel_id = @citId",
                new DbArgs().Add("@day", _clock.Today.DayIndex).Add("@citId", fief.CitadelId));

            AddMerits(meritDelta);
            if (bonus > 0)
            {
                _wallet.Pay(bonus, JournalCodes.CourtStipend, $"Thưởng khảo hạch chánh vụ {grade}");
            }

            if (avgScore < 45)
            {
                DemoteOffice($"Khảo hạch chánh vụ Thái Thú {cityName} xếp hạng Đinh (Yếu kém).");
            }
        });

        return new QuarterlyEvalResult(fief.CitadelId, cityName, grade, meritDelta, bonus, rankAction, narrative);
    }
}
