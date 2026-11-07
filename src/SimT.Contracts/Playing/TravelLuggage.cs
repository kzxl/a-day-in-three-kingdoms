namespace SimT.Contracts.Playing;

/// <summary>Regional price factor in permille: 1000 = baseline, 1120 = 12% dearer.</summary>
public sealed record CitadelDef(
    long Id, string Code, string NameVi, string NameEn, string Region, int PriceFactorPermille)
{
    public string NameHan => ResolveHanzi(Code);

    public static string ResolveHanzi(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "lac_duong" => "洛陽",
        "truong_an" => "長安",
        "nghiep_thanh" => "鄴城",
        "trac_huyen" => "涿縣",
        "quang_tong" => "廣宗",
        "binh_nguyen" => "平原",
        "hua_xuong" => "許昌",
        "bac_binh" => "北平",
        "ha_phi" => "下邳",
        "tuong_duong" => "襄陽",
        "xich_bich" => "赤壁",
        "giang_lang" => "江陵",
        "kien_nghiep" => "建業",
        "han_trung" => "漢中",
        "thanh_do" => "成都",
        "uyen_thanh" => "宛城",
        "tho_xuan" => "壽春",
        "thuong_dang" => "上黨",
        "tay_luong" => "西涼",
        "giao_chi" => "交趾",
        _ => string.Empty
    };
}

/// <summary>Detailed ecological, climatic, and regional specialties of a citadel.</summary>
public sealed record CitadelEcosystemDef(
    long CitadelId, string BiomeNameVi, string ClimateVi, string TopographyVi,
    string PrimarySpecialtyVi, string SecondarySpecialtyVi, int DangerLevel, double MapX, double MapY);

/// <summary>Inter-city route link connecting two citadels.</summary>
public sealed record RouteLinkDef(
    long FromCitadelId, long ToCitadelId, int Days, string RoadTypeVi = "Quan Đạo");

/// <summary>Travel style between citadels.</summary>
public enum TravelMode
{
    Walk = 0,
    Horse = 1,
    Caravan = 2
}

/// <summary>Cost, duration and route itinerary for inter-city travel.</summary>
public sealed record TravelQuote(
    long FromCitadelId, long ToCitadelId, int BaseDays, int ActualDays, long TravelFee,
    int RationsNeeded, int FatigueExpected, IReadOnlyList<long> PathNodeIds,
    string ModeNameVi, int CarryCapacity = TravelLuggage.WalkCapacity);

/// <summary>Sublocation / Point of Interest owned by a citadel.</summary>
public sealed record SublocationDef(long Id, long CitadelId, string Code, string NameVi, string Kind);

/// <summary>Read-only map owned by <c>base.world</c>.</summary>
public interface IWorldMap
{
    IReadOnlyList<CitadelDef> Citadels { get; }
    CitadelDef? Find(long citadelId);
    IReadOnlyList<long> RouteNeighbours(long citadelId);
    int TravelDays(long fromCitadelId, long toCitadelId);
    IReadOnlyList<SublocationDef> Sublocations(long citadelId);
    IReadOnlyList<RouteLinkDef> Routes { get; }
    CitadelEcosystemDef GetEcosystem(long citadelId);
    TravelQuote QuoteTravel(long fromCitadelId, long toCitadelId, TravelMode mode);
}

public enum StrollEventType
{
    Peaceful, NpcEncounter, MarketBargain, RighteousDeed, Epiphany, TeahouseRumor, KarmaFruition
}

public sealed record StrollChoiceOption(
    string Key, string LabelVi, string DescriptionVi, string? CostSummary = null, bool IsRecommended = false);

public sealed record StrollMerchantOffer(
    long ItemId, string ItemCode, string ItemNameVi, string CategoryVi,
    string VisualIcon, long UnitPrice, int Quantity, string DescriptionVi);

public sealed record StrollResult(
    bool Ok, string Title, string LocationName, string Narrative, string RewardSummary,
    StrollEventType EventType, int FatigueDelta, int StressRelief, bool RequiresChoice = false,
    IReadOnlyList<StrollChoiceOption>? Choices = null,
    IReadOnlyList<StrollMerchantOffer>? MerchantOffers = null, long TargetNpcId = 0);

public interface ICityStroll
{
    StrollResult Stroll(long cityId);
    StrollResult ResolveChoice(long cityId, string choiceKey, string? param = null);
}

public sealed record AvailableLuggageItem(
    long ItemId,
    string NameVi,
    int AvailableQty,
    long BasePrice);

/// <summary>
/// Single source of truth for travel mode carry capacities and deterministic luggage selection.
/// </summary>
public static class TravelLuggage
{
    public const int WalkCapacity = 40;
    public const int HorseCapacity = 20;
    public const int CaravanCapacity = 120;

    public static int ForMode(TravelMode mode) => mode switch
    {
        TravelMode.Horse => HorseCapacity,
        TravelMode.Caravan => CaravanCapacity,
        _ => WalkCapacity
    };

