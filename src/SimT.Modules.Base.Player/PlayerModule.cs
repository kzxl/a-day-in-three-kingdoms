using SimT.Contracts;

using SimT.Contracts.Data;

using SimT.Contracts.Ledger;

using SimT.Contracts.Modules;

using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Base.Player;

/// <summary>

/// base.player — protagonist identity, wallet facade, attributes, and per-city bag.

/// The wallet posts to the kernel ledger; it never stores a balance of its own.

/// </summary>

public sealed class PlayerModule : ISimModule

{

    public void Start(ModuleContext context)

    {

        var world = context.Capabilities.Get<IWorldMap>(CapabilityIds.WorldMap);

        var startCitadel = world.Citadels.FirstOrDefault(citadel => citadel.Code == context.Campaign.StartLocationCode)

                           ?? throw new SimException(

                               "player.start_city_missing",

                               $"Product start location '{context.Campaign.StartLocationCode}' is not on the map.");

        var player = PlayerStore.Ensure(context.Db, startCitadel.Id);

        if (context.Ledger.Balance == 0 && context.Campaign.StartingCash > 0)

        {

            context.Ledger.Post(

                context.Clock.Today,

                new LedgerEntry(JournalCodes.StartingCash, LedgerDirection.In, context.Campaign.StartingCash, "Vốn khởi nghiệp"));

        }

        var wallet = new Wallet(context);

        var bag = new Bag(context.Db);

        var reputation = new PlayerReputationService(context.Db, player.Id, context.Capabilities, context.Clock, context.Log);

        var attributes = new PlayerAttributesStore(context.Db, player.Id, reputation, context.Capabilities);

        var destiny = new PlayerDestinyService(context.Db, player.Id, context.Capabilities, context.Clock, context.Log);

        context.Capabilities.Provide(CapabilityIds.PlayerWallet, wallet);

        context.Capabilities.Provide(CapabilityIds.PlayerBag, bag);

        context.Capabilities.Provide(CapabilityIds.PlayerState, new PlayerState(context.Db, player));

        context.Capabilities.Provide(CapabilityIds.PlayerAttributes, attributes);

        context.Capabilities.Provide(CapabilityIds.PlayerReputation, reputation);

        context.Capabilities.Provide(CapabilityIds.PlayerDestiny, destiny);

        context.Ui.AddScreen(new PlayerReputationScreen(reputation));

        context.Ui.AddScreen(new PlayerDestinyScreen(destiny));

        context.Ui.AddCommandHandler(new PlayerReputationCommandHandler(reputation));

        context.Ui.AddCommandHandler(new PlayerDestinyCommandHandler(destiny));

        context.Events.Subscribe<DayAdvanced>(_ =>
        {
            attributes.OnDayAdvanced();
            destiny.EvaluateMilestones();
        });

        context.Log.Info($"protagonist '{player.Name}' at citadel {player.CityId}, wallet {wallet.Balance}");

    }

    private sealed record PlayerSnapshot(long Id, string Name, long CityId);

