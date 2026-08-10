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
    public void AugmentedOr_FollowsTheSameLeftOperandRule()
    {
        // `x |= y` is `x = x | y` — Sharpy has no `__ior__` (assignment_operators.md), so the
        // left-operand rule that types the binary form also decides what the variable holds.
        var s = S(1, 2);
        s |= F(2, 3);
        s.Should().BeOfType<Set<int>>().And.BeEquivalentTo(new[] { 1, 2, 3 });

        var f = F(1, 2);
        f |= S(2, 3);
        f.Should().BeOfType<FrozenSet<int>>().And.BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void AugmentedOr_RebindsRatherThanUpdatingInPlace()
    {
        // Both directions produce a new object. For the frozenset-on-the-left form that matches
        // CPython, which cannot update an immutable value. For the set-on-the-left form it does
        // not: CPython's `set |= other` calls `__ior__` and mutates, so an alias would see the
        // change. `update()` is the in-place spelling here.
        var s = S(1, 2);
        var sAlias = s;
        s |= F(2, 3);
        ReferenceEquals(s, sAlias).Should().BeFalse();
        sAlias.Should().BeEquivalentTo(new[] { 1, 2 }, "the alias still holds the original set");

        var f = F(1, 2);
        var fAlias = f;
        f |= S(2, 3);
        ReferenceEquals(f, fAlias).Should().BeFalse();
        fAlias.Should().BeEquivalentTo(new[] { 1, 2 });
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
