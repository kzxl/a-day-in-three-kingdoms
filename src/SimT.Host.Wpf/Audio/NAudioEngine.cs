using NAudio.Wave;
using SimT.Contracts.Audio;

namespace SimT.Host.Wpf.Audio;

public sealed class NAudioEngine : IAudioEngine, IDisposable
{
    private readonly AudioCatalog _catalog;
    private readonly AudioMixer _mixer = new();
    private readonly WaveOutEvent _output = new() { DesiredLatency = 100 };

    private NAudioEngine(AudioCatalog catalog) => _catalog = catalog;

    public static IAudioEngine Create(string root, bool disabled)
    {
        // Tests never initialize the backend, even when invoked in an interactive desktop session.
        if (disabled || AppDomain.CurrentDomain.GetAssemblies().Any(a =>
            a.GetName().Name?.StartsWith("testhost", StringComparison.OrdinalIgnoreCase) == true ||
            a.GetName().Name?.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) == true))
            return NullAudioEngine.Instance;
        NAudioEngine? engine = null;
        try
        {
            if (WaveOut.DeviceCount == 0) return NullAudioEngine.Instance;
            engine = new NAudioEngine(new AudioCatalog(root));
            engine._output.Init(engine._mixer);
            engine._output.Play();
            return engine;
        }
        catch { engine?.Dispose(); return NullAudioEngine.Instance; }
    }

    public void PlaySfx(string cueKey, float pitch = 1, float volume = 1)
    {
        if (!float.IsFinite(pitch)) return;
        if (_catalog.Find(AudioBus.Sfx, cueKey) is { } path)
            _mixer.Play(path, cueKey, AudioBus.Sfx, false, 0, AudioSettings.Clamp(volume), pitch);
    }

    public void PlayMusic(string trackKey, bool loop = true, float fadeDurationSeconds = 1)
    {
        if (_catalog.Find(AudioBus.Music, trackKey) is { } path)
            _mixer.Play(path, trackKey, AudioBus.Music, loop, SafeFade(fadeDurationSeconds));
    }

    public void StopMusic(float fadeDurationSeconds = .5f) => _mixer.StopMusic(SafeFade(fadeDurationSeconds));
    public void SetVolume(AudioBus bus, float volume) => _mixer.SetVolume(bus, volume);
    public float GetVolume(AudioBus bus) => _mixer.GetVolume(bus);

    public void SetAmbience(IEnumerable<string> keys)
    {
        var wanted = keys.Distinct(StringComparer.Ordinal).ToArray();
        _mixer.RetainAmbience(wanted);
        foreach (var key in wanted)
            if (_catalog.Find(AudioBus.Ambience, key) is { } path)
                _mixer.Play(path, key, AudioBus.Ambience, true, .5f, key == "war" ? .65f : .7f);
    }

    private static float SafeFade(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 10) : 1;
    public void Dispose() { _output.Dispose(); _mixer.Dispose(); }
}
