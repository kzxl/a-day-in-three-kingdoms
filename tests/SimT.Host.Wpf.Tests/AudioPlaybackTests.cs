using System.IO;
using NAudio.Wave;
using SimT.Contracts.Audio;
using SimT.Host.Wpf.Audio;

namespace SimT.Host.Wpf.Tests;

/// <summary>Offline PCM/decoder tests: no WaveOut device is created.</summary>
public sealed class AudioPlaybackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "simt-pcm-" + Guid.NewGuid().ToString("N"));
    private readonly string _wave;

    public AudioPlaybackTests()
    {
        Directory.CreateDirectory(_root);
        _wave = Path.Combine(_root, "tone.wav");
        using var writer = new WaveFileWriter(_wave, WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
        writer.WriteSamples(Enumerable.Repeat(.25f, 882).ToArray(), 0, 882);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Loop_crosses_multiple_file_boundaries_in_one_read_without_silence()
    {
        using var source = new AudioSource(_wave, loop: true);
        var pcm = new float[882 * 3 + 14];
        Assert.Equal(pcm.Length, source.Read(pcm, 0, pcm.Length));
        Assert.All(pcm, sample => Assert.Equal(.25f, sample));
    }

    [Fact]
    public void One_shot_stops_at_eof_and_releases_the_file()
    {
        using (var source = new AudioSource(_wave, loop: false))
        {
            var pcm = new float[2000];
            Assert.Equal(882, source.Read(pcm, 0, pcm.Length));
            Assert.Equal(0, source.Read(pcm, 0, pcm.Length));
        }
        using var exclusive = File.Open(_wave, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Vorbis_loop_replays_identical_samples_across_eof()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SimT.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var path = Path.Combine(root!.FullName, "assets", "audio", "sfx", "ui_turn.ogg");
        using var decoder = new NVorbis.VorbisReader(path);
        var samples = checked((int)(decoder.TotalSamples * decoder.Channels));
        Assert.True(samples > 0);
        var expected = new float[samples];
        Assert.Equal(samples, decoder.ReadSamples(expected, 0, samples));
        using var source = new AudioSource(path, loop: true);
        var actual = new float[samples * 2 + 100];
        Assert.Equal(actual.Length, source.Read(actual, 0, actual.Length));
        for (var i = 0; i < actual.Length; i++) Assert.True(Math.Abs(expected[i % samples] - actual[i]) < .00001f, $"sample {i}/{samples}: expected {expected[i % samples]}, actual {actual[i]}");
    }

    [Fact]
    public void Mixer_limits_sfx_to_twelve_and_scales_master_and_bus()
    {
        using var mixer = new AudioMixer();
        mixer.SetVolume(AudioBus.Master, .5f);
        mixer.SetVolume(AudioBus.Sfx, .4f);
        for (var i = 0; i < 20; i++) mixer.Play(_wave, "click", AudioBus.Sfx, false, 0);
        var pcm = new float[200];
        Assert.Equal(200, mixer.Read(pcm, 0, pcm.Length));
        Assert.All(pcm, sample => Assert.Equal(12 * .25f * .5f * .4f * .65f, sample, 5));
        mixer.SetVolume(AudioBus.Master, 0);
        mixer.Read(pcm, 0, pcm.Length);
        Assert.All(pcm, sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void Crossfade_keeps_constant_signal_and_stop_fades_to_silence()
    {
        using var mixer = new AudioMixer();
        mixer.SetVolume(AudioBus.Music, 1);
        mixer.Play(_wave, "first", AudioBus.Music, true, 0);
        mixer.Play(_wave, "second", AudioBus.Music, true, .01f);
        var pcm = new float[882];
        mixer.Read(pcm, 0, pcm.Length);
        Assert.All(pcm, sample => Assert.Equal(.25f * .65f, sample, 5));
        mixer.StopMusic(.01f);
        mixer.Read(pcm, 0, pcm.Length);
        Assert.True(pcm[0] > pcm[^1]);
        mixer.Read(pcm, 0, pcm.Length);
        Assert.All(pcm, sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void Music_and_multiple_ambient_layers_mix_and_retire_independently()
    {
        using var mixer = new AudioMixer();
        mixer.SetVolume(AudioBus.Music, 1);
        mixer.SetVolume(AudioBus.Ambience, 1);
        mixer.Play(_wave, "city", AudioBus.Music, true, 0);
        mixer.Play(_wave, "birds", AudioBus.Ambience, true, 0);
        mixer.Play(_wave, "rain", AudioBus.Ambience, true, 0);
        mixer.Play(_wave, "birds", AudioBus.Ambience, true, 0); // No duplicate ambient layer.
        var pcm = new float[200];
        mixer.Read(pcm, 0, pcm.Length);
        Assert.All(pcm, sample => Assert.Equal(.75f * .65f, sample, 5));
        mixer.RetainAmbience([]);
        mixer.Read(new float[44100], 0, 44100);
        mixer.Read(pcm, 0, pcm.Length);
        Assert.All(pcm, sample => Assert.Equal(.25f * .65f, sample, 5));
    }

    [Fact]
    public void Backend_factory_is_null_during_tests_and_when_explicitly_disabled()
    {
        Assert.Same(NullAudioEngine.Instance, NAudioEngine.Create(_root, disabled: true));
        Assert.Same(NullAudioEngine.Instance, NAudioEngine.Create(_root, disabled: false));
    }

    [Fact]
    public void Missing_or_corrupt_audio_is_silent_and_does_not_replace_music()
    {
        using var mixer = new AudioMixer();
        mixer.Play(_wave, "city", AudioBus.Music, true, 0);
        mixer.Play(Path.Combine(_root, "absent.ogg"), "missing", AudioBus.Music, true, 1);
        var corrupt = Path.Combine(_root, "corrupt.ogg");
        File.WriteAllText(corrupt, "invalid ogg");
        mixer.Play(corrupt, "broken", AudioBus.Sfx, false, 0);
        var pcm = new float[100];
        mixer.Read(pcm, 0, pcm.Length);
        Assert.All(pcm, sample => Assert.True(sample > 0));
    }
}
