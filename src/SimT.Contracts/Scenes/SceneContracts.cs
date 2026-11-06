using SimT.Contracts.Playing;

namespace SimT.Contracts.Scenes;

public sealed record SceneChoice(string Id, string LabelVi, string? HintVi = null, bool Enabled = true, string? DisabledReasonVi = null);

public sealed record SceneNpcThumb(long NpcId, string NameVi, string FacePath, bool Active);

public sealed record TalkScene(
    string SceneId, long NpcId, string NpcCode, string NameVi, string? NameHan, string? StyleNameVi,
    string RoleVi, string? PersonalityVi, string LocationNameVi, string LocationHan, int Affinity, string RelationTierVi,
    string PortraitFull, string PortraitFace, string BackgroundPath, IReadOnlyList<string> Lines,
    IReadOnlyList<SceneChoice> Choices, IReadOnlyList<SceneNpcThumb> NearbyNpcs,
    string? FeedbackVi = null, string? BlinkPath = null, string? TalkPath = null);

public sealed record DuelFighterView(
    string NameVi, string SubVi, string FacePath, string SpritePath, TroopBranch Branch,
    int Hp, int MaxHp, int Spirit, int MaxSpirit, string? AnimPath = null, string? AnimKey = null);

public sealed record DuelScene(
    string SceneId, long NpcId, string NpcCode, string TitleVi, string CitadelNameVi,
    string BackgroundPath, DuelFighterView Player, DuelFighterView Opponent,
    string MatchupSummaryVi, DuelResult? Result, IReadOnlyList<SceneChoice> Choices);

public sealed record CityHotspot(
    string Id, string LabelVi, string Kind, string TargetScreenId, double XPercent, double YPercent);

public sealed record CityNpcSprite(
    long NpcId, string Code, string NameVi, string RoleVi, string SpritePath, string FacePath,
    double XPercent, double YPercent, string? AnimPath = null);

public sealed record CityEventCard(string TitleVi, string LocationVi, string NarrativeVi, string RewardVi);

public sealed record CityScene(
    string SceneId, long CityId, string CityNameVi, string CityNameHan, string RegionVi,
    string DateVi, string Season, string WeatherEffect, string BackgroundPath,
    IReadOnlyList<CityHotspot> Hotspots, IReadOnlyList<CityNpcSprite> Npcs,
    CityEventCard? EventCard, IReadOnlyList<SceneChoice> Choices);

public sealed record SceneClientMessage(string Type, string? SceneId = null, string? Id = null);

public sealed record TalkChoiceOutcome(
    bool Ok, string MessageVi, bool OpenDuel = false, bool CloseScene = false, TalkScene? UpdatedScene = null);

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
            "guard" or "captain" or "bodyguard" => "Hộ vệ",
            "manager" => "Chưởng quầy",
            "accountant" => "Kế toán",
            "physician" or "healer" => "Y sư",
            "monk" or "taoist" => "Đạo nhân",
            _ => rawRole.Trim()
        };
    }

    public static string ShortName(string? nameVi)
    {
        if (string.IsNullOrWhiteSpace(nameVi)) return "Nhân sĩ";
        var idx = nameVi.IndexOf('(');
        return (idx > 0 ? nameVi[..idx] : nameVi).Trim();
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
        MapScene map => IsValidMapChoice(id, map),
        SocialScene social => IsValidSocialChoice(id, social),
        MarketScene market => IsValidMarketChoice(id, market),
        DossierScene dossier => IsValidDossierChoice(id, dossier),
        _ => false
    };

    private static bool IsValidNavigation(string id, object activeScene) => activeScene switch
    {
        TalkScene talk => talk.NearbyNpcs.Any(n => string.Equals($"npc:{n.NpcId}", id, StringComparison.Ordinal)),
        CityScene city => city.Hotspots.Any(h =>
                              string.Equals(h.Id, id, StringComparison.Ordinal) ||
                              string.Equals(h.TargetScreenId, id, StringComparison.Ordinal))
                          || city.Npcs.Any(n => string.Equals($"npc:{n.NpcId}", id, StringComparison.Ordinal)),
        SocialScene social => social.Nodes.Any(n => string.Equals($"npc:{n.NpcId}", id, StringComparison.Ordinal)),
        DossierScene dossier => dossier.Npcs.Any(n => string.Equals($"npc:{n.Id}", id, StringComparison.Ordinal)),
        _ => false
    };

    private static bool IsValidMapChoice(string id, MapScene map)
    {
        if (!id.StartsWith("travel:", StringComparison.Ordinal))
            return false;
        var parts = id.Split(':');
        if (parts.Length < 2 || !long.TryParse(parts[1], out var cityId))
            return false;
        var mode = parts.Length >= 3 ? parts[2] : "walk";
        return mode is "walk" or "horse" or "caravan" &&
               cityId != map.CurrentCityId &&
               map.Cities.Any(c => c.Id == cityId);
    }

    private static bool IsValidSocialChoice(string id, SocialScene social)
    {
        if (id == "host_banquet")
            return true;
        var parts = id.Split(':');
        if (parts.Length == 2 && long.TryParse(parts[1], out var targetId))
        {
            return parts[0] switch
            {
                "chat" or "gift" or "recruit" or "swear" or "dismiss" =>
                    social.Nodes.Any(n => n.NpcId == targetId) || social.Retainers.Any(r => r.NpcId == targetId),
                _ => false
            };
        }
        if (parts.Length == 3 && parts[0] == "gift" &&
            long.TryParse(parts[1], out var giftNpcId) && long.TryParse(parts[2], out _))
        {
            return social.Nodes.Any(n => n.NpcId == giftNpcId);
        }
        if (parts.Length == 3 && parts[0] == "learn" &&
            long.TryParse(parts[1], out var teacherId) && !string.IsNullOrWhiteSpace(parts[2]))
        {
            return social.Nodes.Any(n => n.NpcId == teacherId) ||
                   social.Skills.Any(s => s.TeacherNpcId == teacherId && string.Equals(s.SkillCode, parts[2], StringComparison.OrdinalIgnoreCase));
        }
        if (parts.Length == 4 && parts[0] == "skill" && parts[1] == "equip" &&
            int.TryParse(parts[3], out var slot) && slot is >= 1 and <= 6)
        {
            return social.Skills.Any(s => string.Equals(s.SkillCode, parts[2], StringComparison.OrdinalIgnoreCase) && s.Learned);
        }
        if (parts.Length == 3 && parts[0] == "skill" && parts[1] == "unequip" &&
            int.TryParse(parts[2], out var unequipSlot) && unequipSlot is >= 1 and <= 6)
        {
            return true;
        }
        if (parts.Length == 3 && parts[0] == "letter" &&
            parts[1] is ("read" or "claim") && long.TryParse(parts[2], out var letterId))
        {
            return social.Letters.Any(l => l.Id == letterId);
        }
        return false;
    }

    private static bool IsValidMarketChoice(string id, MarketScene market)
    {
        var parts = id.Split(':');
        if (parts.Length != 3)
            return false;
        if (parts[0] is not ("buy" or "sell"))
            return false;
        if (!long.TryParse(parts[1], out var itemId) || !int.TryParse(parts[2], out var qty) || qty <= 0)
            return false;
        return market.Items.Any(i => i.ItemId == itemId);
    }

    private static bool IsValidDossierChoice(string id, DossierScene dossier)
    {
        var parts = id.Split(':');
        if (parts.Length != 2 || !long.TryParse(parts[1], out var npcId))
            return false;
        return parts[0] is "visit" or "spar" and var _ && dossier.Npcs.Any(n => n.Id == npcId);
    }
}
