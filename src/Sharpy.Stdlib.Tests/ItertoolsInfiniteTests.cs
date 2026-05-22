using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ItertoolsInfinite_Tests
{
    // --- Count ---

    [Fact]
    public void Count_DefaultStartStep_StartsAtZeroStepOne()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Count())
        {
            result.Add(n);
            if (((ICollection<int>)result).Count == 5)
                break;
        }

        result.Should().Equal(0, 1, 2, 3, 4);
    }

    [Fact]
    public void Count_CustomStart_StartsAtTen()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Count(10))
        {
            result.Add(n);
            if (((ICollection<int>)result).Count == 3)
                break;
        }

        result.Should().Equal(10, 11, 12);
    }

    [Fact]
    public void Count_StepTwo_YieldsEvenNumbers()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Count(0, 2))
        {
            result.Add(n);
            if (((ICollection<int>)result).Count == 4)
                break;
        }

        result.Should().Equal(0, 2, 4, 6);
    }

    [Fact]
    public void Count_NegativeStep_CountsDown()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Count(10, -1))
        {
            result.Add(n);
            if (((ICollection<int>)result).Count == 3)
                break;
        }

        result.Should().Equal(10, 9, 8);
    }

    // --- Cycle ---

    [Fact]
    public void Cycle_MultipleElements_CyclesCorrectly()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Cycle(new[] { 1, 2, 3 }))
        {
            result.Add(n);
            if (((ICollection<int>)result).Count == 7)
                break;
        }

        result.Should().Equal(1, 2, 3, 1, 2, 3, 1);
    }

    [Fact]
    public void Cycle_SingleElement_RepeatsSingleElement()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Cycle(new[] { 42 }))
        {
            result.Add(n);
            if (((ICollection<int>)result).Count == 5)
                break;
        }

        result.Should().Equal(42, 42, 42, 42, 42);
    }

    [Fact]
    public void Cycle_EmptyIterable_ProducesNoElements()
    {
        var result = new List<int>();
        // Python: list(itertools.cycle([])) hangs forever if taken, but empty input yields nothing
        // CycleIterator returns false when nothing was saved, so foreach terminates immediately
        foreach (int n in Itertools.Cycle(Array.Empty<int>()))
        {
            result.Add(n);
            // Safety break in case implementation is infinite
            if (((ICollection<int>)result).Count == 10)
                break;
        }

        result.Should().BeEmpty();
    }

    // --- Repeat ---

    [Fact]
    public void Repeat_InfiniteMode_RepeatsElementIndefinitely()
    {
        var result = new List<string>();
        foreach (string s in Itertools.Repeat("hello"))
        {
            result.Add(s);
            if (((ICollection<string>)result).Count == 3)
                break;
        }

        result.Should().Equal("hello", "hello", "hello");
    }

    [Fact]
    public void Repeat_CountedMode_RepeatsExactNumberOfTimes()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Repeat(7, 3u))
        {
            result.Add(n);
        }

        // Python: list(itertools.repeat(7, 3)) == [7, 7, 7]
        result.Should().Equal(7, 7, 7);
    }

    [Fact]
    public void Repeat_CountZero_ProducesNoElements()
    {
        var result = new List<int>();
        foreach (int n in Itertools.Repeat(99, 0u))
        {
            result.Add(n);
        }

        // Python: list(itertools.repeat(99, 0)) == []
        result.Should().BeEmpty();
    }
}
