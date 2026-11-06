using System.Globalization;
using System.IO;
using System.Text.Json;
using SimT.Contracts.Playing;
using SimT.Contracts.Ui;

namespace SimT.Contracts.Scenes;

public sealed record RowNpcMetadata(
    string NameVi,
    string RoleVi,
    string? SubtitleVi,
    string? NpcCode,
    string Archetype,
    string PortraitFull,
    string PortraitFace,
    string SpritePath);

/// <summary>
/// Single canonical classifier and art path resolver shared by WebView2 scenes and WPF controls
/// (<c>GamePortraitCard</c>, <c>GameTable</c>), backed by <c>assets/scenes/art/manifest.json</c>.
/// </summary>
public static class SceneArtCatalog
{
    public const string PlayerPortraitFace = "art/portraits/player/face.webp";
    public const string PlayerPortraitFull = "art/portraits/player/full.webp";
    public const string PlayerSprite = "art/sprites/traveler.png";

    private static readonly Lazy<ManifestTable> Manifest = new(LoadManifest, isThreadSafe: true);

    public static bool IsManifestLoadedFromDisk => Manifest.Value.LoadedFromDisk;

    public static string ClassifyArchetype(NpcDef npc) =>
        ClassifyArchetype(npc.Code, npc.Role, npc.Attributes, npc.NameVi, npc.Personality);

