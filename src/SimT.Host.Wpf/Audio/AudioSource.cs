using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NVorbis;

namespace SimT.Host.Wpf.Audio;

/// <summary>Streaming decoder with sample-accurate rewind inside a single Read (no silent buffer at EOF).</summary>
internal sealed class AudioSource : ISampleProvider, IDisposable
{
    private VorbisReader? _ogg;
    private readonly string _path;
    private readonly AudioFileReader? _wav;
    private readonly bool _loop;
    public WaveFormat WaveFormat { get; }

    public AudioSource(string path, bool loop)
    {
        _path = path;
        _loop = loop;
        if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            _ogg = new VorbisReader(path);
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_ogg.SampleRate, _ogg.Channels);
        }
        else
        {
            _wav = new AudioFileReader(path);
            WaveFormat = _wav.WaveFormat;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var total = 0;
        var rewound = false;
        while (total < count)
        {
            var read = _ogg?.ReadSamples(buffer, offset + total, count - total)
                ?? _wav!.Read(buffer, offset + total, count - total);
            total += read;
            if (read > 0) { rewound = false; continue; }
            if (!_loop || rewound) break; // Empty files cannot spin forever.
            // NVorbis 0.10.5 seeking to zero changes its initial overlap samples.
            // A fresh decoder preserves the exact first packet at every loop boundary.
            if (_ogg is not null)
            {
                _ogg.Dispose();
                _ogg = new VorbisReader(_path);
            }
            else _wav!.Position = 0;
            rewound = true;
        }
        return total;
    }

    public ISampleProvider ToStereo(int rate)
    {
        ISampleProvider source = this;
        if (WaveFormat.Channels == 1) source = new MonoToStereoSampleProvider(source);
        if (source.WaveFormat.Channels != 2) throw new NotSupportedException("Only mono/stereo audio is supported.");
        return source.WaveFormat.SampleRate == rate ? source : new WdlResamplingSampleProvider(source, rate);
    }

    public void Dispose() { _ogg?.Dispose(); _wav?.Dispose(); }
}
