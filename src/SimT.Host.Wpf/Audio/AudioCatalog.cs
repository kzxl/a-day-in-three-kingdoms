using System.IO;
using System.Text.Json;
using SimT.Contracts.Audio;

namespace SimT.Host.Wpf.Audio;

/// <summary>The shared Part A/B catalogue. Unknown, missing and unsafe paths are silent.</summary>
public sealed class AudioCatalog
{
    private readonly string _root;
    private readonly Dictionary<string, Dictionary<string, string>> _groups;

    public AudioCatalog(string root)
    {
        _root = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        try
        {
            _groups = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                File.ReadAllText(Path.Combine(root, "audio.json"))) ?? new();
        }
        catch { _groups = new(); }
    }

    public string? Find(AudioBus bus, string key)
    {
        var group = bus switch { AudioBus.Music => "music", AudioBus.Ambience => "ambience", _ => "sfx" };
        if (string.IsNullOrWhiteSpace(key) || !_groups.TryGetValue(group, out var cues) || cues is null ||
            !cues.TryGetValue(key, out var relative) || string.IsNullOrWhiteSpace(relative)) return null;
        try
        {
            var path = Path.GetFullPath(Path.Combine(_root, relative));
            return path.StartsWith(_root, StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? path : null;
        }
        catch { return null; }
    }
}
