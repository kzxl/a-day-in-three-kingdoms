using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Power;

/// <summary>
/// Tactical combat simulation engine implementing the 1v1 Martial Skirmish
/// Turn-Based State Machine with 4 asymmetric actions and Troop Counter-Affinities:
/// - Attack (Công), Defend (Thủ), SpecialStrike (Sát), FocusSpirit (Khí).
/// - Rock-paper-scissors troop branches: Cavalry > Infantry > Archers > Cavalry.
/// </summary>
public static class TacticalDuelEngine
{
    /// <summary>
    /// Deterministically resolves the martial troop branch affinity for an NPC.
    /// </summary>
    public static TroopBranch ResolveNpcBranch(NpcDef npc)
    {
        // Skill-based mapping
        if (npc.Skills.Any(s => s.Code is "vo_thanh" or "long_dam" or "dau_than" or "pha_quan"))
        {
            return TroopBranch.Cavalry;
        }

        if (npc.Skills.Any(s => s.Code is "thiet_bich" or "ho_si" or "khac_dich"))
        {
            return TroopBranch.Infantry;
        }

        if (npc.Skills.Any(s => s.Code is "than_xa" or "lien_hoan" or "phuong_so" or "than_toan" or "hoa_than"))
        {
            return TroopBranch.Archers;
        }

        // Bio/Role/Name hints
        var text = $"{npc.NameVi} {npc.Role} {npc.BioVi}".ToLowerInvariant();
        if (text.Contains("kỵ") || text.Contains("mã") || text.Contains("thiết kỵ") || text.Contains("chiến mã"))
        {
            return TroopBranch.Cavalry;
        }

        if (text.Contains("cung") || text.Contains("xạ") || text.Contains("mưu") || text.Contains("quân sư"))
        {
            return TroopBranch.Archers;
        }

        if (text.Contains("thuẫn") || text.Contains("bộ") || text.Contains("hộ vệ") || text.Contains("đao khiên"))
        {
            return TroopBranch.Infantry;
        }

        // Deterministic hash fallback
        return (TroopBranch)(Math.Abs(npc.Id) % 3);
    }

    /// <summary>
    /// Resolves the primary troop branch for a hostile stronghold.
    /// </summary>
    public static TroopBranch ResolveStrongholdBranch(long strongholdId, string faction, string nameVi)
    {
        var lower = nameVi.ToLowerInvariant();
        if (lower.Contains("mã tặc") || lower.Contains("kỵ"))
        {
            return TroopBranch.Cavalry;
        }

        if (lower.Contains("thủy") || lower.Contains("hải") || lower.Contains("cung") || faction == "bach_ba")
        {
            return TroopBranch.Archers;
        }

        if (faction is "khan_vang" or "hac_son")
        {
            return TroopBranch.Infantry;
        }

        return (TroopBranch)(Math.Abs(strongholdId) % 3);
    }

    /// <summary>
    /// Computes the attack and incoming-damage multipliers for an NPC's character traits in a duel.
    /// - Nóng nảy: higher attack, lower defense (takes more incoming damage).
    /// - Điềm tĩnh: lower attack, higher defense (takes less incoming damage).
    /// </summary>
    public static (double AttackMultiplier, double IncomingDamageMultiplier) ResolveTraitCombatModifiers(
        IReadOnlyList<NpcTraitDef>? opponentTraits)
    {
        var atkMult = 1.0;
        var incomingDmgMult = 1.0;

        if (opponentTraits is null)
            return (atkMult, incomingDmgMult);

        for (var i = 0; i < opponentTraits.Count; i++)
        {
            var code = opponentTraits[i].Code;
            if (string.Equals(code, NpcTraitCodes.NongNay, StringComparison.OrdinalIgnoreCase))
            {
                atkMult *= NpcTraitRules.NongNayAttackMultiplier;
                incomingDmgMult *= NpcTraitRules.NongNayIncomingDamageMultiplier;
            }
            else if (string.Equals(code, NpcTraitCodes.DiemTinh, StringComparison.OrdinalIgnoreCase))
            {
                atkMult *= NpcTraitRules.DiemTinhAttackMultiplier;
                incomingDmgMult *= NpcTraitRules.DiemTinhIncomingDamageMultiplier;
            }
        }

        return (atkMult, incomingDmgMult);
    }