    public static string ClassifyArchetype(
        string? npcCode,
        string? rawRole,
        CharacterAttributes? attributes = null,
        string? nameVi = null,
        string? personality = null)
    {
        var code = NormalizeNpcCode(npcCode, nameVi);
        switch (code)
        {
            case "npc_0001" or "npc_0002" or "npc_0003" or "npc_0022" or "npc_0057": return "general";
            case "npc_0025": return "scholar";
            case "npc_0140": return "lady";
        }

        var roleLower = (rawRole ?? string.Empty).Trim().ToLowerInvariant();
        var nameLower = (nameVi ?? string.Empty).Trim().ToLowerInvariant();
        var combined = string.IsNullOrEmpty(nameLower) ? roleLower : $"{roleLower} {nameLower}";

        if (!string.IsNullOrEmpty(roleLower))
        {
            switch (roleLower)
            {
                case "general" or "commander" or "warlord" or "warrior" or "guard" or "captain": return "general";
                case "scholar" or "literati" or "strategist" or "advisor" or "official" or "governor" or "physician": return "scholar";
                case "merchant" or "trader" or "shopkeeper" or "tavern" or "innkeeper": return "merchant";
                case "elder" or "patriarch" or "monk" or "taoist": return "elder";
                case "lady" or "noblewoman" or "dancer": return "lady";
                case "swordswoman" or "heroine": return "swordswoman";
                case "archer" or "bowman": return "archer";
                case "rebel" or "bandit": return "rebel";
                case "farmer_woman": return "farmer_woman";
                case "commoner" or "farmer" or "peasant" or "villager": return "commoner";
            }
        }

        if (!string.IsNullOrEmpty(combined))
        {
            if (ContainsAny(combined, "nữ kiệt", "nữ binh", "nữ hiệp", "hổ nữ", "chúc dung", "cung yêu cơ", "mã vân lộc"))
                return "swordswoman";
            if (ContainsAny(combined, "mỹ nhân", "giai nhân", "tài nữ", "liệt nữ", "hoàng hậu", "phu nhân", "thứ thê", "mẫu thân", "trưởng nữ", "nữ nhi", "điêu thuyền", "đại kiều", "tiểu kiều", "liễu nương", "uyển nhi", "thái diễm", "thái văn cơ", "hồ gia thập bát phách", "chân mật", "chân cơ", "hoàng nguyệt anh", "trương xuân hoa", "bộ luyện sư", "vương dị", "ngô quốc thái"))
                return "lady";
            if (ContainsAny(combined, "khăn vàng", "hoàng cân", "sơn tặc", "lục lâm", "phỉ trại"))
                return "rebel";
            if (ContainsAny(combined, "thương", "lão bản", "chưởng quỹ", "tiền trang", "cự phú", "tửu quán", "tiệm buôn", "chủ quán"))
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

            if (ContainsAny(militaryCheck, "tướng", "soái", "đô đốc", "hiệu úy", "vạn nhân địch", "võ thánh", "chiến thần", "tiên phong", "kiêu kỵ", "kỵ binh", "dũng sĩ", "hộ vệ", "tiêu đầu", "thần xạ", "thần tiễn", "đại suất", "phương suất", "thủ lĩnh", "trảm tướng", "hoa hùng", "dũng quán tam quân"))
                return "general";
            if (ContainsAny(combined, "sĩ", "mưu", "thái thú", "thừa tướng", "quân sư", "thứ sử", "châu mục", "thái sư", "tư đồ", "tư không", "thái úy", "thượng thư", "thị lang", "biệt giá", "tòng sự", "quang lộc", "trọng thần", "danh thần", "văn thần", "thông nho", "tông sư", "tiên sinh", "thần y", "danh y", "dược sư", "lang cố", "điển luận", "cửu phẩm", "tấn cao tổ", "tấn cảnh đế", "tấn văn đế", "ngụy văn đế", "tư mã ý", "trọng đạt"))
                return "scholar";
            if (ContainsAny(militaryCheck, "quân"))
                return "general";
            if (ContainsAny(combined, "trưởng lão", "chân tiên", "đạo sư", "đạo nhân", "lò đúc", "lão xa phu", "thợ rèn", "thợ mộc", "thợ cả"))
                return "elder";
        }

        if (attributes is not null)
        {
            if ((attributes.War >= 75 && attributes.War >= attributes.Int) ||
                (attributes.War >= 65 && attributes.War >= attributes.Int && attributes.War >= attributes.Pol && attributes.War >= attributes.Com))
                return "general";
            if (attributes.Int >= 75 || attributes.Pol >= 75 || (attributes.Int >= 65 && attributes.Int >= attributes.War))
                return "scholar";
            if (attributes.Com >= 75)
                return "merchant";
        }

        if (string.Equals(personality, "SCHOLARLY", StringComparison.OrdinalIgnoreCase)) return "scholar";
        if (string.Equals(personality, "VALIANT", StringComparison.OrdinalIgnoreCase) || string.Equals(personality, "FIERCE", StringComparison.OrdinalIgnoreCase)) return "general";
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
        var manifest = Manifest.Value;
        var normCode = NormalizeNpcCode(npcCode, nameVi);
        if (!string.IsNullOrEmpty(normCode) && manifest.Npc.TryGetValue(normCode, out var direct))
            return ($"art/portraits/{direct.Portrait}/full.webp", $"art/portraits/{direct.Portrait}/face.webp", $"art/sprites/{direct.Sprite}.png");

        var archetype = ClassifyArchetype(normCode, rawRole, attributes, nameVi, personality);
        var (portraitKey, spriteKey) = ResolveArchetypeKeys(archetype);
        return ($"art/portraits/{portraitKey}/full.webp", $"art/portraits/{portraitKey}/face.webp", $"art/sprites/{spriteKey}.png");
    }

    public static (string PortraitKey, string SpriteKey) ResolveArchetypeKeys(string? rawRoleOrArchetype)
    {
        var manifest = Manifest.Value;
        var archetype = ClassifyArchetype(npcCode: null, rawRole: rawRoleOrArchetype);
        if (manifest.Archetype.TryGetValue(archetype, out var entry)) return entry;
        return manifest.Archetype.TryGetValue("commoner", out var fallback) ? fallback : ("commoner", "civ_commoner");
    }

    public static (string PortraitFull, string PortraitFace, string SpritePath) ResolveRowNpcArt(ScreenRow row, INpcRegistry? registry = null)
    {
        var meta = ExtractRowNpcMetadata(row, registry);
        return (meta.PortraitFull, meta.PortraitFace, meta.SpritePath);
    }

