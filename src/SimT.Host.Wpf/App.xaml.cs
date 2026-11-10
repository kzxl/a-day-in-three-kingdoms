using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        Audio.GameAudio.Initialize(e.Args.Contains("--smoke") || e.Args.Contains("--demo"));

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
                    e.Args.Contains("--row"),
                    e.Args.Contains("--advance"),
                    Array.FindIndex(e.Args, arg => arg == "--act") is var actAt && actAt >= 0 && actAt + 1 < e.Args.Length
                        ? e.Args[actAt + 1]
                        : null);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "simt-demo-error.txt"), error.ToString());
                Shutdown(1);
            }

            return;
        }

        try
        {
            var saves = new SaveManager();
            var title = new TitleWindow(saves);
            MainWindow = title;
            title.Show();
        }
        catch (Exception error)
        {
            // A start-up failure must say so instead of vanishing: the player has no console.
            var log = Path.Combine(Path.GetTempPath(), "simt-crash.txt");
            File.WriteAllText(log, error.ToString());
            var message = string.Join(
                Environment.NewLine + Environment.NewLine,
                "Không mở được SimT.",
                error.GetType().Name + ": " + error.Message,
                "Chi tiết: " + log);

            MessageBox.Show(message, "SimT", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
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
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of temp smoke folder on Windows
            }
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
            // The title screen parses its own XAML; a missing resource there must fail the gate,
            // not only the player's first double-click.
            var title = new TitleWindow(saves);
            title.UpdateLayout();
            title.Close();

            var meta = saves.Create("smoke-" + DateTime.UtcNow.Ticks);
            var kernel = saves.Open(meta);
            var window = new MainWindow(kernel, saves, meta, isSmoke: true);
            window.Show();
            ValidateAudioSettingsSmoke(kernel);
            var screens = window.RenderAllScreens();
            var compass = new TradeCompassModalWindow(kernel);
            compass.UpdateLayout();
            compass.Close();
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
    private void ShowDemo(string? screenId, bool selectRow, bool advance, string? act)
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
        if (int.TryParse(Environment.GetEnvironmentVariable("SIMT_DEMO_WIDTH"), out var demoWidth) && demoWidth >= 800)
        {
            window.Width = demoWidth;
        }

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

        if (advance)
        {
            window.AdvanceOneDayForDemo();
        }

        if (act is not null)
        {
            window.RunCommandForDemo(act);
        }

        var screenshotOut = Environment.GetEnvironmentVariable("SIMT_DEMO_SCREENSHOT");
        if (!string.IsNullOrWhiteSpace(screenshotOut))
        {
            window.DismissStartupOverlay(immediate: true);
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
            window.UpdateLayout();
            if (window.Content is FrameworkElement rootEl)
            {
                var w = Math.Max(800, (int)Math.Round(rootEl.ActualWidth > 0 ? rootEl.ActualWidth : window.Width));
                var h = Math.Max(600, (int)Math.Round(rootEl.ActualHeight > 0 ? rootEl.ActualHeight : window.Height));
                rootEl.Measure(new Size(w, h));
                rootEl.Arrange(new Rect(0, 0, w, h));
                rootEl.UpdateLayout();

                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(rootEl);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(screenshotOut))
                {
                    encoder.Save(fs);
                }
            }
            window.Close();
            Shutdown(0);
        }
    }

    /// <summary>Informational version, which includes the commit the build came from.</summary>
    internal static string Version() =>
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(App).Assembly.GetName().Version?.ToString()
        ?? "unknown";

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

        if (kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var social) && social is not null &&
            kernel.Capabilities.TryGet<INpcRegistry>(CapabilityIds.NpcRegistry, out var reg) && reg is not null &&
            kernel.Capabilities.TryGet<IPlayerAttributes>(CapabilityIds.PlayerAttributes, out var attrs) && attrs is not null &&
            kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bag) && bag is not null &&
            kernel.Capabilities.TryGet<IItemCatalog>(CapabilityIds.ItemCatalog, out var items) && items is not null)
        {
            var teaId = items.FindByCode("tra")?.Id ?? 3;
            bag.Add(city, 1, 130);
            trade.Sell(city, 1, 110);
            bag.Add(city, teaId, 50);
            attrs.GainFame(220, "Khởi nghiệp thương trường");
            if (reg.All.SelectMany(n => n.Skills).FirstOrDefault() is { } sk)
                attrs.LearnSkill(sk.Code);
            if ((reg.FindByCode("thuong_nhan_a") ?? reg.FindByCitadel(city).FirstOrDefault()) is { } merchant)
            {
                for (var i = 0; i < 16; i++) social.GiftNpc(merchant.Id, teaId);
                social.RecruitRetainer(merchant.Id, "accountant", 45);
                social.SwearBrotherhood(merchant.Id);
            }
            foreach (var friend in reg.FindByCitadel(city).Where(n => n.Code != "thuong_nhan_a" && social.GetNpcRank(n.Id) <= NpcRank.B).Take(2))
            {
                try
                {
                    for (var i = 0; i < 16; i++) social.GiftNpc(friend.Id, teaId);
                    social.SwearBrotherhood(friend.Id);
                }
                catch { }
            }
            social.HostBanquet();
        }

        kernel.AdvanceDays(30);
        if (kernel.Capabilities.TryGet<ISocialNetwork>(CapabilityIds.SocialNetwork, out var socAfter) &&
            socAfter is not null && socAfter.GetLetters().Count == 0)
        {
            kernel.AdvanceDays(31);
        }
        if (kernel.Capabilities.TryGet<IBag>(CapabilityIds.PlayerBag, out var bagAfter) && bagAfter is not null)
        {
            bagAfter.Add(city, 4, 24);
            trade.Sell(city, 4, 20);
        }
        trade.Buy(city, 3, 22);
        kernel.AdvanceDays(1);
    }

    private static void Command(SimKernel kernel, string screen, string commandId)
    {
        if (kernel.TryGetCommandHandler(screen, out var handler) && handler is not null)
        {
            handler.Execute(commandId);
        }
    }
}