    /// <summary>
    /// Runs a full simulated tactical duel and produces round-by-round clash logs.
    /// </summary>
    public static DuelResult SimulateDuel(
        IPlayerAttributes playerAttrs,
        NpcDef opponent,
        TroopBranch playerBranch,
        int? seed = null,
        IReadOnlyList<NpcTraitDef>? opponentTraits = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var opponentBranch = ResolveNpcBranch(opponent);

        var pAttr = playerAttrs.Attributes;
        var nAttr = opponent.Attributes;

        // Vitals initialization
        var playerMaxHp = 100 + (pAttr.Con / 2);
        var opponentMaxHp = 100 + (nAttr.Con / 2);

        var playerHp = playerMaxHp;
        var opponentHp = opponentMaxHp;

        var playerSpirit = pAttr.Wil >= 80 ? 10 : 0;
        var opponentSpirit = nAttr.Wil >= 80 ? 10 : 0;

        var playerCharged = false;
        var opponentCharged = false;

        var roundLogs = new List<DuelRoundLog>();
        const int maxRounds = 5;

        // Base Effective Combat Power
        var pWar = (double)pAttr.War;
        var nWar = (double)nAttr.War;

        // Skill damage multipliers
        var skillNotes = new List<string>();

        var pSkillMult = 1.0;
        if (playerAttrs.IsSkillEquipped("vo_thanh"))
        {
            var lvl = Math.Max(1, playerAttrs.GetEquippedSkillLevel("vo_thanh"));
            var bonus = 0.15 + (lvl * 0.03); // Lv 1: +18%, Lv 5: +30%
            pSkillMult += bonus;
            skillNotes.Add($"Bạn thi triển tuyệt kỹ [Võ Thánh Lv.{lvl}] (+{(int)(bonus * 100)}% chiến lực)");
        }
        else if (playerAttrs.IsSkillEquipped("long_dam"))
        {
            var lvl = Math.Max(1, playerAttrs.GetEquippedSkillLevel("long_dam"));
            var bonus = 0.11 + (lvl * 0.03); // Lv 1: +14%, Lv 5: +26%
            pSkillMult += bonus;
            skillNotes.Add($"Bạn thi triển tuyệt kỹ [Long Đảm Lv.{lvl}] (+{(int)(bonus * 100)}% chiến lực)");
        }
        else if (playerAttrs.IsSkillEquipped("dau_than"))
        {
            var lvl = Math.Max(1, playerAttrs.GetEquippedSkillLevel("dau_than"));
            var bonus = 0.11 + (lvl * 0.03); // Lv 1: +14%, Lv 5: +26%
            pSkillMult += bonus;
            skillNotes.Add($"Bạn thi triển tuyệt kỹ [Đấu Thần Lv.{lvl}] (+{(int)(bonus * 100)}% chiến lực)");
        }

        var isThietBichEquipped = playerAttrs.IsSkillEquipped("thiet_bich");
        var thietBichLevel = isThietBichEquipped ? Math.Max(1, playerAttrs.GetEquippedSkillLevel("thiet_bich")) : 0;
        if (isThietBichEquipped)
        {
            var defBonus = 10 + (thietBichLevel * 2); // Lv 1: +12%, Lv 5: +20%
            skillNotes.Add($"Bạn kích hoạt thế thủ [Thiết Bích Lv.{thietBichLevel}] (+{defBonus}% phòng thủ)");
        }

        var nSkillMult = 1.0;
        foreach (var skill in opponent.Skills.Where(s => s.Category == "martial" || s.Tier >= 4))
        {
            var mult = skill.Tier switch
            {
                5 => 0.20,
                4 => 0.12,
                _ => 0.08
            };
            nSkillMult += mult;
            skillNotes.Add($"Đối thủ kích hoạt đặc kỹ [{skill.NameVi}] ({skill.EffectSummary})");
        }

        // Troop branch counter multipliers
        var pBranchAtk = TroopBranchHelper.Counters(playerBranch, opponentBranch) ? 1.25 : 1.0;
        var pBranchDef = TroopBranchHelper.Counters(playerBranch, opponentBranch) ? 0.85 : 1.0;

        var nBranchAtk = TroopBranchHelper.Counters(opponentBranch, playerBranch) ? 1.25 : 1.0;
        var nBranchDef = TroopBranchHelper.Counters(opponentBranch, playerBranch) ? 0.85 : 1.0;

        var (traitAtkMult, traitIncomingMult) = ResolveTraitCombatModifiers(opponentTraits);
        nBranchAtk *= traitAtkMult;
        nBranchDef *= traitIncomingMult;

        if (opponentTraits is not null)
        {
            if (opponentTraits.Any(t => string.Equals(t.Code, NpcTraitCodes.NongNay, StringComparison.OrdinalIgnoreCase)))
                skillNotes.Add("Đối thủ [Nóng nảy] (Công +15%, Thủ -15%)");
            if (opponentTraits.Any(t => string.Equals(t.Code, NpcTraitCodes.DiemTinh, StringComparison.OrdinalIgnoreCase)))
                skillNotes.Add("Đối thủ [Điềm tĩnh] (Công -10%, Thủ +15%)");
        }

        int round = 1;
        while (round <= maxRounds && playerHp > 0 && opponentHp > 0)
        {
            var pAction = ChoosePlayerAction(playerSpirit, playerCharged, opponentHp, opponentSpirit, rng);
            var oAction = ChooseOpponentAction(nWar, opponentSpirit, opponentCharged, playerHp, rng);

            // Execute Clash
            var clash = ResolveClash(
                round,
                pAction,
                oAction,
                pWar,
                nWar,
                pSkillMult,
                nSkillMult,
                pBranchAtk,
                pBranchDef,
                nBranchAtk,
                nBranchDef,
                playerHp,
                opponentHp,
                playerSpirit,
                opponentSpirit,
                playerCharged,
                opponentCharged,
                thietBichLevel,
                opponent.NameVi,
                playerBranch,
                opponentBranch,
                rng);

            playerHp = clash.PlayerHpAfter;
            opponentHp = clash.OpponentHpAfter;
            playerSpirit = clash.PlayerSpiritAfter;
            opponentSpirit = clash.OpponentSpiritAfter;
            playerCharged = pAction == TacticalDuelAction.FocusSpirit && oAction != TacticalDuelAction.Attack && oAction != TacticalDuelAction.SpecialStrike;
            opponentCharged = oAction == TacticalDuelAction.FocusSpirit && pAction != TacticalDuelAction.Attack && pAction != TacticalDuelAction.SpecialStrike;

            roundLogs.Add(clash);
            round++;
        }

        var won = playerHp > opponentHp;
        var totalRounds = roundLogs.Count;
        var prizeGold = won ? Math.Max(70, (int)(nAttr.War * 2.4)) : 0;

        // Combat Experience: Train all equipped martial skills
        var expReward = won ? 40 : 20;
        foreach (var prog in playerAttrs.SkillProgresses.Where(p => p.IsEquipped))
        {
            playerAttrs.TrainSkill(prog.SkillCode, expReward);
        }

        var skillDesc = skillNotes.Count > 0 ? " " + string.Join(" · ", skillNotes) : "";
        var summary = won
            ? $"Đại thắng! Trải qua {totalRounds} hiệp tỷ võ biến hóa khôn lường, bạn đã xuất sắc đánh bại danh tướng {opponent.NameVi}! ({TroopBranchHelper.NameVi(playerBranch)} khắc chế thành công).{skillDesc}"
            : $"Thất bại! Võ nghệ và binh pháp của {opponent.NameVi} quá xuất chúng ({nAttr.War} Võ Lực), sau {totalRounds} hiệp bạn đã lực kiệt nhận thua.{skillDesc}";

        var avatarChar = !string.IsNullOrEmpty(opponent.NameVi) ? opponent.NameVi.Substring(0, 1) : "武";

        return new DuelResult(
            won,
            totalRounds,
            prizeGold,
            opponent.NameVi,
            summary,
            playerBranch,
            opponentBranch,
            playerMaxHp,
            opponentMaxHp,
            playerHp,
            opponentHp,
            roundLogs,
            avatarChar,
            opponent.Role,
            opponent.Attributes.War);
    }

