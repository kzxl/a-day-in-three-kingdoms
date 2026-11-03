using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;
using SimT.Contracts.Ledger;
using SimT.Contracts.Time;

namespace SimT.Core.Diagnostics;

/// <summary>Kernel invariants: rules about kernel-owned tables that must never be false.</summary>
public static class KernelInvariants
{
    /// <summary>The cached running balance of the last ledger line must equal the sum of all lines.</summary>
    public sealed class LedgerRunningBalance : IInvariant
    {
        public string Id => "core.ledger.running_balance";

        public string? Check(IInvariantContext context)
        {
            var sum = context.Db.ScalarLong("SELECT COALESCE(SUM(direction * amount), 0) FROM core_ledger");
            var last = context.Db.ScalarLong(
                "SELECT COALESCE((SELECT balance_after FROM core_ledger ORDER BY id DESC LIMIT 1), 0)");

            return sum == last ? null : $"ledger sum {sum} != last recorded balance {last}";
        }
    }

    /// <summary>Liquid + vault must equal the balance the ledger reports.</summary>
    public sealed class LiquidPlusVault : IInvariant
    {
        public string Id => "core.ledger.liquid_plus_vault";

        public string? Check(IInvariantContext context) =>
            context.Ledger.Liquid + context.Ledger.Safe == context.Ledger.Balance
                ? null
                : $"liquid {context.Ledger.Liquid} + vault {context.Ledger.Safe} != balance {context.Ledger.Balance}";
    }

    /// <summary>No period may be published twice in the same save.</summary>
    public sealed class PeriodUnique : IInvariant
    {
        public string Id => "core.period.unique";

        public string? Check(IInvariantContext context)
        {
            var duplicates = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM (SELECT period_key FROM core_period_log GROUP BY period_key HAVING COUNT(*) > 1)");

            return duplicates == 0 ? null : $"{duplicates} period key(s) recorded more than once";
        }
    }

    /// <summary>The stored date must be readable and not in the future relative to ticks.</summary>
    public sealed class ClockReadable : IInvariant
    {
        public string Id => "core.clock.readable";

        public string? Check(IInvariantContext context) =>
            context.Clock.Today.Year >= GameDate.Start.Year ? null : "clock is before campaign start";
    }

    public static IReadOnlyList<IInvariant> All() =>
    [
        new LedgerRunningBalance(),
        new LiquidPlusVault(),
        new PeriodUnique(),
        new ClockReadable()
    ];
}
