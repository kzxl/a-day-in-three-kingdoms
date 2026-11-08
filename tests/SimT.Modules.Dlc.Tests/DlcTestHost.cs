using SimT.Core.Kernel;
using SimT.Testing;

namespace SimT.Modules.Dlc.Tests;

/// <summary>Thin alias so the DLC tests read naturally; the real boot lives in SimT.Testing.</summary>
internal sealed class DlcTestHost : IDisposable
{
    private readonly TestHost _host;
    public DlcTestHost(long? seed = null) => _host = new("simt-dlc", seed);

    public static string RepoRoot => TestHost.RepoRoot;

    public string DatabasePath => _host.DatabasePath;

    public SimKernel Boot() => _host.Boot();

    public void Dispose() => _host.Dispose();
}
