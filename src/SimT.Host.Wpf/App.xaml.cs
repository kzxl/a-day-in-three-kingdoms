using System.IO;
using System.Windows;

namespace SimT.Host.Wpf;

/// <summary>
/// Composition root of the desktop head. It knows no module type: the kernel resolves the
/// product manifest, loads module artefacts from <c>Modules/</c> and hands back screens.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var saves = new SaveManager();

        if (e.Args.Contains("--smoke"))
        {
            Shutdown(RunSmoke(saves));
            return;
        }

        var title = new TitleWindow(saves);
        MainWindow = title;
        title.Show();
    }

    /// <summary>
    /// Creates a campaign, renders all tabs and exits. WinExe has no console, so the result
    /// goes to a file next to the temp directory and to the process exit code.
    /// </summary>
    private static int RunSmoke(SaveManager saves)
    {
        var log = Path.Combine(Path.GetTempPath(), "simt-smoke.txt");
        try
        {
            var meta = saves.Create("smoke-" + DateTime.UtcNow.Ticks);
            var kernel = saves.Open(meta);
            var window = new MainWindow(kernel, saves, meta);
            window.Show();
            var tabs = window.RenderAllTabs();
            File.WriteAllText(log, $"ok screens={tabs} date={kernel.Today} balance={kernel.Ledger.Balance}");
            window.Close();
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(log, "fail " + error);
            return 1;
        }
    }
}
