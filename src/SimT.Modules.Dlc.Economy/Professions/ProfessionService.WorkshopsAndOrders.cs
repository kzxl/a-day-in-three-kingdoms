using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Economy;

internal sealed partial class ProfessionService
{
    // =========================================================================
    // WORKSHOP AUTOMATION & ATELIER MANAGEMENT
    // =========================================================================

    public WorkshopDef? GetWorkshop(long citadelId) =>
        _db.QueryOne(
            """
            SELECT id, citadel_id, trade_id, name_vi, monthly_rent, furnace_level, hired_artisans, is_active, created_day_index
            FROM pf_workshop
            WHERE citadel_id = @citadel
            """,
            row => new WorkshopDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt64(7) != 0,
                row.GetInt32(8)),
            DbArgs.One("@citadel", citadelId));

    public IReadOnlyList<WorkshopDef> AllWorkshops() =>
        _db.Query(
            """
            SELECT id, citadel_id, trade_id, name_vi, monthly_rent, furnace_level, hired_artisans, is_active, created_day_index
            FROM pf_workshop
            ORDER BY citadel_id ASC
            """,
            row => new WorkshopDef(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt64(7) != 0,
                row.GetInt32(8)));

    public WorkResult OpenWorkshop(long citadelId, long tradeId)
    {
        var existing = GetWorkshop(citadelId);
        if (existing is not null)
        {
            return WorkResult.Failure("Đã có xưởng chế tác tại thành trì này.");
        }

        var trade = Trades.FirstOrDefault(t => t.Id == tradeId);
        if (trade is null)
        {
            return WorkResult.Failure("Nghề nghiệp không hợp lệ.");
        }

        if (_wallet.Liquid < WorkshopOpenCost)
        {
            return WorkResult.Failure($"Cần {WorkshopOpenCost} Quan tiền thuê mặt bằng mở xưởng, chỉ có {_wallet.Liquid}.");
        }

        var workshopName = $"Phường Nghề {trade.NameVi}";
        var day = _clock.Today.DayIndex;

        _db.Transaction(() =>
        {
            _wallet.Charge(WorkshopOpenCost, JournalCodes.ToolFee, $"Mở {workshopName}");
            _db.Execute(
                """
                INSERT INTO pf_workshop(citadel_id, trade_id, name_vi, monthly_rent, furnace_level, hired_artisans, is_active, created_day_index)
                VALUES(@citadel, @trade, @name, @rent, 1, 0, 1, @day)
                """,
                new DbArgs()
                    .Add("@citadel", citadelId)
                    .Add("@trade", tradeId)
                    .Add("@name", workshopName)
                    .Add("@rent", WorkshopBaseRent)
                    .Add("@day", day));
        });

        return WorkResult.Success($"Mở thành công {workshopName} tại thành! Tiền thuê cơ sở {WorkshopBaseRent} Quan/tháng.", 1, 0);
    }

    public WorkResult CloseWorkshop(long citadelId)
    {
        var ws = GetWorkshop(citadelId);
        if (ws is null)
        {
            return WorkResult.Failure("Chưa có xưởng chế tác nào tại thành này để sang nhượng.");
        }

        var refund = WorkshopOpenCost * 3 / 10; // 30% lease deposit recovery (36 Quan)
        _db.Transaction(() =>
        {
            _db.Execute("DELETE FROM pf_workshop WHERE citadel_id = @citadel", DbArgs.One("@citadel", citadelId));
            if (refund > 0)
            {
                _wallet.Pay(refund, JournalCodes.CourtStipend, $"Sang nhượng mặt bằng {ws.NameVi}");
            }
        });

        return WorkResult.Success($"Đã sang nhượng mặt bằng {ws.NameVi}, thu hồi {refund} Quan tiền cọc. Bạn có thể mở xưởng chuyên môn mới tại thành!", 0, 0);
    }

    public WorkResult HireArtisan(long citadelId)
    {
        var ws = GetWorkshop(citadelId);
        if (ws is null)
        {
            return WorkResult.Failure("Chưa có xưởng chế tác tại thành này.");
        }

        if (ws.HiredArtisans >= 5)
        {
            return WorkResult.Failure("Xưởng đã đạt giới hạn thợ phụ tối đa (5 người).");
        }

        if (_wallet.Liquid < ArtisanHireCost)
        {
            return WorkResult.Failure($"Cần {ArtisanHireCost} Quan tiền chiêu mộ thợ phụ, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(ArtisanHireCost, JournalCodes.ToolFee, "Chiêu mộ thợ phụ gia công");
            _db.Execute(
                "UPDATE pf_workshop SET hired_artisans = hired_artisans + 1 WHERE citadel_id = @citadel",
                DbArgs.One("@citadel", citadelId));
        });

        return WorkResult.Success($"Đã tuyển thêm 1 thợ phụ (hiện có {ws.HiredArtisans + 1} người). Lương tháng {ArtisanMonthlySalary} Quan/người.", 1, 0);
    }

    public WorkResult DismissArtisan(long citadelId)
    {
        var ws = GetWorkshop(citadelId);
        if (ws is null || ws.HiredArtisans <= 0)
        {
            return WorkResult.Failure("Không có thợ phụ nào để cho nghỉ việc.");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE pf_workshop SET hired_artisans = hired_artisans - 1 WHERE citadel_id = @citadel",
                DbArgs.One("@citadel", citadelId));
        });

        return WorkResult.Success($"Đã cho 1 thợ phụ thôi việc. Còn lại {ws.HiredArtisans - 1} người.", 1, 0);
    }

    public WorkResult UpgradeFurnace(long citadelId)
    {
        var ws = GetWorkshop(citadelId);
        if (ws is null)
        {
            return WorkResult.Failure("Chưa có xưởng chế tác tại thành này.");
        }

        if (ws.FurnaceLevel >= 5)
        {
            return WorkResult.Failure("Lò rèn/cơ sở vật chất xưởng đã đạt cấp tối đa (cấp 5).");
        }

        var cost = ws.FurnaceLevel * 80;
        if (_wallet.Liquid < cost)
        {
            return WorkResult.Failure($"Cần {cost} Quan để nâng cấp cơ sở xưởng, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(cost, JournalCodes.ToolFee, "Nâng cấp cơ sở vật chất xưởng");
            _db.Execute(
                "UPDATE pf_workshop SET furnace_level = furnace_level + 1 WHERE citadel_id = @citadel",
                DbArgs.One("@citadel", citadelId));
        });

        return WorkResult.Success($"Nâng cấp cơ sở xưởng lên cấp {ws.FurnaceLevel + 1}! Tăng mạnh tỷ lệ tạo tác Thần Binh/Tuyệt Phẩm.", 1, 0);
    }

    // =========================================================================
    // GUILD COMMISSIONS & TWO-WAY BOUNTY BOARD
    // =========================================================================

    private static CraftOrderDef MapOrder(DbRecord row) =>
        new(
            row.GetInt64(0),
            row.GetString(1),
            row.GetString(2),
            row.GetInt64(3),
            row.GetInt64(4),
            row.GetInt32(5),
            row.GetInt32(6),
            row.GetInt32(7),
            row.GetInt32(8),
            row.GetString(9),
            row.IsDbNull(10) ? null : row.GetInt32(10),
            row.IsDbNull(11) ? "INBOUND" : row.GetString(11),
            row.IsDbNull(12) ? null : row.GetInt64(12),
            row.IsDbNull(13) ? null : row.GetString(13),
            row.IsDbNull(14) ? null : row.GetInt32(14));

    public IReadOnlyList<CraftOrderDef> AvailableOrders(long citadelId) =>
        _db.Query(
            """
            SELECT id, client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status, accepted_day,
                   COALESCE(direction, 'INBOUND'), assignee_npc_id, assignee_name_vi, completion_day
            FROM pf_order
            WHERE citadel_id = @citadel AND COALESCE(direction, 'INBOUND') = 'INBOUND' AND status = 'AVAILABLE' AND deadline_day >= @today
            ORDER BY id ASC
            """,
            MapOrder,
            new DbArgs().Add("@citadel", citadelId).Add("@today", _clock.Today.DayIndex));

    public IReadOnlyList<CraftOrderDef> ActiveOrders() =>
        _db.Query(
            """
            SELECT id, client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status, accepted_day,
                   COALESCE(direction, 'INBOUND'), assignee_npc_id, assignee_name_vi, completion_day
            FROM pf_order
            WHERE COALESCE(direction, 'INBOUND') = 'INBOUND' AND status = 'ACCEPTED' AND deadline_day >= @today
            ORDER BY deadline_day ASC
            """,
            MapOrder,
            DbArgs.One("@today", _clock.Today.DayIndex));

    public IReadOnlyList<CraftOrderDef> PlayerBounties(long? citadelId = null) =>
        citadelId.HasValue
            ? _db.Query(
                """
                SELECT id, client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status, accepted_day,
                       COALESCE(direction, 'INBOUND'), assignee_npc_id, assignee_name_vi, completion_day
                FROM pf_order
                WHERE COALESCE(direction, 'INBOUND') = 'OUTBOUND' AND citadel_id = @citadel
                ORDER BY id DESC
                """,
                MapOrder,
                DbArgs.One("@citadel", citadelId.Value))
            : _db.Query(
                """
                SELECT id, client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status, accepted_day,
                       COALESCE(direction, 'INBOUND'), assignee_npc_id, assignee_name_vi, completion_day
                FROM pf_order
                WHERE COALESCE(direction, 'INBOUND') = 'OUTBOUND'
                ORDER BY id DESC
                """,
                MapOrder);

    private sealed record OrderAcceptInfo(long Id, string Status, string Name, string Direction);

    public WorkResult AcceptOrder(long orderId)
    {
        var order = _db.QueryOne(
            "SELECT id, status, client_name_vi, COALESCE(direction, 'INBOUND') FROM pf_order WHERE id = @id",
            row => new OrderAcceptInfo(row.GetInt64(0), row.GetString(1), row.GetString(2), row.GetString(3)),
            DbArgs.One("@id", orderId));

        if (order is null || order.Direction != "INBOUND")
        {
            return WorkResult.Failure("Không tìm thấy đơn đặt hàng.");
        }

        if (order.Status != "AVAILABLE")
        {
            return WorkResult.Failure("Đơn hàng này không ở trạng thái có thể tiếp nhận.");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE pf_order SET status = 'ACCEPTED', accepted_day = @day WHERE id = @id",
                new DbArgs().Add("@day", _clock.Today.DayIndex).Add("@id", orderId));
        });

        return WorkResult.Success($"Đã tiếp nhận đơn đặt hàng từ {order.Name}.", 1, 0);
    }

    public WorkResult FulfillOrder(long citadelId, long orderId)
    {
        var order = _db.QueryOne(
            """
            SELECT id, client_name_vi, target_item_id, quantity_needed, reward_copper, fame_reward, status, citadel_id, COALESCE(direction, 'INBOUND')
            FROM pf_order
            WHERE id = @id
            """,
            row => new
            {
                Id = row.GetInt64(0),
                ClientName = row.GetString(1),
                TargetItemId = row.GetInt64(2),
                QuantityNeeded = row.GetInt32(3),
                RewardCopper = row.GetInt32(4),
                FameReward = row.GetInt32(5),
                Status = row.GetString(6),
                CitadelId = row.GetInt64(7),
                Direction = row.GetString(8)
            },
            DbArgs.One("@id", orderId));

        if (order is null || order.Direction != "INBOUND")
        {
            return WorkResult.Failure("Không tìm thấy đơn hàng.");
        }

        if (order.Status != "ACCEPTED" && order.Status != "AVAILABLE")
        {
            return WorkResult.Failure("Đơn hàng này đã hoàn thành hoặc hết hạn.");
        }

        var currentQty = _bag.Quantity(citadelId, order.TargetItemId);
        if (currentQty < order.QuantityNeeded)
        {
            var itemName = _items.Find(order.TargetItemId)?.NameVi ?? "vật phẩm";
            return WorkResult.Failure($"Chưa đủ số lượng giao nộp: cần {order.QuantityNeeded} {itemName}, chỉ có {currentQty} trong tay nải tại thành này.");
        }

        var rewardCopper = order.RewardCopper;
        if (HasPerk("thuong_than_dieu_toan"))
        {
            rewardCopper += (int)(rewardCopper * 0.25);
        }

        _db.Transaction(() =>
        {
            _bag.Remove(citadelId, order.TargetItemId, order.QuantityNeeded);
            _wallet.Pay(rewardCopper, JournalCodes.CourtStipend, $"Thù lao đơn đặt hàng {order.ClientName}");

            _db.Execute(
                "UPDATE pf_guild_fame SET total_fame = total_fame + @fame WHERE id = 1",
                DbArgs.One("@fame", order.FameReward));

            _db.Execute(
                "UPDATE pf_order SET status = 'COMPLETED' WHERE id = @id",
                DbArgs.One("@id", orderId));

            _attributes?.AddExp("CRA", 30);
        });

        return WorkResult.Success(
            $"Giao nộp thành công cho {order.ClientName}! Nhận {rewardCopper} Quan và +{order.FameReward} Danh vọng phường nghề.",
            order.QuantityNeeded,
            0);
    }

    public WorkResult PostBounty(long citadelId, long targetItemId, int quantity, int rewardCopper)
    {
        if (quantity <= 0)
            return WorkResult.Failure("Số lượng vật phẩm ủy thác phải lớn hơn 0.");

        var item = _items.Find(targetItemId);
        if (item is null)
            return WorkResult.Failure("Vật phẩm ủy thác không tồn tại trong danh mục.");

        var minReward = Math.Max(10, (int)(item.BasePrice * quantity * 0.8));
        if (rewardCopper < minReward)
            return WorkResult.Failure($"Tiền thưởng cáo thị quá thấp (tối thiểu {minReward} Quan cho {quantity} {item.NameVi}) sẽ không có thợ hay danh sĩ nào nhận việc.");

        if (_wallet.Liquid < rewardCopper)
            return WorkResult.Failure($"Cần ký quỹ {rewardCopper} Quan tiền thưởng cáo thị, hiện chỉ có {_wallet.Liquid} Quan.");

        var playerName = _player?.Name ?? "Chủ Nhân";
        var deadline = _clock.Today.DayIndex + 30;
        var fameVal = Math.Max(5, quantity * 2);

        _db.Transaction(() =>
        {
            _wallet.Charge(rewardCopper, "PF.BOUNTY_ESCROW", $"Ký quỹ cáo thị thu thập {quantity} {item.NameVi}");
            _db.Execute(
                """
                INSERT INTO pf_order (client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status, direction)
                VALUES (@client, 'PLAYER_BOUNTY', @citadel, @item, @qty, @reward, @fame, @deadline, 'AVAILABLE', 'OUTBOUND')
                """,
                new DbArgs()
                    .Add("@client", playerName)
                    .Add("@citadel", citadelId)
                    .Add("@item", targetItemId)
                    .Add("@qty", quantity)
                    .Add("@reward", rewardCopper)
                    .Add("@fame", fameVal)
                    .Add("@deadline", deadline));
        });

        _log.Info($"dlc.professions: posted player bounty at citadel {citadelId}: {quantity}x {item.Code} for {rewardCopper} copper");
        return WorkResult.Success($"Đã niêm yết Cáo thị ủy thác thu thập {quantity} {item.NameVi} (ký quỹ {rewardCopper} Quan). Các thợ mỏ, dược sư và hào kiệt trong thành sẽ sớm tiếp nhận!", quantity, 0);
    }

    public WorkResult DispatchRetainerBounty(long citadelId, long retainerNpcId, long targetItemId, int quantity)
    {
        if (quantity <= 0)
            return WorkResult.Failure("Số lượng giao phó phải lớn hơn 0.");

        var item = _items.Find(targetItemId);
        if (item is null)
            return WorkResult.Failure("Vật phẩm không hợp lệ.");

        var social = GetSocial();
        var retainer = social?.GetRetainer(retainerNpcId);
        if (retainer is null)
            return WorkResult.Failure("Nhân vật này chưa phải là Môn khách dưới trướng của bạn.");

        var npcDef = _npcs?.Find(retainerNpcId);
        var retainerName = npcDef?.NameVi ?? $"Môn khách #{retainerNpcId}";

        var busy = _db.ScalarLong(
            "SELECT COUNT(*) FROM pf_order WHERE COALESCE(direction, 'INBOUND') = 'OUTBOUND' AND assignee_npc_id = @npc AND status = 'ACCEPTED'",
            DbArgs.One("@npc", retainerNpcId));
        if (busy > 0)
            return WorkResult.Failure($"Môn khách {retainerName} đang bận thực hiện một nhiệm vụ ủy thác khác.");

        var duration = CalculateNpcExtractionDays(citadelId, targetItemId, quantity, npcDef);
        var today = _clock.Today.DayIndex;
        var completionDay = today + duration;
        var deadline = completionDay + 15;
        var playerName = _player?.Name ?? "Chủ Nhân";

        _db.Transaction(() =>
        {
            _db.Execute(
                """
                INSERT INTO pf_order (client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status, accepted_day, direction, assignee_npc_id, assignee_name_vi, completion_day)
                VALUES (@client, 'RETAINER_TASK', @citadel, @item, @qty, 1, 5, @deadline, 'ACCEPTED', @today, 'OUTBOUND', @npcId, @npcName, @compDay)
                """,
                new DbArgs()
                    .Add("@client", playerName)
                    .Add("@citadel", citadelId)
                    .Add("@item", targetItemId)
                    .Add("@qty", quantity)
                    .Add("@deadline", deadline)
                    .Add("@today", today)
                    .Add("@npcId", retainerNpcId)
                    .Add("@npcName", retainerName)
                    .Add("@compDay", completionDay));
        });

        _log.Info($"dlc.professions: dispatched retainer {retainerName} (#{retainerNpcId}) for {quantity}x {item.Code} (ETA {duration} days)");
        return WorkResult.Success($"Đã sai phái Môn khách {retainerName} đi thu thập {quantity} {item.NameVi} (Dự kiến hoàn thành sau {duration} ngày).", quantity, 0);
    }

    public WorkResult CancelBounty(long orderId)
    {
        var order = _db.QueryOne(
            """
            SELECT id, target_item_id, quantity_needed, reward_copper, status, client_type, COALESCE(direction, 'INBOUND')
            FROM pf_order
            WHERE id = @id
            """,
            row => new
            {
                Id = row.GetInt64(0),
                TargetItemId = row.GetInt64(1),
                Quantity = row.GetInt32(2),
                RewardCopper = row.GetInt32(3),
                Status = row.GetString(4),
                ClientType = row.GetString(5),
                Direction = row.GetString(6)
            },
            DbArgs.One("@id", orderId));

        if (order is null || order.Direction != "OUTBOUND")
            return WorkResult.Failure("Không tìm thấy cáo thị ủy thác.");

        if (order.Status != "AVAILABLE")
            return WorkResult.Failure("Cáo thị này đã có người nhận hoặc đã kết thúc, không thể thu hồi.");

        var itemName = _items.Find(order.TargetItemId)?.NameVi ?? "vật phẩm";
        var refund = order.ClientType == "RETAINER_TASK" ? 0 : order.RewardCopper;

        _db.Transaction(() =>
        {
            _db.Execute("UPDATE pf_order SET status = 'CANCELLED' WHERE id = @id", DbArgs.One("@id", orderId));
            if (refund > 0)
            {
                _wallet.Pay(refund, "PF.BOUNTY_REFUND", $"Thu hồi cáo thị {order.Quantity} {itemName}, hoàn trả tiền cọc");
            }
        });

        return WorkResult.Success($"Đã gỡ bỏ cáo thị thu thập {itemName} và hoàn trả {refund} Quan tiền ký quỹ.", 0, 0);
    }

    public void OnDay(GameDate date)
    {
        var today = date.DayIndex;

        // 1. Match AVAILABLE player bounties with eligible local NPCs
        var openBounties = _db.Query(
            """
            SELECT id, citadel_id, target_item_id, quantity_needed, reward_copper, deadline_day
            FROM pf_order
            WHERE COALESCE(direction, 'INBOUND') = 'OUTBOUND' AND status = 'AVAILABLE' AND deadline_day >= @today
            ORDER BY id ASC
            """,
            row => (
                Id: row.GetInt64(0),
                CitadelId: row.GetInt64(1),
                TargetItemId: row.GetInt64(2),
                Quantity: row.GetInt32(3),
                Reward: row.GetInt32(4),
                Deadline: row.GetInt32(5)),
            DbArgs.One("@today", today));

        foreach (var b in openBounties)
        {
            var candidates = _npcs?.FindByCitadel(b.CitadelId);
            if (candidates is null || candidates.Count == 0)
            {
                candidates = _npcs?.All;
            }
            if (candidates is null || candidates.Count == 0) continue;

            var chosen = SelectBestNpcForBounty(candidates, b.CitadelId, b.TargetItemId, b.Id, today);
            if (chosen is null) continue;

            var duration = CalculateNpcExtractionDays(b.CitadelId, b.TargetItemId, b.Quantity, chosen);
            var compDay = today + duration;

            _db.Transaction(() =>
            {
                _db.Execute(
                    """
                    UPDATE pf_order
                    SET status = 'ACCEPTED',
                        accepted_day = @today,
                        assignee_npc_id = @npcId,
                        assignee_name_vi = @npcName,
                        completion_day = @compDay
                    WHERE id = @id AND status = 'AVAILABLE'
                    """,
                    new DbArgs()
                        .Add("@today", today)
                        .Add("@npcId", chosen.Id)
                        .Add("@npcName", chosen.NameVi)
                        .Add("@compDay", compDay)
                        .Add("@id", b.Id));
            });

            _log.Info($"dlc.professions: NPC {chosen.NameVi} (#{chosen.Id}) accepted player bounty #{b.Id} at citadel {b.CitadelId}, ETA day {compDay}");
        }

        // 2. Complete ACCEPTED player bounties that reached completion_day
        var readyBounties = _db.Query(
            """
            SELECT id, citadel_id, target_item_id, quantity_needed, reward_copper, client_type, assignee_npc_id, assignee_name_vi
            FROM pf_order
            WHERE COALESCE(direction, 'INBOUND') = 'OUTBOUND' AND status = 'ACCEPTED' AND completion_day IS NOT NULL AND completion_day <= @today
            ORDER BY id ASC
            """,
            row => (
                Id: row.GetInt64(0),
                CitadelId: row.GetInt64(1),
                TargetItemId: row.GetInt64(2),
                Quantity: row.GetInt32(3),
                Reward: row.GetInt32(4),
                ClientType: row.GetString(5),
                NpcId: row.IsDbNull(6) ? (long?)null : row.GetInt64(6),
                NpcName: row.IsDbNull(7) ? "Thợ Bản Địa" : row.GetString(7)),
            DbArgs.One("@today", today));

        var social = GetSocial();
        foreach (var done in readyBounties)
        {
            var itemName = _items.Find(done.TargetItemId)?.NameVi ?? $"Vật phẩm #{done.TargetItemId}";
            _db.Transaction(() =>
            {
                _bag.Add(done.CitadelId, done.TargetItemId, done.Quantity);
                _db.Execute("UPDATE pf_order SET status = 'COMPLETED' WHERE id = @id", DbArgs.One("@id", done.Id));

                if (done.NpcId.HasValue && social is not null)
                {
                    social.RecordCooperation(
                        done.NpcId.Value,
                        affinityDelta: 4,
                        trustDelta: 3,
                        summaryVi: $"Hoàn thành xuất sắc ủy thác thu thập {done.Quantity} {itemName}, nhận thù lao sòng phẳng.");
                }
            });

            _log.Info($"dlc.professions: NPC {done.NpcName} completed bounty #{done.Id} -> delivered +{done.Quantity} item #{done.TargetItemId} at citadel {done.CitadelId}");
        }
    }

    private ISocialNetwork? GetSocial()
    {
        if (_social != null) return _social;
        if (_broker != null && _broker.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var s)) return s;
        return null;
    }

    private NpcDef? SelectBestNpcForBounty(IReadOnlyList<NpcDef> pool, long citadelId, long itemId, long orderId, long today)
    {
        var busyNpcIds = _db.Query(
            "SELECT assignee_npc_id FROM pf_order WHERE COALESCE(direction, 'INBOUND') = 'OUTBOUND' AND status = 'ACCEPTED' AND assignee_npc_id IS NOT NULL",
            row => row.GetInt64(0)).ToHashSet();

        var requiredSkill = ResolveGatherSkillForItem(itemId);
        NpcDef? best = null;
        int bestScore = int.MinValue;

        foreach (var npc in pool)
        {
            if (busyNpcIds.Contains(npc.Id)) continue;
            if (npc.Role.Contains("Chủ") || npc.Role.Contains("Đế") || npc.Role.Contains("Vương")) continue;

            var hasMatchingSkill = npc.Skills.Any(s => s.Code == requiredSkill || s.Code is "vein_divining" or "exotic_husbandry");
            var attrScore = requiredSkill switch
            {
                "khai_khoang" => npc.Attributes.Con + npc.Attributes.Cra,
                "tam_duoc" => npc.Attributes.Prc + npc.Attributes.Int,
                "thu_liep" => npc.Attributes.War + npc.Attributes.Prc,
                _ => npc.Attributes.Cra + npc.Attributes.Com
            };

            var tieBreaker = (int)((uint)(npc.Id * 31 + orderId * 17 + today) % 25);
            var score = (hasMatchingSkill ? 100 : 0) + attrScore + (npc.CitadelId == citadelId ? 40 : 0) + tieBreaker;
            if (score > bestScore)
            {
                bestScore = score;
                best = npc;
            }
        }

        return best ?? pool[0];
    }

    private int CalculateNpcExtractionDays(long citadelId, long itemId, int quantity, NpcDef? npc)
    {
        var hasLocalNode = false;
        if (_broker != null && _broker.TryGet<IValueChainService>(CapabilityIds.ValueChain, out var vc) && vc != null)
        {
            hasLocalNode = vc.GetResourceNodes(citadelId).Any(n => n.PrimaryItemId == itemId || n.SecondaryItemId == itemId);
        }

        var skillCode = ResolveGatherSkillForItem(itemId);
        var hasSkill = npc?.Skills.Any(s => s.Code == skillCode || s.Code is "vein_divining" or "exotic_husbandry") ?? false;

        var baseDays = Math.Max(2, (quantity + 1) / 2);
        if (!hasLocalNode && itemId >= 25) baseDays += 2;
        if (hasLocalNode) baseDays = Math.Max(1, baseDays - 1);
        if (hasSkill) baseDays = Math.Max(1, baseDays - 1);

        return Math.Clamp(baseDays, 1, 6);
    }

    private static string ResolveGatherSkillForItem(long itemId) => itemId switch
    {
        5 or 15 or 16 or 25 or 29 or 31 => "khai_khoang",
        7 or 13 or 14 or 28 or 30 => "tam_duoc",
        8 or 27 or 32 => "thu_liep",
        _ => "che_tac"
    };

    // =========================================================================
    // MONTHLY TICK (WORKSHOP RENT & ARTISAN LABOUR)
    // =========================================================================

    public static long? GetWorkshopInputItemId(string tradeCode) => tradeCode switch
    {
        "luyen_kim" => 15, // Than đá -> Sắt (#5)
        "duc_dong" => 15,  // Than đá -> Đồng (#16)
        "nau_ruou" => 1,   // Gạo -> Rượu (#10)
        "det_gam" => 4,    // Lụa -> Gấm (#12)
        "tho_det" => 19,   // Cây giống dâu tằm / Tơ -> Vải (#11)
        "duoc_su" => 20,   // Dược thảo -> Thuốc (#7)
        _ => null          // Nghề khai thác thô sơ cấp (thợ mộc, nông phu, ngư dân, khai khoáng...)
    };

    public void OnMonth(GameDate date)
    {
        var workshops = AllWorkshops();
        foreach (var ws in workshops)
        {
            if (!ws.IsActive) continue;

            if (_wallet.Liquid >= ws.MonthlyRent)
            {
                _wallet.Charge(ws.MonthlyRent, JournalCodes.ToolFee, $"Tiền thuê {ws.NameVi} tháng {date.Month:00}/{date.Year}");
            }
            else
            {
                _log.Warn($"Không đủ tiền trả thuê xưởng {ws.NameVi} tháng {date}.");
            }

            if (ws.HiredArtisans > 0)
            {
                var trade = Trades.FirstOrDefault(t => t.Id == ws.TradeId);
                if (trade is null) continue;

                var requiredInputId = GetWorkshopInputItemId(trade.Code);
                var activeArtisans = ws.HiredArtisans;

                if (requiredInputId.HasValue)
                {
                    var availableInput = _bag.Quantity(ws.CitadelId, requiredInputId.Value);
                    activeArtisans = Math.Min(ws.HiredArtisans, availableInput);
                    if (activeArtisans <= 0)
                    {
                        var inputName = _items.Find(requiredInputId.Value)?.NameVi ?? $"Nguyên liệu #{requiredInputId.Value}";
                        _log.Warn($"Xưởng {ws.NameVi} đình trệ tháng {date.Month:00}/{date.Year}: thiếu nguyên liệu đầu vào [{inputName}] trong hành trang tại thành #{ws.CitadelId}.");
                        continue;
                    }
                }

                var totalSalary = activeArtisans * ArtisanMonthlySalary;
                if (_wallet.Liquid >= totalSalary)
                {
                    _wallet.Charge(totalSalary, JournalCodes.ToolFee, $"Lương thợ phụ {ws.NameVi} ({activeArtisans} người)");

                    if (requiredInputId.HasValue)
                    {
                        _bag.Remove(ws.CitadelId, requiredInputId.Value, activeArtisans);
                    }

                    var furnaceBonus = Math.Max(0, ws.FurnaceLevel - 1);
                    var yieldPerWorker = trade.BaseQuantity + furnaceBonus;
                    var totalYield = activeArtisans * yieldPerWorker;
                    _bag.Add(ws.CitadelId, trade.OutputItemId, totalYield);

                    var currentPractice = PracticeCount(trade.Id);
                    var newPractice = currentPractice + activeArtisans;
                    var newTier = Math.Min(MaxTier, newPractice / PracticesPerTier);

                    _db.Execute(
                        """
                        INSERT INTO pf_skill(trade_id, practice_count, tier) VALUES(@trade, @practice, @tier)
                        ON CONFLICT(trade_id) DO UPDATE SET practice_count = excluded.practice_count, tier = excluded.tier
                        """,
                        new DbArgs().Add("@trade", trade.Id).Add("@practice", newPractice).Add("@tier", newTier));

                    _log.Info($"Workshop {ws.NameVi} produced +{totalYield} items via {activeArtisans} artisans.");
                }
                else
                {
                    _log.Warn($"Không đủ trả lương {totalSalary} Quan cho thợ phụ {ws.NameVi}.");
                }
            }
        }

        _db.Execute(
            "UPDATE pf_order SET status = 'EXPIRED' WHERE deadline_day < @day AND status IN ('AVAILABLE', 'ACCEPTED')",
            DbArgs.One("@day", date.DayIndex));

        EnsureNpcOrders(date);
    }

    private void EnsureNpcOrders(GameDate date)
    {
        var activeCount = _db.Query(
            "SELECT COUNT(*) FROM pf_order WHERE COALESCE(direction, 'INBOUND') = 'INBOUND' AND status IN ('AVAILABLE', 'ACCEPTED')",
            row => row.GetInt32(0)).FirstOrDefault();

        if (activeCount >= 6) return;

        var needed = 6 - activeCount;
        var npcs = _npcs?.All;
        if (npcs is null || npcs.Count == 0) return;

        var seed = (uint)(date.DayIndex * 179 + activeCount * 31);
        var rng = new Random((int)seed);

        for (int i = 0; i < needed; i++)
        {
            var npc = npcs[rng.Next(npcs.Count)];
            long targetItem;
            int qty;
            int reward;
            int fame;
            string clientType;

            var role = npc.Role;
            if (role.Contains("Tướng") || role.Contains("Võ") || role.Contains("Hiệp"))
            {
                clientType = "MILITARY";
                var roll = rng.Next(3);
                if (roll == 0) { targetItem = 5; qty = 6 + rng.Next(6); reward = qty * 45; fame = 15; }
                else if (roll == 1) { targetItem = 8; qty = 8 + rng.Next(10); reward = qty * 30; fame = 12; }
                else { targetItem = 11; qty = 5 + rng.Next(5); reward = qty * 28; fame = 10; }
            }
            else if (role.Contains("Y") || role.Contains("Dược") || npc.Skills.Any(s => s.Code == "tam_duoc"))
            {
                clientType = "COMMERCE";
                if (rng.Next(2) == 0) { targetItem = 7; qty = 3 + rng.Next(4); reward = qty * 150; fame = 20; }
                else { targetItem = 13; qty = 2 + rng.Next(3); reward = qty * 260; fame = 25; }
            }
            else if (role.Contains("Mưu") || role.Contains("Sĩ") || role.Contains("Chủ") || role.Contains("Đế") || role.Contains("Thái"))
            {
                clientType = "COURT";
                var roll = rng.Next(4);
                if (roll == 0) { targetItem = 3; qty = 5 + rng.Next(6); reward = qty * 50; fame = 15; }
                else if (roll == 1) { targetItem = 4; qty = 3 + rng.Next(4); reward = qty * 120; fame = 20; }
                else if (roll == 2) { targetItem = 12; qty = 2 + rng.Next(3); reward = qty * 190; fame = 25; }
                else { targetItem = 14; qty = 2 + rng.Next(2); reward = qty * 230; fame = 30; }
            }
            else
            {
                clientType = "NOBLE";
                var roll = rng.Next(3);
                if (roll == 0) { targetItem = 1; qty = 10 + rng.Next(15); reward = qty * 18; fame = 8; }
                else if (roll == 1) { targetItem = 6; qty = 6 + rng.Next(8); reward = qty * 25; fame = 10; }
                else { targetItem = 2; qty = 8 + rng.Next(10); reward = qty * 12; fame = 8; }
            }

            var deadline = date.DayIndex + 45 + rng.Next(30);

            _db.Execute(
                """
                INSERT INTO pf_order (client_name_vi, client_type, citadel_id, target_item_id, quantity_needed, reward_copper, fame_reward, deadline_day, status)
                VALUES (@client, @type, @citadel, @item, @qty, @reward, @fame, @deadline, 'AVAILABLE')
                """,
                new DbArgs()
                    .Add("@client", npc.NameVi)
                    .Add("@type", clientType)
                    .Add("@citadel", npc.CitadelId)
                    .Add("@item", targetItem)
                    .Add("@qty", qty)
                    .Add("@reward", reward)
                    .Add("@fame", fame)
                    .Add("@deadline", deadline));
        }
    }
}
