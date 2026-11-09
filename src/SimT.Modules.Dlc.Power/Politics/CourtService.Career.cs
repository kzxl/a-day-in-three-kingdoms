using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Power;

internal sealed partial class CourtService
{
    public int Merits => (int)State("merits");

    internal void AddMerits(int delta)
    {
        var newMerits = Math.Max(0, Merits + delta);
        SetState("merits", newMerits);
    }

    public IReadOnlyList<CareerLogView> RecentCareerLogs(int limit = 10) =>
        _db.Query(
            """
            SELECT id, day_index, old_office_id, new_office_id, change_type, reason_vi, merits_delta
            FROM po_career_log ORDER BY id DESC LIMIT @lim
            """,
            row =>
            {
                var oldId = row.GetInt64(2);
                var newId = row.GetInt64(3);
                var oldName = Offices.FirstOrDefault(o => o.Id == oldId)?.NameVi ?? (oldId == 0 ? "Bạch đinh" : "Khác");
                var newName = Offices.FirstOrDefault(o => o.Id == newId)?.NameVi ?? (newId == 0 ? "Bạch đinh" : "Khác");
                return new CareerLogView(
                    row.GetInt64(0),
                    row.GetInt64(1),
                    oldId,
                    oldName,
                    newId,
                    newName,
                    row.GetString(4),
                    row.GetString(5),
                    row.GetInt32(6));
            },
            DbArgs.One("@lim", limit));

    public IReadOnlyList<CourtMemorialView> RecentMemorials(int limit = 10) =>
        _db.Query(
            """
            SELECT id, day_index, category, title_vi, proposal_vi, outcome, merit_awarded, impact_narrative_vi
            FROM po_court_memorial ORDER BY id DESC LIMIT @lim
            """,
            row => new CourtMemorialView(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetString(2),
                row.GetString(3),
                row.GetString(4),
                row.GetString(5),
                row.GetInt32(6),
                row.GetString(7)),
            DbArgs.One("@lim", limit));

    private void LogCareerChange(long oldOfficeId, long newOfficeId, string changeType, string reasonVi, int meritsDelta)
    {
        _db.Execute(
            """
            INSERT INTO po_career_log (day_index, old_office_id, new_office_id, change_type, reason_vi, merits_delta)
            VALUES (@day, @old, @new, @type, @reason, @delta)
            """,
            new DbArgs()
                .Add("@day", _clock.Today.DayIndex)
                .Add("@old", oldOfficeId)
                .Add("@new", newOfficeId)
                .Add("@type", changeType)
                .Add("@reason", reasonVi)
                .Add("@delta", meritsDelta));
    }

    public CourtResult RequestPromotion()
    {
        var currentOffice = Offices.FirstOrDefault(o => o.Id == OfficeId);
        var currentRank = currentOffice?.Rank ?? 0;

        var nextOffice = Offices
            .Where(o => o.Rank > currentRank)
            .OrderBy(o => o.Rank)
            .FirstOrDefault();

        if (nextOffice is null)
        {
            return CourtResult.Failure("Đã đạt tột đỉnh quan lộ, không thể thăng hàm thêm.");
        }

        var requiredMerits = nextOffice.Rank * 25;
        if (Merits < requiredMerits)
        {
            return CourtResult.Failure(
                $"Cần tối thiểu {requiredMerits} Công Trạng để được tiến cử thăng {nextOffice.NameVi} (hiện có {Merits}).");
        }

        if (Reputation < nextOffice.ReputationRequired)
        {
            return CourtResult.Failure(
                $"Cần tối thiểu {nextOffice.ReputationRequired} Uy Tín (hiện có {Reputation}).");
        }

        if (nextOffice.Rank >= 4 && AllegianceFactionId == 0)
        {
            return CourtResult.Failure(
                $"Từ phẩm 4 trở lên ({nextOffice.NameVi}) là đại thần phiên trấn, bắt buộc phải quy thuận một minh chủ chư hầu!");
        }

        if (_attributes is not null)
        {
            var attrs = _attributes.Attributes;
            if (nextOffice.Rank >= 6 && attrs.War < 55 && attrs.Ldr < 50)
            {
                return CourtResult.Failure($"Yêu cầu năng lực: Võ Lực >= 55 hoặc Thống Soái >= 50.");
            }
            if (nextOffice.Rank >= 7 && (attrs.Pol < 55 || attrs.Ldr < 45))
            {
                return CourtResult.Failure($"Yêu cầu năng lực: Trị Quốc >= 55 và Thống Soái >= 45.");
            }
            if (nextOffice.Rank >= 8 && attrs.Pol < 65)
            {
                return CourtResult.Failure($"Yêu cầu năng lực: Trị Quốc >= 65.");
            }
            if (nextOffice.Rank >= 9 && (attrs.Pol < 75 || attrs.Int < 70))
            {
                return CourtResult.Failure($"Yêu cầu năng lực: Trị Quốc >= 75 và Mưu Lược >= 70.");
            }
        }

        var oldId = OfficeId;
        _db.Transaction(() =>
        {
            SetState("office_id", nextOffice.Id);
            AddMerits(10);
            LogCareerChange(oldId, nextOffice.Id, "PROMOTION", $"Công trạng viên mãn ({Merits} điểm), minh chủ hạ chiếu sắc phong {nextOffice.NameVi}!", 10);
            _wallet.Pay(150 + nextOffice.Rank * 50, JournalCodes.CourtStipend, $"Ân tứ thăng quan {nextOffice.NameVi}");
        });

        _log.Info($"Player promoted to {nextOffice.Code} rank {nextOffice.Rank}");
        return CourtResult.Success(
            $"🎉 CHÚA CÔNG HẠ CHIẾU THĂNG QUAN: Đắc phong chức vị {nextOffice.NameVi} (Phẩm {nextOffice.Rank})! Bổng lộc tăng lên {nextOffice.MonthlyStipend} Quan/tháng, ân thưởng {150 + nextOffice.Rank * 50} Quan.");
    }

