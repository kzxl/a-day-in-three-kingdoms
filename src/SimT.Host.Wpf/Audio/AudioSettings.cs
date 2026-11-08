using System.IO;
using System.Text.Json;
using SimT.Contracts.Audio;

namespace SimT.Host.Wpf.Audio;

public sealed class AudioSettings
{
    public float Master { get; set; } = 1;
    public float Music { get; set; } = .6f;
    public float Sfx { get; set; } = .8f;
    public float Ambience { get; set; } = .5f;
    public bool Muted { get; set; }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimT", "audio.json");

    public static float Clamp(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    public static AudioSettings Load(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<AudioSettings>(File.ReadAllText(path)) ?? new();
            value.Master = Clamp(value.Master);
            value.Music = Clamp(value.Music);
            value.Sfx = Clamp(value.Sfx);
            value.Ambience = Clamp(value.Ambience);
            return value;
        }
        catch { return new(); }
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, overwrite: true);
        }
        catch { /* A read-only profile must not interrupt play. */ }
    }

    public void Apply(IAudioEngine engine)
    {
        engine.SetVolume(AudioBus.Master, Muted ? 0 : Clamp(Master));
        engine.SetVolume(AudioBus.Music, Clamp(Music));
        engine.SetVolume(AudioBus.Sfx, Clamp(Sfx));
        engine.SetVolume(AudioBus.Ambience, Clamp(Ambience));
    }
}
