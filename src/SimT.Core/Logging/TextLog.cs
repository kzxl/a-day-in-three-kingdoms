using SimT.Contracts.Logging;

namespace SimT.Core.Logging;

/// <summary>K9 — minimal logger. The host may redirect the sink; modules only see the interface.</summary>
public sealed class TextLog : ILog
{
    private readonly string _scope;
    private readonly Action<string> _sink;

    public TextLog(string scope, Action<string>? sink = null)
    {
        _scope = scope;
        _sink = sink ?? (line => Console.WriteLine(line));
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? error = null) =>
        Write("ERROR", error is null ? message : $"{message} :: {error.GetType().Name}: {error.Message}");

    private void Write(string level, string message) => _sink($"[{level}] {_scope}: {message}");
}
