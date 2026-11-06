using System.Globalization;
using System.Runtime.CompilerServices;

namespace SimT.Contracts.Primitives;

public struct Vec3
{
    public float X;
    public float Y;
    public float Z;

    public Vec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vec3 Zero => new(0f, 0f, 0f);
    public static Vec3 One => new(1f, 1f, 1f);
    public static Vec3 UnitX => new(1f, 0f, 0f);
    public static Vec3 UnitY => new(0f, 1f, 0f);
    public static Vec3 UnitZ => new(0f, 0f, 1f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator *(Vec3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator /(Vec3 a, float s) => new(a.X / s, a.Y / s, a.Z / s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => X * X + Y * Y + Z * Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 Normalize()
    {
        float len = Length();
        return len > 1e-6f ? this / len : Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Cross(Vec3 a, Vec3 b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vec3 a, Vec3 b) => (a - b).Length();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Lerp(Vec3 a, Vec3 b, float t) => a + (b - a) * t;

    public override string ToString() =>
        $"({X.ToString("F3", CultureInfo.InvariantCulture)}, {Y.ToString("F3", CultureInfo.InvariantCulture)}, {Z.ToString("F3", CultureInfo.InvariantCulture)})";
}

public struct Mat4
{
    public float M11, M12, M13, M14;
    public float M21, M22, M23, M24;
    public float M31, M32, M33, M34;
    public float M41, M42, M43, M44;

    public static Mat4 Identity => new()
    {
        M11 = 1f, M22 = 1f, M33 = 1f, M44 = 1f
    };

    public static Mat4 CreateTranslation(float x, float y, float z)
    {
        var m = Identity;
        m.M41 = x;
        m.M42 = y;
        m.M43 = z;
        return m;
    }

    public static Mat4 CreateTranslation(Vec3 v) => CreateTranslation(v.X, v.Y, v.Z);

    public static Mat4 CreateScale(float x, float y, float z)
    {
        var m = Identity;
        m.M11 = x;
        m.M22 = y;
        m.M33 = z;
        return m;
    }

    public static Mat4 CreateScale(Vec3 s) => CreateScale(s.X, s.Y, s.Z);

    public static Mat4 CreateLookAt(Vec3 eye, Vec3 target, Vec3 up)
    {
        Vec3 zAxis = (eye - target).Normalize();
        Vec3 xAxis = Vec3.Cross(up, zAxis).Normalize();
        Vec3 yAxis = Vec3.Cross(zAxis, xAxis);

        var m = Identity;
        m.M11 = xAxis.X; m.M12 = yAxis.X; m.M13 = zAxis.X;
        m.M21 = xAxis.Y; m.M22 = yAxis.Y; m.M23 = zAxis.Y;
        m.M31 = xAxis.Z; m.M32 = yAxis.Z; m.M33 = zAxis.Z;

        m.M41 = -Vec3.Dot(xAxis, eye);
        m.M42 = -Vec3.Dot(yAxis, eye);
        m.M43 = -Vec3.Dot(zAxis, eye);
        return m;
    }

    public static Mat4 CreatePerspectiveFieldOfView(float fovYRadians, float aspectRatio, float nearPlane, float farPlane)
    {
        float yScale = 1.0f / MathF.Tan(fovYRadians * 0.5f);
        float xScale = yScale / aspectRatio;
        float negDepth = nearPlane - farPlane;

        var m = new Mat4
        {
            M11 = xScale,
            M22 = yScale,
            M33 = (farPlane + nearPlane) / negDepth,
            M34 = -1.0f,
            M43 = (2.0f * farPlane * nearPlane) / negDepth
        };
        return m;
    }

    public static Mat4 CreateOrthographic(float width, float height, float nearPlane, float farPlane)
    {
        float range = 1.0f / (nearPlane - farPlane);
        var m = Identity;
        m.M11 = 2.0f / width;
        m.M22 = 2.0f / height;
        m.M33 = range;
        m.M43 = range * nearPlane;
        return m;
    }

    public static Mat4 operator *(Mat4 a, Mat4 b)
    {
        var result = new Mat4
        {
            M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
            M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
            M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
            M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,

            M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
            M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
            M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
            M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,

            M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
            M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
            M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
            M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,

            M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
            M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
            M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
            M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44
        };
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 TransformPoint(Vec3 p)
    {
        float x = p.X * M11 + p.Y * M21 + p.Z * M31 + M41;
        float y = p.X * M12 + p.Y * M22 + p.Z * M32 + M42;
        float z = p.X * M13 + p.Y * M23 + p.Z * M33 + M43;
        float w = p.X * M14 + p.Y * M24 + p.Z * M34 + M44;

        if (MathF.Abs(w) > 1e-6f)
        {
            float invW = 1.0f / w;
            return new Vec3(x * invW, y * invW, z * invW);
        }

        return new Vec3(x, y, z);
    }
}

public readonly record struct Ray3D(Vec3 Origin, Vec3 Direction)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 GetPoint(float distance) => Origin + Direction * distance;
}

public readonly record struct Plane3D(Vec3 Normal, float D)
{
    public Plane3D(float a, float b, float c, float d) : this(new Vec3(a, b, c), d) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IntersectsRay(in Ray3D ray, out float distance)
    {
        float denom = Vec3.Dot(Normal, ray.Direction);
        if (MathF.Abs(denom) > 1e-6f)
        {
            distance = -(Vec3.Dot(Normal, ray.Origin) + D) / denom;
            return distance >= 0f;
        }
        distance = 0f;
        return false;
    }
}
