using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShapePath = System.Windows.Shapes.Path;
using SimT.Contracts.Playing;
using SimT.Contracts.Scenes;
using SimT.Contracts.Ui;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Tam Quốc character portrait card displaying face art (resolved via manifest.json),
/// name, localized role title, affection GameProgressBar, and action GameButtons.
/// Used on Nhân Sĩ (npc.directory) and Nhân Duyên (social.network) screens.
/// </summary>
public sealed class GamePortraitCard : Border
{
    private static readonly Brush CardBg = Freeze(new SolidColorBrush(Color.FromRgb(0x1D, 0x16, 0x11)));
    private static readonly Brush CardHoverBg = Freeze(new SolidColorBrush(Color.FromRgb(0x24, 0x1A, 0x13)));
    private static readonly Brush CardBorder = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x2C, 0x20)));
    private static readonly Brush HoverBorder = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush PortraitBg = Freeze(new SolidColorBrush(Color.FromRgb(0x15, 0x11, 0x0D)));
    private static readonly Brush InkFg = Freeze(new SolidColorBrush(Color.FromRgb(0xF3, 0xE6, 0xCC)));
    private static readonly Brush AccentFg = Freeze(new SolidColorBrush(Color.FromRgb(0xC4, 0xA0, 0x62)));
    private static readonly Brush MutedFg = Freeze(new SolidColorBrush(Color.FromRgb(0xB9, 0xAA, 0x8C)));
    private static readonly FontFamily SerifFont = new("Noto Serif, Georgia, Segoe UI");

    private static readonly Dictionary<string, ImageSource?> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string>? CommandInvoked;
    public event Action? CardSelected;

    public GamePortraitCard(
        string nameVi,
        string roleVi,
        string? subtitleVi,
        double affectionValue,
        double affectionMax,
        string? npcCodeOrRole,
        IReadOnlyList<ScreenCommand> commands,
        IReadOnlyList<(string Label, string Value)>? tooltipStats = null,
        string? tooltipDesc = null)
    {
        Width = 248;
        Background = CardBg;
        BorderBrush = CardBorder;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(12, 10, 12, 10);
        Margin = new Thickness(0, 0, 10, 10);
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;

        var root = new StackPanel();

        // Top row: Portrait + Name/Role/Subtitle
        var topDock = new DockPanel { Margin = new Thickness(0, 0, 0, 9) };
        var avatarBorder = BuildAvatarElement(npcCodeOrRole, roleVi, 52, nameVi);
        avatarBorder.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(avatarBorder, Dock.Left);
        topDock.Children.Add(avatarBorder);

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock
        {
            Text = nameVi,
            FontFamily = SerifFont,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = InkFg,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        infoStack.Children.Add(new TextBlock
        {
            Text = RoleTitleVi.Format(roleVi),
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = AccentFg,
            Margin = new Thickness(0, 1, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (!string.IsNullOrWhiteSpace(subtitleVi))
        {
            infoStack.Children.Add(new TextBlock
            {
                Text = subtitleVi,
                FontSize = 11,
                Foreground = MutedFg,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        topDock.Children.Add(infoStack);
        root.Children.Add(topDock);

        // Middle: Affection GameProgressBar
        var bar = new GameProgressBar(
            label: "Hảo cảm",
            value: affectionValue,
            maximum: affectionMax > 0 ? affectionMax : 100,
            tone: GameProgressTone.Affection,
            compact: true)
        {
            Margin = new Thickness(0, 0, 0, 9)
        };
        root.Children.Add(bar);

        // Bottom: Action buttons (Bái phỏng / Gặp gỡ / Hồ sơ)
        var btnWrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var cmd in commands.Where(c => !c.RequiresQuantity).Take(3))
        {
            var variant = cmd.Kind switch
            {
                CommandKind.Primary => GameButtonVariant.Primary,
                CommandKind.Danger => GameButtonVariant.Danger,
                _ => GameButtonVariant.Secondary
            };
            var btn = new GameButton(cmd.Label, variant, isCompact: true)
            {
                Tag = cmd.Id,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btn.Click += (_, e) =>
            {
                e.Handled = true;
                CommandInvoked?.Invoke(cmd.Id);
            };
            btnWrap.Children.Add(btn);
        }
        root.Children.Add(btnWrap);

        Child = root;
        ToolTip = new GameRichTooltip(nameVi, RoleTitleVi.Format(roleVi), tooltipStats, tooltipDesc);

        MouseEnter += (_, _) =>
        {
            Background = CardHoverBg;
            BorderBrush = HoverBorder;
        };
        MouseLeave += (_, _) =>
        {
            Background = CardBg;
            BorderBrush = CardBorder;
        };
        MouseLeftButtonUp += (_, _) => CardSelected?.Invoke();
    }

    public static Border BuildAvatarElement(ScreenRow row, double size = 34, INpcRegistry? registry = null)
    {
        var (_, faceRel, spriteRel) = SceneArtCatalog.ResolveRowNpcArt(row, registry);
        return BuildAvatarBorderFromPaths(faceRel, spriteRel, size);
    }

    public static Border BuildAvatarElement(string? npcCode, string? role, double size = 34, string? nameVi = null)
    {
        var (_, faceRel, spriteRel) = SceneArtCatalog.ResolveNpcArt(npcCode, role, nameVi: nameVi);
        return BuildAvatarBorderFromPaths(faceRel, spriteRel, size);
    }

    private static Border BuildAvatarBorderFromPaths(string faceRel, string spriteRel, double size)
    {
        var imgSource = TryLoadSceneImage(faceRel) ?? TryLoadSceneImage(spriteRel);

        var frame = new Border
        {
            Width = size,
            Height = size,
            Background = PortraitBg,
            BorderBrush = HoverBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(size >= 48 ? 6 : 4),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (imgSource is not null)
        {
            frame.Child = new Image
            {
                Source = imgSource,
                Stretch = Stretch.UniformToFill,
                VerticalAlignment = VerticalAlignment.Top,
                Tag = faceRel
            };
        }
        else
        {
            frame.Child = new ShapePath
            {
                Data = Geometry.Parse("M12,11 A4,4 0 1,0 12,3 A4,4 0 1,0 12,11 M5,20 C5,16 8,14 12,14 C16,14 19,16 19,20"),
                Stroke = AccentFg,
                StrokeThickness = 1.5,
                Width = size * 0.52,
                Height = size * 0.52,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        frame.Tag = faceRel;
        return frame;
    }

    public static ImageSource? TryLoadSceneImage(string relativeArtPath)
    {
        if (string.IsNullOrWhiteSpace(relativeArtPath)) return null;
        if (ImageCache.TryGetValue(relativeArtPath, out var cached)) return cached;

        try
        {
            var normalized = relativeArtPath.Replace('/', System.IO.Path.DirectorySeparatorChar);
            var scenesDir = SceneArtCatalog.FindSceneAssetsDirectory()
                ?? System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "scenes");
            var fullPath = System.IO.Path.Combine(scenesDir, normalized);
            if (!File.Exists(fullPath))
            {
                ImageCache[relativeArtPath] = null;
                return null;
            }

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            ImageCache[relativeArtPath] = bmp;
            return bmp;
        }
        catch
        {
            ImageCache[relativeArtPath] = null;
            return null;
        }
    }

    private static Brush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }
}
