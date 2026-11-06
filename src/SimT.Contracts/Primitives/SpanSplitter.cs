using System.Runtime.CompilerServices;

namespace SimT.Contracts.Primitives;

/// <summary>
/// High-performance, zero-allocation enumerator for splitting character spans.
/// Replaces string.Split() with an allocation-free ref struct.
/// </summary>
public ref struct SpanSplitter
{
    private ReadOnlySpan<char> _span;
    private ReadOnlySpan<char> _current;
    private readonly char _separator;
    private readonly bool _removeEmpty;
    private bool _finished;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanSplitter(ReadOnlySpan<char> span, char separator, bool removeEmpty = false)
    {
        _span = span;
        _separator = separator;
        _removeEmpty = removeEmpty;
        _current = default;
        _finished = false;
    }

    public readonly ReadOnlySpan<char> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly SpanSplitter GetEnumerator() => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        while (!_finished)
        {
            int index = _span.IndexOf(_separator);
            if (index < 0)
            {
                _finished = true;
                _current = _span;
                if (_removeEmpty && _current.IsEmpty)
                {
                    return false;
                }
                return true;
            }

            _current = _span.Slice(0, index);
            _span = _span.Slice(index + 1);

            if (_removeEmpty && _current.IsEmpty)
            {
                continue;
            }
            return true;
        }

        return false;
    }
}

public static class SpanSplitterExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanSplitter SplitFast(this ReadOnlySpan<char> span, char separator, bool removeEmpty = false) =>
        new(span, separator, removeEmpty);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpanSplitter SplitFast(this string str, char separator, bool removeEmpty = false) =>
        new(str.AsSpan(), separator, removeEmpty);
}
