using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SimT.Host.Wpf;

/// <summary>
/// Asks DWM to draw a dark title bar and applies smooth game entry motion.
/// Without it a dark WPF app wears a bright Windows frame, which breaks game immersion.
/// </summary>
internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window, bool withEntryMotion = true)
    {
        if (window.IsLoaded)
        {
            ApplyToHandle(window);
        }
        else
        {
            window.SourceInitialized += (_, _) => ApplyToHandle(window);
        }

        if (withEntryMotion)
        {
            ApplyEntryMotion(window);
        }
    }

    public static void ApplyEntryMotion(Window window)
    {
        window.Loaded += (_, _) =>
        {
            if (window.Content is FrameworkElement root)
            {
                root.RenderTransformOrigin = new Point(0.5, 0.5);
                var scale = new ScaleTransform(0.96, 0.96);
                root.RenderTransform = scale;

                var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                var zoom = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                root.BeginAnimation(UIElement.OpacityProperty, fade);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, zoom);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, zoom);
            }
        };
    }

    private static void ApplyToHandle(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
    }
}
