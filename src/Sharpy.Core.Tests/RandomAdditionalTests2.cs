using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional Random tests covering gaps not in RandomTests.cs or RandomAdditionalTests.cs.
/// </summary>
public class RandomAdditionalTests2
{
    // ===== Seed determinism (multi-call sequence) =====

    [Fact]
    public void Seed_ProducesIdenticalSequence_MultipleValues()
    {
        Sharpy.Random.Seed(1234);
        var seq1 = new int[5];
        for (int i = 0; i < 5; i++)
            seq1[i] = Sharpy.Random.Randint(0, 1000);

        Sharpy.Random.Seed(1234);
        var seq2 = new int[5];
        for (int i = 0; i < 5; i++)
            seq2[i] = Sharpy.Random.Randint(0, 1000);

        seq1.Should().Equal(seq2);
    }

    [Fact]
    public void Seed_DifferentSeeds_ProduceDifferentSequences()
    {
        Sharpy.Random.Seed(1);
        var a = Sharpy.Random.NextDouble();
        Sharpy.Random.Seed(2);
        var b = Sharpy.Random.NextDouble();
        // Extremely unlikely to be equal with different seeds
        a.Should().NotBe(b);
    }

    // ===== Randrange with step =====

    [Fact]
    public void Randrange_OddStep_OnlyOddValues()
    {
        Sharpy.Random.Seed(99);
        for (int i = 0; i < 100; i++)
        {
            var val = Sharpy.Random.Randrange(1, 10, 2);
            (val % 2).Should().Be(1, "step=2 from 1 yields only odd numbers");
            val.Should().BeGreaterThanOrEqualTo(1);
            val.Should().BeLessThan(10);
        }
    }

