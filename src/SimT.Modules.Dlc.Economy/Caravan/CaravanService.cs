using SimT.Contracts.Data;
using SimT.Contracts.Identity;
using SimT.Contracts.Ledger;
using SimT.Contracts.Logging;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Contracts;

namespace SimT.Modules.Dlc.Economy;

/// <summary>
/// Freight, trade routes, retainers escort, and logistics contracts.
/// Goods leave the current city's bag, travel for the road's duration and land in the destination city's bag.
/// Every unit of freight is paid for, so shipping is never a free teleport.
/// </summary>
internal sealed class CaravanService : ICaravanGuild
{
    public const long DefaultFreightPerUnit = 8;

    private readonly IDbSession _db;
    private readonly IGameClock _clock;
    private readonly IWallet _wallet;
    private readonly IBag _bag;
    private readonly IWorldMap _map;
    private readonly IItemCatalog _items;
    private readonly IPlayerState _player;
    private readonly IIdGenerator _ids;
    private readonly ILog _log;
    private readonly IActionTime _actionTime;
    private readonly long _freightPerUnit;
    private readonly INpcRegistry? _npcRegistry;

    public CaravanService(
        IDbSession db,
        IGameClock clock,
        IWallet wallet,
        IBag bag,
        IWorldMap map,
        IItemCatalog items,
        IPlayerState player,
        IIdGenerator ids,
        ILog log,
        IActionTime actionTime,
        long freightPerUnit = DefaultFreightPerUnit,
        INpcRegistry? npcRegistry = null)
    {
        _db = db;
        _clock = clock;
        _wallet = wallet;
        _bag = bag;
        _map = map;
        _items = items;
        _player = player;
        _ids = ids;
        _log = log;
        _actionTime = actionTime;
        _freightPerUnit = freightPerUnit > 0
            ? freightPerUnit
            : throw new SimException("caravan.config_invalid", $"freightPerUnit must be positive, got {freightPerUnit}.");
        _npcRegistry = npcRegistry;
    }

    public long FeePerUnit => _freightPerUnit;

    public CaravanFleetDef Fleet
    {
        get
        {
            var row = _db.QueryOne(
                "SELECT tier, name_vi, capacity_mult, cost_discount, defense_bonus, spoilage_reduction FROM cv_fleet WHERE id = 1",
                r => new CaravanFleetDef(r.GetInt32(0), r.GetString(1), r.GetDouble(2), r.GetDouble(3), r.GetDouble(4), r.GetDouble(5)));
            return row ?? new CaravanFleetDef(1, "Xe Thồ Mộc Cơ Bản", 1.0, 0.0, 0.0, 0.0);
        }
    }

    public IReadOnlyList<MarketRumorDef> Rumors
    {
        get
        {
            var today = _clock.Today.DayIndex;
            return _db.Query(
                """
                SELECT id, citadel_id, item_id, headline_vi, detail_vi, price_mult_permille, end_day
                FROM cv_market_rumor
                WHERE is_active = 1 AND start_day <= @day AND end_day >= @day
                ORDER BY id ASC
                """,
                row =>
                {
                    var id = row.GetInt64(0);
                    var cityId = row.GetInt64(1);
                    var itemId = row.GetInt64(2);
                    var headline = row.GetString(3);
                    var detail = row.GetString(4);
                    var mult = row.GetInt32(5);
                    var endDay = row.GetInt32(6);
                    var cityName = _map.Find(cityId)?.NameVi ?? $"Thành #{cityId}";
                    var itemName = _items.Find(itemId)?.NameVi ?? $"Vật phẩm #{itemId}";
                    var remaining = (int)Math.Max(0, endDay - today);
                    return new MarketRumorDef(id, cityId, cityName, itemId, itemName, headline, detail, mult, remaining);
                },
                DbArgs.One("@day", today));
        }
    }

    public int GetRumorPriceFactorPermille(long citadelId, long itemId)
    {
        var factor = _db.ScalarLong(
            """
            SELECT price_mult_permille
            FROM cv_market_rumor
            WHERE citadel_id = @city AND item_id = @item AND is_active = 1 AND start_day <= @day AND end_day >= @day
            ORDER BY id DESC LIMIT 1
            """,
            new DbArgs().Add("@city", citadelId).Add("@item", itemId).Add("@day", _clock.Today.DayIndex));
        return factor > 0 ? (int)factor : 1000;
    }

