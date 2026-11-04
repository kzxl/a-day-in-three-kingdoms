using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Base.Player;

/// <summary>
/// Core implementation of the Bách Gia Thiên Mệnh passive talent web service.
/// Governs node unlocking, graph prerequisites, point economy, and milestone evaluations.
/// </summary>
internal sealed class PlayerDestinyService : IPlayerDestiny
{
    private readonly IDbSession _db;
    private readonly long _playerId;
    private readonly ICapabilityBroker _capabilities;
    private readonly IGameClock _clock;
    private readonly ILog? _log;

    private readonly List<DestinyNodeDef> _allNodes = [];
    private readonly Dictionary<string, DestinyNodeDef> _nodesById = new(StringComparer.OrdinalIgnoreCase);

    public PlayerDestinyService(
        IDbSession db,
        long playerId,
        ICapabilityBroker capabilities,
        IGameClock clock,
        ILog? log)
    {
        _db = db;
        _playerId = playerId;
        _capabilities = capabilities;
        _clock = clock;
        _log = log;

        LoadNodes();
        EnsureRootNodeLearned();
        EvaluateMilestones();
    }

    public IReadOnlyList<DestinyNodeDef> AllNodes => _allNodes;

    public DestinyState State => BuildState();

    private void LoadNodes()
    {
        _allNodes.Clear();
        _nodesById.Clear();

        var rows = _db.Query(
            @"SELECT id, school, tier, name_vi, summary_vi, point_cost, required_school_points,
                     prerequisites, mod_code, mod_value, grid_x, grid_y
              FROM pl_destiny_node ORDER BY school, tier, id",
            r => new DestinyNodeDef(
                r.GetString(0),
                (DestinySchool)r.GetInt32(1),
                (DestinyNodeTier)r.GetInt32(2),
                r.GetString(3),
                r.GetString(4),
                r.GetInt32(5),
                r.GetInt32(6),
                r.GetString(7).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                r.GetString(8),
                r.GetInt32(9),
                r.GetInt32(10),
                r.GetInt32(11)));

        foreach (var node in rows)
        {
            _allNodes.Add(node);
            _nodesById[node.Id] = node;
        }
    }

    private void EnsureRootNodeLearned()
    {
        var hasRoot = _db.ScalarLong(
            "SELECT COUNT(1) FROM pl_destiny_talent WHERE player_id = @id AND node_id = 'destiny_root'",
            DbArgs.One("@id", _playerId)) > 0;

        if (!hasRoot)
        {
            _db.Execute(
                "INSERT OR IGNORE INTO pl_destiny_talent (player_id, node_id, learned_day) VALUES (@id, 'destiny_root', @day)",
                new DbArgs().Add("@id", _playerId).Add("@day", (int)_clock.Today.DayIndex));
        }
    }

