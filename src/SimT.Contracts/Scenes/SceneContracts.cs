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

    public static string ToArchetypeKey(string? rawRole)
    {
        if (string.IsNullOrWhiteSpace(rawRole))
            return "commoner";

        return rawRole.Trim().ToLowerInvariant() switch
        {
            "general" or "commander" or "warlord" or "warrior" or "guard" or "captain" => "general",
            "scholar" or "literati" or "strategist" or "advisor" or "official" or "governor" or "physician" => "scholar",
            "merchant" or "trader" or "shopkeeper" or "tavern" or "innkeeper" => "merchant",
            "elder" or "patriarch" or "monk" or "taoist" => "elder",
            "lady" or "noblewoman" or "dancer" => "lady",
            "swordswoman" or "heroine" => "swordswoman",
            "archer" or "bowman" => "archer",
            "rebel" or "bandit" => "rebel",
            _ => "commoner"
        };
    }
}

/// <summary>
/// Resolves portrait, sprite, and background paths under <c>assets/scenes/art/</c>
/// matching <c>assets/scenes/art/manifest.json</c>.
/// </summary>
public static class SceneArtCatalog
{
    public const string PlayerPortraitFace = "art/portraits/player/face.webp";
    public const string PlayerPortraitFull = "art/portraits/player/full.webp";
    public const string PlayerSprite = "art/sprites/traveler.png";

    public static (string PortraitFull, string PortraitFace, string SpritePath) ResolveNpcArt(string? npcCode, string? rawRole)
    {
        var (portraitKey, spriteKey) = (npcCode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "npc_0001" => ("liu_bei", "liu_bei"),
            "npc_0002" => ("zhang_fei", "zhang_fei"),
            "npc_0003" => ("guan_yu", "guan_yu"),
            "npc_0022" => ("cao_cao", "cao_cao"),
            "npc_0025" => ("zhuge_liang", "zhuge_liang"),
            "npc_0057" => ("lu_bu", "lu_bu"),
            "npc_0140" => ("lady", "female_warrior"),
            _ => ResolveArchetypeKeys(rawRole)
        };

        return (
            $"art/portraits/{portraitKey}/full.webp",
            $"art/portraits/{portraitKey}/face.webp",
            $"art/sprites/{spriteKey}.png");
    }

    public static (string PortraitKey, string SpriteKey) ResolveArchetypeKeys(string? rawRole)
    {
        var archetype = RoleTitleVi.ToArchetypeKey(rawRole);
        return archetype switch
        {
            "general" => ("general", "han_officer"),
            "scholar" => ("scholar", "zhuge_liang"),
            "merchant" => ("merchant", "traveler"),
            "elder" => ("elder", "zhuge_liang"),
            "lady" => ("lady", "female_warrior"),
            "swordswoman" => ("swordswoman", "female_warrior"),
            "archer" => ("general", "han_archer"),
            "rebel" => ("general", "yellow_turban"),
            _ => ("commoner", "han_infantry")
        };
    }

    public static string BackgroundForKey(string key) => key.Trim().ToLowerInvariant() switch
    {
        "market" => "art/backgrounds/market.png",
        "arena" => "art/backgrounds/arena.png",
        "gate" => "art/backgrounds/gate.png",
        "hall" => "art/backgrounds/hall.png",
        "field" => "art/backgrounds/field.png",
        _ => "art/backgrounds/street.png"
    };
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
