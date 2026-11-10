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

        RecordMetNpc(npcId, _clock.Today, npc.CurrentCitadelId(_capabilities));
        return bonus;
    }

    public GiftEvaluationView EvaluateGift(long npcId, long itemId)
    {
        var npc = _registry.Find(npcId);
        var item = _items.Find(itemId);
        if (npc is null || item is null)
        {
            return new GiftEvaluationView(itemId, item?.NameVi ?? "Vật phẩm", 0, item?.BasePrice ?? 0, 0, string.Empty, string.Empty, true);
        }

        var qty = _bag.Quantity(_player.CurrentCityId, itemId);
        var rank = EvaluateNpcRank(npc);
        var traits = GetTraits(npcId);
        var isMeRuouWine = (item.Code == "ruou" || item.NameVi.Contains("rượu", StringComparison.OrdinalIgnoreCase)) && traits.Any(t => t.Code == NpcTraitCodes.MeRuou);

        // Check high-rank rejection (exempt if trait synergy like MeRuou applies)
        if (!isMeRuouWine && rank == NpcRank.S && item.BasePrice < 35 && (item.Category == "grain" || item.Category == "material" || item.Category == "food"))
        {
            return new GiftEvaluationView(itemId, item.NameVi, qty, item.BasePrice, 0, string.Empty, "Nhân sĩ bậc S từ chối lễ mọn", true);
        }

        if (rank == NpcRank.A && item.BasePrice < 20 && (item.Category == "grain" || item.Category == "material"))
        {
            return new GiftEvaluationView(itemId, item.NameVi, qty, item.BasePrice, 0, string.Empty, "Nhân sĩ bậc A từ chối vật phẩm tầm thường", true);
        }

        var nAttr = npc.Attributes;
        var preferenceMultiplier = 1.0;
        var isFavorite = false;
        var synergyBadge = string.Empty;

        // 1. Archetype base preferences
        if ((nAttr.War >= 75 || npc.Role.Contains("Tướng") || npc.Role.Contains("Hổ")) &&
            (item.Code == "sat" || item.Code == "dong" || item.Code == "ruou"))
        {
            preferenceMultiplier = 1.5;
            isFavorite = true;
            synergyBadge = "⚔️ Hợp Tính Võ Tướng";
        }
        else if ((nAttr.Int >= 75 || nAttr.Pol >= 75 || npc.Role.Contains("Sĩ") || npc.Role.Contains("Thừa Tướng")) &&
                 (item.Code == "tra" || item.Code == "tram_huong" || item.Code == "sam" || item.Code == "thuoc"))
        {
            preferenceMultiplier = 1.5;
            isFavorite = true;
            synergyBadge = "📜 Hợp Tính Danh Nho";
        }
        else if ((nAttr.Com >= 70 || npc.Role == "merchant") &&
                 (item.Code == "lua" || item.Code == "gam" || item.Code == "tram_huong" || item.Code == "tra" || item.Code == "dong"))
        {
            preferenceMultiplier = 1.5;
            isFavorite = true;
            synergyBadge = "💰 Hợp Tính Thương Gia";
        }
        else if (item.Category == "luxury")
        {
            preferenceMultiplier = 1.3;
            synergyBadge = "💎 Quý Vật";
        }

        // 2. Trait preferences
        ApplyTraitGiftAdjustments(npcId, item, ref preferenceMultiplier, ref isFavorite, out var flatTraitBonus, out var _);

        var warning = string.Empty;
        if (traits.Any(t => t.Code == NpcTraitCodes.MeRuou) && (item.Code == "ruou" || item.NameVi.Contains("rượu", StringComparison.OrdinalIgnoreCase)))
        {
            synergyBadge = "🍶 Hợp Mê Rượu (+55%)";
        }
        else if (traits.Any(t => t.Code == NpcTraitCodes.HieuHoc) && (item.Code is "tra" or "giay" or "sach" || item.Category == "book" || item.NameVi.Contains("sách", StringComparison.OrdinalIgnoreCase) || item.NameVi.Contains("trà", StringComparison.OrdinalIgnoreCase)))
        {
            synergyBadge = "📜 Hợp Hiếu Học (+50%)";
        }
        else if (traits.Any(t => t.Code == NpcTraitCodes.MeVo) && (item.Code is "sat" or "dong" or "cung_ten" or "binh_khi" || item.Category == "weapon" || item.NameVi.Contains("đao", StringComparison.OrdinalIgnoreCase) || item.NameVi.Contains("kiếm", StringComparison.OrdinalIgnoreCase)))
        {
            synergyBadge = "⚔️ Hợp Mê Võ (+50%)";
        }
        else if (traits.Any(t => t.Code == NpcTraitCodes.ThanhLiem) && item.BasePrice >= NpcTraitRules.ThanhLiemBriberyPriceThreshold && item.Code != "tra" && item.Code != "giay" && item.Code != "sach" && item.Category != "book")
        {
            warning = "Coi là hối lộ (-45%)";
        }

        // 3. Projected gain
        var currentRel = GetRelationship(npcId);
        int gain;
        if (rank == NpcRank.C)
        {
            var baseFactor = Math.Clamp((int)(item.BasePrice / 15), 4, 15);
            gain = (int)Math.Round(baseFactor * preferenceMultiplier);
            if (currentRel.Affinity >= 80) gain = Math.Max(2, (int)(gain * 0.5));
            else if (currentRel.Affinity >= 50) gain = Math.Max(3, (int)(gain * 0.75));
        }
        else if (rank == NpcRank.B)
        {
            var baseFactor = Math.Clamp((int)(item.BasePrice / 20), 3, 12);
            gain = (int)Math.Round(baseFactor * preferenceMultiplier * 0.85);
            if (currentRel.Affinity >= 80) gain = Math.Max(1, (int)(gain * 0.4));
            else if (currentRel.Affinity >= 50) gain = Math.Max(2, (int)(gain * 0.65));
        }
        else if (rank == NpcRank.A)
        {
            var baseFactor = Math.Clamp((int)(item.BasePrice / 25), 2, 10);
            gain = (int)Math.Round(baseFactor * preferenceMultiplier * 0.65);
            if (currentRel.Affinity >= 80) gain = Math.Max(1, (int)(gain * 0.35));
            else if (currentRel.Affinity >= 50) gain = Math.Max(2, (int)(gain * 0.55));
        }
        else // Rank S
        {
            var baseFactor = Math.Clamp((int)(item.BasePrice / 30), 2, 8);
            gain = (int)Math.Round(baseFactor * preferenceMultiplier * 0.45);
            if (currentRel.Affinity >= 80) gain = Math.Max(1, (int)(gain * 0.3));
            else if (currentRel.Affinity >= 50) gain = Math.Max(1, (int)(gain * 0.5));
        }

        gain = Math.Max(1, gain + flatTraitBonus);

        return new GiftEvaluationView(itemId, item.NameVi, qty, item.BasePrice, gain, synergyBadge, warning, false);
    }

    public IReadOnlyList<GiftEvaluationView> GetBestGiftsForNpc(long npcId, long cityId)
    {
        var targetCity = cityId > 0 ? cityId : _player.CurrentCityId;
        var bagRows = _bag.Rows(targetCity).Where(r => r.Quantity > 0).ToList();
        if (bagRows.Count == 0) return Array.Empty<GiftEvaluationView>();

        var list = new List<GiftEvaluationView>();
        foreach (var r in bagRows)
        {
            list.Add(EvaluateGift(npcId, r.ItemId));
        }

        return list.OrderBy(g => g.IsRejected)
                   .ThenBy(g => !string.IsNullOrEmpty(g.WarningVi))
                   .ThenByDescending(g => g.ProjectedAffinityGain)
                   .ThenByDescending(g => g.BasePrice)
                   .ToList();
    }
}