    private static class PlayerStore
    {
        public static PlayerSnapshot Ensure(IDbSession db, long startCityId)
        {
            var existing = db.QueryOne(
                "SELECT id, name, city_id FROM pl_player ORDER BY id LIMIT 1",
                row => new PlayerSnapshot(row.GetInt64(0), row.GetString(1), row.GetInt64(2)));

            if (existing is null)
            {
                db.Execute(
                    "INSERT INTO pl_player(id, name, city_id) VALUES(1, @name, @city)",
                    new DbArgs().Add("@name", "Lữ Khách").Add("@city", startCityId));
                existing = new PlayerSnapshot(1, "Lữ Khách", startCityId);
            }

            db.Execute(
                @"INSERT OR IGNORE INTO pl_attribute
                  (player_id, war_attr, ldr_attr, int_attr, pol_attr, dip_attr, com_attr, log_attr, cra_attr, con_attr, wil_attr, prc_attr, lck_attr)
                  VALUES(@id, 55, 50, 60, 50, 55, 65, 55, 50, 60, 65, 60, 70)",
                DbArgs.One("@id", existing.Id));

            db.Execute(
                @"INSERT OR IGNORE INTO pl_vitals
                  (player_id, health, fatigue, morale, stress)
                  VALUES(@id, 100, 0, 100, 0)",
                DbArgs.One("@id", existing.Id));

            db.Execute(
                @"INSERT OR IGNORE INTO pl_social
                  (player_id, fame, virtue, credibility)
                  VALUES(@id, 100, 50, 50)",
                DbArgs.One("@id", existing.Id));

            db.Execute(
                @"INSERT OR IGNORE INTO pl_reputation_region (player_id, region_code, reputation) VALUES
                  (@id, 'trung_nguyen', 60),
                  (@id, 'ha_bac', 25),
                  (@id, 'giang_dong', 20),
                  (@id, 'kinh_so', 20),
                  (@id, 'ba_thuc', 15)",
                DbArgs.One("@id", existing.Id));

            db.Execute(
                @"INSERT OR IGNORE INTO pl_reputation_deed
                  (id, player_id, day_index, date_text, category, fame_delta, virtue_delta, credibility_delta, title_vi, narrative_vi)
                  VALUES (1, @id, 1, '184-05-01', 'milestone', 100, 0, 0, 'Khởi Sự Tam Quốc', 'Bắt đầu hành trình lập thân lập nghiệp giữa thời loạn thế.')",
                DbArgs.One("@id", existing.Id));

            return existing;
        }
    }

    private sealed class Wallet : IWallet

    {

        private readonly ModuleContext _context;

        public Wallet(ModuleContext context) => _context = context;

        public long Balance => _context.Ledger.Balance;

        public long Liquid => _context.Ledger.Liquid;

        public void Charge(long amount, string journalCode, string description, string? idempotencyKey = null)

        {

            if (Liquid < amount)

            {

                throw new SimException("wallet.insufficient", $"Cần {amount} tiền mặt nhưng chỉ có {Liquid}.");

            }

            _context.Ledger.Post(

                _context.Clock.Today,

                new LedgerEntry(journalCode, LedgerDirection.Out, amount, description, idempotencyKey));

        }

        public void Pay(long amount, string journalCode, string description, string? idempotencyKey = null) =>

            _context.Ledger.Post(

                _context.Clock.Today,

                new LedgerEntry(journalCode, LedgerDirection.In, amount, description, idempotencyKey));

    }

    private sealed class Bag : IBag

    {

        private readonly IDbSession _db;

        public Bag(IDbSession db) => _db = db;

        public int Quantity(long cityId, long itemId) =>

            (int)_db.ScalarLong(

                "SELECT COALESCE(quantity, 0) FROM pl_bag WHERE city_id=@city AND item_id=@item",

                new DbArgs().Add("@city", cityId).Add("@item", itemId));

        public int TotalQuantity(long cityId) =>

            (int)_db.ScalarLong("SELECT COALESCE(SUM(quantity), 0) FROM pl_bag WHERE city_id=@city", DbArgs.One("@city", cityId));

        public void Add(long cityId, long itemId, int quantity)

        {

            if (quantity <= 0)

            {

                throw new SimException("bag.invalid_quantity", $"Add quantity must be positive, got {quantity}.");

            }

            _db.Execute(

                """

                INSERT INTO pl_bag(city_id, item_id, quantity) VALUES(@city, @item, @quantity)

                ON CONFLICT(city_id, item_id) DO UPDATE SET quantity = quantity + excluded.quantity

                """,

                new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@quantity", quantity));

        }

        public bool Remove(long cityId, long itemId, int quantity)

        {

            if (quantity <= 0)

            {

                throw new SimException("bag.invalid_quantity", $"Remove quantity must be positive, got {quantity}.");

            }

            var current = Quantity(cityId, itemId);

            if (current < quantity)

            {

                return false;

            }

            if (current == quantity)

            {

                _db.Execute(

                    "DELETE FROM pl_bag WHERE city_id=@city AND item_id=@item",

                    new DbArgs().Add("@city", cityId).Add("@item", itemId));

                return true;

            }

            _db.Execute(

                "UPDATE pl_bag SET quantity = quantity - @quantity WHERE city_id=@city AND item_id=@item",

                new DbArgs().Add("@city", cityId).Add("@item", itemId).Add("@quantity", quantity));

            return true;

        }

        public IReadOnlyList<BagRow> Rows(long cityId) =>

            _db.Query(

                "SELECT city_id, item_id, quantity FROM pl_bag WHERE city_id=@city ORDER BY item_id",

                row => new BagRow(row.GetInt64(0), row.GetInt64(1), row.GetInt32(2)),

                DbArgs.One("@city", cityId));

    }

