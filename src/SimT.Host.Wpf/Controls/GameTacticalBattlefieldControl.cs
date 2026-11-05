using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SimT.Contracts.Playing;

namespace SimT.Host.Wpf.Controls;

/// <summary>
/// Hardware-accelerated, Direct3D-rendered Interactive Tactical Battlefield Control.
/// Visualizes army battalion formations (Cavalry wedge, Infantry shield-wall, Archer firing lines),
/// animates projectile arrow volleys, cavalry charges, weapon clash sparks, and martial skill shockwaves.
/// </summary>
public sealed class GameTacticalBattlefieldControl : FrameworkElement
{
    private struct UnitNode
    {
        public double BaseX;
        public double BaseY;
        public double OffsetX;
        public double OffsetY;
        public double Phase;
        public bool IsAlive;
    }

    private struct Arrow
    {
        public double StartX, StartY;
        public double TargetX, TargetY;
        public double Progress; // 0 to 1
        public double ArcHeight;
        public bool IsActive;
    }

    private struct Spark
    {
        public double X, Y;
        public double Vx, Vy;
        public double Life;
        public double Size;
    }

    private readonly UnitNode[] _playerUnits = new UnitNode[14];
    private readonly UnitNode[] _banditUnits = new UnitNode[14];
    private readonly Arrow[] _arrows = new Arrow[8];
    private readonly Spark[] _sparks = new Spark[16];
    private readonly Random _rng = new();

    private TroopBranch _playerBranch = TroopBranch.Cavalry;
    private TroopBranch _banditBranch = TroopBranch.Infantry;
    private string _commanderName = "Lữ Khách";
    private string _chiefName = "Trương Yến";

    private bool _isAnimatingRound;
    private double _roundAnimProgress; // 0.0 to 1.0
    private string? _currentSkillName;
    private Action? _onRoundComplete;
    private bool _isRendering;

