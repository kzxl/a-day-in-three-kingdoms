using SimT.Contracts.Localization;

namespace SimT.Core.Localization;

/// <summary>
/// Abstract base class for dictionary-backed strongly-typed language packages.
/// </summary>
public abstract class BaseLanguagePackage : ILanguagePackage
{
    private readonly Dictionary<string, string> _translations;

    public LanguageInfo Info { get; }

    public IReadOnlyDictionary<string, string> Translations => _translations;

    protected BaseLanguagePackage(LanguageInfo info, IDictionary<string, string> dictionary)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _translations = new Dictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
    }

    public string? GetTranslation(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return _translations.TryGetValue(key, out var val) ? val : null;
    }

    public void AddOrUpdate(string key, string translation)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _translations[key] = translation;
        }
    }
}
