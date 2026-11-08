namespace SimT.Contracts.Playing;

/// <summary>
/// Canonical character trait codes for DLC Character (Đợt 5 — Đặc tính nhân vật).
/// </summary>
public static class NpcTraitCodes
{
    public const string ThamTai = "tham_tai";
    public const string ThanhLiem = "thanh_liem";
    public const string TrungNghia = "trung_nghia";
    public const string DaNghi = "da_nghi";
    public const string HaoSang = "hao_sang";
    public const string KeoKiet = "keo_kiet";
    public const string HieuHoc = "hieu_hoc";
    public const string NongNay = "nong_nay";
    public const string DiemTinh = "diem_tinh";
    public const string KieuNgao = "kieu_ngao";
    public const string KhiemCung = "khiem_cung";
    public const string MeRuou = "me_ruou";
    public const string MeVo = "me_vo";
    public const string TrongChuTin = "trong_chu_tin";
    public const string XaoQuyet = "xao_quyet";
    public const string NhanHau = "nhan_hau";
}

/// <summary>
/// Centralized gameplay impact constants for NPC character traits.
/// Keeping all tuning knobs in one place ensures transparent balancing across
/// market stalls, gift preferences, dialogue, and tactical duels.
/// </summary>
public static class NpcTraitRules
{
    /// <summary>
    /// Affinity required to reveal all traits of an NPC on Dossier and Talk scenes.
    /// Below this threshold (first meeting / Sơ giao), only the first trait is shown and remaining traits are masked as "???".
    /// </summary>
    public const int FullRevealAffinityThreshold = 20;

    public const string MaskedTraitCode = "hidden_trait";
    public const string MaskedTraitLabelVi = "???";
    public const string MaskedTraitDescriptionVi = "Cần kết giao thân thiết hơn (Hảo cảm ≥ 20) để thấu tỏ đặc tính này.";

    // --- 1. Mua bán (INpcMarket / MarketService) ---
    /// <summary>Tham tài: sells to player at 115% (+15% dearer).</summary>
    public const int ThamTaiSellToPlayerPercent = 115;

    /// <summary>Tham tài: buys from player at 85% (-15% cheaper).</summary>
    public const int ThamTaiBuyFromPlayerPercent = 85;

    /// <summary>Hào sảng: sells to player at 85% (-15% cheaper).</summary>
    public const int HaoSangSellToPlayerPercent = 85;

    /// <summary>Hào sảng: buys from player at 115% (+15% dearer).</summary>
    public const int HaoSangBuyFromPlayerPercent = 115;

    /// <summary>Keo kiệt: stall offer stock scaled to 60% of normal quantity.</summary>
    public const int KeoKietStockPercent = 60;

    /// <summary>Trọng chữ tín: price anchored at 100% of item base price (ignores market volatility).</summary>
    public const int TrongChuTinPricePercent = 100;

    // --- 2. Tặng quà (SocialService.GiftNpc) ---
    /// <summary>Mê rượu: additive gift multiplier bonus when gifted wine ("ruou").</summary>
    public const double MeRuouWineBonusMultiplier = 0.55;

    /// <summary>Hiếu học: additive gift multiplier bonus when gifted tea, paper, or books ("tra", "giay", "sach").</summary>
    public const double HieuHocScholarBonusMultiplier = 0.50;

    /// <summary>Mê võ: additive gift multiplier bonus when gifted martial materials or weapons ("sat", "dong", "cung_ten").</summary>
    public const double MeVoMartialBonusMultiplier = 0.50;

    /// <summary>Thanh liêm: subtractive gift multiplier penalty when gifted expensive luxury items (treated as bribery).</summary>
    public const double ThanhLiemExpensiveGiftPenaltyMultiplier = -0.45;

    /// <summary>Base price threshold at or above which Thanh liêm treats a non-scholarly gift as bribery.</summary>
    public const long ThanhLiemBriberyPriceThreshold = 120;

    // --- 3. Đấu võ (TacticalDuelEngine / MilitaryService) ---
    /// <summary>Nóng nảy: +15% attack damage dealt in duels.</summary>
    public const double NongNayAttackMultiplier = 1.15;

    /// <summary>Nóng nảy: -15% defense (+15% incoming damage taken) in duels.</summary>
    public const double NongNayIncomingDamageMultiplier = 1.15;

    /// <summary>Điềm tĩnh: -10% attack damage dealt in duels.</summary>
    public const double DiemTinhAttackMultiplier = 0.90;

    /// <summary>Điềm tĩnh: +15% defense (-15% incoming damage taken) in duels.</summary>
    public const double DiemTinhIncomingDamageMultiplier = 0.85;

    /// <summary>Mê võ: bonus affinity granted when the player defeats this NPC in a martial duel.</summary>
    public const int MeVoDuelVictoryAffinityBonus = 6;
}

/// <summary>
/// Definition of a single NPC character trait.
/// </summary>
public sealed record NpcTraitDef(
    string Code,
    string NameVi,
    string DescriptionVi,
    string? OppositeCode = null);

/// <summary>
/// UI-ready view of an NPC trait, supporting affinity-based masking ("???").
/// </summary>
public sealed record NpcTraitView(
    string Code,
    string NameVi,
    string DescriptionVi,
    bool Hidden = false);

/// <summary>
/// Capability contract for querying NPC character traits across modules (e.g., Debate in Batch 9, Market, Duels).
/// </summary>
public interface INpcTraits
{
    IReadOnlyList<NpcTraitDef> GetAllTraitDefs();
    IReadOnlyList<NpcTraitDef> GetTraits(long npcId);
    IReadOnlyList<NpcTraitView> GetVisibleTraits(long npcId);
    bool HasTrait(long npcId, string traitCode);
}
