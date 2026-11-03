using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Caravan;

internal static class CaravanInvariants
{
    public sealed class DatesOrdered : IInvariant
    {
        public string Id => "caravan.run.dates_ordered";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM cv_run WHERE arrive_day < depart_day");
            return bad == 0 ? null : $"{bad} caravan run(s) arrive before departure";
        }
    }

    public sealed class CargoHasRun : IInvariant
    {
        public string Id => "caravan.cargo.has_run";

        public string? Check(IInvariantContext context)
        {
            var orphans = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM cv_cargo c WHERE NOT EXISTS (SELECT 1 FROM cv_run r WHERE r.id = c.run_id)");

            return orphans == 0 ? null : $"{orphans} cargo row(s) reference a missing run";
        }
    }

    public sealed class StatusIsKnown : IInvariant
    {
        public string Id => "caravan.run.status_known";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM cv_run WHERE status NOT IN ('RUNNING', 'ARRIVED')");

            return bad == 0 ? null : $"{bad} caravan run(s) have an unknown status";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new DatesOrdered();
        yield return new CargoHasRun();
        yield return new StatusIsKnown();
    }
}
