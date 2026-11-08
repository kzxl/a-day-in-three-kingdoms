using System.Windows;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

public partial class App
{
    private static void ValidateAudioSettingsSmoke(SimKernel kernel)
    {
        var settings = new DlcSettingsWindow(kernel);
        settings.PreviewAudioOptionsForSmoke();
        var root = (FrameworkElement)settings.Content;
        root.Measure(new Size(752, 640));
        root.Arrange(new Rect(0, 0, 752, 640));
        root.UpdateLayout();
        var output = Environment.GetEnvironmentVariable("SIMT_AUDIO_QA_SCREENSHOT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var bitmap = new RenderTargetBitmap(752, 640, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(output);
            png.Save(file);
        }
        settings.Close();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Audio.GameAudio.Dispose();
        base.OnExit(e);
    }
}
