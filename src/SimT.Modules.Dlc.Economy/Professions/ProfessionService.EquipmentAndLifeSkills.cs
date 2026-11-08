using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Economy;

internal sealed partial class ProfessionService
{
    // =========================================================================
    // PLAYER EQUIPMENT & COMBAT SLOTS
    // =========================================================================

    public IReadOnlyList<EquippedSlotDef> EquippedSlots => Slots;

    public IReadOnlyList<EquippedSlotDef> Slots =>
        _db.Query(
            """
            SELECT s.slot_code, s.equipment_id, e.name_vi, e.item_id, e.citadel_id, e.durability, e.max_durability, e.quality_rank, e.bonus_stat, e.bonus_value, COALESCE(e.temper_level, 0),
                   e.creator_id, e.creator_name_vi, e.forged_citadel_id, e.forged_day_index, COALESCE(e.fame_score, 0), COALESCE(e.kill_count, 0), e.legend_narrative, e.lot_code
            FROM pf_equipped_slot s
            LEFT JOIN pf_equipment e ON e.id = s.equipment_id
            ORDER BY s.slot_code DESC
            """,
            row =>
            {
                var slotCode = row.GetString(0);
                var slotName = slotCode switch
                {
                    "WEAPON" => "Binh Khí",
                    "ARMOR" => "Hộ Giáp",
                    _ => "Khí Cụ"
                };

                EquipmentDef? equip = null;
                if (!row.IsDbNull(1))
                {
                    equip = new EquipmentDef(
                        row.GetInt64(1),
                        row.GetString(2),
                        row.GetInt64(3),
                        row.GetInt64(4),
                        row.GetInt32(5),
                        row.GetInt32(6),
                        row.GetString(7),
                        row.GetString(8),
                        row.GetInt32(9),
                        row.GetInt32(10),
                        row.IsDbNull(11) ? null : row.GetInt64(11),
                        row.IsDbNull(12) ? null : row.GetString(12),
                        row.IsDbNull(13) ? null : row.GetInt64(13),
                        row.IsDbNull(14) ? null : row.GetInt64(14),
                        row.GetInt32(15),
                        row.GetInt32(16),
                        row.IsDbNull(17) ? null : row.GetString(17),
                        row.IsDbNull(18) ? null : row.GetString(18));
                }

                return new EquippedSlotDef(slotCode, slotName, row.IsDbNull(1) ? null : row.GetInt64(1), equip);
            });

    public EquippedSlotDef? GetSlot(string slotCode) =>
        Slots.FirstOrDefault(s => s.SlotCode.Equals(slotCode, StringComparison.OrdinalIgnoreCase));

    public static int GetEffectiveEquipmentBonus(EquipmentDef? equip)
    {
        if (equip is null || equip.Durability <= 0)
        {
            return 0;
        }

        if (equip.MaxDurability > 0 && equip.Durability * 2 < equip.MaxDurability)
        {
            return Math.Max(1, equip.BonusValue / 2);
        }

        return equip.BonusValue;
    }

    public static string FormatDurabilityStateVi(EquipmentDef? equip)
    {
        if (equip is null) return "—";
        if (equip.Durability <= 0) return "💥 Gãy vỡ (0% hiệu năng)";
        if (equip.MaxDurability > 0 && equip.Durability * 2 < equip.MaxDurability)
            return "⚠️ Mẻ lưỡi (-50% hiệu năng)";
        return "✨ Sắc bén (100% hiệu năng)";
    }

    public int GetTotalBonus(string statCode)
    {
        var total = 0;
        foreach (var slot in Slots)
        {
            if (slot.Equipment is not null && slot.Equipment.Durability > 0)
            {
                if (slot.Equipment.BonusStat.Equals(statCode, StringComparison.OrdinalIgnoreCase))
                {
                    total += GetEffectiveEquipmentBonus(slot.Equipment);
                }
            }
        }
        return total;
    }

    public int GetEquippedStatBonus(string statCode) => GetTotalBonus(statCode);

