using SimT.Contracts.Playing;

namespace SimT.Contracts.Scenes;

/// <summary>
/// Interactive choice button displayed inside a WebView2 scene (`talk`, `duel`, `city`).
/// LabelVi must not exceed 40 characters.
/// </summary>
public sealed record SceneChoice(
    string Id,
    string LabelVi,
    string? HintVi = null,
    bool Enabled = true,
    string? DisabledReasonVi = null);

/// <summary>
/// Nearby NPC thumbnail shown in the top-right switcher of a 2.5D dialogue scene.
/// </summary>
public sealed record SceneNpcThumb(
    long NpcId,
    string NameVi,
    string FacePath,
    bool Active);

/// <summary>
/// Complete view-model sent from C# to WebView2 for the 2.5D dialogue scene.
/// Every line in <see cref="Lines"/> must be at most 90 characters.
/// </summary>
public sealed record TalkScene(
    string SceneId,
    long NpcId,
    string NpcCode,
    string NameVi,
    string? NameHan,
    string? StyleNameVi,
    string RoleVi,
    string? PersonalityVi,
    string LocationNameVi,
    string LocationHan,
    int Affinity,
    string RelationTierVi,
    string PortraitFull,
    string PortraitFace,
    string BackgroundPath,
    IReadOnlyList<string> Lines,
    IReadOnlyList<SceneChoice> Choices,
    IReadOnlyList<SceneNpcThumb> NearbyNpcs,
    string? FeedbackVi = null);

/// <summary>
/// Fighter snapshot rendered on the pixel duel stage.
/// </summary>
public sealed record DuelFighterView(
    string NameVi,
    string SubVi,
    string FacePath,
    string SpritePath,
    TroopBranch Branch,
    int Hp,
    int MaxHp,
    int Spirit,
    int MaxSpirit);

/// <summary>
/// Complete view-model sent from C# to WebView2 for the pixel-art duel scene.
/// When <see cref="Result"/> is null, the scene is in pre-duel troop branch selection mode.
/// </summary>
public sealed record DuelScene(
    string SceneId,
    long NpcId,
    string NpcCode,
    string TitleVi,
    string CitadelNameVi,
    string BackgroundPath,
    DuelFighterView Player,
    DuelFighterView Opponent,
    string MatchupSummaryVi,
    DuelResult? Result,
    IReadOnlyList<SceneChoice> Choices);

/// <summary>
/// Clickable architectural / district hotspot on the "Trong thành" city scene.
/// </summary>
public sealed record CityHotspot(
    string Id,
    string LabelVi,
    string Kind,
    string TargetScreenId,
    double XPercent,
    double YPercent);

/// <summary>
/// Standing NPC pixel sprite on the "Trong thành" city scene (maximum 6).
/// </summary>
public sealed record CityNpcSprite(
    long NpcId,
    string Code,
    string NameVi,
    string RoleVi,
    string SpritePath,
    string FacePath,
    double XPercent,
    double YPercent);

/// <summary>
/// Mini scene event card shown inside the "Trong thành" scene after "Du ngoạn phố".
/// </summary>
public sealed record CityEventCard(
    string TitleVi,
    string LocationVi,
    string NarrativeVi,
    string RewardVi);

/// <summary>
/// Complete view-model sent from C# to WebView2 for the "Trong thành" hub scene.
/// </summary>
public sealed record CityScene(
    string SceneId,
    long CityId,
    string CityNameVi,
    string CityNameHan,
    string RegionVi,
    string DateVi,
    string Season,
    string WeatherEffect,
    string BackgroundPath,
    IReadOnlyList<CityHotspot> Hotspots,
    IReadOnlyList<CityNpcSprite> Npcs,
    CityEventCard? EventCard,
    IReadOnlyList<SceneChoice> Choices);

/// <summary>
/// Inbound JSON message from WebView2 JavaScript to C#.
/// Only <c>ready</c>, <c>choice</c>, <c>navigate</c>, and <c>close</c> are accepted.
/// </summary>
public sealed record SceneClientMessage(
    string Type,
    string? SceneId = null,
    string? Id = null);