    private sealed class PlayerState : IPlayerState

    {

        private readonly IDbSession _db;

        public PlayerState(IDbSession db, PlayerSnapshot snapshot)

        {

            _db = db;

            PlayerId = snapshot.Id;

            Name = snapshot.Name;

            CurrentCityId = snapshot.CityId;

        }

        public long PlayerId { get; }

        public string Name { get; }

        public long CurrentCityId { get; private set; }

        public void MoveTo(long cityId, Contracts.Time.GameDate date)

        {

            _db.Execute(

                "UPDATE pl_player SET city_id=@city WHERE id=@id",

                new DbArgs().Add("@city", cityId).Add("@id", PlayerId));

            CurrentCityId = cityId;

        }

    }

    private sealed record VitalsRaw(int Health, int Fatigue, int Morale, int Stress);

    private sealed class PlayerAttributesStore : IPlayerAttributes

    {

        private readonly IDbSession _db;
        private readonly long _playerId;
        private readonly IPlayerReputation? _reputation;
        private readonly ICapabilityBroker? _capabilities;

        public PlayerAttributesStore(
            IDbSession db,
            long playerId,
            IPlayerReputation? reputation = null,
            ICapabilityBroker? capabilities = null)
        {
            _db = db;
            _playerId = playerId;
            _reputation = reputation;
            _capabilities = capabilities;
        }

        public CharacterAttributes Attributes

        {

            get

            {

                var baseAttrs = _db.QueryOne<CharacterAttributes>(

                    @"SELECT war_attr, ldr_attr, int_attr, pol_attr, dip_attr, com_attr, log_attr, cra_attr,

                             con_attr, wil_attr, prc_attr, lck_attr

                      FROM pl_attribute WHERE player_id = @id",

                    row => new CharacterAttributes(

                        row.GetInt32(0),

                        row.GetInt32(1),

                        row.GetInt32(2),

                        row.GetInt32(3),

                        row.GetInt32(4),

                        row.GetInt32(5),

                        row.GetInt32(6),

                        row.GetInt32(7),

                        row.GetInt32(8),

                        row.GetInt32(9),

                        row.GetInt32(10),

                        row.GetInt32(11)),

                    DbArgs.One("@id", _playerId))

                    ?? new CharacterAttributes(50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50, 50);

                if (_capabilities != null && _capabilities.TryGet<IPlayerEquipment>(CapabilityIds.PlayerEquipment, out var eq) && eq != null)
                {
                    return new CharacterAttributes(
                        baseAttrs.War + eq.GetTotalBonus("WAR"),
                        baseAttrs.Ldr + eq.GetTotalBonus("LDR"),
                        baseAttrs.Int + eq.GetTotalBonus("INT"),
                        baseAttrs.Pol + eq.GetTotalBonus("POL"),
                        baseAttrs.Dip + eq.GetTotalBonus("DIP"),
                        baseAttrs.Com + eq.GetTotalBonus("COM"),
                        baseAttrs.Log + eq.GetTotalBonus("LOG"),
                        baseAttrs.Cra + eq.GetTotalBonus("CRA"),
                        baseAttrs.Con + eq.GetTotalBonus("CON"),
                        baseAttrs.Wil + eq.GetTotalBonus("WIL"),
                        baseAttrs.Prc + eq.GetTotalBonus("PRC"),
                        baseAttrs.Lck + eq.GetTotalBonus("LCK"));
                }

                return baseAttrs;

            }

        }

