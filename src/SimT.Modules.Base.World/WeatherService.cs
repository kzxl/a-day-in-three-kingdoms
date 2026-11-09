using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Base.World;

/// <summary>
/// Deterministic regional weather, 24 solar terms, and climate resonance engine.
/// </summary>
public sealed class WeatherService : IWeatherSystem
{
    private readonly IWorldMap _map;

    public WeatherService(IWorldMap map)
    {
        _map = map;
    }

    public CitadelWeatherDef GetWeather(long citadelId, GameDate date)
    {
        var (solarVi, solarEn) = ResolveSolarTerm(date);
        var citadel = _map.Find(citadelId);
        var region = citadel?.Region ?? "trung_nguyen";

        var type = ResolveWeatherType(citadelId, region, date);

        var (nameVi, nameEn, icon, descVi, moistureDelta, delayDays, basePriceFactor) = DescribeWeather(type, region, solarVi);

        return new CitadelWeatherDef(
            citadelId,
            date,
            type,
            solarVi,
            solarEn,
            nameVi,
            nameEn,
            icon,
            descVi,
            moistureDelta,
            delayDays,
            basePriceFactor);
    }

    public IReadOnlyList<CitadelWeatherDef> GetForecast(long citadelId, GameDate fromDate, int days = 7)
    {
        var list = new List<CitadelWeatherDef>(days);
        for (var i = 0; i < days; i++)
        {
            var date = GameDate.FromDayIndex(fromDate.DayIndex + i);
            list.Add(GetWeather(citadelId, date));
        }
        return list;
    }

    public string GetSolarTermName(GameDate date) => ResolveSolarTerm(date).NameVi;

    public int GetWeatherPriceFactorPermille(long citadelId, string itemCode, GameDate date)
    {
        var weather = GetWeather(citadelId, date);
        var code = itemCode.ToLowerInvariant();

        return weather.Type switch
        {
            WeatherType.Blizzard or WeatherType.Snow => code switch
            {
                "coal" or "iron" => 1250,
                "wine" or "cloth" => 1180,
                _ => 1000
            },
            WeatherType.Drought => code switch
            {
                "rice" or "wheat" or "dried_fish" => 1300,
                "tea" => 1150,
                _ => 1000
            },
            WeatherType.Downpour => code switch
            {
                "salt" or "medicine" => 1200,
                _ => 1000
            },
            _ => 1000
        };
    }

    public int GetCultivationGrowthBonus(long citadelId, GameDate date)
    {
        var weather = GetWeather(citadelId, date);
        return weather.Type switch
        {
            WeatherType.Rain => 15,        // +15% growth speed in nourishing spring rain
            WeatherType.Downpour => -10,   // -10% growth speed due to flooded soil
            WeatherType.Drought => -20,    // -20% growth speed under parched drought
            WeatherType.Blizzard => -25,   // -25% growth speed under frost/blizzard
            _ => 0
        };
    }

    private static (string NameVi, string NameEn) ResolveSolarTerm(GameDate date)
    {
        var isSecondHalf = date.Day > 15;
        return (date.Month, isSecondHalf) switch
        {
            (1, false) => ("Lập Xuân", "Beginning of Spring"),
            (1, true) => ("Vũ Thủy", "Rain Water"),
            (2, false) => ("Kinh Trập", "Awakening of Insects"),
            (2, true) => ("Xuân Phân", "Spring Equinox"),
            (3, false) => ("Thanh Minh", "Pure Brightness"),
            (3, true) => ("Cốc Vũ", "Grain Rain"),
            (4, false) => ("Lập Hạ", "Beginning of Summer"),
            (4, true) => ("Tiểu Mãn", "Grain Buds"),
            (5, false) => ("Mang Chủng", "Grain in Ear"),
            (5, true) => ("Hạ Chí", "Summer Solstice"),
            (6, false) => ("Tiểu Thử", "Minor Heat"),
            (6, true) => ("Đại Thử", "Major Heat"),
            (7, false) => ("Lập Thu", "Beginning of Autumn"),
            (7, true) => ("Xử Thử", "End of Heat"),
            (8, false) => ("Bạch Lộ", "White Dew"),
            (8, true) => ("Thu Phân", "Autumn Equinox"),
            (9, false) => ("Hàn Lộ", "Cold Dew"),
            (9, true) => ("Sương Giáng", "Frost's Descent"),
            (10, false) => ("Lập Đông", "Beginning of Winter"),
            (10, true) => ("Tiểu Tuyết", "Minor Snow"),
            (11, false) => ("Đại Tuyết", "Major Snow"),
            (11, true) => ("Đông Chí", "Winter Solstice"),
            (12, false) => ("Tiểu Hàn", "Minor Cold"),
            (12, true) => ("Đại Hàn", "Major Cold"),
            _ => ("Tiết Khí", "Solar Term")
        };
    }