    public static RowNpcMetadata ExtractRowNpcMetadata(ScreenRow row, INpcRegistry? registry = null)
    {
        var npcCodeFromHint = ExtractCodeFromIconHint(row.IconHint);
        NpcDef? resolvedNpc = null;

        if (registry is not null)
        {
            if (TryExtractNpcId(row, out var npcId)) resolvedNpc = registry.Find(npcId);
            if (resolvedNpc is null && !string.IsNullOrEmpty(npcCodeFromHint)) resolvedNpc = registry.FindByCode(npcCodeFromHint);
        }

        if (resolvedNpc is not null)
        {
            var roleFormatted = RoleTitleVi.Format(resolvedNpc.Role);
            var sub = ExtractRowSubtitle(row, roleFormatted);
            var archetype = ClassifyArchetype(resolvedNpc);
            var (full, face, sprite) = ResolveNpcArt(resolvedNpc);
            return new RowNpcMetadata(resolvedNpc.NameVi, roleFormatted, sub, resolvedNpc.Code, archetype, full, face, sprite);
        }

        var nameVi = row.Cells.Count > 0 && !string.IsNullOrWhiteSpace(row.Cells[0]) ? row.Cells[0] : (row.Title ?? "Nhân sĩ");
        var roleVi = ExtractRowRole(row);
        var subtitleVi = ExtractRowSubtitle(row, roleVi);
        var combinedRole = BuildCombinedRoleHint(row, roleVi);
        var normCode = NormalizeNpcCode(npcCodeFromHint, nameVi);
        var arch = ClassifyArchetype(normCode, combinedRole, nameVi: nameVi);
        var (pFull, pFace, pSprite) = ResolveNpcArt(normCode, combinedRole, nameVi: nameVi);
        return new RowNpcMetadata(nameVi, roleVi, subtitleVi, normCode, arch, pFull, pFace, pSprite);
    }

    public static string BackgroundForKey(string key)
    {
        var k = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (Manifest.Value.Backgrounds.TryGetValue(k, out var rel))
            return rel.StartsWith("art/", StringComparison.OrdinalIgnoreCase) ? rel : $"art/{rel}";
        return k switch
        {
            "market" => "art/backgrounds/market.png",
            "arena" => "art/backgrounds/arena.png",
            "gate" => "art/backgrounds/gate.png",
            "hall" => "art/backgrounds/hall.png",
            "field" => "art/backgrounds/field.png",
            _ => "art/backgrounds/street.png"
        };
    }

    private static string? NormalizeNpcCode(string? npcCode, string? nameVi)
    {
        var raw = (npcCode ?? string.Empty).Trim().ToLowerInvariant();
        if (raw.StartsWith("npc:", StringComparison.Ordinal)) raw = raw["npc:".Length..];
        switch (raw)
        {
            case "liu_bei" or "npc_0001": return "npc_0001";
            case "zhang_fei" or "npc_0002": return "npc_0002";
            case "guan_yu" or "npc_0003": return "npc_0003";
            case "cao_cao" or "npc_0022": return "npc_0022";
            case "zhuge_liang" or "npc_0025": return "npc_0025";
            case "lu_bu" or "npc_0057": return "npc_0057";
            case "diao_chan" or "npc_0140": return "npc_0140";
        }

        var combined = $"{raw} {(nameVi ?? string.Empty).Trim().ToLowerInvariant()}";
        if (combined.Contains("lưu bị", StringComparison.Ordinal)) return "npc_0001";
        if (combined.Contains("trương phi", StringComparison.Ordinal)) return "npc_0002";
        if (combined.Contains("quan vũ", StringComparison.Ordinal)) return "npc_0003";
        if (combined.Contains("tào tháo", StringComparison.Ordinal)) return "npc_0022";
        if (combined.Contains("gia cát lượng", StringComparison.Ordinal)) return "npc_0025";
        if (combined.Contains("lữ bố", StringComparison.Ordinal)) return "npc_0057";
        if (combined.Contains("điêu thuyền", StringComparison.Ordinal)) return "npc_0140";
        return raw.StartsWith("npc_", StringComparison.Ordinal) ? raw : null;
    }

