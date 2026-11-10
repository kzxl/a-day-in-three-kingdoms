using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

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
            var npcs = _capabilities?.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var n) == true ? n : null;

            var demo = GetCityDemographics(cityId);
            var pop = demo?.Population ?? 50000;

            var armory = _db.Query(
                "SELECT weapons_stock, weapons_capacity FROM ml_citadel_armory WHERE citadel_id = @citId",
                r => (Stock: r.GetInt32(0), Cap: r.GetInt32(1)),
                DbArgs.One("@citId", cityId)).FirstOrDefault();

            var gov = Governances.FirstOrDefault(g => g.CitadelId == cityId);
            var garrison = (gov?.DefenseStrength ?? 80) * 15;

            return _db.Query(
                """
                SELECT citadel_id, agriculture_score, commerce_score, fortification_score, order_score, local_treasury, appointed_day,
                       COALESCE(civil_aide_npc_id, 0), COALESCE(military_aide_npc_id, 0), COALESCE(policy_directive, 'BALANCE'),
                       COALESCE(last_monthly_digest_text, '')
                FROM po_governor_fief WHERE citadel_id = @citId AND player_governor = 1
                """,
                row =>
                {
                    var civilId = row.GetInt64(7);
                    var militaryId = row.GetInt64(8);
                    var civilName = civilId > 0 && npcs is not null ? npcs.Find(civilId)?.NameVi ?? $"Quan viên #{civilId}" : "Chưa bổ nhiệm";
                    var militaryName = militaryId > 0 && npcs is not null ? npcs.Find(militaryId)?.NameVi ?? $"Võ tướng #{militaryId}" : "Chưa bổ nhiệm";

                    return new FiefStatusView(
                        row.GetInt64(0),
                        cityName,
                        row.GetInt32(1),
                        row.GetInt32(2),
                        row.GetInt32(3),
                        row.GetInt32(4),
                        row.GetInt64(5),
                        row.GetInt32(6),
                        pop,
                        garrison,
                        armory.Stock > 0 ? armory.Stock : 50,
                        civilId,
                        civilName,
                        militaryId,
                        militaryName,
                        row.GetString(9),
                        row.GetString(10));
                },
                DbArgs.One("@citId", cityId)).FirstOrDefault();
        }
    }

    public CourtResult AppointFiefAide(long npcId, string aideRole)
    {
        var cityId = GovernorCityId;
        if (cityId <= 0)
        {
            return CourtResult.Failure("Bạn chưa cai quản phong ấp nào để bổ nhiệm phụ tá.");
        }

        var role = aideRole.ToUpperInvariant();
        if (role != "CIVIL" && role != "MILITARY")
        {
            return CourtResult.Failure("Chức vụ phụ tá không hợp lệ (phải là Trưởng Thừa hoặc Đô Úy).");
        }

        if (npcId <= 0)
        {
            _db.Transaction(() =>
            {
                if (role == "CIVIL")
                {
                    _db.Execute(
                        "UPDATE po_governor_fief SET civil_aide_npc_id = 0 WHERE citadel_id = @citId",
                        DbArgs.One("@citId", cityId));
                }
                else
                {
                    _db.Execute(
                        "UPDATE po_governor_fief SET military_aide_npc_id = 0 WHERE citadel_id = @citId",
                        DbArgs.One("@citId", cityId));
                }
            });
            var dismissedRole = role == "CIVIL" ? "Trưởng Thừa" : "Đô Úy";
            return CourtResult.Success($"Đã bãi miễn chức vụ {dismissedRole}, trả quyền quản lý về nha môn bản địa.");
        }

        var npcs = _capabilities?.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var n) == true ? n : null;
        var npc = npcs?.Find(npcId);
        var npcName = npc?.NameVi ?? $"Nhân vật #{npcId}";

        _db.Transaction(() =>
        {
            if (role == "CIVIL")
            {
                _db.Execute(
                    "UPDATE po_governor_fief SET civil_aide_npc_id = @npcId WHERE citadel_id = @citId",
                    new DbArgs().Add("@npcId", npcId).Add("@citId", cityId));
            }
            else
            {
                _db.Execute(
                    "UPDATE po_governor_fief SET military_aide_npc_id = @npcId WHERE citadel_id = @citId",
                    new DbArgs().Add("@npcId", npcId).Add("@citId", cityId));
            }
        });

        var roleTitle = role == "CIVIL" ? "Trưởng Thừa (Quản Lý Dân Nông & Kho Thóc)" : "Đô Úy (Thống Soái Binh Bị & Tiễu Phỉ)";
        return CourtResult.Success($"📜 Bổ nhiệm thành công: {npcName} tiếp nhận trọng trách {roleTitle}!");
    }

    public CourtResult SetFiefDirective(string directiveCode)
    {
        var cityId = GovernorCityId;
        if (cityId <= 0)
        {
            return CourtResult.Failure("Bạn chưa cai quản phong ấp nào.");
        }

        var code = directiveCode.ToUpperInvariant() switch
        {
            "AGRI" => "AGRI",
            "COMM" => "COMM",
            "MILITARY" => "MILITARY",
            _ => "BALANCE"
        };

        _db.Execute(
            "UPDATE po_governor_fief SET policy_directive = @dir WHERE citadel_id = @citId",
            new DbArgs().Add("@dir", code).Add("@citId", cityId));

        var nameVi = code switch
        {
            "AGRI" => "🌾 Trọng Nông Tích Cốc",
            "COMM" => "🏮 Trọng Thương Thu Thuế",
            "MILITARY" => "🛡️ Quân Bị Tiễu Phỉ",
            _ => "⚖️ Cân Bằng Toàn Diện"
        };

        return CourtResult.Success($"Phương châm chỉ đạo phong ấp chuyển sang: {nameVi}. Phụ tá sẽ tự động thi hành hàng tháng.");
    }

    public GovernanceMonthlyDigestView? LatestMonthlyDigest =>
        RecentMonthlyDigests(1).FirstOrDefault();

    public IReadOnlyList<GovernanceMonthlyDigestView> RecentMonthlyDigests(int limit = 6)
    {
        var map = _capabilities?.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var m) == true ? m : null;

        return _db.Query(
            """
            SELECT id, citadel_id, year, month, directive, population_delta, treasury_delta, food_consumed, garrison_delta, merits_earned, civil_action_vi, military_action_vi, summary_vi
            FROM po_governance_digest ORDER BY id DESC LIMIT @lim
            """,
            row =>
            {
                var citId = row.GetInt64(1);
                var cityName = map?.Find(citId)?.NameVi ?? $"Thành #{citId}";
                return new GovernanceMonthlyDigestView(
                    row.GetInt64(0),
                    citId,
                    cityName,
                    row.GetInt32(2),
                    row.GetInt32(3),
                    row.GetString(4),
                    row.GetInt32(5),
                    row.GetInt32(6),
                    row.GetInt32(7),
                    row.GetInt32(8),
                    row.GetInt32(9),
                    row.GetString(10),
                    row.GetString(11),
                    row.GetString(12));
            },
            DbArgs.One("@lim", limit));
    }

    public void SimulateMonthlyFiefAutonomy(GameDate date)
    {
        var cityId = GovernorCityId;
        if (cityId <= 0) return;

        var fief = PlayerFief;
        if (fief is null) return;

        var map = _capabilities?.TryGet<IWorldMap>(CapabilityIds.WorldMap, out var m) == true ? m : null;
        var cityName = map?.Find(cityId)?.NameVi ?? $"Thành #{cityId}";

        int popDelta = 0;
        int treasuryDelta = 0;
        int foodConsumed = Math.Max(20, fief.Population / 2500);
        int garrisonDelta = 0;
        int meritsEarned = 10;
        string civTitle = fief.CivilAideNpcId > 0 ? $"Trưởng Thừa {fief.CivilAideNameVi}" : "Nha Lại Dân Phụ";
        string milTitle = fief.MilitaryAideNpcId > 0 ? $"Đô Úy {fief.MilitaryAideNameVi}" : "Hiệu Úy Đồn Trú";
        string civilAction = "";
        string militaryAction = "";
        string summary = "";

        switch (fief.PolicyDirective)
        {
            case "AGRI":
            {
                popDelta = (int)(fief.Population * 0.02);
                treasuryDelta = -40;
                var newAgri = Math.Min(100, fief.Agriculture + 6);
                _db.Execute("UPDATE po_governor_fief SET agriculture_score = @agri, local_treasury = local_treasury + @t WHERE citadel_id = @citId",
                    new DbArgs().Add("@agri", newAgri).Add("@t", treasuryDelta).Add("@citId", cityId));
                civilAction = $"{civTitle} mở rộng kênh thủy lợi, Nông Khẩn tăng lên {newAgri}/100.";
                militaryAction = $"{milTitle} duy trì đội tuần phòng bờ cõi.";
                summary = $"[Tháng {date.Month}] Trọng Nông: Dân số +{popDelta}, Nông nghiệp +6, Kho lương thặng dư dồi dào.";
                break;
            }

            case "COMM":
            {
                popDelta = (int)(fief.Population * 0.01);
                treasuryDelta = 120;
                var newComm = Math.Min(100, fief.Commerce + 6);
                _db.Execute("UPDATE po_governor_fief SET commerce_score = @comm, local_treasury = local_treasury + @t WHERE citadel_id = @citId",
                    new DbArgs().Add("@comm", newComm).Add("@t", treasuryDelta).Add("@citId", cityId));
                civilAction = $"{civTitle} mở rộng trạm dịch buôn bán, thuế chợ nộp về +{treasuryDelta} Quan.";
                militaryAction = $"{milTitle} hộ tống các đoàn lữ hành vào thành an toàn.";
                summary = $"[Tháng {date.Month}] Trọng Thương: Ngân khố nha môn +{treasuryDelta} Quan, Thương nghiệp +6, phố phường tấp nập.";
                break;
            }

            case "MILITARY":
            {
                garrisonDelta = 250;
                treasuryDelta = -60;
                var newDef = Math.Min(100, fief.Fortification + 6);
                var newOrder = Math.Min(100, fief.PublicOrder + 5);
                _db.Execute(
                    "UPDATE po_governor_fief SET fortification_score = @def, order_score = @ord, local_treasury = local_treasury + @t WHERE citadel_id = @citId",
                    new DbArgs().Add("@def", newDef).Add("@ord", newOrder).Add("@t", treasuryDelta).Add("@citId", cityId));
                _db.Execute(
                    "UPDATE po_city_governance SET defense_strength = defense_strength + 5, unrest_level = MAX(0, unrest_level - 4) WHERE citadel_id = @citId",
                    DbArgs.One("@citId", cityId));
                civilAction = $"{civTitle} xuất kho chuẩn bị quân lương cho thao luyện.";
                militaryAction = $"{milTitle} tuyển mộ và thao luyện thêm {garrisonDelta} dũng sĩ, trị an vững vàng.";
                summary = $"[Tháng {date.Month}] Quân Bị: Quân đồn trú +{garrisonDelta}, Thành phòng +6, Trị an được củng cố tuyệt đối.";
                break;
            }

            default: // BALANCE
            {
                popDelta = (int)(fief.Population * 0.01);
                treasuryDelta = 35;
                var newAgri = Math.Min(100, fief.Agriculture + 3);
                var newComm = Math.Min(100, fief.Commerce + 3);
                var newDef = Math.Min(100, fief.Fortification + 3);
                var newOrd = Math.Min(100, fief.PublicOrder + 3);
                _db.Execute(
                    "UPDATE po_governor_fief SET agriculture_score = @a, commerce_score = @c, fortification_score = @d, order_score = @o, local_treasury = local_treasury + @t WHERE citadel_id = @citId",
                    new DbArgs().Add("@a", newAgri).Add("@c", newComm).Add("@d", newDef).Add("@o", newOrd).Add("@t", treasuryDelta).Add("@citId", cityId));
                civilAction = $"{civTitle} điều hòa nông thương, thu chi cân đối.";
                militaryAction = $"{milTitle} canh phòng cẩn mật các cổng thành.";
                summary = $"[Tháng {date.Month}] Cân Bằng: Nông-Thương-Thủ-Trị đều tăng đều đặn, ngân khố +{treasuryDelta} Quan.";
                break;
            }
        }

        // --- Aide Traits Impact & Emergent Governance Dynamics ---
        var traitsCap = _capabilities?.TryGet<INpcTraits>(CapabilityIds.NpcTraits, out var tc) == true ? tc : null;
        var civilTraits = fief.CivilAideNpcId > 0 && traitsCap is not null ? traitsCap.GetTraits(fief.CivilAideNpcId) : (IReadOnlyList<NpcTraitDef>)Array.Empty<NpcTraitDef>();
        var militaryTraits = fief.MilitaryAideNpcId > 0 && traitsCap is not null ? traitsCap.GetTraits(fief.MilitaryAideNpcId) : (IReadOnlyList<NpcTraitDef>)Array.Empty<NpcTraitDef>();

        // 1. Civil Aide Traits
        if (civilTraits.Any(t => t.Code == NpcTraitCodes.ThamTai || t.Code == NpcTraitCodes.XaoQuyet))
        {
            var embezzled = Math.Min(35, Math.Max(12, fief.LocalTreasury > 0 ? (int)(fief.LocalTreasury * 0.05) : 12));
            treasuryDelta -= embezzled;
            meritsEarned = Math.Max(2, meritsEarned - 3);
            civilAction += $" ⚠️ {civTitle} tính khí tham tài bòn rút {embezzled} Quan ngân khố, dân tâm xì xào phẫn nộ!";
            _db.Execute("UPDATE po_city_governance SET unrest_level = MIN(100, unrest_level + 2) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
        }
        else if (civilTraits.Any(t => t.Code == NpcTraitCodes.ThanhLiem))
        {
            treasuryDelta += 30;
            meritsEarned += 4;
            civilAction += $" ✨ {civTitle} giữ mình thanh liêm, kiểm toán nha môn nghiêm cẩn, tiết kiệm thêm +30 Quan!";
            _db.Execute("UPDATE po_city_governance SET unrest_level = MAX(0, unrest_level - 3) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
        }

        if (civilTraits.Any(t => t.Code == NpcTraitCodes.KeoKiet))
        {
            foodConsumed = (int)(foodConsumed * 0.75);
            civilAction += " (Keo kiệt thắt chặt định mức lương: Tiết kiệm lương thực)";
        }
        else if (civilTraits.Any(t => t.Code == NpcTraitCodes.HieuHoc))
        {
            meritsEarned += 3;
            civilAction += " (Hiếu học thi thư: +3 Huân công)";
        }

        // 2. Military Aide Traits
        if (militaryTraits.Any(t => t.Code == NpcTraitCodes.TrungNghia))
        {
            garrisonDelta += 40;
            meritsEarned += 3;
            militaryAction += $" 🛡️ {milTitle} một lòng trung nghĩa, tận tâm thao luyện thu nạp thêm +40 dũng sĩ!";
        }
        else if (militaryTraits.Any(t => t.Code == NpcTraitCodes.NongNay))
        {
            militaryAction += $" ⚠️ {milTitle} nóng nảy áp đặt quân lệnh hà khắc, binh sĩ oán thán (Loạn +1%).";
            _db.Execute("UPDATE po_city_governance SET unrest_level = MIN(100, unrest_level + 1) WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
        }
        else if (militaryTraits.Any(t => t.Code == NpcTraitCodes.DiemTinh))
        {
            militaryAction += $" 🏯 {milTitle} điềm tĩnh phòng thủ, vững thế thành môn (Thủ +3).";
            _db.Execute("UPDATE po_city_governance SET defense_strength = defense_strength + 3 WHERE citadel_id = @citId", DbArgs.One("@citId", cityId));
        }

        // 3. Megaproject Monthly Bonuses
        var projects = GetFiefMegaprojects(cityId);
        if (projects.Any(p => p.ProjectCode == "CANAL" && p.IsCompleted))
        {
            popDelta += (int)(fief.Population * 0.01);
            summary += " [Đại Kênh Thủy Lợi: +1% Dân]";
        }
        if (projects.Any(p => p.ProjectCode == "ACADEMY" && p.IsCompleted))
        {
            meritsEarned += 5;
            summary += " [Thái Học Viện: +5 Huân Công]";
        }

        _db.Transaction(() =>
        {
            if (popDelta != 0)
            {
                _db.Execute(
                    "UPDATE po_city_demographics SET population = population + @p WHERE citadel_id = @citId",
                    new DbArgs().Add("@p", popDelta).Add("@citId", cityId));
            }

            AddMerits(meritsEarned);

            _db.Execute(
                """
                INSERT INTO po_governance_digest (citadel_id, year, month, directive, population_delta, treasury_delta, food_consumed, garrison_delta, merits_earned, civil_action_vi, military_action_vi, summary_vi)
                VALUES (@citId, @y, @m, @dir, @pDelta, @tDelta, @fCons, @gDelta, @merits, @civAct, @milAct, @summ)
                """,
                new DbArgs()
                    .Add("@citId", cityId)
                    .Add("@y", date.Year)
                    .Add("@m", date.Month)
                    .Add("@dir", fief.PolicyDirective)
                    .Add("@pDelta", popDelta)
                    .Add("@tDelta", treasuryDelta)
                    .Add("@fCons", foodConsumed)
                    .Add("@gDelta", garrisonDelta)
                    .Add("@merits", meritsEarned)
                    .Add("@civAct", civilAction)
                    .Add("@milAct", militaryAction)
                    .Add("@summ", summary));

            _db.Execute(
                "UPDATE po_governor_fief SET last_monthly_digest_text = @summ WHERE citadel_id = @citId",
                new DbArgs().Add("@summ", summary).Add("@citId", cityId));
        });

        _log.Info($"[Monthly Governance] Fief {cityName} simulated: {summary}");
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