    private static WeatherType ResolveWeatherType(long citadelId, string region, GameDate date)
    {
        // Replay determinism guard: Lạc Dương in Month 1 (days 1..10) is always Clear
        if (citadelId == 1 && date.Month == 1 && date.Day <= 10)
        {
            return WeatherType.Clear;
        }

        // Pseudo-random deterministic roll from date and citadel
        var hash = (uint)((date.DayIndex * 31337) ^ (citadelId * 7919) ^ 0x9E3779B9);
        var roll = (int)(hash % 100);

        var month = date.Month;

        // Winter (Month 10, 11, 12)
        if (month is 10 or 11 or 12)
        {
            if (region is "ha_bac" or "tay_bac")
            {
                if (roll < 35) return WeatherType.Snow;
                if (roll < 60) return WeatherType.Blizzard;
                if (roll < 80) return WeatherType.Cloudy;
                return WeatherType.Clear;
            }
            if (region is "giang_dong" or "linh_nam")
            {
                if (roll < 25) return WeatherType.Rain;
                if (roll < 60) return WeatherType.Cloudy;
                return WeatherType.Clear;
            }
            // Trung Nguyên, Kinh Sở, Ba Thục
            if (roll < 20) return WeatherType.Snow;
            if (roll < 45) return WeatherType.Fog;
            if (roll < 75) return WeatherType.Cloudy;
            return WeatherType.Clear;
        }

        // Summer (Month 4, 5, 6)
        if (month is 4 or 5 or 6)
        {
            if (region is "ha_bac" or "tay_bac")
            {
                if (roll < 30) return WeatherType.Drought;
                if (roll < 70) return WeatherType.Clear;
                if (roll < 90) return WeatherType.Cloudy;
                return WeatherType.Rain;
            }
            if (region is "giang_dong" or "kinh_so")
            {
                if (roll < 35) return WeatherType.Downpour;
                if (roll < 65) return WeatherType.Rain;
                if (roll < 85) return WeatherType.Cloudy;
                return WeatherType.Clear;
            }
            // Trung Nguyên & Ba Thục
            if (roll < 20) return WeatherType.Drought;
            if (roll < 45) return WeatherType.Rain;
            if (roll < 80) return WeatherType.Clear;
            return WeatherType.Cloudy;
        }

        // Autumn (Month 7, 8, 9)
        if (month is 7 or 8 or 9)
        {
            if (region is "giang_dong" or "kinh_so")
            {
                if (roll < 30) return WeatherType.Fog;
                if (roll < 60) return WeatherType.Rain;
                return WeatherType.Clear;
            }
            if (region is "ba_thuc")
            {
                if (roll < 40) return WeatherType.Fog;
                if (roll < 70) return WeatherType.Cloudy;
                return WeatherType.Clear;
            }
            // Hà Bắc & Trung Nguyên
            if (roll < 25) return WeatherType.Cloudy;
            if (roll < 45) return WeatherType.Rain;
            return WeatherType.Clear;
        }

        // Spring (Month 1, 2, 3)
        if (roll < 30) return WeatherType.Rain;
        if (roll < 50) return WeatherType.Cloudy;
        if (roll < 65 && region is "kinh_so" or "ba_thuc") return WeatherType.Fog;
        return WeatherType.Clear;
    }

    private static (string NameVi, string NameEn, string Icon, string DescVi, int MoistureDelta, int DelayDays, int PriceFactor) DescribeWeather(
        WeatherType type, string region, string solarVi) => type switch
    {
        WeatherType.Clear => (
            "Trời Quang Nắng Ấm",
            "Clear Sky",
            "☀️",
            $"Tiết {solarVi}, bầu trời quang đãng, nắng ấm chan hòa. Giao thương hanh thông, cây cỏ tốt tươi.",
            0,
            0,
            1000),

        WeatherType.Cloudy => (
            "Âm U Nhiều Mây",
            "Cloudy",
            "⛅",
            $"Mây lành che mát, tiết trời dịu mát dễ chịu, thích hợp cho việc du ngoạn dạo phố và gặp gỡ danh sĩ.",
            0,
            0,
            1000),

        WeatherType.Rain => (
            "Mưa Rào Mát Lành",
            "Refreshing Rain",
            "🌧️",
            $"Mưa xuân tưới mát ruộng đồng, bổ sung độ ẩm đất (+10 Thủy Lợi), cây trồng phát triển tươi tốt mà không tốn công tưới.",
            10,
            0,
            1000),

        WeatherType.Downpour => (
            "Giông Bão Mưa Lớn",
            "Heavy Storm",
            "⛈️",
            $"Mưa to gió lớn làm đường sá trơn trượt lầy lội. Tiêu cục vận chuyển kéo dài thêm +1 ngày, muối ăn và thảo dược sốt giá.",
            20,
            1,
            1100),

        WeatherType.Fog => (
            "Sương Mù Bến Sông",
            "River Fog",
            "🌫️",
            $"Sương mù dày đặc che khuất tầm nhìn mặt sông. Thủy lộ cần cảnh giác, bù lại độ ẩm tự nhiên được duy trì ổn định.",
            5,
            0,
            1050),

        WeatherType.Snow => (
            "Tuyết Rơi Giá Lạnh",
            "Gentle Snow",
            "🌨️",
            $"Tuyết trắng rơi nhẹ phủ kín mái ngói. Nhu cầu than củi sưởi ấm, áo da lông cừu và rượu ấm tại thành thị tăng mạnh.",
            5,
            0,
            1150),

        WeatherType.Blizzard => (
            "Bão Tuyết Phương Bắc",
            "Northern Blizzard",
            "❄️",
            $"Bão tuyết dữ dội, gió bắc thấu xương làm tê liệt các tuyến quan đạo (+2 ngày trễ hạn). Giá than củi, áo ấm và rượu tăng vọt!",
            0,
            2,
            1250),

        WeatherType.Drought => (
            "Nắng Gắt Đại Hạn",
            "Scorching Drought",
            "🏜️",
            $"Nắng hạn gay gắt, độ ẩm đất bốc hơi nhanh (-15 Thủy Lợi). Ruộng cần được dẫn nước khẩn cấp, giá lương thực gạo thóc tăng phi mã!",
            -15,
            0,
            1250),

        _ => ("Trời Quang", "Clear", "☀️", "Thời tiết bình ổn.", 0, 0, 1000)
    };
}