    private static TacticalDuelAction ChoosePlayerAction(
        int spirit,
        bool charged,
        int opponentHp,
        int opponentSpirit,
        Random rng)
    {
        if (spirit >= 30)
        {
            var roll = rng.NextDouble();
            if (roll < 0.50) return TacticalDuelAction.SpecialStrike;
            if (roll < 0.80) return TacticalDuelAction.Attack;
            if (roll < 0.92) return TacticalDuelAction.FocusSpirit;
            return TacticalDuelAction.Defend;
        }

        if (charged)
        {
            return rng.NextDouble() < 0.70 ? TacticalDuelAction.Attack : TacticalDuelAction.Defend;
        }

        var r = rng.NextDouble();
        if (r < 0.50) return TacticalDuelAction.Attack;
        if (r < 0.80) return TacticalDuelAction.FocusSpirit;
        return TacticalDuelAction.Defend;
    }

    private static TacticalDuelAction ChooseOpponentAction(
        double war,
        int spirit,
        bool charged,
        int playerHp,
        Random rng)
    {
        if (spirit >= 30)
        {
            var threshold = war >= 85 ? 0.55 : 0.40;
            var roll = rng.NextDouble();
            if (roll < threshold) return TacticalDuelAction.SpecialStrike;
            if (roll < 0.80) return TacticalDuelAction.Attack;
            return TacticalDuelAction.Defend;
        }

        if (charged)
        {
            return rng.NextDouble() < 0.65 ? TacticalDuelAction.Attack : TacticalDuelAction.Defend;
        }

        var r = rng.NextDouble();
        if (war >= 85)
        {
            if (r < 0.55) return TacticalDuelAction.Attack;
            if (r < 0.80) return TacticalDuelAction.FocusSpirit;
            return TacticalDuelAction.Defend;
        }

        if (r < 0.45) return TacticalDuelAction.Attack;
        if (r < 0.75) return TacticalDuelAction.FocusSpirit;
        return TacticalDuelAction.Defend;
    }

