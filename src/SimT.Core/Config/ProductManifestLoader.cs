using System.Text.Json;
using SimT.Contracts;
using SimT.Contracts.Products;

namespace SimT.Core.Config;

/// <summary>Reads <c>assets/products/*.json</c>: which modules a build ships and how a campaign starts.</summary>
public static class ProductManifestLoader
{
    public static ProductManifest Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new SimException("product.missing", $"Product manifest not found: {path}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        return new ProductManifest(
            Id: Text(root, "id"),
            Name: Text(root, "name"),
            Modules: Strings(root, "modules"),
            Seed: root.TryGetProperty("seed", out var seed) ? seed.GetInt64() : 1L,
            StartLocationCode: Text(root, "startLocation"),
            StartingCash: root.TryGetProperty("startingCash", out var cash) ? cash.GetInt64() : 0L,
            StartDate: SimT.Contracts.Time.GameDateCodec.Parse(TextOr(root, "startDate", "0001-01-01")));
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text
            ? text
            : throw new SimException("product.invalid", $"Product manifest is missing '{name}'.");

    private static string TextOr(JsonElement root, string name, string fallback) =>
        root.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text ? text : fallback;

    private static IReadOnlyList<string> Strings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new SimException("product.invalid", $"Product manifest needs a '{name}' array.");
        }

        var items = new List<string>();
        foreach (var element in value.EnumerateArray())
        {
            var text = element.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                items.Add(text);
            }
        }

        return items;
    }
}
