namespace SimT.Contracts.Localization;

/// <summary>
/// Text localization and token resolution service.
/// Decouples engine logs, UI labels, and module messages from hardcoded language strings.
/// </summary>
public interface ILocalizationService
{
    string CurrentLocale { get; }

    void SetLocale(string locale);

    string Get(string key, params object[] args);

    bool Has(string key);
}
