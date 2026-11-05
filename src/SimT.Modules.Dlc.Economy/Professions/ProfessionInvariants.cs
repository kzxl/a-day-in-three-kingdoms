using SimT.Contracts.Data;
using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Economy;

internal static class ProfessionInvariants
{
    /// <summary>Tier must always be the value the practice count implies, capped at the ceiling.</summary>
    public sealed class TierMatchesPractice : IInvariant
    {
        public string Id => "professions.skill.tier_matches_practice";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.Query(
                """
                SELECT trade_id, practice_count, tier FROM pf_skill
                WHERE tier <> MIN(5, practice_count / 4)
                """,
                row => $"trade {row.GetInt64(0)}: practice {row.GetInt32(1)} tier {row.GetInt32(2)}");

            return bad.Count == 0 ? null : $"inconsistent skill rows: {string.Join("; ", bad)}";
        }
    }

    public sealed class PracticeNonNegative : IInvariant
    {
        public string Id => "professions.skill.practice_non_negative";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM pf_skill WHERE practice_count < 0");
            return bad == 0 ? null : $"{bad} skill row(s) have negative practice";
        }
    }

    public sealed class OutputRegistered : IInvariant
    {
        public string Id => "professions.trade.output_positive";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM pf_trade WHERE output_item_id <= 0 OR base_quantity <= 0 OR days_per_batch <= 0");

            return bad == 0 ? null : $"{bad} trade definition(s) are not usable";
        }
    }

    public sealed class RecipeValid : IInvariant
    {
        public string Id => "professions.recipe.valid";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM pf_recipe WHERE material_quantity <= 0 OR output_quantity <= 0 OR craft_days <= 0 OR craft_fee < 0");

            return bad == 0 ? null : $"{bad} crafting recipe(s) have invalid quantities or costs";
        }
    }

    public sealed class EquipmentDurabilityValid : IInvariant
    {
        public string Id => "professions.equipment.durability_valid";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong(
                "SELECT COUNT(*) FROM pf_equipment WHERE durability < 0 OR durability > max_durability OR max_durability <= 0");

            return bad == 0 ? null : $"{bad} equipment item(s) have invalid durability values";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new TierMatchesPractice();
        yield return new PracticeNonNegative();
        yield return new OutputRegistered();
        yield return new RecipeValid();
        yield return new EquipmentDurabilityValid();
    }
}
