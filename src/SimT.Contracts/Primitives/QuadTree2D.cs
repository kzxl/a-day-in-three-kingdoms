using System.Runtime.CompilerServices;

namespace SimT.Contracts.Primitives;

/// <summary>
/// Axis-Aligned Bounding Box for 2D spatial queries and culling.
/// </summary>
public readonly record struct BoundingBox2D(float MinX, float MinY, float MaxX, float MaxY)
{
    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BoundingBox2D FromPoint(float x, float y, float radius = 0f) =>
        new(x - radius, y - radius, x + radius, y + radius);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BoundingBox2D FromRect(float x, float y, float width, float height) =>
        new(x, y, x + width, y + height);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(in BoundingBox2D other) =>
        MinX <= other.MaxX && MaxX >= other.MinX &&
        MinY <= other.MaxY && MaxY >= other.MinY;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in BoundingBox2D other) =>
        MinX <= other.MinX && MaxX >= other.MaxX &&
        MinY <= other.MinY && MaxY >= other.MaxY;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsPoint(float x, float y) =>
        x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
}

/// <summary>
/// Represents an entity positioned within the 2D spatial index.
/// </summary>
public interface ISpatialItem2D
{
    BoundingBox2D Bounds { get; }
}

/// <summary>
/// Sovereign 2D QuadTree spatial index.
/// Delivers O(log N) frustum culling, range lookups, and mouse-hover collision detection.
/// </summary>
public sealed class QuadTree2D<T> where T : class, ISpatialItem2D
{
    private readonly int _maxItemsPerNode;
    private readonly int _maxDepth;
    private readonly Node _root;
    private int _count;

    public BoundingBox2D Bounds => _root.Bounds;
    public int Count => _count;

    public QuadTree2D(BoundingBox2D bounds, int maxItemsPerNode = 16, int maxDepth = 8)
    {
        if (maxItemsPerNode <= 0) throw new ArgumentOutOfRangeException(nameof(maxItemsPerNode));
        if (maxDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxDepth));

        _maxItemsPerNode = maxItemsPerNode;
        _maxDepth = maxDepth;
        _root = new Node(bounds, 0);
    }

    public void Insert(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_root.Insert(item, _maxItemsPerNode, _maxDepth))
        {
            _count++;
        }
    }

    public bool Remove(T item)
    {
        if (item == null) return false;
        if (_root.Remove(item))
        {
            _count--;
            return true;
        }
        return false;
    }

    public void Clear()
    {
        _root.Clear();
        _count = 0;
    }

    public void Query(in BoundingBox2D range, List<T> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        _root.Query(range, results);
    }

    public void Query(in BoundingBox2D range, Action<T> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        _root.Query(range, visitor);
    }

    private sealed class Node
    {
        public BoundingBox2D Bounds { get; }
        private readonly int _depth;
        private readonly List<T> _items;
        private Node[]? _children;

        public bool IsLeaf => _children == null;

        public Node(BoundingBox2D bounds, int depth)
        {
            Bounds = bounds;
            _depth = depth;
            _items = new List<T>(8);
        }

        public bool Insert(T item, int maxItems, int maxDepth)
        {
            if (!Bounds.Intersects(item.Bounds))
            {
                return false;
            }

            if (IsLeaf)
            {
                if (_items.Count < maxItems || _depth >= maxDepth)
                {
                    _items.Add(item);
                    return true;
                }
                Subdivide();
            }

            bool inserted = false;
            if (_children != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_children[i].Insert(item, maxItems, maxDepth))
                    {
                        inserted = true;
                    }
                }
            }

            if (!inserted)
            {
                _items.Add(item);
                inserted = true;
            }

            return inserted;
        }

        public bool Remove(T item)
        {
            if (!Bounds.Intersects(item.Bounds))
            {
                return false;
            }

            bool removed = _items.Remove(item);

            if (_children != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (_children[i].Remove(item))
                    {
                        removed = true;
                    }
                }
            }

            return removed;
        }

        public void Clear()
        {
            _items.Clear();
            _children = null;
        }

        public void Query(in BoundingBox2D range, List<T> results)
        {
            if (!Bounds.Intersects(range)) return;

            for (int i = 0; i < _items.Count; i++)
            {
                if (range.Intersects(_items[i].Bounds))
                {
                    results.Add(_items[i]);
                }
            }

            if (_children != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    _children[i].Query(range, results);
                }
            }
        }

        public void Query(in BoundingBox2D range, Action<T> visitor)
        {
            if (!Bounds.Intersects(range)) return;

            for (int i = 0; i < _items.Count; i++)
            {
                if (range.Intersects(_items[i].Bounds))
                {
                    visitor(_items[i]);
                }
            }

            if (_children != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    _children[i].Query(range, visitor);
                }
            }
        }

        private void Subdivide()
        {
            float midX = (Bounds.MinX + Bounds.MaxX) * 0.5f;
            float midY = (Bounds.MinY + Bounds.MaxY) * 0.5f;
            int nextDepth = _depth + 1;

            _children = new Node[4]
            {
                new(new BoundingBox2D(Bounds.MinX, Bounds.MinY, midX, midY), nextDepth), // Top-Left
                new(new BoundingBox2D(midX, Bounds.MinY, Bounds.MaxX, midY), nextDepth), // Top-Right
                new(new BoundingBox2D(Bounds.MinX, midY, midX, Bounds.MaxY), nextDepth), // Bottom-Left
                new(new BoundingBox2D(midX, midY, Bounds.MaxX, Bounds.MaxY), nextDepth)  // Bottom-Right
            };

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                bool pushed = false;
                for (int c = 0; c < 4; c++)
                {
                    if (_children[c].Bounds.Contains(item.Bounds))
                    {
                        _children[c].Insert(item, int.MaxValue, nextDepth);
                        pushed = true;
                        break;
                    }
                }
                if (pushed)
                {
                    _items.RemoveAt(i);
                }
            }
        }
    }
}
