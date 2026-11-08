using SimT.Contracts.Playing;

namespace SimT.Modules.Dlc.Character;

/// <summary>
/// Centralized rules, trait multiplier table, push/counter formulas, and contextual Vietnamese
/// sentence templates for Batch 9 (Đợt 9 — Biện luận).
/// </summary>
public static class DebateRules
{
    public const int DefaultLoiCopperCost = 40;
    public const int DispleasureCounterBonus = 6;
    public const double StatPushScale = 0.25;
    public const double OpponentCounterScale = 0.14;

    public sealed record TraitDebateEntry(
        double Loi = 1.0,
        double Tinh = 1.0,
        double Ly = 1.0,
        double Uy = 1.0,
        bool LoiDispleases = false);

    /// <summary>
    /// Single-place table mapping NPC character traits to argument push multipliers.
    /// Blank cells are 1.0.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, TraitDebateEntry> TraitMultipliers =
        new Dictionary<string, TraitDebateEntry>(StringComparer.OrdinalIgnoreCase)
        {
            [NpcTraitCodes.ThamTai] = new(Loi: 1.5),
            [NpcTraitCodes.ThanhLiem] = new(Loi: 0.3, LoiDispleases: true),
            [NpcTraitCodes.TrungNghia] = new(Loi: 0.5, Tinh: 1.4),
            [NpcTraitCodes.DaNghi] = new(Tinh: 0.6, Ly: 1.4),
            [NpcTraitCodes.KieuNgao] = new(Uy: 0.4),
            [NpcTraitCodes.KhiemCung] = new(Uy: 1.3),
            [NpcTraitCodes.NongNay] = new(Uy: -0.5)
        };

    public static double GetSingleTraitMultiplier(string traitCode, DebateArgumentKind kind)
    {
        if (!TraitMultipliers.TryGetValue(traitCode, out var entry))
            return 1.0;

        return kind switch
        {
            DebateArgumentKind.Loi => entry.Loi,
            DebateArgumentKind.Tinh => entry.Tinh,
            DebateArgumentKind.Ly => entry.Ly,
            DebateArgumentKind.Uy => entry.Uy,
            _ => 1.0
        };
    }

    public static double GetCombinedMultiplier(
        IReadOnlyList<NpcTraitDef> opponentTraits,
        DebateArgumentKind kind,
        out bool displeasesOpponent)
    {
        displeasesOpponent = false;
        if (opponentTraits is null || opponentTraits.Count == 0)
            return 1.0;

        var product = 1.0;
        var hasNegative = false;
        for (var i = 0; i < opponentTraits.Count; i++)
        {
            var code = opponentTraits[i].Code;
            var m = GetSingleTraitMultiplier(code, kind);
            if (m < 0)
            {
                hasNegative = true;
                product *= Math.Abs(m);
            }
            else
            {
                product *= m;
            }

            if (kind == DebateArgumentKind.Loi &&
                TraitMultipliers.TryGetValue(code, out var entry) &&
                entry.LoiDispleases)
            {
                displeasesOpponent = true;
            }
        }

        return hasNegative ? -product : product;
    }

    public static int GetPlayerBaseStat(
        DebateArgumentKind kind,
        CharacterAttributes attrs,
        int fame,
        int loiCopperCost = DefaultLoiCopperCost) => kind switch
    {
        DebateArgumentKind.Ly => Math.Max(20, attrs.Int),
        DebateArgumentKind.Tinh => Math.Max(20, attrs.Dip),
        DebateArgumentKind.Loi => Math.Max(25, loiCopperCost + (attrs.Com / 2)),
        DebateArgumentKind.Uy => Math.Clamp(40 + (fame / 4) + (attrs.Ldr / 4), 30, 110),
        _ => 50
    };

    public static int ComputePlayerPush(
        int playerBaseStat,
        IReadOnlyList<NpcTraitDef> opponentTraits,
        DebateArgumentKind kind,
        out double multiplier,
        out bool displeasesOpponent)
    {
        multiplier = GetCombinedMultiplier(opponentTraits, kind, out displeasesOpponent);
        var raw = playerBaseStat * StatPushScale * multiplier;
        var rounded = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        if (rounded == 0)
            rounded = multiplier < 0 ? -1 : 1;
        return rounded;
    }