/// <summary>
/// Outcome of executing a player choice inside <see cref="TalkScene"/>.
/// </summary>
public sealed record TalkChoiceOutcome(
    bool Ok,
    string MessageVi,
    bool OpenDuel = false,
    bool CloseScene = false,
    TalkScene? UpdatedScene = null);

/// <summary>
/// Capability exposed by <c>dlc.dialogue</c> (<see cref="CapabilityIds.Dialogue"/>).
/// </summary>
public interface IDialogueModule
{
    TalkScene BuildTalkScene(long npcId, string? feedbackVi = null);

    TalkChoiceOutcome HandleChoice(long npcId, string choiceId);

    IReadOnlyList<string> GetLinesForNpc(long npcId, string relationTierKey);
}

/// <summary>
/// Deterministic Vietnamese localization for raw NPC role codes stored in SQLite.
/// </summary>
public static class RoleTitleVi
{
    public static string Format(string? rawRole)
    {
        if (string.IsNullOrWhiteSpace(rawRole))
            return "Bách tính";

        return rawRole.Trim().ToLowerInvariant() switch
        {
            "merchant" or "trader" or "shopkeeper" => "Thương nhân",
            "tavern" or "innkeeper" => "Chưởng quỹ quán trọ",
            "general" or "commander" or "warlord" or "warrior" => "Võ tướng",
            "scholar" or "literati" or "strategist" or "advisor" => "Văn sĩ",
            "farmer" or "commoner" or "peasant" or "villager" => "Bách tính",
            "elder" or "patriarch" => "Trưởng lão",
            "lady" or "noblewoman" or "dancer" => "Giai nhân",
            "swordswoman" or "heroine" => "Nữ hiệp",
            "archer" or "bowman" => "Cung thủ",
            "rebel" or "bandit" => "Nghĩa quân",
            "official" or "governor" or "magistrate" => "Quan lại",
            "blacksmith" or "craftsman" or "artisan" => "Thợ rèn",
            "guard" or "captain" => "Thị vệ",
            "physician" or "healer" => "Y sư",
            "monk" or "taoist" => "Đạo nhân",
            _ => rawRole.Trim()
        };
    }

    public static string ToArchetypeKey(string? rawRole) =>
        SceneArtCatalog.ClassifyArchetype(npcCode: null, rawRole: rawRole);

