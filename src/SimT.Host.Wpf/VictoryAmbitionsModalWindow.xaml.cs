using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimT.Contracts.Playing;
using SimT.Core.Kernel;

namespace SimT.Host.Wpf;

public partial class VictoryAmbitionsModalWindow : HubModalWindowBase
{
    private readonly SimKernel _kernel;
    private readonly IEndgameAmbitions _ambitions;
    private EndgameAmbitionState _state;
    private AmbitionBranch _selectedBranch;

    public VictoryAmbitionsModalWindow(SimKernel kernel)
    {
        InitializeComponent();
        _kernel = kernel;

        _ambitions = _kernel.Capabilities.Get<IEndgameAmbitions>(CapabilityIds.EndgameAmbitions);

        _state = _ambitions.Evaluate();
        _selectedBranch = _state.Branches.FirstOrDefault() ?? throw new InvalidOperationException("No ambition branches found.");

        RenderAll();
    }

    private void RenderAll()
    {
        _state = _ambitions.Evaluate();

        var goldColor = Color.FromRgb(217, 119, 6);
        HubHeader.Configure(
            "👑",
            "KẾT CỤC ĐẠI NGHIỆP · ENDGAME AMBITIONS",
            "Thiên Cổ Lưu Danh · 5 Đại Chí Hướng",
            "Theo đuổi và hoàn thành các đại nghiệp để mở khóa Kết Cục Vinh Quang & Khắc Bia Sử Sách",
            "TIẾN ĐỘ ĐẠI NGHIỆP",
            $"{_state.CompletedCount} / 5 HOÀN THÀNH",
            goldColor);

        // Render left branch cards
        BranchesListPanel.Children.Clear();
        foreach (var branch in _state.Branches)
        {
            var card = BuildBranchCard(branch, branch.Category == _selectedBranch.Category);
            BranchesListPanel.Children.Add(card);
        }

        RenderSelectedDetail();
    }