    public static int ComputeOpponentCounter(
        CharacterAttributes npcAttrs,
        bool useDipStat,
        bool displeased,
        int rngRoll0To2)
    {
        var stat = useDipStat ? Math.Max(npcAttrs.Dip, npcAttrs.Int - 5) : Math.Max(npcAttrs.Int, npcAttrs.Dip - 5);
        var baseCounter = Math.Max(3, (int)Math.Round(stat * OpponentCounterScale, MidpointRounding.AwayFromZero));
        var bonus = Math.Clamp(rngRoll0To2, 0, 2) + (displeased ? DispleasureCounterBonus : 0);
        return baseCounter + bonus;
    }

    public static int ComputeHaggleModifierPercent(DebateOutcomeKind outcome, int finalPersuasion) => outcome switch
    {
        DebateOutcomeKind.MajorWin => -Math.Clamp(10 + ((Math.Clamp(finalPersuasion, 80, 100) - 80) * 5 / 20), 10, 15),
        DebateOutcomeKind.MinorWin => -Math.Clamp(5 + ((Math.Clamp(finalPersuasion, 50, 79) - 50) * 4 / 29), 5, 9),
        DebateOutcomeKind.MinorLoss or DebateOutcomeKind.MajorLoss => 5,
        _ => 0
    };

    public static int ComputeRecruitFeeDiscountPercent(DebateOutcomeKind outcome) => outcome switch
    {
        DebateOutcomeKind.MajorWin => 40,
        DebateOutcomeKind.MinorWin => 25,
        _ => 0
    };

    public static int ComputeTaxReliefPercent(DebateOutcomeKind outcome) => outcome switch
    {
        DebateOutcomeKind.MajorWin => 4,
        DebateOutcomeKind.MinorWin => 2,
        _ => 0
    };

    public static string KindLabelVi(DebateArgumentKind kind) => kind switch
    {
        DebateArgumentKind.Ly => "Lý",
        DebateArgumentKind.Tinh => "Tình",
        DebateArgumentKind.Loi => "Lợi",
        DebateArgumentKind.Uy => "Uy",
        _ => "Lý"
    };

    public static string BuildArgumentTagVi(DebateArgumentKind kind, int costCopper) => kind switch
    {
        DebateArgumentKind.Loi => $"Lợi · {costCopper} quan",
        DebateArgumentKind.Ly => "Lý · Mưu lược",
        DebateArgumentKind.Tinh => "Tình · Khẩu tài",
        DebateArgumentKind.Uy => "Uy · Danh vọng",
        _ => "Lý · Mưu lược"
    };