    private DestinyState BuildState()
    {
        var learnedIds = _db.Query(
            "SELECT node_id FROM pl_destiny_talent WHERE player_id = @id",
            r => r.GetString(0),
            DbArgs.One("@id", _playerId)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var milestones = _db.Query(
            @"SELECT code, category, name_vi, description_vi, universal_points, resonance_points,
                     resonance_school, is_completed, completed_day
              FROM pl_destiny_milestone ORDER BY code",
            r => new DestinyMilestoneDef(
                r.GetString(0),
                r.GetString(1),
                r.GetString(2),
                r.GetString(3),
                r.GetInt32(4),
                r.GetInt32(5),
                (DestinySchool)r.GetInt32(6),
                r.GetInt32(7) == 1,
                r.IsDbNull(8) ? null : r.GetInt32(8)));

        var totalUniversalEarned = milestones.Where(m => m.IsCompleted).Sum(m => m.UniversalPoints);
        var resonanceEarned = new Dictionary<DestinySchool, int>();
        foreach (DestinySchool s in Enum.GetValues<DestinySchool>())
        {
            if (s != DestinySchool.Universal)
            {
                resonanceEarned[s] = milestones.Where(m => m.IsCompleted && m.ResonanceSchool == s).Sum(m => m.ResonancePoints);
            }
        }

        var resonanceSpent = new Dictionary<DestinySchool, int>();
        var totalUniversalSpent = 0;
        var totalPointsSpent = 0;

        // Tally spent points across learned nodes
        foreach (var id in learnedIds)
        {
            if (!_nodesById.TryGetValue(id, out var node) || node.PointCost <= 0)
            {
                continue;
            }

            totalPointsSpent += node.PointCost;

            if (node.School != DestinySchool.Universal && resonanceEarned.TryGetValue(node.School, out var earned) &&
                (!resonanceSpent.TryGetValue(node.School, out var spent) || spent < earned))
            {
                resonanceSpent[node.School] = (resonanceSpent.GetValueOrDefault(node.School)) + 1;
            }
            else
            {
                totalUniversalSpent += node.PointCost;
            }
        }

        var resonanceAvailable = new Dictionary<DestinySchool, int>();
        foreach (var (school, earned) in resonanceEarned)
        {
            var spent = resonanceSpent.GetValueOrDefault(school);
            resonanceAvailable[school] = Math.Max(0, earned - spent);
        }

        var universalAvailable = Math.Max(0, totalUniversalEarned - totalUniversalSpent);
        var totalEarned = totalUniversalEarned + resonanceEarned.Values.Sum();

        return new DestinyState(
            TotalPointsEarned: totalEarned,
            PointsSpent: totalPointsSpent,
            UniversalPointsAvailable: universalAvailable,
            ResonancePointsAvailable: resonanceAvailable,
            LearnedNodeIds: learnedIds,
            Milestones: milestones);
    }

    public bool CanLearnNode(string nodeId, out string? reasonVi)
    {
        reasonVi = null;
        if (!_nodesById.TryGetValue(nodeId, out var node))
        {
            reasonVi = "Bí pháp thiên mệnh không tồn tại trong điển tịch.";
            return false;
        }

        var state = State;
        if (state.LearnedNodeIds.Contains(nodeId))
        {
            reasonVi = "Thiên tư này đã được lĩnh hội từ trước.";
            return false;
        }

        // Prerequisites check
        if (node.PrerequisiteIds.Count > 0)
        {
            var hasPrereq = node.PrerequisiteIds.Any(p => state.LearnedNodeIds.Contains(p));
            if (!hasPrereq)
            {
                var prereqNames = node.PrerequisiteIds
                    .Select(p => _nodesById.TryGetValue(p, out var n) ? n.NameVi : p);
                reasonVi = $"Chưa khai thông mạch đạo liên kết. Yêu cầu lĩnh hội một trong các điểm: {string.Join(" hoặc ", prereqNames)}.";
                return false;
            }
        }

        // Required points in school
        if (node.RequiredSchoolPoints > 0)
        {
            var pointsInSchool = state.LearnedNodeIds
                .Select(id => _nodesById.GetValueOrDefault(id))
                .Where(n => n != null && n.School == node.School && n.Id != "destiny_root")
                .Sum(n => n!.PointCost);

            if (pointsInSchool < node.RequiredSchoolPoints)
            {
                reasonVi = $"Cần tích lũy ít nhất {node.RequiredSchoolPoints} điểm thuộc học phái này trước khi lĩnh hội đặc kỹ cao cấp (Hiện có: {pointsInSchool}).";
                return false;
            }
        }

        // Available points check
        var hasResonance = node.School != DestinySchool.Universal &&
                           state.ResonancePointsAvailable.TryGetValue(node.School, out var resPts) && resPts > 0;
        if (!hasResonance && state.UniversalPointsAvailable < node.PointCost)
        {
            reasonVi = "Không đủ Điểm Thiên Mệnh (Yêu cầu 1 Điểm Tự Do hoặc 1 Điểm Ngộ Đạo chuyên biệt).";
            return false;
        }

        return true;
    }

    public (bool Success, string MessageVi) LearnNode(string nodeId)
    {
        if (!CanLearnNode(nodeId, out var reason))
        {
            return (false, reason ?? "Không thể lĩnh hội thiên tư này.");
        }

        var node = _nodesById[nodeId];
        _db.Execute(
            "INSERT INTO pl_destiny_talent (player_id, node_id, learned_day) VALUES (@id, @node, @day)",
            new DbArgs().Add("@id", _playerId).Add("@node", nodeId).Add("@day", (int)_clock.Today.DayIndex));

        _log?.Info($"[base.player] protagonist learned destiny talent: {node.NameVi} ({nodeId})");
        return (true, $"Lĩnh hội thành công thiên tư [{node.NameVi}]! Khai mở: {node.SummaryVi}");
    }

    public (bool Success, string MessageVi) RespecAll()
    {
        var count = _db.ScalarLong(
            "SELECT COUNT(1) FROM pl_destiny_talent WHERE player_id = @id AND node_id != 'destiny_root'",
            DbArgs.One("@id", _playerId));

        if (count == 0)
        {
            return (false, "Hiện chưa phân bổ điểm thiên mệnh nào để tẩy tủy.");
        }

        _db.Execute(
            "DELETE FROM pl_destiny_talent WHERE player_id = @id AND node_id != 'destiny_root'",
            DbArgs.One("@id", _playerId));

        _log?.Info($"[base.player] protagonist respec destiny talents. Reset {count} nodes.");
        return (true, $"Tẩy tủy tu tâm thành công! Toàn bộ {count} điểm thiên mệnh đã được hoàn trả về ngân sách.");
    }

    public void EvaluateMilestones()
    {
        var today = (int)_clock.Today.DayIndex;

        // 1. Wealth milestones
        if (_capabilities.TryGet<IWallet>(CapabilityIds.PlayerWallet, out var wallet) && wallet is not null)
        {
            if (wallet.Balance >= 10000) MarkMilestone("ms_wealth_1", today);
            if (wallet.Balance >= 50000) MarkMilestone("ms_wealth_2", today);
        }

        // 2. Exploration milestones
        if (_capabilities.TryGet<IPlayerState>(CapabilityIds.PlayerState, out var playerState) && playerState is not null)
        {
            var visitedCount = _db.ScalarLong(
                "SELECT COUNT(DISTINCT city_id) FROM pl_bag WHERE quantity > 0",
                DbArgs.Empty);
            if (visitedCount >= 5) MarkMilestone("ms_explore_1", today);
            if (visitedCount >= 15) MarkMilestone("ms_explore_2", today);
        }

        // 3. Court office
        if (_capabilities.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court is not null)
        {
            if (court.OfficeId > 0) MarkMilestone("ms_court_1", today);
        }

        // 4. Martial bandit defeat
        if (_capabilities.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var military) && military is not null)
        {
            if (military.AllStrongholds().Any(s => s.ClearedCount > 0)) MarkMilestone("ms_martial_1", today);
        }

        // 5. Social sworn brother
        if (_capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            if (social.GetSwornBrothers().Count > 0) MarkMilestone("ms_social_1", today);
        }

        // 6. Lifespan milestone (30 days of survival)
        if (today >= 30)
        {
            MarkMilestone("ms_life_1", today);
        }
    }