    public static string FormatPersonality(string? rawPersonality)
    {
        if (string.IsNullOrWhiteSpace(rawPersonality))
            return "Trầm ổn";

        var trimmed = rawPersonality.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "VALIANT" => "Dũng mãnh",
            "SCHOLARLY" => "Nho nhã",
            "PRAGMATIC" => "Thực tế",
            "BENEVOLENT" => "Nhân đức",
            "HONORABLE" => "Nghĩa khí",
            "FIERCE" => "Mãnh liệt",
            "CAUTIOUS" => "Thận trọng",
            "AMBITIOUS" => "Hùng tâm",
            "DILIGENT" => "Cần mẫn",
            "CUNNING" => "Thâm mưu",
            "LOYAL" => "Trung nghĩa",
            _ => IsAllAsciiUpper(trimmed) ? "Trầm ổn" : trimmed
        };
    }

    public static string FormatRegion(string? rawRegion)
    {
        if (string.IsNullOrWhiteSpace(rawRegion))
            return "Trung Nguyên";

        var trimmed = rawRegion.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "trung_nguyen" => "Trung Nguyên",
            "quan_trung" => "Quan Trung",
            "ha_bac" => "Hà Bắc",
            "u_chau" => "U Châu",
            "ky_chau" => "Ký Châu",
            "thanh_chau" => "Thanh Châu",
            "du_chau" => "Dự Châu",
            "tu_chau" => "Từ Châu",
            "kinh_chau" => "Kinh Châu",
            "giang_dong" => "Giang Đông",
            "duong_chau" => "Dương Châu",
            "ich_chau" => "Ích Châu",
            "luong_chau" or "tay_luong" => "Tây Lương",
            "ung_chau" => "Ung Châu",
            "giao_chau" => "Giao Châu",
            "nam_trung" => "Nam Trung",
            "quang_chau" => "Quảng Châu",
            "duyen_chau" => "Duyên Châu",
            "tinh_chau" => "Tịnh Châu",
            _ => trimmed.Contains('_') ? trimmed.Replace('_', ' ') : trimmed
        };
    }

    public static string FormatItemCategory(string? rawCategory)
    {
        if (string.IsNullOrWhiteSpace(rawCategory))
            return "Hàng hóa";

        return rawCategory.Trim().ToLowerInvariant() switch
        {
            "grain" or "luong_thuc" => "Lương thực & Nhu yếu",
            "food" or "thuc_pham" => "Thực phẩm & Đồ uống",
            "luxury" or "xa_xi" => "Thượng phẩm & Xa xỉ",
            "textile" or "to_lua" => "Tơ lụa & Vải vóc",
            "medicine" or "duoc_lieu" => "Dược liệu & Thuốc men",
            "material" or "metal" or "khoang_san" => "Khoáng sản & Vật liệu",
            "seed" or "vat_tu" => "Hạt giống & Cây trồng",
            "farming" or "supply" or "nong_nghiep" => "Vật tư Nông nghiệp",
            "tool" or "nong_cu" => "Nông cụ & Khí cụ",
            "weapon" or "binh_khi" => "Binh khí & Quân trang",
            "book" or "thu_tich" => "Thư tịch & Bí kíp",
            "all" => "Tất cả kỳ vật",
            _ => rawCategory.Trim()
        };
    }

    private static bool IsAllAsciiUpper(string text)
    {
        foreach (var ch in text)
        {
            if (ch is >= 'a' and <= 'z' || ch > 127)
                return false;
        }
        return true;
    }
}

/// <summary>
/// Enforces the WebView2-to-C# trust boundary: only <c>ready</c>, <c>choice</c>,
/// <c>navigate</c>, and <c>close</c> are accepted, and every <c>id</c> is checked against
/// the currently active scene before C# acts on it.
/// </summary>
public static class SceneTrustGuard
{
    public static bool IsAllowedMessage(SceneClientMessage? message, object? activeScene)
    {
        if (message is null || string.IsNullOrWhiteSpace(message.Type))
            return false;

        var type = message.Type.Trim().ToLowerInvariant();
        if (type is "ready" or "close")
            return true;

        if (activeScene is null || string.IsNullOrWhiteSpace(message.Id))
            return false;

        var id = message.Id.Trim();
        return type switch
        {
            "choice" => IsValidChoice(id, activeScene),
            "navigate" => IsValidNavigation(id, activeScene),
            _ => false
        };
    }

    private static bool IsValidChoice(string id, object activeScene) => activeScene switch
    {
        TalkScene talk => talk.Choices.Any(c => c.Enabled && string.Equals(c.Id, id, StringComparison.Ordinal)),
        DuelScene duel => duel.Choices.Any(c => c.Enabled && string.Equals(c.Id, id, StringComparison.Ordinal)),
        CityScene city => city.Choices.Any(c => c.Enabled && string.Equals(c.Id, id, StringComparison.Ordinal)),
        _ => false
    };

    private static bool IsValidNavigation(string id, object activeScene) => activeScene switch
    {
        TalkScene talk => talk.NearbyNpcs.Any(n => string.Equals($"npc:{n.NpcId}", id, StringComparison.Ordinal)),
        CityScene city => city.Hotspots.Any(h =>
                              string.Equals(h.Id, id, StringComparison.Ordinal) ||
                              string.Equals(h.TargetScreenId, id, StringComparison.Ordinal))
                          || city.Npcs.Any(n => string.Equals($"npc:{n.NpcId}", id, StringComparison.Ordinal)),
        _ => false
    };
}
