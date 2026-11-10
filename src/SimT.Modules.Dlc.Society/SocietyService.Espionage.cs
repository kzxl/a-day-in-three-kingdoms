using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Society;

internal sealed partial class SocietyService : IEspionageNetwork
{
    public IReadOnlyList<SpyNetworkNodeDef> GetSpyNodes()
    {
        var existing = _db.Query(
            "SELECT citadel_id, intel_level, infiltrated_day_index, active_operation, sabotage_count FROM so_spy_network",
            r => (
                CitadelId: r.GetInt64(0),
                IntelLevel: r.GetInt32(1),
                DayIndex: r.GetInt32(2),
                Operation: r.GetString(3),
                SabotageCount: r.GetInt32(4)))
            .ToDictionary(x => x.CitadelId);

        var result = new List<SpyNetworkNodeDef>(_map.Citadels.Count);
        foreach (var cit in _map.Citadels)
        {
            if (existing.TryGetValue(cit.Id, out var row))
            {
                result.Add(new SpyNetworkNodeDef(cit.Id, cit.NameVi, row.IntelLevel, row.DayIndex, row.Operation, row.SabotageCount));
            }
            else
            {
                result.Add(new SpyNetworkNodeDef(cit.Id, cit.NameVi, 0, 0, "none", 0));
            }
        }
        return result;
    }

    public SpyNetworkNodeDef GetSpyNode(long citadelId)
    {
        var citName = _map.Find(citadelId)?.NameVi ?? $"Thành #{citadelId}";
        var row = _db.QueryOne(
            "SELECT intel_level, infiltrated_day_index, active_operation, sabotage_count FROM so_spy_network WHERE citadel_id = @citId",
            r => new SpyNetworkNodeDef(citadelId, citName, r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3)),
            DbArgs.One("@citId", citadelId));

