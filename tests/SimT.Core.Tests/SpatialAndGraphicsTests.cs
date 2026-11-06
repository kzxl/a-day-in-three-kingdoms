using SimT.Contracts.Primitives;
using Xunit;

namespace SimT.Core.Tests;

public sealed class SpatialAndGraphicsTests
{
    private sealed class MapEntity : ISpatialItem2D
    {
        public string EntityId { get; }
        public BoundingBox2D Bounds { get; }

        public MapEntity(string id, float x, float y, float size = 10f)
        {
            EntityId = id;
            Bounds = BoundingBox2D.FromRect(x, y, size, size);
        }
    }

    // ==========================================
    // 1. QUADTREE 2D TESTS
    // ==========================================

    [Fact]
    public void QuadTree2D_InsertsAndQueriesIntersectingItems()
    {
        var worldBounds = new BoundingBox2D(0, 0, 1000, 1000);
        var tree = new QuadTree2D<MapEntity>(worldBounds, maxItemsPerNode: 4, maxDepth: 4);

        var e1 = new MapEntity("Settlement_East", 100, 100);
        var e2 = new MapEntity("Settlement_West", 800, 800);
        var e3 = new MapEntity("Caravan_Mid", 150, 150);

        tree.Insert(e1);
        tree.Insert(e2);
        tree.Insert(e3);

        Assert.Equal(3, tree.Count);

        // Query near e1 and e3
        var results = new List<MapEntity>();
        tree.Query(new BoundingBox2D(50, 50, 200, 200), results);

        Assert.Equal(2, results.Count);
        Assert.Contains(e1, results);
        Assert.Contains(e3, results);
        Assert.DoesNotContain(e2, results);

        // Remove
        Assert.True(tree.Remove(e1));
        Assert.Equal(2, tree.Count);

        results.Clear();
        tree.Query(new BoundingBox2D(50, 50, 200, 200), results);
        Assert.Single(results);
        Assert.Equal("Caravan_Mid", results[0].EntityId);

        // Clear
        tree.Clear();
        Assert.Equal(0, tree.Count);
    }

    // ==========================================
    // 2. BEZIER PATH 2D TESTS
    // ==========================================

    [Fact]
    public void BezierPath2D_FlattensCurvesAndEvaluatesPoints()
    {
        var path = new BezierPath2D();
        path.MoveTo(0, 0);
        path.CubicTo(50, 100, 150, 100, 200, 0);

        var polylines = path.Flatten(tolerance: 0.5f);
        Assert.Single(polylines);

        var points = polylines[0];
        Assert.True(points.Count > 4);
        Assert.Equal(0f, points[0].X);
        Assert.Equal(0f, points[0].Y);
        Assert.Equal(200f, points[^1].X);
        Assert.Equal(0f, points[^1].Y);

        // Point evaluation
        var p0 = new VectorPoint2D(0, 0);
        var p1 = new VectorPoint2D(50, 100);
        var p2 = new VectorPoint2D(150, 100);
        var p3 = new VectorPoint2D(200, 0);

        var atStart = BezierPath2D.EvaluateCubic(p0, p1, p2, p3, 0f);
        var atEnd = BezierPath2D.EvaluateCubic(p0, p1, p2, p3, 1f);
        var atMid = BezierPath2D.EvaluateCubic(p0, p1, p2, p3, 0.5f);

        Assert.Equal(0f, atStart.X);
        Assert.Equal(0f, atStart.Y);
        Assert.Equal(200f, atEnd.X);
        Assert.Equal(0f, atEnd.Y);
        Assert.Equal(100f, atMid.X);
        Assert.Equal(75f, atMid.Y);
    }

    // ==========================================
    // 3. MATH 3D TESTS
    // ==========================================

    [Fact]
    public void Math3D_ComputesVectorsMatricesAndRays()
    {
        var v1 = new Vec3(1, 2, 3);
        var v2 = new Vec3(4, 5, 6);

        var vSum = v1 + v2;
        Assert.Equal(5, vSum.X);
        Assert.Equal(7, vSum.Y);
        Assert.Equal(9, vSum.Z);

        var dot = Vec3.Dot(new Vec3(1, 0, 0), new Vec3(0, 1, 0));
        Assert.Equal(0f, dot);

        var cross = Vec3.Cross(new Vec3(1, 0, 0), new Vec3(0, 1, 0));
        Assert.Equal(0f, cross.X);
        Assert.Equal(0f, cross.Y);
        Assert.Equal(1f, cross.Z);

        // Ray & Plane intersection
        var ray = new Ray3D(new Vec3(0, 100, 0), new Vec3(0, -1, 0));
        var groundPlane = new Plane3D(Vec3.UnitY, 0);

        Assert.True(groundPlane.IntersectsRay(ray, out float distance));
        Assert.Equal(100f, distance);
        var hit = ray.GetPoint(distance);
        Assert.Equal(0f, hit.X);
        Assert.Equal(0f, hit.Y);
        Assert.Equal(0f, hit.Z);
    }

    // ==========================================
    // 4. CAMERA 3D TESTS
    // ==========================================

    [Fact]
    public void Camera3D_UnprojectsScreenPointToGround()
    {
        var camera = new Camera3D
        {
            Position = new Vec3(0, 200, 200),
            Target = new Vec3(0, 0, 0),
            Up = Vec3.UnitY,
            Projection = CameraProjectionMode.Perspective,
            AspectRatio = 1.0f
        };

        // Screen center (500, 500) on a (1000, 1000) viewport
        var centerRay = camera.ScreenPointToRay(500, 500, 1000, 1000);
        Assert.True(Camera3D.RaycastGround(centerRay, groundY: 0f, out var groundHit));

        Assert.Equal(0f, groundHit.Y, precision: 1);
        Assert.Equal(0f, groundHit.X, precision: 1);
        Assert.Equal(0f, groundHit.Z, precision: 1);
    }
}
