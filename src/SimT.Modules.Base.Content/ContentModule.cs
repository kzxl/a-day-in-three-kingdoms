using SimT.Contracts.Data;
using SimT.Contracts.Modules;
using SimT.Contracts.Playing;

namespace SimT.Modules.Base.Content;

/// <summary>
/// base.content — item master data. Reads its own table once at start and exposes it as the
/// <c>content.items</c> capability.
/// </summary>
public sealed class ContentModule : ISimModule
{
    public void Start(ModuleContext context)
    {
        var catalog = new ItemCatalog(context.Db);
        context.Capabilities.Provide(CapabilityIds.ItemCatalog, catalog);

        var hub = new ItemHubService(catalog);
        context.Capabilities.Provide(CapabilityIds.ItemHub, hub);

        var screen = new ItemHubScreen(hub, context.Capabilities);
        context.Ui.AddScreen(screen);
        context.Ui.AddCommandHandler(screen);

        context.Log.Info($"catalog ready with {catalog.All.Count} items, item hub ready with {hub.Entries.Count} entries");
    }

    private sealed class ItemCatalog : IItemCatalog
    {
        private readonly ItemDef[] _items;
        private readonly Dictionary<long, ItemDef> _byId;

        public ItemCatalog(IDbSession db)
        {
            _items = db.Query(
                    "SELECT id, code, name_vi, name_en, category, base_price, shelf_life_days, weight FROM ct_item ORDER BY id",
                    row => new ItemDef(
                        row.GetInt64(0),
                        row.GetString(1),
                        row.GetString(2),
                        row.GetString(3),
                        row.GetString(4),
                        row.GetInt64(5),
                        row.GetInt32(6),
                        row.GetInt32(7)))
                .ToArray();

            _byId = new Dictionary<long, ItemDef>(_items.Length);
            foreach (var item in _items)
            {
                _byId[item.Id] = item;
            }
        }

        public IReadOnlyList<ItemDef> All => _items;

        public ItemDef? Find(long itemId) => _byId.GetValueOrDefault(itemId);

        public ItemDef? FindByCode(string code)
        {
            foreach (var item in _items)
            {
                if (string.Equals(item.Code, code, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }
    }
}
