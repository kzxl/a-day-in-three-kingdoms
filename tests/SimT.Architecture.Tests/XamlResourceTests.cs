using System.Text.RegularExpressions;

namespace SimT.Architecture.Tests;

/// <summary>
/// A missing <c>StaticResource</c> is a start-up crash, not a cosmetic glitch: the window throws
/// while parsing XAML and the player sees nothing at all. 0.2.0 shipped with exactly that bug —
/// the theme renamed <c>AccentPure</c> and the title screen still asked for it — so the keys are
/// now checked like any other contract.
/// </summary>
public sealed class XamlResourceTests
{
    private static readonly Regex KeyDefinition = new("x:Key=\"([A-Za-z0-9_.]+)\"", RegexOptions.Compiled);
    private static readonly Regex KeyUsage = new(@"\{StaticResource ([A-Za-z0-9_.]+)\}", RegexOptions.Compiled);

    [Fact]
    public void Every_static_resource_used_by_a_window_is_defined_somewhere()
    {
        var xaml = Directory
            .EnumerateFiles(Path.Combine(RepoLayout.Root, "src", "SimT.Host.Wpf"), "*.xaml", SearchOption.AllDirectories)
            .ToList();

        // Keys may live in the theme or in a window's own resources; what matters is that the
        // key exists before a window asks for it.
        var defined = xaml
            .SelectMany(file => KeyDefinition.Matches(File.ReadAllText(file)))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = new List<string>();
        foreach (var file in xaml)
        {
            foreach (Match usage in KeyUsage.Matches(File.ReadAllText(file)))
            {
                var key = usage.Groups[1].Value;
                if (!defined.Contains(key))
                {
                    missing.Add($"{Path.GetFileName(file)} -> {key}");
                }
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void The_theme_defines_at_least_the_tokens_the_shell_relies_on()
    {
        var theme = Path.Combine(RepoLayout.Root, "src", "SimT.Host.Wpf", "Themes", "Dark.xaml");
        var defined = KeyDefinition.Matches(File.ReadAllText(theme))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var required in new[]
                 {
                     "Bg", "Rail", "Surface", "SurfaceAlt", "Line", "Text", "Muted", "Faint",
                     "Accent", "AccentSoft", "Gain", "Loss",
                     "Overline", "Caption", "H1", "Display", "Number", "NumberLarge",
                     "PrimaryButton", "GhostButton", "DangerButton", "ChipButton",
                     "NavItem", "SaveItem"
                 })
        {
            Assert.Contains(required, defined);
        }
    }
}
