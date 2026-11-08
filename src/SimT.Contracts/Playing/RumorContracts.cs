using SimT.Contracts.Scenes;

namespace SimT.Contracts.Playing;

public static class TavernRules
{
    public const int CostQuan = 20;
    public const int CostDays = 0;
    public const int LifetimeDays = 10;
    public const int BatchSize = 3;
    public const double FalseProbability = 0.20;
    public const string ActionCode = "society.tavern";
}

public sealed record RumorView(
    long Id, long HeardCityId, long HeardDay, long ExpiresDay, string Kind,
    string TitleVi, string NarrativeVi, bool? VerifiedTruth,
    long? ItemId, long? TargetCityId, long? NpcId, long? KarmaSeedId,
    string? EventKey, long? EventDay)
{
    public bool IsExpired(long day) => day >= ExpiresDay;
    public string StatusVi => VerifiedTruth switch { true => "Đúng", false => "Sai", _ => "Chưa rõ" };
}

public interface ITavernRumors
{
    IReadOnlyList<RumorView> Listen();
    IReadOnlyList<RumorView> ActiveAt(long cityId);
    IReadOnlyList<RumorView> History();
}

/// <summary>Optional event feed. Historical events can publish a separate capability using this contract.</summary>
public interface IEventRumorSource
{
    IReadOnlyList<RumorEventHint> Upcoming(long day, int horizonDays);
    bool HasOccurred(string eventKey, long day);
}

public sealed record RumorEventHint(string Key, long CityId, long Day, string TitleVi, string NarrativeVi);

/// <summary>Shared scene policy keeps the optional DLC command out of hosts without society.</summary>
public static class TavernScene
{
    public static CityHotspot? Hotspot(IReadOnlyList<SublocationDef> locations, bool enabled)
    {
        if (!enabled) return null;
        var tavern = locations.FirstOrDefault(s => s.Kind is "tavern" or "inn"
            || s.NameVi.Contains("Tửu", StringComparison.OrdinalIgnoreCase)
            || s.NameVi.Contains("Quán trọ", StringComparison.OrdinalIgnoreCase));
        return new("society.tavern", "Tửu quán", tavern?.NameVi ?? "Uống rượu nghe chuyện",
            "society.tavern", 15, 34);
    }
}
