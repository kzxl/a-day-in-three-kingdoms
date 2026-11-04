namespace SimT.Contracts.Audio;

/// <summary>
/// Audio channel bus for volume scaling and routing.
/// </summary>
public enum AudioBus
{
    Master,
    Music,
    Sfx,
    Ambience
}

/// <summary>
/// Audio subsystem contract. Games require sound cues (coins, footsteps, blades, ambient weather)
/// and music tracks. Kept as a pure interface with a no-op null implementation so platforms
/// can plug in NAudio, SoLoud, Raylib or WebAudio without coupling Core.
/// </summary>
public interface IAudioEngine
{
    void PlaySfx(string cueKey, float pitch = 1.0f, float volume = 1.0f);

    void PlayMusic(string trackKey, bool loop = true, float fadeDurationSeconds = 1.0f);

    void StopMusic(float fadeDurationSeconds = 0.5f);

    void SetVolume(AudioBus bus, float volume);

    float GetVolume(AudioBus bus);
}

/// <summary>
/// Safe default no-op audio engine when no physical audio output device or backend is attached.
/// </summary>
public sealed class NullAudioEngine : IAudioEngine
{
    public static readonly NullAudioEngine Instance = new();

    private NullAudioEngine() { }

    public void PlaySfx(string cueKey, float pitch = 1.0f, float volume = 1.0f) { }

    public void PlayMusic(string trackKey, bool loop = true, float fadeDurationSeconds = 1.0f) { }

    public void StopMusic(float fadeDurationSeconds = 0.5f) { }

    public void SetVolume(AudioBus bus, float volume) { }

    public float GetVolume(AudioBus bus) => 1.0f;
}
