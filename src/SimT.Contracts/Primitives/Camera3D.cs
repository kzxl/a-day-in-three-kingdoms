using System.Runtime.CompilerServices;

namespace SimT.Contracts.Primitives;

public enum CameraProjectionMode
{
    Perspective = 0,
    Orthographic = 1
}

/// <summary>
/// Sovereign 3D camera supporting Perspective and Orthographic (Isometric) projections,
/// view-frustum derivation, and screen-to-world ray unprojection for map picking.
/// </summary>
public sealed class Camera3D
{
    public Vec3 Position { get; set; } = new(0f, 500f, 1000f);
    public Vec3 Target { get; set; } = new(0f, 0f, 0f);
    public Vec3 Up { get; set; } = Vec3.UnitY;

    public float FovYDegrees { get; set; } = 45f;
    public float AspectRatio { get; set; } = 16f / 9f;
    public float NearPlane { get; set; } = 1f;
    public float FarPlane { get; set; } = 10000f;
    public float OrthographicSize { get; set; } = 600f;
    public CameraProjectionMode Projection { get; set; } = CameraProjectionMode.Perspective;

    public float FovYRadians
    {
        get => FovYDegrees * (MathF.PI / 180.0f);
        set => FovYDegrees = value * (180.0f / MathF.PI);
    }

    public Vec3 Forward => (Target - Position).Normalize();
    public Vec3 Right => Vec3.Cross(Forward, Up).Normalize();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mat4 GetViewMatrix() => Mat4.CreateLookAt(Position, Target, Up);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mat4 GetProjectionMatrix()
    {
        if (Projection == CameraProjectionMode.Perspective)
        {
            return Mat4.CreatePerspectiveFieldOfView(FovYRadians, AspectRatio, NearPlane, FarPlane);
        }
        else
        {
            float width = OrthographicSize * AspectRatio;
            return Mat4.CreateOrthographic(width, OrthographicSize, NearPlane, FarPlane);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Mat4 GetViewProjectionMatrix() => GetViewMatrix() * GetProjectionMatrix();

    /// <summary>
    /// Unprojects 2D viewport coordinates into a 3D world-space Ray for mouse picking.
    /// </summary>
    public Ray3D ScreenPointToRay(float screenX, float screenY, float viewportWidth, float viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0) return new Ray3D(Position, Forward);

        // Normalized Device Coordinates [-1, 1]
        float ndcX = (2f * screenX / viewportWidth) - 1f;
        float ndcY = 1f - (2f * screenY / viewportHeight); // Invert Y for screen space

        if (Projection == CameraProjectionMode.Perspective)
        {
            float tanHalfFov = MathF.Tan(FovYRadians * 0.5f);
            float rayX = ndcX * tanHalfFov * AspectRatio;
            float rayY = ndcY * tanHalfFov;
            Vec3 rayDir = (Right * rayX + Up * rayY + Forward).Normalize();
            return new Ray3D(Position, rayDir);
        }
        else
        {
            float halfHeight = OrthographicSize * 0.5f;
            float halfWidth = halfHeight * AspectRatio;
            Vec3 origin = Position + (Right * (ndcX * halfWidth)) + (Up * (ndcY * halfHeight));
            return new Ray3D(origin, Forward);
        }
    }

    /// <summary>
    /// Casts a ray against the horizontal ground plane (Y = groundY) to determine world target coordinates.
    /// </summary>
    public static bool RaycastGround(in Ray3D ray, float groundY, out Vec3 hitPoint)
    {
        var groundPlane = new Plane3D(Vec3.UnitY, -groundY);
        if (groundPlane.IntersectsRay(ray, out float distance))
        {
            hitPoint = ray.GetPoint(distance);
            return true;
        }

        hitPoint = Vec3.Zero;
        return false;
    }
}