        public CharacterAttributeExps Exps
        {
            get
            {
                return _db.QueryOne<CharacterAttributeExps>(
                    @"SELECT war_exp, ldr_exp, int_exp, pol_exp, dip_exp, com_exp, log_exp, cra_exp,
                             con_exp, wil_exp, prc_exp, lck_exp
                      FROM pl_attribute WHERE player_id = @id",
                    row => new CharacterAttributeExps(
                        row.GetInt32(0),
                        row.GetInt32(1),
                        row.GetInt32(2),
                        row.GetInt32(3),
                        row.GetInt32(4),
                        row.GetInt32(5),
                        row.GetInt32(6),
                        row.GetInt32(7),
                        row.GetInt32(8),
                        row.GetInt32(9),
                        row.GetInt32(10),
                        row.GetInt32(11)),
                    DbArgs.One("@id", _playerId))
                    ?? CharacterAttributeExps.Zero;
            }
        }

        public CharacterVitals Vitals

        {

            get

            {

                var attrs = Attributes;

                var vitalsRow = _db.QueryOne<VitalsRaw>(

                    "SELECT health, fatigue, morale, stress FROM pl_vitals WHERE player_id = @id",

                    row => new VitalsRaw(row.GetInt32(0), row.GetInt32(1), row.GetInt32(2), row.GetInt32(3)),

                    DbArgs.One("@id", _playerId))

                    ?? new VitalsRaw(100, 0, 100, 0);

                var maxHp = 80 + (int)(attrs.Con * 1.5) + (int)(attrs.War * 0.5);

                var maxStamina = 70 + (int)(attrs.Con * 1.2) + (int)(attrs.War * 0.3) + vitalsRow.Health / 10;

                var currentStamina = Math.Max(0, maxStamina - vitalsRow.Fatigue);

                return new CharacterVitals(

                    vitalsRow.Health,

                    maxHp,

                    vitalsRow.Fatigue,

                    vitalsRow.Morale,

                    vitalsRow.Stress,

                    currentStamina,

                    maxStamina);

            }

        }

        public CharacterSocial Social

        {

            get

            {

                return _db.QueryOne<CharacterSocial>(

                    "SELECT fame, virtue, credibility FROM pl_social WHERE player_id = @id",

                    row => new CharacterSocial(row.GetInt32(0), row.GetInt32(1), row.GetInt32(2)),

                    DbArgs.One("@id", _playerId))

                    ?? new CharacterSocial(100, 50, 50);

            }

        }

        public int EffectivePrc
        {
            get
            {
                var basePrc = Attributes.Prc;
                var bonus = 0;
                var baLacLvl = GetEquippedSkillLevel("ba_lac_than_nhan");
                if (baLacLvl > 0)
                {
                    bonus += 15 + (baLacLvl - 1) * 3;
                }

                var nguyetDanLvl = GetEquippedSkillLevel("nguyet_dan_giam_dinh");
                if (nguyetDanLvl > 0)
                {
                    bonus += 25 + (nguyetDanLvl - 1) * 3;
                }

                var vongKhiLvl = GetEquippedSkillLevel("vong_khi_thien_co");
                if (vongKhiLvl > 0)
                {
                    bonus += 40 + (vongKhiLvl - 1) * 5;
                }

                return Math.Clamp(basePrc + bonus, 1, 150);
            }
        }

        public void AddExp(string attributeCode, int amount)

        {

            var colExp = attributeCode.ToLowerInvariant() + "_exp";

            var colAttr = attributeCode.ToLowerInvariant() + "_attr";

            _db.Execute(

                $"UPDATE pl_attribute SET {colExp} = {colExp} + @amount WHERE player_id = @id",

                new DbArgs().Add("@amount", amount).Add("@id", _playerId));

            _db.Execute(

                $@"UPDATE pl_attribute

                   SET {colAttr} = MIN(120, {colAttr} + ({colExp} / 100)),

                       {colExp} = {colExp} % 100

                   WHERE player_id = @id AND {colExp} >= 100",

                DbArgs.One("@id", _playerId));

        }

