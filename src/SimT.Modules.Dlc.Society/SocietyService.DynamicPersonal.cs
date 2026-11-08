using System.Text.Json;
using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService
{
    public IReadOnlyList<DynamicWorldEventDef> GetDynamicEvents(bool activeOnly = true)
    {
        var sql = activeOnly
            ? "SELECT code, category, title_vi, narrative_vi, citadel_id, occurred_date, duration_days, impacts_json, status FROM so_dynamic_event WHERE status = 'active' ORDER BY occurred_date DESC"
            : "SELECT code, category, title_vi, narrative_vi, citadel_id, occurred_date, duration_days, impacts_json, status FROM so_dynamic_event ORDER BY occurred_date DESC";

        return _db.Query(
            sql,
            row =>
            {
                var impactsJson = row.GetStringOrNull(7);
                var impacts = !string.IsNullOrWhiteSpace(impactsJson)
                    ? JsonSerializer.Deserialize<HistoricalEventImpactDef[]>(impactsJson) ?? Array.Empty<HistoricalEventImpactDef>()
                    : Array.Empty<HistoricalEventImpactDef>();

                var dateStr = row.GetString(5);
                var date = GameDateCodec.Parse(dateStr);

                return new DynamicWorldEventDef(
                    row.GetString(0),
                    row.GetString(1),
                    row.GetString(2),
                    row.GetString(3),
                    row.GetInt64(4),
                    date,
                    row.GetInt32(6),
                    impacts,
                    row.GetString(8));
            });
    }

    public IReadOnlyList<PersonalNpcEventDef> GetPersonalEvents(int limit = 50)
    {
        return _db.Query(
            $"SELECT code, kind, npc_id, target_npc_id, citadel_id, event_date, title_vi, narrative_vi, impact_note_vi FROM so_personal_event ORDER BY id DESC LIMIT {Math.Max(1, limit)}",
            row =>
            {
                var targetId = row.IsDbNull(3) ? (long?)null : row.GetInt64(3);
                var dateStr = row.GetString(5);
                var date = GameDateCodec.Parse(dateStr);

                return new PersonalNpcEventDef(
                    row.GetString(0),
                    row.GetString(1),
                    row.GetInt64(2),
                    targetId,
                    row.GetInt64(4),
                    date,
                    row.GetString(6),
                    row.GetString(7),
                    row.GetStringOrNull(8));
            });
    }

    public IReadOnlyList<UnifiedSandboxEventView> GetUnifiedEvents(SandboxEventTier? tierFilter = null, int limit = 50)
    {
        var unified = new List<UnifiedSandboxEventView>();

        if (!tierFilter.HasValue || tierFilter.Value == SandboxEventTier.Historical)
        {
            var historical = GetHistoricalEvents(occurredOnly: true);
            foreach (var h in historical)
            {
                unified.Add(new UnifiedSandboxEventView(
                    h.Code,
                    SandboxEventTier.Historical,
                    h.OccurredDate ?? h.ScheduledDate,
                    h.IsDiverged ? (h.DivergenceTitleVi ?? h.TitleVi) : h.TitleVi,
                    h.IsDiverged ? (h.DivergenceNarrativeVi ?? h.NarrativeVi) : h.NarrativeVi,
                    h.PrimaryCitadelId,
                    h.Status,
                    h.IsDiverged,
                    h.IsDiverged ? (h.DivergenceCode ?? "diverged") : null));
            }
        }

        if (!tierFilter.HasValue || tierFilter.Value == SandboxEventTier.Dynamic)
        {
            var dynamicEvents = GetDynamicEvents(activeOnly: false);
            foreach (var d in dynamicEvents)
            {
                unified.Add(new UnifiedSandboxEventView(
                    d.Code,
                    SandboxEventTier.Dynamic,
                    d.OccurredDate,
                    d.TitleVi,
                    d.NarrativeVi,
                    d.CitadelId,
                    d.Status,
                    false,
                    null));
            }
        }

        if (!tierFilter.HasValue || tierFilter.Value == SandboxEventTier.Personal)
        {
            var personalEvents = GetPersonalEvents(limit: limit);
            foreach (var p in personalEvents)
            {
                unified.Add(new UnifiedSandboxEventView(
                    p.Code,
                    SandboxEventTier.Personal,
                    p.OccurredDate,
                    p.TitleVi,
                    p.NarrativeVi,
                    p.CitadelId,
                    "occurred",
                    false,
                    p.ImpactNoteVi));
            }
        }

        return unified
            .OrderByDescending(u => u.Date.DayIndex)
            .Take(Math.Max(1, limit))
            .ToList();
    }

    public DynamicWorldEventDef? EvaluateAndTriggerDynamicEvents(GameDate date)
    {
        if (_broker == null || !_broker.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) || court == null)
        {
            return null;
        }

        var dateStr = GameDateCodec.Format(date);
        var governances = court.Governances;

        foreach (var gov in governances)
        {
            var cit = _map.Find(gov.CitadelId);
            var citName = cit?.NameVi ?? "Châu quận";

            // 1. Bandit Uprising / Order Crisis when unrest is high or defense is broken
            if (gov.UnrestLevel >= 50 || gov.DefenseStrength < 40)
            {
                var eventCode = $"dyn_bandit_{gov.CitadelId}_{date.Year}_{date.Month}";
                var exists = _db.ScalarLong("SELECT COUNT(*) FROM so_dynamic_event WHERE code = @code", DbArgs.One("@code", eventCode));
                if (exists == 0)
                {
                    var impacts = new List<HistoricalEventImpactDef>
                    {
                        new("route_danger", gov.CitadelId, $"Sơn tặc nổi dậy cướp bóc tại {citName}", DurationDays: 30, SeverityPermille: 1500)
                    };
                    var impactsJson = JsonSerializer.Serialize(impacts);

                    _db.Execute(
                        @"INSERT INTO so_dynamic_event (code, category, title_vi, narrative_vi, citadel_id, occurred_date, duration_days, impacts_json, status)
                          VALUES (@code, 'order', @title, @narr, @cit, @date, 30, @impacts, 'active')",
                        new DbArgs()
                            .Add("@code", eventCode)
                            .Add("@title", $"Loạn Khói Lửa — Sơn Tặc Quấy Nhiễu {citName}")
                            .Add("@narr", $"Trị an tại {citName} suy sụp, lòng dân bất an (Bất an: {gov.UnrestLevel}, Phòng thủ: {gov.DefenseStrength}). Các băng đảng lục lâm thừa cơ nổi dậy cướp bóc thương lộ, trăm họ lo sợ.")
                            .Add("@cit", gov.CitadelId)
                            .Add("@date", dateStr)
                            .Add("@impacts", impactsJson));

                    LogEvent(
                        dateStr,
                        0,
                        "Bách Tính",
                        gov.CitadelId,
                        citName,
                        "governance",
                        $"[Thế Giới Biến Động] Loạn Khói Lửa tại {citName}",
                        $"Trị an suy giảm (Bất an: {gov.UnrestLevel}, Phòng thủ: {gov.DefenseStrength}) làm bùng phát nạn cướp bóc, thương lộ bị đe dọa.",
                        $"unrest:{gov.UnrestLevel} · route_danger:+50%",
                        date.DayIndex);

                    return new DynamicWorldEventDef(
                        eventCode, "order", $"Loạn Khói Lửa — Sơn Tặc Quấy Nhiễu {citName}",
                        $"Trị an tại {citName} suy sụp nghiêm trọng.", gov.CitadelId, date, 30, impacts, "active");
                }
            }

            // 2. Bumper Harvest in Autumn (Month 8 or 9) when unrest is low
            if ((date.Month == 8 || date.Month == 9) && gov.UnrestLevel <= 25)
            {
                var eventCode = $"dyn_harvest_{gov.CitadelId}_{date.Year}";
                var exists = _db.ScalarLong("SELECT COUNT(*) FROM so_dynamic_event WHERE code = @code", DbArgs.One("@code", eventCode));
                if (exists == 0)
                {
                    var impacts = new List<HistoricalEventImpactDef>
                    {
                        new("price_shock", gov.CitadelId, $"Mùa màng bội thu giúp hạ giá lương thực tại {citName}", DurationDays: 30, SeverityPermille: 700)
                    };
                    var impactsJson = JsonSerializer.Serialize(impacts);

                    _db.Execute(
                        @"INSERT INTO so_dynamic_event (code, category, title_vi, narrative_vi, citadel_id, occurred_date, duration_days, impacts_json, status)
                          VALUES (@code, 'harvest', @title, @narr, @cit, @date, 30, @impacts, 'active')",
                        new DbArgs()
                            .Add("@code", eventCode)
                            .Add("@title", $"Đại Phong Thu — Lúa Vàng Rợp Đồng {citName}")
                            .Add("@narr", $"Châu quận {citName} mưa thuận gió hòa, bách tính an cư lạc nghiệp (Bất an: {gov.UnrestLevel}). Nông phu trúng mùa lúa lớn, kho thóc đầy ắp, giá lương thảo hạ nhiệt.")
                            .Add("@cit", gov.CitadelId)
                            .Add("@date", dateStr)
                            .Add("@impacts", impactsJson));

                    LogEvent(
                        dateStr,
                        0,
                        "Hương Thân",
                        gov.CitadelId,
                        citName,
                        "governance",
                        $"[Thế Giới Biến Động] Đại Phong Thu tại {citName}",
                        $"Mưa thuận gió hòa đem lại mùa màng bội thu cho bách tính.",
                        "harvest:bumper · food_price:-30%",
                        date.DayIndex);

                    return new DynamicWorldEventDef(
                        eventCode, "harvest", $"Đại Phong Thu — Lúa Vàng Rợp Đồng {citName}",
                        $"Mùa màng bội thu tại {citName}.", gov.CitadelId, date, 30, impacts, "active");
                }
            }
        }

        return null;
    }

    public PersonalNpcEventDef? EvaluateAndTriggerPersonalEvents(GameDate date)
    {
        var dateStr = GameDateCodec.Format(date);
        var activities = AllActivities();
        var livingByCitadel = activities
            .Where(a => !a.IsDeceased)
            .GroupBy(a => a.CurrentCitadelId)
            .Where(g => g.Count() >= 2)
            .ToList();

        foreach (var group in livingByCitadel)
        {
            var citId = group.Key;
            var cit = _map.Find(citId);
            var citName = cit?.NameVi ?? "Châu quận";
            var npcs = group.Take(4).ToList();

            for (var i = 0; i < npcs.Count - 1; i++)
            {
                for (var j = i + 1; j < npcs.Count; j++)
                {
                    var npcA = _registry.Find(npcs[i].NpcId);
                    var npcB = _registry.Find(npcs[j].NpcId);
                    if (npcA == null || npcB == null || npcA.IsDeceasedFlag || npcB.IsDeceasedFlag)
                    {
                        continue;
                    }

                    // Check bond or compatibility
                    var bondScore = _db.ScalarLong(
                        "SELECT COALESCE(affinity, 0) FROM so_npc_bond WHERE (npc_a = @a AND npc_b = @b) OR (npc_a = @b AND npc_b = @a)",
                        new DbArgs().Add("@a", npcA.Id).Add("@b", npcB.Id));

                    // 1. Sworn Brotherhood or Deep Bond when high affinity
                    if (bondScore >= 50)
                    {
                        var code = $"pers_sworn_{Math.Min(npcA.Id, npcB.Id)}_{Math.Max(npcA.Id, npcB.Id)}_{date.Year}";
                        var exists = _db.ScalarLong("SELECT COUNT(*) FROM so_personal_event WHERE code = @code", DbArgs.One("@code", code));
                        if (exists == 0)
                        {
                            var title = $"Kim Lan Tương Tri — {npcA.NameVi} & {npcB.NameVi}";
                            var narr = $"Tại {citName}, {npcA.NameVi} và {npcB.NameVi} cùng uống rượu đàm đạo thâu đêm, tình cảm tương đắc gắn bó keo sơn, nguyện cùng tiến thoái.";

                            _db.Execute(
                                @"INSERT INTO so_personal_event (code, kind, npc_id, target_npc_id, citadel_id, event_date, title_vi, narrative_vi, impact_note_vi)
                                  VALUES (@code, 'sworn_brother', @a, @b, @cit, @date, @title, @narr, @impact)",
                                new DbArgs()
                                    .Add("@code", code)
                                    .Add("@a", npcA.Id)
                                    .Add("@b", npcB.Id)
                                    .Add("@cit", citId)
                                    .Add("@date", dateStr)
                                    .Add("@title", title)
                                    .Add("@narr", narr)
                                    .Add("@impact", "bond:sworn_brother · affinity:+20"));

                            LogEvent(
                                dateStr,
                                npcA.Id,
                                npcA.NameVi,
                                citId,
                                citName,
                                "relationship",
                                $"[Nhân Duyên Biến Động] {title}",
                                narr,
                                "bond:sworn_brother",
                                date.DayIndex);

                            return new PersonalNpcEventDef(code, "sworn_brother", npcA.Id, npcB.Id, citId, date, title, narr, "bond:sworn_brother");
                        }
                    }

                    // 2. Personal Vendetta / Rivalry clash when negative affinity
                    if (bondScore <= -30)
                    {
                        var code = $"pers_vendetta_{Math.Min(npcA.Id, npcB.Id)}_{Math.Max(npcA.Id, npcB.Id)}_{date.Year}";
                        var exists = _db.ScalarLong("SELECT COUNT(*) FROM so_personal_event WHERE code = @code", DbArgs.One("@code", code));
                        if (exists == 0)
                        {
                            var title = $"Đoạn Tuyệt Ân Nghĩa — {npcA.NameVi} Thề Báo Thù {npcB.NameVi}";
                            var narr = $"Mối bất hòa giữa {npcA.NameVi} và {npcB.NameVi} tại {citName} đã lên đến đỉnh điểm. Hai bên công khai đoạn tuyệt giao hảo, tuyên bố thù không đội trời chung.";

                            _db.Execute(
                                @"INSERT INTO so_personal_event (code, kind, npc_id, target_npc_id, citadel_id, event_date, title_vi, narrative_vi, impact_note_vi)
                                  VALUES (@code, 'vendetta', @a, @b, @cit, @date, @title, @narr, @impact)",
                                new DbArgs()
                                    .Add("@code", code)
                                    .Add("@a", npcA.Id)
                                    .Add("@b", npcB.Id)
                                    .Add("@cit", citId)
                                    .Add("@date", dateStr)
                                    .Add("@title", title)
                                    .Add("@narr", narr)
                                    .Add("@impact", "rivalry:vendetta · affinity:-25"));

                            LogEvent(
                                dateStr,
                                npcA.Id,
                                npcA.NameVi,
                                citId,
                                citName,
                                "relationship",
                                $"[Nhân Duyên Biến Động] {title}",
                                narr,
                                "rivalry:vendetta",
                                date.DayIndex);

                            return new PersonalNpcEventDef(code, "vendetta", npcA.Id, npcB.Id, citId, date, title, narr, "rivalry:vendetta");
                        }
                    }
                }
            }
        }

        return null;
    }
}
