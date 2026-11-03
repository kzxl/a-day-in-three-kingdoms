namespace SimT.Contracts;

/// <summary>All simulation failures carry a stable code plus the operation that failed.</summary>
public class SimException : Exception
{
    public SimException(string code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    public string Code { get; }
}
