using System.IO;
using System.Windows;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

/// <summary>
/// Composition root of the desktop head. It knows no module type: the kernel resolves the
/// product manifest, loads module artefacts from <c>Modules/</c> and hands back screens.
/// </summary>
public partial class App : Application
{
    private const string DemoSaves = "simt-demo-saves";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "simt-crash.txt"), args.Exception.ToString());
        };

        if (e.Args.Contains("--smoke"))
        {
            Shutdown(RunSmokeIsolated());
            return;
        }

        if (e.Args.Contains("--demo"))
        {
            try
            {
                var screenIndex = Array.FindIndex(e.Args, arg => arg == "--screen");
                ShowDemo(
                    screenIndex >= 0 && screenIndex + 1 < e.Args.Length ? e.Args[screenIndex + 1] : null,
                    e.Args.Contains("--row"));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "simt-demo-error.txt"), error.ToString());
                Shutdown(1);
            }

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
    /// Creates a campaign, renders every screen and exits. WinExe has no console, so the result
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
            var screens = window.RenderAllScreens();
            File.WriteAllText(log, $"ok screens={screens} date={kernel.Today} balance={kernel.Ledger.Balance}");
            window.Close();
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(log, "fail " + error);
            return 1;
        }
    }

    /// <summary>
    /// Developer preview: opens the play screen directly on a fresh campaign with a little
    /// activity on it, so the shell can be inspected (or screenshotted) without clicking
    /// through the title screen. Data lives in the temp folder and is replaced each time.
    /// </summary>
    private void ShowDemo(string? screenId, bool selectRow)
    {
        var root = Path.Combine(Path.GetTempPath(), DemoSaves);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Environment.SetEnvironmentVariable("SIMT_SAVES", root);

        var saves = new SaveManager();
        var meta = saves.Create("Ván thử");
        var kernel = saves.Open(meta);

        SeedDemo(kernel);

        var window = new MainWindow(kernel, saves, meta);
        MainWindow = window;
        window.Show();

        if (screenId is not null)
        {
            window.SelectScreen(screenId);
        }

        if (selectRow)
        {
            window.SelectFirstRow();
        }


    }

    /// <summary>Enough activity that every screen has rows worth looking at.</summary>
    private static void SeedDemo(SimKernel kernel)
    {
        var trade = kernel.Capabilities.Get<IMarketTrade>(CapabilityIds.MarketTrade);
        var warehouse = kernel.Capabilities.Get<IMarketWarehouse>(CapabilityIds.MarketWarehouse);
        var city = kernel.Capabilities.Get<IPlayerState>(CapabilityIds.PlayerState).CurrentCityId;

        trade.Buy(city, 1, 12);
        trade.Buy(city, 3, 6);
        warehouse.Rent(city, out _);
        warehouse.Store(city, 1, 4, out _);

        Command(kernel, "professions.trades", "work:1");
        Command(kernel, "cultivation.farm", "rent:1");
        Command(kernel, "cultivation.farm", "plant:1:gao");
        Command(kernel, "lineage.family", "marry");
        Command(kernel, "lineage.family", "beget");
        Command(kernel, "politics.court", "serve");
        Command(kernel, "politics.court", "apply:1");

        kernel.AdvanceDays(12);
    }

    private static void Command(SimKernel kernel, string screen, string commandId)
    {
        if (kernel.TryGetCommandHandler(screen, out var handler) && handler is not null)
        {
            handler.Execute(commandId);
        }
    }
}