    public CaravanCrewDef Crew
    {
        get
        {
            var rows = _db.Query(
                "SELECT escort_npc_id, accountant_npc_id, navigator_npc_id FROM cv_fleet_crew WHERE id = 1",
                r => (
                    EscortId: r.IsDbNull(0) ? (long?)null : r.GetInt64(0),
                    AccountantId: r.IsDbNull(1) ? (long?)null : r.GetInt64(1),
                    NavigatorId: r.IsDbNull(2) ? (long?)null : r.GetInt64(2)
                ));

            if (rows.Count == 0)
            {
                return new CaravanCrewDef(null, null, 0, null, null, 0, null, null, 0);
            }

            var row = rows[0];

            var escortNpc = row.EscortId.HasValue ? _npcRegistry?.Find(row.EscortId.Value) : null;
            var acctNpc = row.AccountantId.HasValue ? _npcRegistry?.Find(row.AccountantId.Value) : null;
            var navNpc = row.NavigatorId.HasValue ? _npcRegistry?.Find(row.NavigatorId.Value) : null;

            return new CaravanCrewDef(
                row.EscortId,
                escortNpc?.NameVi,
                escortNpc?.Attributes.War ?? 0,
                row.AccountantId,
                acctNpc?.NameVi,
                acctNpc?.Attributes.Com ?? 0,
                row.NavigatorId,
                navNpc?.NameVi,
                navNpc?.Attributes.Log ?? 0);
        }
    }

    public CaravanResult AssignCrew(string role, long? npcId)
    {
        var normRole = role?.Trim().ToLowerInvariant() switch
        {
            "escort" or "tieu_dau" or "tieudau" => "escort",
            "accountant" or "chuong_quy" or "chuongquy" => "accountant",
            "navigator" or "dan_duong" or "danduong" => "navigator",
            _ => null
        };

        if (normRole is null)
        {
            return CaravanResult.Failure($"Chức vụ không hợp lệ: '{role}'. Hợp lệ: escort (Tiêu Đầu), accountant (Chưởng Quỹ), navigator (Dẫn Đường).");
        }

        string roleTitleVi = normRole switch
        {
            "escort" => "Tiêu Đầu",
            "accountant" => "Chưởng Quỹ",
            _ => "Dẫn Đường"
        };

        var col = normRole switch
        {
            "escort" => "escort_npc_id",
            "accountant" => "accountant_npc_id",
            _ => "navigator_npc_id"
        };

        if (npcId.HasValue)
        {
            var npc = _npcRegistry?.Find(npcId.Value);
            if (npc is null)
            {
                return CaravanResult.Failure($"Không tìm thấy nhân vật có ID {npcId.Value}.");
            }

            _db.Execute(
                $"""
                INSERT INTO cv_fleet_crew (id, {col}) VALUES (1, @npcId)
                ON CONFLICT(id) DO UPDATE SET {col} = @npcId
                """,
                DbArgs.One("@npcId", npcId.Value));

            var statInfo = normRole switch
            {
                "escort" => $"Võ lực {npc.Attributes.War} (giảm nguy cơ cướp đường và áp tải an toàn)",
                "accountant" => $"Thương tài {npc.Attributes.Com} (giảm phí cước vận tải lên tới 25%)",
                _ => $"Hậu cần {npc.Attributes.Log} (rút ngắn thời gian di chuyển đường xa 1 ngày)"
            };

            return CaravanResult.Success($"Đã bổ nhiệm {npc.NameVi} làm {roleTitleVi} cho thương đội ({statInfo}).");
        }
        else
        {
            _db.Execute(
                $"""
                INSERT INTO cv_fleet_crew (id, {col}) VALUES (1, NULL)
                ON CONFLICT(id) DO UPDATE SET {col} = NULL
                """);

            return CaravanResult.Success($"Đã bãi nhiệm chức vụ {roleTitleVi} của thương đội.");
        }
    }

