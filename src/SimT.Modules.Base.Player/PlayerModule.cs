using SimT.Contracts;
using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Base.Player;

/// <summary>
/// base.player — protagonist identity, wallet facade and per-city bag. The wallet posts to
/// the kernel ledger; it never stores a balance of its own.
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

        context.Capabilities.Provide(CapabilityIds.PlayerWallet, wallet);
        context.Capabilities.Provide(CapabilityIds.PlayerBag, bag);
        context.Capabilities.Provide(CapabilityIds.PlayerState, new PlayerState(context.Db, player));

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

            if (existing is not null)
            {
                return existing;
            }

            db.Execute(
                "INSERT INTO pl_player(id, name, city_id) VALUES(1, @name, @city)",
                new DbArgs().Add("@name", "Lữ Khách").Add("@city", startCityId));

            return new PlayerSnapshot(1, "Lữ Khách", startCityId);
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
}
