using NAudio.Wave;
using SimT.Contracts.Audio;

namespace SimT.Host.Wpf.Audio;

/// <summary>A single continuous output clock mixes streaming music, ambient layers and up to 12 SFX.</summary>
internal sealed class AudioMixer : ISampleProvider, IDisposable
{
    private sealed class Voice(AudioSource owner, ISampleProvider source, AudioBus bus, string key, float level)
    {
        public readonly AudioSource Owner = owner;
        public readonly ISampleProvider Source = source;
        public readonly AudioBus Bus = bus;
        public readonly string Key = key;
        public float Gain = level;
        public float Target = level;
        public float Step;
        public bool Retiring;
        public void Fade(float target, float seconds)
        {
            Target = target;
            Step = seconds > 0 ? (target - Gain) / (44100 * seconds) : 0;
            if (seconds <= 0) Gain = target;
        }
    }

    private readonly object _gate = new();
    private readonly List<Voice> _voices = [];
    private readonly float[] _volumes = [1, .6f, .8f, .5f];
    private float[] _scratch = [];
    private bool _disposed;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    public void SetVolume(AudioBus bus, float value) { lock (_gate) _volumes[(int)bus] = AudioSettings.Clamp(value); }
    public float GetVolume(AudioBus bus) { lock (_gate) return _volumes[(int)bus]; }

    public void Play(string path, string key, AudioBus bus, bool loop, float fade, float level = 1, float pitch = 1)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (bus != AudioBus.Sfx && _voices.Any(v => v.Bus == bus && v.Key == key && !v.Retiring)) return;
            AudioSource? source = null;
            try
            {
                source = new AudioSource(path, loop);
                // Resampling at a different rate changes pitch and duration together for short cues.
                var provider = source.ToStereo((int)(44100 / Math.Clamp(pitch, .5f, 2)));
                var voice = new Voice(source, provider, bus, key, bus == AudioBus.Sfx ? level : 0);
                if (bus == AudioBus.Music) StopMusic(fade);
                if (bus == AudioBus.Sfx && _voices.Count(v => v.Bus == bus) >= 12)
                    Remove(_voices.First(v => v.Bus == bus));
                if (bus != AudioBus.Sfx) voice.Fade(level, fade);
                _voices.Add(voice);
            }
            catch { source?.Dispose(); }
        }
    }

    public void StopMusic(float fade)
    {
        lock (_gate)
            foreach (var v in _voices.Where(v => v.Bus == AudioBus.Music)) { v.Retiring = true; v.Fade(0, fade); }
    }

    public void RetainAmbience(IEnumerable<string> keys)
    {
        var wanted = keys.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
            foreach (var v in _voices.Where(v => v.Bus == AudioBus.Ambience && !wanted.Contains(v.Key) && !v.Retiring))
            { v.Retiring = true; v.Fade(0, .5f); }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        lock (_gate)
        {
            if (_disposed) return count;
            if (_scratch.Length < count) _scratch = new float[count];
            for (var index = _voices.Count - 1; index >= 0; index--)
            {
                var voice = _voices[index];
                int read;
                try { read = voice.Source.Read(_scratch, 0, count); }
                catch { Remove(voice); continue; }
                var busGain = _volumes[0] * _volumes[(int)voice.Bus];
                for (var i = 0; i < read; i++)
                {
                    if (i % 2 == 0 && voice.Gain != voice.Target)
                    {
                        voice.Gain += voice.Step;
                        if ((voice.Step >= 0 && voice.Gain >= voice.Target) || (voice.Step < 0 && voice.Gain <= voice.Target))
                            voice.Gain = voice.Target;
                    }
                    buffer[offset + i] += _scratch[i] * voice.Gain * busGain;
                }
                if (read < count || (voice.Retiring && voice.Gain <= 0)) Remove(voice);
            }
        }
        // Leave headroom in the mix and prevent float overflows at the output converter.
        for (var i = offset; i < offset + count; i++) buffer[i] = Math.Clamp(buffer[i] * .65f, -1, 1);
        return count;
    }

    private void Remove(Voice voice) { _voices.Remove(voice); voice.Owner.Dispose(); }
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var voice in _voices) voice.Owner.Dispose();
            _voices.Clear();
        }
    }
}