    private void MarkMilestone(string code, int day)
    {
        var completed = _db.ScalarLong(
            "SELECT is_completed FROM pl_destiny_milestone WHERE code = @code",
            DbArgs.One("@code", code));

        if (completed == 0)
        {
            _db.Execute(
                "UPDATE pl_destiny_milestone SET is_completed = 1, completed_day = @day WHERE code = @code",
                new DbArgs().Add("@day", day).Add("@code", code));

            _log?.Info($"[base.player] destiny milestone achieved: {code} on day {day}");
        }
    }

    public int GetModifier(string modCode)
    {
        if (string.IsNullOrWhiteSpace(modCode)) return 0;

        return (int)_db.ScalarLong(
            @"SELECT COALESCE(SUM(n.mod_value), 0)
              FROM pl_destiny_talent t
              JOIN pl_destiny_node n ON t.node_id = n.id
              WHERE t.player_id = @id AND n.mod_code = @code",
            new DbArgs().Add("@id", _playerId).Add("@code", modCode));
    }

    public bool HasKeystone(string keystoneId)
    {
        return _db.ScalarLong(
            "SELECT COUNT(1) FROM pl_destiny_talent WHERE player_id = @id AND node_id = @node",
            new DbArgs().Add("@id", _playerId).Add("@node", keystoneId)) > 0;
    }
}
