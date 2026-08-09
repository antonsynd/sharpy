using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// The mixed set/frozenset operators (#1312). CPython's left-operand rule decides the RESULT TYPE:
/// <c>set | frozenset</c> is a set, <c>frozenset | set</c> is a frozenset. Every expected value
/// below was checked against python3.
/// </summary>
public class FrozenSetMixedOperatorTests
{
    private static Set<int> S(params int[] items) => new(items);

    private static FrozenSet<int> F(params int[] items) => new(items);

    [Fact]
    public void SetOnTheLeft_YieldsASet()
    {
        (S(1, 2) | F(2, 3)).Should().BeOfType<Set<int>>().And.BeEquivalentTo(new[] { 1, 2, 3 });
        (S(1, 2) & F(2, 3)).Should().BeOfType<Set<int>>().And.BeEquivalentTo(new[] { 2 });
        (S(1, 2) - F(2, 3)).Should().BeOfType<Set<int>>().And.BeEquivalentTo(new[] { 1 });
        (S(1, 2) ^ F(2, 3)).Should().BeOfType<Set<int>>().And.BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void FrozenSetOnTheLeft_YieldsAFrozenSet()
    {
        (F(1, 2) | S(2, 3)).Should().BeOfType<FrozenSet<int>>().And.BeEquivalentTo(new[] { 1, 2, 3 });
        (F(1, 2) & S(2, 3)).Should().BeOfType<FrozenSet<int>>().And.BeEquivalentTo(new[] { 2 });
        (F(1, 2) - S(2, 3)).Should().BeOfType<FrozenSet<int>>().And.BeEquivalentTo(new[] { 1 });
        (F(1, 2) ^ S(2, 3)).Should().BeOfType<FrozenSet<int>>().And.BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void MixedComparisons_MatchSubsetSemantics()
    {
        (S(2) < F(1, 2)).Should().BeTrue();
        (S(1, 2) < F(1, 2)).Should().BeFalse("a set is not a PROPER subset of its equal");
        (S(1, 2) <= F(1, 2)).Should().BeTrue();
        (S(1, 2, 3) > F(1, 2)).Should().BeTrue();
        (S(1, 2) >= F(1, 2)).Should().BeTrue();

        (F(2) < S(1, 2)).Should().BeTrue();
        (F(1, 2) <= S(1, 2)).Should().BeTrue();
        (F(1, 2, 3) > S(1, 2)).Should().BeTrue();
        (F(1, 2) >= S(1, 2)).Should().BeTrue();
    }

    [Fact]
    public void EmptyOperands_BehaveAsIdentityOrAnnihilator()
    {
        (S() | F(1)).Should().BeEquivalentTo(new[] { 1 });
        (F() | S(1)).Should().BeEquivalentTo(new[] { 1 });
        (S(1) & F()).Should().BeEmpty();
        (F(1) & S()).Should().BeEmpty();
    }
}
