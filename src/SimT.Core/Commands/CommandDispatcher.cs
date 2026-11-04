using SimT.Contracts;
using SimT.Contracts.Commands;

namespace SimT.Core.Commands;

/// <summary>
/// Default implementation of structured command registry and dispatcher.
/// Validates commands prior to execution and returns structured execution results.
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly Dictionary<string, IStructuredCommand> _commands = new(StringComparer.Ordinal);

    public void Register(IStructuredCommand command)
    {
        if (!_commands.TryAdd(command.CommandId, command))
        {
            throw new SimException("command.duplicate", $"Command '{command.CommandId}' is already registered.");
        }
    }

    public bool TryGet(string commandId, out IStructuredCommand? command) =>
        _commands.TryGetValue(commandId, out command);

    public CommandResult Dispatch(string commandId, ICommandContext context)
    {
        if (!_commands.TryGetValue(commandId, out var command))
        {
            return CommandResult.Fail($"Command '{commandId}' not found.");
        }

        var validation = command.CanExecute(context);
        if (!validation.CanExecute)
        {
            return CommandResult.Fail(validation.Reason ?? "Action cannot be executed.");
        }

        return command.Execute(context);
    }
}
