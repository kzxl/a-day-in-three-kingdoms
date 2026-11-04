using SimT.Contracts.Data;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed class SocietyService : ISocietyChronicle
{
    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly INpcRegistry _registry;
    private readonly IWorldMap _map;
    private readonly ILog _log;

    public SocietyService(
        IDbSession db,
        IGameClock clock,
        INpcRegistry registry,
        IWorldMap map,
        ILog log)
    {
        _db = db;
        _clock = clock;
        _registry = registry;
        _map = map;
        _log = log;

        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        var count = _db.ScalarLong("SELECT COUNT(*) FROM so_npc_activity");
        if (count > 0)
        {
            return;
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            foreach (var npc in _registry.All)
            {
                var a = npc.Attributes;
                var focus = DetermineFocusPath(a);
                var wealth = InitialWealth(focus);
                var initialAction = InitialAction(focus);

                _db.Execute(
                    "INSERT INTO so_npc_activity (npc_id, current_citadel_id, current_wealth, focus_path, current_action, stamina, reputation, updated_date) " +
                    "VALUES (@npcId, @citadelId, @wealth, @focus, @action, 100, 50, @date)",
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@citadelId", npc.CitadelId)
                        .Add("@wealth", wealth)
                        .Add("@focus", focus)
                        .Add("@action", initialAction)
                        .Add("@date", formattedDate));

                _db.Execute(
                    "INSERT INTO so_npc_stat_delta (npc_id, war_delta, ldr_delta, int_delta, pol_delta, dip_delta, com_delta, log_delta, cra_delta, updated_date) " +
                    "VALUES (@npcId, 0, 0, 0, 0, 0, 0, 0, 0, @date)",
                    new DbArgs()
                        .Add("@npcId", npc.Id)
                        .Add("@date", formattedDate));
            }

            // Create inaugural entries for prominent heroes
            var heroes = _registry.All.Where(n => n.Attributes.War >= 92 || n.Attributes.Int >= 92 || n.Attributes.Com >= 85).Take(12);
            foreach (var h in heroes)
            {
                var cit = _map.Find(h.CitadelId);
                var citName = cit?.NameVi ?? "Thiên hạ";
                _db.Execute(
                    "INSERT INTO so_journey_log (date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact, created_tick) " +
                    "VALUES (@date, @npcId, @name, @citId, @citName, 'scholarly', @headline, @detail, 'Khởi đầu', @tick)",
                    new DbArgs()
                        .Add("@date", formattedDate)
                        .Add("@npcId", h.Id)
                        .Add("@name", h.NameVi)
                        .Add("@citId", h.CitadelId)
                        .Add("@citName", citName)
                        .Add("@headline", $"{h.NameVi} tọa trấn tại {citName}")
                        .Add("@detail", $"{h.NameVi} ({h.Role}) ngụ tại {citName}, danh vang tứ hải, anh hùng hào kiệt đều muốn kết giao.")
                        .Add("@tick", today.DayIndex));
            }
        });

        _log.Info($"Society engine initialized with {_registry.All.Count} autonomous NPCs.");
    }

    public void OnMonthClosed(GameDate date)
    {
        SimulateTurn(date);
    }

    public void SimulateTurn(GameDate date)
    {
        var formattedDate = GameDateCodec.Format(date);
        var tick = date.DayIndex;

        var activities = AllActivities();
        var npcsById = _registry.All.ToDictionary(n => n.Id);

        _db.Transaction(() =>
        {
            foreach (var act in activities)
            {
                if (!npcsById.TryGetValue(act.NpcId, out var npc))
                {
                    continue;
                }

                // Deterministic pseudo-random seed per turn per NPC
                var seed = (uint)(date.DayIndex * 397 ^ (int)npc.Id);
                var rng = new SimpleRng(seed);

                // 50% chance of notable activity each month
                if (rng.Next(100) >= 50)
                {
                    continue;
                }

                var cit = _map.Find(act.CurrentCitadelId);
                var citName = cit?.NameVi ?? "Châu quận";

                // 15% chance to travel to a connected neighbour citadel
                if (rng.Next(100) < 15)
                {
                    var neighbours = _map.RouteNeighbours(act.CurrentCitadelId);
                    if (neighbours.Count > 0)
                    {
                        var targetCitId = neighbours[rng.Next(neighbours.Count)];
                        var targetCit = _map.Find(targetCitId);
                        var targetName = targetCit?.NameVi ?? "Phương xa";

                        var travelPurpose = act.FocusPath switch
                        {
                            "martial" => "chiêu tập dũng sĩ và tìm đối thủ luận kiếm",
                            "merchant" => "áp tải hàng hóa và mở rộng chiếm lĩnh thương trường",
                            "strategist" => "thị sát địa hình và hội kiến hiền tài",
                            "governor" => "kinh lý châu huyện và tuần sát dân tình",
                            _ => "du ngoạn danh sơn và hái thuốc cứu người"
                        };

                        var headline = $"{npc.NameVi} rời {citName} sang {targetName}";
                        var detail = $"{npc.NameVi} thu xếp hành trang rời {citName}, vượt dặm trường sang {targetName} nhằm {travelPurpose}.";
                        var actionText = $"Đang lưu trú tại {targetName}";

                        _db.Execute(
                            "UPDATE so_npc_activity SET current_citadel_id = @newCit, current_action = @action, updated_date = @date WHERE npc_id = @id",
                            new DbArgs()
                                .Add("@newCit", targetCitId)
                                .Add("@action", actionText)
                                .Add("@date", formattedDate)
                                .Add("@id", npc.Id));

                        _db.Execute(
                            "INSERT INTO so_journey_log (date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact, created_tick) " +
                            "VALUES (@date, @npcId, @name, @citId, @citName, 'travel', @headline, @detail, 'Di chuyển', @tick)",
                            new DbArgs()
                                .Add("@date", formattedDate)
                                .Add("@npcId", npc.Id)
                                .Add("@name", npc.NameVi)
                                .Add("@citId", targetCitId)
                                .Add("@citName", targetName)
                                .Add("@headline", headline)
                                .Add("@detail", detail)
                                .Add("@tick", tick));

                        continue;
                    }
                }

                // Path-specific actions
                switch (act.FocusPath)
                {
                    case "martial":
                        ExecuteMartialAction(npc, act, citName, formattedDate, tick, rng);
                        break;
                    case "merchant":
                        ExecuteMerchantAction(npc, act, citName, formattedDate, tick, rng);
                        break;
                    case "strategist":
                        ExecuteStrategistAction(npc, act, citName, formattedDate, tick, rng);
                        break;
                    case "governor":
                        ExecuteGovernorAction(npc, act, citName, formattedDate, tick, rng);
                        break;
                    default:
                        ExecuteWandererAction(npc, act, citName, formattedDate, tick, rng);
                        break;
                }
            }
        });

        _log.Info($"Society monthly simulation executed for date {formattedDate}.");
    }

    private void ExecuteMartialAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, SimpleRng rng)
    {
        var roll = rng.Next(4);
        var skillTag = npc.Skills.Count > 0 ? $"[{npc.Skills[0].NameVi}] " : "";
        string headline, detail, impact, actionText;
        int deltaWar = 0, deltaLdr = 0, repGain = 0, goldGain = 0;

        switch (roll)
        {
            case 0:
                headline = $"{skillTag}{npc.NameVi} tiễu trừ sơn tặc tại ngoại thành {citName}";
                detail = $"{npc.NameVi} dẫn bộ hạ xuất kích đánh tan sào huyệt thổ phỉ hẻm núi, thu giữ quân giới và ban thưởng cho dân làng.";
                deltaWar = 1;
                repGain = 8;
                goldGain = 180;
                impact = "+1 Võ Lực, +180 Quan";
                actionText = "Vừa đắc thắng trở về sau chuyến tiễu phỉ";
                break;
            case 1:
                headline = $"{skillTag}{npc.NameVi} thao luyện binh sĩ trên thao trường {citName}";
                detail = $"Trời đông giá rét nhưng hiệu lệnh thao trường của {npc.NameVi} vang dội, quân sĩ đồng lòng tề chỉnh.";
                deltaLdr = 1;
                repGain = 5;
                impact = "+1 Thống Soái, +5 Uy danh";
                actionText = "Chỉ huy huấn luyện quân sĩ tại đại doanh";
                break;
            case 2:
                headline = $"{skillTag}{npc.NameVi} tỷ võ luận kiếm tại võ đài {citName}";
                detail = $"{npc.NameVi} luận bàn võ nghệ cùng các hảo thủ trong vùng, quyền cước tuyệt luân khiến quần hùng thán phục.";
                deltaWar = 1;
                repGain = 6;
                impact = "+1 Võ Lực, +6 Uy danh";
                actionText = "Tọa đàm võ học cùng các danh sĩ";
                break;
            default:
                headline = $"{skillTag}{npc.NameVi} tuần tra thành phòng {citName}";
                detail = $"{npc.NameVi} đốc thúc quân phòng thủ canh gác nghiêm ngặt, giữ yên trị an nơi biên ải.";
                deltaLdr = 1;
                goldGain = 80;
                impact = "+1 Thống Soái, +80 Quan";
                actionText = "Đốc thúc phòng bị và kiểm tra nha môn";
                break;
        }

        ApplyNpcGrowth(npc.Id, date, deltaWar, deltaLdr, 0, 0, 0, 0, 0, 0);
        UpdateActivity(npc.Id, date, actionText, goldGain, repGain, act.CurrentWealth);
        LogEvent(date, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "martial", headline, detail, impact, tick);
    }

    private void ExecuteMerchantAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, SimpleRng rng)
    {
        var roll = rng.Next(4);
        string headline, detail, impact, actionText;
        int deltaCom = 0, deltaLog = 0, repGain = 0, goldDelta = 0;

        switch (roll)
        {
            case 0:
                headline = $"{npc.NameVi} giao thương đắc lợi tại thương hội {citName}";
                detail = $"{npc.NameVi} nắm bắt thời cơ mua rẻ bán đắt mặt hàng tơ lụa và trà quý, thu về khoản ngân lượng hậu hĩ.";
                deltaCom = 1;
                goldDelta = 260;
                impact = "+1 Thương Mại, +260 Quan";
                actionText = "Kiểm đếm doanh thu và tính toán sổ sách";
                break;
            case 1:
                headline = $"{npc.NameVi} quyên lương phát chẩn cho nạn dân {citName}";
                detail = $"{npc.NameVi} mở kho thóc tư gia, lập đàn chẩn tế cho hàng trăm lưu dân đói khổ, bá tánh hết lời ca ngợi thiện tâm.";
                goldDelta = -150;
                repGain = 15;
                impact = "+15 Uy danh, -150 Quan thiện nguyện";
                actionText = "Phát chẩn lương thực tại cổng thành";
                break;
            case 2:
                headline = $"{npc.NameVi} khai trương chi điếm mới tại chợ {citName}";
                detail = $"{npc.NameVi} xuất ngân lượng thuê mặt bằng sầm uất tại phố đông, thiết lập kênh tiêu thụ kho tàng mới.";
                deltaCom = 1;
                goldDelta = -180;
                repGain = 5;
                impact = "+1 Thương Mại, -180 Quan vốn";
                actionText = "Khai trương sạp buôn mới";
                break;
            default:
                headline = $"{npc.NameVi} điều phối lương thảo và kho bãi {citName}";
                detail = $"{npc.NameVi} áp dụng phương pháp cất trữ mới chống ẩm mốc, nâng cao hiệu suất kho tàng.";
                deltaLog = 1;
                goldDelta = 100;
                impact = "+1 Hậu Cần, +100 Quan";
                actionText = "Kiểm tra hệ thống kho bãi";
                break;
        }

        ApplyNpcGrowth(npc.Id, date, 0, 0, 0, 0, 0, deltaCom, deltaLog, 0);
        UpdateActivity(npc.Id, date, actionText, goldDelta, repGain, act.CurrentWealth);
        LogEvent(date, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "commerce", headline, detail, impact, tick);
    }

    private void ExecuteStrategistAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, SimpleRng rng)
    {
        var roll = rng.Next(3);
        string headline, detail, impact, actionText;
        int deltaInt = 0, deltaDip = 0, repGain = 0, goldDelta = 0;

        switch (roll)
        {
            case 0:
                headline = $"{npc.NameVi} nghiên cứu cổ thư binh pháp tại {citName}";
                detail = $"{npc.NameVi} thâu đêm chong đèn đọc Binh Thư Tôn Tử, thấu suốt cội nguồn mưu mẹo quyền biến.";
                deltaInt = 1;
                repGain = 4;
                impact = "+1 Trí Tuệ, +4 Uy danh";
                actionText = "Nghiên cứu cổ thư mưu lược";
                break;
            case 1:
                headline = $"{npc.NameVi} luận đàm đại thế thiên hạ tại trà đình {citName}";
                detail = $"{npc.NameVi} cùng các bậc danh sĩ đối ẩm, phân tích thế chân vạc và phong vân biến ảo thời cục.";
                deltaDip = 1;
                repGain = 7;
                impact = "+1 Ngoại Giao, +7 Uy danh";
                actionText = "Luận đàm thiên hạ thế sự";
                break;
            default:
                headline = $"{npc.NameVi} hiến kế lược an dân cho phủ nha {citName}";
                detail = $"Kế sách của {npc.NameVi} được quan viên khen ngợi, ban tặng bạc thưởng và trọng dụng.";
                deltaInt = 1;
                goldDelta = 120;
                repGain = 6;
                impact = "+1 Trí Tuệ, +120 Quan";
                actionText = "Soạn thảo sách lược kinh tế mưu sinh";
                break;
        }

        ApplyNpcGrowth(npc.Id, date, 0, 0, deltaInt, 0, deltaDip, 0, 0, 0);
        UpdateActivity(npc.Id, date, actionText, goldDelta, repGain, act.CurrentWealth);
        LogEvent(date, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "scholarly", headline, detail, impact, tick);
    }

    private void ExecuteGovernorAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, SimpleRng rng)
    {
        var roll = rng.Next(3);
        string headline, detail, impact, actionText;
        int deltaPol = 0, deltaLdr = 0, repGain = 0, goldDelta = 0;

        switch (roll)
        {
            case 0:
                headline = $"{npc.NameVi} đôn đốc đắp đê khuyến nông tại {citName}";
                detail = $"{npc.NameVi} xuống tận ruộng đồng chỉ đạo khơi thông mương máng, khuyến khích nông dân cấy lúa đúng vụ.";
                deltaPol = 1;
                goldDelta = 140;
                repGain = 8;
                impact = "+1 Chính Trị, +140 Quan";
                actionText = "Thị sát nông điền ngoại thành";
                break;
            case 1:
                headline = $"{npc.NameVi} phân xử công minh các vụ kiện tại {citName}";
                detail = $"{npc.NameVi} thụ lý án tích đọng nhiều năm, xét xử sáng suốt thấu tình đạt lý, dân tình tâm phục khẩu phục.";
                deltaPol = 1;
                repGain = 12;
                impact = "+1 Chính Trị, +12 Uy danh";
                actionText = "Phán xử công án nơi công đường";
                break;
            default:
                headline = $"{npc.NameVi} tu bổ tường thành và hào sâu {citName}";
                detail = $"{npc.NameVi} trưng dụng phu phen tu sửa ải quan, dựng tháp canh kiên cố bảo vệ bá tánh.";
                deltaLdr = 1;
                repGain = 6;
                impact = "+1 Thống Soái, +6 Uy danh";
                actionText = "Chỉ huy gia cố công sự phòng thủ";
                break;
        }

        ApplyNpcGrowth(npc.Id, date, 0, deltaLdr, 0, deltaPol, 0, 0, 0, 0);
        UpdateActivity(npc.Id, date, actionText, goldDelta, repGain, act.CurrentWealth);
        LogEvent(date, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "governance", headline, detail, impact, tick);
    }

    private void ExecuteWandererAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, SimpleRng rng)
    {
        var roll = rng.Next(2);
        string headline, detail, impact, actionText;
        int deltaCra = 0, repGain = 0, goldDelta = 0;

        switch (roll)
        {
            case 0:
                headline = $"{npc.NameVi} hái thuốc cứu người tại thôn xóm {citName}";
                detail = $"{npc.NameVi} lên non hái thuốc chế phương tễ cứu sống người bệnh hiểm nghèo, nghĩa cử lưu truyền muôn phương.";
                deltaCra = 1;
                repGain = 10;
                goldDelta = 70;
                impact = "+1 Kỹ Nghệ, +70 Quan";
                actionText = "Bốc thuốc chữa bệnh cho người nghèo";
                break;
            default:
                headline = $"{npc.NameVi} rèn đúc bảo kiếm tại danh quán {citName}";
                detail = $"{npc.NameVi} chọn thép tốt tôi rèn thần binh, thanh kiếm sắc bén chém sắt như chém bùn.";
                deltaCra = 1;
                goldDelta = 110;
                impact = "+1 Kỹ Nghệ, +110 Quan";
                actionText = "Chế tác khí cụ và điêu khắc";
                break;
        }

        ApplyNpcGrowth(npc.Id, date, 0, 0, 0, 0, 0, 0, 0, deltaCra);
        UpdateActivity(npc.Id, date, actionText, goldDelta, repGain, act.CurrentWealth);
        LogEvent(date, npc.Id, npc.NameVi, act.CurrentCitadelId, citName, "scholarly", headline, detail, impact, tick);
    }

    private void ApplyNpcGrowth(long npcId, string date, int war, int ldr, int @int, int pol, int dip, int com, int log, int cra)
    {
        if (war == 0 && ldr == 0 && @int == 0 && pol == 0 && dip == 0 && com == 0 && log == 0 && cra == 0)
        {
            return;
        }

        _db.Execute(
            "UPDATE so_npc_stat_delta SET " +
            "war_delta = war_delta + @war, " +
            "ldr_delta = ldr_delta + @ldr, " +
            "int_delta = int_delta + @int, " +
            "pol_delta = pol_delta + @pol, " +
            "dip_delta = dip_delta + @dip, " +
            "com_delta = com_delta + @com, " +
            "log_delta = log_delta + @log, " +
            "cra_delta = cra_delta + @cra, " +
            "updated_date = @date WHERE npc_id = @id",
            new DbArgs()
                .Add("@war", war)
                .Add("@ldr", ldr)
                .Add("@int", @int)
                .Add("@pol", pol)
                .Add("@dip", dip)
                .Add("@com", com)
                .Add("@log", log)
                .Add("@cra", cra)
                .Add("@date", date)
                .Add("@id", npcId));
    }

    private void UpdateActivity(long npcId, string date, string action, int goldDelta, int repDelta, long currentWealth)
    {
        var newWealth = Math.Max(0, currentWealth + goldDelta);

        _db.Execute(
            "UPDATE so_npc_activity SET " +
            "current_wealth = @wealth, " +
            "current_action = @action, " +
            "reputation = reputation + @rep, " +
            "updated_date = @date WHERE npc_id = @id",
            new DbArgs()
                .Add("@wealth", newWealth)
                .Add("@action", action)
                .Add("@rep", repDelta)
                .Add("@date", date)
                .Add("@id", npcId));

        if (goldDelta != 0)
        {
            var direction = goldDelta > 0 ? "IN" : "OUT";
            var amount = Math.Abs(goldDelta);
            _db.Execute(
                "INSERT INTO so_ledger (npc_id, date, direction, amount, reason, balance_after) " +
                "VALUES (@npcId, @date, @dir, @amount, @reason, @bal)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", date)
                    .Add("@dir", direction)
                    .Add("@amount", amount)
                    .Add("@reason", action)
                    .Add("@bal", newWealth));
        }
    }

    private void LogEvent(
        string date,
        long npcId,
        string nameVi,
        long citadelId,
        string citadelNameVi,
        string eventKind,
        string headline,
        string detail,
        string impact,
        long tick)
    {
        _db.Execute(
            "INSERT INTO so_journey_log (date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact, created_tick) " +
            "VALUES (@date, @npcId, @name, @citId, @citName, @kind, @headline, @detail, @impact, @tick)",
            new DbArgs()
                .Add("@date", date)
                .Add("@npcId", npcId)
                .Add("@name", nameVi)
                .Add("@citId", citadelId)
                .Add("@citName", citadelNameVi)
                .Add("@kind", eventKind)
                .Add("@headline", headline)
                .Add("@detail", detail)
                .Add("@impact", impact)
                .Add("@tick", tick));
    }

    public IReadOnlyList<SocietyLogEntry> RecentEvents(int limit = 50, string? category = null, long? citadelId = null)
    {
        var sql = "SELECT id, date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact " +
                  "FROM so_journey_log ";

        var clauses = new List<string>();
        var args = new DbArgs();

        if (!string.IsNullOrWhiteSpace(category))
        {
            clauses.Add("event_kind = @kind");
            args.Add("@kind", category);
        }

        if (citadelId.HasValue)
        {
            clauses.Add("citadel_id = @citId");
            args.Add("@citId", citadelId.Value);
        }

        if (clauses.Count > 0)
        {
            sql += "WHERE " + string.Join(" AND ", clauses) + " ";
        }

        sql += "ORDER BY id DESC LIMIT " + Math.Clamp(limit, 1, 200);

        return _db.Query(sql, record => new SocietyLogEntry(
            record.GetInt64(0),
            GameDateCodec.Parse(record.GetString(1)),
            record.GetInt64(2),
            record.GetString(3),
            record.GetInt64(4),
            record.GetString(5),
            record.GetString(6),
            record.GetString(7),
            record.GetString(8),
            record.GetStringOrNull(9)),
            args);
    }

    public NpcActivityDef? GetNpcActivity(long npcId)
    {
        return _db.QueryOne(
            "SELECT npc_id, current_citadel_id, current_wealth, focus_path, current_action, stamina, reputation, updated_date " +
            "FROM so_npc_activity WHERE npc_id = @id",
            record => new NpcActivityDef(
                record.GetInt64(0),
                record.GetInt64(1),
                record.GetInt64(2),
                record.GetString(3),
                record.GetString(4),
                record.GetInt32(5),
                record.GetInt32(6),
                GameDateCodec.Parse(record.GetString(7))),
            DbArgs.One("@id", npcId));
    }

    public IReadOnlyList<NpcActivityDef> AllActivities()
    {
        return _db.Query(
            "SELECT npc_id, current_citadel_id, current_wealth, focus_path, current_action, stamina, reputation, updated_date " +
            "FROM so_npc_activity",
            record => new NpcActivityDef(
                record.GetInt64(0),
                record.GetInt64(1),
                record.GetInt64(2),
                record.GetString(3),
                record.GetString(4),
                record.GetInt32(5),
                record.GetInt32(6),
                GameDateCodec.Parse(record.GetString(7))));
    }

    public NpcStatDelta GetStatDeltas(long npcId)
    {
        var row = _db.QueryOne(
            "SELECT npc_id, war_delta, ldr_delta, int_delta, pol_delta, dip_delta, com_delta, log_delta, cra_delta, updated_date " +
            "FROM so_npc_stat_delta WHERE npc_id = @id",
            record => new NpcStatDelta(
                record.GetInt64(0),
                record.GetInt32(1),
                record.GetInt32(2),
                record.GetInt32(3),
                record.GetInt32(4),
                record.GetInt32(5),
                record.GetInt32(6),
                record.GetInt32(7),
                record.GetInt32(8),
                GameDateCodec.Parse(record.GetString(9))),
            DbArgs.One("@id", npcId));

        return row ?? NpcStatDelta.Empty(npcId, _clock.Today);
    }

    private static string DetermineFocusPath(CharacterAttributes a)
    {
        if (a.War >= 75 && a.War >= Math.Max(a.Com, Math.Max(a.Int, a.Pol)))
        {
            return "martial";
        }
        if (a.Com >= 70 && a.Com >= Math.Max(a.Int, a.Pol))
        {
            return "merchant";
        }
        if (a.Int >= 75 && a.Int >= a.Pol)
        {
            return "strategist";
        }
        if (a.Pol >= 70)
        {
            return "governor";
        }
        return "wanderer";
    }

    private static long InitialWealth(string focus) => focus switch
    {
        "merchant" => 2500,
        "governor" => 3000,
        "martial" => 1500,
        "strategist" => 1200,
        _ => 800
    };

    private static string InitialAction(string focus) => focus switch
    {
        "martial" => "Luyện tập thương thuật tại binh doanh",
        "merchant" => "Khảo sát thời giá tại chợ phiên",
        "strategist" => "Đọc sách nghiên cứu binh thư cổ kim",
        "governor" => "Xử lý công văn tại nha môn",
        _ => "Ngao du sơn thủy kết giao danh sĩ"
    };

    private sealed class SimpleRng
    {
        private uint _state;

        public SimpleRng(uint seed)
        {
            _state = seed == 0 ? 0x12345678 : seed;
        }

        public int Next(int max)
        {
            if (max <= 1) return 0;
            // Xorshift32
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)max);
        }
    }
}
