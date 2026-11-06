using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimT.Contracts.Localization;
using SimT.Core.Localization.Languages;

namespace SimT.Core.Localization;

/// <summary>
/// Sovereign Dynamic Bilingual Localization Engine.
/// Implements multi-tier fallback, subfolder modular package loading (languages/{code}/{dlc}.json),
/// live runtime switching, and bilingual formatting.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private sealed class LanguageManifestSchema
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

    private sealed class DynamicModularPackage : BaseLanguagePackage
    {
        public DynamicModularPackage(LanguageInfo info, IDictionary<string, string> dictionary)
            : base(info, dictionary)
        {
        }
    }

    private readonly ConcurrentDictionary<string, ILanguagePackage> _packages = new(StringComparer.OrdinalIgnoreCase);
    private ILanguagePackage _currentPackage;
    private readonly ILanguagePackage _fallbackViPackage;
    private readonly ILanguagePackage _fallbackEnPackage;

    public event Action<LanguageInfo>? LanguageChanged;

    public LanguageInfo CurrentLanguage => _currentPackage.Info;
    public string CurrentLocale => _currentPackage.Info.Code;

    public bool IsBilingualMode { get; set; } = false;
    public string SecondaryLocale { get; set; } = "en";

    public LocalizationService(string defaultLocale = "vi")
    {
        // 1. Built-in hardcoded fallback packages
        _fallbackViPackage = new VietnameseLanguagePackage();
        _fallbackEnPackage = new DynamicModularPackage(
            new LanguageInfo("en", "English", "English", "en-US"),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["app.title"] = "SimT — A Day in the Three Kingdoms",
                ["app.subtitle"] = "Grand Strategy, Societal & Economic Simulation",
                ["app.version"] = "Version",
                ["app.settings"] = "Game Settings",
                ["app.exit"] = "Exit Game",
                ["app.save"] = "Save Game",
                ["app.load"] = "Load Game",
                ["time.day"] = "Day",
                ["time.month"] = "Month",
                ["time.year"] = "Year",
                ["time.turn"] = "Turn",
                ["time.spring"] = "Spring",
                ["time.summer"] = "Summer",
                ["time.autumn"] = "Autumn",
                ["time.winter"] = "Winter",
                ["common.ok"] = "OK",
                ["common.cancel"] = "Cancel",
                ["common.close"] = "Close"
            });

        RegisterPackage(_fallbackViPackage);
        RegisterPackage(_fallbackEnPackage);

        var defaultClean = defaultLocale.Trim().ToLowerInvariant();
        var defaultPrefix = defaultClean.Split('-')[0];
        _currentPackage = _packages.TryGetValue(defaultClean, out var def) ? def
            : _packages.TryGetValue(defaultPrefix, out var defP) ? defP
            : _fallbackViPackage;

        // 2. Discover external modular packages from runtime directories
        TryDiscoverLanguages();
    }

    public void RegisterPackage(ILanguagePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        _packages[package.Info.Code] = package;
    }

    public IReadOnlyList<LanguageInfo> GetAvailableLanguages()
    {
        return _packages.Values
            .Select(p => p.Info)
            .OrderBy(i => i.Code == "vi" ? 0 : i.Code == "en" ? 1 : i.Code == "zh" ? 2 : 3)
            .ThenBy(i => i.DisplayName)
            .ToList();
    }

    public void RegisterStrings(string locale, IReadOnlyDictionary<string, string> entries)
    {
        if (string.IsNullOrWhiteSpace(locale) || entries == null) return;
        var cleanCode = locale.Trim().ToLowerInvariant();
        var prefix = cleanCode.Split('-')[0];

        if (_packages.TryGetValue(cleanCode, out var existing) && existing is BaseLanguagePackage basePkg)
        {
            foreach (var (k, v) in entries) basePkg.AddOrUpdate(k, v);
        }
        else if (_packages.TryGetValue(prefix, out var prefixExisting) && prefixExisting is BaseLanguagePackage prefixBasePkg)
        {
            foreach (var (k, v) in entries) prefixBasePkg.AddOrUpdate(k, v);
            _packages[cleanCode] = prefixExisting;
        }
        else
        {
            var dict = new Dictionary<string, string>(entries, StringComparer.OrdinalIgnoreCase);
            var info = new LanguageInfo(cleanCode, locale, locale, locale);
            var pkg = new DynamicModularPackage(info, dict);
            RegisterPackage(pkg);
            _packages[prefix] = pkg;

            if (_currentPackage.Info.Code.Equals(cleanCode, StringComparison.OrdinalIgnoreCase) ||
                _currentPackage.Info.Code.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _currentPackage = pkg;
            }
        }
    }

    public bool SetLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return false;

        var cleanCode = locale.Trim().ToLowerInvariant();
        if (!_packages.TryGetValue(cleanCode, out var pkg))
        {
            var prefix = cleanCode.Split('-')[0];
            if (!_packages.TryGetValue(prefix, out pkg))
            {
                return false;
            }
        }

        if (_currentPackage.Info.Code.Equals(pkg.Info.Code, StringComparison.OrdinalIgnoreCase))
            return true;

        _currentPackage = pkg;
        LanguageChanged?.Invoke(_currentPackage.Info);
        return true;
    }

    public string Get(string key, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        // Tier 1: Current active package
        string? val = _currentPackage.GetTranslation(key);

        // Tier 2: English fallback
        if (string.IsNullOrEmpty(val) && _currentPackage != _fallbackEnPackage)
        {
            val = _fallbackEnPackage.GetTranslation(key);
        }

        // Tier 3: Vietnamese fallback
        if (string.IsNullOrEmpty(val) && _currentPackage != _fallbackViPackage)
        {
            val = _fallbackViPackage.GetTranslation(key);
        }

        if (string.IsNullOrEmpty(val))
        {
            foreach (var other in _packages.Values)
            {
                var otherVal = other.GetTranslation(key);
                if (!string.IsNullOrEmpty(otherVal))
                {
                    val = otherVal;
                    break;
                }
            }
        }

        string template = val ?? key;
        return args.Length > 0 ? string.Format(System.Globalization.CultureInfo.InvariantCulture, template, args) : template;
    }

    public string GetBilingual(string key, string separator = " · ", params object[] args)
    {
        var primary = Get(key, args);
        if (!IsBilingualMode) return primary;

        var secCode = SecondaryLocale.Trim().ToLowerInvariant();
        if (secCode.Equals(CurrentLocale, StringComparison.OrdinalIgnoreCase))
        {
            return primary;
        }

        if (!_packages.TryGetValue(secCode, out var secPkg))
        {
            return primary;
        }

        var secVal = secPkg.GetTranslation(key);
        if (string.IsNullOrWhiteSpace(secVal))
        {
            return primary;
        }

        var secondary = args.Length > 0 ? string.Format(System.Globalization.CultureInfo.InvariantCulture, secVal, args) : secVal;
        if (primary.Equals(secondary, StringComparison.OrdinalIgnoreCase))
        {
            return primary;
        }

        return $"{primary}{separator}{secondary}";
    }

    public bool Has(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return _packages.Values.Any(p => p.GetTranslation(key) is not null);
    }

    /// <summary>
    /// Loads modular language files partitioned per folder: languages/{langCode}/{dlc_or_core}.json
    /// Also supports standalone languages/{langCode}.json packages.
    /// </summary>
    public int LoadPackagesFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;

        int loadedCount = 0;

        // 1. Scan language subdirectories: languages/{code}/
        var subDirs = Directory.GetDirectories(directoryPath);
        foreach (var dir in subDirs)
        {
            var folderCode = Path.GetFileName(dir).Trim().ToLowerInvariant();
            var (info, dict) = ParseLanguageDirectory(dir, folderCode);

            if (dict.Count > 0)
            {
                if (_packages.TryGetValue(info.Code, out var existing) && existing is DynamicModularPackage dynamicPkg)
                {
                    // Merge into existing package
                    foreach (var (k, v) in dict)
                    {
                        dynamicPkg.AddOrUpdate(k, v);
                    }
                }
                else
                {
                    RegisterPackage(new DynamicModularPackage(info, dict));
                }
                loadedCount++;
            }
        }

        // 2. Scan legacy/standalone single-file packages: languages/{code}.json
        var singleFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (var file in singleFiles)
        {
            try
            {
                var pkg = JsonLanguagePackage.FromFile(file);
                RegisterPackage(pkg);
                loadedCount++;
            }
            catch
            {
                // Ignore malformed files
            }
        }

        return loadedCount;
    }

    private static (LanguageInfo Info, Dictionary<string, string> Translations) ParseLanguageDirectory(string dir, string folderCode)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        LanguageInfo? parsedInfo = null;

        var jsonFiles = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
        foreach (var file in jsonFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                var fileName = Path.GetFileNameWithoutExtension(file);

                if (fileName.Equals("manifest", StringComparison.OrdinalIgnoreCase))
                {
                    var manifest = JsonSerializer.Deserialize<LanguageManifestSchema>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (manifest != null)
                    {
                        var code = string.IsNullOrWhiteSpace(manifest.Code) ? folderCode : manifest.Code.Trim().ToLowerInvariant();
                        var name = string.IsNullOrWhiteSpace(manifest.DisplayName) ? folderCode.ToUpperInvariant() : manifest.DisplayName;
                        parsedInfo = new LanguageInfo(code, name, manifest.NativeName, manifest.CultureName);

                        if (manifest.Translations != null)
                        {
                            foreach (var (k, v) in manifest.Translations) dict[k] = v;
                        }
                    }
                }
                else
                {
                    // Modular translation file: can be raw dictionary {"k": "v"} or schema with "translations"
                    using var jsonDoc = JsonDocument.Parse(content);
                    if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (jsonDoc.RootElement.TryGetProperty("translations", out var transProp) && transProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in transProp.EnumerateObject())
                            {
                                dict[prop.Name] = prop.Value.GetString() ?? string.Empty;
                            }
                        }
                        else
                        {
                            // Pure key-value dictionary file
                            foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                            {
                                if (prop.Value.ValueKind == JsonValueKind.String)
                                {
                                    dict[prop.Name] = prop.Value.GetString() ?? string.Empty;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Silently skip malformed individual file to protect overall stability
            }
        }

        var defaultName = folderCode switch
        {
            "vi" => "Tiếng Việt",
            "en" => "English",
            "zh" => "中文",
            _ => folderCode.ToUpperInvariant()
        };

        var finalInfo = parsedInfo ?? new LanguageInfo(folderCode, defaultName, defaultName, null);
        return (finalInfo, dict);
    }

    private void TryDiscoverLanguages()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var langDir = Path.Combine(baseDir, "languages");
            if (Directory.Exists(langDir))
            {
                LoadPackagesFromDirectory(langDir);
            }
        }
        catch
        {
            // Safeguard
        }
    }
}