        return row ?? new SpyNetworkNodeDef(citadelId, citName, 0, 0, "none", 0);
    }

    public string DeploySpyCell(long citadelId)
    {
        var node = GetSpyNode(citadelId);
        if (node.IntelLevel >= 3)
        {
            return $"[Mật Thám Ty] Lưới Tế Tác tại {node.CitadelNameVi} đã đạt cấp tối đa (Cấp 3 — Thiên La Địa Võng).";
        }

        var nextLevel = node.IntelLevel + 1;
        var costGold = nextLevel switch
        {
            1 => 150L,
            2 => 250L,
            _ => 400L
        };

        IWallet? wallet = null;
        _broker?.TryGet(CapabilityIds.PlayerWallet, out wallet);
        if (wallet != null && wallet.Balance < costGold)
        {
            throw new SimException("society.insufficient_funds",
                $"Không đủ ngân lượng thiết lập Mật Thám Cấp {nextLevel} tại {node.CitadelNameVi} (cần {costGold} Quan, hiện có {wallet.Balance} Quan).");
        }

        var today = _clock.Today;
        var dayIdx = today.DayIndex;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            wallet?.Charge(costGold, "society.espionage", $"Thiết lập Tế Tác Cấp {nextLevel} tại {node.CitadelNameVi}", $"spy:deploy:{citadelId}:{nextLevel}:{formattedDate}");

            _db.Execute(
                """
                INSERT INTO so_spy_network (citadel_id, intel_level, infiltrated_day_index, active_operation, sabotage_count)
                VALUES (@citId, @lvl, @day, 'recon', 0)
                ON CONFLICT(citadel_id) DO UPDATE SET
                    intel_level = @lvl,
                    infiltrated_day_index = @day
                """,
                new DbArgs().Add("@citId", citadelId).Add("@lvl", nextLevel).Add("@day", dayIdx));

            if (_broker?.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) == true && attrs != null)
            {
                attrs.AddExp("INT", 18);
                attrs.AddExp("LDR", 12);
            }
        });

        _log.Info($"Deployed spy cell at citadel {citadelId}: level={nextLevel}");
        return $"[Tế Tác Mật Báo] Đã cài cắm thành công Mật Thám Cấp {nextLevel}/3 tại {node.CitadelNameVi} (-{costGold} Quan). Nắm bắt toàn bộ động tĩnh thành trì!";
    }

    public string ExecuteCovertWhisper(long citadelId, string operationKind)
    {
        var node = GetSpyNode(citadelId);
        if (node.IntelLevel <= 0)
        {
            throw new SimException("society.no_spy_cell",
                $"Chưa có lưới Tế Tác tại {node.CitadelNameVi}. Hãy thiết lập Mật Thám Cấp 1 trước khi tung tin đồn mật.");
        }

        const long whisperCost = 90L;
        IWallet? wallet = null;
        _broker?.TryGet(CapabilityIds.PlayerWallet, out wallet);
        if (wallet != null && wallet.Balance < whisperCost)
        {
            throw new SimException("society.insufficient_funds",
                $"Không đủ ngân lượng phát động mật kế tại {node.CitadelNameVi} (cần {whisperCost} Quan).");
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);
        var normalizedOp = string.IsNullOrWhiteSpace(operationKind) ? "price_whisper" : operationKind.Trim().ToLowerInvariant();

        var (headlineVi, detailVi, impactVi) = normalizedOp switch
        {
            "counter_intel" => (
                $"Phản gián trừ gian tại {node.CitadelNameVi}",
                $"Đội Tế Tác ngầm quét sạch tai mắt đối thủ tại {node.CitadelNameVi}, bảo vệ tuyệt mật thương lộ.",
                "Mật vụ +1 · Bảo mật thương đoàn"),
            "sow_discord" => (
                $"Tung lời đồn ly gián hào phú tại {node.CitadelNameVi}",
                $"Tin mật lan truyền trong các tửu lâu {node.CitadelNameVi} khiến các thương hội đối thủ nghi kỵ lẫn nhau.",
                "Mật vụ +1 · Áp chế đối thủ"),
            _ => (
                $"Tung tin đồn khan hiếm hàng hóa tại {node.CitadelNameVi}",
                $"Tế Tác phao tin quân lương và tơ lụa sắp tăng giá tại chợ {node.CitadelNameVi}, tạo sóng đầu cơ có lợi.",
                "Mật vụ +1 · Thao túng thị trường")
        };

        _db.Transaction(() =>
        {
            wallet?.Charge(whisperCost, "society.whisper", $"Mật kế Tế Tác ({normalizedOp}) tại {node.CitadelNameVi}", $"spy:whisper:{citadelId}:{normalizedOp}:{formattedDate}:{node.SabotageCount + 1}");

            _db.Execute(
                """
                UPDATE so_spy_network
                SET active_operation = @op,
                    sabotage_count = sabotage_count + 1
                WHERE citadel_id = @citId
                """,
                new DbArgs().Add("@op", normalizedOp).Add("@citId", citadelId));

            _db.Execute(
                """
                INSERT INTO so_journey_log (date, npc_id, npc_name_vi, citadel_id, citadel_name_vi, event_kind, headline_vi, detail_vi, stat_impact, created_tick)
                VALUES (@date, 1, 'Mật Thám Tế Tác', @citId, @citName, 'scholarly', @head, @detail, @impact, @tick)
                """,
                new DbArgs()
                    .Add("@date", formattedDate)
                    .Add("@citId", citadelId)
                    .Add("@citName", node.CitadelNameVi)
                    .Add("@head", headlineVi)
                    .Add("@detail", detailVi)
                    .Add("@impact", impactVi)
                    .Add("@tick", today.DayIndex));

            if (_broker?.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) == true && attrs != null)
            {
                attrs.AddExp("INT", 20);
            }
        });

        _log.Info($"Executed covert whisper '{normalizedOp}' at citadel {citadelId}");
        return $"[Mật Kế Thành Công] {headlineVi} (-{whisperCost} Quan). {detailVi}";
    }

    public IReadOnlyList<HermitTomeDef> GetHermitTomes()
    {
        return _db.Query(
            """
            SELECT code, name_vi, hermit_name_vi, citadel_id, stat_code, stat_bonus, skill_code, is_acquired, acquired_day_index
            FROM so_hermit_tome
            ORDER BY citadel_id, code
            """,
            r =>
            {
                var citId = r.GetInt64(3);
                var citName = _map.Find(citId)?.NameVi ?? $"Thành #{citId}";
                return new HermitTomeDef(
                    r.GetString(0),
                    r.GetString(1),
                    r.GetString(2),
                    citId,
                    citName,
                    r.GetString(4),
                    r.GetInt32(5),
                    r.GetString(6),
                    r.GetInt32(7) != 0,
                    r.GetInt32(8));
            });
    }

    public string SeekHermitTome(string tomeCode)
    {
        var tome = GetHermitTomes().FirstOrDefault(t => string.Equals(t.Code, tomeCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new SimException("society.tome_not_found", $"Không tìm thấy kỳ thư mã '{tomeCode}'.");

        if (tome.IsAcquired)
        {
            return $"[Kỳ Thư Các] Bạn đã lĩnh hội '{tome.NameVi}' từ {tome.HermitNameVi} trước đó rồi.";
        }

        var spyNode = GetSpyNode(tome.CitadelId);
        var currentCityId = _player.CurrentCityId;
        if (currentCityId != tome.CitadelId && spyNode.IntelLevel < 1)
        {
            throw new SimException("society.hermit_unreachable",
                $"Cần đích thân đến {tome.CitadelNameVi} hoặc có Mật Thám Cấp 1 tại {tome.CitadelNameVi} để tầm訪 {tome.HermitNameVi}.");
        }

        const long offeringGold = 200L;
        IWallet? wallet = null;
        _broker?.TryGet(CapabilityIds.PlayerWallet, out wallet);
        if (wallet != null && wallet.Balance < offeringGold)
        {
            throw new SimException("society.insufficient_funds",
                $"Cần {offeringGold} Quan lễ vật cầu kiến {tome.HermitNameVi} để lĩnh hội {tome.NameVi}.");
        }

        var today = _clock.Today;
        var formattedDate = GameDateCodec.Format(today);

        _db.Transaction(() =>
        {
            wallet?.Charge(offeringGold, "society.hermit_tome", $"Lễ vật tầm sư học đạo cầu {tome.NameVi}", $"tome:{tome.Code}:{formattedDate}");

            _db.Execute(
                "UPDATE so_hermit_tome SET is_acquired = 1, acquired_day_index = @day WHERE code = @code",
                new DbArgs().Add("@day", today.DayIndex).Add("@code", tome.Code));

            if (_broker?.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) == true && attrs != null)
            {
                attrs.AddExp(tome.StatCode, tome.StatBonus * 35);
                attrs.LearnSkill(tome.SkillCode);
                attrs.TrainSkill(tome.SkillCode, 60);
                attrs.GainFame(30, $"Được {tome.HermitNameVi} truyền thụ kỳ thư '{tome.NameVi}' tại {tome.CitadelNameVi}.", virtueDelta: 6);
            }
        });

        _log.Info($"Acquired Hermit Tome {tome.Code} from {tome.HermitNameVi}");
        return $"[Kỳ Ngộ Ẩn Sĩ] Tại {tome.CitadelNameVi}, {tome.HermitNameVi} cảm kích tấm lòng cầu đạo, truyền thụ tuyệt học '{tome.NameVi}'! Tăng mạnh kinh nghiệm {tome.StatCode} (+{tome.StatBonus} tư chất), kỹ năng {tome.SkillCode} +60 EXP và Uy Danh +30!";
    }
}
