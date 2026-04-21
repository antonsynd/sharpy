using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for builtin iteration functions: Iter, Next, Reversed.
/// Enumerate, Filter, Map, Range, Sorted, Zip are covered in their own test files.
/// </summary>
public class BuiltinIteration_Tests
{
    // ── Iter ──

    [Fact]
    public void Iter_List_CreatesIterator_NextReturnsElements()
    {
        List<int> list = [1, 2, 3];
        var it = Iter(list);
        it.Next().Should().Be(1);
        it.Next().Should().Be(2);
        it.Next().Should().Be(3);
        FluentActions.Invoking(() => it.Next()).Should().Throw<StopIteration>();
    }

    [Fact]
    public void Iter_EmptyList_ImmediatelyExhausted()
    {
        var it = Iter(new List<int>());
        FluentActions.Invoking(() => it.Next()).Should().Throw<StopIteration>();
    }

    [Fact]
    public void Iter_Null_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Iter<int>(null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Iter_SingleElementList_ReturnsElementThenExhausts()
    {
        var it = Iter(new List<string> { "only" });
        it.Next().Should().Be("only");
        FluentActions.Invoking(() => it.Next()).Should().Throw<StopIteration>();
    }

    [Fact]
    public void Iter_AlreadyIterator_ReturnsSameInstance()
    {
        List<int> list = [1, 2];
        var it1 = Iter(list);
        var it2 = Iter(it1); // Iter on an Iterator should return it directly
        object.ReferenceEquals(it1, it2).Should().BeTrue();
    }

    // ── Next (without default) ──

    [Fact]
    public void Next_NonExhaustedIterator_ReturnsElement()
    {
        var it = Iter(new List<int> { 10, 20 });
        Next(it).Should().Be(10);
        Next(it).Should().Be(20);
    }

    [Fact]
    public void Next_ExhaustedIterator_ThrowsStopIteration()
    {
        var it = Iter(new List<int>());
        FluentActions.Invoking(() => Next(it)).Should().Throw<StopIteration>();
    }

    [Fact]
    public void Next_Null_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Next<int>(null!)).Should().Throw<TypeError>();
    }

    // ── Reversed ──

    [Fact]
    public void Reversed_List_YieldsElementsInReverseOrder()
    {
        List<int> list = [1, 2, 3];
        var it = Reversed(list);
        it.Next().Should().Be(3);
        it.Next().Should().Be(2);
        it.Next().Should().Be(1);
        FluentActions.Invoking(() => it.Next()).Should().Throw<StopIteration>();
    }

    [Fact]
    public void Reversed_EmptyList_IsImmediatelyExhausted()
    {
        var it = Reversed(new List<int>());
        FluentActions.Invoking(() => it.Next()).Should().Throw<StopIteration>();
    }

    [Fact]
    public void Reversed_SingleElement_ReturnsThatElementThenExhausts()
    {
        var it = Reversed(new List<string> { "sole" });
        it.Next().Should().Be("sole");
        FluentActions.Invoking(() => it.Next()).Should().Throw<StopIteration>();
    }

    [Fact]
    public void Reversed_Null_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Reversed((IEnumerable<int>)null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Reversed_DoesNotMutateOriginalList()
    {
        List<int> list = [1, 2, 3];
        var it = Reversed(list);
        // Consume iterator
        it.Next();
        it.Next();
        it.Next();
        // Original should be untouched
        list.Should().ContainInOrder(1, 2, 3);
    }

    // ── Sorted — additional cases not in SortedTests.cs ──

    [Fact]
    public void Sorted_OriginalListUnchanged()
    {
        List<int> original = [3, 1, 2];
        var sorted = Sorted(original);
        original.Should().ContainInOrder(3, 1, 2); // unchanged
        sorted[0].Should().Be(1);
    }

    [Fact]
    public void Sorted_SingleElement_ReturnsSingleElementList()
    {
        List<int> list = [42];
        var result = Sorted(list);
        Len(result).Should().Be(1);
        result[0].Should().Be(42);
    }

    [Fact]
    public void Sorted_WithKeyAndReverse_CombinesCorrectly()
    {
        List<string> list = ["banana", "fig", "cherry"];
        var result = Sorted(list, key: s => s.Length, reverse: true);
        // lengths: banana=6, cherry=6, fig=3 — stable sort, banana before cherry
        result[((ICollection<string>)result).Count - 1].Should().Be("fig"); // shortest last when reversed
    }

    // ── Range — additional cases not in RangeTests.cs ──

    [Fact]
    public void Range_ZeroStop_IsImmediatelyExhausted()
    {
        var range = Range(0);
        FluentActions.Invoking(() => range.Next()).Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_NegativeStep_SameStartAndStop_IsEmpty()
    {
        var range = Range(5, 5, -1);
        FluentActions.Invoking(() => range.Next()).Should().Throw<StopIteration>();
    }

    [Fact]
    public void Range_SingleStep_IncrementsCorrectly()
    {
        var range = Range(3, 6, 1);
        range.Next().Should().Be(3);
        range.Next().Should().Be(4);
        range.Next().Should().Be(5);
        FluentActions.Invoking(() => range.Next()).Should().Throw<StopIteration>();
    }
}
