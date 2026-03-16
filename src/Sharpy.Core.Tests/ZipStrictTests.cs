using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ZipStrict_Tests
{
    // ── 2-arg strict mode ──

    [Fact]
    public void Zip_EqualLength_Strict_WorksNormally()
    {
        List<int> a = [1, 2, 3];
        List<string> b = ["a", "b", "c"];

        var zipped = Zip(a, b, strict: true);

        zipped.Next().Should().Be((1, "a"));
        zipped.Next().Should().Be((2, "b"));
        zipped.Next().Should().Be((3, "c"));

        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Zip_FirstShorter_Strict_ThrowsValueError()
    {
        List<int> shorter = [1, 2];
        List<string> longer = ["a", "b", "c"];

        var zipped = Zip(shorter, longer, strict: true);

        // First two elements should work fine
        zipped.Next();
        zipped.Next();

        // Third call: first is exhausted but second has more
        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Zip_SecondShorter_Strict_ThrowsValueError()
    {
        List<int> longer = [1, 2, 3];
        List<string> shorter = ["a", "b"];

        var zipped = Zip(longer, shorter, strict: true);

        zipped.Next();
        zipped.Next();

        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Zip_BothEmpty_Strict_WorksNoElements()
    {
        var a = new List<int>();
        var b = new List<string>();

        var zipped = Zip(a, b, strict: true);

        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Zip_NonStrict_DifferentLengths_TruncatesSilently()
    {
        List<int> longer = [1, 2, 3, 4, 5];
        List<string> shorter = ["a", "b"];

        var zipped = Zip(longer, shorter, strict: false);

        zipped.Next().Should().Be((1, "a"));
        zipped.Next().Should().Be((2, "b"));

        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<StopIteration>();
    }

    // ── 3-arg strict mode ──

    [Fact]
    public void Zip_ThreeEqualLength_Strict_WorksNormally()
    {
        List<int> a = [1, 2];
        List<string> b = ["a", "b"];
        List<bool> c = [true, false];

        var zipped = Zip(a, b, c, strict: true);

        zipped.Next().Should().Be((1, "a", true));
        zipped.Next().Should().Be((2, "b", false));

        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Zip_ThreeArgs_ThirdShorter_Strict_ThrowsValueError()
    {
        List<int> a = [1, 2, 3];
        List<string> b = ["a", "b", "c"];
        List<bool> c = [true, false];

        var zipped = Zip(a, b, c, strict: true);

        zipped.Next();
        zipped.Next();

        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Zip_ThreeArgs_FirstShorter_Strict_ThrowsValueError()
    {
        List<int> a = [1];
        List<string> b = ["a", "b"];
        List<bool> c = [true, false];

        var zipped = Zip(a, b, c, strict: true);

        zipped.Next();

        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Zip_ThreeEmpty_Strict_WorksNoElements()
    {
        var a = new List<int>();
        var b = new List<string>();
        var c = new List<bool>();

        var zipped = Zip(a, b, c, strict: true);

        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Zip_ThreeArgs_NonStrict_TruncatesSilently()
    {
        List<int> a = [1, 2, 3];
        List<string> b = ["a"];
        List<bool> c = [true, false];

        var zipped = Zip(a, b, c, strict: false);

        zipped.Next().Should().Be((1, "a", true));

        FluentActions.Invoking(() => zipped.Next())
            .Should().Throw<StopIteration>();
    }
}
