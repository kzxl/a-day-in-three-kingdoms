using System.Windows;

namespace SimT.Host.Wpf.Tests;

public static class WpfTestContext
{
    private static readonly object Sync = new();
    private static bool _initialized;

    public static void RunOnSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                lock (Sync)
                {
                    if (!_initialized)
                    {
                        if (Application.Current == null)
                        {
                            try
                            {
                                var app = new Application();
                                var darkTheme = new ResourceDictionary
                                {
                                    Source = new Uri("/SimT.Host.Wpf;component/Themes/Dark.xaml", UriKind.RelativeOrAbsolute)
                                };
                                app.Resources.MergedDictionaries.Add(darkTheme);
                            }
                            catch (InvalidOperationException)
                            {
                                // In case another thread / runner initialized it
                            }
                        }
                        _initialized = true;
                    }
                }

                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
    }
}