    [Fact]
    public void Randrange_StepLargerThanWidth_SingleValue()
    {
        // range(3, 4, 5) has exactly one value: 3
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 10; i++)
        {
            var val = Sharpy.Random.Randrange(3, 4, 5);
            val.Should().Be(3);
        }
    }

    // ===== Choice single element =====

    [Fact]
    public void Choice_SingleElementList_ReturnsThatElement()
    {
        Sharpy.Random.Seed(42);
        var list = new List<int> { 999 };
        for (int i = 0; i < 10; i++)
        {
            Sharpy.Random.Choice((IList<int>)list).Should().Be(999);
        }
    }

    [Fact]
    public void Choice_SingleElementArray_ReturnsThatElement()
    {
        Sharpy.Random.Seed(42);
        var arr = new[] { "only" };
        for (int i = 0; i < 10; i++)
        {
            Sharpy.Random.Choice(arr).Should().Be("only");
        }
    }

    // ===== Sample edge cases =====

    [Fact]
    public void Sample_KEqualsLen_ReturnsPermutation()
    {
        Sharpy.Random.Seed(42);
        var pop = new List<int> { 1, 2, 3, 4, 5 };
        var result = Sharpy.Random.Sample(pop, 5);
        ((ICollection<int>)result).Count.Should().Be(5);
        result.Should().BeEquivalentTo(pop);
    }

    [Fact]
    public void Sample_SingleElement_ReturnsThatElement()
    {
        Sharpy.Random.Seed(42);
        var pop = new List<int> { 42 };
        var result = Sharpy.Random.Sample(pop, 1);
        ((ICollection<int>)result).Count.Should().Be(1);
        result[0].Should().Be(42);
    }

    [Fact]
    public void Sample_UniqueElements_NoDuplicates()
    {
        Sharpy.Random.Seed(7);
        var pop = new List<int> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        var result = Sharpy.Random.Sample(pop, 7);
        var seen = new HashSet<int>();
        foreach (var item in (IEnumerable<int>)result)
        {
            seen.Add(item).Should().BeTrue("Sample should return unique elements");
        }
    }

    // ===== Randint edge cases =====

    [Fact]
    public void Randint_SameAAndB_ReturnsThatValue()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 10; i++)
        {
            Sharpy.Random.Randint(7, 7).Should().Be(7);
        }
    }

    [Fact]
    public void Randint_LargeRange_StaysInBounds()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 100; i++)
        {
            var val = Sharpy.Random.Randint(int.MinValue / 2, int.MaxValue / 2);
            val.Should().BeGreaterThanOrEqualTo(int.MinValue / 2);
            val.Should().BeLessThanOrEqualTo(int.MaxValue / 2);
        }
    }

    // ===== Uniform edge cases =====

    [Fact]
    public void Uniform_AEqualsB_ReturnsA()
    {
        Sharpy.Random.Seed(42);
        var val = Sharpy.Random.Uniform(5.5, 5.5);
        val.Should().BeApproximately(5.5, 1e-10);
    }

    [Fact]
    public void Uniform_BLessThanA_ReturnsInReversedRange()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 50; i++)
        {
            var val = Sharpy.Random.Uniform(10.0, 1.0);
            // When b < a, result is in [b, a]
            val.Should().BeGreaterThanOrEqualTo(1.0);
            val.Should().BeLessThanOrEqualTo(10.0);
        }
    }

    // ===== NextDouble additional =====

    [Fact]
    public void NextDouble_NeverReturns1()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 1000; i++)
        {
            Sharpy.Random.NextDouble().Should().BeLessThan(1.0);
        }
    }

    [Fact]
    public void NextDouble_Statistical_MeanApproximatesHalf()
    {
        Sharpy.Random.Seed(42);
        double sum = 0;
        int n = 1000;
        for (int i = 0; i < n; i++)
            sum += Sharpy.Random.NextDouble();
        (sum / n).Should().BeApproximately(0.5, 0.05);
    }

    // ===== Getrandbits additional =====

    [Fact]
    public void Getrandbits_Sixteen_InRange()
    {
        Sharpy.Random.Seed(42);
        for (int i = 0; i < 100; i++)
        {
            var val = Sharpy.Random.Getrandbits(16);
            val.Should().BeGreaterThanOrEqualTo(0);
            val.Should().BeLessThan(65536); // 2^16
        }
    }

    // ===== Shuffle edge cases =====

    [Fact]
    public void Shuffle_EmptyList_DoesNotThrow()
    {
        var list = new List<int>();
        Action act = () => Sharpy.Random.Shuffle(list);
        act.Should().NotThrow();
    }

    [Fact]
    public void Shuffle_SingleElement_Unchanged()
    {
        var list = new List<int> { 42 };
        Sharpy.Random.Shuffle(list);
        list[0].Should().Be(42);
    }

    [Fact]
    public void Shuffle_PreservesAllElements()
    {
        Sharpy.Random.Seed(42);
        var list = new List<string> { "a", "b", "c", "d", "e" };
        var original = new List<string>(list);
        Sharpy.Random.Shuffle(list);
        list.Should().BeEquivalentTo(original);
        ((System.Collections.Generic.ICollection<string>)list).Count.Should().Be(
            ((System.Collections.Generic.ICollection<string>)original).Count);
    }

    // ===== Gauss additional =====

    [Fact]
    public void Gauss_NegativeSigma_StillReturnsValue()
    {
        // Box-Muller with negative sigma: result differs, not NaN
        Sharpy.Random.Seed(42);
        var val = Sharpy.Random.Gauss(0, -1.0);
        double.IsNaN(val).Should().BeFalse();
    }

    // ===== Choices additional =====

    [Fact]
    public void Choices_KOne_ReturnsSingleElement()
    {
        Sharpy.Random.Seed(42);
        var pop = new List<int> { 1, 2, 3 };
        var result = Sharpy.Random.Choices(pop, k: 1);
        ((ICollection<int>)result).Count.Should().Be(1);
        pop.Should().Contain(result[0]);
    }

    [Fact]
    public void Choices_AllSameElement_AllResultsAreThatElement()
    {
        Sharpy.Random.Seed(42);
        var pop = new List<int> { 7 };
        var result = Sharpy.Random.Choices(pop, k: 10);
        foreach (var item in (IEnumerable<int>)result)
        {
            item.Should().Be(7);
        }
    }

    [Fact]
    public void Choices_WithCumulativeWeightsZeroTotal_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Choices(
            new List<int> { 1, 2 },
            new List<double> { 0.0, 0.0 },
            k: 1))
            .Should().Throw<Sharpy.ValueError>();
    }
}
