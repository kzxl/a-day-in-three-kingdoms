using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

public sealed record LineageChildNode(
    long ChildId,
    string NameVi,
    int Age,
    string Gender,
    bool IsDesignatedHeir,
    string FocusVi,
    string TutorVi,
    int PotentialScore);

/// <summary>
/// Hardware-accelerated Genealogical Ancestry & Clan Lineage Pedigree Tree Canvas.
/// Renders Clan Heritage (Từ Đường Gia Tộc), Primary Couple, Lineage Tree branches,
/// and Designated Heir (Thế Tử Kế Thừa) with golden crowns and mentor assignments via Direct3D.
/// </summary>
public sealed class GameLineagePedigreeControl : FrameworkElement
{
    private string _clanName = "Gia Tộc Họ Vũ";
    private int _clanPrestige = 1250;
    private string _clanRankVi = "Hào Tộc Danh Môn";
    private string _clanMottoVi = "Trung Quân Ái Quốc · Bách Thế Lưu Phương";

    private string _fatherName = "Vũ Đình Khiêm";
    private string _fatherTitle = "Gia Chủ · Thống Soái";
    private string _motherName = "Tiêu Nguyệt Hoa";
    private string _motherTitle = "Chính Thất Phu Nhân";

    private readonly List<LineageChildNode> _children = new();
    private int _hoveredChildIndex = -1;

    // Direct3D Cached Brushes and Pens
    private static readonly Brush PanelBgBrush;
    private static readonly Brush BannerBgBrush;
    private static readonly Pen BannerBorderPen;
    private static readonly Brush ParentCardBgBrush;
    private static readonly Pen ParentBorderPen;
    private static readonly Brush ChildCardBgBrush;
    private static readonly Pen ChildBorderPen;
    private static readonly Pen HeirBorderPen;
    private static readonly Brush HeirBadgeBrush;
    private static readonly Pen LineageBranchPen;
    private static readonly Brush TextWhiteBrush;
    private static readonly Brush TextMutedBrush;
    private static readonly Brush GoldBrush;
    private static readonly Brush EmeraldBrush;

    public event Action<LineageChildNode>? ChildSelected;

