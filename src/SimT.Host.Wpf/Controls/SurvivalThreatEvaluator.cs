using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

public enum SurvivalThreatKind
{
    None = 0,
    CriticalHp = 1,
    SevereFatigue = 2,
    SevereStress = 3,
    UnsafeTerritory = 4,
    WarlordExtortion = 5,
    RetainerBetrayalRisk = 6
}

public sealed record SurvivalThreatAssessment(
    SurvivalThreatKind Kind,
    string BadgeText,
    string Message,
    string ActionLabel,
    string ActionCommand,
    string BorderColorHex,
    string BackgroundColorHex)
{
    public static readonly SurvivalThreatAssessment Safe = new(
        SurvivalThreatKind.None,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        "#00000000",
        "#00000000");
}

/// <summary>
/// Pure evaluation logic for protagonist survival alarms.
/// Prioritizes physical danger (HP), physiological breakdown (Fatigue),
/// cognitive overload (Stress), and environmental danger (Citadel Danger Level).
/// </summary>
public static class SurvivalThreatEvaluator
{
    public static SurvivalThreatAssessment Evaluate(
        CharacterVitals? vitals,
        int dangerLevel = 1,
        bool isAtWar = false,
        bool hasWarlordExtortion = false,
        bool hasRetainerBetrayalRisk = false)
    {
        if (vitals is not null)
        {
            // 1. Critical HP (HP <= 30% of MaxHP)
            if (vitals.MaxHp > 0 && (double)vitals.Health / vitals.MaxHp <= 0.30)
            {
                return new SurvivalThreatAssessment(
                    SurvivalThreatKind.CriticalHp,
                    "🩸 NGUY CẤP",
                    $"Sinh mệnh tổn hao nghiêm trọng ({vitals.Health}/{vitals.MaxHp})! Nguy cơ tử vong nếu tiếp tục bôn ba — cần tĩnh dưỡng hoặc tìm y quán.",
                    "Tĩnh Dưỡng (R)",
                    "rest",
                    "#EF4444",
                    "#3B1414");
            }

            // 2. Severe Fatigue (Fatigue >= 80% or Stamina <= 10%)
            if (vitals.Fatigue >= 80 || (vitals.MaxStamina > 0 && (double)vitals.CurrentStamina / vitals.MaxStamina <= 0.10))
            {
                return new SurvivalThreatAssessment(
                    SurvivalThreatKind.SevereFatigue,
                    "💤 KIỆT SỨC",
                    $"Thể xác rã rời, độ mệt mỏi đạt {vitals.Fatigue}%! Tốc độ di chuyển giảm, dễ ngất xỉu khi du ngoạn hoặc gặp đạo tặc.",
                    "Nghỉ Ngơi Ngay (R)",
                    "rest",
                    "#F59E0B",
                    "#331F0B");
            }

            // 3. Severe Stress (Stress >= 80%)
            if (vitals.Stress >= 80)
            {
                return new SurvivalThreatAssessment(
                    SurvivalThreatKind.SevereStress,
                    "🌪️ TÂM THẦN",
                    $"Tâm thần u uất do áp lực đè nặng ({vitals.Stress}%)! Trí lực suy giảm mạnh, cần tịnh tâm hoặc vào tửu quán đàm đạo.",
                    "Tịnh Tâm Nghỉ Ngơi (R)",
                    "rest",
                    "#EC4899",
                    "#2B162E");
            }
        }

        // 4. Warlord Extortion / Scrutiny Demand (Gói 1)
        if (hasWarlordExtortion)
        {
            return new SurvivalThreatAssessment(
                SurvivalThreatKind.WarlordExtortion,
                "📜 TRÁT QUÂN PHIỆT",
                "Quân phiệt sở tại đang gửi trát bức bách đòi quân phí hoặc rình rập tịch biên tài sản! Cần vào Quan Trường đối phó ngay.",
                "Vào Quan Trường",
                "politics.court",
                "#DC2626",
                "#2D1215");
        }

        // 5. Retainer Betrayal / Embezzlement Risk (Gói 3)
        if (hasRetainerBetrayalRisk)
        {
            return new SurvivalThreatAssessment(
                SurvivalThreatKind.RetainerBetrayalRisk,
                "🐍 DÃ TÂM DỊ CHÍ",
                "Có gia thần dã tâm cao đang bất mãn lòng trung, nguy cơ tham ô công quỹ hoặc phản chủ đào tẩu! Cần thẩm tra hoặc trấn an.",
                "Thẩm Tra Gia Thần",
                "social.network",
                "#EAB308",
                "#2A220E");
        }

        // 6. Territory Danger Level (Level >= 3 or At War)
        if (isAtWar || dangerLevel >= 3)
        {
            var secDesc = isAtWar ? "Chiến sự khốc liệt bùng nổ" : "Vùng biên ải sa trường hiểm trở (Độ hiểm nguy cấp 3)";
            return new SurvivalThreatAssessment(
                SurvivalThreatKind.UnsafeTerritory,
                "⚔️ HIỂM ĐỊA",
                $"{secDesc}! Nguy cơ tao loạn và thổ phỉ phục kích cao — đề phòng khi xuất hành đường trường.",
                "Xem Bản Đồ (M)",
                "map",
                "#D97706",
                "#261E1A");
        }

        return SurvivalThreatAssessment.Safe;
    }
}
