namespace SimT.Contracts.Narrative;

/// <summary>
/// Comparison operator for narrative event trigger conditions.
/// </summary>
public enum ConditionOperator
{
    Equals = 0,
    NotEquals = 1,
    GreaterThan = 2,
    GreaterThanOrEqual = 3,
    LessThan = 4,
    LessThanOrEqual = 5,
    Contains = 6
}

/// <summary>
/// Atomic prerequisite condition tested against runtime world state.
/// </summary>
public sealed record NarrativeCondition(
    string PropertyKey,
    ConditionOperator Operator,
    object TargetValue);

/// <summary>
/// Self-contained narrative event card drawn procedurally from the event deck.
/// </summary>
public sealed record NarrativeEventCard(
    string EventId,
    string TitleKey,
    string DescriptionKey,
    int BaseWeight,
    IReadOnlyList<NarrativeCondition> Conditions,
    IReadOnlyList<string> ChoiceKeys);

/// <summary>
/// Autonomous narrative event deck evaluating emergent world conditions and drawing historical encounters.
/// </summary>
public interface IEventDeckEngine
{
    void RegisterCard(NarrativeEventCard card);
    IReadOnlyList<NarrativeEventCard> EvaluateEligibleCards(IReadOnlyDictionary<string, object> worldState);
    NarrativeEventCard? DrawEvent(IReadOnlyDictionary<string, object> worldState, uint randomSeed);
}
