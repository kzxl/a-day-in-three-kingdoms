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

        var constants = new GameConstants(
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

        Validate(constants);
        return constants;
    }

    /// <summary>
    /// A zero or absurd calendar value would silently divide by zero or freeze time later, so it
    /// is rejected at load time with the key that is wrong.
    /// </summary>
    private static void Validate(GameConstants constants)
    {
        if (constants.DaysPerMonth is < 1 or > 60)
        {
            throw new SimException("config.invalid_value", $"daysPerMonth must be 1..60, got {constants.DaysPerMonth}.");
        }

        if (constants.MonthsPerYear is < 1 or > 24)
        {
            throw new SimException("config.invalid_value", $"monthsPerYear must be 1..24, got {constants.MonthsPerYear}.");
        }

        if (constants.PhasesPerDay is < 1 or > 12)
        {
            throw new SimException("config.invalid_value", $"phasesPerDay must be 1..12, got {constants.PhasesPerDay}.");
        }

        if (constants.FatigueCap < 0 || constants.GrainPerDay < 0 || constants.DailyFatigueRecovery < 0)
        {
            throw new SimException("config.invalid_value", "fatigue and grain values cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(constants.CurrencyNameVi) || string.IsNullOrWhiteSpace(constants.GrainNameVi))
        {
            throw new SimException("config.invalid_value", "currency and grain names are required.");
        }
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