    static GameLineagePedigreeControl()
    {
        PanelBgBrush = new RadialGradientBrush(
            Color.FromRgb(0x13, 0x1A, 0x26),
            Color.FromRgb(0x0C, 0x11, 0x18))
        {
            Center = new Point(0.5, 0.4),
            GradientOrigin = new Point(0.5, 0.4),
            RadiusX = 0.8,
            RadiusY = 0.8
        };
        PanelBgBrush.Freeze();

        BannerBgBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x23, 0x34));
        BannerBgBrush.Freeze();

        BannerBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x37, 0x4D)), 1.0);
        BannerBorderPen.Freeze();

        ParentCardBgBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x32));
        ParentCardBgBrush.Freeze();

        ParentBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)), 1.5);
        ParentBorderPen.Freeze();

        ChildCardBgBrush = new SolidColorBrush(Color.FromRgb(0x15, 0x1C, 0x28));
        ChildCardBgBrush.Freeze();

        ChildBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x27, 0x36, 0x4B)), 1.2);
        ChildBorderPen.Freeze();

        HeirBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), 2.0);
        HeirBorderPen.Freeze();

        HeirBadgeBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        HeirBadgeBrush.Freeze();

        LineageBranchPen = new Pen(new SolidColorBrush(Color.FromArgb(0x90, 0xF5, 0x9E, 0x0B)), 1.8);
        LineageBranchPen.Freeze();

        TextWhiteBrush = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF7));
        TextWhiteBrush.Freeze();

        TextMutedBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xA8));
        TextMutedBrush.Freeze();

        GoldBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        GoldBrush.Freeze();

        EmeraldBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        EmeraldBrush.Freeze();
    }

    public GameLineagePedigreeControl()
    {
        MinHeight = 360;
        ClipToBounds = true;
        Cursor = Cursors.Hand;

        MouseMove += OnCanvasMouseMove;
        MouseLeave += OnCanvasMouseLeave;
        MouseDown += OnCanvasMouseDown;
    }

    public void SetLineage(
        string clanName,
        int prestige,
        string clanRank,
        string motto,
        string fatherName,
        string fatherTitle,
        string motherName,
        string motherTitle,
        IEnumerable<LineageChildNode> children)
    {
        _clanName = clanName;
        _clanPrestige = prestige;
        _clanRankVi = clanRank;
        _clanMottoVi = motto;
        _fatherName = fatherName;
        _fatherTitle = fatherTitle;
        _motherName = motherName;
        _motherTitle = motherTitle;

        _children.Clear();
        _children.AddRange(children);
        InvalidateVisual();
    }

    private Rect GetChildRect(int index, double startX, double spacing, double y, double cardW, double cardH)
    {
        var x = startX + index * (cardW + spacing);
        return new Rect(x, y, cardW, cardH);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 100 || h <= 100) return;

        // Background
        dc.DrawRoundedRectangle(PanelBgBrush, null, new Rect(0, 0, w, h), 8, 8);

        // 1. Ancestor Clan Banner (Từ Đường Gia Tộc)
        var bannerRect = new Rect(14, 12, w - 28, 48);
        dc.DrawRoundedRectangle(BannerBgBrush, BannerBorderPen, bannerRect, 6, 6);

        DrawText(dc, "🏛️ TỪ ĐƯỜNG GIA TỘC", 11, bannerRect.Left + 12, bannerRect.Top + 8, GoldBrush, bold: true);
        DrawText(dc, $"{_clanName} · {_clanRankVi} (Uy Vọng: {_clanPrestige:N0})", 12.5, bannerRect.Left + 12, bannerRect.Top + 24, TextWhiteBrush, bold: true);
        DrawText(dc, $"Gia Huấn: \"{_clanMottoVi}\"", 10, bannerRect.Right - 280, bannerRect.Top + 16, TextMutedBrush);

        // 2. Parents Level (Chủ Công & Chính Thất)
        var parentCardW = 160.0;
        var parentCardH = 56.0;
        var cx = w / 2.0;
        var parentY = 76.0;

        var fatherRect = new Rect(cx - parentCardW - 14, parentY, parentCardW, parentCardH);
        var motherRect = new Rect(cx + 14, parentY, parentCardW, parentCardH);

        // Father Card
        dc.DrawRoundedRectangle(ParentCardBgBrush, ParentBorderPen, fatherRect, 6, 6);
        DrawText(dc, "👑 " + _fatherName, 12, fatherRect.Left + 10, fatherRect.Top + 10, TextWhiteBrush, bold: true);
        DrawText(dc, _fatherTitle, 10, fatherRect.Left + 10, fatherRect.Top + 30, GoldBrush);

        // Mother Card
        dc.DrawRoundedRectangle(ParentCardBgBrush, ParentBorderPen, motherRect, 6, 6);
        DrawText(dc, "❤️ " + _motherName, 12, motherRect.Left + 10, motherRect.Top + 10, TextWhiteBrush, bold: true);
        DrawText(dc, _motherTitle, 10, motherRect.Left + 10, motherRect.Top + 30, TextMutedBrush);

        // Marriage Link between Father and Mother
        dc.DrawLine(LineageBranchPen, new Point(fatherRect.Right, parentY + parentCardH / 2.0), new Point(motherRect.Left, parentY + parentCardH / 2.0));

        // Center Spine down to Children
        var midX = cx;
        var midY = parentY + parentCardH / 2.0;
        var forkY = midY + 40.0;
        dc.DrawLine(LineageBranchPen, new Point(midX, midY), new Point(midX, forkY));

        // 3. Children Branch
        if (_children.Count == 0)
        {
            DrawCenteredText(dc, "Gia môn chưa có hậu duệ kế thừa. Hãy nạp thê thiếp hoặc sinh con dưỡng cái.", 12, cx, forkY + 40, TextMutedBrush);
            return;
        }

        var childCardW = Math.Min(190.0, (w - 40.0) / _children.Count - 12.0);
        childCardW = Math.Max(140.0, childCardW);
        var childCardH = 120.0;
        var spacing = 12.0;
        var totalRowW = _children.Count * childCardW + (_children.Count - 1) * spacing;
        var startX = Math.Max(14.0, (w - totalRowW) / 2.0);
        var childrenY = forkY + 28.0;

        // Horizontal Branch Bar
        var firstChildX = startX + childCardW / 2.0;
        var lastChildX = startX + (_children.Count - 1) * (childCardW + spacing) + childCardW / 2.0;
        dc.DrawLine(LineageBranchPen, new Point(firstChildX, forkY), new Point(lastChildX, forkY));

        // Render each Child Card
        for (var i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            var cRect = GetChildRect(i, startX, spacing, childrenY, childCardW, childCardH);
            var cardCenterX = cRect.Left + childCardW / 2.0;

            // Vertical feeder line from branch
            dc.DrawLine(LineageBranchPen, new Point(cardCenterX, forkY), new Point(cardCenterX, cRect.Top));

            var isHovered = (i == _hoveredChildIndex);
            var borderPen = child.IsDesignatedHeir ? HeirBorderPen : (isHovered ? ParentBorderPen : ChildBorderPen);

            dc.DrawRoundedRectangle(ChildCardBgBrush, borderPen, cRect, 6, 6);

            // Heir Ribbon / Crown if heir
            if (child.IsDesignatedHeir)
            {
                var heirBadgeRect = new Rect(cRect.Right - 66, cRect.Top + 6, 58, 16);
                dc.DrawRoundedRectangle(HeirBadgeBrush, null, heirBadgeRect, 3, 3);
                DrawCenteredText(dc, "👑 THẾ TỬ", 9, heirBadgeRect.Left + 29, heirBadgeRect.Top + 8, Brushes.Black, bold: true);
            }

            var genderGlyph = child.Gender == "Nam" ? "👦" : "👧";
            DrawText(dc, $"{genderGlyph} {child.NameVi}", 11.5, cRect.Left + 10, cRect.Top + 10, TextWhiteBrush, bold: true);
            DrawText(dc, $"{child.Age} tuổi · {child.Gender}", 9.5, cRect.Left + 10, cRect.Top + 30, TextMutedBrush);

            // Focus & Potential
            DrawText(dc, $"Định Hướng: {child.FocusVi}", 9.5, cRect.Left + 10, cRect.Top + 50, GoldBrush);
            DrawText(dc, $"Sư Phụ: {child.TutorVi}", 9.5, cRect.Left + 10, cRect.Top + 68, EmeraldBrush);

            // Potential Bar
            var barW = cRect.Width - 20;
            var barH = 5.0;
            var barX = cRect.Left + 10;
            var barY = cRect.Top + 92;
            dc.DrawRoundedRectangle(BannerBgBrush, null, new Rect(barX, barY, barW, barH), 2, 2);
            var pct = Math.Clamp(child.PotentialScore / 100.0, 0.05, 1.0);
            dc.DrawRoundedRectangle(EmeraldBrush, null, new Rect(barX, barY, barW * pct, barH), 2, 2);
            DrawText(dc, $"Tư Chất: {child.PotentialScore}/100", 8.5, barX, barY + 7, TextMutedBrush);
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var pt = e.GetPosition(this);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0 || _children.Count == 0) return;

        var childCardW = Math.Min(190.0, (w - 40.0) / _children.Count - 12.0);
        childCardW = Math.Max(140.0, childCardW);
        var childCardH = 120.0;
        var spacing = 12.0;
        var totalRowW = _children.Count * childCardW + (_children.Count - 1) * spacing;
        var startX = Math.Max(14.0, (w - totalRowW) / 2.0);
        var childrenY = 76.0 + 56.0 / 2.0 + 40.0 + 28.0;

        var oldHover = _hoveredChildIndex;
        _hoveredChildIndex = -1;

        for (var i = 0; i < _children.Count; i++)
        {
            var r = GetChildRect(i, startX, spacing, childrenY, childCardW, childCardH);
            if (r.Contains(pt))
            {
                _hoveredChildIndex = i;
                break;
            }
        }

        if (oldHover != _hoveredChildIndex)
        {
            InvalidateVisual();
        }
    }

    private void OnCanvasMouseLeave(object sender, MouseEventArgs e)
    {
        if (_hoveredChildIndex != -1)
        {
            _hoveredChildIndex = -1;
            InvalidateVisual();
        }
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_hoveredChildIndex >= 0 && _hoveredChildIndex < _children.Count)
        {
            ChildSelected?.Invoke(_children[_hoveredChildIndex]);
        }
    }

    private void DrawCenteredText(DrawingContext dc, string text, double size, double cx, double cy, Brush brush, bool bold = false)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(ft, new Point(cx - ft.Width / 2.0, cy - ft.Height / 2.0));
    }

    private void DrawText(DrawingContext dc, string text, double size, double x, double y, Brush brush, bool bold = false)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(ft, new Point(x, y));
    }
}