    public CaravanResult UpgradeFleet()
    {
        var fleet = Fleet;
        if (fleet.Tier >= 3)
        {
            return CaravanResult.Failure("Thương đội đã đạt đẳng cấp tối cao (Đại Vận Thuyền).");
        }

        var nextTier = fleet.Tier + 1;
        var cost = nextTier == 2 ? 600 : 1500;
        var nextName = nextTier == 2 ? "Thiết Xa Bọc Thép" : "Đại Vận Thuyền Sông";
        var capMult = nextTier == 2 ? 1.5 : 2.0;
        var costDiscount = nextTier == 2 ? 0.20 : 0.35;
        var defBonus = nextTier == 2 ? 0.35 : 0.60;
        var spoilRed = nextTier == 2 ? 0.25 : 0.50;

        if (_wallet.Liquid < cost)
        {
            return CaravanResult.Failure($"Cần {cost} Quan để nâng cấp lên {nextName}, chỉ có {_wallet.Liquid}.");
        }

        _db.Transaction(() =>
        {
            _wallet.Charge(cost, JournalCodes.CaravanFreight, $"Nâng cấp thương đội lên {nextName}");
            _db.Execute(
                """
                INSERT INTO cv_fleet (id, tier, name_vi, capacity_mult, cost_discount, defense_bonus, spoilage_reduction)
                VALUES (1, @tier, @name, @cap, @costDisc, @def, @spoil)
                ON CONFLICT(id) DO UPDATE SET
                    tier = excluded.tier,
                    name_vi = excluded.name_vi,
                    capacity_mult = excluded.capacity_mult,
                    cost_discount = excluded.cost_discount,
                    defense_bonus = excluded.defense_bonus,
                    spoilage_reduction = excluded.spoilage_reduction
                """,
                new DbArgs()
                    .Add("@tier", nextTier)
                    .Add("@name", nextName)
                    .Add("@cap", capMult)
                    .Add("@costDisc", costDiscount)
                    .Add("@def", defBonus)
                    .Add("@spoil", spoilRed));
        });

        return CaravanResult.Success($"Đã nâng cấp thương đội lên {nextName}! Giảm {(int)(costDiscount * 100)}% cước vận, giảm {(int)(spoilRed * 100)}% hao hụt và tăng phòng ngự trước cướp đường.");
    }

    public IReadOnlyList<CaravanRunSummary> Runs =>
        _db.Query(
            """
            SELECT r.id, r.from_city, r.to_city, r.depart_day, r.arrive_day, r.status,
                   COALESCE((SELECT SUM(quantity) FROM cv_cargo c WHERE c.run_id = r.id), 0),
                   COALESCE((SELECT SUM(lost) FROM cv_cargo c WHERE c.run_id = r.id), 0),
                   r.escort_npc_id,
                   r.ambush_loss
            FROM cv_run r
            ORDER BY r.id DESC
            LIMIT 20
            """,
            row => new CaravanRunSummary(
                row.GetInt64(0),
                row.GetInt64(1),
                row.GetInt64(2),
                row.GetInt32(3),
                row.GetInt32(4),
                row.GetString(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.IsDbNull(8) ? null : row.GetInt64(8),
                row.GetInt32(9)));

    public IReadOnlyList<FreightContractDef> Contracts =>
        _db.Query(
            """
            SELECT id, guild_name, from_city, to_city, item_id, quantity, reward_copper, deadline_day, status, accepted_day, completed_day
            FROM cv_contract
            ORDER BY id ASC
            """,
            row => new FreightContractDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetString(8),
                row.IsDbNull(9) ? null : row.GetInt32(9),
                row.IsDbNull(10) ? null : row.GetInt32(10)));

    public int UnitsInTransit =>
        (int)_db.ScalarLong(
            "SELECT COALESCE(SUM(c.quantity), 0) FROM cv_cargo c JOIN cv_run r ON r.id = c.run_id WHERE r.status = 'RUNNING'");

    public CaravanResult Send(long fromCity, long toCity) =>
        SendWithEscort(fromCity, toCity, null);