        public void ApplyFatigue(int amount)

        {

            _db.Execute(

                @"UPDATE pl_vitals

                  SET fatigue = MIN(100, fatigue + @amount)

                  WHERE player_id = @id",

                new DbArgs().Add("@amount", amount).Add("@id", _playerId));

        }

        public void Rest(int days)
        {
            // Canonical deep rest formula: +30 fatigue recovery / day, -10 stress, +2 HP
            var recovery = days * 30;
            var stressRelief = days * 10;
            var healthGain = days * 2;

            _db.Execute(
                @"UPDATE pl_vitals
                  SET fatigue = MAX(0, fatigue - @rec),
                      stress = MAX(0, stress - @st),
                      health = MIN(100, health + @hp)
                  WHERE player_id = @id",
                new DbArgs()
                    .Add("@rec", recovery)
                    .Add("@st", stressRelief)
                    .Add("@hp", healthGain)
                    .Add("@id", _playerId));
        }

        public void OnDayAdvanced()
        {
            var attrs = Attributes;
            // Canonical passive daily stamina & vitals recovery from simulator:
            // Modest passive recovery per elapsed calendar day (3 + Con*0.05 + War*0.02)
            var dailyRecovery = 3 + (int)(attrs.Con * 0.05) + (int)(attrs.War * 0.02);
            var dailyStressRelief = 2 + (int)(attrs.Wil * 0.05);
            const int dailyHealthRecovery = 1;

            _db.Execute(
                @"UPDATE pl_vitals
                  SET fatigue = MAX(0, fatigue - @rec),
                      stress = MAX(0, stress - @st),
                      health = MIN(100, health + @hp)
                  WHERE player_id = @id",
                new DbArgs()
                    .Add("@rec", dailyRecovery)
                    .Add("@st", dailyStressRelief)
                    .Add("@hp", dailyHealthRecovery)
                    .Add("@id", _playerId));
        }

        public IReadOnlyList<string> LearnedSkills
        {
            get
            {
                return _db.Query(
                    "SELECT skill_code FROM pl_skill WHERE player_id = @id ORDER BY skill_code ASC",
                    row => row.GetString(0),
                    DbArgs.One("@id", _playerId));
            }
        }

        public IReadOnlyList<PlayerSkillProgressDef> SkillProgresses
        {
            get
            {
                return _db.Query(
                    @"SELECT skill_code, level, proficiency, is_equipped, equipped_slot, tier
                      FROM pl_skill
                      WHERE player_id = @id
                      ORDER BY is_equipped DESC, equipped_slot ASC, tier DESC, level DESC, skill_code ASC",
                    row =>
                    {
                        var code = row.GetString(0);
                        var lvl = row.GetInt32(1);
                        var prof = row.GetInt32(2);
                        var isEq = row.GetInt32(3) == 1;
                        var slot = row.IsDbNull(4) ? (int?)null : row.GetInt32(4);
                        var tier = row.GetInt32(5);
                        var maxProf = GetMaxProficiency(lvl);
                        return new PlayerSkillProgressDef(code, lvl, prof, maxProf, isEq, slot, tier);
                    },
                    DbArgs.One("@id", _playerId));
            }
        }

        public PlayerSkillProgressDef? GetSkillProgress(string skillCode)
        {
            if (string.IsNullOrWhiteSpace(skillCode)) return null;
            return _db.Query(
                @"SELECT skill_code, level, proficiency, is_equipped, equipped_slot, tier
                  FROM pl_skill
                  WHERE player_id = @id AND skill_code = @code",
                row =>
                {
                    var code = row.GetString(0);
                    var lvl = row.GetInt32(1);
                    var prof = row.GetInt32(2);
                    var isEq = row.GetInt32(3) == 1;
                    var slot = row.IsDbNull(4) ? (int?)null : row.GetInt32(4);
                    var tier = row.GetInt32(5);
                    var maxProf = GetMaxProficiency(lvl);
                    return new PlayerSkillProgressDef(code, lvl, prof, maxProf, isEq, slot, tier);
                },
                new DbArgs().Add("@id", _playerId).Add("@code", skillCode)).FirstOrDefault();
        }