    public static string BuildArgumentLine(
        string topic,
        DebateArgumentKind kind,
        int templateVariant,
        string cityNameVi,
        string npcShortNameVi,
        string itemNameVi,
        int costCopper)
    {
        var normTopic = DebateTopics.Normalize(topic);
        var idx = Math.Abs(templateVariant) % 3;

        return (normTopic, kind, idx) switch
        {
            (DebateTopics.Haggle, DebateArgumentKind.Ly, 0) =>
                "Lụa Thục năm nay được mùa, giá ấy đã quá cao.",
            (DebateTopics.Haggle, DebateArgumentKind.Ly, 1) =>
                $"Đường buôn về {cityNameVi} thông suốt, giá {itemNameVi} nên hạ cho phải lẽ.",
            (DebateTopics.Haggle, DebateArgumentKind.Ly, _) =>
                $"Kho hàng quanh {cityNameVi} đang dồi dào, giữ giá {itemNameVi} cao khó xuất nhanh.",

            (DebateTopics.Haggle, DebateArgumentKind.Tinh, 0) =>
                $"Đôi bên giao thương lâu dài ở {cityNameVi}, xin bớt chút giá lấy chỗ thân tình.",
            (DebateTopics.Haggle, DebateArgumentKind.Tinh, 1) =>
                $"Giữa thời loạn gặp bạn buôn tri kỷ, xin {npcShortNameVi} nhường một bước kết giao.",
            (DebateTopics.Haggle, DebateArgumentKind.Tinh, _) =>
                "Thương hội xưa nay trọng tình nghĩa, bớt chút lời để giữ mối khách quen.",

            (DebateTopics.Haggle, DebateArgumentKind.Loi, 0) =>
                $"Ta đặt ngay {costCopper} quan tiền mặt tại bàn, gom trọn lô hàng cho sạp khỏi tồn kho.",
            (DebateTopics.Haggle, DebateArgumentKind.Loi, 1) =>
                $"Xin trao {costCopper} quan bạc nén làm lễ trà nước, đôi bên chốt giá sỉ mau lẹ.",
            (DebateTopics.Haggle, DebateArgumentKind.Loi, _) =>
                $"Chưởng quỹ nhận ngay {costCopper} quan tiền tươi, xuất một lượt khỏi tốn công giữ kho.",

            (DebateTopics.Haggle, DebateArgumentKind.Uy, 0) =>
                $"Thanh thế thương hội ta khắp {cityNameVi} đều rõ, xin định giá công bằng giữ chữ tín.",
            (DebateTopics.Haggle, DebateArgumentKind.Uy, 1) =>
                "Các sạp lớn trong thành đều nể danh ta, chớ vì chút lời nhỏ mà mất mối lớn.",
            (DebateTopics.Haggle, DebateArgumentKind.Uy, _) =>
                $"Uy danh buôn bán của ta ở {cityNameVi} xưa nay sòng phẳng, không thể chịu giá ép.",

            (DebateTopics.Recruit, DebateArgumentKind.Ly, 0) =>
                $"Thời cuộc biến đổi khôn lường, bậc kỳ tài như {npcShortNameVi} cần nơi đất lành dụng võ.",
            (DebateTopics.Recruit, DebateArgumentKind.Ly, 1) =>
                $"Cơ nghiệp tại {cityNameVi} đang đà hưng khởi, hợp sức lúc này ắt làm nên đại sự.",
            (DebateTopics.Recruit, DebateArgumentKind.Ly, _) =>
                "Trí giả chọn người biết trọng dụng thực tài, chớ để sở học cả đời mai một.",

            (DebateTopics.Recruit, DebateArgumentKind.Tinh, 0) =>
                $"Ta cầu hiền như khát nước, mong cùng {npcShortNameVi} kết nghĩa đồng tâm dựng nghiệp.",
            (DebateTopics.Recruit, DebateArgumentKind.Tinh, 1) =>
                "Bậc anh tài gặp người tri ngộ xưa nay hiếm có, xin chớ phụ tấm lòng thành.",
            (DebateTopics.Recruit, DebateArgumentKind.Tinh, _) =>
                "Tuy quen biết chưa lâu nhưng lòng kính trọng hiền sĩ của ta có trời đất chứng giám.",

            (DebateTopics.Recruit, DebateArgumentKind.Loi, 0) =>
                $"Xin dâng trước {costCopper} quan lễ mọn an gia, bổng lộc mỗi tháng đều đãi theo bậc thượng khách.",
            (DebateTopics.Recruit, DebateArgumentKind.Loi, 1) =>
                $"Trao ngay {costCopper} quan bạc trắng làm lễ ra mắt, việc chi dùng trong phủ đều chu toàn.",
            (DebateTopics.Recruit, DebateArgumentKind.Loi, _) =>
                $"Lễ kính hiền {costCopper} quan đặt trước mặt, quyết không để tiên sinh phải bận lòng sinh kế.",

            (DebateTopics.Recruit, DebateArgumentKind.Uy, 0) =>
                $"Danh tiếng của ta tại {cityNameVi} ngày một vang xa, về dưới trướng ắt rạng rỡ môn phong.",
            (DebateTopics.Recruit, DebateArgumentKind.Uy, 1) =>
                "Hào kiệt bốn phương đang hội tụ về đây, đây chính là lúc lập công danh lưu sử sách.",
            (DebateTopics.Recruit, DebateArgumentKind.Uy, _) =>
                "Uy tín của ta trên trường quan thương đã rõ, lời hứa trọng dụng nặng tựa ngàn vàng.",

            (DebateTopics.Tax, DebateArgumentKind.Ly, 0) =>
                $"Khoan thư sức dân và giảm thuế tại {cityNameVi} sẽ giúp hàng hóa lưu thông gấp bội.",
            (DebateTopics.Tax, DebateArgumentKind.Ly, 1) =>
                $"Thuế khóa vừa sức thì thương nhân bốn phương đổ về {cityNameVi}, quốc khố càng bền.",
            (DebateTopics.Tax, DebateArgumentKind.Ly, _) =>
                "Chiến sự liên miên, hạ bớt thuế suất lúc này mới giữ vững dân tâm và mạch giao thương.",

            (DebateTopics.Tax, DebateArgumentKind.Tinh, 0) =>
                $"Bá tánh và tiểu thương trong thành {cityNameVi} đều trông cậy lượng khoan hồng của quan phủ.",
            (DebateTopics.Tax, DebateArgumentKind.Tinh, 1) =>
                "Thương hội luôn góp sức cứu tế dân nghèo, cúi xin đại nhân châm chước giảm thuế.",
            (DebateTopics.Tax, DebateArgumentKind.Tinh, _) =>
                "Đại nhân trị nhậm lấy nhân chính làm gốc, xin mở lượng hải hà thương xót dân buôn.",

            (DebateTopics.Tax, DebateArgumentKind.Loi, 0) =>
                $"Thương hội xin nộp trước {costCopper} quan công quỹ tu bổ thành trì để đổi kỳ hạn giảm thuế.",
            (DebateTopics.Tax, DebateArgumentKind.Loi, 1) =>
                $"Xin đóng góp {costCopper} quan vào kho lương {cityNameVi}, mong quan phủ phê chuẩn giảm thuế.",
            (DebateTopics.Tax, DebateArgumentKind.Loi, _) =>
                $"Dâng {costCopper} quan phụ giúp việc công nha môn, cúi mong đại nhân nới nhẹ thuế tháng này.",

            _ =>
                $"Thương hội liên kết khắp vùng, mong quan phủ {cityNameVi} nới thuế để yên lòng giới công thương."
        };
    }

