namespace SimT.Contracts.Logging;

/// <summary>Module-scoped logger. Kernel owns the implementation, modules only write.</summary>
public interface ILog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? error = null);
}
