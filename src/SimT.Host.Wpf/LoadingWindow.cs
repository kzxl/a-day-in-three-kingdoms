using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SimT.Host.Wpf.Controls;
using SimT.Host.Wpf.Scenes;

namespace SimT.Host.Wpf;

/// <summary>
/// Standalone loading window shown immediately when the player selects or creates a save on
/// TitleWindow. Uses the exact size, position, and WindowState that MainWindow will inherit
/// when step 4 finishes so the handoff to MainWindow is completely seamless.
/// </summary>
public sealed class LoadingWindow : Window
{
    private bool _allowClose;

    public GameLoadingScreen Screen { get; } = new();

    public LoadingWindow(LoadingProgressState state, Window? sourceWindow = null)
    {
        Title = "Một Ngày Ở Tam Quốc";
        MinWidth = 1024;
        MinHeight = 720;
        Background = new SolidColorBrush(Color.FromRgb(0x15, 0x11, 0x0D));
        Content = Screen;

        InitializePlacement(sourceWindow);
        DarkTitleBar.Apply(this);
        Screen.AttachState(state);
    }

    public void InitializePlacement(Window? sourceWindow)
    {
        var workArea = SystemParameters.WorkArea;
        var targetWidth = Math.Min(1380.0, Math.Max(MinWidth, workArea.Width));
        var targetHeight = Math.Min(880.0, Math.Max(MinHeight, workArea.Height));

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = targetWidth;
        Height = targetHeight;
        Left = workArea.Left + Math.Max(0.0, (workArea.Width - targetWidth) / 2.0);
        Top = workArea.Top + Math.Max(0.0, (workArea.Height - targetHeight) / 2.0);

        if (sourceWindow?.WindowState == WindowState.Maximized)
            WindowState = WindowState.Maximized;
    }

    public void CopyPlacementTo(Window targetWindow)
    {
        targetWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        if (WindowState == WindowState.Maximized && !RestoreBounds.IsEmpty)
        {
            targetWindow.Left = RestoreBounds.Left;
            targetWindow.Top = RestoreBounds.Top;
            targetWindow.Width = RestoreBounds.Width;
            targetWindow.Height = RestoreBounds.Height;
            targetWindow.WindowState = WindowState.Maximized;
        }
        else
        {
            targetWindow.Left = Left;
            targetWindow.Top = Top;
            targetWindow.Width = ActualWidth > 0 ? ActualWidth : Width;
            targetWindow.Height = ActualHeight > 0 ? ActualHeight : Height;
            targetWindow.WindowState = WindowState;
        }
    }

    public void AllowClose()
    {
        _allowClose = true;
        Screen.StopLoop();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Screen.StopLoop();
        base.OnClosed(e);
    }
}
