using System.Runtime.CompilerServices;

namespace SimT.Contracts.Primitives;

/// <summary>
/// Sovereign zero-allocation easing equations for smooth UI, camera, and unit animations.
/// Takes normalized progress t [0.0, 1.0] and returns eased value [0.0, 1.0].
/// </summary>
public static class Easing
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Linear(float t) => Math.Clamp(t, 0f, 1f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseInQuad(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseOutQuad(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * (2f - t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseInOutQuad(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t < 0.5f ? 2f * t * t : -1f + (4f - 2f * t) * t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseInCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseOutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f) - 1f;
        return (t * t * t) + 1f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseInOutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t < 0.5f ? 4f * t * t * t : ((t - 1f) * (2f * t - 2f) * (2f * t - 2f)) + 1f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseInSine(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return 1f - MathF.Cos(t * (MathF.PI * 0.5f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseOutSine(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return MathF.Sin(t * (MathF.PI * 0.5f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseInOutSine(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return -0.5f * (MathF.Cos(MathF.PI * t) - 1f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EaseOutBounce(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
        {
            return n1 * t * t;
        }
        else if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return (n1 * t * t) + 0.75f;
        }
        else if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return (n1 * t * t) + 0.9375f;
        }
        else
        {
            t -= 2.625f / d1;
            return (n1 * t * t) + 0.984375f;
        }
    }
}