    public CaravanResult SendWithEscort(long fromCity, long toCity, long? escortNpcId)
    {
        var baseDays = _map.TravelDays(fromCity, toCity);
        if (baseDays <= 0)
        {
            return CaravanResult.Failure("Không có đường thương giữa hai thành này.");
        }

        var rows = _bag.Rows(fromCity).Where(row => row.Quantity > 0).ToList();
        if (rows.Count == 0)
        {
            return CaravanResult.Failure("Túi trống, không có gì để gửi.");
        }

        var crew = Crew;
        var effectiveEscort = escortNpcId ?? crew.EscortNpcId;

        // Navigator bonus: Log >= 60 reduces route time by 1 day for longer journeys (>= 3 days)
        var days = (crew.NavigatorLog >= 60 && baseDays >= 3) ? Math.Max(1, baseDays - 1) : baseDays;

        var units = rows.Sum(row => row.Quantity);
        var baseFee = units * _freightPerUnit;
        var fleet = Fleet;

        // Fleet discount + Accountant bonus (up to 25% discount based on Com stat)
        var fleetDiscountPermille = (int)(fleet.CostDiscount * 1000);
        var acctDiscountPermille = crew.AccountantCom > 0 ? Math.Min(250, crew.AccountantCom * 250 / 100) : 0;
        var totalDiscountPermille = Math.Min(500, fleetDiscountPermille + acctDiscountPermille);
        var fee = Math.Max(1, baseFee * (1000 - totalDiscountPermille) / 1000);

        if (_wallet.Liquid < fee)
        {
            return CaravanResult.Failure($"Cần {fee} Quan cước, chỉ có {_wallet.Liquid}.");
        }

        var departDay = _clock.Today.DayIndex;
        var arriveDay = departDay + days;

        _db.Transaction(() =>
        {
            _actionTime.Charge(ActionCodes.CaravanSend);
            _wallet.Charge(fee, JournalCodes.CaravanFreight, $"Cước {units} đơn vị về thành {toCity}");

            foreach (var row in rows)
            {
                if (!_bag.Remove(fromCity, row.ItemId, row.Quantity))
                {
                    throw new SimException("caravan.bag_race", "Bag changed while loading the caravan; rolled back.");
                }
            }

            var runId = _ids.Next("caravan.run");
            _db.Execute(
                """
                INSERT INTO cv_run(id, from_city, to_city, depart_day, arrive_day, fee_paid, status, escort_npc_id, ambush_loss)
                VALUES(@id, @from, @to, @depart, @arrive, @fee, 'RUNNING', @escort, 0)
                """,
                new DbArgs()
                    .Add("@id", runId)
                    .Add("@from", fromCity)
                    .Add("@to", toCity)
                    .Add("@depart", departDay)
                    .Add("@arrive", arriveDay)
                    .Add("@fee", fee)
                    .Add("@escort", (object?)effectiveEscort ?? DBNull.Value));

            foreach (var row in rows)
            {
                _db.Execute(
                    "INSERT INTO cv_cargo(run_id, item_id, quantity) VALUES(@run, @item, @quantity)",
                    new DbArgs().Add("@run", runId).Add("@item", row.ItemId).Add("@quantity", row.Quantity));
            }
        });

        var escortText = effectiveEscort.HasValue ? " (có tiêu đầu hộ vệ)" : string.Empty;
        var navText = (days < baseDays) ? " (Dẫn Đường rút ngắn 1 ngày)" : string.Empty;
        return CaravanResult.Success(
            $"Đoàn {units} đơn vị lên đường{escortText}{navText}, tới sau {days} ngày (ngày {GameDate.FromDayIndex(arriveDay)}), cước {fee} Quan.");
    }

    public CaravanResult SendWithEscortFee(long fromCity, long toCity, int escortFee)
    {
        if (escortFee > 0 && _wallet.Liquid < escortFee)
        {
            return CaravanResult.Failure($"Cần {escortFee} Quan thuê tiêu sư, chỉ có {_wallet.Liquid}.");
        }

        if (escortFee > 0)
        {
            _wallet.Charge(escortFee, JournalCodes.CaravanFreight, "Tiền thuê tiêu sư áp tải bảo vệ hàng");
        }

        return SendWithEscort(fromCity, toCity, escortFee > 0 ? 999999L : null);
    }

