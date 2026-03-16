using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class NextDefault_Tests
{
    [Fact]
    public void Next_WithDefault_NonExhaustedIterator_ReturnsNextElement()
    {
        List<int> list = [10, 20, 30];
        var it = Iter(list);
        Next(it, -1).Should().Be(10);
        Next(it, -1).Should().Be(20);
        Next(it, -1).Should().Be(30);
    }

    [Fact]
    public void Next_WithDefault_ExhaustedIterator_ReturnsDefault()
    {
        List<int> list = [1];
        var it = Iter(list);

        // Consume the single element
        Next(it, -1).Should().Be(1);

        // Now exhausted - should return the default
        Next(it, -1).Should().Be(-1);
        Next(it, 42).Should().Be(42);
    }

    [Fact]
    public void Next_WithDefault_EmptyIterator_ReturnsDefault()
    {
        var empty = new List<int>();
        var it = Iter(empty);
        Next(it, 99).Should().Be(99);
    }

    [Fact]
    public void Next_WithDefault_NullIterator_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Next<int>(null!, 0))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Next_WithDefault_StringIterator_ReturnsDefault()
    {
        List<string> list = ["hello"];
        var it = Iter(list);

        Next(it, "default").Should().Be("hello");
        Next(it, "default").Should().Be("default");
    }
}
