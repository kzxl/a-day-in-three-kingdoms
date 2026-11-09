using SimT.Contracts.Time;

namespace SimT.Contracts.Playing;

/// <summary>
/// Major weather patterns across the Three Kingdoms.
/// </summary>
public enum WeatherType
{
    Clear = 0,     // Trời quang nắng ấm ☀️
    Cloudy = 1,    // Âm u nhiều mây ⛅
    Rain = 2,      // Mưa rào mát lành 🌧️
    Downpour = 3,  // Giông bão mưa lớn ⛈️
    Fog = 4,       // Sương mù bến sông 🌫️
    Snow = 5,      // Tuyết rơi giá lạnh 🌨️
    Blizzard = 6,  // Bão tuyết phương bắc ❄️
    Drought = 7    // Nắng gắt đại hạn 🏜️
}

/// <summary>
/// Snapshot of weather condition at a specific citadel on a given date.
/// </summary>
public sealed record CitadelWeatherDef(
    long CitadelId,
    GameDate Date,
    WeatherType Type,
    string SolarTermNameVi,
    string SolarTermNameEn,
    string NameVi,
    string NameEn,
    string Icon,
    string DescriptionVi,
    int CultivationMoistureDelta,
    int CaravanTravelDelayDays,
    int PriceFactorPermille);

/// <summary>
/// Contract for querying and forecasting regional weather and solar terms across the realm.
/// </summary>
public interface IWeatherSystem
{
    /// <summary>Gets the weather condition for a citadel on a specific game date.</summary>
    CitadelWeatherDef GetWeather(long citadelId, GameDate date);

    /// <summary>Generates a multi-day weather forecast outlook for a citadel.</summary>
    IReadOnlyList<CitadelWeatherDef> GetForecast(long citadelId, GameDate fromDate, int days = 7);

    /// <summary>Resolves the 24 Solar Terms (Tiết khí) name for a given date.</summary>
    string GetSolarTermName(GameDate date);

    /// <summary>Calculates weather-driven supply and demand price adjustment (1000 = baseline 1.0x).</summary>
    int GetWeatherPriceFactorPermille(long citadelId, string itemCode, GameDate date);

    /// <summary>Calculates cultivation growth rate bonus percentage (+10 = +10% speed).</summary>
    int GetCultivationGrowthBonus(long citadelId, GameDate date);
}
