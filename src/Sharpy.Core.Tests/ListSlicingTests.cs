using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ListSlicingTests
{
    // ===== GetSlice via indexer [start, end] =====

    [Fact]
    public void GetSlice_BasicRange_ReturnsSublist()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var result = list[1, 4];
        result.Should().HaveCount(3);
        result[0].Should().Be(2);
        result[1].Should().Be(3);
        result[2].Should().Be(4);
    }

    [Fact]
    public void GetSlice_FromBeginning_ReturnsPrefix()
    {
        var list = new List<int> { 10, 20, 30, 40 };
        var result = list[0, 2];
        result.Should().HaveCount(2);
        result[0].Should().Be(10);
        result[1].Should().Be(20);
    }

    [Fact]
    public void GetSlice_ToEnd_ReturnsSuffix()
    {
        var list = new List<int> { 10, 20, 30, 40 };
        var result = list[2, 4];
        result.Should().HaveCount(2);
        result[0].Should().Be(30);
        result[1].Should().Be(40);
    }

    [Fact]
    public void GetSlice_StartEqualsEnd_ReturnsEmpty()
    {
        var list = new List<int> { 1, 2, 3 };
        var result = list[1, 1];
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetSlice_NegativeStart_CountsFromEnd()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        // list[-2:] in Python
        var result = list.GetSlice(new Slice(-2, int.MaxValue, 1));
        result.Should().HaveCount(2);
        result[0].Should().Be(4);
        result[1].Should().Be(5);
    }

    [Fact]
    public void GetSlice_NegativeEnd_CountsFromEnd()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        // list[0:-2] in Python
        var result = list.GetSlice(new Slice(0, -2, 1));
        result.Should().HaveCount(3);
        result[0].Should().Be(1);
        result[1].Should().Be(2);
        result[2].Should().Be(3);
    }

    [Fact]
    public void GetSlice_OutOfRangeEnd_ClampsSilently()
    {
        var list = new List<int> { 1, 2, 3 };
        // Python clamps: list[0:100] returns the whole list
        var result = list[0, 100];
        result.Should().HaveCount(3);
    }

    [Fact]
    public void GetSlice_OutOfRangeStart_ClampsSilently()
    {
        var list = new List<int> { 1, 2, 3 };
        // Python clamps: list[-100:2] starts at 0
        var result = list.GetSlice(new Slice(-100, 2, 1));
        result.Should().HaveCount(2);
        result[0].Should().Be(1);
        result[1].Should().Be(2);
    }

    [Fact]
    public void GetSlice_EntireList_ReturnsCopy()
    {
        var list = new List<int> { 1, 2, 3 };
        var result = list[0, 3];
        result.Should().HaveCount(3);
        result[0].Should().Be(1);
        // Verify it's a copy, not the same object
        result.Should().NotBeSameAs(list);
    }

    // ===== GetSlice with step =====

    [Fact]
    public void GetSlice_Step2_ReturnsEveryOther()
    {
        var list = new List<int> { 0, 1, 2, 3, 4, 5 };
        var result = list[0, 6, 2];
        result.Should().HaveCount(3);
        result[0].Should().Be(0);
        result[1].Should().Be(2);
        result[2].Should().Be(4);
    }

    [Fact]
    public void GetSlice_Step3_ReturnsEveryThird()
    {
        var list = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        var result = list[0, 9, 3];
        result.Should().HaveCount(3);
        result[0].Should().Be(0);
        result[1].Should().Be(3);
        result[2].Should().Be(6);
    }

    [Fact]
    public void GetSlice_StepNegative1_ReversesSlice()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        // Python: list[::-1] reverses
        var result = list.GetSlice(new Slice(-1, int.MinValue, -1));
        result.Should().HaveCount(5);
        result[0].Should().Be(5);
        result[1].Should().Be(4);
        result[4].Should().Be(1);
    }

    [Fact]
    public void GetSlice_StepNegative2_ReturnsEveryOtherReversed()
    {
        var list = new List<int> { 0, 1, 2, 3, 4, 5 };
        // Python: list[5::-2] = [5, 3, 1]
        var result = list.GetSlice(new Slice(5, int.MinValue, -2));
        result.Should().HaveCount(3);
        result[0].Should().Be(5);
        result[1].Should().Be(3);
        result[2].Should().Be(1);
    }

    [Fact]
    public void GetSlice_ZeroStep_ThrowsValueError()
    {
        var list = new List<int> { 1, 2, 3 };
        list.Invoking(l => l.GetSlice(new Slice(0, 3, 0)))
            .Should().Throw<ValueError>()
            .WithMessage("*zero*");
    }

    [Fact]
    public void GetSlice_EmptyList_ReturnsEmpty()
    {
        var list = new List<int>();
        var result = list[0, 5];
        result.Should().BeEmpty();
    }

    // ===== SetSlice =====

    [Fact]
    public void SetSlice_ReplacesRange_SameSize()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        list[1, 3] = new List<int> { 20, 30 };
        list[0].Should().Be(1);
        list[1].Should().Be(20);
        list[2].Should().Be(30);
        list[3].Should().Be(4);
        list[4].Should().Be(5);
    }

    [Fact]
    public void SetSlice_ReplacesRange_SmallerReplacement()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        list[1, 4] = new List<int> { 99 };
        list.Should().HaveCount(3);
        list[0].Should().Be(1);
        list[1].Should().Be(99);
        list[2].Should().Be(5);
    }

    [Fact]
    public void SetSlice_ReplacesRange_LargerReplacement()
    {
        var list = new List<int> { 1, 2, 3 };
        list[1, 2] = new List<int> { 20, 21, 22 };
        list.Should().HaveCount(5);
        list[0].Should().Be(1);
        list[1].Should().Be(20);
        list[2].Should().Be(21);
        list[3].Should().Be(22);
        list[4].Should().Be(3);
    }

    [Fact]
    public void SetSlice_EmptyRange_InsertsElements()
    {
        var list = new List<int> { 1, 2, 3 };
        list[1, 1] = new List<int> { 10, 11 };
        list.Should().HaveCount(5);
        list[0].Should().Be(1);
        list[1].Should().Be(10);
        list[2].Should().Be(11);
        list[3].Should().Be(2);
        list[4].Should().Be(3);
    }

    [Fact]
    public void SetSlice_MultiStep_ReplacesEveryOther()
    {
        var list = new List<int> { 0, 1, 2, 3, 4 };
        // Replace every other starting at index 0: positions 0, 2, 4
        list[0, 5, 2] = new List<int> { 10, 20, 30 };
        list[0].Should().Be(10);
        list[1].Should().Be(1);
        list[2].Should().Be(20);
        list[3].Should().Be(3);
        list[4].Should().Be(30);
    }

    [Fact]
    public void SetSlice_MultiStep_SizeMismatch_ThrowsValueError()
    {
        var list = new List<int> { 0, 1, 2, 3, 4 };
        list.Invoking(l => { l[0, 5, 2] = new List<int> { 10, 20 }; })
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void SetSlice_NegativeStep_ThrowsNotImplementedError()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        list.Invoking(l => l.SetSlice(new Slice(4, 0, -1), new List<int> { 9, 8 }))
            .Should().Throw<NotImplementedError>();
    }

    // ===== DeleteSlice =====

    [Fact]
    public void DeleteSlice_RemovesRange()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        list.DeleteSlice(new Slice(1, 4, 1));
        list.Should().HaveCount(2);
        list[0].Should().Be(1);
        list[1].Should().Be(5);
    }

    [Fact]
    public void DeleteSlice_EmptyRange_NoOp()
    {
        var list = new List<int> { 1, 2, 3 };
        list.DeleteSlice(new Slice(2, 2, 1));
        list.Should().HaveCount(3);
    }

    [Fact]
    public void DeleteSlice_ZeroStep_ThrowsValueError()
    {
        var list = new List<int> { 1, 2, 3 };
        list.Invoking(l => l.DeleteSlice(new Slice(0, 3, 0)))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void DeleteSlice_NegativeStep_ThrowsNotImplementedError()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        list.Invoking(l => l.DeleteSlice(new Slice(4, 0, -1)))
            .Should().Throw<NotImplementedError>();
    }

    [Fact]
    public void DeleteSlice_MultiStep_RemovesEveryOther()
    {
        var list = new List<int> { 0, 1, 2, 3, 4 };
        // Delete every 2nd element starting at 0: positions 0, 2, 4
        list.DeleteSlice(new Slice(0, 5, 2));
        list.Should().HaveCount(2);
        list[0].Should().Be(1);
        list[1].Should().Be(3);
    }

    // ===== C# list-pattern support members: Length, this[Index], this[Range] (#991) =====

    [Fact]
    public void Length_ReturnsElementCount()
    {
        var list = new List<int> { 1, 2, 3 };
        list.Length.Should().Be(3);
        new List<int>().Length.Should().Be(0);
    }

    [Fact]
    public void IndexIndexer_FromStartAndFromEnd()
    {
        var list = new List<int> { 10, 20, 30, 40 };
        list[(System.Index)0].Should().Be(10);
        list[(System.Index)2].Should().Be(30);
        list[^1].Should().Be(40);
        list[^2].Should().Be(30);
    }

    [Fact]
    public void RangeIndexer_ReturnsSublist()
    {
        var list = new List<int> { 10, 20, 30, 40, 50 };

        var middle = list[1..4];
        middle.Should().HaveCount(3);
        middle[0].Should().Be(20);
        middle[2].Should().Be(40);

        var tail = list[2..];
        tail.Should().HaveCount(3);
        tail[0].Should().Be(30);

        var head = list[..2];
        head.Should().HaveCount(2);
        head[1].Should().Be(20);
    }
}
