using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class SocialService
{
    internal int GetActiveDebateRecruitDiscountPercent(long npcId)
    {
        var day = _clock.Today.DayIndex;
        var mod = _db.ScalarLong(
            """
            SELECT COALESCE(modifier_percent, 0)
            FROM dg_debate_effect
            WHERE topic = 'recruit' AND npc_id = @id AND start_day <= @day AND end_day >= @day
            ORDER BY id DESC LIMIT 1
            """,
            new DbArgs().Add("@id", npcId).Add("@day", day));
        return mod < 0 ? (int)Math.Abs(mod) : 0;
    }

    private void ApplyDebateRecruitModifier(long npcId, ref int reqAffinity, ref int signingFee)
    {
        var discountPct = GetActiveDebateRecruitDiscountPercent(npcId);
        if (discountPct <= 0)
            return;

        reqAffinity = -100;
        if (signingFee > 0)
            signingFee = Math.Max(0, signingFee * (100 - discountPct) / 100);
    }

    internal void AdjustAffinityFromDebate(long npcId, int delta, string summaryVi)
    {
        var currentRel = GetRelationship(npcId);
        var newAffinity = Math.Clamp(currentRel.Affinity + delta, -100, 100);
        var newImp = CalculateImpression(newAffinity);
        var formattedDate = GameDateCodec.Format(_clock.Today);

        _db.Transaction(() =>
        {
            _db.Execute(
                "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                "VALUES (@npcId, @aff, @trust, @imp, @gifts, @date) " +
                "ON CONFLICT(npc_id) DO UPDATE SET " +
                "affinity = @aff, impression = @imp, last_interaction_date = @date",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@aff", newAffinity)
                    .Add("@trust", currentRel.Trust)
                    .Add("@imp", newImp)
                    .Add("@gifts", currentRel.GiftCount)
                    .Add("@date", formattedDate));

            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'debate', @sent, @delta, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@sent", delta >= 0 ? "positive" : "negative")
                    .Add("@delta", delta)
                    .Add("@summary", summaryVi));
        });

        RecordMetNpc(npcId, _clock.Today);
    }

    internal bool CompleteDebateRecruit(long npcId, int discountPercent, out int feePaid, out string roleTitleVi)
    {
        feePaid = 0;
        roleTitleVi = "Gia thần";
        var npc = _registry.Find(npcId);
        if (npc is null || IsWarlord(npc))
            return false;

        if (GetRetainer(npcId) is { } existing)
        {
            roleTitleVi = RoleTitleVi(existing.RoleAssigned);
            return true;
        }

        var rank = EvaluateNpcRank(npc);
        var (_, _, baseFee, minStipend) = GetRankRequirements(rank);
        if (HasCompletedWish(npcId))
            baseFee = 0;

        var discountedFee = Math.Max(0, baseFee * (100 - Math.Clamp(discountPercent, 10, 90)) / 100);
        var chargeAmount = Math.Min((int)Math.Max(0L, _wallet.Liquid), discountedFee);
        var assignedRole = AutoAssignRole(npc.Attributes);
        roleTitleVi = RoleTitleVi(assignedRole);
        var rel = GetRelationship(npcId);
        var initialLoyalty = Math.Clamp(65 + (rel.Affinity / 5) + (_attributes.Social.Virtue / 10), 60, 100);
        var formattedDate = GameDateCodec.Format(_clock.Today);
        var paid = chargeAmount;

        _db.Transaction(() =>
        {
            if (paid > 0)
            {
                _wallet.Charge(
                    paid,
                    "social.recruit_debate",
                    $"Sính lễ chiêu mộ gia thần sau biện luận: {npc.NameVi}",
                    $"recruit_debate:{npcId}:{formattedDate}");
            }

            _db.Execute(
                "INSERT INTO kz_retainer (npc_id, role_assigned, monthly_stipend, loyalty, recruited_date) " +
                "VALUES (@npcId, @role, @stipend, @loyalty, @date)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@role", assignedRole)
                    .Add("@stipend", minStipend)
                    .Add("@loyalty", initialLoyalty)
                    .Add("@date", formattedDate));

            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'recruit', 'positive', 10, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@summary", $"{npc.NameVi} cảm phục tài biện luận, gia nhập môn hạ với sính lễ ưu đãi {paid} Quan."));
        });

        feePaid = paid;
        RecordMetNpc(npcId, _clock.Today, npc.CurrentCitadelId(_capabilities));
        return true;
    }
}
