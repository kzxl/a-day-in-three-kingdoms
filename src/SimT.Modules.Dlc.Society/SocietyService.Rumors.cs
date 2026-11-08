using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService : ITavernRumors
{
    private IRng? _rumorRng;
    private IActionTime? _rumorTime;
    internal void SetRumorContext(IRng rng, IActionTime time)
    {
        _rumorRng = rng;
        _rumorTime = time;
    }

    public IReadOnlyList<RumorView> Listen()
    {
        var city = _player.CurrentCityId;
        var cached = ActiveAt(city);
        if (cached.Count > 0) return cached;
        var wallet = _broker?.Get<IWallet>(CapabilityIds.PlayerWallet)
            ?? throw new SimException("rumor.wallet", "Không tìm thấy ví tiền.");
        if (wallet.Liquid < TavernRules.CostQuan)
            throw new SimException("rumor.cash", $"Cần {TavernRules.CostQuan} quan để uống rượu nghe chuyện.");
        var candidates = BuildRumorCandidates(city);
        if (candidates.Count < TavernRules.BatchSize)
            throw new SimException("rumor.sources", "Chưa đủ chuyện mới trong thành.");
        var rng = _rumorRng ?? throw new InvalidOperationException("Rumor RNG not initialized.");
        var day = _clock.Today.DayIndex;
        var rngState = rng.State;
        // Keep a varied first round; fill remaining slots from real, distinct facts.
        var groups = candidates.GroupBy(c => c.Kind).ToList();
        var selected = new List<RumorView>();
        while (groups.Count > 0 && selected.Count < TavernRules.BatchSize)
        {
            var histIdx = selected.Count == 1
                ? groups.FindIndex(g => g.Key == "event" && g.First().EventKey?.StartsWith("evt_", StringComparison.Ordinal) == true)
                : -1;
            var index = selected.Count == 0 && groups[0].Key == "price"
                ? 0
                : histIdx >= 0
                    ? histIdx
                    : rng.Next(0, groups.Count);
            var choices = groups[index].ToArray();
            selected.Add(choices[0]);
            groups.RemoveAt(index);
        }
        foreach (var candidate in candidates)
        {
            if (selected.Count == TavernRules.BatchSize) break;
            if (!selected.Contains(candidate)) selected.Add(candidate);
        }
        try
        {
            _db.Transaction(() =>
            {
                wallet.Charge(TavernRules.CostQuan, TavernRules.ActionCode, "Uống rượu nghe chuyện");
                foreach (var fact in selected)
                {
                    var isTrue = rng.NextDouble() >= TavernRules.FalseProbability;
                    var target = fact.TargetCityId;
                    var narrative = fact.NarrativeVi;
                    if (!isTrue)
                    {
                        var alternatives = _map.Citadels.Where(c => c.Id != target).OrderBy(c => c.Id).ToArray();
                        var wrong = alternatives[rng.Next(0, alternatives.Length)];
                        var originalName = _map.Find(target ?? 0)?.NameVi;
                        if (originalName is not null) narrative = narrative.Replace(originalName, wrong.NameVi);
                        target = wrong.Id;
                    }
                    _db.Execute("""
                        INSERT INTO so_rumor(heard_city_id,heard_day,expires_day,kind,title_vi,narrative_vi,
                            is_true,item_id,target_city_id,npc_id,karma_seed_id,event_key,event_day)
                        VALUES(@city,@day,@expiry,@kind,@title,@text,@truth,@item,@target,@npc,@seed,@key,@eventDay)
                        """, new DbArgs().Add("@city", city).Add("@day", day)
                        .Add("@expiry", day + TavernRules.LifetimeDays).Add("@kind", fact.Kind)
                        .Add("@title", fact.TitleVi).Add("@text", narrative).Add("@truth", isTrue ? 1 : 0)
                        .Add("@item", fact.ItemId).Add("@target", target).Add("@npc", fact.NpcId)
                        .Add("@seed", fact.KarmaSeedId).Add("@key", fact.EventKey).Add("@eventDay", fact.EventDay));
                }
                _rumorTime!.Charge(TavernRules.ActionCode);
            });
        }
        catch { rng.Reseed(rngState); throw; }
        return ActiveAt(city);
    }

    public IReadOnlyList<RumorView> ActiveAt(long cityId) => History()
        .Where(r => r.HeardCityId == cityId && !r.IsExpired(_clock.Today.DayIndex)).ToArray();

    public IReadOnlyList<RumorView> History()
    {
        VerifyRumors();
        return _db.Query("""
            SELECT id,heard_city_id,heard_day,expires_day,kind,title_vi,narrative_vi,is_true,verified,
                item_id,target_city_id,npc_id,karma_seed_id,event_key,event_day
            FROM so_rumor ORDER BY heard_day DESC,id
            """, r => new RumorView(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3),
                r.GetString(4), r.GetString(5), r.GetString(6), r.GetBool(8) ? r.GetBool(7) : null,
                NullableId(r, 9), NullableId(r, 10), NullableId(r, 11), NullableId(r, 12),
                r.GetStringOrNull(13), NullableId(r, 14)));
    }

    private static long? NullableId(DbRecord row, int column) => row.IsDbNull(column) ? null : row.GetInt64(column);

    internal void VerifyRumors(long? arrivedCity = null)
    {
        var day = _clock.Today.DayIndex;
        _db.Execute("""
            UPDATE so_rumor SET verified=1 WHERE verified=0 AND (
                expires_day<=@day OR (kind IN ('price','whereabouts') AND target_city_id=@city))
            """, new DbArgs().Add("@day", day).Add("@city", arrivedCity ?? _player.CurrentCityId));
        // A seed matures when its trigger is reached. Optional sources verify their own event keys.
        var events = _db.Query("""
            SELECT id,karma_seed_id,event_key FROM so_rumor WHERE verified=0 AND kind='event'
            """, r => (Id: r.GetInt64(0), Seed: NullableId(r, 1), Key: r.GetStringOrNull(2)));
        foreach (var e in events)
        {
            var happened = e.Seed is { } seed && GetAllSeeds().Any(s => s.Id == seed
                && (s.IsResolved || s.TriggerAfterDay <= day));
            if (e.Key is { } key)
                foreach (var source in EventSources()) happened |= source.HasOccurred(key, day);
            if (happened) _db.Execute("UPDATE so_rumor SET verified=1 WHERE id=@id", DbArgs.One("@id", e.Id));
        }
    }

    private IEnumerable<IEventRumorSource> EventSources()
    {
        foreach (var id in new[] { CapabilityIds.MarketRumorEvents, CapabilityIds.HistoricalRumorEvents })
            if (_broker is not null && _broker.TryGet<IEventRumorSource>(id, out var source) && source is not null)
                yield return source;
    }
}
