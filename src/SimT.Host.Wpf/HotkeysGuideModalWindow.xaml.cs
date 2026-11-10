using System.Windows;

namespace SimT.Host.Wpf;

public partial class HotkeysGuideModalWindow : HubModalWindowBase
{
    public HotkeysGuideModalWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
