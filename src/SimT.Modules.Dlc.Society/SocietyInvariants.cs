using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Society;

internal static class SocietyInvariants
{
    public sealed class WealthNonNegative : IInvariant
    {
        public string Id => "society.npc_activity.wealth_non_negative";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM so_npc_activity WHERE current_wealth < 0");
            return bad == 0 ? null : $"{bad} NPC(s) have negative wealth";
        }
    }

    public sealed class ValidFocusPaths : IInvariant
    {
        public string Id => "society.npc_activity.focus_path_valid";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM so_npc_activity WHERE focus_path NOT IN ('martial', 'merchant', 'strategist', 'governor', 'wanderer')");
            return bad == 0 ? null : $"{bad} NPC(s) have an invalid career focus path";
        }
    }

    public sealed class StatDeltasNonNegative : IInvariant
    {
        public string Id => "society.npc_stat_delta.non_negative";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM so_npc_stat_delta WHERE war_delta < 0 OR ldr_delta < 0 OR int_delta < 0 OR pol_delta < 0 OR dip_delta < 0 OR com_delta < 0 OR log_delta < 0 OR cra_delta < 0");
            return bad == 0 ? null : $"{bad} NPC stat delta row(s) contain negative values";
        }
    }

    public sealed class LogEventsHaveHeadlines : IInvariant
    {
        public string Id => "society.journey_log.headlines_present";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM so_journey_log WHERE LENGTH(TRIM(headline_vi)) = 0");
            return bad == 0 ? null : $"{bad} journey log record(s) lack a headline";
        }
    }

    public sealed class MerchantRivalCapitalNonNegative : IInvariant
    {
        public string Id => "society.merchant_rival.capital_non_negative";

        public string? Check(IInvariantContext context)
        {
            var hasTable = context.Db.ScalarLong("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='so_merchant_rival'") > 0;
            if (!hasTable) return null;

            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM so_merchant_rival WHERE capital_quan < 0 OR fleet_carts < 1");
            return bad == 0 ? null : $"{bad} merchant rival(s) have negative capital or invalid fleet size";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new WealthNonNegative();
        yield return new ValidFocusPaths();
        yield return new StatDeltasNonNegative();
        yield return new LogEventsHaveHeadlines();
        yield return new MerchantRivalCapitalNonNegative();
    }
}