        public bool HasSkill(string skillCode)
        {
            if (string.IsNullOrWhiteSpace(skillCode)) return false;
            return _db.ScalarLong(
                "SELECT COUNT(*) FROM pl_skill WHERE player_id = @id AND skill_code = @code",
                new DbArgs().Add("@id", _playerId).Add("@code", skillCode)) > 0;
        }

        public bool IsSkillEquipped(string skillCode)
        {
            if (string.IsNullOrWhiteSpace(skillCode)) return false;
            return _db.ScalarLong(
                "SELECT COUNT(*) FROM pl_skill WHERE player_id = @id AND skill_code = @code AND is_equipped = 1",
                new DbArgs().Add("@id", _playerId).Add("@code", skillCode)) > 0;
        }

        public int GetEquippedSkillLevel(string skillCode)
        {
            if (string.IsNullOrWhiteSpace(skillCode)) return 0;
            return _db.Query(
                "SELECT level FROM pl_skill WHERE player_id = @id AND skill_code = @code AND is_equipped = 1",
                r => r.GetInt32(0),
                new DbArgs().Add("@id", _playerId).Add("@code", skillCode)).FirstOrDefault();
        }

        public bool LearnSkill(string skillCode)
        {
            if (string.IsNullOrWhiteSpace(skillCode)) return false;
            if (HasSkill(skillCode)) return false;

            var tier = ResolveSkillTier(skillCode);

            _db.Execute(
                @"INSERT INTO pl_skill (player_id, skill_code, learned_date, proficiency, level, is_equipped, equipped_slot, tier)
                  VALUES (@id, @code, '184-05-01', 1, 1, 0, NULL, @tier)
                  ON CONFLICT(player_id, skill_code) DO NOTHING",
                new DbArgs().Add("@id", _playerId).Add("@code", skillCode).Add("@tier", tier));

            AutoEquipIfSlotAvailable(skillCode, tier);

            return true;
        }

        public bool TrainSkill(string skillCode, int expGain)
        {
            if (string.IsNullOrWhiteSpace(skillCode) || expGain <= 0) return false;
            var current = GetSkillProgress(skillCode);
            if (current == null) return false;
            if (current.Level >= 5) return false;

            var level = current.Level;
            var prof = current.Proficiency + expGain;
            var leveledUp = false;

            while (level < 5)
            {
                var needed = GetMaxProficiency(level);
                if (prof >= needed)
                {
                    prof -= needed;
                    level++;
                    leveledUp = true;
                }
                else
                {
                    break;
                }
            }

            if (level >= 5)
            {
                level = 5;
                prof = GetMaxProficiency(5);
            }
            else
            {
                prof = Math.Max(1, prof);
            }

            _db.Execute(
                "UPDATE pl_skill SET level = @lvl, proficiency = @prof WHERE player_id = @id AND skill_code = @code",
                new DbArgs().Add("@lvl", level).Add("@prof", prof).Add("@id", _playerId).Add("@code", skillCode));

            return leveledUp;
        }

