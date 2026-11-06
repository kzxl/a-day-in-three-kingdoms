namespace SimT.Contracts.Localization;

/// <summary>
/// Immutable metadata descriptor for a supported language package.
/// </summary>
public sealed record LanguageInfo(
    string Code,
    string DisplayName,
    string? NativeName = null,
    string? CultureName = null)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Contract representing an independent language translation package.
/// </summary>
public interface ILanguagePackage
{
    LanguageInfo Info { get; }
    IReadOnlyDictionary<string, string> Translations { get; }
    string? GetTranslation(string key);
}

/// <summary>
/// Sovereign Dynamic Bilingual Localization Engine.
/// Provides dynamic multi-language switching, bilingual presentation, community JSON package discovery, and fallback resolution.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Active primary language descriptor.</summary>
    LanguageInfo CurrentLanguage { get; }

    /// <summary>Active primary language ISO code (e.g. "vi", "en", "zh").</summary>
    string CurrentLocale { get; }

    /// <summary>Whether dynamic bilingual display is enabled (e.g. "Lạc Dương · Luoyang").</summary>
    bool IsBilingualMode { get; set; }

    /// <summary>Secondary language ISO code used when bilingual mode is active (default "en").</summary>
    string SecondaryLocale { get; set; }

    /// <summary>Fired whenever the active primary or secondary language changes.</summary>
    event Action<LanguageInfo>? LanguageChanged;

    /// <summary>Gets all registered language packages.</summary>
    IReadOnlyList<LanguageInfo> GetAvailableLanguages();

    /// <summary>Switches active language by code.</summary>
    bool SetLocale(string locale);

    /// <summary>Retrieves single-language translation with fallback.</summary>
    string Get(string key, params object[] args);

    /// <summary>
    /// Retrieves bilingual translation formatted as "{Primary}{separator}{Secondary}".
    /// If bilingual mode is disabled or translations match, returns single primary text.
    /// </summary>
    string GetBilingual(string key, string separator = " · ", params object[] args);

    /// <summary>Checks whether a key exists in any registered catalog.</summary>
    bool Has(string key);

    /// <summary>Registers an external or compiled language package.</summary>
    void RegisterPackage(ILanguagePackage package);

    /// <summary>Discovers and loads community JSON language packages from a directory.</summary>
    int LoadPackagesFromDirectory(string directoryPath);
}
