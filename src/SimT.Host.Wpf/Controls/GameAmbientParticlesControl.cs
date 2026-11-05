using System.Windows;
using System.Windows.Media;

namespace SimT.Host.Wpf.Controls;

public enum AmbientParticleKind
{
    BattleEmbers,
    StrollPetals
}

/// <summary>
/// Hardware-accelerated, zero-allocation ambient particle canvas for RPG atmosphere.
/// Renders battle embers or drifting stroll petals using Direct3D DrawingContext
/// with fixed-array pre-allocated structs and clean Unloaded lifecycle binding.
/// </summary>
public sealed class GameAmbientParticlesControl : FrameworkElement
{
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(AmbientParticleKind),
            typeof(GameAmbientParticlesControl),
            new FrameworkPropertyMetadata(AmbientParticleKind.BattleEmbers, FrameworkPropertyMetadataOptions.AffectsRender));

    public AmbientParticleKind Kind
    {
        get => (AmbientParticleKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private struct Particle
    {
        public double X;
        public double Y;
        public double Vx;
        public double Vy;
        public double Size;
        public double Life;
        public double Decay;
        public double Phase;
    }

    private readonly Particle[] _particles;
    private readonly Random _rng = new();
    private bool _isRendering;

    // Cached pens & brushes
    private static readonly Brush EmberBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush EmberCoreBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
    private static readonly Brush PetalBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x72, 0xB6));
    private static readonly Brush PetalGoldBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0xA4, 0x41));

    static GameAmbientParticlesControl()
    {
        EmberBrush.Freeze();
        EmberCoreBrush.Freeze();
        PetalBrush.Freeze();
        PetalGoldBrush.Freeze();
    }

    public GameAmbientParticlesControl(AmbientParticleKind kind, int count = 18)
    {
        Kind = kind;
        _particles = new Particle[Math.Clamp(count, 8, 32)];
        IsHitTestVisible = false;
        ClipToBounds = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public GameAmbientParticlesControl() : this(AmbientParticleKind.BattleEmbers)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitParticles();
        StartRenderLoop();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopRenderLoop();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            StartRenderLoop();
        }
        else
        {
            StopRenderLoop();
        }
    }

    private void StartRenderLoop()
    {
        if (_isRendering) return;
        _isRendering = true;
        CompositionTarget.Rendering += OnCompositionRendering;
    }

    private void StopRenderLoop()
    {
        if (!_isRendering) return;
        _isRendering = false;
        CompositionTarget.Rendering -= OnCompositionRendering;
    }

    private void InitParticles()
    {
        var w = ActualWidth > 10 ? ActualWidth : 800;
        var h = ActualHeight > 10 ? ActualHeight : 600;

        for (var i = 0; i < _particles.Length; i++)
        {
            ResetParticle(ref _particles[i], w, h, initialScatter: true);
        }
    }

    private void ResetParticle(ref Particle p, double w, double h, bool initialScatter = false)
    {
        p.Phase = _rng.NextDouble() * Math.PI * 2;
        p.Decay = 0.004 + _rng.NextDouble() * 0.007;
        p.Life = initialScatter ? _rng.NextDouble() : 1.0;

        if (Kind == AmbientParticleKind.BattleEmbers)
        {
            // Embers rise from bottom
            p.X = _rng.NextDouble() * w;
            p.Y = initialScatter ? _rng.NextDouble() * h : (h + _rng.NextDouble() * 20);
            p.Vx = (_rng.NextDouble() - 0.5) * 0.8;
            p.Vy = -0.7 - _rng.NextDouble() * 1.4;
            p.Size = 2.0 + _rng.NextDouble() * 3.5;
        }
        else
        {
            // Petals drift down from top
            p.X = _rng.NextDouble() * w;
            p.Y = initialScatter ? _rng.NextDouble() * h : (-10 - _rng.NextDouble() * 20);
            p.Vx = 0.4 + _rng.NextDouble() * 0.9;
            p.Vy = 0.5 + _rng.NextDouble() * 1.1;
            p.Size = 3.5 + _rng.NextDouble() * 4.5;
        }
    }

    private void OnCompositionRendering(object? sender, EventArgs e)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        for (var i = 0; i < _particles.Length; i++)
        {
            ref var p = ref _particles[i];
            p.Phase += 0.03;
            p.X += p.Vx + Math.Sin(p.Phase) * 0.4;
            p.Y += p.Vy;
            p.Life -= p.Decay;

            if (p.Life <= 0 || p.Y < -30 || p.Y > h + 30 || p.X < -30 || p.X > w + 30)
            {
                ResetParticle(ref p, w, h);
            }
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        for (var i = 0; i < _particles.Length; i++)
        {
            ref readonly var p = ref _particles[i];
            if (p.Life <= 0) continue;

            var alpha = (byte)(Math.Clamp(p.Life, 0.0, 1.0) * 190);
            if (alpha < 5) continue;

            if (Kind == AmbientParticleKind.BattleEmbers)
            {
                var emberColor = (p.Life > 0.5)
                    ? Color.FromArgb(alpha, 0xF5, 0x9E, 0x0B)
                    : Color.FromArgb(alpha, 0xEF, 0x44, 0x44);

                var brush = new SolidColorBrush(emberColor);
                dc.DrawEllipse(brush, null, new Point(p.X, p.Y), p.Size, p.Size);
            }
            else
            {
                var petalColor = (i % 3 == 0)
                    ? Color.FromArgb(alpha, 0xD9, 0xA4, 0x41)
                    : Color.FromArgb(alpha, 0xF4, 0x72, 0xB6);

                var brush = new SolidColorBrush(petalColor);
                dc.PushTransform(new RotateTransform(p.Phase * 25, p.X, p.Y));
                dc.DrawEllipse(brush, null, new Point(p.X, p.Y), p.Size, p.Size * 0.55);
                dc.Pop();
            }
        }
    }
}
