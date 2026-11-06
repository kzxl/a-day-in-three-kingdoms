using SimT.Contracts.Primitives;
using Xunit;

namespace SimT.Core.Tests;

public sealed class ZeroPrimitivesTests
{
    [Fact]
    public void ValueStopwatch_MeasuresLatencyAccurately()
    {
        var sw = ValueStopwatch.StartNew();
        Assert.True(sw.IsActive);
        Assert.True(sw.RawStartTimestamp > 0);

        Thread.Sleep(10);

        var elapsed = sw.GetElapsedTime();
        Assert.True(elapsed.TotalMilliseconds >= 5.0);
        Assert.True(sw.ElapsedMilliseconds >= 5.0);

        var staticElapsed = ValueStopwatch.GetElapsedMilliseconds(sw.RawStartTimestamp);
        Assert.True(staticElapsed >= 5.0);
    }

    [Fact]
    public void FastConvert_ConvertsVariousTypesCorrectly()
    {
        // Int
        Assert.Equal(42, FastConvert.AsInt(42));
        Assert.Equal(42, FastConvert.AsInt("42"));
        Assert.Equal(42, FastConvert.AsInt(42.8));
        Assert.Equal(0, FastConvert.AsInt(null));
        Assert.Equal(99, FastConvert.AsInt("invalid", 99));

        // Long
        Assert.Equal(5000000000L, FastConvert.AsLong(5000000000L));
        Assert.Equal(5000000000L, FastConvert.AsLong("5000000000"));
        Assert.Equal(100L, FastConvert.AsLong(100));

        // Double
        Assert.Equal(3.14, FastConvert.AsDouble(3.14), precision: 2);
        Assert.Equal(3.14, FastConvert.AsDouble("3.14"), precision: 2);
        Assert.Equal(10.0, FastConvert.AsDouble(10));

        // Bool
        Assert.True(FastConvert.AsBool(true));
        Assert.True(FastConvert.AsBool("true"));
        Assert.True(FastConvert.AsBool(1));
        Assert.True(FastConvert.AsBool("1"));
        Assert.False(FastConvert.AsBool(false));
        Assert.False(FastConvert.AsBool(0));
        Assert.False(FastConvert.AsBool("false"));
        Assert.False(FastConvert.AsBool(null));

        // String
        Assert.Equal("123", FastConvert.AsString(123));
        Assert.Equal("hello", FastConvert.AsString("hello"));
        Assert.Equal("default", FastConvert.AsString(null, "default"));
    }

    [Fact]
    public void SpanSplitter_SplitsWithoutStringAllocation()
    {
        ReadOnlySpan<char> text = "core.module.action.sub";
        var parts = new List<string>();

        foreach (var segment in text.SplitFast('.'))
        {
            parts.Add(segment.ToString());
        }

        Assert.Equal(new[] { "core", "module", "action", "sub" }, parts);
    }

    [Fact]
    public void SpanSplitter_HandlesEmptyTokensWhenRequested()
    {
        ReadOnlySpan<char> text = "a,,b,c";
        var partsWithEmpty = new List<string>();
        foreach (var segment in text.SplitFast(',', removeEmpty: false))
        {
            partsWithEmpty.Add(segment.ToString());
        }
        Assert.Equal(new[] { "a", "", "b", "c" }, partsWithEmpty);

        var partsNoEmpty = new List<string>();
        foreach (var segment in text.SplitFast(',', removeEmpty: true))
        {
            partsNoEmpty.Add(segment.ToString());
        }
        Assert.Equal(new[] { "a", "b", "c" }, partsNoEmpty);
    }

    [Fact]
    public void ArrayPoolRentScope_RentsAndDisposesSafely()
    {
        using (var scope = ArrayPoolRentScope<int>.Rent(256))
        {
            Assert.Equal(256, scope.Length);
            Assert.True(scope.Span.Length >= 256);

            for (int i = 0; i < 256; i++)
            {
                scope.Span[i] = i * 2;
            }

            Assert.Equal(0, scope.Span[0]);
            Assert.Equal(20, scope.Span[10]);
            Assert.Equal(510, scope.Span[255]);
        }
    }
}
