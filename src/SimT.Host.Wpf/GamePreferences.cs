using System.IO;
using System.Text.Json;
using SimT.Contracts.Localization;

namespace SimT.Host.Wpf;

/// <summary>
/// Persistent user configuration for locale, audio, and gameplay preferences.
/// Saved to %APPDATA%\SimT\preferences.json.
/// </summary>
public sealed class GamePreferences
{
    private static readonly Lazy<GamePreferences> LazyInstance = new(() => Load(null));
    public static GamePreferences Instance => LazyInstance.Value;

    public string Language { get; set; } = "vi";
    public bool BilingualMode { get; set; } = false;
    public string SecondaryLanguage { get; set; } = "zh";
    public bool AutoSave { get; set; } = true;
    public bool ConfirmAdvance { get; set; } = true;
    public bool ShowAlerts { get; set; } = true;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimT", "preferences.json");

    public static GamePreferences Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<GamePreferences>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Fallback gracefully on read errors
        }

        return new GamePreferences();
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var temp = path + ".tmp";
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // Non-critical background save failure safeguard
        }
    }

    public void Apply(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);

        if (!string.IsNullOrWhiteSpace(Language))
        {
            localization.SetLocale(Language);
        }

        localization.IsBilingualMode = BilingualMode;

        if (!string.IsNullOrWhiteSpace(SecondaryLanguage))
        {
            localization.SecondaryLocale = SecondaryLanguage;
        }
    }
}
