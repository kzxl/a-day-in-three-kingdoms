using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Logging;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService : ISocietyChronicle, IMerchantRivalry, IKarmaEngine
{
    public IMerchantRivalry Rivals => this;
    public IKarmaEngine Karma => this;
    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly INpcRegistry _registry;
    private readonly IWorldMap _map;
    private readonly IPlayerState _player;
    private readonly ILog _log;
    private readonly ICapabilityBroker? _broker;
    private readonly IRng? _kernelRng;
    private readonly long _campaignSeed;

    public SocietyService(
        IDbSession db,
        IGameClock clock,
        INpcRegistry registry,
        IWorldMap map,
        IPlayerState player,
        ILog log,
        ICapabilityBroker? broker = null,
        IRng? kernelRng = null,
        long campaignSeed = 0)
    {
        _db = db;
        _clock = clock;
        _registry = registry;
        _map = map;
        _player = player;
        _log = log;
        _broker = broker;
        _kernelRng = kernelRng;
        _campaignSeed = campaignSeed;

        EnsureInitialized();
    }

    /// <summary>
    /// Derives an independent, deterministic sub-stream IRng for a specific domain entity and date.
    /// Eliminates cross-NPC execution order bias while strictly binding to the Kernel campaign seed.
    /// </summary>
    private IRng CreateSubStreamRng(long entityId, long dayIndex, ulong domainSalt)
    {
        var baseSeed = _campaignSeed != 0 ? (ulong)_campaignSeed : (_kernelRng?.State ?? 0x123456789ABCDEFUL);
        var z = baseSeed ^ ((ulong)entityId * 0x9E3779B97F4A7C15UL) ^ ((ulong)dayIndex * 0xBF58476D1CE4E5B9UL) ^ domainSalt;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        var streamSeed = z ^ (z >> 31);
        return new SocietySubStreamRng(streamSeed);
    }

    public void EnsureInitialized()
    {
        EnsureActivityInitialized();
        EnsureMerchantRivalsInitialized();
        EnsureKarmaInitialized();
        EnsureHistoricalEventsInitialized();
        RefreshNpcLifetimes();
    }

    private void EnsureActivityInitialized()
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

        Dictionary<long, CityGovernanceView>? govByCitadel = null;
        if (_broker is not null && _broker.TryGet<IPoliticalCourt>(CapabilityIds.CourtOffice, out var court) && court is not null)
        {
            govByCitadel = court.Governances.ToDictionary(g => g.CitadelId);
        }

        IMilitaryCommand? military = null;
        if (_broker is not null && _broker.TryGet<IMilitaryCommand>(CapabilityIds.MilitaryCommand, out var mil))
        {
            military = mil;
        }

        HashSet<long>? swornBrotherIds = null;
        Dictionary<long, RetainerDef>? retainersByNpc = null;
        if (_broker is not null && _broker.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null)
        {
            swornBrotherIds = social.GetSwornBrothers().Select(s => s.NpcId).ToHashSet();
            retainersByNpc = social.GetRetainers().ToDictionary(r => r.NpcId);
        }

        _db.Transaction(() =>
        {
            foreach (var act in activities)
            {
                if (_newlyArrivedNpcIds.Remove(act.NpcId) || act.IsDeceased || !npcsById.TryGetValue(act.NpcId, out var npc) || npc.IsDeceasedFlag)
                {
                    continue;
                }

                // Check if NPC is bound to player retinue (sworn brother or personal companion retainer)
                var isSworn = swornBrotherIds != null && swornBrotherIds.Contains(npc.Id);
                var isRetainerCompanion = retainersByNpc != null && retainersByNpc.TryGetValue(npc.Id, out var ret) && ret.RoleAssigned is "bodyguard" or "advisor";
                var isCompanion = isSworn || isRetainerCompanion;

                if (isCompanion)
                {
                    // Companions accompany protagonist to their current location
                    var playerCityId = _player.CurrentCityId;
                    if (act.CurrentCitadelId != playerCityId)
                    {
                        var playerCit = _map.Find(playerCityId);
                        var targetName = playerCit?.NameVi ?? "Bên cạnh minh chủ";
                        var actionText = isSworn ? $"Đang hộ vệ nghĩa huynh đệ tại {targetName}" : $"Túc trực bên cạnh minh chủ tại {targetName}";

                        _db.Execute(
                            "UPDATE so_npc_activity SET current_citadel_id = @newCit, current_action = @action, updated_date = @date WHERE npc_id = @id",
                            new DbArgs()
                                .Add("@newCit", playerCityId)
                                .Add("@action", actionText)
                                .Add("@date", formattedDate)
                                .Add("@id", npc.Id));

                        _registry.UpdateCitadel(npc.Id, playerCityId);
                    }
                    continue;
                }

                // Deterministic pseudo-random seed per turn per NPC
                var rng = CreateSubStreamRng(npc.Id, (int)date.DayIndex, 0x50C1E71UL);

                // 50% chance of notable activity each month
                if (rng.Next(100) >= 50)
                {
                    continue;
                }

                var cit = _map.Find(act.CurrentCitadelId);
                var citName = cit?.NameVi ?? "Châu quận";

                // Faction rulers and regional governors stay to manage their state; others travel according to lifestyle
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

                var travelChance = isGovernorOrRuler ? 2 : (act.FocusPath switch
                {
                    "merchant" => 25,
                    "wanderer" => 20,
                    "martial" => 15,
                    "strategist" => 10,
                    _ => 15
                });

                // Migration pressure: high unrest or low route security pushes commoners and wanderers to flee
                var isUnrestMigration = false;
                var isSecurityMigration = false;
                if (!isGovernorOrRuler)
                {
                    if (currentUnrest >= 25)
                    {
                        travelChance += (currentUnrest - 20) * 2;
                        isUnrestMigration = true;
                    }
                    if (currentSecurity < 40)
                    {
                        travelChance += (45 - currentSecurity);
                        isSecurityMigration = true;
                    }
                }

                if (rng.Next(100) < travelChance)
                {
                    var neighbours = _map.RouteNeighbours(act.CurrentCitadelId);
                    if (neighbours.Count > 0)
                    {
                        long targetCitId;
                        if ((isUnrestMigration || isSecurityMigration) && (govByCitadel != null || military != null))
                        {
                            // Flee towards neighbouring citadel with lowest unrest, highest route security and strongest defense
                            targetCitId = neighbours
                                .OrderBy(nid => govByCitadel != null && govByCitadel.TryGetValue(nid, out var ng) ? ng.UnrestLevel : 20)
                                .ThenByDescending(nid => military != null ? military.GetCitadelSecurity(nid) : 50)
                                .ThenByDescending(nid => govByCitadel != null && govByCitadel.TryGetValue(nid, out var ng) ? ng.DefenseStrength : 50)
                                .First();
                        }
                        else if (act.FocusPath == "merchant")
                        {
                            // Merchants gravitate towards flourishing trade hubs
                            targetCitId = neighbours
                                .OrderByDescending(nid => _map.Find(nid)?.PriceFactorPermille ?? 1000)
                                .First();
                        }
                        else
                        {
                            targetCitId = neighbours[rng.Next(neighbours.Count)];
                        }

                        var targetCit = _map.Find(targetCitId);
                        var targetName = targetCit?.NameVi ?? "Phương xa";

                        string headline, detail, statImpact, actionText;

                        if (isUnrestMigration && isSecurityMigration)
                        {
                            headline = $"{npc.NameVi} di cư lánh loạn và giặc cỏ từ {citName} sang {targetName}";
                            detail = $"{npc.NameVi} thu xếp gia quyến rời {citName} do trị an suy đồi và phỉ khấu lộng hành, chuyển đến {targetName} an cư lạc nghiệp.";
                            actionText = $"Lánh nạn tại {targetName}";
                            statImpact = "Di cư tị nạn";
                        }
                        else if (isSecurityMigration)
                        {
                            headline = $"{npc.NameVi} lánh nạn đạo tặc từ {citName} sang {targetName}";
                            detail = $"{npc.NameVi} thu xếp hành trang rời khỏi {citName} do sơn tặc hoành hành, quan lộ nguy hiểm, tìm đến thành trì kiên cố {targetName} an toàn hơn.";
                            actionText = $"Tị nạn đạo tặc tại {targetName}";
                            statImpact = "Tránh giặc tị nạn";
                        }
                        else if (isUnrestMigration)
                        {
                            headline = $"{npc.NameVi} di cư lánh loạn từ {citName} sang {targetName}";
                            detail = $"{npc.NameVi} thu xếp gia quyến rời khỏi {citName} do trị an bất ổn và hiểm họa chiến tranh, tìm đến vùng đất thái bình {targetName} để an cư lập nghiệp.";
                            actionText = $"Di cư lánh loạn tại {targetName}";
                            statImpact = "Di cư lánh loạn";
                        }
                        else if (act.FocusPath == "merchant" && targetCit != null && cit != null && targetCit.PriceFactorPermille > cit.PriceFactorPermille)
                        {
                            headline = $"{npc.NameVi} chuyển dịch thương đoàn từ {citName} đến {targetName}";
                            detail = $"{npc.NameVi} mở rộng mạng lưới giao thương, dời điểm kinh doanh chủ lực sang {targetName} nhằm nắm bắt cơ hội thương nghiệp phồn hoa.";
                            actionText = $"Lập thương hội tại {targetName}";
                            statImpact = "Chuyển dịch thương hội";
                        }
                        else if (act.FocusPath == "strategist")
                        {
                            headline = $"{npc.NameVi} du học du thuyết từ {citName} sang {targetName}";
                            detail = $"{npc.NameVi} bái biệt các bằng hữu tại {citName}, cất bước sang {targetName} để hội kiến các bậc danh sĩ và tìm minh chủ phụng sự.";
                            actionText = $"Du thuyết tại {targetName}";
                            statImpact = "Du thuyết danh thành";
                        }
                        else
                        {
                            var travelPurpose = act.FocusPath switch
                            {
                                "martial" => "chiêu tập dũng sĩ và tìm đối thủ luận kiếm",
                                "merchant" => "áp tải hàng hóa và mở rộng chiếm lĩnh thương trường",
                                "strategist" => "thị sát địa hình và hội kiến hiền tài",
                                "governor" => "kinh lý châu huyện và tuần sát dân tình",
                                _ => "du ngoạn danh sơn và hái thuốc cứu người"
                            };

                            headline = $"{npc.NameVi} rời {citName} sang {targetName}";
                            detail = $"{npc.NameVi} thu xếp hành trang rời {citName}, vượt dặm trường sang {targetName} nhằm {travelPurpose}.";
                            actionText = $"Đang lưu trú tại {targetName}";
                            statImpact = "Ngao du thiên hạ";
                        }

                        _db.Execute(
                            "UPDATE so_npc_activity SET current_citadel_id = @newCit, current_action = @action, updated_date = @date WHERE npc_id = @id",
                            new DbArgs()
                                .Add("@newCit", targetCitId)
                                .Add("@action", actionText)
                                .Add("@date", formattedDate)
                                .Add("@id", npc.Id));

                        _registry.UpdateCitadel(npc.Id, targetCitId);

                        _db.Execute(
                            "INSERT INTO so_journey_log (date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact, created_tick) " +
                            "VALUES (@date, @npcId, @name, @citId, @citName, 'travel', @headline, @detail, @statImpact, @tick)",
                            new DbArgs()
                                .Add("@date", formattedDate)
                                .Add("@npcId", npc.Id)
                                .Add("@name", npc.NameVi)
                                .Add("@citId", targetCitId)
                                .Add("@citName", targetName)
                                .Add("@headline", headline)
                                .Add("@detail", detail)
                                .Add("@statImpact", statImpact)
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

            // Periodic replenishment: quarterly (month % 3 == 0) or whenever a citadel drops below 18 NPCs
            if (date.Month % 3 == 0 || AnyCitadelUnderpopulated())
            {
                ReplenishNpcsCore(date, formattedDate, tick, count: 1);
            }

            // Simulate AI Merchant Rivals progression and market competition
            SimulateMerchantTurnCore(date, formattedDate, tick);
        });

        _log.Info($"Society monthly simulation executed for date {formattedDate}.");
    }

    private void ExecuteMartialAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, IRng rng)
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

    private void ExecuteMerchantAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, IRng rng)
    {
        var rival = GetRival(npc.Id);
        if (rival != null)
        {
            ExecuteRivalMerchantAction(npc, act, rival, citName, date, tick, rng);
            return;
        }

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

    private void ExecuteStrategistAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, IRng rng)
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

    private void ExecuteGovernorAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, IRng rng)
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

    private void ExecuteWandererAction(NpcDef npc, NpcActivityDef act, string citName, string date, long tick, IRng rng)
    {
        var lifeSkill = npc.Skills.FirstOrDefault(s => s.Code is "am_thuc" or "tam_duoc" or "dieu_ngu" or "thu_liep" or "che_tac" or "khai_khoang" or "det_may" or "u_men" or "luyen_dan");

        string headline, detail, impact, actionText;
        int deltaCra = 0, deltaWar = 0, repGain = 0, goldDelta = 0;

        if (lifeSkill != null)
        {
            switch (lifeSkill.Code)
            {
                case "am_thuc":
                case "u_men":
                    headline = $"[Ẩm Thực] {npc.NameVi} chế biến tiệc rượu mỹ vị danh chấn {citName}";
                    detail = $"{npc.NameVi} kết hợp ngũ vị điều hòa, chế biến đặc sản bồi bổ khí huyết khiến thực khách khắp nơi tấm tắc ngợi khen.";
                    deltaCra = 1;
                    repGain = 8;
                    goldDelta = 95;
                    impact = "+1 Kỹ Nghệ, +95 Quan";
                    actionText = "Nấu nướng và ủ men danh tửu";
                    break;

                case "tam_duoc":
                case "luyen_dan":
                    headline = $"[Dược Đạo] {npc.NameVi} tầm dược luyện đan cứu người tại {citName}";
                    detail = $"{npc.NameVi} tìm được thảo dược ngàn năm nơi rừng thẳm, tôi luyện đan dược bổ khí dưỡng huyết cứu chữa dân nghèo.";
                    deltaCra = 1;
                    repGain = 12;
                    goldDelta = 110;
                    impact = "+1 Kỹ Nghệ, +110 Quan";
                    actionText = "Hái thuốc và luyện đan cứu nhân độ thế";
                    break;

                case "che_tac":
                case "khai_khoang":
                    headline = $"[Chế Tác] {npc.NameVi} rèn đúc thần binh và công cụ tại lò rèn {citName}";
                    detail = $"{npc.NameVi} chọn thép tốt tôi rèn trăm mẻ, rèn nên khí cụ sắc bén bền chắc cung ứng cho thương lái.";
                    deltaCra = 1;
                    repGain = 9;
                    goldDelta = 125;
                    impact = "+1 Kỹ Nghệ, +125 Quan";
                    actionText = "Rèn khí cụ và tinh luyện khoáng sản";
                    break;

                case "dieu_ngu":
                    headline = $"[Điếu Ngư] {npc.NameVi} buông cần câu cá lớn trên sông tại {citName}";
                    detail = $"{npc.NameVi} kiên nhẫn đợi nước triều, vớt được loài ngư sản quý hiếm mang bán cho thương điếm.";
                    deltaCra = 1;
                    repGain = 6;
                    goldDelta = 80;
                    impact = "+1 Kỹ Nghệ, +80 Quan";
                    actionText = "Buông cần câu cá bên bến nước";
                    break;

                case "thu_liep":
                    headline = $"[Thú Liệp] {npc.NameVi} giương cung săn mãnh thú ngoại ô {citName}";
                    detail = $"{npc.NameVi} dấn thân vào chốn rừng thiêng nước độc, hạ gục ác thú thu hoạch da lông và thịt săn dồi dào.";
                    deltaWar = 1;
                    repGain = 10;
                    goldDelta = 115;
                    impact = "+1 Võ Lực, +115 Quan";
                    actionText = "Săn bắn thú rừng dã ngoại";
                    break;

                case "det_may":
                    headline = $"[Dệt May] {npc.NameVi} dệt gấm thêu hoa tại xưởng dệt {citName}";
                    detail = $"{npc.NameVi} khéo tay dệt nên tấm lụa óng ả mềm mại, các phú thương tranh nhau trả giá cao.";
                    deltaCra = 1;
                    repGain = 7;
                    goldDelta = 105;
                    impact = "+1 Kỹ Nghệ, +105 Quan";
                    actionText = "Canh cửi dệt lụa may y phục";
                    break;

                default:
                    headline = $"{npc.NameVi} chế tác khí nghệ thủ công tại {citName}";
                    detail = $"{npc.NameVi} gia công sản vật tinh xảo, giao dịch cùng phường thợ trong vùng.";
                    deltaCra = 1;
                    repGain = 6;
                    goldDelta = 75;
                    impact = "+1 Kỹ Nghệ, +75 Quan";
                    actionText = "Chế tác thủ công nghệ";
                    break;
            }
        }
        else
        {
            var roll = rng.Next(2);
            if (roll == 0)
            {
                headline = $"{npc.NameVi} hái thuốc cứu người tại thôn xóm {citName}";
                detail = $"{npc.NameVi} lên non hái thuốc chế phương tễ cứu sống người bệnh hiểm nghèo, nghĩa cử lưu truyền muôn phương.";
                deltaCra = 1;
                repGain = 10;
                goldDelta = 70;
                impact = "+1 Kỹ Nghệ, +70 Quan";
                actionText = "Bốc thuốc chữa bệnh cho người nghèo";
            }
            else
            {
                headline = $"{npc.NameVi} rèn đúc bảo kiếm tại danh quán {citName}";
                detail = $"{npc.NameVi} chọn thép tốt tôi rèn thần binh, thanh kiếm sắc bén chém sắt như chém bùn.";
                deltaCra = 1;
                goldDelta = 110;
                impact = "+1 Kỹ Nghệ, +110 Quan";
                actionText = "Chế tác khí cụ và điêu khắc";
            }
        }

        ApplyNpcGrowth(npc.Id, date, deltaWar, 0, 0, 0, 0, 0, 0, deltaCra);
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

    public void RecordEvent(GameDate date, long npcId, string npcNameVi, long citadelId, string headlineVi, string detailVi, string category = "travel", string statImpact = "")
    {
        var cit = _map.Find(citadelId);
        var citName = cit?.NameVi ?? "Châu quận";
        var formattedDate = GameDateCodec.Format(date);
        var validCategory = category is "martial" or "commerce" or "governance" or "scholarly" or "travel" ? category : "travel";
        LogEvent(formattedDate, npcId, npcNameVi, citadelId, citName, validCategory, headlineVi, detailVi, statImpact, date.DayIndex);
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
            record =>
            {
                var id = record.GetInt64(0);
                var action = record.GetString(4);
                return new NpcActivityDef(
                    id,
                    record.GetInt64(1),
                    record.GetInt64(2),
                    record.GetString(3),
                    action,
                    record.GetInt32(5),
                    record.GetInt32(6),
                    GameDateCodec.Parse(record.GetString(7)),
                    IsNpcDeceased(id, action),
                    GetNpcAllegiance(id));
            },
            DbArgs.One("@id", npcId));
    }

    public IReadOnlyList<NpcActivityDef> AllActivities()
    {
        return _db.Query(
            "SELECT npc_id, current_citadel_id, current_wealth, focus_path, current_action, stamina, reputation, updated_date " +
            "FROM so_npc_activity",
            record =>
            {
                var id = record.GetInt64(0);
                var action = record.GetString(4);
                return new NpcActivityDef(
                    id,
                    record.GetInt64(1),
                    record.GetInt64(2),
                    record.GetString(3),
                    action,
                    record.GetInt32(5),
                    record.GetInt32(6),
                    GameDateCodec.Parse(record.GetString(7)),
                    IsNpcDeceased(id, action),
                    GetNpcAllegiance(id));
            });
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

    public IReadOnlyList<NpcDef> ReplenishNpcs(GameDate date, int count = 1)
    {
        var formattedDate = GameDateCodec.Format(date);
        var tick = date.DayIndex;
        IReadOnlyList<NpcDef> result = Array.Empty<NpcDef>();
        _db.Transaction(() =>
        {
            result = ReplenishNpcsCore(date, formattedDate, tick, count);
        });
        return result;
    }

    private IReadOnlyList<NpcDef> ReplenishNpcsCore(GameDate date, string formattedDate, long tick, int count)
    {
        if (count <= 0) return Array.Empty<NpcDef>();

        // Cap dynamic population to prevent unbounded memory/database inflation (R_PERF)
        if (_registry.All.Count >= 400)
        {
            return Array.Empty<NpcDef>();
        }

        var spawned = new List<NpcDef>();
        var citadels = _map.Citadels;
        if (citadels.Count == 0) return Array.Empty<NpcDef>();

        var existingNames = new HashSet<string>(_registry.All.Select(n => n.NameVi), StringComparer.OrdinalIgnoreCase);

        var surnames = new[]
        {
            "Triệu", "Lý", "Chu", "Ngô", "Trịnh", "Vương", "Phùng", "Trần", "Chử", "Vệ",
            "Tưởng", "Thẩm", "Hàn", "Dương", "Tần", "Hứa", "Hà", "Lữ", "Khổng", "Tào",
            "Nghiêm", "Kim", "Ngụy", "Đào", "Khương", "Tạ", "Lôi", "Hạ", "Đoàn", "Liêu"
        };

        var middleGivenNames = new[]
        {
            "Văn Huy", "Bá Đạt", "Tử Kính", "Trọng Viễn", "Thúc Minh", "Quý Hòa", "Đức Nhuận",
            "Cảnh Sơn", "Phụng Tiên", "Huyền Thông", "Nguyên Phương", "Tử Hào", "Hoài Viễn",
            "An Bang", "Trấn Quốc", "Tông Diệu", "Trọng Hằng", "Sĩ Trưng", "Dực Sơn", "Kiến Lâm",
            "Minh Lương", "Quốc Bảo", "Đạo Vinh", "Diên Khánh", "Khắc Minh", "Bá Xuyên"
        };

        var archetypes = new[]
        {
            (Focus: "merchant", Role: "Lữ Hành Thương Nhân", SkillName: "Thương Tài", SkillCode: "thuong_tai_dyn",
                SkillEffect: "Giảm 5% giá mua tại sạp chợ", SkillDesc: "Nhạy bén giá cả thương trường, đàm phán mua rẻ bán đắt."),
            (Focus: "martial", Role: "Du Hiệp Kiếm Khách", SkillName: "Kỵ Xạ", SkillCode: "ky_xa_dyn",
                SkillEffect: "+10% sát thương bạo kích cung tiễn", SkillDesc: "Kỹ năng cung tiễn điêu luyện trên lưng ngựa."),
            (Focus: "strategist", Role: "Ẩn Danh Mưu Sĩ", SkillName: "Biện Thuyết", SkillCode: "bien_thuyet_dyn",
                SkillEffect: "+15% hiệu quả đàm phán ngoại giao", SkillDesc: "Hùng biện sắc sảo, nhìn thấu tâm lý đối phương."),
            (Focus: "governor", Role: "Châu Huyện Hiền Sĩ", SkillName: "Khuyến Nông", SkillCode: "khuyen_nong_dyn",
                SkillEffect: "+10% sản lượng nông trang địa phương", SkillDesc: "Khuyến khích khai hoang mở đất, tích cốc phòng đói."),
            (Focus: "wanderer", Role: "Phương Ngoại Kỳ Sĩ", SkillName: "Dược Lý", SkillCode: "duoc_ly_dyn",
                SkillEffect: "Tăng tốc độ hồi phục thể lực", SkillDesc: "Am hiểu y lý thảo dược cứu độ thế gian.")
        };

        for (var i = 0; i < count; i++)
        {
            var rng = CreateSubStreamRng(i * 1009L + _registry.All.Count, (int)(tick % int.MaxValue), 0x50C1E71_002UL);

            // Rebalance population: pick citadel with fewest NPCs
            var targetCit = citadels
                .OrderBy(c => _registry.FindByCitadel(c.Id).Count)
                .ThenBy(c => c.Id)
                .First();

            var arch = archetypes[rng.Next(archetypes.Length)];

            string nameVi = string.Empty;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var s = surnames[rng.Next(surnames.Length)];
                var mg = middleGivenNames[rng.Next(middleGivenNames.Length)];
                var candidate = $"{s} {mg}";
                if (!existingNames.Contains(candidate))
                {
                    nameVi = candidate;
                    break;
                }
            }
            if (string.IsNullOrEmpty(nameVi))
            {
                nameVi = $"Sĩ Nhân {tick % 1000}_{i + 1}";
            }
            existingNames.Add(nameVi);

            CharacterAttributes attrs = arch.Focus switch
            {
                "merchant" => new CharacterAttributes(
                    War: 40 + rng.Next(15), Ldr: 45 + rng.Next(15), Int: 62 + rng.Next(16),
                    Pol: 58 + rng.Next(15), Dip: 65 + rng.Next(16), Com: 72 + rng.Next(15),
                    Log: 65 + rng.Next(15), Cra: 50 + rng.Next(15), Con: 55 + rng.Next(15),
                    Wil: 60 + rng.Next(15), Prc: 64 + rng.Next(15), Lck: 60 + rng.Next(20)),
                "martial" => new CharacterAttributes(
                    War: 75 + rng.Next(15), Ldr: 66 + rng.Next(15), Int: 45 + rng.Next(15),
                    Pol: 40 + rng.Next(15), Dip: 42 + rng.Next(15), Com: 40 + rng.Next(15),
                    Log: 52 + rng.Next(15), Cra: 50 + rng.Next(15), Con: 70 + rng.Next(18),
                    Wil: 72 + rng.Next(16), Prc: 68 + rng.Next(15), Lck: 55 + rng.Next(15)),
                "strategist" => new CharacterAttributes(
                    War: 38 + rng.Next(15), Ldr: 50 + rng.Next(15), Int: 76 + rng.Next(15),
                    Pol: 68 + rng.Next(15), Dip: 70 + rng.Next(15), Com: 52 + rng.Next(15),
                    Log: 60 + rng.Next(15), Cra: 48 + rng.Next(15), Con: 50 + rng.Next(15),
                    Wil: 65 + rng.Next(15), Prc: 68 + rng.Next(15), Lck: 58 + rng.Next(15)),
                "governor" => new CharacterAttributes(
                    War: 45 + rng.Next(15), Ldr: 65 + rng.Next(15), Int: 68 + rng.Next(15),
                    Pol: 74 + rng.Next(15), Dip: 66 + rng.Next(15), Com: 58 + rng.Next(15),
                    Log: 64 + rng.Next(15), Cra: 52 + rng.Next(15), Con: 56 + rng.Next(15),
                    Wil: 62 + rng.Next(15), Prc: 60 + rng.Next(15), Lck: 56 + rng.Next(15)),
                _ => new CharacterAttributes(
                    War: 55 + rng.Next(15), Ldr: 50 + rng.Next(15), Int: 60 + rng.Next(15),
                    Pol: 50 + rng.Next(15), Dip: 55 + rng.Next(15), Com: 52 + rng.Next(15),
                    Log: 55 + rng.Next(15), Cra: 72 + rng.Next(16), Con: 64 + rng.Next(15),
                    Wil: 70 + rng.Next(15), Prc: 68 + rng.Next(15), Lck: 60 + rng.Next(15))
            };

            var code = $"npc_dyn_{tick}_{rng.Next(1000, 9999)}";
            var skills = new[]
            {
                new NpcSkillDef(arch.SkillCode, arch.SkillName, arch.Focus switch {
                    "martial" => "martial",
                    "merchant" => "commerce",
                    "governor" => "governance",
                    "strategist" => "strategist",
                    _ => "special"
                }, 3, arch.SkillDesc, arch.SkillEffect, Level: Math.Clamp(2 + rng.Next(4), 1, 10))
            };

            var newNpc = _registry.RegisterDynamicNpc(
                code,
                nameVi,
                arch.Role,
                targetCit.Id,
                attrs,
                personality: "Cần cù nhẫn nại",
                bioVi: $"Nhân sĩ mới nổi tại {targetCit.NameVi}, từng bước xây dựng sự nghiệp thời loạn thế.",
                skills);

            var focus = arch.Focus;
            var wealth = InitialWealth(focus);
            var initialAction = InitialAction(focus);

            _db.Execute(
                "INSERT INTO so_npc_activity (npc_id, current_citadel_id, current_wealth, focus_path, current_action, stamina, reputation, updated_date) " +
                "VALUES (@npcId, @citadelId, @wealth, @focus, @action, 100, 50, @date)",
                new DbArgs()
                    .Add("@npcId", newNpc.Id)
                    .Add("@citadelId", targetCit.Id)
                    .Add("@wealth", wealth)
                    .Add("@focus", focus)
                    .Add("@action", initialAction)
                    .Add("@date", formattedDate));

            _db.Execute(
                "INSERT INTO so_npc_stat_delta (npc_id, war_delta, ldr_delta, int_delta, pol_delta, dip_delta, com_delta, log_delta, cra_delta, updated_date) " +
                "VALUES (@npcId, 0, 0, 0, 0, 0, 0, 0, 0, @date)",
                new DbArgs()
                    .Add("@npcId", newNpc.Id)
                    .Add("@date", formattedDate));

            var eventKind = arch.Focus switch
            {
                "martial" => "martial",
                "merchant" => "commerce",
                "governor" => "governance",
                _ => "scholarly"
            };

            var headline = $"Nhân tài mới xuất thế: {newNpc.NameVi} tại {targetCit.NameVi}";
            var detail = $"{newNpc.NameVi} ({newNpc.Role}) vừa xuất đầu lộ diện tại {targetCit.NameVi}, chí hướng rộng lớn, bắt đầu tạo dựng thanh danh thời loạn thế.";

            _db.Execute(
                "INSERT INTO so_journey_log (date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact, created_tick) " +
                "VALUES (@date, @npcId, @name, @citId, @citName, @kind, @headline, @detail, 'Xuất thế', @tick)",
                new DbArgs()
                    .Add("@date", formattedDate)
                    .Add("@npcId", newNpc.Id)
                    .Add("@name", newNpc.NameVi)
                    .Add("@citId", targetCit.Id)
                    .Add("@citName", targetCit.NameVi)
                    .Add("@kind", eventKind)
                    .Add("@headline", headline)
                    .Add("@detail", detail)
                    .Add("@tick", tick));

            spawned.Add(newNpc);
            _log.Info($"Society replenishment: spawned {newNpc.NameVi} ({newNpc.Role}) in {targetCit.NameVi}.");
        }

        return spawned;
    }

    private bool AnyCitadelUnderpopulated()
    {
        foreach (var c in _map.Citadels)
        {
            if (_registry.FindByCitadel(c.Id).Count < 18)
            {
                return true;
            }
        }
        return false;
    }


    private sealed class SocietySubStreamRng : IRng
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private ulong _state;

        public SocietySubStreamRng(ulong seed)
        {
            _state = seed == 0 ? 0x0123456789ABCDEFUL : seed;
        }

        public ulong State => _state;

        public void Reseed(ulong state) => _state = state == 0 ? 0x0123456789ABCDEFUL : state;

        public ulong NextULong()
        {
            _state += Gamma;
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