    public CourtResult DemoteOffice(string reasonVi)
    {
        if (OfficeId == 0)
        {
            return CourtResult.Failure("Hiện tại chưa có chức vị để giáng phạt.");
        }

        var currentOffice = Offices.FirstOrDefault(o => o.Id == OfficeId);
        if (currentOffice is null)
        {
            return CourtResult.Failure("Chức vị không xác định.");
        }

        var oldId = OfficeId;
        var prevOffice = Offices
            .Where(o => o.Rank < currentOffice.Rank)
            .OrderByDescending(o => o.Rank)
            .FirstOrDefault();

        long newOfficeId = prevOffice?.Id ?? 0;
        var newTitle = prevOffice?.NameVi ?? "Bạch đinh";

        _db.Transaction(() =>
        {
            SetState("office_id", newOfficeId);
            AddMerits(-25);

            // If demoted below rank 4 and currently governor, strip governorship
            if ((prevOffice?.Rank ?? 0) < 4 && GovernorCityId > 0)
            {
                _db.Execute(
                    "UPDATE po_governor_fief SET player_governor = 0 WHERE citadel_id = @citId",
                    DbArgs.One("@citId", GovernorCityId));
                SetState("governor_city_id", 0);
            }

            LogCareerChange(oldId, newOfficeId, "DEMOTION", reasonVi, -25);
        });

        _log.Info($"Player demoted from {currentOffice.Code} to {newTitle}: {reasonVi}");
        return CourtResult.Success(
            $"⚠️ CHÚA CÔNG HẠ CHIẾU KHIỂN TRÁCH: Bị giáng chức từ {currentOffice.NameVi} xuống {newTitle}! Lý do: {reasonVi}");
    }

    public CourtResult SubmitMemorial(string category, string proposalVi)
    {
        if (AllegianceFactionId == 0)
        {
            return CourtResult.Failure("Phải quy thuận một thế lực chư hầu mới có thể dâng sớ Thượng Tấu tại Triều Hội.");
        }

        var currentOffice = Offices.FirstOrDefault(o => o.Id == OfficeId);
        if ((currentOffice?.Rank ?? 0) < 3)
        {
            return CourtResult.Failure("Cần chức vị tối thiểu Phẩm 3 (Thái Thú hoặc Đô Úy) để có tư cách nghị sự dâng sớ trước minh chủ.");
        }

        var faction = Factions.FirstOrDefault(f => f.Id == AllegianceFactionId);
        var factionName = faction?.NameVi ?? "Minh chủ";

        int pol = _attributes?.Attributes.Pol ?? 50;
        int intel = _attributes?.Attributes.Int ?? 50;
        int successThreshold = 55;
        int rollScore = (pol + intel) / 2 + (Reputation / 3);

        bool accepted = rollScore >= successThreshold;
        string outcome = accepted ? "ACCEPTED" : "REJECTED";
        int meritReward = accepted ? 25 : 5;

        string titleVi = category.ToLowerInvariant() switch
        {
            "reform" => "Thượng Tấu Canh Tân Khuyến Nông",
            "military" => "Thượng Tấu Binh Cơ Trừ Phỉ",
            "personnel" => "Thượng Tấu Cử Hiền Đãi Sĩ",
            "relief" => "Thượng Tấu Mở Kho Chẩn Bần",
            _ => "Thượng Tấu Trị Quốc An Dân"
        };

        string impactNarrative = accepted
            ? $"Chúa công khen ngợi lời can gián xác đáng, bá quan đều phục, ban thưởng ân điển!"
            : $"Triều thần cho rằng kế sách chưa chín muồi, chúa công lưu sớ chờ dịp khác.";

        _db.Transaction(() =>
        {
            AddMerits(meritReward);
            _db.Execute(
                """
                INSERT INTO po_court_memorial (faction_id, day_index, category, title_vi, proposal_vi, outcome, merit_awarded, impact_narrative_vi)
                VALUES (@facId, @day, @cat, @title, @prop, @outcome, @merit, @impact)
                """,
                new DbArgs()
                    .Add("@facId", AllegianceFactionId)
                    .Add("@day", _clock.Today.DayIndex)
                    .Add("@cat", category)
                    .Add("@title", titleVi)
                    .Add("@prop", proposalVi)
                    .Add("@outcome", outcome)
                    .Add("@merit", meritReward)
                    .Add("@impact", impactNarrative));

            if (accepted)
            {
                SetState("reputation", Reputation + 4);
                _wallet.Pay(120, JournalCodes.CourtStipend, "Ân thưởng sớ thượng tấu được chuẩn y");
            }
        });

        return accepted
            ? CourtResult.Success($"✅ Thượng tấu '{titleVi}' được minh chủ chuẩn y! Công trạng +{meritReward}, Uy tín +4, thưởng 120 Quan.")
            : CourtResult.Failure($"Lời sớ chưa thuyết phục được quần thần. Công trạng an ủi +{meritReward}.");
    }
}
