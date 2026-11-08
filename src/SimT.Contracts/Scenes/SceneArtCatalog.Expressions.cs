using System.Text.Json;

namespace SimT.Contracts.Scenes;

public static partial class SceneArtCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Expressions = new(LoadExpressions);

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
