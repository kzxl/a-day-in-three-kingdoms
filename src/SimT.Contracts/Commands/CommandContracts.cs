namespace SimT.Contracts.Commands;

/// <summary>
/// Pre-execution validation outcome. Enables UI to disable buttons and display an actionable reason.
/// </summary>
public sealed record CommandValidation(bool CanExecute, string? Reason = null)
{
    public static readonly CommandValidation Allowed = new(true);

    public static CommandValidation Refuse(string reason) => new(false, reason);
}

/// <summary>
/// Computed preview of consequences before committing an action (time, cash, and impacts).
/// </summary>
public sealed record CommandPreview(
    string Summary,
    int CostDays = 0,
    long CostAmount = 0,
    IReadOnlyList<string>? Effects = null);

/// <summary>
/// Outcome of executing a structured command.
/// </summary>
public sealed record CommandResult(
    bool Success,
    string Message,
    object? Payload = null)
{
    public static CommandResult Ok(string message, object? payload = null) => new(true, message, payload);

    public static CommandResult Fail(string message) => new(false, message);
}

/// <summary>
/// Context provided to a command during validation, preview, and execution.
/// </summary>
public interface ICommandContext
{
    string ActorId { get; }

    T GetCapability<T>(string capabilityId) where T : class;

    bool TryGetCapability<T>(string capabilityId, out T? capability) where T : class;
}

/// <summary>
/// A 3-phase structured command: Validation -> Preview -> Execution.
/// Replaces opaque string commands with a strongly-typed, verifiable contract.
/// </summary>
public interface IStructuredCommand
{
    string CommandId { get; }

    CommandValidation CanExecute(ICommandContext context);

    CommandPreview Preview(ICommandContext context);

    CommandResult Execute(ICommandContext context);
}

/// <summary>
/// Central registry and dispatcher for structured commands.
/// </summary>
public interface ICommandDispatcher
{
    void Register(IStructuredCommand command);

    bool TryGet(string commandId, out IStructuredCommand? command);

    CommandResult Dispatch(string commandId, ICommandContext context);
}
