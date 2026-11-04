using System.Data.Common;

namespace SimT.Contracts.Data;

/// <summary>
/// One scoped database session. Modules never open their own connection: the kernel
/// hands each module a session and closes it when the turn or request ends.
/// </summary>
public interface IDbSession
{
    string DatabasePath { get; }

    int Execute(string sql, DbArgs? args = null);

    long ScalarLong(string sql, DbArgs? args = null);

    double ScalarDouble(string sql, DbArgs? args = null);

    string? ScalarString(string sql, DbArgs? args = null);

    List<T> Query<T>(string sql, Func<DbRecord, T> map, DbArgs? args = null);

    T? QueryOne<T>(string sql, Func<DbRecord, T> map, DbArgs? args = null)
        where T : class;

    /// <summary>Runs <paramref name="body"/> inside one transaction; re-entrant via savepoints.</summary>
    void Transaction(Action body);
}

/// <summary>
/// Read-only row view. Deliberately not <see cref="DbDataReader"/> so Contracts stays
/// free of driver types and modules can be tested with an in-memory fake.
/// </summary>
public readonly struct DbRecord
{
    private readonly DbDataReader _reader;

    public DbRecord(DbDataReader reader) => _reader = reader;

    public long GetInt64(int ordinal) => _reader.GetInt64(ordinal);

    public int GetInt32(int ordinal) => _reader.GetInt32(ordinal);

    public double GetDouble(int ordinal) => _reader.GetDouble(ordinal);

    public string GetString(int ordinal) => _reader.GetString(ordinal);

    public string GetStringOrEmpty(int ordinal) => _reader.IsDBNull(ordinal) ? string.Empty : _reader.GetString(ordinal);

    public string? GetStringOrNull(int ordinal) => _reader.IsDBNull(ordinal) ? null : _reader.GetString(ordinal);

    public bool IsDbNull(int ordinal) => _reader.IsDBNull(ordinal);

    public bool GetBool(int ordinal) => _reader.GetInt64(ordinal) != 0;
}

/// <summary>Statement parameters. Keys include the <c>@</c> prefix.</summary>
public sealed class DbArgs
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public static DbArgs Empty => new();

    public static DbArgs One(string name, object? value) => new DbArgs().Add(name, value);

    public DbArgs Add(string name, object? value)
    {
        _values[name] = value;
        return this;
    }

    public IReadOnlyDictionary<string, object?> Values => _values;
}
