using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Modules.Dlc.Economy;

/// <summary>
/// One handler instance per market screen. Command ids are <c>verb:itemId</c>, so the screen model
/// stays data-only and the host needs no knowledge of market verbs.
/// </summary>
internal sealed class MarketCommandHandler : IScreenCommandHandler
{
    private readonly MarketService _service;
    private readonly IPlayerState _player;
    private readonly IEconomicWarfare? _warfare;

    public MarketCommandHandler(string screenId, MarketService service, IPlayerState player, IEconomicWarfare? warfare = null)
    {
        ScreenId = screenId;
        _service = service;
        _player = player;
        _warfare = warfare;
    }

    public string ScreenId { get; }

    public string? Execute(string commandId)
    {
        var cityId = _player.CurrentCityId;

        if (string.Equals(commandId, "rent", StringComparison.Ordinal))
        {
            _service.Rent(cityId, out var rentMessage);
            return rentMessage;
        }

        // Forms: verb:item and verb:item:quantity
        if (string.Equals(commandId, "stall:rent", StringComparison.Ordinal) || string.Equals(commandId, "rent_stall", StringComparison.Ordinal))
        {
            _service.RentStall(cityId, out var rentMsg);
            return rentMsg;
        }

        if (string.Equals(commandId, "stall:stock_all", StringComparison.Ordinal))
        {
            _service.StockAll(cityId, out var stockMsg);
            return stockMsg;
        }

        var parts = commandId.Split(':');
        if (parts.Length >= 2 && string.Equals(parts[0], "stall", StringComparison.Ordinal))
        {
            var action = parts[1];
            if (string.Equals(action, "stock", StringComparison.Ordinal) && parts.Length >= 3 && long.TryParse(parts[2], out var stockItemId))
            {
                var qty = parts.Length >= 4 && int.TryParse(parts[3], out var pQty) ? pQty : 1;
                _service.Stock(cityId, stockItemId, qty, out var msg);
                return msg;
            }

            if (string.Equals(action, "unstock", StringComparison.Ordinal) && parts.Length >= 3 && long.TryParse(parts[2], out var unstockItemId))
            {
                var qty = parts.Length >= 4 && int.TryParse(parts[3], out var pQty) ? pQty : 1;
                _service.Unstock(cityId, unstockItemId, qty, out var msg);
                return msg;
            }

            if (string.Equals(action, "markup", StringComparison.Ordinal) && parts.Length >= 4 && long.TryParse(parts[2], out var markupItemId) && int.TryParse(parts[3], out var percent))
            {
                var current = _service.Shelves(cityId).FirstOrDefault(s => s.ItemId == markupItemId);
                if (current is null) return "Không tìm thấy hàng trên quầy.";
                var newPrice = Math.Max(1L, current.SellingPrice * (100 + percent) / 100);
                _service.SetPrice(cityId, markupItemId, newPrice, out var msg);
                return msg;
            }
        }

        if (parts.Length >= 2 && string.Equals(parts[0], "npc_buy", StringComparison.Ordinal) && long.TryParse(parts[1], out var offerId))
        {
            var qty = parts.Length >= 3 && int.TryParse(parts[2], out var parsedQty) && parsedQty > 0 ? parsedQty : 1;
            return _service.BuyFromNpcOffer(cityId, offerId, qty).Message;
        }

        if (parts.Length >= 2 && string.Equals(parts[0], "warfare", StringComparison.OrdinalIgnoreCase) && _warfare is not null)
        {
            if (string.Equals(parts[1], "corner", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4
                && long.TryParse(parts[2], out var cornerItemId) && int.TryParse(parts[3], out var cornerQty))
            {
                return _warfare.CornerMarket(cityId, cornerItemId, cornerQty).Message;
            }

            if (string.Equals(parts[1], "smuggle", StringComparison.OrdinalIgnoreCase) && parts.Length >= 5
                && long.TryParse(parts[2], out var destCityId) && long.TryParse(parts[3], out var smuggleItemId) && int.TryParse(parts[4], out var smuggleQty))
            {
                return _warfare.ExecuteSmuggleRun(cityId, destCityId, smuggleItemId, smuggleQty).MessageVi;
            }
        }

        if (string.Equals(commandId, "store:all", StringComparison.OrdinalIgnoreCase))
        {
            _service.StoreAll(cityId, false, out var msg);
            return msg;
        }

        if (string.Equals(commandId, "store:perishable", StringComparison.OrdinalIgnoreCase))
        {
            _service.StoreAll(cityId, true, out var msg);
            return msg;
        }

        if (string.Equals(commandId, "take:all", StringComparison.OrdinalIgnoreCase))
        {
            _service.TakeAll(cityId, out var msg);
            return msg;
        }

        if (parts.Length < 2 || !long.TryParse(parts[1], out var itemId))
        {
            return "Lệnh không hợp lệ.";
        }

        var verb = parts[0];
        var quantity = parts.Length > 2 && int.TryParse(parts[2], out var parsed) && parsed > 0 ? parsed : 1;

        switch (verb)
        {
            case "buy":
                return _service.Buy(cityId, itemId, quantity).Message;
            case "sell":
                return _service.Sell(cityId, itemId, quantity).Message;
            case "store":
                _service.Store(cityId, itemId, quantity, out var storeMessage);
                return storeMessage;
            case "take":
                _service.Take(cityId, itemId, quantity, out var takeMessage);
                return takeMessage;
            default:
                return $"Động từ '{verb}' không thuộc DLC market.";
        }
    }
}
