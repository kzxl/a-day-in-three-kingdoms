using SimT.Contracts.Playing;
using SimT.Contracts.Time;
using SimT.Core.Kernel;
using Xunit;

namespace SimT.Modules.Dlc.Tests;

public sealed class WeatherSystemTests : IDisposable
{
    private readonly DlcTestHost _host = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public void WeatherSystem_capability_is_registered_and_generates_weather()
    {
        using var kernel = _host.Boot();
        var weatherSys = kernel.Capabilities.Get<IWeatherSystem>(CapabilityIds.WeatherSystem);
        Assert.NotNull(weatherSys);

        var today = new GameDate(184, 1, 1);
        var weather = weatherSys.GetWeather(1, today);

        Assert.NotNull(weather);
        Assert.Equal(1, weather.CitadelId);
        Assert.Equal(today, weather.Date);
        Assert.Equal(WeatherType.Clear, weather.Type);
        Assert.Equal("Lập Xuân", weather.SolarTermNameVi);
        Assert.Equal("Beginning of Spring", weather.SolarTermNameEn);
        Assert.Equal("Trời Quang Nắng Ấm", weather.NameVi);
        Assert.Equal("☀️", weather.Icon);
        Assert.Equal(1000, weatherSys.GetWeatherPriceFactorPermille(1, "rice", today));
    }

    [Fact]
    public void WeatherSystem_resolves_all_24_solar_terms_across_12_months()
    {
        using var kernel = _host.Boot();
        var weatherSys = kernel.Capabilities.Get<IWeatherSystem>(CapabilityIds.WeatherSystem);

        var terms = new HashSet<string>();
        for (var month = 1; month <= 12; month++)
        {
            var termEarly = weatherSys.GetSolarTermName(new GameDate(184, month, 5));
            var termLate = weatherSys.GetSolarTermName(new GameDate(184, month, 20));

            terms.Add(termEarly);
            terms.Add(termLate);
            Assert.False(string.IsNullOrWhiteSpace(termEarly));
            Assert.False(string.IsNullOrWhiteSpace(termLate));
            Assert.NotEqual(termEarly, termLate);
        }

        Assert.Equal(24, terms.Count);
        Assert.Contains("Tiểu Hàn", terms);
        Assert.Contains("Đại Hàn", terms);
        Assert.Contains("Lập Xuân", terms);
        Assert.Contains("Vũ Thủy", terms);
        Assert.Contains("Thanh Minh", terms);
        Assert.Contains("Cốc Vũ", terms);
        Assert.Contains("Hạ Chí", terms);
        Assert.Contains("Bạch Lộ", terms);
        Assert.Contains("Sương Giáng", terms);
        Assert.Contains("Lập Đông", terms);
        Assert.Contains("Đông Chí", terms);
    }

    [Fact]
    public void WeatherSystem_generates_forecast_with_expected_length_and_dates()
    {
        using var kernel = _host.Boot();
        var weatherSys = kernel.Capabilities.Get<IWeatherSystem>(CapabilityIds.WeatherSystem);

        var startDate = new GameDate(184, 3, 10);
        var forecast = weatherSys.GetForecast(2, startDate, 5);

        Assert.Equal(5, forecast.Count);
        for (var i = 0; i < forecast.Count; i++)
        {
            var expectedDayIndex = startDate.DayIndex + i;
            var expectedDate = GameDate.FromDayIndex(expectedDayIndex);
            Assert.Equal(expectedDate, forecast[i].Date);
            Assert.False(string.IsNullOrWhiteSpace(forecast[i].NameVi));
            Assert.False(string.IsNullOrWhiteSpace(forecast[i].Icon));
        }
    }

    [Fact]
    public void WeatherSystem_resonance_metrics_are_bounded_and_consistent()
    {
        using var kernel = _host.Boot();
        var weatherSys = kernel.Capabilities.Get<IWeatherSystem>(CapabilityIds.WeatherSystem);

        for (var cityId = 1; cityId <= 5; cityId++)
        {
            for (var day = 1; day <= 30; day += 7)
            {
                var date = new GameDate(184, 4, day);
                var weather = weatherSys.GetWeather(cityId, date);

                Assert.True(weather.CultivationMoistureDelta >= -25 && weather.CultivationMoistureDelta <= 25);
                Assert.True(weather.CaravanTravelDelayDays >= 0 && weather.CaravanTravelDelayDays <= 3);

                var bonus = weatherSys.GetCultivationGrowthBonus(cityId, date);
                Assert.True(bonus >= -300 && bonus <= 300);

                var priceMult = weatherSys.GetWeatherPriceFactorPermille(cityId, "gao", date);
                Assert.True(priceMult >= 700 && priceMult <= 2000);
            }
        }
    }
}