    private static string? ExtractCodeFromIconHint(string? iconHint)
    {
        if (string.IsNullOrWhiteSpace(iconHint)) return null;
        var trimmed = iconHint.Trim();
        if (!trimmed.StartsWith("npc:", StringComparison.OrdinalIgnoreCase)) return null;
        var rest = trimmed["npc:".Length..];
        var colon = rest.IndexOf(':');
        return colon > 0 ? rest[..colon] : rest;
    }

    private static bool TryExtractNpcId(ScreenRow row, out long npcId)
    {
        string[] prefixes = ["talk:npc:", "npc:modal:", "duel:", "chat:", "gift:", "recruit:", "dismiss:", "swear:", "learn:", "train_skill:"];
        foreach (var cmd in row.Commands)
        {
            foreach (var prefix in prefixes)
            {
                if (!cmd.Id.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var rest = cmd.Id[prefix.Length..];
                var colon = rest.IndexOf(':');
                var idPart = colon >= 0 ? rest[..colon] : rest;
                if (long.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out npcId)) return true;
            }
        }
        npcId = 0;
        return false;
    }

    private static string ExtractRowRole(ScreenRow row)
    {
        foreach (var detail in row.Info)
        {
            if (detail.Label is "Thân Phận" or "Danh Phận" or "Chức Vụ" or "Trách Nhiệm" or "Vai Trò" && !string.IsNullOrWhiteSpace(detail.Value))
                return RoleTitleVi.Format(detail.Value);
        }

        if (row.Cells.Count >= 3)
        {
            var cell1 = row.Cells[1].Trim();
            var cell2 = row.Cells[2].Trim();
            if (!string.IsNullOrWhiteSpace(cell2) &&
                (cell1.Contains('(') || cell1.Contains("Phẩm", StringComparison.OrdinalIgnoreCase) || cell1.StartsWith('—') || cell1.StartsWith("⭐") || cell1.StartsWith("🔷") || cell1.StartsWith("🔶") || cell1.StartsWith("⚪")))
                return RoleTitleVi.Format(cell2);
        }

        return row.Cells.Count > 1 && !string.IsNullOrWhiteSpace(row.Cells[1]) ? RoleTitleVi.Format(row.Cells[1]) : "Danh sĩ";
    }

    private static string? ExtractRowSubtitle(ScreenRow row, string resolvedRole)
    {
        if (row.Cells.Count >= 4 && string.Equals(RoleTitleVi.Format(row.Cells[2]), resolvedRole, StringComparison.OrdinalIgnoreCase))
        {
            var styleOrRank = row.Cells[1].Trim();
            var col3 = row.Cells[3].Trim();
            var hasCleanStyle = !string.IsNullOrWhiteSpace(styleOrRank) && !styleOrRank.StartsWith('—');
            if (hasCleanStyle && !string.IsNullOrWhiteSpace(col3) && !col3.Contains('/')) return $"{styleOrRank} · {col3}";
            if (hasCleanStyle) return styleOrRank;
            if (!string.IsNullOrWhiteSpace(col3)) return col3;
        }

        if (row.Cells.Count > 2)
        {
            var cell2 = row.Cells[2].Trim();
            if (!string.Equals(RoleTitleVi.Format(cell2), resolvedRole, StringComparison.OrdinalIgnoreCase)) return cell2;
        }
        return null;
    }

