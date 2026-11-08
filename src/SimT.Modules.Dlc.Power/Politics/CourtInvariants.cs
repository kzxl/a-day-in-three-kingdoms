using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Power;

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
                "SELECT COUNT(*) FROM po_state WHERE key IN ('reputation', 'office_id', 'allegiance_faction_id')");

            return count == 3 ? null : $"expected 3 state rows, found {count}";
        }
    }

    public sealed class FactionsValid : IInvariant
    {
        public string Id => "politics.factions.valid";

        public string? Check(IInvariantContext context)
        {
            var count = context.Db.ScalarLong("SELECT COUNT(*) FROM po_faction");
            if (count < 13)
            {
                return $"expected at least 13 historical factions, found {count}";
            }

            var invalid = context.Db.ScalarLong("SELECT COUNT(*) FROM po_faction WHERE troop_count < 0 OR treasury_copper < 0");
            return invalid == 0 ? null : $"found {invalid} factions with negative troops or treasury";
        }
    }

    public sealed class CityUnrestValid : IInvariant
    {
        public string Id => "politics.unrest.within_range";

        public string? Check(IInvariantContext context)
        {
            var invalid = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM po_city_governance WHERE unrest_level < 0 OR unrest_level > 100");
            return invalid == 0 ? null : $"found {invalid} citadels with unrest outside [0, 100]";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new OfficeExists();
        yield return new ReputationNonNegative();
        yield return new StateKeysPresent();
        yield return new FactionsValid();
        yield return new CityUnrestValid();
    }
}
