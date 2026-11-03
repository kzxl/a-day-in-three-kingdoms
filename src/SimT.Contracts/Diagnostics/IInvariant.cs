using SimT.Contracts.Data;
using SimT.Contracts.Ledger;
using SimT.Contracts.Time;

namespace SimT.Contracts.Diagnostics;

/// <summary>
/// A cross-table consistency rule contributed by the kernel or a module. Invariants are read
/// only: a failure means stored state contradicts itself, which is always a bug in some writer.
/// </summary>
public interface IInvariant
{
    string Id { get; }

    /// <summary>Returns null when the rule holds, otherwise a human-readable explanation.</summary>
    string? Check(IInvariantContext context);
}

public interface IInvariantContext
{
    IDbSession Db { get; }

    IGameClock Clock { get; }

    ILedger Ledger { get; }
}

public sealed record InvariantFailure(string InvariantId, string Message);