    private static string BuildCombinedRoleHint(ScreenRow row, string resolvedRole)
    {
        var parts = new List<string> { resolvedRole };
        for (var i = 1; i < Math.Min(row.Cells.Count, 4); i++)
        {
            if (!string.IsNullOrWhiteSpace(row.Cells[i])) parts.Add(row.Cells[i]);
        }
        return string.Join(' ', parts);
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            if (text.Contains(kw, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static ManifestTable LoadManifest()
    {
        var npc = new Dictionary<string, (string Portrait, string Sprite)>(StringComparer.OrdinalIgnoreCase)
        {
            ["npc_0001"] = ("liu_bei", "liu_bei"), ["npc_0002"] = ("zhang_fei", "zhang_fei"),
            ["npc_0003"] = ("guan_yu", "guan_yu"), ["npc_0022"] = ("cao_cao", "cao_cao"),
            ["npc_0025"] = ("zhuge_liang", "zhuge_liang"), ["npc_0057"] = ("lu_bu", "lu_bu"),
            ["npc_0140"] = ("lady", "civ_lady")
        };
        var archetype = new Dictionary<string, (string Portrait, string Sprite)>(StringComparer.OrdinalIgnoreCase)
        {
            ["general"] = ("general", "han_officer"), ["scholar"] = ("scholar", "civ_scholar"),
            ["merchant"] = ("merchant", "civ_merchant"), ["elder"] = ("elder", "civ_elder"),
            ["commoner"] = ("commoner", "civ_commoner"), ["lady"] = ("lady", "civ_lady"),
            ["swordswoman"] = ("swordswoman", "female_warrior"), ["archer"] = ("general", "han_archer"),
            ["rebel"] = ("commoner", "yellow_turban"), ["farmer_woman"] = ("farmer_woman", "civ_farmer_woman")
        };
        var backgrounds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["street"] = "backgrounds/street.png", ["market"] = "backgrounds/market.png",
            ["arena"] = "backgrounds/arena.png", ["gate"] = "backgrounds/gate.png",
            ["hall"] = "backgrounds/hall.png", ["field"] = "backgrounds/field.png"
        };

        var path = FindManifestPath();
        if (path is null) return new ManifestTable(npc, archetype, backgrounds, false);

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            ReadPairMap(root, "npc", npc);
            ReadPairMap(root, "archetype", archetype);
            if (root.TryGetProperty("backgrounds", out var bgEl) && bgEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in bgEl.EnumerateObject())
                {
                    var v = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(v)) backgrounds[prop.Name] = v!;
                }
            }
            return new ManifestTable(npc, archetype, backgrounds, true);
        }
        catch
        {
            return new ManifestTable(npc, archetype, backgrounds, false);
        }
    }

    private static void ReadPairMap(JsonElement root, string propName, Dictionary<string, (string Portrait, string Sprite)> target)
    {
        if (!root.TryGetProperty(propName, out var el) || el.ValueKind != JsonValueKind.Object) return;
        foreach (var prop in el.EnumerateObject())
        {
            var p = prop.Value.TryGetProperty("portrait", out var pEl) ? pEl.GetString() : null;
            var s = prop.Value.TryGetProperty("sprite", out var sEl) ? sEl.GetString() : null;
            if (!string.IsNullOrWhiteSpace(p) && !string.IsNullOrWhiteSpace(s)) target[prop.Name] = (p!, s!);
        }
    }

    private static readonly string? CachedScenesDirectory = ResolveSceneAssetsDirectory();
    private static readonly string? CachedManifestPath = ResolveManifestPath();

    public static string? FindSceneAssetsDirectory() => CachedScenesDirectory;

    private static string? ResolveSceneAssetsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 7 && dir is not null; depth++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "assets", "scenes");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? FindManifestPath() => CachedManifestPath;

    private static string? ResolveManifestPath()
    {
        var scenesDir = FindSceneAssetsDirectory();
        if (scenesDir is null) return null;
        var manifestPath = Path.Combine(scenesDir, "art", "manifest.json");
        return File.Exists(manifestPath) ? manifestPath : null;
    }

    private sealed record ManifestTable(
        Dictionary<string, (string Portrait, string Sprite)> Npc,
        Dictionary<string, (string Portrait, string Sprite)> Archetype,
        Dictionary<string, string> Backgrounds,
        bool LoadedFromDisk);
}
