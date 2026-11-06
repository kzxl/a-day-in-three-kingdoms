using System.Globalization;
using System.Runtime.CompilerServices;

namespace SimT.Contracts.Primitives;

/// <summary>
/// Sovereign, zero-allocation primitive conversion utilities.
/// Direct register unboxing and span parsing for high-throughput loops.
/// </summary>
public static class FastConvert
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AsInt(object? value, int defaultValue = 0)
    {
        if (value == null || value == DBNull.Value) return defaultValue;
        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is double d) return (int)d;
        if (value is decimal m) return (int)m;
        if (value is short s) return s;
        if (value is byte b) return b;
        if (value is float f) return (int)f;
        if (value is uint ui) return (int)ui;
        if (value is ulong ul) return (int)ul;
        if (value is string str && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }
        return defaultValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AsLong(object? value, long defaultValue = 0)
    {
        if (value == null || value == DBNull.Value) return defaultValue;
        if (value is long l) return l;
        if (value is int i) return i;
        if (value is double d) return (long)d;
        if (value is decimal m) return (long)m;
        if (value is short s) return s;
        if (value is byte b) return b;
        if (value is float f) return (long)f;
        if (value is uint ui) return ui;
        if (value is ulong ul) return (long)ul;
        if (value is string str && long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
        {
            return parsed;
        }
        return defaultValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AsDouble(object? value, double defaultValue = 0.0)
    {
        if (value == null || value == DBNull.Value) return defaultValue;
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is decimal m) return (double)m;
        if (value is short s) return s;
        if (value is byte b) return b;
        if (value is string str && double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
        {
            return parsed;
        }
        return defaultValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AsBool(object? value, bool defaultValue = false)
    {
        if (value == null || value == DBNull.Value) return defaultValue;
        if (value is bool b) return b;
        if (value is int i) return i != 0;
        if (value is long l) return l != 0;
        if (value is string str)
        {
            if (bool.TryParse(str, out bool parsed)) return parsed;
            if (str == "1") return true;
            if (str == "0") return false;
        }
        return defaultValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string AsString(object? value, string defaultValue = "")
    {
        if (value == null || value == DBNull.Value) return defaultValue;
        return value.ToString() ?? defaultValue;
    }
}
