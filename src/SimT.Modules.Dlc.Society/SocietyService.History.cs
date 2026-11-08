using System.Text.Json;
using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService : IEventRumorSource
{
    private readonly HashSet<long> _deceasedNpcIds = new();
    private readonly Dictionary<long, long> _allegianceOverrides = new();
    private long _nextPendingHistoricalDayIndex = long.MaxValue;
    private string? _pendingPlayerSceneEventCode;
    private string? _lastDistantHistoricalStatus;

    private bool IsNpcDeceased(long npcId, string action) =>
        _deceasedNpcIds.Contains(npcId) ||
        action.StartsWith("Đã mất", StringComparison.Ordinal) ||
        (_registry.Find(npcId)?.IsDeceasedFlag ?? false);

    private long GetNpcAllegiance(long npcId) =>
        _allegianceOverrides.TryGetValue(npcId, out var fac) ? fac : 0;

    public string? ConsumeLastHistoricalStatus()
    {
        var msg = _lastDistantHistoricalStatus;
        _lastDistantHistoricalStatus = null;
        return msg;
    }

    private void EnsureHistoricalEventsInitialized()
    {
        var alreadyOccurred = LoadHistoricalEventsWhere(
            "WHERE status IN ('occurred', 'viewed') ORDER BY day_index ASC, code ASC",
            new DbArgs());
        foreach (var evt in alreadyOccurred)
        {
            var dateStr = GameDateCodec.Format(evt.ScheduledDate);
            foreach (var impact in evt.Impacts)
            {
                ApplySingleImpact(impact, dateStr, (int)evt.ScheduledDate.DayIndex, applyTemporaryShocks: false);
            }
        }

        var today = _clock.Today;
        var dueAtStartup = LoadHistoricalEventsWhere(
            "WHERE status = 'pending' AND day_index <= @day ORDER BY day_index ASC, code ASC",
            DbArgs.One("@day", today.DayIndex));

        if (dueAtStartup.Count > 0)
        {
            _db.Transaction(() =>
            {
                foreach (var evt in dueAtStartup)
                {
                    ApplyHistoricalEventInternal(
                        evt,
                        evt.ScheduledDate,
                        applyTemporaryShocks: false,
                        queuePlayerScene: false,
                        finalStatus: "viewed",
                        defaultChoice: "continue");
                }
            });
        }

        RefreshNextPendingHistoricalDay();
    }

    public void OnDayAdvanced(GameDate date)
    {
        if (date.Day == 1 && date.Month == 1) RefreshNpcLifetimes();
        if (date.DayIndex < _nextPendingHistoricalDayIndex)
        {
            return;
        }

        var dueEvents = LoadHistoricalEventsWhere(
            "WHERE status = 'pending' AND day_index <= @day ORDER BY day_index ASC, code ASC",
            DbArgs.One("@day", date.DayIndex));

        if (dueEvents.Count == 0)
        {
            RefreshNextPendingHistoricalDay();
            return;
        }

        _db.Transaction(() =>
        {
            foreach (var evt in dueEvents)
            {
                ApplyHistoricalEventInternal(
                    evt,
                    date,
                    applyTemporaryShocks: true,
                    queuePlayerScene: true,
                    finalStatus: "occurred",
                    defaultChoice: null);
            }
        });

        RefreshNextPendingHistoricalDay();
        VerifyRumors();
    }

    private void ApplyHistoricalEventInternal(
        HistoricalEventView evt,
        GameDate appliedOnDate,
        bool applyTemporaryShocks,
        bool queuePlayerScene,
        string finalStatus,
        string? defaultChoice)
    {
        var checkStatus = _db.ScalarString(
            "SELECT status FROM so_historical_event WHERE code = @code",
            DbArgs.One("@code", evt.Code));
        if (!string.Equals(checkStatus, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var eventDateStr = GameDateCodec.Format(evt.ScheduledDate);
        var startDay = (int)Math.Max(evt.ScheduledDate.DayIndex, appliedOnDate.DayIndex);

        foreach (var impact in evt.Impacts)
        {
            ApplySingleImpact(impact, eventDateStr, startDay, applyTemporaryShocks);
        }

        _db.Execute(
            "UPDATE so_historical_event SET status = @status, occurred_date = @date, choice_code = COALESCE(@choice, choice_code) WHERE code = @code",
            new DbArgs()
                .Add("@status", finalStatus)
                .Add("@date", eventDateStr)
                .Add("@choice", defaultChoice)
                .Add("@code", evt.Code));

        var primaryNpc = evt.RelatedNpcCodes.Count > 0 ? _registry.FindByCode(evt.RelatedNpcCodes[0]) : null;
        var primaryCit = _map.Find(evt.PrimaryCitadelId);

        LogEvent(
            eventDateStr,
            primaryNpc?.Id ?? 0,
            primaryNpc?.NameVi ?? "Sử Quan",
            evt.PrimaryCitadelId,
            primaryCit?.NameVi ?? "Thiên hạ",
            "history",
            $"[Sử Biến] {evt.TitleVi}",
            evt.NarrativeVi,
            BuildImpactSummary(evt),
            evt.ScheduledDate.DayIndex);

        if (queuePlayerScene)
        {
            if (evt.RelatedCitadelIds.Contains(_player.CurrentCityId))
            {
                _pendingPlayerSceneEventCode = evt.Code;
            }
            else
            {
                _lastDistantHistoricalStatus =
                    $"📜 Thiên hạ đại biến ({eventDateStr}): {evt.TitleVi} — đã ghi vào Sử Ký Thế Thái.";
            }
        }
    }

    private void ApplySingleImpact(
        HistoricalEventImpactDef impact,
        string eventDateStr,
        int startDay,
        bool applyTemporaryShocks)
    {
        switch (impact.Kind)
        {
            case "city_owner":
                if (impact.FactionId.HasValue && _broker is not null)
                {
                    _broker.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court);
                    if (court is not null)
                    {
                        var govNpc = !string.IsNullOrWhiteSpace(impact.NpcCode) ? _registry.FindByCode(impact.NpcCode) : null;
                        court.SetCityOwner(impact.CitadelId, impact.FactionId.Value, govNpc?.Id);
                    }
                }
                break;

            case "npc_move":
                if (!string.IsNullOrWhiteSpace(impact.NpcCode))
                {
                    var npc = _registry.FindByCode(impact.NpcCode);
                    if (npc is not null && !npc.IsDeceasedFlag && !_deceasedNpcIds.Contains(npc.Id))
                    {
                        _registry.UpdateCitadel(npc.Id, impact.CitadelId);
                        _db.Execute(
                            "UPDATE so_npc_activity SET current_citadel_id = @cit, current_action = @act, updated_date = @date WHERE npc_id = @id",
                            new DbArgs().Add("@cit", impact.CitadelId).Add("@act", impact.NoteVi).Add("@date", eventDateStr).Add("@id", npc.Id));
                    }
                }
                break;

            case "npc_allegiance":
                if (!string.IsNullOrWhiteSpace(impact.NpcCode) && impact.FactionId.HasValue)
                {
                    var npc = _registry.FindByCode(impact.NpcCode);
                    if (npc is not null)
                    {
                        _allegianceOverrides[npc.Id] = impact.FactionId.Value;
                        if (!_deceasedNpcIds.Contains(npc.Id) && !npc.IsDeceasedFlag)
                        {
                            _db.Execute(
                                "UPDATE so_npc_activity SET current_action = @act, updated_date = @date WHERE npc_id = @id",
                                new DbArgs().Add("@act", impact.NoteVi).Add("@date", eventDateStr).Add("@id", npc.Id));
                        }
                    }
                }
                break;

            case "npc_death":
                if (!string.IsNullOrWhiteSpace(impact.NpcCode))
                {
                    var npc = _registry.FindByCode(impact.NpcCode);
                    if (npc is not null)
                    {
                        _deceasedNpcIds.Add(npc.Id);
                        _registry.MarkDeceased(npc.Id, impact.NoteVi);
                        _db.Execute(
                            "UPDATE so_npc_activity SET current_action = @act, stamina = 0, updated_date = @date WHERE npc_id = @id",
                            new DbArgs().Add("@act", $"Đã mất ({impact.NoteVi})").Add("@date", eventDateStr).Add("@id", npc.Id));
                    }
                }
                break;

            case "price_shock":
                if (applyTemporaryShocks &&
                    _broker is not null &&
                    _broker.TryGet<IEventRumorSource>(CapabilityIds.MarketRumorEvents, out var marketSource) &&
                    marketSource is IMarketShockTrigger shockTrigger)
                {
                    shockTrigger.TriggerShock(
                        impact.CitadelId,
                        impact.ShockType ?? "SIEGE",
                        startDay,
                        Math.Max(1, impact.DurationDays),
                        Math.Max(1000, impact.SeverityPermille),
                        impact.NoteVi);
                }
                break;

            case "route_danger":
            case "route_block":
                if (applyTemporaryShocks)
                {
                    var isBlocked = string.Equals(impact.Kind, "route_block", StringComparison.OrdinalIgnoreCase);
                    var dangerDelta = Math.Max(15, (impact.SeverityPermille - 1000) / 10);
                    _map.SetRouteHazard(
                        impact.CitadelId,
                        impact.TargetCitadelId ?? 0,
                        dangerDelta,
                        isBlocked,
                        startDay + Math.Max(1, impact.DurationDays),
                        impact.NoteVi);
                }
                break;
        }
    }

    public IReadOnlyList<HistoricalEventView> GetHistoricalEvents(bool occurredOnly = false) =>
        occurredOnly
            ? LoadHistoricalEventsWhere("WHERE status IN ('occurred', 'viewed') ORDER BY day_index DESC, code DESC", new DbArgs())
            : LoadHistoricalEventsWhere("ORDER BY day_index ASC, code ASC", new DbArgs());

    public HistoricalEventView? FindHistoricalEvent(string code) =>
        LoadHistoricalEventsWhere("WHERE code = @code LIMIT 1", DbArgs.One("@code", code)).FirstOrDefault();

    public HistoricalEventView? DequeuePendingHistoricalEvent(long playerCitadelId)
    {
        if (!string.IsNullOrWhiteSpace(_pendingPlayerSceneEventCode))
        {
            var evt = FindHistoricalEvent(_pendingPlayerSceneEventCode);
            _pendingPlayerSceneEventCode = null;
            if (evt is not null && evt.Status == "occurred" && evt.RelatedCitadelIds.Contains(playerCitadelId))
            {
                return evt;
            }
        }

        return LoadHistoricalEventsWhere("WHERE status = 'occurred' ORDER BY day_index DESC, code DESC", new DbArgs())
            .FirstOrDefault(e => e.RelatedCitadelIds.Contains(playerCitadelId));
    }

    public HistoricalChoiceOutcome ResolveHistoricalEventChoice(string eventCode, string choiceKey)
    {
        var evt = FindHistoricalEvent(eventCode);
        if (evt is null)
        {
            return new HistoricalChoiceOutcome(false, "Không tìm thấy sự biến lịch sử này.", 0, 0, 0);
        }

        var choice = evt.Choices.FirstOrDefault(c => string.Equals(c.Key, choiceKey, StringComparison.OrdinalIgnoreCase))
                     ?? evt.Choices.LastOrDefault()
                     ?? new HistoricalEventChoiceDef("continue", "Tiếp tục", "Khép lại trang sử", 0, 0, 0, 0);

        var alreadyChosen = !string.IsNullOrWhiteSpace(evt.ChoiceCode) && evt.Status == "viewed";
        long copperDelta = 0;
        var fameDelta = 0;
        var virtueDelta = 0;

        if (!alreadyChosen && !string.Equals(choice.Key, "continue", StringComparison.OrdinalIgnoreCase))
        {
            IWallet? wallet = null;
            _broker?.TryGet(CapabilityIds.PlayerWallet, out wallet);
            if (choice.CostCopper > 0)
            {
                if (wallet is null || wallet.Liquid < choice.CostCopper)
                    return new HistoricalChoiceOutcome(false, $"Cần {choice.CostCopper} Quan để chọn '{choice.LabelVi}' (hiện có {wallet?.Liquid ?? 0} Quan).", 0, 0, 0);
                wallet.Charge(choice.CostCopper, JournalCodes.CourtService, $"Sử biến: {evt.TitleVi}");
                copperDelta -= choice.CostCopper;
            }
            if (choice.RewardCopper > 0 && wallet is not null)
            {
                wallet.Pay(choice.RewardCopper, JournalCodes.CourtStipend, $"Sử biến: {evt.TitleVi}");
                copperDelta += choice.RewardCopper;
            }
            if ((choice.FameDelta != 0 || choice.VirtueDelta != 0) &&
                _broker is not null && _broker.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null)
            {
                attrs.GainFame(Math.Max(0, choice.FameDelta), $"Sử biến: {evt.TitleVi}", choice.VirtueDelta);
                (fameDelta, virtueDelta) = (choice.FameDelta, choice.VirtueDelta);
            }
        }

        _db.Execute(
            "UPDATE so_historical_event SET status = 'viewed', choice_code = COALESCE(choice_code, @choice) WHERE code = @code",
            new DbArgs().Add("@choice", choice.Key).Add("@code", evt.Code));
        if (string.Equals(_pendingPlayerSceneEventCode, evt.Code, StringComparison.OrdinalIgnoreCase))
            _pendingPlayerSceneEventCode = null;
        if (_broker is not null && _broker.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
            social.CheckAndAwardRosterMilestones();

        var msg = string.Equals(choice.Key, "continue", StringComparison.OrdinalIgnoreCase)
            ? $"Đã ghi nhận sử biến: {evt.TitleVi}."
            : $"[{evt.TitleVi}] Đã thực hiện: {choice.LabelVi}.";
        return new HistoricalChoiceOutcome(true, msg, copperDelta, fameDelta, virtueDelta);
    }

    public IReadOnlyList<RumorEventHint> Upcoming(long day, int horizonDays)
    {
        var maxDay = day + Math.Max(1, horizonDays);
        return _db.Query(
            "SELECT code, primary_citadel_id, day_index, rumor_title_vi, rumor_narrative_vi FROM so_historical_event " +
            "WHERE status = 'pending' AND day_index >= @day AND day_index <= @maxDay ORDER BY day_index ASC, code ASC",
            row => new RumorEventHint(row.GetString(0), row.GetInt64(1), row.GetInt64(2), row.GetString(3), row.GetString(4)),
            new DbArgs().Add("@day", day).Add("@maxDay", maxDay));
    }

    public bool HasOccurred(string eventKey, long day) =>
        _db.ScalarLong(
            "SELECT COUNT(*) FROM so_historical_event WHERE code = @code AND (status IN ('occurred', 'viewed') OR day_index <= @day)",
            new DbArgs().Add("@code", eventKey).Add("@day", day)) > 0;

    private void RefreshNextPendingHistoricalDay()
    {
        var next = _db.ScalarLong("SELECT COALESCE(MIN(day_index), 999999999) FROM so_historical_event WHERE status = 'pending'");
        _nextPendingHistoricalDayIndex = next > 0 ? next : long.MaxValue;
    }

    private IReadOnlyList<HistoricalEventView> LoadHistoricalEventsWhere(string whereAndOrderSql, DbArgs args)
    {
        return _db.Query(
            $"SELECT code, year, month, day, title_vi, narrative_vi, primary_citadel_id, related_citadels, related_npcs, " +
            $"background_key, choices_json, impacts_json, status, occurred_date, choice_code FROM so_historical_event {whereAndOrderSql}",
            row =>
            {
                var code = row.GetString(0);
                var date = new GameDate(row.GetInt32(1), row.GetInt32(2), row.GetInt32(3));
                var citadels = row.GetString(7)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => long.TryParse(s, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToArray();
                var npcs = row.GetString(8).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var choices = JsonSerializer.Deserialize<HistoricalEventChoiceDef[]>(row.GetString(10)) ?? Array.Empty<HistoricalEventChoiceDef>();
                var impacts = JsonSerializer.Deserialize<HistoricalEventImpactDef[]>(row.GetString(11)) ?? Array.Empty<HistoricalEventImpactDef>();
                var occurredStr = row.GetStringOrNull(13);
                GameDate? occurredDate = !string.IsNullOrWhiteSpace(occurredStr) ? GameDateCodec.Parse(occurredStr) : null;

                return new HistoricalEventView(
                    code, date, row.GetString(4), row.GetString(5), row.GetInt64(6), citadels, npcs, row.GetString(9),
                    choices, impacts, row.GetString(12), occurredDate, row.GetStringOrNull(14), $"evt:{code}");
            },
            args);
    }

    private static string BuildImpactSummary(HistoricalEventView evt) =>
        $"evt:{evt.Code} · {evt.Impacts.FirstOrDefault()?.NoteVi ?? "Thiên hạ biến chuyển"}";
}
