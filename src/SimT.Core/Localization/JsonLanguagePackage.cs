using System.Text.Json;
using System.Text.Json.Serialization;
using SimT.Contracts.Localization;

namespace SimT.Core.Localization;

/// <summary>
/// Community-loadable JSON language package.
/// Enables dropping custom .json localization files into the languages/ folder without recompilation.
/// </summary>
public sealed class JsonLanguagePackage : BaseLanguagePackage
{
    private sealed class JsonSchema
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("nativeName")]
        public string? NativeName { get; set; }

        [JsonPropertyName("cultureName")]
        public string? CultureName { get; set; }

        [JsonPropertyName("translations")]
        public Dictionary<string, string>? Translations { get; set; }
    }

    private JsonLanguagePackage(LanguageInfo info, IDictionary<string, string> dictionary)
        : base(info, dictionary)
    {
    }

    public static JsonLanguagePackage FromJson(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new ArgumentException("Nội dung gói ngôn ngữ JSON không được để trống.", nameof(jsonContent));

        var doc = JsonSerializer.Deserialize<JsonSchema>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Không thể phân giải gói ngôn ngữ JSON.");

        if (string.IsNullOrWhiteSpace(doc.Code))
            throw new InvalidOperationException("Gói ngôn ngữ JSON thiếu trường 'code'.");

        var info = new LanguageInfo(
            doc.Code.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(doc.DisplayName) ? doc.Code : doc.DisplayName,
            doc.NativeName,
            doc.CultureName);

        return new JsonLanguagePackage(info, doc.Translations ?? new Dictionary<string, string>());
    }

    public static JsonLanguagePackage FromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Không tìm thấy file ngôn ngữ: {filePath}", filePath);

        var json = File.ReadAllText(filePath);
        return FromJson(json);
    }
}