    public WorkResult Equip(long equipmentId)
    {
        var equip = FindEquipment(equipmentId);
        if (equip is null)
        {
            return WorkResult.Failure("Không tìm thấy trang bị này.");
        }

        var slotCode = ResolveSlotForEquipment(equip);
        var slotName = slotCode switch
        {
            "WEAPON" => "Binh Khí",
            "ARMOR" => "Hộ Giáp",
            _ => "Khí Cụ"
        };

        _db.Transaction(() =>
        {
            _db.Execute(
                """
                INSERT INTO pf_equipped_slot(slot_code, equipment_id, equipped_at_day)
                VALUES(@slot, @id, @day)
                ON CONFLICT(slot_code) DO UPDATE SET equipment_id = excluded.equipment_id, equipped_at_day = excluded.equipped_at_day
                """,
                new DbArgs().Add("@slot", slotCode).Add("@id", equipmentId).Add("@day", _clock.Today.DayIndex));
        });

        return WorkResult.Success($"Đã trang bị {equip.NameVi} vào vị trí {slotName} (+{equip.BonusValue} {equip.BonusStat}).", 1, 0);
    }

    public WorkResult Unequip(string slotCode)
    {
        var slot = GetSlot(slotCode);
        if (slot is null || slot.EquipmentId is null)
        {
            return WorkResult.Failure("Vị trí này hiện đang để trống.");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE pf_equipped_slot SET equipment_id = NULL WHERE slot_code = @slot",
                DbArgs.One("@slot", slot.SlotCode));
        });

        return WorkResult.Success($"Đã gỡ trang bị ở vị trí {slot.SlotNameVi}.", 1, 0);
    }

    public void DegradeEquipped(string slotCode, int loss)
    {
        if (loss <= 0) return;
        var slot = GetSlot(slotCode);
        if (slot is not null && slot.EquipmentId.HasValue)
        {
            var reduction = 0;
            if (HasPerk("eternal_edge"))
            {
                reduction += 1;
            }
            var maintLvl = _attributes?.GetEquippedSkillLevel("weapon_maintenance") ?? 0;
            if (maintLvl > 0)
            {
                reduction += (maintLvl + 1) / 2;
            }

            var effectiveLoss = loss > 1 ? Math.Max(1, loss - reduction) : loss;
            _db.Execute(
                "UPDATE pf_equipment SET durability = MAX(0, durability - @loss) WHERE id = @id",
                new DbArgs().Add("@loss", effectiveLoss).Add("@id", slot.EquipmentId.Value));
        }
    }

    private static string ResolveSlotForEquipment(EquipmentDef equip)
    {
        if (equip.BonusStat == "WAR")
        {
            var name = equip.NameVi.ToLowerInvariant();
            if (name.Contains("giáp") || name.Contains("tháp") || name.Contains("khôi"))
            {
                return "ARMOR";
            }
            return "WEAPON";
        }
        return "TOOL";
    }

    // =========================================================================
    // MASTERY PERKS & TALENTS
    // =========================================================================

    public IReadOnlyList<MasteryPerkDef> Perks =>
        _db.Query(
            """
            SELECT id, code, name_vi, description_vi, required_fame, is_unlocked
            FROM pf_mastery_perk
            ORDER BY required_fame ASC
            """,
            row => new MasteryPerkDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetString(3),
                row.GetInt32(4),
                row.GetInt64(5) != 0));

    public bool HasPerk(string code) =>
        _db.ScalarLong("SELECT COUNT(*) FROM pf_mastery_perk WHERE code = @code AND is_unlocked = 1", DbArgs.One("@code", code)) > 0;

    public WorkResult UnlockPerk(long perkId)
    {
        var perk = Perks.FirstOrDefault(p => p.Id == perkId);
        if (perk is null)
        {
            return WorkResult.Failure("Không tìm thấy bí kỹ này.");
        }

        if (perk.IsUnlocked)
        {
            return WorkResult.Failure($"Bí kỹ '{perk.NameVi}' đã được giác ngộ.");
        }

        if (TotalGuildFame < perk.RequiredFame)
        {
            return WorkResult.Failure($"Chưa đủ danh vọng phường nghề (cần {perk.RequiredFame} điểm, hiện có {TotalGuildFame}).");
        }

        _db.Transaction(() =>
        {
            _db.Execute(
                "UPDATE pf_mastery_perk SET is_unlocked = 1 WHERE id = @id",
                DbArgs.One("@id", perkId));
        });

        return WorkResult.Success($"Giác ngộ thành công bí kỹ '{perk.NameVi}'! {perk.DescriptionVi}", 1, 0);
    }

    // =========================================================================
    // DEEP RPG LIFE SKILLS (COOKING, FISHING, FORAGING, HUNTING, MINING)
    // =========================================================================

    public IReadOnlyList<DishRecipeDef> Dishes =>
        _db.Query(
            """
            SELECT d.id, d.code, d.name_vi, d.description_vi, d.primary_item_id, d.primary_qty,
                   d.secondary_item_id, d.secondary_qty, d.fatigue_relief, d.stress_relief, d.health_gain,
                   d.cook_fee, COALESCE(inv.quantity, 0)
            FROM pf_dish d
            LEFT JOIN pf_dish_inventory inv ON inv.dish_id = d.id
            ORDER BY d.id ASC
            """,
            row => new DishRecipeDef(
                row.GetInt64(0),
                row.GetString(1),
                row.GetString(2),
                row.GetString(3),
                row.GetInt64(4),
                row.GetInt32(5),
                row.IsDbNull(6) ? null : row.GetInt64(6),
                row.GetInt32(7),
                row.GetInt32(8),
                row.GetInt32(9),
                row.GetInt32(10),
                row.GetInt64(11),
                row.GetInt32(12)));

    public WorkResult Cook(long citadelId, long dishId)
    {
        var dish = Dishes.FirstOrDefault(d => d.Id == dishId);
        if (dish is null)
            return WorkResult.Failure("Không tìm thấy thực đơn món ăn.");

        if (_wallet.Liquid < dish.CookFee)
            return WorkResult.Failure($"Không đủ tiền mua gia vị, củi lửa ({dish.CookFee} Quan).");

        var primaryItem = _items.Find(dish.PrimaryItemId);
        if (primaryItem is null)
            return WorkResult.Failure("Vật phẩm nguyên liệu không tồn tại.");

        var hasPrimary = _bag.Quantity(citadelId, dish.PrimaryItemId);
        if (hasPrimary < dish.PrimaryQuantity)
            return WorkResult.Failure($"Thiếu nguyên liệu chính: Cần {dish.PrimaryQuantity} {primaryItem.NameVi} (hiện có {hasPrimary}).");

        if (dish.SecondaryItemId.HasValue && dish.SecondaryQuantity > 0)
        {
            var secondaryItem = _items.Find(dish.SecondaryItemId.Value);
            var hasSecondary = _bag.Quantity(citadelId, dish.SecondaryItemId.Value);
            if (hasSecondary < dish.SecondaryQuantity)
                return WorkResult.Failure($"Thiếu nguyên liệu phụ: Cần {dish.SecondaryQuantity} {secondaryItem?.NameVi ?? "phụ gia"} (hiện có {hasSecondary}).");
        }

        _bag.Remove(citadelId, dish.PrimaryItemId, dish.PrimaryQuantity);
        if (dish.SecondaryItemId.HasValue && dish.SecondaryQuantity > 0)
        {
            _bag.Remove(citadelId, dish.SecondaryItemId.Value, dish.SecondaryQuantity);
        }

        _wallet.Charge(dish.CookFee, JournalCodes.ToolFee, $"Nấu món {dish.NameVi}");

        _db.Execute(
            """
            INSERT INTO pf_dish_inventory (dish_id, quantity)
            VALUES (@id, 1)
            ON CONFLICT(dish_id) DO UPDATE SET quantity = quantity + 1
            """,
            DbArgs.One("@id", dish.Id));

        (bool Awakened, bool LeveledUp, int Level) practice = (false, false, 1);
        if (_attributes is not null)
        {
            practice = _attributes.PracticeSkill("am_thuc", 25);
            _attributes.AddExp("CRA", 15);
            _attributes.AddExp("INT", 10);
        }

        var milestoneMsg = FormatSkillProgressMessage("Ẩm Thực", practice);
        _log.Info($"dlc.professions: cooked {dish.Code}: {dish.NameVi}");

        return WorkResult.Success($"Nấu thành công 1 phần {dish.NameVi} thơm ngon!{milestoneMsg}", 1, 0);
    }

    public WorkResult Eat(long dishId)
    {
        var dish = Dishes.FirstOrDefault(d => d.Id == dishId);
        if (dish is null)
            return WorkResult.Failure("Không tìm thấy món ăn.");

        if (dish.InInventory <= 0)
            return WorkResult.Failure($"Không còn {dish.NameVi} trong giỏ ẩm thực để dùng bữa.");

        _db.Execute(
            """
            UPDATE pf_dish_inventory
            SET quantity = quantity - 1
            WHERE dish_id = @id AND quantity > 0
            """,
            DbArgs.One("@id", dish.Id));

        _attributes?.RecoverVitals(dish.FatigueRelief, dish.StressRelief, dish.HealthGain);
        _log.Info($"dlc.professions: consumed {dish.Code}: -{dish.FatigueRelief} fatigue, -{dish.StressRelief} stress, +{dish.HealthGain} hp");

        return WorkResult.Success(
            $"Thưởng thức {dish.NameVi}: Giảm {dish.FatigueRelief} Mệt mỏi, giảm {dish.StressRelief} Căng thẳng, hồi phục {dish.HealthGain} Khí huyết.",
            1,
            0);
    }

    public long CalculateDishSellValue(DishRecipeDef dish) => dish.Code switch
    {
        "com_nam" => 35,
        "canh_ca" => 75,
        "thit_nuong" => 90,
        "ruou_am" => 80,
        "dan_duoc" => 180,
        _ => Math.Max(25, dish.CookFee * 3)
    };

    public WorkResult SellDish(long citadelId, long dishId, int quantity = 1)
    {
        if (quantity <= 0) quantity = 1;

        var dish = Dishes.FirstOrDefault(d => d.Id == dishId);
        if (dish is null)
            return WorkResult.Failure("Không tìm thấy món ăn này.");

        var count = _db.Query(
            "SELECT quantity FROM pf_dish_inventory WHERE dish_id = @id",
            row => row.GetInt32(0),
            DbArgs.One("@id", dishId)).FirstOrDefault();

        if (count < quantity)
            return WorkResult.Failure($"Trong kho chỉ còn {count} phần {dish.NameVi}, không đủ {quantity} phần để bán.");

        var unitPrice = CalculateDishSellValue(dish);
        var total = unitPrice * quantity;

        _db.Transaction(() =>
        {
            if (count == quantity)
            {
                _db.Execute("DELETE FROM pf_dish_inventory WHERE dish_id = @id", DbArgs.One("@id", dishId));
            }
            else
            {
                _db.Execute("UPDATE pf_dish_inventory SET quantity = quantity - @qty WHERE dish_id = @id",
                    new DbArgs().Add("@qty", quantity).Add("@id", dishId));
            }

            _wallet.Pay(total, JournalCodes.MarketSell, $"Bán {quantity} phần {dish.NameVi}");
        });

        _log.Info($"dlc.professions: sold {quantity} {dish.Code} for {total} copper");
        return WorkResult.Success($"Đã bán {quantity} phần {dish.NameVi} cho tửu lâu/khách quán, thu về {total} Quan.", quantity, 0);
    }

    private bool IsPlayerExhausted(out string reasonVi)
    {
        if (_attributes is not null && _attributes.Vitals.Fatigue >= 90)
        {
            reasonVi = $"Cơ thể đã kiệt sức (Mệt mỏi {_attributes.Vitals.Fatigue}/100). Hãy dùng bữa ẩm thực hoặc nghỉ ngơi dưỡng sức trước khi tiếp tục lao lực!";
            return true;
        }

        reasonVi = string.Empty;
        return false;
    }

    public WorkResult Fish(long citadelId)
    {
        if (IsPlayerExhausted(out var exhaustedMsg))
            return WorkResult.Failure(exhaustedMsg);

        _clock.Advance(1);
        _attributes?.ApplyFatigue(10);

        var practice = _attributes?.PracticeSkill("dieu_ngu", 25) ?? (false, false, 0);
        var skillLevel = practice.Level;
        var attrs = _attributes?.Attributes;
        var wil = attrs?.Wil ?? 50;
        var prc = attrs?.Prc ?? 50;
        var lck = attrs?.Lck ?? 50;

        var region = _map?.Find(citadelId)?.Region ?? "trung_tho";
        var isWaterRegion = region is "giang_dong" or "duong_chau" or "kinh_chau";

        var catchCount = (practice.Level > 0 ? 2 : 1) + (skillLevel >= 3 ? 1 : 0) + (skillLevel >= 6 ? 1 : 0) + (skillLevel >= 9 ? 1 : 0);
        if (wil >= 60 || prc >= 60) catchCount += 1;
        if (isWaterRegion) catchCount += 1;

        _bag.Add(citadelId, 8, catchCount); // 8: ca_kho

        _attributes?.RecoverVitals(fatigueRelief: 0, stressRelief: 15, healthGain: 0);
        _attributes?.AddExp("WIL", 15);
        _attributes?.AddExp("PRC", 10);

        var extraMsg = string.Empty;
        var roll = (int)((uint)(_clock.Today.DayIndex * 37 + citadelId * 13 + skillLevel * 7) % 100);
        if (isWaterRegion && (skillLevel >= 3 || lck >= 60) && roll < 35)
        {
            _wallet.Pay(50, "PF.SALVAGE", "Bắt được Ba Ba Vàng / Trai ngọc Trường Giang");
            extraMsg = " Vớt được Ba Ba Vàng quý hiếm bán được 50 Quan!";
        }
        else if ((skillLevel >= 4 || lck >= 65) && roll < 25)
        {
            _wallet.Pay(80, "PF.SALVAGE", "Vớt được rương cổ tích dưới lòng sông");
            extraMsg = " Cơ duyên vớt được Rương Cổ Tích dưới đáy sông (+80 Quan)!";
        }

        var milestoneMsg = FormatSkillProgressMessage("Điêu Ngư", practice);
        var regionalFlavor = isWaterRegion ? "Lưu vực Trường Giang thủy triều mênh mông" : "Bến đò sông Hoàng Hà";
        _log.Info($"dlc.professions: fished at citadel {citadelId} ({region}): +{catchCount} fish");
        return WorkResult.Success($"1 ngày buông cần tại {regionalFlavor}: Câu được {catchCount} Cá tươi/khô, tâm trí thư thái (giảm 15 Căng thẳng).{extraMsg}{milestoneMsg}", catchCount, 1);
    }

    public WorkResult Forage(long citadelId)
    {
        if (IsPlayerExhausted(out var exhaustedMsg))
            return WorkResult.Failure(exhaustedMsg);

        _clock.Advance(1);
        _attributes?.ApplyFatigue(15);

        var practice = _attributes?.PracticeSkill("tam_duoc", 25) ?? (false, false, 0);
        var skillLevel = practice.Level;
        var attrs = _attributes?.Attributes;
        var prc = attrs?.Prc ?? 50;
        var @int = attrs?.Int ?? 50;

        _attributes?.PracticeSkill("exotic_husbandry", 15);
        var exoticLvl = _attributes?.GetEquippedSkillLevel("exotic_husbandry") ?? 0;

        var region = _map?.Find(citadelId)?.Region ?? "trung_tho";
        RegionalResourceNodeDef? node = null;
        if (_broker != null && _broker.TryGet<IValueChainService>(CapabilityIds.ValueChain, out var vc) && vc != null)
        {
            node = vc.GetResourceNodes(citadelId).FirstOrDefault();
        }

        var isHerbRichTerrain = node?.NodeType is "HERB_VALLEY" or "FOREST" || region is "u_chau" or "ich_chau" or "giao_chau";
        var nodeName = node is not null && node.NodeType is "HERB_VALLEY" or "FOREST"
            ? node.NodeNameVi
            : "Rừng Núi Ngoại Thành";

        var herbCount = 1 + (skillLevel >= 3 ? 1 : 0) + (skillLevel >= 7 ? 1 : 0) + (isHerbRichTerrain && prc >= 60 ? 1 : 0);
        _bag.Add(citadelId, 7, herbCount); // 7: thuoc

        var extraMsg = string.Empty;

        if (node is not null && node.NodeType is "HERB_VALLEY" or "FOREST")
        {
            if (node.PrimaryItemId != 7 && node.PrimaryItemId is 13 or 14)
            {
                _bag.Add(citadelId, node.PrimaryItemId, 1);
                extraMsg = node.PrimaryItemId == 13
                    ? $" Vùng lạnh phương Bắc tại {node.NodeNameVi} đào được 1 củ Nhân Sâm Liêu Đông quý giá!"
                    : $" Giữa {node.NodeNameVi} phát hiện 1 khúc Trầm Hương thượng hạng!";
            }

            if (node.SecondaryItemId.HasValue && (exoticLvl > 0 || HasPerk("earth_vein_sight") || skillLevel >= 3))
            {
                var secId = node.SecondaryItemId.Value;
                _bag.Add(citadelId, secId, 1);
                var secName = _items.Find(secId)?.NameVi ?? "Kỳ trân";
                extraMsg += $" Thu thập thêm +1 {secName} đặc sản vùng!";
            }
        }
        else if (region == "u_chau" && (skillLevel >= 2 || prc >= 60))
        {
            _bag.Add(citadelId, 13, 1);
            extraMsg = " Vùng lạnh phương Bắc đào được 1 củ Nhân Sâm Liêu Đông quý giá!";
        }
        else if ((region == "ich_chau" || region == "giao_chau" || citadelId == 20) && (skillLevel >= 1 || prc >= 55))
        {
            _bag.Add(citadelId, 14, 1);
            extraMsg = " Trong thung lũng sương mù phát hiện 1 khúc Trầm Hương lâu năm!";
        }
        else if ((exoticLvl > 0 || HasPerk("earth_vein_sight")) && node?.SecondaryItemId is >= 25)
        {
            var secId = node.SecondaryItemId.Value;
            _bag.Add(citadelId, secId, 1);
            var secName = _items.Find(secId)?.NameVi ?? "Kỳ trân";
            extraMsg = $" Nhờ linh nhãn phát hiện thêm +1 {secName}!";
        }

        _attributes?.AddExp("PRC", 20);
        _attributes?.AddExp("INT", 10);

        var milestoneMsg = FormatSkillProgressMessage("Tầm Dược", practice);
        _log.Info($"dlc.professions: foraged at citadel {citadelId} ({nodeName}): +{herbCount} herbs");
        return WorkResult.Success($"1 ngày vượt suối trèo non tầm dược tại {nodeName}: Thu hái được {herbCount} Thảo dược quý.{extraMsg}{milestoneMsg}", herbCount, 1);
    }

    public WorkResult Hunt(long citadelId)
    {
        if (IsPlayerExhausted(out var exhaustedMsg))
            return WorkResult.Failure(exhaustedMsg);

        _clock.Advance(1);

        var practice = _attributes?.PracticeSkill("thu_liep", 25) ?? (false, false, 0);
        var skillLevel = practice.Level;
        var attrs = _attributes?.Attributes;
        var war = attrs?.War ?? 50;

        var region = _map?.Find(citadelId)?.Region ?? "trung_tho";
        var isWilderness = region is "u_chau" or "ich_chau";

        if (isWilderness && war < 35)
        {
            _attributes?.ApplyFatigue(25);
            _attributes?.RecoverVitals(fatigueRelief: 0, stressRelief: -10, healthGain: -20);
            _log.Info($"dlc.professions: hunted at citadel {citadelId} - wounded by wild beast");
            return WorkResult.Failure("Gặp phải đàn dã thú hung hãn trên núi! Võ lực không đủ trấn áp, bị thương tổn hại 20 Khí huyết, đành ôm vết thương lui về.");
        }

        _attributes?.ApplyFatigue(20);
        DegradeEquipped("WEAPON", 2);

        _attributes?.PracticeSkill("exotic_husbandry", 15);
        var exoticHuntLvl = _attributes?.GetEquippedSkillLevel("exotic_husbandry") ?? 0;

        var meatCount = (practice.Level > 0 ? 2 : 1) + (skillLevel >= 3 ? 1 : 0) + (skillLevel >= 6 ? 1 : 0) + (skillLevel >= 9 ? 1 : 0) + (war >= 60 ? 1 : 0) + (exoticHuntLvl > 0 ? 1 : 0);
        _bag.Add(citadelId, 8, meatCount);

        if (exoticHuntLvl > 0)
        {
            var trophyId = citadelId is 8 or 19 ? 27L : 32L;
            _bag.Add(citadelId, trophyId, 1);
        }

        var extraMsg = string.Empty;
        if (region == "u_chau")
        {
            _bag.Add(citadelId, 11, 1);
            if (skillLevel >= 4)
            {
                _bag.Add(citadelId, 12, 1);
                extraMsg = " Săn được Hươu tuyết phương Bắc, thu được da lông thú thượng phẩm (+1 Gấm)!";
            }
            else
            {
                extraMsg = " Thu được da lông hươu tuyết giữ ấm (+1 Vải thô).";
            }
        }
        else if (region == "ich_chau")
        {
            if (war >= 65)
            {
                _bag.Add(citadelId, 13, 1);
                _wallet.Pay(60, "PF.SALVAGE", "Tiền thưởng diệt trừ ác thú");
                extraMsg = " Bắn hạ Mãnh Hổ Ba Thục! Thu hoạch Mật Gấu đại bổ (+1 Nhân sâm) và nhận 60 Quan tiền thưởng trừ hại cho dân!";
            }
            else
            {
                extraMsg = " Bắn được Sơn dương núi đá, bổ sung lương thực cho thương đội.";
            }
        }
        else if (region is "giang_dong" or "duong_chau" or "kinh_chau")
        {
            _bag.Add(citadelId, 5, 1);
            extraMsg = " Bắt được cá sấu đầm lầy, vẩy cứng chắc như sắt giáp (+1 Sắt)!";
        }
        else
        {
            extraMsg = " Bắn trúng bầy lợn rừng châu thổ, thu hoạch thịt tươi dồi dào.";
        }

        _attributes?.AddExp("WAR", 20);
        _attributes?.AddExp("PRC", 15);
        _attributes?.AddExp("CON", 10);

        var milestoneMsg = FormatSkillProgressMessage("Thú Liệp", practice);
        _log.Info($"dlc.professions: hunted at citadel {citadelId} ({region}): +{meatCount} meat");
        return WorkResult.Success($"1 ngày giương cung săn bắn tại vùng {region}: Thu hoạch {meatCount} phần thịt thú săn.{extraMsg}{milestoneMsg}", meatCount, 1);
    }

    public WorkResult Mine(long citadelId)
    {
        if (IsPlayerExhausted(out var exhaustedMsg))
            return WorkResult.Failure(exhaustedMsg);

        var activeTool = GetSlot("TOOL")?.Equipment;
        var hasWorkingTool = activeTool is not null && activeTool.Durability > 0;

        _clock.Advance(1);
        _attributes?.ApplyFatigue(hasWorkingTool ? 20 : 25);

        var practice = _attributes?.PracticeSkill("khai_khoang", 25) ?? (false, false, 0);
        var skillLevel = practice.Level;
        var attrs = _attributes?.Attributes;
        var cra = attrs?.Cra ?? 50;
        var con = attrs?.Con ?? 50;

        long primaryItemId = 15;
        long secondaryItemId = 5;
        var nodeName = "Vỉa Đá Lộ Thiên Ngoại Thành";
        var isRichMineOrForest = false;

        if (_broker != null && _broker.TryGet<IValueChainService>(CapabilityIds.ValueChain, out var vc) && vc != null)
        {
            var node = vc.GetResourceNodes(citadelId).FirstOrDefault();
            if (node != null)
            {
                nodeName = node.NodeNameVi;
                if (node.NodeType is "MINE" or "FOREST")
                {
                    isRichMineOrForest = true;
                    primaryItemId = node.PrimaryItemId;
                    secondaryItemId = node.SecondaryItemId ?? 15;
                }
                else if (node.SecondaryItemId is 5 or 15 or 16 or 25 or 26 or 29 or 31)
                {
                    isRichMineOrForest = true;
                    primaryItemId = node.SecondaryItemId.Value;
                    secondaryItemId = 15;
                }
            }
        }
        else if (citadelId is 14 or 16 or 19)
        {
            isRichMineOrForest = true;
            primaryItemId = 16;
            secondaryItemId = 15;
        }

        _attributes?.PracticeSkill("vein_divining", 20);
        var veinLvl = _attributes?.GetEquippedSkillLevel("vein_divining") ?? 0;

        var toolBonus = hasWorkingTool ? 1 : 0;
        if (hasWorkingTool)
        {
            DegradeEquipped("TOOL", 2);
        }

        var yieldCount = (isRichMineOrForest ? 2 : 1)
                         + (skillLevel >= 3 ? 1 : 0)
                         + (skillLevel >= 7 ? 1 : 0)
                         + toolBonus
                         + (veinLvl > 0 ? (veinLvl + 1) / 2 : 0);
        _bag.Add(citadelId, primaryItemId, yieldCount);

        var primaryName = _items.Find(primaryItemId)?.NameVi ?? "Khoáng thạch";
        var extraMsg = string.Empty;
        if (isRichMineOrForest && (skillLevel >= 2 || con >= 60 || cra >= 60 || veinLvl > 0 || HasPerk("earth_vein_sight")))
        {
            var secQty = 1 + (veinLvl >= 3 ? 1 : 0) + (HasPerk("earth_vein_sight") ? 1 : 0);
            _bag.Add(citadelId, secondaryItemId, secQty);
            var secName = _items.Find(secondaryItemId)?.NameVi ?? "Khoáng thạch";
            extraMsg = $" Đào trúng mạch phụ thu thêm +{secQty} {secName}!";
        }
        else if (!hasWorkingTool)
        {
            extraMsg = " (Tay không thiếu cuốc chim nên tốn thêm thể lực và sản lượng hạn chế).";
        }

        _attributes?.AddExp("CRA", 20);
        _attributes?.AddExp("CON", 15);

        var milestoneMsg = FormatSkillProgressMessage("Khai Khoáng", practice);
        _log.Info($"dlc.professions: mined at citadel {citadelId} ({nodeName}): +{yieldCount} item #{primaryItemId}");
        return WorkResult.Success($"1 ngày vung cuốc khai mỏ tại {nodeName}: Khai thác được {yieldCount} {primaryName}.{extraMsg}{milestoneMsg}", yieldCount, 1);
    }

    private static string FormatSkillProgressMessage(string skillNameVi, (bool Awakened, bool LeveledUp, int Level) progress)
    {
        if (progress.Awakened)
            return $" [LĨNH HỘI: Linh quang lóe sáng! Tự ngộ kỹ năng {skillNameVi} - Cấp 1 {SkillRankTitles.ForLevel(1)}!]";
        if (progress.LeveledUp)
            return $" [THĂNG CẤP: Kỹ năng {skillNameVi} đột phá Cấp {progress.Level} - {SkillRankTitles.ForLevel(progress.Level)}!]";
        if (progress.Level == 0)
            return $" (Thao tác sơ khởi còn vụng về, chưa thể lĩnh hội áo nghĩa {skillNameVi}. Hãy kiên trì rèn luyện!).";
        return string.Empty;
    }

    public EquipmentDef? RecordCombatFeat(long equipmentId, string wielderNameVi, string featDescriptionVi, int killDelta = 1, int fameDelta = 10)
    {
        var equip = FindEquipment(equipmentId);
        if (equip is null)
        {
            return null;
        }

        var newNarrative = string.IsNullOrWhiteSpace(equip.LegendNarrative)
            ? $"{wielderNameVi}: {featDescriptionVi}"
            : $"{equip.LegendNarrative} | {wielderNameVi}: {featDescriptionVi}";

        _db.Transaction(() =>
        {
            _db.Execute(
                """
                UPDATE pf_equipment
                SET fame_score = fame_score + @fame,
                    kill_count = kill_count + @kills,
                    legend_narrative = @narrative
                WHERE id = @id
                """,
                new DbArgs()
                    .Add("@fame", fameDelta)
                    .Add("@kills", killDelta)
                    .Add("@narrative", newNarrative)
                    .Add("@id", equipmentId));
        });

        var chronicle = GetChronicle();
        if (chronicle != null)
        {
            var creatorInfo = !string.IsNullOrWhiteSpace(equip.CreatorNameVi)
                ? $"do Danh Sư {equip.CreatorNameVi} chế tác"
                : "truyền thế";

            var headline = $"{wielderNameVi} cầm bảo khí {equip.NameVi} lập đại công!";
            var detail = $"{wielderNameVi} mang theo thần binh {equip.NameVi} ({creatorInfo}) xuất chiến, {featDescriptionVi}. Uy danh bảo vật chấn động tứ phương!";
            var impact = $"Trảm địch +{killDelta} · Uy danh +{fameDelta}";

            chronicle.RecordEvent(
                _clock.Today,
                equip.CreatorId ?? 0,
                equip.CreatorNameVi ?? "Danh Sư",
                equip.CitadelId,
                headline,
                detail,
                category: "martial",
                statImpact: impact);
        }

        if (_attributes != null && equip.CreatorId.HasValue && equip.CreatorId.Value == (_player?.PlayerId ?? 0))
        {
            _attributes.GainFame(Math.Max(1, fameDelta / 2), $"Vũ khí do tự tay đúc được {wielderNameVi} cầm lập chiến công.");
        }

        return FindEquipment(equipmentId);
    }
}
