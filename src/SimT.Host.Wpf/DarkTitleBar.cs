using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SimT.Host.Wpf;

/// <summary>
/// Asks DWM to draw a dark title bar. Without it a dark WPF app wears a bright Windows frame,
/// which is the first thing that makes a desktop tool look unfinished.
/// </summary>
internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        if (window.IsLoaded)
        {
            ApplyToHandle(window);
            return;
        }

        window.SourceInitialized += (_, _) => ApplyToHandle(window);
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
