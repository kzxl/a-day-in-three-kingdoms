using System.Text.Json;
using SimT.Contracts;
using SimT.Core.Kernel;
using SimT.Core.Modules;

namespace SimT.Testing;

/// <summary>
/// Boots the real kernel in a throwaway directory. The product manifest is filtered to the
/// module artefacts that are actually copied into the test output, so a test project only has
/// to reference the modules it exercises — no "no such module artefact" coupling when another
/// DLC is added later.
/// </summary>
public sealed class TestHost : IDisposable
{
    private readonly string _directory;
    private readonly string _productPath;

    public TestHost(string tag = "simt")
    {
        _directory = Path.Combine(Path.GetTempPath(), tag + "-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        _productPath = Path.Combine(_directory, "product.json");
        File.WriteAllText(_productPath, JsonSerializer.Serialize(new
        {
            id = "test-product",
            name = "Test product",
            modules = AvailableModules(),
            seed = 1840502,
            startLocation = "lac_duong",
            startingCash = 2000
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string RepoRoot { get; } = FindRoot();

    public string DatabasePath => Path.Combine(_directory, "campaign.db");

    public static string ConstantsPath => Path.Combine(RepoRoot, "assets", "config", "game-constants.json");

    public SimKernel Boot() => SimKernel.Open(new KernelOptions(
        DatabasePath: DatabasePath,
        ModulesDirectory: AppContext.BaseDirectory,
        ProductPath: _productPath,
        ConstantsPath: ConstantsPath));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>Module ids whose DLL is present next to the running test assembly.</summary>
    private static IReadOnlyList<string> AvailableModules()
    {
        var discovered = ModuleLoader.Discover(AppContext.BaseDirectory)
            .Select(descriptor => descriptor.Manifest.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        if (discovered.Count == 0)
        {
            throw new SimException("test.no_modules", $"No module artefacts found in {AppContext.BaseDirectory}.");
        }

        return discovered;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SimT.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("SimT.sln not found above the test output directory.");
    }
}
