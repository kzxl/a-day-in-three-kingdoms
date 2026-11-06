using SimT.Contracts.Narrative;
using SimT.Contracts.Primitives;

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

        if (IsNumericType(actualVal) && IsNumericType(targetVal))
        {
            double actualNum = FastConvert.AsDouble(actualVal);
            double targetNum = FastConvert.AsDouble(targetVal);

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

        string actualStr = FastConvert.AsString(actualVal);
        string targetStr = FastConvert.AsString(targetVal);

        return condition.Operator switch
        {
            ConditionOperator.Equals => string.Equals(actualStr, targetStr, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotEquals => !string.Equals(actualStr, targetStr, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Contains => actualStr.Contains(targetStr, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool IsNumericType(object val) => val switch
    {
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal => true,
        string str => double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _),
        _ => false
    };
}