    public static string BuildOpponentReply(
        string topic,
        string npcShortNameVi,
        string cityNameVi,
        double multiplier,
        bool displeased,
        int variant)
    {
        if (displeased)
        {
            return $"{npcShortNameVi} nghiêm sắc mặt: “Ta giữ mình thanh liêm, chớ đem tiền bạc ra làm vẩn đục cuộc bàn luận!”";
        }

        if (multiplier < 0)
        {
            return $"{npcShortNameVi} đập bàn đứng dậy: “Ngươi định lấy danh thế áp chế ta sao? Ta quyết không nhượng bộ!”";
        }

        if (multiplier >= 1.3)
        {
            return (Math.Abs(variant) % 2) == 0
                ? $"{npcShortNameVi} trầm ngâm gật đầu: “Lời ấy quả có lý, đúng điều ta đang cân nhắc nhưng vẫn cần xét thêm.”"
                : $"{npcShortNameVi} dịu giọng: “Các hạ nói trúng chỗ thấu tình đạt lý, song quy củ vẫn khó bỏ ngay.”";
        }

        if (multiplier <= 0.6)
        {
            return $"{npcShortNameVi} khẽ lắc đầu: “Lý lẽ ấy chưa đủ sức thuyết phục ta đổi ý tại {cityNameVi}.”";
        }

        return DebateTopics.Normalize(topic) switch
        {
            DebateTopics.Recruit =>
                $"{npcShortNameVi} đáp: “Chọn người gửi gắm chí lớn là việc cả đời, tại hạ cần thấy rõ thành ý hơn nữa.”",
            DebateTopics.Tax =>
                $"{npcShortNameVi} đáp: “Việc quân lương và thành phòng {cityNameVi} đang cần chi dùng, giảm thuế không thể tùy tiện.”",
            _ =>
                $"{npcShortNameVi} đáp: “Hàng hóa tại chợ {cityNameVi} vốn đắt đỏ từ nguồn, hạ giá sâu quá sạp ta khó xoay sở.”"
        };
    }
}
