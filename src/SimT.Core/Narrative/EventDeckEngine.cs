using System.Globalization;
using SimT.Contracts.Narrative;

namespace SimT.Core.Narrative;

/// <summary>
/// Autonomous narrative event deck evaluating emergent world conditions and drawing historical encounters.
/// </summary>
public sealed class EventDeckEngine : IEventDeckEngine
{
    private readonly List<NarrativeEventCard> _deck = new();

    public void RegisterCard(NarrativeEventCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _deck.RemoveAll(c => c.EventId == card.EventId);
        _deck.Add(card);
    }

    public IReadOnlyList<NarrativeEventCard> EvaluateEligibleCards(IReadOnlyDictionary<string, object> worldState)
    {
        ArgumentNullException.ThrowIfNull(worldState);

        var eligible = new List<NarrativeEventCard>();
        foreach (var card in _deck)
        {
            if (IsCardEligible(card, worldState))
            {
                eligible.Add(card);
            }
        }
        return eligible;
    }

    public NarrativeEventCard? DrawEvent(IReadOnlyDictionary<string, object> worldState, uint randomSeed)
    {
        var eligible = EvaluateEligibleCards(worldState);
        if (eligible.Count == 0)
        {
            return null;
        }

        long totalWeight = 0;
        foreach (var card in eligible)
        {
            totalWeight += Math.Max(1, card.BaseWeight);
        }

        if (totalWeight <= 0)
        {
            return eligible[0];
        }

        long roll = randomSeed % totalWeight;
        long cumulative = 0;

        foreach (var card in eligible)
        {
            cumulative += Math.Max(1, card.BaseWeight);
            if (cumulative > roll)
            {
                return card;
            }
        }

        return eligible[^1];
    }

    private static bool IsCardEligible(NarrativeEventCard card, IReadOnlyDictionary<string, object> worldState)
    {
        if (card.Conditions == null || card.Conditions.Count == 0)
        {
            return true;
        }

        foreach (var condition in card.Conditions)
        {
            if (!EvaluateCondition(condition, worldState))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EvaluateCondition(NarrativeCondition condition, IReadOnlyDictionary<string, object> worldState)
    {
        bool hasProp = worldState.TryGetValue(condition.PropertyKey, out var actualVal);

        if (!hasProp || actualVal == null)
        {
            return condition.Operator switch
            {
                ConditionOperator.NotEquals => condition.TargetValue != null,
                _ => false
            };
        }

        var targetVal = condition.TargetValue;
        if (targetVal == null)
        {
            return condition.Operator switch
            {
                ConditionOperator.Equals => false,
                ConditionOperator.NotEquals => true,
                _ => false
            };
        }

        if (TryCompareNumeric(actualVal, targetVal, out double actualNum, out double targetNum))
        {
            return condition.Operator switch
            {
                ConditionOperator.Equals => Math.Abs(actualNum - targetNum) < 0.0001,
                ConditionOperator.NotEquals => Math.Abs(actualNum - targetNum) >= 0.0001,
                ConditionOperator.GreaterThan => actualNum > targetNum,
                ConditionOperator.GreaterThanOrEqual => actualNum >= targetNum,
                ConditionOperator.LessThan => actualNum < targetNum,
                ConditionOperator.LessThanOrEqual => actualNum <= targetNum,
                _ => false
            };
        }

        string actualStr = actualVal.ToString() ?? string.Empty;
        string targetStr = targetVal.ToString() ?? string.Empty;

        return condition.Operator switch
        {
            ConditionOperator.Equals => string.Equals(actualStr, targetStr, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotEquals => !string.Equals(actualStr, targetStr, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Contains => actualStr.Contains(targetStr, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryCompareNumeric(object a, object b, out double numA, out double numB)
    {
        if (TryToDouble(a, out numA) && TryToDouble(b, out numB))
        {
            return true;
        }
        numA = 0;
        numB = 0;
        return false;
    }

    private static bool TryToDouble(object val, out double result)
    {
        switch (val)
        {
            case sbyte sb: result = sb; return true;
            case byte b: result = b; return true;
            case short s: result = s; return true;
            case ushort us: result = us; return true;
            case int i: result = i; return true;
            case uint ui: result = ui; return true;
            case long l: result = l; return true;
            case ulong ul: result = ul; return true;
            case float f: result = f; return true;
            case double d: result = d; return true;
            case decimal dec: result = (double)dec; return true;
            case string str: return double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            default:
                result = 0;
                return false;
        }
    }
}
