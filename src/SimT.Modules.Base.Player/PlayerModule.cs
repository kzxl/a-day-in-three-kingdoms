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

        var attributes = new PlayerAttributesStore(context.Db, player.Id);

        context.Capabilities.Provide(CapabilityIds.PlayerWallet, wallet);

        context.Capabilities.Provide(CapabilityIds.PlayerBag, bag);

        context.Capabilities.Provide(CapabilityIds.PlayerState, new PlayerState(context.Db, player));

        context.Capabilities.Provide(CapabilityIds.PlayerAttributes, attributes);

        context.Events.Subscribe<DayAdvanced>(_ => attributes.OnDayAdvanced());

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

        public PlayerAttributesStore(IDbSession db, long playerId)

        {

            _db = db;

            _playerId = playerId;

        }

        public CharacterAttributes Attributes

        {

            get

            {

                return _db.QueryOne<CharacterAttributes>(

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

        public bool HasSkill(string skillCode)
        {
            if (string.IsNullOrWhiteSpace(skillCode)) return false;
            return _db.ScalarLong(
                "SELECT COUNT(*) FROM pl_skill WHERE player_id = @id AND skill_code = @code",
                new DbArgs().Add("@id", _playerId).Add("@code", skillCode)) > 0;
        }

        public bool LearnSkill(string skillCode)
        {
            if (string.IsNullOrWhiteSpace(skillCode)) return false;
            if (HasSkill(skillCode)) return false;

            _db.Execute(
                @"INSERT INTO pl_skill (player_id, skill_code, learned_date, proficiency)
                  VALUES (@id, @code, '184-05-01', 1)
                  ON CONFLICT(player_id, skill_code) DO NOTHING",
                new DbArgs().Add("@id", _playerId).Add("@code", skillCode));

            return true;
        }
    }

}

