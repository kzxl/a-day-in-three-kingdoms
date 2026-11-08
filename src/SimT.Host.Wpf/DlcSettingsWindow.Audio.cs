using System.Windows;
using SimT.Host.Wpf.Audio;

namespace SimT.Host.Wpf;

public partial class DlcSettingsWindow
{
    private bool _audioOptionsReady;

    internal void PreviewAudioOptionsForSmoke() => TabOptions_Click(this, new RoutedEventArgs());

    private void InitializeAudioOptions()
    {
        var settings = GameAudio.Settings;
        AudioMaster.Value = settings.Master * 100;
        AudioMusic.Value = settings.Music * 100;
        AudioSfx.Value = settings.Sfx * 100;
        AudioAmbience.Value = settings.Ambience * 100;
        UpdateMuteLabel();
        _audioOptionsReady = true;
    }

    private void AudioVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_audioOptionsReady) return;
        var settings = GameAudio.Settings;
        settings.Master = (float)AudioMaster.Value / 100;
        settings.Music = (float)AudioMusic.Value / 100;
        settings.Sfx = (float)AudioSfx.Value / 100;
        settings.Ambience = (float)AudioAmbience.Value / 100;
        GameAudio.SaveSettings();
    }

    private void AudioMute_Click(object sender, RoutedEventArgs e)
    {
        GameAudio.Settings.Muted = !GameAudio.Settings.Muted;
        UpdateMuteLabel();
        GameAudio.SaveSettings();
    }

    private void UpdateMuteLabel() => AudioMute.Content = GameAudio.Settings.Muted ? "Bật tiếng" : "Tắt tiếng";
}
