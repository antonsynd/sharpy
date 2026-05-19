using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ItertoolsCombinatorics_Tests
{
    // --- Combinations ---

    [Fact]
    public void Combinations_EmptyPoolRZero_ReturnsSingleEmptyTuple()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.Combinations(Array.Empty<int>(), 0))
        {
            result.Add(arr);
        }

        // Python: list(itertools.combinations([], 0)) == [()]
        result.Should().HaveCount(1);
        result[0].Should().BeEmpty();
    }

    [Fact]
    public void Combinations_FourChooseTwo_Returns6Combinations()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.Combinations(new[] { 1, 2, 3, 4 }, 2))
        {
            result.Add(arr);
        }

        // Python: C(4,2) = 6
        result.Should().HaveCount(6);
        result[0].Should().Equal(1, 2);
        result[1].Should().Equal(1, 3);
        result[2].Should().Equal(1, 4);
        result[3].Should().Equal(2, 3);
        result[4].Should().Equal(2, 4);
        result[5].Should().Equal(3, 4);
    }

    // --- CombinationsWithReplacement ---

    [Fact]
    public void CombinationsWithReplacement_SingleElementR3_ReturnsTriple()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.CombinationsWithReplacement(new[] { 1 }, 3))
        {
            result.Add(arr);
        }

        // Python: list(itertools.combinations_with_replacement([1], 3)) == [(1,1,1)]
        result.Should().HaveCount(1);
        result[0].Should().Equal(1, 1, 1);
    }

    [Fact]
    public void CombinationsWithReplacement_TwoChooseTwo_Returns3Combinations()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.CombinationsWithReplacement(new[] { 1, 2 }, 2))
        {
            result.Add(arr);
        }

        // Python: list(itertools.combinations_with_replacement([1,2], 2)) == [(1,1),(1,2),(2,2)]
        result.Should().HaveCount(3);
        result[0].Should().Equal(1, 1);
        result[1].Should().Equal(1, 2);
        result[2].Should().Equal(2, 2);
    }

    [Fact]
    public void CombinationsWithReplacement_EmptyPoolRZero_ReturnsSingleEmptyTuple()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.CombinationsWithReplacement(Array.Empty<int>(), 0))
        {
            result.Add(arr);
        }

        // Python: list(itertools.combinations_with_replacement([], 0)) == [()]
        result.Should().HaveCount(1);
        result[0].Should().BeEmpty();
    }

    // --- Permutations ---

    [Fact]
    public void Permutations_RZero_ReturnsSingleEmptyTuple()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.Permutations(new[] { 1, 2, 3 }, 0))
        {
            result.Add(arr);
        }

        // Python: list(itertools.permutations([1,2,3], 0)) == [()]
        result.Should().HaveCount(1);
        result[0].Should().BeEmpty();
    }

    [Fact]
    public void Permutations_FourChooseTwo_Returns12Permutations()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.Permutations(new[] { 1, 2, 3, 4 }, 2))
        {
            result.Add(arr);
        }

        // Python: P(4,2) = 12
        result.Should().HaveCount(12);
    }

    [Fact]
    public void Permutations_FullPermutations_VerifyFirstAndLast()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.Permutations(new[] { 1, 2, 3 }))
        {
            result.Add(arr);
        }

        // 3! = 6 permutations
        result.Should().HaveCount(6);
        // Python order: (1,2,3), (1,3,2), (2,1,3), (2,3,1), (3,1,2), (3,2,1)
        result[0].Should().Equal(1, 2, 3);
        result[5].Should().Equal(3, 2, 1);
    }

    // --- Product ---

    [Fact]
    public void Product_NoArgs_ReturnsSingleEmptyTuple()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.Product<int>())
        {
            result.Add(arr);
        }

        // Python: list(itertools.product()) == [()]
        result.Should().HaveCount(1);
        result[0].Should().BeEmpty();
    }

    [Fact]
    public void Product_ThreeIterables_ReturnsCartesianProduct()
    {
        var result = new List<int[]>();
        foreach (int[] arr in Itertools.Product(new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5, 6 }))
        {
            result.Add(arr);
        }

        // Python: 2*2*2 = 8 combinations
        // Python: list(itertools.product([1,2],[3,4],[5,6]))
        result.Should().HaveCount(8);
        result[0].Should().Equal(1, 3, 5);
        result[1].Should().Equal(1, 3, 6);
        result[7].Should().Equal(2, 4, 6);
    }

    [Fact]
    public void Product_SamePoolWithSelf_YieldsPairsWithRepetition()
    {
        var result = new List<int[]>();
        // Simulating repeat=2 by passing same array twice
        foreach (int[] arr in Itertools.Product(new[] { 1, 2 }, new[] { 1, 2 }))
        {
            result.Add(arr);
        }

        // Python: list(itertools.product([1,2], repeat=2)) == [(1,1),(1,2),(2,1),(2,2)]
        result.Should().HaveCount(4);
        result[0].Should().Equal(1, 1);
        result[1].Should().Equal(1, 2);
        result[2].Should().Equal(2, 1);
        result[3].Should().Equal(2, 2);
    }
}
