using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Power;

/// <summary>
/// Natural simulation engine for macro geopolitics and frontline faction wars.
/// Operates autonomously on monthly turns to simulate border tensions, sieges, and territorial handovers.
/// Provides <see cref="IGeopoliticalWar"/> to couple into economy (shocks, hazards, sovereign loans).
/// </summary>
public sealed class GeopoliticalConflictEngine : IGeopoliticalWar
{
    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWorldMap _map;
    private readonly ILog _log;
    private readonly ICapabilityBroker? _broker;

    private readonly Dictionary<long, CitadelWarStatus> _statusCache = new();
    private readonly Dictionary<long, FrontlineConflictView> _activeConflictByCitadel = new();
    private bool _cacheLoaded;

    public GeopoliticalConflictEngine(
        IDbSession db,
        IGameClock clock,
        IWorldMap map,
        ILog log,
        ICapabilityBroker? broker = null)
    {
        _db = db;
        _clock = clock;
        _map = map;
        _log = log;
        _broker = broker;
    }

    private void InvalidateCache()
    {
        _statusCache.Clear();
        _activeConflictByCitadel.Clear();
        _cacheLoaded = false;
    }

    private void EnsureConflictCache()
    {
        if (_cacheLoaded) return;
        _activeConflictByCitadel.Clear();
        var list = ActiveConflicts();
        foreach (var c in list)
        {
            _activeConflictByCitadel[c.TargetCitadelId] = c;
        }
        _cacheLoaded = true;
    }

