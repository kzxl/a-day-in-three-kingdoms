using System.Reflection;

namespace SimT.Architecture.Tests;

/// <summary>Locates the repository so the gate can inspect source files, not just binaries.</summary>
internal static class RepoLayout
{
    private static readonly string Separator = Path.DirectorySeparatorChar.ToString();

    public static string Root { get; } = FindRoot();

    public static IReadOnlyList<string> Sources(params string[] extensions)
    {
        var src = Path.Combine(Root, "src");
        return Directory
            .EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Separator}bin{Separator}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Separator}obj{Separator}", StringComparison.OrdinalIgnoreCase))
            .Where(path => extensions.Length == 0 || extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public static IReadOnlyList<Assembly> ModuleAssemblies() =>
    [
        typeof(SimT.Modules.Base.Content.ContentModule).Assembly,
        typeof(SimT.Modules.Base.World.WorldModule).Assembly,
        typeof(SimT.Modules.Base.Player.PlayerModule).Assembly,
        typeof(SimT.Modules.Base.Npc.NpcModule).Assembly,
        typeof(SimT.Modules.Dlc.Market.MarketModule).Assembly,
        typeof(SimT.Modules.Dlc.Caravan.CaravanModule).Assembly
    ];

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
