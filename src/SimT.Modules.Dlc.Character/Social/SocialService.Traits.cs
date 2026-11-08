using SimT.Contracts.Data;
using SimT.Contracts.Playing;
using SimT.Contracts.Time;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class SocialService : INpcTraits
{
    private readonly Dictionary<long, IReadOnlyList<NpcTraitDef>> _traitCache = new();
    private long _campaignSeed = 1840502L;

    public void SetCampaignSeed(long seed)
    {
        if (_campaignSeed == seed)
            return;
        _campaignSeed = seed;
        _traitCache.Clear();
    }

    public IReadOnlyList<NpcTraitDef> GetAllTraitDefs() => NpcTraitCatalog.AllTraits;

    public IReadOnlyList<NpcTraitDef> GetTraits(long npcId)
    {
        if (_traitCache.TryGetValue(npcId, out var cached))
            return cached;

        var npc = _registry.Find(npcId);
        var psyche = GetPsycheProfile(npcId);
        var resolved = NpcTraitCatalog.ResolveTraits(npc, psyche, _campaignSeed, npcId);
        _traitCache[npcId] = resolved;
        return resolved;
    }

    public bool HasTrait(long npcId, string traitCode)
    {
        var traits = GetTraits(npcId);
        for (var i = 0; i < traits.Count; i++)
        {
            if (string.Equals(traits[i].Code, traitCode, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public IReadOnlyList<NpcTraitView> GetVisibleTraits(long npcId)
    {
        var rel = GetRelationship(npcId);
        return BuildVisibleTraits(npcId, rel.Affinity);
    }

    public IReadOnlyList<NpcTraitView> BuildVisibleTraits(long npcId, int affinity)
    {
        var traits = GetTraits(npcId);
        var revealAll = affinity >= NpcTraitRules.FullRevealAffinityThreshold;
        var views = new List<NpcTraitView>(traits.Count);

        for (var i = 0; i < traits.Count; i++)
        {
            var t = traits[i];
            if (i == 0 || revealAll)
            {
                views.Add(new NpcTraitView(t.Code, t.NameVi, t.DescriptionVi, Hidden: false));
            }
            else
            {
                views.Add(new NpcTraitView(
                    NpcTraitRules.MaskedTraitCode,
                    NpcTraitRules.MaskedTraitLabelVi,
                    NpcTraitRules.MaskedTraitDescriptionVi,
                    Hidden: true));
            }
        }

        return views;
    }

    /// <summary>
    /// Adjusts gift multiplier additively/subtractively based on NPC character traits.
    /// Does not replace existing archetype gift logic; layers on top of it.
    /// </summary>
    private void ApplyTraitGiftAdjustments(
        long npcId,
        ItemDef item,
        ref double preferenceMultiplier,
        ref bool isFavorite,
        out int flatBonusDelta,
        out string traitFlavorVi)
    {
        flatBonusDelta = 0;
        traitFlavorVi = string.Empty;
        var traits = GetTraits(npcId);
        if (traits.Count == 0)
            return;

        var code = item.Code.ToLowerInvariant();
        var cat = (item.Category ?? string.Empty).ToLowerInvariant();
        var nameLower = item.NameVi.ToLowerInvariant();

        var isWine = code == "ruou" || nameLower.Contains("rượu", StringComparison.Ordinal);
        var isScholarItem = code is "tra" or "giay" or "sach" ||
                            cat == "book" ||
                            nameLower.Contains("trà", StringComparison.Ordinal) ||
                            nameLower.Contains("sách", StringComparison.Ordinal) ||
                            nameLower.Contains("kinh", StringComparison.Ordinal);
        var isMartialItem = code is "sat" or "dong" or "cung_ten" or "binh_khi" ||
                            cat == "weapon" ||
                            nameLower.Contains("đao", StringComparison.Ordinal) ||
                            nameLower.Contains("kiếm", StringComparison.Ordinal) ||
                            nameLower.Contains("thương", StringComparison.Ordinal) ||
                            nameLower.Contains("cung", StringComparison.Ordinal);

        foreach (var t in traits)
        {
            switch (t.Code)
            {
                case NpcTraitCodes.MeRuou when isWine:
                    preferenceMultiplier += NpcTraitRules.MeRuouWineBonusMultiplier;
                    flatBonusDelta += 3;
                    isFavorite = true;
                    traitFlavorVi = " [Mê rượu: rất khoái mỹ tửu]";
                    break;

                case NpcTraitCodes.HieuHoc when isScholarItem:
                    preferenceMultiplier += NpcTraitRules.HieuHocScholarBonusMultiplier;
                    flatBonusDelta += 3;
                    isFavorite = true;
                    traitFlavorVi = " [Hiếu học: quý trọng trà thơm sách quý]";
                    break;

                case NpcTraitCodes.MeVo when isMartialItem:
                    preferenceMultiplier += NpcTraitRules.MeVoMartialBonusMultiplier;
                    flatBonusDelta += 3;
                    isFavorite = true;
                    traitFlavorVi = " [Mê võ: ưng ý chất liệu binh gia]";
                    break;

                case NpcTraitCodes.ThanhLiem when item.BasePrice >= NpcTraitRules.ThanhLiemBriberyPriceThreshold && !isScholarItem:
                    preferenceMultiplier = Math.Max(0.35, preferenceMultiplier + NpcTraitRules.ThanhLiemExpensiveGiftPenaltyMultiplier);
                    flatBonusDelta -= 2;
                    isFavorite = false;
                    traitFlavorVi = " [Thanh liêm: e ngại lễ vật xa xỉ như hối lộ]";
                    break;
            }
        }
    }

    public int RecordDuelVictoryBonus(long npcId)
    {
        if (!HasTrait(npcId, NpcTraitCodes.MeVo))
            return 0;

        var npc = _registry.Find(npcId);
        if (npc is null)
            return 0;

        var bonus = NpcTraitRules.MeVoDuelVictoryAffinityBonus;
        var currentRel = GetRelationship(npcId);
        var newAffinity = Math.Clamp(currentRel.Affinity + bonus, -100, 100);
        var newTrust = Math.Clamp(currentRel.Trust + 3, 0, 100);
        var newImpression = CalculateImpression(newAffinity);
        var formattedDate = GameDateCodec.Format(_clock.Today);

        _db.Transaction(() =>
        {
            _db.Execute(
                "INSERT INTO kz_relationship (npc_id, affinity, trust, impression, gift_count, last_interaction_date) " +
                "VALUES (@npcId, @aff, @trust, @imp, @gifts, @date) " +
                "ON CONFLICT(npc_id) DO UPDATE SET " +
                "affinity = @aff, trust = @trust, impression = @imp, last_interaction_date = @date",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@aff", newAffinity)
                    .Add("@trust", newTrust)
                    .Add("@imp", newImpression)
                    .Add("@gifts", currentRel.GiftCount)
                    .Add("@date", formattedDate));

            _db.Execute(
                "INSERT INTO kz_memory (npc_id, date, topic, sentiment, affinity_delta, summary_vi) " +
                "VALUES (@npcId, @date, 'duel_respect', 'positive', @delta, @summary)",
                new DbArgs()
                    .Add("@npcId", npcId)
                    .Add("@date", formattedDate)
                    .Add("@delta", bonus)
                    .Add("@summary", $"Thắng tỉ thí võ nghệ khiến người [Mê võ] như {npc.NameVi} tâm phục khẩu phục (+{bonus} Hảo cảm)."));
        });

        return bonus;
    }
}
