using System.Runtime.CompilerServices;

namespace SimT.Contracts.Primitives;

/// <summary>
/// 2D point representation for vector paths and curve evaluation.
/// </summary>
public readonly record struct VectorPoint2D(float X, float Y)
{
    public static VectorPoint2D Zero => new(0f, 0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorPoint2D operator +(VectorPoint2D a, VectorPoint2D b) => new(a.X + b.X, a.Y + b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorPoint2D operator -(VectorPoint2D a, VectorPoint2D b) => new(a.X - b.X, a.Y - b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorPoint2D operator *(VectorPoint2D a, float scale) => new(a.X * scale, a.Y * scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float DistanceTo(VectorPoint2D other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

public enum PathCommandType : byte
{
    MoveTo = 0,
    LineTo = 1,
    QuadTo = 2,
    CubicTo = 3,
    Close = 4
}

/// <summary>
/// Sovereign 2D vector path supporting linear segments, quadratic &amp; cubic Bézier curves,
/// and adaptive error-bounded flattening for caravan, river, and army march visualizations.
/// </summary>
public sealed class BezierPath2D
{
    private readonly List<PathCommandType> _commands = new();
    private readonly List<VectorPoint2D> _points = new();

    public IReadOnlyList<PathCommandType> Commands => _commands;
    public IReadOnlyList<VectorPoint2D> Points => _points;

    public void MoveTo(float x, float y)
    {
        _commands.Add(PathCommandType.MoveTo);
        _points.Add(new VectorPoint2D(x, y));
    }

    public void LineTo(float x, float y)
    {
        _commands.Add(PathCommandType.LineTo);
        _points.Add(new VectorPoint2D(x, y));
    }

    public void QuadTo(float cx, float cy, float x, float y)
    {
        _commands.Add(PathCommandType.QuadTo);
        _points.Add(new VectorPoint2D(cx, cy));
        _points.Add(new VectorPoint2D(x, y));
    }

    public void CubicTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
    {
        _commands.Add(PathCommandType.CubicTo);
        _points.Add(new VectorPoint2D(c1x, c1y));
        _points.Add(new VectorPoint2D(c2x, c2y));
        _points.Add(new VectorPoint2D(x, y));
    }

    public void Close()
    {
        _commands.Add(PathCommandType.Close);
    }

    public void Clear()
    {
        _commands.Clear();
        _points.Clear();
    }

    /// <summary>
    /// Evaluates a point along a quadratic Bézier curve at time t in [0, 1].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorPoint2D EvaluateQuadratic(VectorPoint2D p0, VectorPoint2D p1, VectorPoint2D p2, float t)
    {
        float u = 1f - t;
        return (p0 * (u * u)) + (p1 * (2f * u * t)) + (p2 * (t * t));
    }

    /// <summary>
    /// Evaluates a point along a cubic Bézier curve at time t in [0, 1].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorPoint2D EvaluateCubic(VectorPoint2D p0, VectorPoint2D p1, VectorPoint2D p2, VectorPoint2D p3, float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        return (p0 * (uu * u)) + (p1 * (3f * uu * t)) + (p2 * (3f * u * tt)) + (p3 * (tt * t));
    }

    /// <summary>
    /// Adaptively flattens the path into contiguous linear polyline segments with sub-pixel tolerance.
    /// </summary>
    public List<List<VectorPoint2D>> Flatten(float tolerance = 0.5f)
    {
        if (tolerance < 0.05f) tolerance = 0.05f;

        var polylines = new List<List<VectorPoint2D>>();
        List<VectorPoint2D>? currentLine = null;

        VectorPoint2D current = VectorPoint2D.Zero;
        VectorPoint2D start = VectorPoint2D.Zero;
        int ptIdx = 0;

        for (int i = 0; i < _commands.Count; i++)
        {
            var cmd = _commands[i];
            switch (cmd)
            {
                case PathCommandType.MoveTo:
                    current = _points[ptIdx++];
                    start = current;
                    currentLine = new List<VectorPoint2D> { current };
                    polylines.Add(currentLine);
                    break;

                case PathCommandType.LineTo:
                    current = _points[ptIdx++];
                    currentLine ??= new List<VectorPoint2D> { current };
                    currentLine.Add(current);
                    break;

                case PathCommandType.QuadTo:
                    currentLine ??= new List<VectorPoint2D> { current };
                    var qCtrl = _points[ptIdx++];
                    var qEnd = _points[ptIdx++];
                    FlattenQuadratic(current, qCtrl, qEnd, tolerance, 0, currentLine);
                    current = qEnd;
                    break;

                case PathCommandType.CubicTo:
                    currentLine ??= new List<VectorPoint2D> { current };
                    var c1 = _points[ptIdx++];
                    var c2 = _points[ptIdx++];
                    var cEnd = _points[ptIdx++];
                    FlattenCubic(current, c1, c2, cEnd, tolerance, 0, currentLine);
                    current = cEnd;
                    break;

                case PathCommandType.Close:
                    if (currentLine != null && currentLine.Count > 0)
                    {
                        currentLine.Add(start);
                        current = start;
                    }
                    break;
            }
        }

        return polylines;
    }

    private static void FlattenQuadratic(
        VectorPoint2D p0, VectorPoint2D p1, VectorPoint2D p2,
        float tolerance, int depth, List<VectorPoint2D> output)
    {
        if (depth > 12)
        {
            output.Add(p2);
            return;
        }

        float dx = p2.X - p0.X;
        float dy = p2.Y - p0.Y;
        float dist = MathF.Abs((p1.X - p2.X) * dy - (p1.Y - p2.Y) * dx);

        if (dist * dist <= tolerance * tolerance * (dx * dx + dy * dy))
        {
            output.Add(p2);
            return;
        }

        var p01 = (p0 + p1) * 0.5f;
        var p12 = (p1 + p2) * 0.5f;
        var p012 = (p01 + p12) * 0.5f;

        FlattenQuadratic(p0, p01, p012, tolerance, depth + 1, output);
        FlattenQuadratic(p012, p12, p2, tolerance, depth + 1, output);
    }

    private static void FlattenCubic(
        VectorPoint2D p0, VectorPoint2D p1, VectorPoint2D p2, VectorPoint2D p3,
        float tolerance, int depth, List<VectorPoint2D> output)
    {
        if (depth > 12)
        {
            output.Add(p3);
            return;
        }

        var mid = (p0 + p3) * 0.5f;
        float d1 = p1.DistanceTo(mid);
        float d2 = p2.DistanceTo(mid);

        if (d1 + d2 <= tolerance)
        {
            output.Add(p3);
            return;
        }

        var p01 = (p0 + p1) * 0.5f;
        var p12 = (p1 + p2) * 0.5f;
        var p23 = (p2 + p3) * 0.5f;
        var p012 = (p01 + p12) * 0.5f;
        var p123 = (p12 + p23) * 0.5f;
        var p0123 = (p012 + p123) * 0.5f;

        FlattenCubic(p0, p01, p012, p0123, tolerance, depth + 1, output);
        FlattenCubic(p0123, p123, p23, p3, tolerance, depth + 1, output);
    }
}