    public CaravanResult Travel(long fromCity, long toCity, IPlayerState player)
    {
        var baseDays = _map.TravelDays(fromCity, toCity);
        if (baseDays <= 0)
        {
            return CaravanResult.Failure("Không có đường đi giữa hai thành này.");
        }

        var crew = Crew;
        var days = (crew.NavigatorLog >= 60 && baseDays >= 3) ? Math.Max(1, baseDays - 1) : baseDays;

        var incidentLog = string.Empty;

        _db.Transaction(() =>
        {
            _clock.Advance(days);
            player.MoveTo(toCity, _clock.Today);

            // Escort active freight contracts cargo from starting city to destination city
            var activeContracts = _db.Query(
                "SELECT id, item_id, quantity FROM cv_contract WHERE status = 'IN_TRANSIT' AND from_city = @from AND to_city = @to",
                row => (Id: row.GetInt64(0), ItemId: row.GetInt64(1), Quantity: row.GetInt32(2)),
                new DbArgs().Add("@from", fromCity).Add("@to", toCity));

            foreach (var c in activeContracts)
            {
                if (_bag.Quantity(fromCity, c.ItemId) >= c.Quantity)
                {
                    _bag.Remove(fromCity, c.ItemId, c.Quantity);
                    _bag.Add(toCity, c.ItemId, c.Quantity);
                }
            }

            // Travel encounter check on multi-day journeys (flavor / road hazard notice)
            if (days >= 2)
            {
                var seed = (int)(fromCity * 17 + toCity * 31 + _clock.Today.DayIndex);
                var rng = new Random(seed);
                var roll = rng.NextDouble();
                if (roll < 0.20)
                {
                    if (crew.EscortNpcId.HasValue && crew.EscortWar >= 60)
                    {
                        incidentLog = $" Trên đường đi qua hẻm núi gặp toán sơn tặc chặn đường, Tiêu Đầu {crew.EscortNameVi} rút đao uy dũng hét vang, lũ cướp khiếp sợ tháo lui tán loạn!";
                    }
                    else
                    {
                        incidentLog = " Trên đường thiên lý gặp toán thổ phỉ chặn đường, bạn cùng đoàn tùy tùng cảnh giác đề phòng, thuận lợi kích lui chúng và tiếp tục lộ trình.";
                    }
                }
                else if (roll < 0.35)
                {
                    if (crew.AccountantNpcId.HasValue && crew.AccountantCom >= 60)
                    {
                        incidentLog = $" Qua trạm tuần kiểm quan ải, Chưởng Quỹ {crew.AccountantNameVi} am hiểu quan lộ xuất trình thông quan văn điệp, được quan lại tạo điều kiện đi nhanh.";
                    }
                    else
                    {
                        incidentLog = " Đoàn xe ngang qua dịch trạm quan phương, dừng chân tiếp tế lương thảo nước uống rồi tiếp tục lên đường.";
                    }
                }
                else if (roll < 0.50 && days < baseDays)
                {
                    incidentLog = $" Nhờ Dẫn Đường {crew.NavigatorNameVi} chỉ lối đường mòn hiểm trở, đoàn xe đã vượt quan ải sớm 1 ngày!";
                }
            }
        });

        return CaravanResult.Success($"Đi {days} ngày, tới nơi ngày {_clock.Today}.{incidentLog}");
    }

    public CaravanResult AcceptContract(long contractId)
    {
        var contract = _db.QueryOne(
            "SELECT id, guild_name, from_city, to_city, item_id, quantity, reward_copper, deadline_day, status, accepted_day, completed_day FROM cv_contract WHERE id = @id",
            row => new FreightContractDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetString(8),
                row.IsDbNull(9) ? null : row.GetInt32(9),
                row.IsDbNull(10) ? null : row.GetInt32(10)
            ),
            DbArgs.One("@id", contractId));

        if (contract is null)
        {
            return CaravanResult.Failure("Hợp đồng không tồn tại.");
        }

        if (contract.Status != "AVAILABLE")
        {
            return CaravanResult.Failure("Hợp đồng này không còn khả dụng hoặc đã được tiếp nhận.");
        }

