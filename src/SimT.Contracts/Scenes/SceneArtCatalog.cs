using SimT.Contracts.Playing;

namespace SimT.Contracts.Scenes;

/// <summary>
/// Resolves portrait, sprite, and background paths under <c>assets/scenes/art/</c>
/// matching <c>assets/scenes/art/manifest.json</c>.
/// </summary>
public static class SceneArtCatalog
{
    public const string PlayerPortraitFace = "art/portraits/player/face.webp";
    public const string PlayerPortraitFull = "art/portraits/player/full.webp";
    public const string PlayerSprite = "art/sprites/traveler.png";

    public static string ClassifyArchetype(NpcDef npc) =>
        ClassifyArchetype(npc.Code, npc.Role, npc.Attributes, npc.NameVi, npc.Personality);

    /// <summary>
    /// Single canonical archetype classifier shared by portrait/sprite resolution and dialogue role selection.
    /// Priority order:
    /// 1) Direct match by <paramref name="npcCode"/> in <c>manifest.npc</c>.
    /// 2) Keyword match in <paramref name="rawRole"/> / <paramref name="nameVi"/>.
    /// 3) Female character match (<c>lady</c> / <c>swordswoman</c>).
    /// 4) Attribute match (<c>War</c> high -> <c>general</c>, <c>Int</c>/<c>Pol</c> high -> <c>scholar</c>).
    /// 5) Fallback to <c>commoner</c>.
    /// </summary>
    public static string ClassifyArchetype(
        string? npcCode,
        string? rawRole,
        CharacterAttributes? attributes = null,
        string? nameVi = null,
        string? personality = null)
    {
        var code = (npcCode ?? string.Empty).Trim().ToLowerInvariant();
        switch (code)
        {
            case "npc_0001" or "npc_0002" or "npc_0003" or "npc_0022" or "npc_0057":
                return "general";
            case "npc_0025":
                return "scholar";
            case "npc_0140":
                return "lady";
        }

        var roleLower = (rawRole ?? string.Empty).Trim().ToLowerInvariant();
        var nameLower = (nameVi ?? string.Empty).Trim().ToLowerInvariant();
        var combined = string.IsNullOrEmpty(nameLower) ? roleLower : $"{roleLower} {nameLower}";

        if (!string.IsNullOrEmpty(roleLower))
        {
            switch (roleLower)
            {
                case "general" or "commander" or "warlord" or "warrior" or "guard" or "captain":
                    return "general";
                case "scholar" or "literati" or "strategist" or "advisor" or "official" or "governor" or "physician":
                    return "scholar";
                case "merchant" or "trader" or "shopkeeper" or "tavern" or "innkeeper":
                    return "merchant";
                case "elder" or "patriarch" or "monk" or "taoist":
                    return "elder";
                case "lady" or "noblewoman" or "dancer":
                    return "lady";
                case "swordswoman" or "heroine":
                    return "swordswoman";
                case "archer" or "bowman":
                    return "archer";
                case "rebel" or "bandit":
                    return "rebel";
                case "farmer_woman":
                    return "farmer_woman";
                case "commoner" or "farmer" or "peasant" or "villager":
                    return "commoner";
            }
        }

        if (!string.IsNullOrEmpty(combined))
        {
            if (ContainsAny(combined, "nữ kiệt", "nữ binh", "nữ hiệp", "hổ nữ", "chúc dung", "cung yêu cơ"))
                return "swordswoman";
            if (ContainsAny(combined, "mỹ nhân", "giai nhân", "tài nữ", "liệt nữ", "hoàng hậu", "phu nhân", "thứ thê", "mẫu thân", "trưởng nữ", "nữ nhi", "điêu thuyền", "đại kiều", "tiểu kiều", "liễu nương", "uyển nhi"))
                return "lady";

            if (ContainsAny(combined, "khăn vàng", "hoàng cân", "sơn tặc", "lục lâm", "phỉ trại"))
                return "rebel";

            if (ContainsAny(combined, "thương", "lão bản", "chưởng quỹ", "tiền trang", "cự phú", "tửu quán", "tiệm buôn"))
                return "merchant";

            var militaryCheck = combined
                .Replace("thừa tướng", " ")
                .Replace("lang cố chi tướng", " ")
                .Replace("quân sư", " ")
                .Replace("quân sự", " ")
                .Replace("giám quân", " ")
                .Replace("tham quân", " ")
                .Replace("quân nhu", " ")
                .Replace("quân kỷ", " ");

            if (ContainsAny(militaryCheck, "tướng", "soái", "đô đốc", "hiệu úy", "vạn nhân địch", "võ thánh", "chiến thần", "tiên phong", "kiêu kỵ", "kỵ binh", "dũng sĩ", "hộ vệ", "tiêu đầu", "thần xạ", "thần tiễn", "đại suất", "phương suất", "thủ lĩnh"))
                return "general";

            if (ContainsAny(combined, "sĩ", "mưu", "thái thú", "thừa tướng", "quân sư", "thứ sử", "châu mục", "thái sư", "tư đồ", "tư không", "thái úy", "thượng thư", "thị lang", "biệt giá", "tòng sự", "quang lộc", "trọng thần", "danh thần", "văn thần", "thông nho", "tông sư", "tiên sinh", "thần y", "danh y", "dược sư", "lang cố", "điển luận", "cửu phẩm"))
                return "scholar";

            if (ContainsAny(militaryCheck, "quân"))
                return "general";

            if (ContainsAny(combined, "trưởng lão", "chân tiên", "đạo sư", "đạo nhân"))
                return "elder";
        }

        if (attributes is not null)
        {
            if (attributes.War >= 75 ||
                (attributes.War >= 65 && attributes.War >= attributes.Int && attributes.War >= attributes.Pol && attributes.War >= attributes.Com))
            {
                return "general";
            }

            if (attributes.Int >= 75 || attributes.Pol >= 75 ||
                (attributes.Int >= 65 && attributes.Int >= attributes.War))
            {
                return "scholar";
            }

            if (attributes.Com >= 75)
            {
                return "merchant";
            }
        }

        if (string.Equals(personality, "SCHOLARLY", StringComparison.OrdinalIgnoreCase))
            return "scholar";
        if (string.Equals(personality, "VALIANT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(personality, "FIERCE", StringComparison.OrdinalIgnoreCase))
            return "general";

        return "commoner";
    }

    public static (string PortraitFull, string PortraitFace, string SpritePath) ResolveNpcArt(NpcDef npc) =>
        ResolveNpcArt(npc.Code, npc.Role, npc.Attributes, npc.NameVi, npc.Personality);

    public static (string PortraitFull, string PortraitFace, string SpritePath) ResolveNpcArt(
        string? npcCode,
        string? rawRole,
        CharacterAttributes? attributes = null,
        string? nameVi = null,
        string? personality = null)
    {
        var (portraitKey, spriteKey) = (npcCode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "npc_0001" => ("liu_bei", "liu_bei"),
            "npc_0002" => ("zhang_fei", "zhang_fei"),
            "npc_0003" => ("guan_yu", "guan_yu"),
            "npc_0022" => ("cao_cao", "cao_cao"),
            "npc_0025" => ("zhuge_liang", "zhuge_liang"),
            "npc_0057" => ("lu_bu", "lu_bu"),
            "npc_0140" => ("lady", "civ_lady"),
            _ => ResolveArchetypeKeys(ClassifyArchetype(npcCode, rawRole, attributes, nameVi, personality))
        };

        return (
            $"art/portraits/{portraitKey}/full.webp",
            $"art/portraits/{portraitKey}/face.webp",
            $"art/sprites/{spriteKey}.png");
    }

    public static (string PortraitKey, string SpriteKey) ResolveArchetypeKeys(string? rawRoleOrArchetype)
    {
        var archetype = ClassifyArchetype(npcCode: null, rawRole: rawRoleOrArchetype);
        return archetype switch
        {
            "general" => ("general", "han_officer"),
            "scholar" => ("scholar", "civ_scholar"),
            "merchant" => ("merchant", "civ_merchant"),
            "elder" => ("elder", "civ_elder"),
            "lady" => ("lady", "civ_lady"),
            "swordswoman" => ("swordswoman", "female_warrior"),
            "archer" => ("general", "han_archer"),
            "rebel" => ("commoner", "yellow_turban"),
            "farmer_woman" => ("farmer_woman", "civ_farmer_woman"),
            _ => ("commoner", "civ_commoner")
        };
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            if (text.Contains(kw, StringComparison.Ordinal))
                return true;
        }
        return false;
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
