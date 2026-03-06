using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class RandomAdditional_Tests
{
    // --- Randrange ---

    [Fact]
    public void Randrange_Stop_ReturnsInRange()
    {
        Sharpy.Random.Seed(42);
        var result = Sharpy.Random.Randrange(10);
        result.Should().BeInRange(0, 9);
    }

    [Fact]
    public void Randrange_StartStop_ReturnsInRange()
    {
        Sharpy.Random.Seed(42);
        var result = Sharpy.Random.Randrange(5, 10);
        result.Should().BeInRange(5, 9);
    }

    [Fact]
    public void Randrange_WithStep_ReturnsValidValue()
    {
        Sharpy.Random.Seed(42);
        var result = Sharpy.Random.Randrange(0, 10, 2);
        (result % 2).Should().Be(0);
        result.Should().BeInRange(0, 8);
    }

    [Fact]
    public void Randrange_InvalidStop_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Randrange(0))
            .Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Randrange_ZeroStep_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Randrange(0, 10, 0))
            .Should().Throw<Sharpy.ValueError>();
    }

    // --- Gauss ---

    [Fact]
    public void Gauss_ReturnsFiniteValue()
    {
        Sharpy.Random.Seed(42);
        var result = Sharpy.Random.Gauss(0.0, 1.0);
        double.IsFinite(result).Should().BeTrue();
    }

    // --- Getrandbits ---

    [Fact]
    public void Getrandbits_ReturnsValueInRange()
    {
        Sharpy.Random.Seed(42);
        var result = Sharpy.Random.Getrandbits(8);
        result.Should().BeInRange(0, 255);
    }

    [Fact]
    public void Getrandbits_ZeroBits_ReturnsZero()
    {
        Sharpy.Random.Getrandbits(0).Should().Be(0);
    }

    [Fact]
    public void Getrandbits_NegativeBits_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Getrandbits(-1))
            .Should().Throw<Sharpy.ValueError>();
    }

    // --- Choices ---

    [Fact]
    public void Choices_ReturnsKItems()
    {
        Sharpy.Random.Seed(42);
        var pop = new[] { "a", "b", "c" };
        var result = Sharpy.Random.Choices<string>(pop, null, 5);
        ((System.Collections.Generic.ICollection<string>)result).Count.Should().Be(5);
    }

    [Fact]
    public void Choices_WithWeights_ReturnsKItems()
    {
        Sharpy.Random.Seed(42);
        var pop = new[] { "a", "b", "c" };
        var weights = new[] { 1.0, 1.0, 1.0 };
        var result = Sharpy.Random.Choices<string>(pop, weights, 3);
        ((System.Collections.Generic.ICollection<string>)result).Count.Should().Be(3);
    }

    [Fact]
    public void Choices_EmptyPopulation_ThrowsValueError()
    {
        FluentActions.Invoking(() => Sharpy.Random.Choices<int>(new int[0], null, 1))
            .Should().Throw<Sharpy.ValueError>();
    }

    // --- Sys additions ---

    [Fact]
    public void Sys_Maxsize_ReturnsIntMaxValue()
    {
        Sharpy.Sys.Maxsize.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Sys_Getsizeof_NullReturnsPositive()
    {
        Sharpy.Sys.Getsizeof(null).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sys_Getsizeof_StringReturnsReasonableSize()
    {
        Sharpy.Sys.Getsizeof("hello").Should().BeGreaterThan(40);
    }

    [Fact]
    public void Sys_Getsizeof_IntReturnsPositive()
    {
        Sharpy.Sys.Getsizeof(42).Should().BeGreaterThan(0);
    }
}
