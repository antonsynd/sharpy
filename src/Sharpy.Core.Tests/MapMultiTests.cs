using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for multi-iterable map() and its strict length-checking, mirroring ZipStrictTests (#990).
/// </summary>
public class MapMulti_Tests
{
    // ── 2-iterable ──

    [Fact]
    public void Map_TwoIterables_AppliesFunctionElementwise()
    {
        List<int> a = [1, 2, 3];
        List<int> b = [10, 20, 30];

        var mapped = Map((int x, int y) => x + y, a, b);

        mapped.Next().Should().Be(11);
        mapped.Next().Should().Be(22);
        mapped.Next().Should().Be(33);

        FluentActions.Invoking(() => mapped.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Map_TwoIterables_NonStrict_TruncatesAtShortest()
    {
        List<int> longer = [1, 2, 3, 4];
        List<int> shorter = [10, 20];

        var mapped = Map((int x, int y) => x + y, longer, shorter, strict: false);

        mapped.Next().Should().Be(11);
        mapped.Next().Should().Be(22);

        FluentActions.Invoking(() => mapped.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Map_TwoIterables_EqualLength_Strict_WorksNormally()
    {
        List<int> a = [1, 2, 3];
        List<int> b = [10, 20, 30];

        var mapped = Map((int x, int y) => x + y, a, b, strict: true);

        mapped.Next().Should().Be(11);
        mapped.Next().Should().Be(22);
        mapped.Next().Should().Be(33);

        FluentActions.Invoking(() => mapped.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Map_TwoIterables_SecondShorter_Strict_ThrowsValueError()
    {
        List<int> longer = [1, 2, 3];
        List<int> shorter = [10, 20];

        var mapped = Map((int x, int y) => x + y, longer, shorter, strict: true);

        mapped.Next();
        mapped.Next();

        FluentActions.Invoking(() => mapped.Next())
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Map_TwoIterables_FirstShorter_Strict_ThrowsValueError()
    {
        List<int> shorter = [1, 2];
        List<int> longer = [10, 20, 30];

        var mapped = Map((int x, int y) => x + y, shorter, longer, strict: true);

        mapped.Next();
        mapped.Next();

        FluentActions.Invoking(() => mapped.Next())
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Map_TwoEmpty_Strict_WorksNoElements()
    {
        var a = new List<int>();
        var b = new List<int>();

        var mapped = Map((int x, int y) => x + y, a, b, strict: true);

        FluentActions.Invoking(() => mapped.Next())
            .Should().Throw<StopIteration>();
    }

    // ── 3-iterable ──

    [Fact]
    public void Map_ThreeIterables_AppliesFunctionElementwise()
    {
        List<int> a = [1, 2];
        List<int> b = [10, 20];
        List<int> c = [100, 200];

        var mapped = Map((int x, int y, int z) => x + y + z, a, b, c);

        mapped.Next().Should().Be(111);
        mapped.Next().Should().Be(222);

        FluentActions.Invoking(() => mapped.Next())
            .Should().Throw<StopIteration>();
    }

    [Fact]
    public void Map_ThreeIterables_ThirdShorter_Strict_ThrowsValueError()
    {
        List<int> a = [1, 2, 3];
        List<int> b = [10, 20, 30];
        List<int> c = [100, 200];

        var mapped = Map((int x, int y, int z) => x + y + z, a, b, c, strict: true);

        mapped.Next();
        mapped.Next();

        FluentActions.Invoking(() => mapped.Next())
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Map_ThreeIterables_NonStrict_TruncatesAtShortest()
    {
        List<int> a = [1, 2, 3];
        List<int> b = [10];
        List<int> c = [100, 200, 300];

        var mapped = Map((int x, int y, int z) => x + y + z, a, b, c, strict: false);

        mapped.Next().Should().Be(111);

        FluentActions.Invoking(() => mapped.Next())
            .Should().Throw<StopIteration>();
    }
}