    public IReadOnlyList<FrontlineConflictView> ActiveConflicts()
    {
        return _db.Query(
            """
            SELECT c.id, c.attacker_faction_id, COALESCE(fa.name_vi, 'Ngoại Lực'),
                   c.defender_faction_id, COALESCE(fd.name_vi, 'Bản Địa'),
                   c.target_citadel_id, c.intensity, c.war_stage,
                   c.started_day_index, c.siege_progress, c.supply_attrition, c.status
            FROM po_faction_conflict c
            LEFT JOIN po_faction fa ON fa.id = c.attacker_faction_id
            LEFT JOIN po_faction fd ON fd.id = c.defender_faction_id
            WHERE c.status = 'ACTIVE'
            ORDER BY c.intensity DESC, c.siege_progress DESC
            """,
            reader => new FrontlineConflictView(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetString(4),
                reader.GetInt64(5),
                _map.Find(reader.GetInt64(5))?.NameVi ?? "Thành trì",
                reader.GetInt32(6),
                reader.GetString(7),
                reader.GetInt64(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetString(11)));
    }

    public FrontlineConflictView? GetConflictForCitadel(long citadelId)
    {
        EnsureConflictCache();
        return _activeConflictByCitadel.TryGetValue(citadelId, out var c) ? c : null;
    }

    public CitadelWarStatus GetCitadelWarStatus(long citadelId)
    {
        if (_statusCache.TryGetValue(citadelId, out var cached))
        {
            return cached;
        }

        EnsureConflictCache();
        CitadelWarStatus status;

        if (_activeConflictByCitadel.TryGetValue(citadelId, out var direct))
        {
            var isBesieged = direct.Intensity >= 2 && (direct.WarStage is "SIEGE" or "BREACH");
            status = new CitadelWarStatus(citadelId, IsFrontline: true, IsBesieged: isBesieged, direct.Intensity, direct.Id);
        }
        else
        {
            // Check adjacent frontline pressure
            var neighbours = _map.RouteNeighbours(citadelId);
            FrontlineConflictView? nConflict = null;
            foreach (var nid in neighbours)
            {
                if (_activeConflictByCitadel.TryGetValue(nid, out var n) && n.Intensity >= 2)
                {
                    nConflict = n;
                    break;
                }
            }

            if (nConflict != null)
            {
                status = new CitadelWarStatus(citadelId, IsFrontline: true, IsBesieged: false, Intensity: 1, ActiveConflictId: null);
            }
            else
            {
                status = new CitadelWarStatus(citadelId, IsFrontline: false, IsBesieged: false, Intensity: 0, ActiveConflictId: null);
            }
        }

        _statusCache[citadelId] = status;
        return status;
    }

    public bool IsCitadelUnderSiege(long citadelId) => GetCitadelWarStatus(citadelId).IsBesieged;

    public bool IsRouteBlockaded(long originCitadelId, long destinationCitadelId)
    {
        var originStatus = GetCitadelWarStatus(originCitadelId);
        var destStatus = GetCitadelWarStatus(destinationCitadelId);

        // If either terminus is besieged, route is blockaded
        if (originStatus.IsBesieged || destStatus.IsBesieged) return true;

        // If both endpoints are intense warzones, the corridor is closed
        if (originStatus.Intensity >= 2 && destStatus.Intensity >= 2) return true;

        return false;
    }

    public void TriggerConflict(long attackerFactionId, long defenderFactionId, long targetCitadelId, int initialIntensity = 1)
    {
        InvalidateCache();
        var existing = GetConflictForCitadel(targetCitadelId);
        if (existing != null)
        {
            var newIntensity = initialIntensity > existing.Intensity
                ? Math.Clamp(initialIntensity, 1, 3)
                : Math.Min(3, existing.Intensity + 1);
            var newStage = newIntensity >= 3 ? "BREACH" : (newIntensity >= 2 ? "SIEGE" : "SKIRMISH");
            _db.Execute(
                """
                UPDATE po_faction_conflict
                SET intensity = @intensity, war_stage = @stage
                WHERE id = @id
                """,
                new DbArgs()
                    .Add("@id", existing.Id)
                    .Add("@intensity", newIntensity)
                    .Add("@stage", newStage));
            InvalidateCache();
            return;
        }

        var stage = initialIntensity >= 3 ? "BREACH" : (initialIntensity >= 2 ? "SIEGE" : "SKIRMISH");
        _db.Execute(
            """
            INSERT INTO po_faction_conflict (
                attacker_faction_id, defender_faction_id, target_citadel_id,
                intensity, war_stage, started_day_index, siege_progress, supply_attrition, status)
            VALUES (@attacker, @defender, @citadel, @intensity, @stage, @day, 0, 0, 'ACTIVE')
            """,
            new DbArgs()
                .Add("@attacker", attackerFactionId)
                .Add("@defender", defenderFactionId)
                .Add("@citadel", targetCitadelId)
                .Add("@intensity", Math.Clamp(initialIntensity, 1, 3))
                .Add("@stage", stage)
                .Add("@day", _clock.Today.DayIndex));
        InvalidateCache();
    }

    public void AdvanceConflictMonth(long dayIndex, ulong seed)
    {
        var rng = new ConflictRng(seed == 0 ? (ulong)(dayIndex * 4099 + 17) : seed);

        // 1. Advance ongoing active conflicts
        var active = ActiveConflicts();
        foreach (var c in active)
        {
            var gov = _db.Query<(int Defense, int Unrest)>(
                "SELECT defense_strength, unrest_level FROM po_city_governance WHERE citadel_id = @id",
                r => (Defense: r.GetInt32(0), Unrest: r.GetInt32(1)),
                DbArgs.One("@id", c.TargetCitadelId));

            var progressDelta = rng.Next(10, 25);
            if (c.Intensity >= 2) progressDelta += 5;
            if (c.Intensity >= 3) progressDelta += 10;

            var newProgress = c.SiegeProgress + progressDelta;
            var newAttrition = c.SupplyAttrition + rng.Next(5, 15);

            // Supply attrition threshold: if supplies collapse before breach, attackers retreat
            if (newAttrition >= 100 && newProgress < 100)
            {
                _db.Execute(
                    "UPDATE po_faction_conflict SET status = 'RESOLVED_REPULSED', siege_progress = @prog, supply_attrition = @attr WHERE id = @id",
                    new DbArgs().Add("@id", c.Id).Add("@prog", newProgress).Add("@attr", newAttrition));

                _log.Info($"Conflict #{c.Id} at citadel {c.TargetCitadelNameVi}: Attacker repulsed due to supply collapse.");
                continue;
            }

            // Citadel Conquest / Handover threshold
            if (newProgress >= 100)
            {
                _db.Execute(
                    """
                    UPDATE po_city_governance
                    SET faction_id = @facId, unrest_level = 75, defense_strength = 40
                    WHERE citadel_id = @citId
                    """,
                    new DbArgs().Add("@facId", c.AttackerFactionId).Add("@citId", c.TargetCitadelId));

                _db.Execute(
                    "UPDATE po_faction_conflict SET status = 'RESOLVED_VICTORY', war_stage = 'PACIFIED', siege_progress = 100 WHERE id = @id",
                    DbArgs.One("@id", c.Id));

                _log.Info($"Conflict #{c.Id}: Citadel {c.TargetCitadelNameVi} captured by Faction {c.AttackerFactionNameVi}!");
                continue;
            }

            // Progressive siege stage update
            var newStage = newProgress >= 80 ? "BREACH" : (newProgress >= 40 ? "SIEGE" : "SKIRMISH");
            var newIntensity = newProgress >= 80 ? 3 : (newProgress >= 40 ? 2 : c.Intensity);
            var defenseDmg = rng.Next(5, 15);
            var unrestSpike = rng.Next(5, 12);

            _db.Execute(
                """
                UPDATE po_city_governance
                SET defense_strength = MAX(0, defense_strength - @defDmg),
                    unrest_level = MIN(100, unrest_level + @unrest)
                WHERE citadel_id = @citId
                """,
                new DbArgs().Add("@defDmg", defenseDmg).Add("@unrest", unrestSpike).Add("@citId", c.TargetCitadelId));

            _db.Execute(
                """
                UPDATE po_faction_conflict
                SET siege_progress = @prog, supply_attrition = @attr, war_stage = @stage, intensity = @intensity
                WHERE id = @id
                """,
                new DbArgs()
                    .Add("@id", c.Id)
                    .Add("@prog", newProgress)
                    .Add("@attr", newAttrition)
                    .Add("@stage", newStage)
                    .Add("@intensity", newIntensity));
        }

        // 2. Ambient Skirmish Generation: If active conflicts < 2, simulate emergent border friction
        var remainingActive = ActiveConflicts();
        if (remainingActive.Count < 2)
        {
            var borderPairs = FindBorderDisputeCandidates();
            if (borderPairs.Count > 0)
            {
                var candidate = borderPairs[rng.Next(borderPairs.Count)];
                TriggerConflict(candidate.AttackerFactionId, candidate.DefenderFactionId, candidate.TargetCitadelId, 1);
                _log.Info($"Emergent border friction triggered: Faction {candidate.AttackerFactionId} clashing with Faction {candidate.DefenderFactionId} at Citadel {candidate.TargetCitadelId}.");
            }
        }

        InvalidateCache();
    }

    private IReadOnlyList<(long AttackerFactionId, long DefenderFactionId, long TargetCitadelId)> FindBorderDisputeCandidates()
    {
        var govRows = _db.Query(
            "SELECT citadel_id, faction_id FROM po_city_governance",
            r => (CitadelId: r.GetInt64(0), FactionId: r.GetInt64(1)));

        var factionByCitadel = govRows.ToDictionary(g => g.CitadelId, g => g.FactionId);
        var results = new List<(long, long, long)>();

        foreach (var (citId, facId) in factionByCitadel)
        {
            if (facId <= 0) continue;
            var neighbours = _map.RouteNeighbours(citId);
            foreach (var nid in neighbours)
            {
                if (factionByCitadel.TryGetValue(nid, out var nFacId) && nFacId > 0 && nFacId != facId)
                {
                    var existing = _db.ScalarLong(
                        "SELECT COUNT(*) FROM po_faction_conflict WHERE target_citadel_id = @id AND status = 'ACTIVE'",
                        DbArgs.One("@id", nid));

                    if (existing == 0)
                    {
                        results.Add((facId, nFacId, nid));
                    }
                }
            }
        }

        return results;
    }

    private sealed class ConflictRng : IRng
    {
        private ulong _state;

        public ConflictRng(ulong seed)
        {
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong State => _state;

        public void Reseed(ulong state) => _state = state == 0 ? 0x9E3779B97F4A7C15UL : state;

        public ulong NextULong()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            var span = (ulong)(long)(maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextULong() % span));
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));
    }
}
