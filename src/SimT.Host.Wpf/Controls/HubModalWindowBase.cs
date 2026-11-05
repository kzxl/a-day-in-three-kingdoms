using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SimT.Host.Wpf;

/// <summary>
/// Architectural base shell for modal information hubs in SimT.
/// Enforces consistent window positioning, dark title bar styling,
/// Escape-key dismissal, and real-time state synchronization with the host.
/// </summary>
public class HubModalWindowBase : Window
{
    /// <summary>
    /// Fired when an action inside the hub changes game state (vitals, date, gold, items, battles)
    /// to trigger real-time UI updates on the underlying MainWindow.
    /// </summary>
    public event Action? HubStateChanged;

    public HubModalWindowBase()
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Loaded += OnHubLoaded;
        PreviewKeyDown += OnHubPreviewKeyDown;
        DarkTitleBar.Apply(this);
    }

    private void OnHubLoaded(object sender, RoutedEventArgs e)
    {
        if (Background == null)
        {
            var bgBrush = TryFindResource("Bg") as Brush;
            if (bgBrush != null) Background = bgBrush;
        }

        // Trigger initial synchronization on open
        NotifyHubStateChanged();
    }

    protected virtual void OnHubPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Notifies the host MainWindow to refresh headers and active screens immediately.
    /// </summary>
    protected void NotifyHubStateChanged()
    {
        HubStateChanged?.Invoke();
    }
}
