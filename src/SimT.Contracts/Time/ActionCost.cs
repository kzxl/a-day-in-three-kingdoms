namespace SimT.Contracts.Time;

/// <summary>
/// Stable action codes, so a cost table can be data instead of an if-chain. A module may add its
/// own code; unknown codes fall back to the policy default.
/// </summary>
public static class ActionCodes
{
    public const string TradeBuy = "trade.buy";
    public const string TradeSell = "trade.sell";
    public const string MarketRent = "market.rent";
    public const string StallRent = "market.stall_rent";
    public const string StallStock = "market.stall_stock";
    public const string WarehouseMove = "market.warehouse_move";
    public const string CaravanSend = "caravan.send";
    public const string CaravanTravel = "caravan.travel";
    public const string ProfessionWork = "profession.work";
    public const string FarmRent = "farm.rent";
    public const string FarmPlant = "farm.plant";
    public const string FarmHarvest = "farm.harvest";
    public const string FarmFertilise = "farm.fertilise";
    public const string LineageMarry = "lineage.marry";
    public const string LineageBeget = "lineage.beget";
    public const string PoliticsServe = "politics.serve";
}

/// <summary>
/// What one action costs: whole days off the calendar. Time is the currency of every meaningful
/// action, which is what makes the calendar and the month close matter.
/// </summary>
public readonly record struct ActionCost(int Days)
{
    public static ActionCost Free { get; } = new(0);

    public bool IsFree => Days == 0;

    public override string ToString() => Days == 0 ? "không tốn ngày" : $"{Days} ngày";
}

/// <summary>A validated cost for a specific action, ready to show or charge.</summary>
public readonly record struct ActionTimeQuote(string ActionCode, ActionCost Cost)
{
    public int Days => Cost.Days;

    public override string ToString() => Cost.ToString();
}
