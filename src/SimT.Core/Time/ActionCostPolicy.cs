using System.Text.Json;
using SimT.Contracts;
using SimT.Contracts.Time;

namespace SimT.Core.Time;

/// <summary>
/// K2 — action costs are data. The table ships in <c>assets/config/action-costs.json</c> and the
/// default covers anything not listed, so a new DLC action costs time even if nobody remembered
/// to add a row.
/// </summary>
public sealed class ActionCostPolicy : IActionCostPolicy
{
    private const int MaxDaysPerAction = 30;

    private readonly Dictionary<string, ActionCost> _costs = new(StringComparer.Ordinal);

    private ActionCostPolicy(ActionCost fallback) => Fallback = fallback;

    public ActionCost Fallback { get; }

    public IReadOnlyDictionary<string, ActionCost> All => _costs;

    public ActionCost Of(string actionCode) =>
        _costs.TryGetValue(actionCode, out var cost) ? cost : Fallback;

    public static ActionCostPolicy Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SimException("config.missing", $"Action cost table not found: {path}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var fallback = ReadCost(root.GetProperty("default"), "default");
        var policy = new ActionCostPolicy(fallback);

        if (root.TryGetProperty("actions", out var actions))
        {
            foreach (var property in actions.EnumerateObject())
            {
                policy._costs[property.Name] = ReadCost(property.Value, property.Name);
            }
        }

        return policy;
    }

    /// <summary>Table with no file: every action costs one day. Used by tests that only need a clock.</summary>
    public static ActionCostPolicy Default() => new(new ActionCost(1));

    private static ActionCost ReadCost(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new SimException("config.invalid_value", $"action-costs.json: '{name}' must be an object.");
        }

        if (!element.TryGetProperty("days", out var daysElement) || !daysElement.TryGetInt32(out var days))
        {
            throw new SimException("config.invalid_value", $"action-costs.json: '{name}.days' must be a number.");
        }

        if (days < 0 || days > MaxDaysPerAction)
        {
            throw new SimException("config.invalid_value", $"action-costs.json: '{name}.days' must be 0..{MaxDaysPerAction}, got {days}.");
        }

        return new ActionCost(days);
    }
}
