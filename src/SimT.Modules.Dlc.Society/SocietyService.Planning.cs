using System;
using System.Collections.Generic;
using System.Linq;
using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService
{
    public NpcNeedsDef? GetNpcNeeds(long npcId)
    {
        return _db.QueryOne(
            "SELECT npc_id, safety_need, wealth_need, fame_need, power_need, social_need, updated_date " +
            "FROM so_npc_needs WHERE npc_id = @id",
            r => new NpcNeedsDef(
                r.GetInt64(0),
                r.GetInt32(1),
                r.GetInt32(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.GetInt32(5),
                GameDateCodec.Parse(r.GetString(6))),
            DbArgs.One("@id", npcId));
    }

    public NpcGoalDef? GetNpcGoal(long npcId)
    {
        return _db.QueryOne(
            "SELECT npc_id, goal_type, stage, current_step, total_steps, target_citadel_id, target_npc_id, " +
            "target_item_id, capital_allocated, progress, plan_desc, started_day_index, updated_date " +
            "FROM so_npc_goal WHERE npc_id = @id",
            r => new NpcGoalDef(
                r.GetInt64(0),
                r.GetString(1),
                r.GetString(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.IsDBNull(5) ? null : r.GetInt64(5),
                r.IsDBNull(6) ? null : r.GetInt64(6),
                r.IsDBNull(7) ? null : r.GetInt64(7),
                r.GetInt64(8),
                r.GetInt32(9),
                r.GetString(10),
                r.GetInt64(11),
                GameDateCodec.Parse(r.GetString(12))),
            DbArgs.One("@id", npcId));
    }

    private void EnsureDecisionModelInitialized()
    {
        var hasNeedsTable = _db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='so_npc_needs'") > 0;
        if (!hasNeedsTable) return;

        var count = _db.ScalarLong("SELECT COUNT(*) FROM so_npc_needs");
        if (count > 0) return;

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            foreach (var npc in _registry.All)
            {
                var a = npc.Attributes;
                var focus = DetermineFocusPath(a);

                var safety = 40;
                var wealth = focus == "merchant" ? 75 : 45;
                var fame = (focus == "martial" || focus == "strategist") ? 65 : 40;
                var power = (focus == "governor" || a.Pol >= 60) ? 65 : 35;
                var social = 50;

                _db.Execute(
                    "INSERT INTO so_npc_needs (npc_id, safety_need, wealth_need, fame_need, power_need, social_need, updated_date) " +
                    "VALUES (@id, @safety, @wealth, @fame, @power, @social, @date)",
                    new DbArgs()
                        .Add("@id", npc.Id)
                        .Add("@safety", safety)
                        .Add("@wealth", wealth)
                        .Add("@fame", fame)
                        .Add("@power", power)
                        .Add("@social", social)
                        .Add("@date", formattedDate));
            }
        });
    }

    private Dictionary<long, NpcNeedsDef> LoadAllNpcNeedsMap()
    {
        return _db.Query(
            "SELECT npc_id, safety_need, wealth_need, fame_need, power_need, social_need, updated_date FROM so_npc_needs",
            r => new NpcNeedsDef(
                r.GetInt64(0),
                r.GetInt32(1),
                r.GetInt32(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.GetInt32(5),
                GameDateCodec.Parse(r.GetString(6))))
            .ToDictionary(n => n.NpcId);
    }

    private Dictionary<long, NpcGoalDef> LoadAllNpcGoalsMap()
    {
        return _db.Query(
            "SELECT npc_id, goal_type, stage, current_step, total_steps, target_citadel_id, target_npc_id, " +
            "target_item_id, capital_allocated, progress, plan_desc, started_day_index, updated_date FROM so_npc_goal",
            r => new NpcGoalDef(
                r.GetInt64(0),
                r.GetString(1),
                r.GetString(2),
                r.GetInt32(3),
                r.GetInt32(4),
                r.IsDBNull(5) ? null : r.GetInt64(5),
                r.IsDBNull(6) ? null : r.GetInt64(6),
                r.IsDBNull(7) ? null : r.GetInt64(7),
                r.GetInt64(8),
                r.GetInt32(9),
                r.GetString(10),
                r.GetInt64(11),
                GameDateCodec.Parse(r.GetString(12))))
            .ToDictionary(g => g.NpcId);
    }

    private void ExecuteNpcDecisionTurn(
        NpcDef npc,
        NpcActivityDef act,
        Dictionary<long, CityGovernanceView>? govByCitadel,
        IMilitaryCommand? military,
        ISocialNetwork? social,
        Dictionary<long, NpcNeedsDef> needsByNpc,
        Dictionary<long, NpcGoalDef> goalsByNpc,
        string formattedDate,
        GameDate date,
        long tick,
        IRng rng)
    {
        var cit = _map.Find(act.CurrentCitadelId);
        var citName = cit?.NameVi ?? "Châu quận";

        var isGovernorOrRuler = act.FocusPath == "governor"
            || npc.Role.Contains("Hoàng Đế")
            || npc.Role.Contains("Quân Chủ")
            || npc.Role.Contains("Thái Thú")
            || npc.Role.Contains("Thái Sư")
            || npc.Role.Contains("Giáo Tổ");

        var currentUnrest = govByCitadel != null && govByCitadel.TryGetValue(act.CurrentCitadelId, out var gView)
            ? gView.UnrestLevel
            : 10;
        var currentSecurity = military != null ? military.GetCitadelSecurity(act.CurrentCitadelId) : 60;

        // 1. Perceive environment and update Needs
        if (!needsByNpc.TryGetValue(npc.Id, out var needs))
        {
            var safety = 40;
            var wealth = act.FocusPath == "merchant" ? 75 : 45;
            var fame = (act.FocusPath == "martial" || act.FocusPath == "strategist") ? 65 : 40;
            var power = isGovernorOrRuler ? 65 : 35;
            _db.Execute(
                "INSERT INTO so_npc_needs (npc_id, safety_need, wealth_need, fame_need, power_need, social_need, updated_date) " +
                "VALUES (@id, @safety, @wealth, @fame, @power, 50, @date) " +
                "ON CONFLICT(npc_id) DO NOTHING",
                new DbArgs().Add("@id", npc.Id).Add("@safety", safety).Add("@wealth", wealth).Add("@fame", fame).Add("@power", power).Add("@date", formattedDate));
            needs = new NpcNeedsDef(npc.Id, safety, wealth, fame, power, 50, date);
            needsByNpc[npc.Id] = needs;
        }
        else
        {
            var newSafety = needs.SafetyNeed;
            var newWealth = needs.WealthNeed;
            if (currentUnrest >= 25) newSafety = Math.Min(100, newSafety + (currentUnrest - 20));
            if (currentSecurity < 45) newSafety = Math.Min(100, newSafety + (45 - currentSecurity));
            if (act.CurrentWealth < 150) newWealth = Math.Min(100, newWealth + 10);
            else if (act.CurrentWealth > 1500) newWealth = Math.Max(20, newWealth - 5);

            if (newSafety != needs.SafetyNeed || newWealth != needs.WealthNeed)
            {
                _db.Execute(
                    "UPDATE so_npc_needs SET safety_need = @safety, wealth_need = @wealth, updated_date = @date WHERE npc_id = @id",
                    new DbArgs().Add("@safety", newSafety).Add("@wealth", newWealth).Add("@date", formattedDate).Add("@id", npc.Id));
                needs = new NpcNeedsDef(npc.Id, newSafety, newWealth, needs.FameNeed, needs.PowerNeed, needs.SocialNeed, date);
                needsByNpc[npc.Id] = needs;
            }
        }

        // 2. Fetch or formulate active multi-step goal
        if (!goalsByNpc.TryGetValue(npc.Id, out var activeGoal))
        {
            activeGoal = FormulateNewGoal(npc, act, needs, cit, isGovernorOrRuler, currentUnrest, currentSecurity, govByCitadel, military, social, formattedDate, tick, rng);
            goalsByNpc[npc.Id] = activeGoal;
        }

        // 3. Execute current stage of the goal
        ExecuteGoalStep(npc, act, activeGoal, citName, isGovernorOrRuler, currentUnrest, formattedDate, tick, rng);
    }

    private NpcGoalDef FormulateNewGoal(
        NpcDef npc,
        NpcActivityDef act,
        NpcNeedsDef needs,
        CitadelDef? cit,
        bool isGovernorOrRuler,
        int currentUnrest,
        int currentSecurity,
        Dictionary<long, CityGovernanceView>? govByCitadel,
        IMilitaryCommand? military,
        ISocialNetwork? social,
        string formattedDate,
        long tick,
        IRng rng)
    {
        var neighbours = _map.RouteNeighbours(act.CurrentCitadelId);

        // Utility scoring
        var fleeScore = (!isGovernorOrRuler && (currentUnrest >= 25 || currentSecurity < 40)) ? (needs.SafetyNeed * 2) : 0;
        var tradeScore = (act.FocusPath == "merchant" || npc.Attributes.Com >= 50) ? (needs.WealthNeed + npc.Attributes.Com) : 10;
        var martialScore = (act.FocusPath == "martial" || npc.Attributes.War >= 50) ? (needs.FameNeed + npc.Attributes.War) : 10;
        var strategistScore = (act.FocusPath == "strategist" || npc.Attributes.Int >= 50) ? (needs.FameNeed + needs.PowerNeed + npc.Attributes.Int) : 10;
        var govScore = isGovernorOrRuler ? 300 : 0;

        string goalType;
        int totalSteps;
        long? targetCitId = null;
        long? targetItemId = null;
        long capitalAllocated = 0;
        string planDesc;

        if (govScore > 0)
        {
            goalType = "CityGovernance";
            totalSteps = 3;
            targetCitId = act.CurrentCitadelId;
            planDesc = "An dân định quốc và củng cố thành phòng";
        }
        else if (fleeScore >= 120 && neighbours.Count > 0)
        {
            goalType = "FleeUnrest";
            totalSteps = 3;
            targetCitId = neighbours
                .OrderBy(nid => govByCitadel != null && govByCitadel.TryGetValue(nid, out var ng) ? ng.UnrestLevel : 20)
                .ThenByDescending(nid => military != null ? military.GetCitadelSecurity(nid) : 50)
                .First();
            planDesc = $"Lánh nạn sang vùng đất thái bình {_map.Find(targetCitId.Value)?.NameVi ?? "phương xa"}";
        }
        else if (tradeScore >= martialScore && tradeScore >= strategistScore && neighbours.Count > 0)
        {
            goalType = "TradeExpedition";
            totalSteps = 4;
            targetCitId = neighbours
                .OrderByDescending(nid => _map.Find(nid)?.PriceFactorPermille ?? 1000)
                .First();
            targetItemId = (rng.Next(3) switch { 0 => 1L, 1 => 3L, _ => 4L }); // Grain, Silk, or Wine
            capitalAllocated = Math.Max(100, Math.Min(act.CurrentWealth / 2, 800));
            planDesc = $"Tổ chức thương đoàn áp tải hàng hóa sang {_map.Find(targetCitId.Value)?.NameVi ?? "thương thành"}";
        }
        else if (martialScore >= strategistScore)
        {
            if (currentUnrest >= 15 || rng.Next(2) == 0)
            {
                goalType = "PacifyBandits";
                totalSteps = 3;
                targetCitId = act.CurrentCitadelId;
                planDesc = "Xuất quân tiễu trừ thảo khấu hẻm núi bảo vệ lê dân";
            }
            else
            {
                goalType = "MilitaryDrill";
                totalSteps = 3;
                targetCitId = act.CurrentCitadelId;
                planDesc = "Thao diễn binh mã và chỉnh đốn võ bị";
            }
        }
        else if (strategistScore > 20)
        {
            if (neighbours.Count > 0 && rng.Next(2) == 0)
            {
                goalType = "SeekPatron";
                totalSteps = 3;
                var randNeighbour = neighbours[rng.Next(neighbours.Count)];
                var playerCityId = _player.CurrentCityId;
                var favorsPlayer = neighbours.Contains(playerCityId)
                    && social != null
                    && social.HasMet(npc.Id)
                    && social.GetDisposition(npc.Id).IsFavored;
                targetCitId = favorsPlayer ? playerCityId : randNeighbour;
                planDesc = favorsPlayer
                    ? $"Khăn gói sang {_map.Find(targetCitId.Value)?.NameVi ?? "danh thành"} hội kiến tri âm hiến kế"
                    : $"Khăn gói sang {_map.Find(targetCitId.Value)?.NameVi ?? "danh thành"} yết kiến chư hầu hiến kế";
            }
            else
            {
                goalType = "ScholarTour";
                totalSteps = 2;
                targetCitId = act.CurrentCitadelId;
                planDesc = "Biên soạn binh thư và diễn thuyết cổ kim";
            }
        }
        else
        {
            goalType = "WandererLife";
            totalSteps = 2;
            targetCitId = neighbours.Count > 0 && rng.Next(2) == 0 ? neighbours[rng.Next(neighbours.Count)] : act.CurrentCitadelId;
            planDesc = "Ngao du sơn thủy và hành hiệp trượng nghĩa";
        }

        var goal = new NpcGoalDef(
            npc.Id,
            goalType,
            "Planning",
            1,
            totalSteps,
            targetCitId,
            null,
            targetItemId,
            capitalAllocated,
            0,
            planDesc,
            tick,
            _clock.Today);

        _db.Execute(
            "INSERT INTO so_npc_goal (npc_id, goal_type, stage, current_step, total_steps, target_citadel_id, target_npc_id, target_item_id, capital_allocated, progress, plan_desc, started_day_index, updated_date) " +
            "VALUES (@id, @type, @stage, @step, @total, @citId, @npcId, @itemId, @cap, @prog, @desc, @startTick, @date) " +
            "ON CONFLICT(npc_id) DO UPDATE SET goal_type = @type, stage = @stage, current_step = @step, total_steps = @total, target_citadel_id = @citId, target_item_id = @itemId, capital_allocated = @cap, progress = @prog, plan_desc = @desc, updated_date = @date",
            new DbArgs()
                .Add("@id", goal.NpcId)
                .Add("@type", goal.GoalType)
                .Add("@stage", goal.Stage)
                .Add("@step", goal.CurrentStep)
                .Add("@total", goal.TotalSteps)
                .Add("@citId", (object?)goal.TargetCitadelId ?? DBNull.Value)
                .Add("@npcId", (object?)goal.TargetNpcId ?? DBNull.Value)
                .Add("@itemId", (object?)goal.TargetItemId ?? DBNull.Value)
                .Add("@cap", goal.CapitalAllocated)
                .Add("@prog", goal.Progress)
                .Add("@desc", goal.PlanDesc)
                .Add("@startTick", goal.StartedDayIndex)
                .Add("@date", formattedDate));

        return goal;
    }

    private void ExecuteGoalStep(
        NpcDef npc,
        NpcActivityDef act,
        NpcGoalDef goal,
        string citName,
        bool isGovernorOrRuler,
        int currentUnrest,
        string formattedDate,
        long tick,
        IRng rng)
    {
        switch (goal.GoalType)
        {
            case "TradeExpedition":
                ExecuteTradeExpeditionStep(npc, act, goal, citName, formattedDate, tick, rng);
                break;
            case "PacifyBandits":
                ExecutePacifyBanditsStep(npc, act, goal, citName, formattedDate, tick, rng);
                break;
            case "MilitaryDrill":
                ExecuteMilitaryDrillStep(npc, act, goal, citName, formattedDate, tick, rng);
                break;
            case "CityGovernance":
                ExecuteCityGovernanceStep(npc, act, goal, citName, formattedDate, tick, rng);
                break;
            case "SeekPatron":
                ExecuteSeekPatronStep(npc, act, goal, citName, formattedDate, tick, rng);
                break;
            case "FleeUnrest":
                ExecuteFleeUnrestStep(npc, act, goal, citName, formattedDate, tick, rng);
                break;
            default:
                ExecuteDefaultLifeStep(npc, act, goal, citName, formattedDate, tick, rng);
                break;
        }
    }

    private void ExecuteTradeExpeditionStep(
        NpcDef npc,
        NpcActivityDef act,
        NpcGoalDef goal,
        string citName,
        string formattedDate,
        long tick,
        IRng rng)
    {
        var targetCit = goal.TargetCitadelId.HasValue ? _map.Find(goal.TargetCitadelId.Value) : null;
        var targetName = targetCit?.NameVi ?? "Phương xa";
        var itemName = goal.TargetItemId switch { 1L => "lúa mì lương thực", 3L => "tơ lụa thục cẩm", _ => "rượu bồ đào" };

        if (goal.CurrentStep == 1) // Planning & Capital Pooling
        {
            var actionText = $"Khảo sát giá {itemName} và trù bị thương vụ";
            var headline = $"{npc.NameVi} lên kế hoạch gom hàng {itemName} chuyển sang {targetName}";
            var detail = $"{npc.NameVi} nhận thấy chênh lệch giá {itemName} giữa {citName} và {targetName}, quyết định trích xuất {goal.CapitalAllocated} quan gom hàng.";

            ApplyNpcGrowth(npc.Id, formattedDate, 0, 0, 0, 0, 0, 1, 0, 0);
            UpdateActivity(npc.Id, formattedDate, actionText, 0, 1, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "commerce", headline, detail, "[Kế Hoạch Giao Thương 1/4]", tick);
            AdvanceGoal(goal, "Preparing", 2, 25, formattedDate);
        }
        else if (goal.CurrentStep == 2) // Preparing: Purchasing goods
        {
            var cost = Math.Min(act.CurrentWealth, goal.CapitalAllocated);
            var qty = (int)Math.Max(5, cost / 20);

            // Physical cargo tracking for conservation of mass
            _db.Execute(
                "INSERT INTO so_merchant_cargo (npc_id, item_id, quantity, unit_cost_quan, origin_citadel_id) " +
                "VALUES (@id, @itemId, @qty, @unitCost, @originCit) " +
                "ON CONFLICT(npc_id, item_id) DO UPDATE SET quantity = quantity + @qty",
                new DbArgs()
                    .Add("@id", npc.Id)
                    .Add("@itemId", goal.TargetItemId ?? 1L)
                    .Add("@qty", qty)
                    .Add("@unitCost", 20)
                    .Add("@originCit", act.CurrentCitadelId));

            var actionText = $"Thu mua {qty} kiện {itemName} tại chợ {citName}";
            var headline = $"{npc.NameVi} gom hàng {itemName} tại {citName}";
            var detail = $"{npc.NameVi} đã chi {cost} quan tại các quầy sạp {citName} để gom {qty} kiện {itemName}, chuẩn bị bốc dỡ lên xe ngựa xuất hành.";

            UpdateActivity(npc.Id, formattedDate, actionText, -(int)cost, 2, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "commerce", headline, detail, $"[Gom Hàng 2/4] -{cost} Quan", tick);
            AdvanceGoal(goal, "Executing", 3, 50, formattedDate);
        }
        else if (goal.CurrentStep == 3) // Executing: In Transit / Relocating
        {
            if (goal.TargetCitadelId.HasValue && goal.TargetCitadelId.Value != act.CurrentCitadelId)
            {
                _db.Execute(
                    "UPDATE so_npc_activity SET current_citadel_id = @newCit, updated_date = @date WHERE npc_id = @id",
                    new DbArgs().Add("@newCit", goal.TargetCitadelId.Value).Add("@date", formattedDate).Add("@id", npc.Id));
                _registry.UpdateCitadel(npc.Id, goal.TargetCitadelId.Value);
            }

            var actionText = $"Áp tải đoàn xe {itemName} cập bến {targetName}";
            var headline = $"{npc.NameVi} dẫn đầu thương đội tiến vào {targetName}";
            var detail = $"Băng qua dặm trường quan lộ hiểm trở, đoàn xe chở đầy {itemName} của {npc.NameVi} đã an toàn đến bờ cõi {targetName}.";

            UpdateActivity(npc.Id, formattedDate, actionText, 0, 2, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, goal.TargetCitadelId ?? act.CurrentCitadelId, targetName, "travel", headline, detail, "[Áp Tải Chuyển Dời 3/4]", tick);
            AdvanceGoal(goal, "Completing", 4, 75, formattedDate);
        }
        else // Step 4: Completing / Selling cargo
        {
            var cost = goal.CapitalAllocated;
            var profitMargin = 25 + rng.Next(15); // 25..40% return
            var revenue = cost + (cost * profitMargin / 100);
            var profit = revenue - cost;

            // Clear cargo
            _db.Execute("DELETE FROM so_merchant_cargo WHERE npc_id = @id AND item_id = @itemId",
                new DbArgs().Add("@id", npc.Id).Add("@itemId", goal.TargetItemId ?? 1L));

            // Stat growth & Need satisfaction
            ApplyNpcGrowth(npc.Id, formattedDate, 0, 0, 0, 0, 0, 1, 0, 0); // +1 COM
            _db.Execute(
                "UPDATE so_npc_needs SET wealth_need = MAX(15, wealth_need - 25), updated_date = @date WHERE npc_id = @id",
                new DbArgs().Add("@date", formattedDate).Add("@id", npc.Id));

            var actionText = $"Thu lời {profit} quan từ chuyến buôn {itemName} tại {targetName}";
            var headline = $"{npc.NameVi} đại thắng chuyến buôn tại {targetName}";
            var detail = $"{npc.NameVi} xuất bán toàn bộ {itemName} cho các thương gia {targetName}, thu về {revenue} quan tiền đồng (lời ròng {profit} quan). Uy danh thương hội tăng mạnh.";

            UpdateActivity(npc.Id, formattedDate, actionText, (int)revenue, 6, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, goal.TargetCitadelId ?? act.CurrentCitadelId, targetName, "commerce", headline, detail, $"[Hoàn Tất Giao Thương 4/4] +{profit} Quan, +1 Thương Thuật", tick);
            CompleteGoal(goal.NpcId);
        }
    }

    private void ExecutePacifyBanditsStep(
        NpcDef npc,
        NpcActivityDef act,
        NpcGoalDef goal,
        string citName,
        string formattedDate,
        long tick,
        IRng rng)
    {
        if (goal.CurrentStep == 1)
        {
            var actionText = $"Trinh sát hang ổ phỉ khấu quanh {citName}";
            var headline = $"{npc.NameVi} dò la tung tích sơn tặc tại ngoại thành {citName}";
            var detail = $"{npc.NameVi} thân chinh dò xét hẻm núi hiểm trở, nắm bắt quy luật phục kích của thảo khấu nhằm vạch kế hoạch tiễu trừ.";

            ApplyNpcGrowth(npc.Id, formattedDate, 1, 0, 0, 0, 0, 0, 0, 0);
            UpdateActivity(npc.Id, formattedDate, actionText, 0, 2, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "martial", headline, detail, "[Tiễu Phỉ 1/3: Trinh Sát]", tick);
            AdvanceGoal(goal, "Preparing", 2, 33, formattedDate);
        }
        else if (goal.CurrentStep == 2)
        {
            var actionText = $"Chiêu mộ dũng sĩ và bày trận phục kích tại {citName}";
            var headline = $"{npc.NameVi} tập hợp tráng đinh chuẩn bị tiễu trừ sơn trại tại {citName}";
            var detail = $"{npc.NameVi} mở kho chiêu đãi dũng sĩ, bố trí cung nỏ và kế hoạch khép chặt vòng vây tại cửa ải hiểm yếu.";

            UpdateActivity(npc.Id, formattedDate, actionText, 0, 3, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "martial", headline, detail, "[Tiễu Phỉ 2/3: Bày Trận]", tick);
            AdvanceGoal(goal, "Executing", 3, 66, formattedDate);
        }
        else
        {
            // Systemic ripple effect: Reduce unrest in city governance table
            if (_broker != null && _broker.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court != null)
            {
                court.AdjustCityStability(act.CurrentCitadelId, -3, +2);
            }

            ApplyNpcGrowth(npc.Id, formattedDate, 1, 1, 0, 0, 0, 0, 0, 0); // +1 War, +1 Ldr
            _db.Execute(
                "UPDATE so_npc_needs SET safety_need = MAX(10, safety_need - 25), fame_need = MAX(10, fame_need - 15), updated_date = @date WHERE npc_id = @id",
                new DbArgs().Add("@date", formattedDate).Add("@id", npc.Id));

            var bounty = 160 + rng.Next(60);
            var actionText = $"Đại phá sào huyệt đạo tặc phụ cận {citName}";
            var headline = $"{npc.NameVi} bình định giặc cỏ, giữ yên bờ cõi {citName}";
            var detail = $"{npc.NameVi} xuất kích thần tốc, trảm tướng cướp, giải cứu dân lành và thu hồi lương mã. Quan phủ ban thưởng {bounty} quan bạc.";

            UpdateActivity(npc.Id, formattedDate, actionText, bounty, 8, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "martial", headline, detail, $"[Tiễu Phỉ 3/3: Đại Thắng] +1 Võ Lực, +1 Thống Soái, +{bounty} Quan", tick);
            CompleteGoal(goal.NpcId);
        }
    }

    private void ExecuteMilitaryDrillStep(
        NpcDef npc,
        NpcActivityDef act,
        NpcGoalDef goal,
        string citName,
        string formattedDate,
        long tick,
        IRng rng)
    {
        if (goal.CurrentStep == 1)
        {
            ApplyNpcGrowth(npc.Id, formattedDate, 0, 1, 0, 0, 0, 0, 0, 0);
            UpdateActivity(npc.Id, formattedDate, $"Kiểm tra binh giáp và quân lương tại {citName}", 0, 2, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "martial",
                $"{npc.NameVi} kiểm tra quân bị trên thao trường {citName}",
                $"{npc.NameVi} nghiêm cẩn rà soát quân giới, chỉnh đốn kỷ luật và phát thưởng khích lệ binh sĩ.",
                "[Luyện Binh 1/3]", tick);
            AdvanceGoal(goal, "Preparing", 2, 33, formattedDate);
        }
        else if (goal.CurrentStep == 2)
        {
            UpdateActivity(npc.Id, formattedDate, $"Thao luyện chiến trận và sa bàn tại {citName}", 0, 3, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "martial",
                $"{npc.NameVi} chỉ huy diễn tập hợp đồng binh chủng tại {citName}",
                $"Khói lửa thao trường rực sáng, quân ngũ chuyển dịch trận đồ biến hóa khôn lường dưới cờ lệnh của {npc.NameVi}.",
                "[Luyện Binh 2/3]", tick);
            AdvanceGoal(goal, "Executing", 3, 66, formattedDate);
        }
        else
        {
            ApplyNpcGrowth(npc.Id, formattedDate, 1, 1, 0, 0, 0, 0, 0, 0);
            UpdateActivity(npc.Id, formattedDate, $"Hoàn tất tổng duyệt binh sĩ tại đại doanh {citName}", 80, 5, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "martial",
                $"{npc.NameVi} hoàn tất khóa thao luyện quân sĩ tại {citName}",
                $"Quân sĩ tề chỉnh uy nghiêm, sĩ khí ngút trời. Tướng sĩ đều đồng lòng thán phục tài lãnh đạo của {npc.NameVi}.",
                "[Luyện Binh 3/3: Hoàn Tất] +1 Thống Soái, +1 Võ Lực", tick);
            CompleteGoal(goal.NpcId);
        }
    }

    private void ExecuteCityGovernanceStep(
        NpcDef npc,
        NpcActivityDef act,
        NpcGoalDef goal,
        string citName,
        string formattedDate,
        long tick,
        IRng rng)
    {
        if (goal.CurrentStep == 1)
        {
            ApplyNpcGrowth(npc.Id, formattedDate, 0, 0, 0, 1, 0, 0, 0, 0);
            UpdateActivity(npc.Id, formattedDate, $"Thị sát kho thóc và sổ sách hộ tịch {citName}", 0, 2, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "governance",
                $"{npc.NameVi} tra cứu hộ tịch và kho lương tại {citName}",
                $"{npc.NameVi} thanh tra nha môn, trừng trị lại dịch tham ô, bảo đảm nguồn dự trữ lương thảo đầy đủ.",
                "[Cai Trị 1/3: Thanh Tra]", tick);
            AdvanceGoal(goal, "Preparing", 2, 33, formattedDate);
        }
        else if (goal.CurrentStep == 2)
        {
            UpdateActivity(npc.Id, formattedDate, $"Đốc thúc tu sửa thành hào thủy lợi tại {citName}", 0, 3, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "governance",
                $"{npc.NameVi} chỉ đạo gia cố đê điều và tường thành {citName}",
                $"Bách tính hăng hái góp công sửa sang công sự phòng thủ và kênh mương, chuẩn bị ứng phó thiên tai binh biến.",
                "[Cai Trị 2/3: Hưng Công]", tick);
            AdvanceGoal(goal, "Executing", 3, 66, formattedDate);
        }
        else
        {
            if (_broker != null && _broker.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court != null)
            {
                court.AdjustCityStability(act.CurrentCitadelId, -3, +3);
            }

            ApplyNpcGrowth(npc.Id, formattedDate, 0, 1, 0, 1, 0, 0, 0, 0); // +1 Pol, +1 Ldr
            UpdateActivity(npc.Id, formattedDate, $"An định trăm họ và ban hành chính sách giảm tô tại {citName}", 120, 8, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "governance",
                $"{npc.NameVi} ban bố chính sách an dân tại {citName}",
                $"Giảm thuế khóa, khuyến khích canh nông, trăm họ yên vui ca ngợi đức độ của bậc phụ mẫu chi quan {npc.NameVi}.",
                "[Cai Trị 3/3: Thuần Hóa] +1 Chính Trị, +1 Thống Soái", tick);
            CompleteGoal(goal.NpcId);
        }
    }

    private void ExecuteSeekPatronStep(
        NpcDef npc,
        NpcActivityDef act,
        NpcGoalDef goal,
        string citName,
        string formattedDate,
        long tick,
        IRng rng)
    {
        var targetCit = goal.TargetCitadelId.HasValue ? _map.Find(goal.TargetCitadelId.Value) : null;
        var targetName = targetCit?.NameVi ?? "Danh thành";

        if (goal.CurrentStep == 1)
        {
            ApplyNpcGrowth(npc.Id, formattedDate, 0, 0, 1, 0, 0, 0, 0, 0);
            UpdateActivity(npc.Id, formattedDate, $"Soạn thảo bình thiên hạ sách tại {citName}", 0, 2, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "scholarly",
                $"{npc.NameVi} trước tác luận mưu tại thư phòng {citName}",
                $"{npc.NameVi} ngày đêm nghiên cứu thế cục phân tranh, phác thảo sách lược định quốc an dân chờ thời cơ cống hiến.",
                "[Cầu Minh Chủ 1/3: Luận Kế]", tick);
            AdvanceGoal(goal, "Preparing", 2, 33, formattedDate);
        }
        else if (goal.CurrentStep == 2)
        {
            if (goal.TargetCitadelId.HasValue && goal.TargetCitadelId.Value != act.CurrentCitadelId)
            {
                _db.Execute(
                    "UPDATE so_npc_activity SET current_citadel_id = @newCit, updated_date = @date WHERE npc_id = @id",
                    new DbArgs().Add("@newCit", goal.TargetCitadelId.Value).Add("@date", formattedDate).Add("@id", npc.Id));
                _registry.UpdateCitadel(npc.Id, goal.TargetCitadelId.Value);
            }

            UpdateActivity(npc.Id, formattedDate, $"Lên đường đến {targetName} yết kiến các bậc quân vương", 0, 3, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, goal.TargetCitadelId ?? act.CurrentCitadelId, targetName, "travel",
                $"{npc.NameVi} cất bước du hành đến {targetName}",
                $"{npc.NameVi} vượt ngàn dặm mang theo trước tác hướng về {targetName}, mong mỏi hội kiến minh chúa tri âm.",
                "[Cầu Minh Chủ 2/3: Hành Trình]", tick);
            AdvanceGoal(goal, "Executing", 3, 66, formattedDate);
        }
        else
        {
            ApplyNpcGrowth(npc.Id, formattedDate, 0, 0, 1, 1, 0, 0, 0, 0); // +1 Int, +1 Pol
            _db.Execute(
                "UPDATE so_npc_needs SET power_need = MAX(10, power_need - 20), fame_need = MAX(10, fame_need - 15), updated_date = @date WHERE npc_id = @id",
                new DbArgs().Add("@date", formattedDate).Add("@id", npc.Id));

            UpdateActivity(npc.Id, formattedDate, $"Đàm đạo sách lược cùng các danh sĩ tại phủ viện {targetName}", 100, 10, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, goal.TargetCitadelId ?? act.CurrentCitadelId, targetName, "scholarly",
                $"{npc.NameVi} biện luận kế sách tại {targetName}",
                $"Lời lẽ sắc bén, mưu lược sâu xa khiến văn võ bá quan đều kính nể. Danh tiếng của {npc.NameVi} vang dội khắp vùng.",
                "[Cầu Minh Chủ 3/3: Danh Vang] +1 Trí Tuệ, +1 Chính Trị, +10 Uy Danh", tick);
            CompleteGoal(goal.NpcId);
        }
    }

    private void ExecuteFleeUnrestStep(
        NpcDef npc,
        NpcActivityDef act,
        NpcGoalDef goal,
        string citName,
        string formattedDate,
        long tick,
        IRng rng)
    {
        var targetCit = goal.TargetCitadelId.HasValue ? _map.Find(goal.TargetCitadelId.Value) : null;
        var targetName = targetCit?.NameVi ?? "Vùng an toàn";

        if (goal.CurrentStep == 1)
        {
            ApplyNpcGrowth(npc.Id, formattedDate, 0, 0, 0, 0, 0, 0, 1, 0);
            UpdateActivity(npc.Id, formattedDate, $"Thu xếp gia sản chuẩn bị lánh nạn khỏi {citName}", 0, 1, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "travel",
                $"{npc.NameVi} thu dọn hành lý lánh nạn chiến sự tại {citName}",
                $"Bất ổn trị an và khói lửa tràn về, {npc.NameVi} quyết định thu vén sản nghiệp, dặn dò gia nhân chuẩn bị rời bỏ cố hương.",
                "[Tị Nạn 1/3: Chuẩn Bị]", tick);
            AdvanceGoal(goal, "Preparing", 2, 33, formattedDate);
        }
        else if (goal.CurrentStep == 2)
        {
            if (goal.TargetCitadelId.HasValue && goal.TargetCitadelId.Value != act.CurrentCitadelId)
            {
                _db.Execute(
                    "UPDATE so_npc_activity SET current_citadel_id = @newCit, updated_date = @date WHERE npc_id = @id",
                    new DbArgs().Add("@newCit", goal.TargetCitadelId.Value).Add("@date", formattedDate).Add("@id", npc.Id));
                _registry.UpdateCitadel(npc.Id, goal.TargetCitadelId.Value);
            }

            UpdateActivity(npc.Id, formattedDate, $"Vượt quan ải tị nạn sang {targetName}", 0, 1, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, goal.TargetCitadelId ?? act.CurrentCitadelId, targetName, "travel",
                $"{npc.NameVi} vượt biên cương đến {targetName} tị nạn",
                $"{npc.NameVi} dẫn theo gia quyến qua nhiều hiểm trở, bình an đặt chân đến địa giới {targetName}.",
                "[Tị Nạn 2/3: Chuyển Dời]", tick);
            AdvanceGoal(goal, "Executing", 3, 66, formattedDate);
        }
        else
        {
            _db.Execute(
                "UPDATE so_npc_needs SET safety_need = 30, updated_date = @date WHERE npc_id = @id",
                new DbArgs().Add("@date", formattedDate).Add("@id", npc.Id));

            UpdateActivity(npc.Id, formattedDate, $"An cư lập nghiệp tại miền đất mới {targetName}", 0, 3, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, goal.TargetCitadelId ?? act.CurrentCitadelId, targetName, "travel",
                $"{npc.NameVi} an cư lạc nghiệp tại {targetName}",
                $"Tìm được nơi đất lành chim đậu, {npc.NameVi} dựng nhà mở quán, bắt đầu một cuộc sống thái bình mới giữa thời loạn.",
                "[Tị Nạn 3/3: Định Cư]", tick);
            CompleteGoal(goal.NpcId);
        }
    }

    private void ExecuteDefaultLifeStep(
        NpcDef npc,
        NpcActivityDef act,
        NpcGoalDef goal,
        string citName,
        string formattedDate,
        long tick,
        IRng rng)
    {
        if (goal.CurrentStep == 1)
        {
            ApplyNpcGrowth(npc.Id, formattedDate, 0, 0, 0, 0, 0, 0, 0, 1);
            UpdateActivity(npc.Id, formattedDate, $"Du ngoạn thưởng ngoạn phong cảnh tại {citName}", 0, 1, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "scholarly",
                $"{npc.NameVi} ngao du sơn thủy quanh {citName}",
                $"{npc.NameVi} dạo bước danh lam thắng cảnh, tìm hiểu phong thổ tập quán dân gian.",
                "[Hành Trình 1/2]", tick);
            AdvanceGoal(goal, "Executing", 2, 50, formattedDate);
        }
        else
        {
            ApplyNpcGrowth(npc.Id, formattedDate, 0, 0, 0, 0, 0, 0, 0, 1); // +1 CRA
            UpdateActivity(npc.Id, formattedDate, $"Hành hiệp trượng nghĩa và hái thuốc cứu người tại {citName}", 60, 4, act.CurrentWealth);
            LogEvent(formattedDate, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "scholarly",
                $"{npc.NameVi} giúp đỡ người nghèo tại {citName}",
                $"{npc.NameVi} bốc thuốc cứu người khốn khó, được bá tánh khen ngợi là bậc nghĩa sĩ.",
                "[Hành Trình 2/2] +1 Kỹ Nghệ", tick);
            CompleteGoal(goal.NpcId);
        }
    }

    private void AdvanceGoal(NpcGoalDef goal, string nextStage, int nextStep, int progress, string date)
    {
        _db.Execute(
            "UPDATE so_npc_goal SET stage = @stage, current_step = @step, progress = @prog, updated_date = @date WHERE npc_id = @id",
            new DbArgs()
                .Add("@stage", nextStage)
                .Add("@step", nextStep)
                .Add("@prog", progress)
                .Add("@date", date)
                .Add("@id", goal.NpcId));
    }

    private void CompleteGoal(long npcId)
    {
        _db.Execute("DELETE FROM so_npc_goal WHERE npc_id = @id", DbArgs.One("@id", npcId));
    }
}
