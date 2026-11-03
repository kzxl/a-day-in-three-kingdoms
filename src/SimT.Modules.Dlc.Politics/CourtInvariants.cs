using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Politics;

internal static class CourtInvariants
{
    public sealed class OfficeExists : IInvariant
    {
        public string Id => "politics.office.exists";

        public string? Check(IInvariantContext context)
        {
            var officeId = context.Db.ScalarLong("SELECT COALESCE((SELECT value FROM po_state WHERE key='office_id'), 0)");
            if (officeId == 0)
            {
                return null;
            }

            var exists = context.Db.ScalarLong("SELECT COUNT(*) FROM po_office WHERE id=@id", DbArgs.One("@id", officeId));
            return exists == 1 ? null : $"office_id {officeId} does not exist";
        }
    }

    public sealed class ReputationNonNegative : IInvariant
    {
        public string Id => "politics.reputation.non_negative";

        public string? Check(IInvariantContext context)
        {
            var reputation = context.Db.ScalarLong("SELECT COALESCE((SELECT value FROM po_state WHERE key='reputation'), 0)");
            return reputation >= 0 ? null : $"reputation is {reputation}";
        }
    }

    public sealed class StateKeysPresent : IInvariant
    {
        public string Id => "politics.state.keys";

        public string? Check(IInvariantContext context)
        {
            var count = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM po_state WHERE key IN ('reputation', 'office_id')");

            return count == 2 ? null : $"expected 2 state rows, found {count}";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new OfficeExists();
        yield return new ReputationNonNegative();
        yield return new StateKeysPresent();
    }
}