    private static DuelRoundLog ResolveClash(
        int round,
        TacticalDuelAction pAction,
        TacticalDuelAction oAction,
        double pWar,
        double oWar,
        double pSkillMult,
        double nSkillMult,
        double pBranchAtk,
        double pBranchDef,
        double nBranchAtk,
        double nBranchDef,
        int currentPlayerHp,
        int currentOpponentHp,
        int currentPlayerSpirit,
        int currentOpponentSpirit,
        bool pCharged,
        bool oCharged,
        int pThietBichLevel,
        string opponentName,
        TroopBranch playerBranch,
        TroopBranch opponentBranch,
        Random rng)
    {
        // Base damage rolls
        var d10P = rng.Next(1, 11);
        var d10O = rng.Next(1, 11);
        var d20P = rng.Next(1, 21);
        var d20O = rng.Next(1, 21);

        var pRawAtk = (int)((pWar * 0.42 + d10P) * pSkillMult * pBranchAtk * (pCharged ? 1.30 : 1.0));
        var oRawAtk = (int)((oWar * 0.42 + d10O) * nSkillMult * nBranchAtk * (oCharged ? 1.30 : 1.0));

        var pRawSpec = (int)((pWar * 0.88 + d20P) * pSkillMult * pBranchAtk * (pCharged ? 1.30 : 1.0));
        var oRawSpec = (int)((oWar * 0.88 + d20O) * nSkillMult * nBranchAtk * (oCharged ? 1.30 : 1.0));

        int pDmg = 0;
        int oDmg = 0;
        int pSpiritDelta = 0;
        int oSpiritDelta = 0;
        bool isCritical = false;
        bool isParried = false;
        string narrative;

        // Spend spirit for SpecialStrike
        if (pAction == TacticalDuelAction.SpecialStrike) pSpiritDelta -= 30;
        if (oAction == TacticalDuelAction.SpecialStrike) oSpiritDelta -= 30;

        // Tactical Matrix Resolution
        switch (pAction, oAction)
        {
            case (TacticalDuelAction.Attack, TacticalDuelAction.Attack):
                pDmg = (int)(pRawAtk * nBranchDef);
                oDmg = (int)(oRawAtk * pBranchDef);
                pSpiritDelta += 10;
                oSpiritDelta += 10;
                narrative = $"Hiệp {round}: Đao kiếm giao phong kịch liệt! Cả hai bên đều xuất chiêu [CÔNG] dũng mãnh đối kích. Bạn gây {pDmg} sát thương, đối thủ đánh trả {oDmg} sát thương!";
                break;

            case (TacticalDuelAction.Attack, TacticalDuelAction.Defend):
                pDmg = Math.Max(2, (int)(pRawAtk * 0.25 * nBranchDef));
                oDmg = 0;
                pSpiritDelta += 8;
                oSpiritDelta += 12;
                isParried = true;
                narrative = $"Hiệp {round}: Bạn mãnh hổ vồ mồi giáng chiêu [CÔNG], nhưng {opponentName} phòng ngự vững như bàn thạch [THỦ] hóa giải 75% lực đánh (gây {pDmg} sát thương)!";
                break;

            case (TacticalDuelAction.Defend, TacticalDuelAction.Attack):
                var defFactor = pThietBichLevel > 0 ? Math.Max(0.12, 0.22 - (pThietBichLevel * 0.015)) : 0.25;
                oDmg = Math.Max(2, (int)(oRawAtk * defFactor * pBranchDef));
                pDmg = 0;
                pSpiritDelta += 12;
                oSpiritDelta += 8;
                isParried = true;
                narrative = $"Hiệp {round}: {opponentName} dồn lực vung vũ khí [CÔNG], bạn bình tĩnh lập thuẫn thế [THỦ] {(pThietBichLevel > 0 ? $"[Thiết Bích Lv.{pThietBichLevel}] " : "")}chặn đứng đòn hiểm (chỉ nhận {oDmg} sát thương)!";
                break;

            case (TacticalDuelAction.Defend, TacticalDuelAction.Defend):
                pDmg = 0;
                oDmg = 0;
                pSpiritDelta += 8;
                oSpiritDelta += 8;
                narrative = $"Hiệp {round}: Cả hai cao thủ đều lập thế phòng bị [THỦ], kìm chân quan sát sơ hở của đối phương, tích tụ khí tức.";
                break;

            case (TacticalDuelAction.Attack, TacticalDuelAction.FocusSpirit):
                // CRUSH: Focus interrupted!
                pDmg = (int)(pRawAtk * 1.35 * nBranchDef);
                oDmg = 0;
                pSpiritDelta += 12;
                oSpiritDelta = 0;
                isCritical = true;
                narrative = $"Hiệp {round}: [PHÁ CHIÊU BẠO KÍCH!] {opponentName} đang tĩnh tâm [KHÍ] liền bị bạn bất thần xuất đòn [CÔNG] phá vỡ chân khí, gây {pDmg} sát thương choáng váng!";
                break;

            case (TacticalDuelAction.FocusSpirit, TacticalDuelAction.Attack):
                // CRUSH against player
                pDmg = 0;
                oDmg = (int)(oRawAtk * 1.35 * pBranchDef);
                pSpiritDelta = 0;
                oSpiritDelta += 12;
                isCritical = true;
                narrative = $"Hiệp {round}: [BỊ PHÁ KHÍ!] Trong lúc bạn đang vận khí [KHÍ], {opponentName} chớp thời cơ phóng đòn [CÔNG] đánh tan tụ khí, gây {oDmg} sát thương nặng nề!";
                break;

            case (TacticalDuelAction.SpecialStrike, TacticalDuelAction.Defend):
                // Parry-counter against Special
                isParried = true;
                pDmg = Math.Max(5, (int)(pRawSpec * 0.20 * nBranchDef));
                oDmg = (int)(oWar * 0.40 + rng.Next(1, 10));
                oSpiritDelta += 15;
                narrative = $"Hiệp {round}: Bạn dồn toàn lực xuất chiêu [SÁT] cực hạn! Nhưng {opponentName} đã đoán trước dùng [THỦ] phản kích chuẩn xác, chỉ trúng {pDmg} và đánh trả bạn {oDmg} sát thương!";
                break;

            case (TacticalDuelAction.Defend, TacticalDuelAction.SpecialStrike):
                // Player parry-counters Special
                isParried = true;
                oDmg = Math.Max(5, (int)(oRawSpec * 0.20 * pBranchDef));
                pDmg = (int)(pWar * 0.40 + rng.Next(1, 10));
                pSpiritDelta += 15;
                narrative = $"Hiệp {round}: [PHẢN ĐÒN XUẤT THẦN!] {opponentName} cuồng nộ tung tuyệt kỹ [SÁT] sát chiêu, bạn vững thế [THỦ] đỡ gạt ngoạn mục (nhận {oDmg}) rồi phản đòn {pDmg} sát thương!";
                break;

            case (TacticalDuelAction.SpecialStrike, TacticalDuelAction.Attack):
                // Special overpowers Attack
                pDmg = (int)(pRawSpec * nBranchDef);
                oDmg = (int)(oRawAtk * 0.65 * pBranchDef);
                pSpiritDelta += 5;
                isCritical = true;
                narrative = $"Hiệp {round}: [TUYỆT KỸ TRẤN ÁP!] Đòn [CÔNG] của đối phương bị tuyệt kỹ [SÁT] của bạn nghiền ép áp đảo hoàn toàn! Bạn gây chấn động {pDmg} sát thương (đối thủ trả {oDmg})!";
                break;

            case (TacticalDuelAction.Attack, TacticalDuelAction.SpecialStrike):
                // Opponent special overpowers Attack
                pDmg = (int)(pRawAtk * 0.65 * nBranchDef);
                oDmg = (int)(oRawSpec * pBranchDef);
                oSpiritDelta += 5;
                isCritical = true;
                narrative = $"Hiệp {round}: {opponentName} rống vang chiến trường thi triển sát chiêu [SÁT] quét sạch đòn [CÔNG] của bạn! Bạn chịu {oDmg} sát thương (chỉ đánh trúng {pDmg})!";
                break;

            case (TacticalDuelAction.SpecialStrike, TacticalDuelAction.SpecialStrike):
                // Clash of Titans!
                pDmg = (int)(pRawSpec * 1.15 * nBranchDef);
                oDmg = (int)(oRawSpec * 1.15 * pBranchDef);
                isCritical = true;
                narrative = $"Hiệp {round}: [LONG TRANH HỔ ĐẤU!] Hai đại cao thủ đồng loạt tung tuyệt kỹ tối thượng [SÁT]! Uy lực chấn động lôi đài, bạn gây {pDmg} sát thương, nhận lại {oDmg} sát thương chí mạng!";
                break;

            case (TacticalDuelAction.SpecialStrike, TacticalDuelAction.FocusSpirit):
                // DECIMATION
                pDmg = (int)(pRawSpec * 1.50 * nBranchDef);
                oDmg = 0;
                oSpiritDelta = 0;
                isCritical = true;
                narrative = $"Hiệp {round}: [HỦY DIỆT!] {opponentName} bất cẩn [KHÍ] giữa trận tiền, lãnh trọn toàn bộ tuyệt kỹ [SÁT] chí mạng của bạn! Gây ra sát thương hủy diệt {pDmg} điểm!";
                break;

            case (TacticalDuelAction.FocusSpirit, TacticalDuelAction.SpecialStrike):
                pDmg = 0;
                oDmg = (int)(oRawSpec * 1.50 * pBranchDef);
                pSpiritDelta = 0;
                isCritical = true;
                narrative = $"Hiệp {round}: [HIỂM NGUY!] Bạn vận khí [KHÍ] không kịp trở tay trước tuyệt kỹ [SÁT] cuồng bạo của {opponentName}! Hứng trọn {oDmg} sát thương kinh hoàng!";
                break;

            case (TacticalDuelAction.FocusSpirit, TacticalDuelAction.Defend):
                pDmg = 0;
                oDmg = 0;
                pSpiritDelta += 35;
                narrative = $"Hiệp {round}: [TỤ KHÍ QUY NGUYÊN!] Thấy đối phương vào thế [THỦ], bạn thong dong điều hòa khí huyết [KHÍ], tích tụ +35 Khí lực và nhận buff uy lực hiệp sau!";
                break;

            case (TacticalDuelAction.Defend, TacticalDuelAction.FocusSpirit):
                pDmg = 0;
                oDmg = 0;
                oSpiritDelta += 35;
                narrative = $"Hiệp {round}: Bạn cẩn trọng phòng thủ [THỦ], tạo thời cơ cho {opponentName} tụ khí quy nguyên [KHÍ] (+35 Khí lực, chuẩn bị sát chiêu)!";
                break;

            case (TacticalDuelAction.FocusSpirit, TacticalDuelAction.FocusSpirit):
                pDmg = 0;
                oDmg = 0;
                pSpiritDelta += 35;
                oSpiritDelta += 35;
                narrative = $"Hiệp {round}: [LƯỠNG HÙNG ĐỐI TỌA!] Cả hai võ sĩ đều lùi một bước tĩnh tâm vận [KHÍ], cùng tích lũy +35 Khí lực để chuẩn bị cho màn quyết đấu sinh tử kế tiếp!";
                break;

            default:
                narrative = $"Hiệp {round}: Giao đấu cân tài cân sức.";
                break;
        }

        var nextPlayerHp = Math.Max(0, currentPlayerHp - oDmg);
        var nextOpponentHp = Math.Max(0, currentOpponentHp - pDmg);
        var nextPlayerSpirit = Math.Clamp(currentPlayerSpirit + pSpiritDelta, 0, 100);
        var nextOpponentSpirit = Math.Clamp(currentOpponentSpirit + oSpiritDelta, 0, 100);

        return new DuelRoundLog(
            round,
            pAction,
            oAction,
            pDmg,
            oDmg,
            nextPlayerHp,
            nextOpponentHp,
            nextPlayerSpirit,
            nextOpponentSpirit,
            isCritical,
            isParried,
            narrative);
    }
}