    private UIElement BuildBranchCard(AmbitionBranch branch, bool isSelected)
    {
        var border = new Border
        {
            Background = (Brush)FindResource(isSelected ? "SurfaceAlt" : "Bg"),
            BorderBrush = (Brush)FindResource(isSelected ? "Accent" : "Line"),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8),
            Cursor = Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBlock = new TextBlock
        {
            Text = branch.Icon,
            FontSize = 22,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(iconBlock, 0);
        grid.Children.Add(iconBlock);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        var titleBlock = new TextBlock
        {
            Text = branch.TitleVi,
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Foreground = (Brush)FindResource(branch.IsFulfilled ? "Accent" : "Text")
        };
        titleRow.Children.Add(titleBlock);

        if (branch.IsFulfilled)
        {
            var winBadge = new Border
            {
                Background = (Brush)FindResource("SurfaceAlt"),
                BorderBrush = (Brush)FindResource("Accent"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(6, 0, 0, 0)
            };
            winBadge.Child = new TextBlock
            {
                Text = "ĐẠT THÀNH",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Accent")
            };
            titleRow.Children.Add(winBadge);
        }
        textStack.Children.Add(titleRow);

        var subBlock = new TextBlock
        {
            Text = branch.TitleEn,
            FontSize = 10,
            Foreground = (Brush)FindResource("Muted"),
            Margin = new Thickness(0, 1, 0, 4)
        };
        textStack.Children.Add(subBlock);

        // Progress bar in card
        var pb = new ProgressBar
        {
            Height = 4,
            Minimum = 0,
            Maximum = 100,
            Value = branch.Percent,
            Foreground = (Brush)FindResource(branch.IsFulfilled ? "Accent" : "Accent"),
            Background = (Brush)FindResource("Line"),
            BorderThickness = new Thickness(0)
        };
        textStack.Children.Add(pb);

        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);

        var pctBlock = new TextBlock
        {
            Text = $"{branch.Percent}%",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource(branch.IsFulfilled ? "Accent" : "Muted"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(pctBlock, 2);
        grid.Children.Add(pctBlock);

        border.Child = grid;

        border.MouseLeftButtonUp += (s, e) =>
        {
            _selectedBranch = branch;
            RenderAll();
        };

        return border;
    }

    private void RenderSelectedDetail()
    {
        SelectedGlyphText.Text = _selectedBranch.Icon;
        SelectedTitleText.Text = _selectedBranch.TitleVi;
        SelectedSubtitleText.Text = _selectedBranch.SubtitleVi;

        SelectedPercentText.Text = $"{_selectedBranch.Percent}%";
        SelectedProgressBar.Value = _selectedBranch.Percent;
        SelectedProgressBar.Foreground = (Brush)FindResource(_selectedBranch.IsFulfilled ? "Accent" : "Accent");

        if (_selectedBranch.IsFulfilled)
        {
            SelectedStatusText.Text = "ĐẠI NGHIỆP THÀNH CÔNG";
            SelectedStatusText.Foreground = (Brush)FindResource("Accent");
            SelectedStatusPill.BorderBrush = (Brush)FindResource("Accent");
        }
        else
        {
            SelectedStatusText.Text = $"{_selectedBranch.Percent}% HOÀN THÀNH";
            SelectedStatusText.Foreground = (Brush)FindResource("Accent");
            SelectedStatusPill.BorderBrush = (Brush)FindResource("Line");
        }

        // Requirements list
        RequirementsStackPanel.Children.Clear();
        foreach (var req in _selectedBranch.Requirements)
        {
            var reqRow = BuildRequirementRow(req);
            RequirementsStackPanel.Children.Add(reqRow);
        }

        // Epilogue Section
        EpilogueTitleText.Text = _selectedBranch.EpilogueTitleVi;
        EpilogueNarrativeText.Text = _selectedBranch.EpilogueNarrativeVi;

        if (_selectedBranch.IsFulfilled)
        {
            EpilogueBadge.Text = "ĐÃ MỞ KHÓA KẾT CỤC";
            EpilogueBadge.Foreground = (Brush)FindResource("Accent");
            EpilogueBox.BorderBrush = (Brush)FindResource("Accent");
            EpilogueBox.Opacity = 1.0;
            ChronicleRecordButton.Visibility = Visibility.Visible;
            ChronicleRecordButton.IsEnabled = true;
        }
        else
        {
            EpilogueBadge.Text = "CHƯA ĐẠT ĐIỀU KIỆN";
            EpilogueBadge.Foreground = (Brush)FindResource("Muted");
            EpilogueBox.BorderBrush = (Brush)FindResource("Line");
            EpilogueBox.Opacity = 0.85;
            ChronicleRecordButton.Visibility = Visibility.Collapsed;
        }
    }

    private UIElement BuildRequirementRow(AmbitionRequirement req)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("SurfaceAlt"),
            BorderBrush = (Brush)FindResource(req.IsMet ? "Gain" : "Line"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 6)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkBlock = new TextBlock
        {
            Text = req.IsMet ? "✓" : "○",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource(req.IsMet ? "Gain" : "Muted"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(checkBlock, 0);
        grid.Children.Add(checkBlock);

        var labelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var labelBlock = new TextBlock
        {
            Text = req.LabelVi,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource(req.IsMet ? "Text" : "Muted")
        };
        labelStack.Children.Add(labelBlock);

        var subLabel = new TextBlock
        {
            Text = req.LabelEn,
            FontSize = 10,
            Foreground = (Brush)FindResource("Muted")
        };
        labelStack.Children.Add(subLabel);

        Grid.SetColumn(labelStack, 1);
        grid.Children.Add(labelStack);

        var valBlock = new TextBlock
        {
            Text = $"{req.CurrentValue:N0} / {req.TargetValue:N0} {req.UnitVi}",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource(req.IsMet ? "Gain" : "Accent"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(valBlock, 2);
        grid.Children.Add(valBlock);

        border.Child = grid;
        return border;
    }

    private void ChronicleRecord_Click(object sender, RoutedEventArgs e)
    {
        ChronicleRecordButton.Label = "Đã Khắc Bia Vào Sử Sách ✓";
        ChronicleRecordButton.IsEnabled = false;

        NotifyHubStateChanged();
        MessageBox.Show(
            $"Đại nghiệp '{_selectedBranch.TitleVi}' đã được khắc bia vào biên niên sử thiên hạ!\n\n\"{_selectedBranch.EpilogueNarrativeVi}\"",
            "Thiên Cổ Lưu Danh",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
