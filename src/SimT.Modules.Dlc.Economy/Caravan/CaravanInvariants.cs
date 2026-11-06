using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Economy;

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

    public sealed class LostWithinQuantity : IInvariant
    {
        public string Id => "caravan.cargo.lost_within_quantity";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM cv_cargo WHERE lost < 0 OR lost >= quantity");
            return bad == 0 ? null : $"{bad} cargo line(s) lost an impossible amount";
        }
    }

    public sealed class ContractsValid : IInvariant
    {
        public string Id => "caravan.contract.valid";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM cv_contract WHERE from_city = to_city OR quantity <= 0 OR reward_copper <= 0");

            return bad == 0 ? null : $"{bad} freight contract(s) have invalid route or values";
        }
    }

    public sealed class ContractStatusValid : IInvariant
    {
        public string Id => "caravan.contract.status_known";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM cv_contract WHERE status NOT IN ('AVAILABLE', 'IN_TRANSIT', 'DELIVERED', 'EXPIRED')");

            return bad == 0 ? null : $"{bad} freight contract(s) have an unknown status";
        }
    }

    public sealed class ContractDatesOrdered : IInvariant
    {
        public string Id => "caravan.contract.dates_ordered";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM cv_contract WHERE completed_day IS NOT NULL AND accepted_day IS NOT NULL AND completed_day < accepted_day");

            return bad == 0 ? null : $"{bad} freight contract(s) completed before acceptance";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new DatesOrdered();
        yield return new CargoHasRun();
        yield return new StatusIsKnown();
        yield return new LostWithinQuantity();
        yield return new ContractsValid();
        yield return new ContractStatusValid();
        yield return new ContractDatesOrdered();
    }
}
