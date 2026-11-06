using SimT.Contracts.Primitives;

namespace SimT.Contracts.Objects;

/// <summary>
/// 2D Transform component defining local and hierarchical world coordinates, rotation, and scale.
/// </summary>
public sealed class Transform2D : ComponentBase, ISpatialItem2D
{
    public VectorPoint2D LocalPosition { get; set; } = VectorPoint2D.Zero;
    public float LocalRotation { get; set; } = 0f;
    public VectorPoint2D LocalScale { get; set; } = new(1f, 1f);

    public VectorPoint2D WorldPosition
    {
        get
        {
            if (GameObject?.Parent?.Transform is { } parentTransform)
            {
                return parentTransform.WorldPosition + LocalPosition;
            }
            return LocalPosition;
        }
        set
        {
            if (GameObject?.Parent?.Transform is { } parentTransform)
            {
                LocalPosition = value - parentTransform.WorldPosition;
            }
            else
            {
                LocalPosition = value;
            }
        }
    }

    /// <summary>
    /// Bounding box in world coordinates for spatial index integration (<see cref="QuadTree2D{T}"/>).
    /// </summary>
    public BoundingBox2D Bounds
    {
        get
        {
            var pos = WorldPosition;
            var halfW = MathF.Abs(LocalScale.X) * 0.5f;
            var halfH = MathF.Abs(LocalScale.Y) * 0.5f;
            return new BoundingBox2D(pos.X - halfW, pos.Y - halfH, pos.X + halfW, pos.Y + halfH);
        }
    }
}

/// <summary>
/// 2D AABB Collider component for mouse picking, hover, and intersection tests.
/// </summary>
public sealed class Collider2D : ComponentBase
{
    public VectorPoint2D Size { get; set; } = new(32f, 32f);
    public VectorPoint2D Offset { get; set; } = VectorPoint2D.Zero;
    public bool IsTrigger { get; set; } = false;

    public BoundingBox2D Bounds
    {
        get
        {
            var center = (GameObject?.Transform.WorldPosition ?? VectorPoint2D.Zero) + Offset;
            var halfW = Size.X * 0.5f;
            var halfH = Size.Y * 0.5f;
            return new BoundingBox2D(center.X - halfW, center.Y - halfH, center.X + halfW, center.Y + halfH);
        }
    }

    public bool ContainsPoint(float x, float y) => Bounds.ContainsPoint(x, y);

    public bool Intersects(Collider2D other) => Bounds.Intersects(other.Bounds);
}

/// <summary>
/// Controls object visibility and alpha opacity.
/// </summary>
public sealed class VisibilityComponent : ComponentBase
{
    public bool IsVisible { get; set; } = true;
    public float Opacity { get; set; } = 1f;
}

/// <summary>
/// Controls Z-layer drawing priority and optional Y-sorting for isometric depth.
/// </summary>
public sealed class SortingOrderComponent : ComponentBase
{
    public int ZIndex { get; set; } = 0;
    public bool EnableYSort { get; set; } = false;

    public float EffectiveOrder => EnableYSort && GameObject is not null
        ? ZIndex + (GameObject.Transform.WorldPosition.Y * 0.001f)
        : ZIndex;
}

/// <summary>
/// Sprite rendering component holding sprite identification, dimensions, anchor, and flips.
/// </summary>
public sealed class SpriteRendererComponent : ComponentBase
{
    public string SpriteId { get; set; } = string.Empty;
    public float Width { get; set; } = 32f;
    public float Height { get; set; } = 32f;
    public VectorPoint2D Anchor { get; set; } = new(0.5f, 0.5f); // Center anchor
    public bool FlipX { get; set; } = false;
    public bool FlipY { get; set; } = false;
    public float Opacity { get; set; } = 1f;
}

/// <summary>
/// Text rendering component for floating nameplates, damage numbers, and health bars.
/// </summary>
public sealed class TextRendererComponent : ComponentBase
{
    public string Text { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Noto Serif";
    public float FontSize { get; set; } = 12f;
    public string ColorHex { get; set; } = "#FFFFFF";
    public bool Bold { get; set; } = false;
}
