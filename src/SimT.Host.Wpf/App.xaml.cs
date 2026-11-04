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

        if (e.Args.Contains("--smoke"))
        {
            Shutdown(RunSmokeIsolated());
            return;
        }

        var saves = new SaveManager();
        var title = new TitleWindow(saves);
        MainWindow = title;
        title.Show();
    }

    /// <summary>
    /// Smoke runs use a throwaway saves root so a release or gate run never leaves junk
    /// campaigns in the player's profile. An explicit SIMT_SAVES still wins.
    /// </summary>
    private static int RunSmokeIsolated()
    {
        var ownsDirectory = Environment.GetEnvironmentVariable("SIMT_SAVES") is null;
        var root = Path.Combine(Path.GetTempPath(), "simt-smoke-saves-" + Guid.NewGuid().ToString("N"));

        if (ownsDirectory)
        {
            Environment.SetEnvironmentVariable("SIMT_SAVES", root);
        }

        var code = RunSmoke(new SaveManager());

        if (ownsDirectory && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        return code;
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