    public static (int UsedGao, int UsedMeat) ComputeRationsUsed(int rationsNeeded, int gaoQty, int meatQty)
    {
        var needed = Math.Max(0, rationsNeeded);
        var usedGao = Math.Min(needed, Math.Max(0, gaoQty));
        needed -= usedGao;
        var usedMeat = Math.Min(needed, Math.Max(0, meatQty));
        return (usedGao, usedMeat);
    }

    public static IReadOnlyList<AvailableLuggageItem> BuildAvailableAfterRations(
        IReadOnlyList<BagRow> rows,
        IItemCatalog? items,
        int rationsNeeded)
    {
        var gaoQty = rows.FirstOrDefault(r => r.ItemId == 1)?.Quantity ?? 0;
        var meatQty = rows.FirstOrDefault(r => r.ItemId == 8)?.Quantity ?? 0;
        var (usedGao, usedMeat) = ComputeRationsUsed(rationsNeeded, gaoQty, meatQty);

        var list = new List<AvailableLuggageItem>(rows.Count);
        foreach (var r in rows)
        {
            var rem = r.Quantity - (r.ItemId == 1 ? usedGao : r.ItemId == 8 ? usedMeat : 0);
            if (rem <= 0) continue;
            var def = items?.Find(r.ItemId);
            list.Add(new AvailableLuggageItem(
                r.ItemId,
                def?.NameVi ?? $"Vật phẩm #{r.ItemId}",
                rem,
                def?.BasePrice ?? 1));
        }

        return list
            .OrderByDescending(x => x.BasePrice)
            .ThenBy(x => x.ItemId)
            .ToList();
    }

    public static Dictionary<long, int> SelectDefaultLuggage(
        IReadOnlyList<AvailableLuggageItem> available,
        int capacity)
    {
        var selected = new Dictionary<long, int>();
        var remainingCap = Math.Max(0, capacity);
        foreach (var item in available)
        {
            if (remainingCap <= 0) break;
            var take = Math.Min(item.AvailableQty, remainingCap);
            if (take > 0)
            {
                selected[item.ItemId] = take;
                remainingCap -= take;
            }
        }
        return selected;
    }

    public static bool TryParseSpecSyntax(string? rawSpec)
    {
        if (string.IsNullOrWhiteSpace(rawSpec))
            return true;

        foreach (var token in rawSpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = token.Split('=');
            if (kv.Length != 2 ||
                !long.TryParse(kv[0], out var itemId) || itemId <= 0 ||
                !int.TryParse(kv[1], out _))
            {
                return false;
            }
        }
        return true;
    }

    public static bool TryValidateExplicitLuggage(
        string rawSpec,
        IReadOnlyList<AvailableLuggageItem> available,
        int capacity,
        out Dictionary<long, int> selected,
        out string errorVi)
    {
        selected = new Dictionary<long, int>();
        errorVi = string.Empty;
        var availMap = available.ToDictionary(x => x.ItemId);
        var total = 0;

        if (string.IsNullOrWhiteSpace(rawSpec))
            return true;

        foreach (var token in rawSpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = token.Split('=');
            if (kv.Length != 2 ||
                !long.TryParse(kv[0], out var itemId) || itemId <= 0 ||
                !int.TryParse(kv[1], out var qty))
            {
                errorVi = "Danh sách hành lý mang theo không đúng định dạng.";
                return false;
            }

            if (qty < 0)
            {
                errorVi = "Số lượng hành lý mang theo không được âm.";
                return false;
            }

            if (selected.ContainsKey(itemId))
            {
                errorVi = "Danh sách hành lý bị trùng vật phẩm.";
                return false;
            }

            var maxAvail = availMap.TryGetValue(itemId, out var a) ? a.AvailableQty : 0;
            if (qty > maxAvail)
            {
                var name = availMap.TryGetValue(itemId, out var def) ? def.NameVi : $"#{itemId}";
                errorVi = $"Số lượng {name} mang theo ({qty}) vượt quá tồn kho sau khi trừ lương ({maxAvail}).";
                return false;
            }

            total += qty;
            if (total > capacity)
            {
                errorVi = $"Tổng hành lý mang theo ({total}) vượt quá sức mang của phương tiện ({capacity} đơn vị).";
                return false;
            }

            if (qty > 0)
                selected[itemId] = qty;
        }

        return true;
    }

    public static string FormatItemQuantities(
        IEnumerable<(long ItemId, int Quantity)> rows,
        IItemCatalog? items)
    {
        var parts = rows
            .Where(r => r.Quantity > 0)
            .OrderBy(r => r.ItemId)
            .Select(r => $"{r.Quantity} {items?.Find(r.ItemId)?.NameVi ?? $"#{r.ItemId}"}")
            .ToList();
        return string.Join(", ", parts);
    }
}
