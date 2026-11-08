using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class SocialService
{
    private static readonly (string Key, int Threshold, int Fame, string EventKind, string HeadlineVi, string DetailVi)[] RosterMilestones =
    [
        ("met_10", 10, 20, "relationship",
            "[Võ Tướng Lục] Sơ thức quần hùng — Đã diện kiến 10 danh sĩ thiên hạ",
            "Hành trình bôn tẩu tứ phương đã diện kiến 10 bậc anh hào danh sĩ trong thiên hạ, uy danh vang xa."),
        ("met_50", 50, 50, "relationship",
            "[Võ Tướng Lục] Giao du rộng khắp — Đã diện kiến 50 danh sĩ thiên hạ",
            "Kết giao rộng khắp mười lăm châu quận, diện kiến 50 danh sĩ hào kiệt, tiếng tăm truyền khắp chư hầu."),
        ("met_100", 100, 100, "relationship",
            "[Võ Tướng Lục] Thiên hạ tri kỷ — Đã diện kiến 100 danh sĩ thiên hạ",
            "Trăm bậc kỳ tài danh tướng trong thiên hạ đều từng hội ngộ, danh chấn Hoa Hạ.")
    ];

    public bool HasMet(long npcId)
    {
        SyncLegacyMetNpcs();
        return _db.ScalarLong(
            "SELECT COUNT(*) FROM kz_met_npc WHERE npc_id = @id",
            DbArgs.One("@id", npcId)) > 0;
    }

    public MetNpcRecord? GetMetNpc(long npcId)
    {
        SyncLegacyMetNpcs();
        return _db.QueryOne(
            "SELECT npc_id, first_met_date, met_citadel_id FROM kz_met_npc WHERE npc_id = @id",
            r => new MetNpcRecord(
                r.GetInt64(0),
                GameDateCodec.Parse(r.GetString(1)),
                r.GetInt64(2)),
            DbArgs.One("@id", npcId));
    }

    public IReadOnlyList<MetNpcRecord> GetAllMetNpcs()
    {
        SyncLegacyMetNpcs();
        return _db.Query(
            "SELECT npc_id, first_met_date, met_citadel_id FROM kz_met_npc ORDER BY npc_id ASC",
            r => new MetNpcRecord(
                r.GetInt64(0),
                GameDateCodec.Parse(r.GetString(1)),
                r.GetInt64(2)));
    }

    public void RecordMetNpc(long npcId, GameDate? date = null, long? citadelId = null)
    {
        if (npcId <= 0) return;
        SyncLegacyMetNpcs();

        var metDate = date ?? _clock.Today;
        var npc = _registry.Find(npcId);
        var metCitadel = citadelId ?? (npc?.CurrentCitadelId(_capabilities) ?? _player.CurrentCityId);
        if (metCitadel <= 0) metCitadel = 1;

        var existingDateStr = _db.ScalarString(
            "SELECT first_met_date FROM kz_met_npc WHERE npc_id = @id",
            DbArgs.One("@id", npcId));

        if (string.IsNullOrWhiteSpace(existingDateStr))
        {
            _db.Execute(
                "INSERT INTO kz_met_npc (npc_id, first_met_date, met_citadel_id) VALUES (@id, @date, @cit)",
                new DbArgs()
                    .Add("@id", npcId)
                    .Add("@date", GameDateCodec.Format(metDate))
                    .Add("@cit", metCitadel));
        }
        else if (GameDateCodec.TryParse(existingDateStr, out var existingDate) && metDate.DayIndex < existingDate.DayIndex)
        {
            _db.Execute(
                "UPDATE kz_met_npc SET first_met_date = @date WHERE npc_id = @id",
                new DbArgs()
                    .Add("@date", GameDateCodec.Format(metDate))
                    .Add("@id", npcId));
        }

        CheckAndAwardRosterMilestones();
    }

    public void SyncLegacyMetNpcs()
    {
        var relRows = _db.Query(
            "SELECT npc_id, last_interaction_date FROM kz_relationship WHERE affinity <> 0",
            r => (NpcId: r.GetInt64(0), DateStr: r.GetString(1)));
        var memRows = _db.Query(
            "SELECT npc_id, date FROM kz_memory ORDER BY id ASC",
            r => (NpcId: r.GetInt64(0), DateStr: r.GetString(1)));

        if (relRows.Count == 0 && memRows.Count == 0)
            return;

        var existing = _db.Query(
            "SELECT npc_id, first_met_date FROM kz_met_npc",
            r => (NpcId: r.GetInt64(0), Date: GameDateCodec.Parse(r.GetString(1))))
            .ToDictionary(x => x.NpcId, x => x.Date);

        var candidates = new Dictionary<long, List<GameDate>>();
        foreach (var m in memRows)
        {
            if (GameDateCodec.TryParse(m.DateStr, out var d))
            {
                if (!candidates.TryGetValue(m.NpcId, out var list))
                    candidates[m.NpcId] = list = new List<GameDate>();
                list.Add(d);
            }
        }

        foreach (var r in relRows)
        {
            if (GameDateCodec.TryParse(r.DateStr, out var d))
            {
                if (!candidates.TryGetValue(r.NpcId, out var list))
                    candidates[r.NpcId] = list = new List<GameDate>();
                if (list.Count == 0 || r.DateStr != "184-05-01")
                    list.Add(d);
            }
            else if (!candidates.ContainsKey(r.NpcId))
            {
                candidates[r.NpcId] = new List<GameDate> { _clock.Today };
            }
        }

        foreach (var (npcId, dates) in candidates)
        {
            if (npcId <= 0) continue;
            var earliest = dates.Count > 0 ? dates.OrderBy(d => d.DayIndex).First() : _clock.Today;
            if (!existing.TryGetValue(npcId, out var curDate))
            {
                var citId = _registry.Find(npcId)?.CurrentCitadelId(_capabilities) ?? _player.CurrentCityId;
                if (citId <= 0) citId = 1;
                _db.Execute(
                    "INSERT OR IGNORE INTO kz_met_npc (npc_id, first_met_date, met_citadel_id) VALUES (@id, @date, @cit)",
                    new DbArgs()
                        .Add("@id", npcId)
                        .Add("@date", GameDateCodec.Format(earliest))
                        .Add("@cit", citId));
            }
            else if (earliest.DayIndex < curDate.DayIndex)
            {
                _db.Execute(
                    "UPDATE kz_met_npc SET first_met_date = @date WHERE npc_id = @id",
                    new DbArgs()
                        .Add("@date", GameDateCodec.Format(earliest))
                        .Add("@id", npcId));
            }
        }
    }

    public IReadOnlyList<RosterMilestoneRecord> GetClaimedRosterMilestones()
    {
        return _db.Query(
            "SELECT milestone_key, claimed_date, fame_awarded FROM kz_roster_milestone ORDER BY rowid ASC",
            r => new RosterMilestoneRecord(
                r.GetString(0),
                GameDateCodec.Parse(r.GetString(1)),
                r.GetInt32(2)));
    }

    public IReadOnlyList<RosterMilestoneRecord> CheckAndAwardRosterMilestones()
    {
        SyncLegacyMetNpcs();

        var claimed = _db.Query(
            "SELECT milestone_key FROM kz_roster_milestone",
            r => r.GetString(0))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var metCount = (int)_db.ScalarLong("SELECT COUNT(*) FROM kz_met_npc");
        var today = _clock.Today;
        var todayStr = GameDateCodec.Format(today);

        ISocietyChronicle? society = null;
        _capabilities?.TryGet(CapabilityIds.SocietyChronicle, out society);

        foreach (var m in RosterMilestones)
        {
            if (metCount >= m.Threshold && !claimed.Contains(m.Key))
            {
                AwardMilestone(m.Key, today, todayStr, m.Fame, m.EventKind, m.HeadlineVi, m.DetailVi, society);
                claimed.Add(m.Key);
            }
        }

        if (society is not null && !claimed.Contains("all_events"))
        {
            var events = society.GetHistoricalEvents(occurredOnly: false);
            if (events.Count > 0 && events.All(e => string.Equals(e.Status, "viewed", StringComparison.OrdinalIgnoreCase)))
            {
                const int allEventsFame = 150;
                const string headline = "[Võ Tướng Lục] Chứng nhân thời cuộc — Đã chứng kiến trọn vẹn mọi sử biến thiên hạ";
                const string detail = "Trải qua mọi biến cố thăng trầm của thời đại Tam Quốc, tận mắt chứng kiến toàn bộ sử biến thiên hạ.";
                AwardMilestone("all_events", today, todayStr, allEventsFame, "history", headline, detail, society);
                claimed.Add("all_events");
            }
        }

        return GetClaimedRosterMilestones();
    }

    private void AwardMilestone(
        string key,
        GameDate today,
        string todayStr,
        int fame,
        string eventKind,
        string headlineVi,
        string detailVi,
        ISocietyChronicle? society)
    {
        var inserted = _db.Execute(
            "INSERT OR IGNORE INTO kz_roster_milestone (milestone_key, claimed_date, fame_awarded) VALUES (@key, @date, @fame)",
            new DbArgs().Add("@key", key).Add("@date", todayStr).Add("@fame", fame));

        if (inserted <= 0)
            return;

        _attributes.GainFame(fame, headlineVi, virtueDelta: 2, credDelta: 2);
        society?.RecordMilestoneEvent(today, eventKind, headlineVi, detailVi, $"milestone:{key} +{fame} Uy danh");
    }
}
