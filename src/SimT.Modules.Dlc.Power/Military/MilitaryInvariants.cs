using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Power;

internal static class MilitaryInvariants
{
    public sealed class DangerLevelBounded : IInvariant
    {
        public string Id => "military.stronghold.danger_level_bounded";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM ml_stronghold WHERE danger_level < 1 OR danger_level > 10");
            return bad == 0 ? null : $"{bad} stronghold(s) have danger level outside [1, 10]";
        }
    }

    public sealed class LootGoldNonNegative : IInvariant
    {
        public string Id => "military.stronghold.loot_gold_non_negative";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM ml_stronghold WHERE loot_gold < 0");
            return bad == 0 ? null : $"{bad} stronghold(s) have negative loot gold";
        }
    }

    public sealed class RequisitionQuantityPositive : IInvariant
    {
        public string Id => "military.requisition.quantity_positive";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM ml_supply_requisition WHERE quantity_needed <= 0");
            return bad == 0 ? null : $"{bad} supply requisition(s) have non-positive quantity";
        }
    }

    public sealed class DuelWinnerValid : IInvariant
    {
        public string Id => "military.duel.winner_valid";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM ml_duel_log WHERE winner NOT IN ('player', 'npc', 'draw')");
            return bad == 0 ? null : $"{bad} duel log(s) have invalid winner flag";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new DangerLevelBounded();
        yield return new LootGoldNonNegative();
        yield return new RequisitionQuantityPositive();
        yield return new DuelWinnerValid();
    }
}
