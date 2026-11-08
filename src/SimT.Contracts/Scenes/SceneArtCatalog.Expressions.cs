using System.Globalization;
using System.IO;
using System.Text.Json;

namespace SimT.Contracts.Scenes;

public static partial class SceneArtCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Expressions = new(LoadExpressions);
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlySet<string>>> ArchetypeVariants = new(LoadArchetypeVariants, isThreadSafe: true);

    public static string ResolveArchetypeVariantKey(string basePortrait, string? npcCode, IReadOnlySet<string>? configuredVariants = null)
    {
        if (string.IsNullOrWhiteSpace(basePortrait) ||
            string.Equals(basePortrait, "player", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(npcCode) ||
            string.Equals(npcCode.Trim(), "player", StringComparison.OrdinalIgnoreCase))
            return basePortrait;

        var manifest = Manifest.Value;
        var normCode = NormalizeNpcCode(npcCode, null);
        if (!string.IsNullOrEmpty(normCode) && (manifest.Npc.ContainsKey(normCode) || manifest.Profile.ContainsKey(normCode)))
            return basePortrait;

        if (!TryExtractCodeNumber(normCode ?? npcCode, out var number))
            return basePortrait;

        var rem = Math.Abs(number % 3);
        if (rem == 0) return basePortrait;

        var candidate = rem == 1 ? $"{basePortrait}_v2" : $"{basePortrait}_v3";
        if (configuredVariants is not null)
            return configuredVariants.Contains(candidate) ? candidate : basePortrait;

        if (!ArchetypeVariants.Value.TryGetValue(basePortrait, out var variants) || !variants.Contains(candidate))
            return basePortrait;

        return FindSceneAssetsDirectory() is { } dir &&
               File.Exists(Path.Combine(dir, "art", "portraits", candidate, "full.webp")) &&
               File.Exists(Path.Combine(dir, "art", "portraits", candidate, "face.webp"))
            ? candidate : basePortrait;
    }

    private static bool TryExtractCodeNumber(string? npcCode, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(npcCode)) return false;
        var (start, end) = (-1, -1);
        for (var i = 0; i < npcCode.Length; i++)
        {
            if (char.IsAsciiDigit(npcCode[i]))
            {
                if (start < 0) start = i;
                end = i + 1;
            }
            else if (start >= 0) break;
        }
        return start >= 0 && int.TryParse(npcCode.AsSpan(start, end - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> LoadArchetypeVariants()
    {
        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (FindManifestPath() is not { } path) return result;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("archetype", out var archEl) && archEl.ValueKind == JsonValueKind.Object)
                foreach (var entry in archEl.EnumerateObject())
                {
                    var p = entry.Value.TryGetProperty("portrait", out var pEl) ? pEl.GetString() : null;
                    if (string.IsNullOrWhiteSpace(p) || !entry.Value.TryGetProperty("variants", out var vEl) || vEl.ValueKind != JsonValueKind.Array) continue;
                    var set = result.TryGetValue(p!, out var existing) ? new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in vEl.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s) set.Add(s);
                    result[p!] = set;
                }
            if (doc.RootElement.TryGetProperty("portraitVariants", out var pvEl) && pvEl.ValueKind == JsonValueKind.Object)
                foreach (var entry in pvEl.EnumerateObject())
                {
                    if (entry.Value.ValueKind != JsonValueKind.Array) continue;
                    var set = result.TryGetValue(entry.Name, out var existing) ? new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in entry.Value.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s) set.Add(s);
                    result[entry.Name] = set;
                }
        }
        catch (IOException) { }
        catch (JsonException) { }
        return result;
    }

    public static string ResolvePortraitExpression(string normalPath, string? expression)
    {
        if (expression is not ("happy" or "angry" or "sad")) return normalPath;
        var key = normalPath.Replace('\\', '/').Split('/');
        if (key.Length != 4 || key[0] != "art" || key[1] != "portraits" || key[3] != "full.webp") return normalPath;
        if (!Expressions.Value.TryGetValue(key[2], out var variants) || !variants.TryGetValue(expression, out var rel)) return normalPath;
        // Validate disk presence too: old or partially installed asset packs keep the normal portrait.
        var expected = $"portraits/{key[2]}/full_{expression}.webp";
        return rel == expected && FindSceneAssetsDirectory() is { } dir && File.Exists(Path.Combine(dir, "art", rel))
            ? PrefixArt(rel) : normalPath;
    }

    public static IReadOnlyDictionary<string, string> ResolvePortraitExpressions(string normalPath) =>
        new[] { "happy", "angry", "sad" }.ToDictionary(e => e, e => ResolvePortraitExpression(normalPath, e));

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadExpressions()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        try
        {
            if (FindManifestPath() is not { } path) return result;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("portraitExpressions", out var entries))
                foreach (var entry in entries.EnumerateObject())
                    result[entry.Name] = entry.Value.EnumerateObject()
                        .Where(e => e.Value.ValueKind == JsonValueKind.String)
                        .ToDictionary(e => e.Name, e => e.Value.GetString()!);
        }
        catch (IOException) { }
        catch (JsonException) { }
        return result;
    }
}