        public string EquipSkill(string skillCode, int slot, int tier)
        {
            if (slot < 1 || slot > 6)
            {
                return "Vị trí ô trang bị không hợp lệ (Chỉ hỗ trợ ô từ 1 đến 6).";
            }

            var progress = GetSkillProgress(skillCode);
            if (progress == null)
            {
                return $"Chưa lĩnh hội đặc kỹ '{skillCode}'. Hãy bái sư học đạo trước!";
            }

            var effectiveTier = tier > 0 ? tier : progress.Tier;

            var currentEquipped = SkillProgresses
                .Where(s => s.IsEquipped && !s.SkillCode.Equals(skillCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            currentEquipped.RemoveAll(s => s.EquippedSlot == slot);

            if (effectiveTier >= 5)
            {
                var tier5Count = currentEquipped.Count(s => s.Tier >= 5);
                if (tier5Count >= 1)
                {
                    return "Giới hạn tuyệt kỹ: Đã trang bị 1 tuyệt kỹ Phẩm S (Bậc 5). Chỉ được kích hoạt tối đa 1 tuyệt kỹ Phẩm S!";
                }
            }
            else if (effectiveTier == 4)
            {
                var tier4Count = currentEquipped.Count(s => s.Tier == 4);
                if (tier4Count >= 2)
                {
                    return "Giới hạn tuyệt kỹ: Đã trang bị 2 tuyệt kỹ Phẩm A (Bậc 4). Chỉ được kích hoạt tối đa 2 tuyệt kỹ Phẩm A!";
                }
            }

            _db.Transaction(() =>
            {
                _db.Execute(
                    "UPDATE pl_skill SET is_equipped = 0, equipped_slot = NULL WHERE player_id = @id AND equipped_slot = @slot",
                    new DbArgs().Add("@id", _playerId).Add("@slot", slot));

                _db.Execute(
                    "UPDATE pl_skill SET is_equipped = 1, equipped_slot = @slot WHERE player_id = @id AND skill_code = @code",
                    new DbArgs().Add("@id", _playerId).Add("@slot", slot).Add("@code", skillCode));
            });

            return $"Đã trang bị thành công đặc kỹ [{skillCode}] vào Ô {slot}.";
        }

        public string UnequipSkill(int slot)
        {
            if (slot < 1 || slot > 6)
            {
                return "Vị trí ô trang bị không hợp lệ.";
            }

            var affected = _db.Execute(
                "UPDATE pl_skill SET is_equipped = 0, equipped_slot = NULL WHERE player_id = @id AND equipped_slot = @slot",
                new DbArgs().Add("@id", _playerId).Add("@slot", slot));

            return affected > 0 ? $"Đã gỡ bỏ đặc kỹ tại Ô {slot}." : $"Ô {slot} hiện đang trống.";
        }

        private static int GetMaxProficiency(int level) => level switch
        {
            1 => 100,
            2 => 250,
            3 => 500,
            4 => 1000,
            _ => 1000
        };

        private int ResolveSkillTier(string skillCode)
        {
            if (_capabilities != null && _capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) && reg != null)
            {
                var skill = reg.All.SelectMany(n => n.Skills).FirstOrDefault(s => s.Code.Equals(skillCode, StringComparison.OrdinalIgnoreCase));
                if (skill != null) return skill.Tier;
            }
            return 1;
        }

        private void AutoEquipIfSlotAvailable(string skillCode, int tier)
        {
            var progresses = SkillProgresses;
            var occupiedSlots = progresses.Where(p => p.IsEquipped && p.EquippedSlot.HasValue).Select(p => p.EquippedSlot!.Value).ToHashSet();
            int? freeSlot = null;
            for (int s = 1; s <= 6; s++)
            {
                if (!occupiedSlots.Contains(s))
                {
                    freeSlot = s;
                    break;
                }
            }

            if (freeSlot == null) return;

            if (tier >= 5 && progresses.Count(p => p.IsEquipped && p.Tier >= 5) >= 1) return;
            if (tier == 4 && progresses.Count(p => p.IsEquipped && p.Tier == 4) >= 2) return;

            _db.Execute(
                "UPDATE pl_skill SET is_equipped = 1, equipped_slot = @slot WHERE player_id = @id AND skill_code = @code",
                new DbArgs().Add("@id", _playerId).Add("@slot", freeSlot.Value).Add("@code", skillCode));
        }

        public void GainFame(int amount, string reason, int virtueDelta = 0, int credDelta = 0)
        {
            _reputation?.GainFame(amount, "deed", reason, reason, virtueDelta: virtueDelta, credDelta: credDelta);
        }
    }

}

