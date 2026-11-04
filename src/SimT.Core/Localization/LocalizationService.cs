using System.Text.Json;
using SimT.Contracts.Localization;

namespace SimT.Core.Localization;

/// <summary>
/// Dictionary-backed localization service with file-loading support and token interpolation.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _catalogs = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLocale = "vi-VN";

    public LocalizationService(string defaultLocale = "vi-VN")
    {
        _currentLocale = defaultLocale;
        _catalogs[defaultLocale] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public string CurrentLocale => _currentLocale;

    public void SetLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return;
        }

        _currentLocale = locale;
        if (!_catalogs.ContainsKey(locale))
        {
            _catalogs[locale] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void RegisterStrings(string locale, IReadOnlyDictionary<string, string> entries)
    {
        if (!_catalogs.TryGetValue(locale, out var catalog))
        {
            catalog = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _catalogs[locale] = catalog;
        }

        foreach (var (k, v) in entries)
        {
            catalog[k] = v;
        }
    }

    public void LoadJsonFile(string locale, string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var json = File.ReadAllText(filePath);
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (entries is not null)
        {
            RegisterStrings(locale, entries);
        }
    }

    public string Get(string key, params object[] args)
    {
        if (_catalogs.TryGetValue(_currentLocale, out var catalog) && catalog.TryGetValue(key, out var template))
        {
            return args.Length > 0 ? string.Format(System.Globalization.CultureInfo.InvariantCulture, template, args) : template;
        }

        // Fallback: check other locales or return key itself
        foreach (var other in _catalogs.Values)
        {
            if (other.TryGetValue(key, out var fallbackTemplate))
            {
                return args.Length > 0 ? string.Format(System.Globalization.CultureInfo.InvariantCulture, fallbackTemplate, args) : fallbackTemplate;
            }
        }

        return key;
    }

    public bool Has(string key) =>
        _catalogs.TryGetValue(_currentLocale, out var catalog) && catalog.ContainsKey(key);
}