    // Cached brushes & pens
    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x1D));
    private static readonly Pen GridLinePen = new(new SolidColorBrush(Color.FromRgb(0x1A, 0x22, 0x30)), 1.0)
    {
        DashStyle = DashStyles.Dash
    };
    private static readonly Pen ClashFrontierPen = new(new SolidColorBrush(Color.FromRgb(0x3B, 0x48, 0x5D)), 1.5)
    {
        DashStyle = DashStyles.Dot
    };
    private static readonly Brush PlayerUnitBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)); // Gain Green
    private static readonly Brush BanditUnitBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // Loss Red
    private static readonly Brush SkillAuraBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xD9, 0xA4, 0x41));
    private static readonly Pen SkillAuraPen = new(new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), 2.0);
    private static readonly Pen ArrowPen = new(new SolidColorBrush(Color.FromRgb(0xFA, 0xCC, 0x15)), 1.5);
    private static readonly Brush SparkBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush TextLightBrush = new SolidColorBrush(Color.FromRgb(0xEB, 0xF0, 0xF7));
    private static readonly Brush TextMutedBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x97, 0xA8));

    static GameTacticalBattlefieldControl()
    {
        BgBrush.Freeze();
        GridLinePen.Freeze();
        ClashFrontierPen.Freeze();
        PlayerUnitBrush.Freeze();
        BanditUnitBrush.Freeze();
        SkillAuraBrush.Freeze();
        SkillAuraPen.Freeze();
        ArrowPen.Freeze();
        SparkBrush.Freeze();
        TextLightBrush.Freeze();
        TextMutedBrush.Freeze();
    }

    public GameTacticalBattlefieldControl()
    {
        Height = 160;
        MinHeight = 140;
        ClipToBounds = true;

        InitUnits();

        Loaded += (_, _) => StartRenderLoop();
        Unloaded += (_, _) => StopRenderLoop();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) StartRenderLoop();
            else StopRenderLoop();
        };
    }

    public void ConfigureArmies(
        TroopBranch playerBranch,
        TroopBranch banditBranch,
        string commanderName,
        string chiefName)
    {
        _playerBranch = playerBranch;
        _banditBranch = banditBranch;
        _commanderName = commanderName;
        _chiefName = chiefName;

        LayoutBattalions();
        InvalidateVisual();
    }

    private void StartRenderLoop()
    {
        if (_isRendering) return;
        _isRendering = true;
        CompositionTarget.Rendering += OnRenderTick;
    }

    private void StopRenderLoop()
    {
        if (!_isRendering) return;
        _isRendering = false;
        CompositionTarget.Rendering -= OnRenderTick;
    }

    private void InitUnits()
    {
        for (var i = 0; i < _playerUnits.Length; i++)
        {
            _playerUnits[i].IsAlive = true;
            _playerUnits[i].Phase = _rng.NextDouble() * Math.PI * 2;
        }

        for (var i = 0; i < _banditUnits.Length; i++)
        {
            _banditUnits[i].IsAlive = true;
            _banditUnits[i].Phase = _rng.NextDouble() * Math.PI * 2;
        }
    }

    private void LayoutBattalions()
    {
        var w = ActualWidth > 100 ? ActualWidth : 500;
        var h = ActualHeight > 50 ? ActualHeight : 160;
        var centerY = h / 2.0;

        // Player battalion: Left side
        var pCenterX = w * 0.22;
        for (var i = 0; i < _playerUnits.Length; i++)
        {
            ref var u = ref _playerUnits[i];
            if (_playerBranch == TroopBranch.Cavalry)
            {
                // Wedge Arrowhead
                var row = i / 3;
                var col = i % 3;
                u.BaseX = pCenterX + (row * 18.0) - 15;
                u.BaseY = centerY + (col - 1) * (18.0 + row * 6.0);
            }
            else if (_playerBranch == TroopBranch.Archers)
            {
                // Staggered firing line (further back)
                var col = i % 7;
                var row = i / 7;
                u.BaseX = pCenterX - 25 + row * 16.0;
                u.BaseY = centerY + (col - 3) * 16.0;
            }
            else
            {
                // Phalanx Shield Wall (rectangular tight block)
                var col = i % 4;
                var row = i / 4;
                u.BaseX = pCenterX + row * 16.0;
                u.BaseY = centerY + (col - 1.5) * 18.0;
            }
        }

        // Bandit battalion: Right side
        var bCenterX = w * 0.78;
        for (var i = 0; i < _banditUnits.Length; i++)
        {
            ref var u = ref _banditUnits[i];
            var col = i % 4;
            var row = i / 4;
            u.BaseX = bCenterX - row * 16.0;
            u.BaseY = centerY + (col - 1.5) * 18.0 + (row % 2 == 1 ? 8 : -8);
        }
    }

    public void PlayRoundAnimation(BanditBattleRoundLog log, Action onComplete)
    {
        _isAnimatingRound = true;
        _roundAnimProgress = 0.0;
        _currentSkillName = log.SkillTriggeredVi;
        _onRoundComplete = onComplete;

        // Spawn flying arrows if archers involved
        if (_playerBranch == TroopBranch.Archers || _banditBranch == TroopBranch.Archers)
        {
            var fromLeft = _playerBranch == TroopBranch.Archers;
            var startX = fromLeft ? ActualWidth * 0.25 : ActualWidth * 0.75;
            var targetX = fromLeft ? ActualWidth * 0.75 : ActualWidth * 0.25;
            var centerY = ActualHeight / 2.0;

            for (var i = 0; i < _arrows.Length; i++)
            {
                _arrows[i].StartX = startX + (_rng.NextDouble() - 0.5) * 20;
                _arrows[i].StartY = centerY + (_rng.NextDouble() - 0.5) * 40;
                _arrows[i].TargetX = targetX + (_rng.NextDouble() - 0.5) * 30;
                _arrows[i].TargetY = centerY + (_rng.NextDouble() - 0.5) * 50;
                _arrows[i].Progress = 0.0;
                _arrows[i].ArcHeight = 25.0 + _rng.NextDouble() * 15.0;
                _arrows[i].IsActive = true;
            }
        }
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        // 1. Idle breathing pulse for troops
        for (var i = 0; i < _playerUnits.Length; i++)
        {
            ref var u = ref _playerUnits[i];
            u.Phase += 0.05;
            u.OffsetY = Math.Sin(u.Phase) * 1.5;
        }

        for (var i = 0; i < _banditUnits.Length; i++)
        {
            ref var u = ref _banditUnits[i];
            u.Phase += 0.05;
            u.OffsetY = Math.Sin(u.Phase) * 1.5;
        }

        // 2. Round Animation Progression
        if (_isAnimatingRound)
        {
            _roundAnimProgress += 0.035;

            var w = ActualWidth;
            var pClashDist = w * 0.22;
            var bClashDist = -w * 0.22;

            if (_roundAnimProgress < 0.4)
            {
                // Phase 1: Charge forward towards center
                var t = _roundAnimProgress / 0.4;
                var ease = t * t;
                for (var i = 0; i < _playerUnits.Length; i++) _playerUnits[i].OffsetX = ease * pClashDist;
                for (var i = 0; i < _banditUnits.Length; i++) _banditUnits[i].OffsetX = ease * bClashDist;

                // Move arrows
                for (var i = 0; i < _arrows.Length; i++)
                {
                    if (_arrows[i].IsActive) _arrows[i].Progress = Math.Clamp(t * 1.2, 0, 1);
                }
            }
            else if (_roundAnimProgress < 0.7)
            {
                // Phase 2: Impact clash & spark generation
                if (_sparks[0].Life <= 0)
                {
                    SpawnClashSparks(w / 2.0, ActualHeight / 2.0);
                }
                UpdateSparks();
            }
            else if (_roundAnimProgress < 1.0)
            {
                // Phase 3: Recoil back to formation
                var t = (_roundAnimProgress - 0.7) / 0.3;
                var recoil = 1.0 - t;
                for (var i = 0; i < _playerUnits.Length; i++) _playerUnits[i].OffsetX = recoil * pClashDist;
                for (var i = 0; i < _banditUnits.Length; i++) _banditUnits[i].OffsetX = recoil * bClashDist;
                UpdateSparks();
            }
            else
            {
                // Round Finished
                _isAnimatingRound = false;
                _currentSkillName = null;
                for (var i = 0; i < _playerUnits.Length; i++) _playerUnits[i].OffsetX = 0;
                for (var i = 0; i < _banditUnits.Length; i++) _banditUnits[i].OffsetX = 0;
                for (var i = 0; i < _arrows.Length; i++) _arrows[i].IsActive = false;

                _onRoundComplete?.Invoke();
                _onRoundComplete = null;
            }
        }

        InvalidateVisual();
    }

    private void SpawnClashSparks(double cx, double cy)
    {
        for (var i = 0; i < _sparks.Length; i++)
        {
            var angle = _rng.NextDouble() * Math.PI * 2;
            var speed = 1.5 + _rng.NextDouble() * 3.5;
            _sparks[i].X = cx + (_rng.NextDouble() - 0.5) * 20;
            _sparks[i].Y = cy + (_rng.NextDouble() - 0.5) * 30;
            _sparks[i].Vx = Math.Cos(angle) * speed;
            _sparks[i].Vy = Math.Sin(angle) * speed;
            _sparks[i].Life = 1.0;
            _sparks[i].Size = 2.0 + _rng.NextDouble() * 3.0;
        }
    }

    private void UpdateSparks()
    {
        for (var i = 0; i < _sparks.Length; i++)
        {
            if (_sparks[i].Life <= 0) continue;
            _sparks[i].X += _sparks[i].Vx;
            _sparks[i].Y += _sparks[i].Vy;
            _sparks[i].Life -= 0.08;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 80 || h <= 40) return;

        // 1. Draw Battlefield Terrain Deck
        dc.DrawRoundedRectangle(BgBrush, null, new Rect(0, 0, w, h), 8, 8);

        // Perspective terrain floor lines
        var centerY = h / 2.0;
        dc.DrawLine(GridLinePen, new Point(0, centerY - 35), new Point(w, centerY - 35));
        dc.DrawLine(GridLinePen, new Point(0, centerY + 35), new Point(w, centerY + 35));

        // Center Frontier Clash Line
        dc.DrawLine(ClashFrontierPen, new Point(w / 2.0, 10), new Point(w / 2.0, h - 10));

        // 2. Draw Commander Flag Banners
        DrawCommanderFlag(dc, new Point(w * 0.10, centerY - 48), _commanderName, TroopBranchHelper.Icon(_playerBranch), isPlayer: true);
        DrawCommanderFlag(dc, new Point(w * 0.90, centerY - 48), _chiefName, TroopBranchHelper.Icon(_banditBranch), isPlayer: false);

        // 3. Draw Player Battalion Units
        var pGlyph = TroopBranchHelper.Icon(_playerBranch);
        for (var i = 0; i < _playerUnits.Length; i++)
        {
            ref readonly var u = ref _playerUnits[i];
            if (!u.IsAlive) continue;

            var pt = new Point(u.BaseX + u.OffsetX, u.BaseY + u.OffsetY);
            dc.DrawEllipse(PlayerUnitBrush, null, pt, 5.5, 5.5);
        }

        // 4. Draw Bandit Battalion Units
        for (var i = 0; i < _banditUnits.Length; i++)
        {
            ref readonly var u = ref _banditUnits[i];
            if (!u.IsAlive) continue;

            var pt = new Point(u.BaseX + u.OffsetX, u.BaseY + u.OffsetY);
            dc.DrawEllipse(BanditUnitBrush, null, pt, 5.5, 5.5);
        }

        // 5. Draw Flying Projectile Arrows
        for (var i = 0; i < _arrows.Length; i++)
        {
            ref readonly var a = ref _arrows[i];
            if (!a.IsActive || a.Progress <= 0 || a.Progress >= 1.0) continue;

            var curX = a.StartX + (a.TargetX - a.StartX) * a.Progress;
            var arcY = Math.Sin(a.Progress * Math.PI) * a.ArcHeight;
            var curY = a.StartY + (a.TargetY - a.StartY) * a.Progress - arcY;

            dc.DrawEllipse(ArrowPen.Brush, null, new Point(curX, curY), 2.5, 2.5);
        }

        // 6. Draw Clash Sparks
        for (var i = 0; i < _sparks.Length; i++)
        {
            ref readonly var s = ref _sparks[i];
            if (s.Life <= 0) continue;

            var alpha = (byte)(s.Life * 255);
            var b = new SolidColorBrush(Color.FromArgb(alpha, 0xF5, 0x9E, 0x0B));
            dc.DrawEllipse(b, null, new Point(s.X, s.Y), s.Size, s.Size);
        }

        // 7. Draw Skill Radiance Aura (if skill triggered during clash)
        if (!string.IsNullOrEmpty(_currentSkillName) && _roundAnimProgress >= 0.35 && _roundAnimProgress <= 0.85)
        {
            var auraRadius = 38.0 + Math.Sin((_roundAnimProgress - 0.35) * Math.PI / 0.5) * 22.0;
            dc.DrawEllipse(SkillAuraBrush, SkillAuraPen, new Point(w / 2.0, centerY), auraRadius, auraRadius);

            var skillText = new FormattedText($"⚡ {_currentSkillName}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                13,
                SkillAuraPen.Brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(skillText, new Point((w - skillText.Width) / 2.0, centerY - 45));
        }
    }

    private void DrawCommanderFlag(DrawingContext dc, Point pt, string name, string icon, bool isPlayer)
    {
        var flagBrush = isPlayer ? PlayerUnitBrush : BanditUnitBrush;
        var flagPen = new Pen(flagBrush, 1.5);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x30, 0x1F, 0x29, 0x37)), flagPen, new Rect(pt.X - 32, pt.Y - 14, 64, 28), 4, 4);

        var glyph = new FormattedText($"{icon} {name}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            10.5,
            TextLightBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(glyph, new Point(pt.X - (glyph.Width / 2.0), pt.Y - 7));
    }
}
