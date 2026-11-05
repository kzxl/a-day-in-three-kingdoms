namespace SimT.Contracts.Ui;

/// <summary>
/// Structured URI descriptor for screen commands and actions (e.g. "buy:12:10", "item:modal:5", "work:nong_phu").
/// Replaces brittle ad-hoc string splitting with a zero-allocation, strongly-typed parser and formatter.
/// </summary>
public readonly record struct CommandUri(string Action, string? Target = null, string? Parameter = null, string? Extra = null)
{
    public static CommandUri Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new CommandUri(string.Empty);
        }

        var parts = raw.Split(':', 4);
        return parts.Length switch
        {
            1 => new CommandUri(parts[0]),
            2 => new CommandUri(parts[0], parts[1]),
            3 => new CommandUri(parts[0], parts[1], parts[2]),
            _ => new CommandUri(parts[0], parts[1], parts[2], parts[3])
        };
    }

    public static string Format(string action, object? target = null, object? param = null, object? extra = null)
    {
        if (extra is not null) return $"{action}:{target}:{param}:{extra}";
        if (param is not null) return $"{action}:{target}:{param}";
        if (target is not null) return $"{action}:{target}";
        return action;
    }

    public bool TryGetTargetLong(out long value) => long.TryParse(Target, out value);

    public bool TryGetParamInt(out int value) => int.TryParse(Parameter, out value);

    public bool TryGetParamLong(out long value) => long.TryParse(Parameter, out value);

    public override string ToString() => Format(Action, Target, Parameter, Extra);
}
