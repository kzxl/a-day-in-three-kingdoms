using System.Text.Json;
using SimT.Contracts;
using SimT.Contracts.Config;

namespace SimT.Core.Config;

/// <summary>
/// K8 — typed, validated access to the shared constants file. The JSON shape is identical to
/// the legacy web project so both projects can be compared against the same document.
/// </summary>
public static class GameConstantsLoader
{
    public static GameConstants Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SimException("config.missing", $"Constants file not found: {path}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var currency = Require(root, "currency");
        var grain = Require(root, "grain");
        var calendar = Require(root, "calendar");
        var fatigue = Require(root, "fatigue");

        return new GameConstants(
            CurrencyNameVi: Text(currency, "vi"),
            CurrencyNameEn: Text(currency, "en"),
            GrainNameVi: Text(grain, "vi"),
            GrainNameEn: Text(grain, "en"),
            DaysPerMonth: Integer(calendar, "daysPerMonth"),
            MonthsPerYear: Integer(calendar, "monthsPerYear"),
            PhasesPerDay: Integer(calendar, "phasesPerDay"),
            FatigueCap: Integer(fatigue, "max"),
            GrainPerDay: Number(calendar, "grainPerDay"),
            DailyFatigueRecovery: Integer(calendar, "dailyFatigueRecovery"),
            FallbackDate: Text(calendar, "fallbackDate"));
    }

    private static JsonElement Require(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
            ? value
            : throw new SimException("config.missing_key", $"game-constants.json is missing '{name}'.");

    private static string Text(JsonElement parent, string name)
    {
        var value = Require(parent, name);
        return value.GetString() ?? throw new SimException("config.invalid_value", $"'{name}' must be a string.");
    }

    private static int Integer(JsonElement parent, string name)
    {
        var value = Require(parent, name);
        return value.GetInt32();
    }

    private static double Number(JsonElement parent, string name)
    {
        var value = Require(parent, name);
        return value.GetDouble();
    }
}
