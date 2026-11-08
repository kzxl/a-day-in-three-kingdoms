using SimT.Contracts.Diagnostics;

namespace SimT.Modules.Dlc.Character;

internal static class SocialInvariants
{
    public sealed class AffinityBounded : IInvariant
    {
        public string Id => "social.relationship.affinity_bounded";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM kz_relationship WHERE affinity < -100 OR affinity > 100");
            return bad == 0 ? null : $"{bad} relationship row(s) have affinity outside [-100, 100]";
        }
    }

    public sealed class TrustBounded : IInvariant
    {
        public string Id => "social.relationship.trust_bounded";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM kz_relationship WHERE trust < 0 OR trust > 100");
            return bad == 0 ? null : $"{bad} relationship row(s) have trust outside [0, 100]";
        }
    }

    public sealed class StipendNonNegative : IInvariant
    {
        public string Id => "social.retainer.stipend_non_negative";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM kz_retainer WHERE monthly_stipend < 0");
            return bad == 0 ? null : $"{bad} retainer row(s) have negative monthly stipend";
        }
    }

    public sealed class LoyaltyBounded : IInvariant
    {
        public string Id => "social.retainer.loyalty_bounded";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM kz_retainer WHERE loyalty < 0 OR loyalty > 100");
            return bad == 0 ? null : $"{bad} retainer row(s) have loyalty outside [0, 100]";
        }
    }

    public sealed class MemorySummariesPresent : IInvariant
    {
        public string Id => "social.memory.summaries_present";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM kz_memory WHERE LENGTH(TRIM(summary_vi)) = 0");
            return bad == 0 ? null : $"{bad} memory record(s) lack a summary";
        }
    }

    public sealed class SwornBrotherMaxTwo : IInvariant
    {
        public string Id => "social.sworn_brother.max_two";

        public string? Check(IInvariantContext context)
        {
            var count = context.Db.ScalarLong("SELECT COUNT(*) FROM kz_sworn_brother");
            return count <= 2 ? null : $"Exceeded max 2 sworn brothers: found {count}";
        }
    }

    public sealed class LetterGiftsNonNegative : IInvariant
    {
        public string Id => "social.letter.gifts_non_negative";

        public string? Check(IInvariantContext context)
        {
            var bad = context.Db.ScalarLong("SELECT COUNT(*) FROM kz_letter WHERE gift_copper < 0 OR gift_item_quantity < 0");
            return bad == 0 ? null : $"{bad} letter record(s) have negative copper or item quantity";
        }
    }

    public sealed class RosterMilestonesValid : IInvariant
    {
        public string Id => "social.roster.milestones_valid";

        public string? Check(IInvariantContext context)
        {
            var badMet = context.Db.ScalarLong("SELECT COUNT(*) FROM kz_met_npc WHERE npc_id <= 0 OR met_citadel_id <= 0 OR LENGTH(TRIM(first_met_date)) = 0");
            if (badMet > 0) return $"{badMet} met NPC record(s) have invalid npc_id, citadel_id, or date";
            var badMilestones = context.Db.ScalarLong("SELECT COUNT(*) FROM kz_roster_milestone WHERE fame_awarded <= 0 OR LENGTH(TRIM(claimed_date)) = 0");
            return badMilestones == 0 ? null : $"{badMilestones} roster milestone record(s) have invalid fame_awarded or claimed_date";
        }
    }

    public static IEnumerable<IInvariant> All()
    {
        yield return new AffinityBounded();
        yield return new TrustBounded();
        yield return new StipendNonNegative();
        yield return new LoyaltyBounded();
        yield return new MemorySummariesPresent();
        yield return new SwornBrotherMaxTwo();
        yield return new LetterGiftsNonNegative();
        yield return new RosterMilestonesValid();
    }
}
