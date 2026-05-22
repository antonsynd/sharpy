using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Random_Tests
{
    [Fact]
    public void Seed_ProducesDeterministicResults()
    {
        Sharpy.Random.Seed(42);
        var first = Sharpy.Random.NextDouble();

        Sharpy.Random.Seed(42);
        var second = Sharpy.Random.NextDouble();

        first.Should().Be(second);
    }

    [Fact]
    public void NextDouble_ReturnsValueInRange()
    {
        Sharpy.Random.Seed(123);

        for (int i = 0; i < 50; i++)
        {
            var value = Sharpy.Random.NextDouble();
            value.Should().BeGreaterThanOrEqualTo(0.0);
            value.Should().BeLessThan(1.0);
        }
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(0, 0)]
    [InlineData(-5, 5)]
    public void Randint_ReturnsValueInInclusiveRange(int a, int b)
    {
        Sharpy.Random.Seed(99);

        for (int i = 0; i < 50; i++)
        {
            var value = Sharpy.Random.Randint(a, b);
            value.Should().BeGreaterThanOrEqualTo(a);
            value.Should().BeLessThanOrEqualTo(b);
        }
    }

    [Fact]
    public void Uniform_ReturnsValueInRange()
    {
        Sharpy.Random.Seed(77);

        for (int i = 0; i < 50; i++)
        {
            var value = Sharpy.Random.Uniform(1.0, 5.0);
            value.Should().BeGreaterThanOrEqualTo(1.0);
            value.Should().BeLessThanOrEqualTo(5.0);
        }
    }

    [Fact]
    public void Choice_Array_ReturnsElementFromSequence()
    {
        Sharpy.Random.Seed(42);
        var arr = new[] { 10, 20, 30, 40, 50 };

        for (int i = 0; i < 20; i++)
        {
            var choice = Sharpy.Random.Choice(arr);
            arr.Should().Contain(choice);
        }
    }

    [Fact]
    public void Choice_IList_ReturnsElementFromSequence()
    {
        Sharpy.Random.Seed(42);
        IList<string> list = new List<string> { "a", "b", "c" };

        for (int i = 0; i < 20; i++)
        {
            var choice = Sharpy.Random.Choice(list);
            list.Should().Contain(choice);
        }
    }

    [Fact]
    public void Choice_EmptyArray_ThrowsIndexError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Choice(Array.Empty<int>()))
            .Should().Throw<Sharpy.IndexError>();
    }

    [Fact]
    public void Choice_EmptyList_ThrowsIndexError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Choice((IList<int>)new List<int>()))
            .Should().Throw<Sharpy.IndexError>();
    }

    [Fact]
    public void Shuffle_RearrangesElements()
    {
        Sharpy.Random.Seed(42);
        var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var original = new List<int>(list);

        Sharpy.Random.Shuffle(list);

        // Same elements, just reordered
        list.Should().BeEquivalentTo(original);
        // Very unlikely to be in the same order with 10 elements
        list.Should().NotEqual(original);
    }

    [Fact]
    public void Shuffle_NullList_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Shuffle<int>(null!))
            .Should().Throw<Sharpy.TypeError>();
    }

    [Fact]
    public void Sample_ReturnsKUniqueElements()
    {
        Sharpy.Random.Seed(42);
        var population = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var sample = Sharpy.Random.Sample(population, 3);

        ((ICollection<int>)sample).Count.Should().Be(3);
        // All elements should be from the population
        foreach (var item in (IEnumerable<int>)sample)
        {
            population.Should().Contain(item);
        }
    }

    [Fact]
    public void Sample_KLargerThanPopulation_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Sample(new List<int> { 1, 2 }, 5))
            .Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Sample_NegativeK_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Sample(new List<int> { 1 }, -1))
            .Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Sample_ZeroK_ReturnsEmptyList()
    {
        Sharpy.Random.Seed(42);
        var sample = Sharpy.Random.Sample(new List<int> { 1, 2, 3 }, 0);

        ((ICollection<int>)sample).Count.Should().Be(0);
    }
}