        if (_player.CurrentCityId != contract.FromCityId)
        {
            var fromName = _map.Find(contract.FromCityId)?.NameVi ?? contract.FromCityId.ToString();
            return CaravanResult.Failure($"Hợp đồng này yêu cầu tiếp nhận tại {fromName}.");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE cv_contract SET status = 'IN_TRANSIT', accepted_day = @day WHERE id = @id",
                new DbArgs().Add("@id", contract.Id).Add("@day", _clock.Today.DayIndex));

            _bag.Add(contract.FromCityId, contract.ItemId, contract.Quantity);
        });

        var destName = _map.Find(contract.ToCityId)?.NameVi ?? contract.ToCityId.ToString();
        var itemName = _items.Find(contract.ItemId)?.NameVi ?? $"Vật phẩm #{contract.ItemId}";
        return CaravanResult.Success(
            $"Đã nhận ủy thác từ {contract.GuildName}. Vận chuyển {contract.Quantity} {itemName} tới thành {destName} trước ngày {GameDate.FromDayIndex(contract.DeadlineDayIndex)} (thù lao {contract.RewardCopper} Quan).");
    }

    public CaravanResult DeliverContract(long contractId)
    {
        var contract = _db.QueryOne(
            "SELECT id, guild_name, from_city, to_city, item_id, quantity, reward_copper, deadline_day, status, accepted_day, completed_day FROM cv_contract WHERE id = @id",
            row => new FreightContractDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetInt64(2),
                row.GetInt64(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.GetInt32(6),
                row.GetInt32(7),
                row.GetString(8),
                row.IsDbNull(9) ? null : row.GetInt32(9),
                row.IsDbNull(10) ? null : row.GetInt32(10)
            ),
            DbArgs.One("@id", contractId));

        if (contract is null)
        {
            return CaravanResult.Failure("Hợp đồng không tồn tại.");
        }

        if (contract.Status != "IN_TRANSIT")
        {
            return CaravanResult.Failure("Hợp đồng này chưa được nhận hoặc đã hoàn tất.");
        }

        if (_player.CurrentCityId != contract.ToCityId)
        {
            var destName = _map.Find(contract.ToCityId)?.NameVi ?? contract.ToCityId.ToString();
            return CaravanResult.Failure($"Chưa tới đích để giao hàng. Điểm bàn giao là {destName}.");
        }

        var availableInBag = _bag.Quantity(contract.ToCityId, contract.ItemId);
        if (availableInBag < contract.Quantity)
        {
            var itemName = _items.Find(contract.ItemId)?.NameVi ?? "hàng hóa";
            return CaravanResult.Failure($"Không đủ số lượng {itemName} trong túi để bàn giao (cần {contract.Quantity}, hiện có {availableInBag}).");
        }

        var onTime = _clock.Today.DayIndex <= contract.DeadlineDayIndex;
        var payout = onTime ? contract.RewardCopper : Math.Max(10, contract.RewardCopper / 2);

        _db.Transaction(() =>
        {
            if (!_bag.Remove(contract.ToCityId, contract.ItemId, contract.Quantity))
            {
                throw new SimException("caravan.deliver_bag", "Failed to remove contract goods from bag.");
            }

            _wallet.Pay(payout, JournalCodes.CaravanReward, $"Thù lao vận tiêu hợp đồng {contract.Id} ({contract.GuildName})");

            _db.Execute(
                "UPDATE cv_contract SET status = 'DELIVERED', completed_day = @day WHERE id = @id",
                new DbArgs().Add("@id", contract.Id).Add("@day", _clock.Today.DayIndex));
        });

        return onTime
            ? CaravanResult.Success($"Giao tiêu thành công cho {contract.GuildName}! Nhận đủ {payout} Quan thù lao đúng hạn.")
            : CaravanResult.Success($"Giao tiêu trễ hạn cho {contract.GuildName}! Bị trừ 50% tiền cước, nhận {payout} Quan.");
    }

    /// <summary>Arrival hook: runs on every day, marks a run arrived exactly once.</summary>
    public void OnDay(GameDate date)
    {
        // Expire outdated market rumors
        _db.Execute(
            "UPDATE cv_market_rumor SET is_active = 0 WHERE is_active = 1 AND end_day < @day",
            DbArgs.One("@day", date.DayIndex));

        // Expire unaccepted deadline-passed contracts
        _db.Execute(
            "UPDATE cv_contract SET status = 'EXPIRED' WHERE status = 'AVAILABLE' AND deadline_day < @day",
            DbArgs.One("@day", date.DayIndex));

        var due = _db.Query(
            "SELECT id, to_city, depart_day, arrive_day, escort_npc_id FROM cv_run WHERE status = 'RUNNING' AND arrive_day <= @day ORDER BY id",
            row => (
                Id: row.GetInt64(0),
                ToCity: row.GetInt64(1),
                Depart: row.GetInt32(2),
                Arrive: row.GetInt32(3),
                EscortNpcId: row.IsDbNull(4) ? (long?)null : row.GetInt64(4)
            ),
            DbArgs.One("@day", date.DayIndex));

        foreach (var run in due)
        {
            var transitDays = Math.Max(1, run.Arrive - run.Depart);
            var fleet = Fleet;

            _db.Transaction(() =>
            {
                var moved = _db.Execute(
                    "UPDATE cv_run SET status = 'ARRIVED' WHERE id=@id AND status = 'RUNNING'",
                    DbArgs.One("@id", run.Id));

                if (moved == 0)
                {
                    return;
                }

                var cargo = _db.Query(
                    "SELECT item_id, quantity FROM cv_cargo WHERE run_id=@id ORDER BY item_id",
                    row => (ItemId: row.GetInt64(0), Quantity: row.GetInt32(1)),
                    DbArgs.One("@id", run.Id));

                var totalUnits = cargo.Sum(c => c.Quantity);
                var ambushLoss = 0;

                // Ambush check: If unescorted and route takes >= 2 days, bandits may ambush
                if (!run.EscortNpcId.HasValue && transitDays >= 2)
                {
                    var ambushChance = transitDays >= 3 ? 0.35 : 0.20;
                    var rng = new Random((int)(run.Id * 31 + run.Depart));
                    if (rng.NextDouble() < ambushChance)
                    {
                        var lossRate = Math.Max(0.05, 0.20 * (1.0 - fleet.DefenseBonus));
                        ambushLoss = Math.Max(1, (int)(totalUnits * lossRate));
                        _db.Execute(
                            "UPDATE cv_run SET ambush_loss = @loss WHERE id = @id",
                            new DbArgs().Add("@id", run.Id).Add("@loss", ambushLoss));
                    }
                }

                var remainingAmbush = ambushLoss;
                foreach (var line in cargo)
                {
                    var lost = Spoilage(line.ItemId, line.Quantity, transitDays, fleet.SpoilageReduction);
                    var lineAmbushLoss = 0;
                    if (remainingAmbush > 0)
                    {
                        lineAmbushLoss = Math.Min(line.Quantity - lost, remainingAmbush);
                        remainingAmbush -= lineAmbushLoss;
                    }

                    var totalLost = lost + lineAmbushLoss;
                    if (totalLost > 0)
                    {
                        _db.Execute(
                            "UPDATE cv_cargo SET lost=@lost WHERE run_id=@run AND item_id=@item",
                            new DbArgs().Add("@run", run.Id).Add("@item", line.ItemId).Add("@lost", totalLost));
                    }

                    var delivered = line.Quantity - totalLost;
                    if (delivered > 0)
                    {
                        _bag.Add(run.ToCity, line.ItemId, delivered);
                    }
                }
            });

            _log.Info($"caravan {run.Id} arrived at citadel {run.ToCity} on {date}");
        }
    }

    /// <summary>
    /// Freight loss: perishables lose transitDays / shelfLife of their quantity on the road,
    /// factoring in fleet preservation upgrades.
    /// </summary>
    private int Spoilage(long itemId, int quantity, int transitDays, double reduction = 0.0)
    {
        var item = _items.Find(itemId);
        if (item is null || item.ShelfLifeDays <= 0 || quantity <= 0)
        {
            return 0;
        }

        var baseLost = (long)quantity * transitDays / item.ShelfLifeDays;
        var lost = (int)(baseLost * (1.0 - Math.Clamp(reduction, 0.0, 0.85)));
        return Math.Min(lost, quantity - 1);
    }
}
