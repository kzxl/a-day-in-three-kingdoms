using System.Data.Common;
using Microsoft.Data.Sqlite;
using SimT.Contracts;
using SimT.Contracts.Data;

namespace SimT.Core.Data;

/// <summary>
/// K1 — the one SQLite driver. Modules receive this through <c>ModuleContext</c> and never
/// open their own connection, so transaction boundaries stay visible.
/// </summary>
public sealed class SqliteSession : IDbSession, IDisposable
{
    private readonly SqliteConnection _connection;
    private int _depth;
    private bool _disposed;

    private SqliteSession(SqliteConnection connection) => _connection = connection;

    public string DatabasePath => _connection.DataSource ?? string.Empty;

    public static SqliteSession Open(string databasePath)
    {
        var full = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = full,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        var session = new SqliteSession(connection);
        session.Execute("PRAGMA journal_mode=WAL;");
        session.Execute("PRAGMA synchronous=NORMAL;");
        session.Execute("PRAGMA temp_store=MEMORY;");
        session.Execute("PRAGMA cache_size=-8000;");
        session.Execute("PRAGMA mmap_size=268435456;");
        session.Execute("PRAGMA foreign_keys=ON;");
        session.Execute("PRAGMA busy_timeout=5000;");
        return session;
    }

    public int Execute(string sql, DbArgs? args = null)
    {
        try
        {
            using var command = CreateCommand(sql, args);
            return command.ExecuteNonQuery();
        }
        catch (SqliteException error)
        {
            throw Translate(error, "execute");
        }
    }

    public long ScalarLong(string sql, DbArgs? args = null)
    {
        try
        {
            using var command = CreateCommand(sql, args);
            var value = command.ExecuteScalar();
            return value is null or DBNull ? 0L : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (SqliteException error)
        {
            throw Translate(error, "scalar");
        }
    }

    public double ScalarDouble(string sql, DbArgs? args = null)
    {
        try
        {
            using var command = CreateCommand(sql, args);
            var value = command.ExecuteScalar();
            return value is null or DBNull ? 0d : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (SqliteException error)
        {
            throw Translate(error, "scalar");
        }
    }

    public string? ScalarString(string sql, DbArgs? args = null)
    {
        try
        {
            using var command = CreateCommand(sql, args);
            var value = command.ExecuteScalar();
            return value is null or DBNull ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (SqliteException error)
        {
            throw Translate(error, "scalar");
        }
    }

    public List<T> Query<T>(string sql, Func<DbRecord, T> map, DbArgs? args = null)
    {
        try
        {
            using var command = CreateCommand(sql, args);
            using var reader = command.ExecuteReader();
            var rows = new List<T>();
            while (reader.Read())
            {
                rows.Add(map(new DbRecord(reader)));
            }

            return rows;
        }
        catch (SqliteException error)
        {
            throw Translate(error, "query");
        }
    }

    public T? QueryOne<T>(string sql, Func<DbRecord, T> map, DbArgs? args = null)
        where T : class
    {
        var rows = Query(sql, map, args);
        return rows.Count == 0 ? null : rows[0];
    }

    public void Transaction(Action body)
    {
        var savepoint = _depth == 0 ? null : $"sp_{_depth}";
        Execute(savepoint is null ? "BEGIN IMMEDIATE;" : $"SAVEPOINT {savepoint};");
        _depth++;
        try
        {
            body();
            _depth--;
            Execute(savepoint is null ? "COMMIT;" : $"RELEASE {savepoint};");
        }
        catch
        {
            _depth--;
            try
            {
                Execute(savepoint is null ? "ROLLBACK;" : $"ROLLBACK TO {savepoint}; RELEASE {savepoint};");
            }
            catch (Exception rollbackError)
            {
                throw new SimException(
                    "db.rollback_failed",
                    "Transaction failed and the rollback also failed; the save may need a manual check.",
                    rollbackError);
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.Dispose();
    }

    /// <summary>
    /// Turns a driver error into a contract error so callers (and the desktop host) can show a
    /// message instead of crashing on a raw SqliteException.
    /// </summary>
    private static SimException Translate(SqliteException error, string operation) =>
        new("db.sqlite", $"{operation} failed: {error.Message}", error);

    private SqliteCommand CreateCommand(string sql, DbArgs? args)
    {
        if (_disposed)
        {
            throw new SimException("db.disposed", "The database session has already been disposed.");
        }

        var command = _connection.CreateCommand();
        command.CommandText = sql;
        if (args is not null)
        {
            foreach (var pair in args.Values)
            {
                command.Parameters.AddWithValue(pair.Key, pair.Value ?? DBNull.Value);
            }
        }

        return command;
    }
}
