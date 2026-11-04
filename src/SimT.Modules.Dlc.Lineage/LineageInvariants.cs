using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Lineage;

internal static class LineageInvariants
{
    /// <summary>One household: a second spouse row means two writers disagreed about the family.</summary>
    public sealed class SingleSpouse : IInvariant
    {
        public string Id => "lineage.spouse.single";

        public string? Check(IInvariantContext context)
        {
            var count = context.Db.ScalarLong("SELECT COUNT(*) FROM ln_spouse");
            return count <= 1 ? null : $"{count} spouse rows exist";
        }
    }

    public sealed class SingleHeir : IInvariant
    {
        public string Id => "lineage.heir.single";

        public string? Check(IInvariantContext context)
        {
            var count = context.Db.ScalarLong("SELECT COUNT(*) FROM ln_child WHERE is_heir = 1");
            return count <= 1 ? null : $"{count} children are marked heir";
        }
    }

    /// <summary>No child may be born before the marriage that produced them.</summary>
    public sealed class ChildrenBornAfterMarriage : IInvariant
    {
        public string Id => "lineage.child.after_marriage";

        public string? Check(IInvariantContext context)
        {
            var spouses = context.Db.ScalarLong("SELECT COUNT(*) FROM ln_spouse");
            if (spouses == 0)
            {
                var early = context.Db.ScalarLong("SELECT COUNT(*) FROM ln_child");
                return early == 0 ? null : $"{early} child(ren) exist without a marriage";
            }

            var married = context.Db.ScalarLong("SELECT MIN(married_day_index) FROM ln_spouse");
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM ln_child WHERE born_day_index < @married",
                DbArgs.One("@married", married));

            return bad == 0 ? null : $"{bad} child(ren) born before the marriage";
        }
    }

    public sealed class ShrineLevelBounded : IInvariant
    {
        public string Id => "lineage.shrine.level_bounded";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM ln_shrine WHERE level < 1 OR level > 5 OR clan_prestige < 0");
            return bad == 0 ? null : $"{bad} shrine row(s) have invalid level or negative prestige";
        }
    }

    public sealed class TutoringBonusBounded : IInvariant
    {
        public string Id => "lineage.tutoring.bonus_bounded";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM ln_tutoring WHERE stat_bonus < 0 OR progress < 0");
            return bad == 0 ? null : $"{bad} tutoring row(s) have negative bonus or progress";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new SingleSpouse();
        yield return new SingleHeir();
        yield return new ChildrenBornAfterMarriage();
        yield return new ShrineLevelBounded();
        yield return new TutoringBonusBounded();
    }
}
