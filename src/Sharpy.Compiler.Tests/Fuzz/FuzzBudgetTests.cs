using System;
using System.Collections.Generic;
using Xunit;

namespace Sharpy.Compiler.Tests.Fuzz;

/// <summary>
/// The fuzz knobs the nightly soak sets (<see cref="FuzzBudget"/>). Pure: these tests pass raw
/// strings to the parsers and never touch the process environment.
/// </summary>
public class FuzzBudgetTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("1", 1)]
    [InlineData("3", 3)]
    [InlineData("40", 40)]
    public void ParseScale_AcceptsUnsetOrAPositiveInteger(string? raw, int expected) =>
        Assert.Equal(expected, FuzzBudget.ParseScale(raw));

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("x")]
    [InlineData("1.5")]
    [InlineData(" ")]
    public void ParseScale_RefusesAnythingElse(string raw)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => FuzzBudget.ParseScale(raw));
        Assert.Contains(FuzzBudget.ScaleVariable, ex.Message);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("-7", -7)]
    [InlineData("123456", 123456)]
    public void ParseSalt_AcceptsUnsetOrAnInteger(string? raw, int expected) =>
        Assert.Equal(expected, FuzzBudget.ParseSalt(raw));

    [Theory]
    [InlineData("x")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public void ParseSalt_RefusesAnythingElse(string raw)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => FuzzBudget.ParseSalt(raw));
        Assert.Contains(FuzzBudget.SaltVariable, ex.Message);
    }

    /// <summary>Salt 0 is what a PR run uses: every seed the suite writes must stay as written.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(7777)]
    [InlineData(99999)]
    [InlineData(int.MaxValue)]
    public void Mix_WithZeroSalt_IsTheIdentity(int seed) =>
        Assert.Equal(seed, FuzzBudget.Mix(seed, 0));

    /// <summary>The nightly passes the run number as the salt; consecutive runs must not share a seed.</summary>
    [Fact]
    public void Mix_WithConsecutiveSalts_GivesDistinctSeedsThatDifferFromTheWrittenOne()
    {
        var seen = new HashSet<int>();
        for (int salt = 1; salt <= 1000; salt++)
        {
            var mixed = FuzzBudget.Mix(42, salt);
            Assert.NotEqual(42, mixed);
            Assert.True(seen.Add(mixed), $"salt {salt} repeats seed {mixed}");
        }
    }

    /// <summary>Positive control: a different seed reaches the fuzzer as different input text.</summary>
    [Fact]
    public void Mix_WithSalt_ChangesWhatTheFuzzerGenerates()
    {
        var written = new SharpyFuzzer(42).GenerateValidLooking();
        var unsalted = new SharpyFuzzer(FuzzBudget.Mix(42, 0)).GenerateValidLooking();
        var salted = new SharpyFuzzer(FuzzBudget.Mix(42, 1)).GenerateValidLooking();
        Assert.Equal(written, unsalted);
        Assert.NotEqual(written, salted);
    }
}
