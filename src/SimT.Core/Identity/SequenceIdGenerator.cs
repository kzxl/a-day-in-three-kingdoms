using SimT.Contracts.Data;
using SimT.Contracts.Identity;

namespace SimT.Core.Identity;

/// <summary>
/// K4 — ids come from a per-scope sequence inside the save, never from a clock or a GUID,
/// so replaying the same actions reproduces the same rows.
/// </summary>
public sealed class SequenceIdGenerator : IIdGenerator
{
    private readonly IDbSession _db;

    public SequenceIdGenerator(IDbSession db) => _db = db;

    public long Next(string scope)
    {
        long value = 0;
        _db.Transaction(() =>
        {
            var updated = _db.Execute(
                "UPDATE core_sequence SET value = value + 1 WHERE scope=@scope",
                DbArgs.One("@scope", scope));

            if (updated == 0)
            {
                _db.Execute(
                    "INSERT INTO core_sequence(scope, value) VALUES(@scope, 1)",
                    DbArgs.One("@scope", scope));
                value = 1;
                return;
            }

            value = _db.ScalarLong("SELECT value FROM core_sequence WHERE scope=@scope", DbArgs.One("@scope", scope));
        });

        return value;
    }

    public string NextId(string prefix, string scope) => $"{prefix}{Next(scope):D6}";
}
