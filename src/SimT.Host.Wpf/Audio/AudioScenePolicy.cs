using SimT.Contracts.Scenes;

namespace SimT.Host.Wpf.Audio;

public sealed record AudioScenePlan(string Music, string[] Ambience);

public static class AudioScenePolicy
{
    public static AudioScenePlan Select(object? scene, bool hasWar = false) => scene switch
    {
        MapScene => new("map", []),
        DuelScene => new("duel", []),
        EventScene => new("event", hasWar ? ["war"] : []),
        DebateScene debate => new("city", !debate.IsFinished && debate.MaxTurns > 0 && debate.Turn >= debate.MaxTurns - 1 ? ["war"] : []),
        CityScene city => new("city", CityAmbience(city.Season, city.WeatherEffect)),
        _ => new("city", [])
    };

    public static string[] CityAmbience(string season, string weather)
    {
        var layers = new List<string> { "ambience.market" }; // Optional Part B cue; absent keys are silent.
        var normalized = season.ToLowerInvariant();
        if (normalized.Contains("xuân") || normalized.Contains("hạ") || normalized is "spring" or "summer")
            layers.Add("birds");
        if (normalized.Contains("đông") || normalized == "winter" || weather == "snow") layers.Add("wind");
        if (weather.Contains("rain", StringComparison.OrdinalIgnoreCase) || weather.Contains("mưa", StringComparison.OrdinalIgnoreCase))
            layers.Add("rain");
        return layers.ToArray();
    }

}
