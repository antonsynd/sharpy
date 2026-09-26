using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// An ordering over elements compares with python's operator and operand order at the first
/// comparison that actually happens: it never refuses eagerly, never refuses an element when a key
/// is given, and never skips a comparison python makes (#2085). The refusal is python's two-operand
/// <c>'op' not supported between instances of 'A' and 'B'</c>. Every expected value is python3
/// 3.12's, recorded beside it.
/// </summary>
public class ComparisonRefusalTests
{
    private sealed class Plain
    {
    }

    private static string TypeErrorOf(Action action)
    {
        var ex = Assert.Throws<TypeError>(action);
        return ex.Message;
    }

    // python3: None < 1 / None >= 1
    [Theory]
    [InlineData("<", "'<' not supported between instances of 'NoneType' and 'int'")]
    [InlineData(">=", "'>=' not supported between instances of 'NoneType' and 'int'")]
    public void Comparison_NoneLeft_NamesBothOperands(string op, string expected)
    {
        object? none = null;
        TypeErrorOf(() => _ = op == "<" ? Operator.Lt<object?>(none, 1) : Operator.Ge<object?>(none, 1))
            .Should().Be(expected);
    }

    // python3: 1 <= None / 1 > None
    [Theory]
    [InlineData("<=", "'<=' not supported between instances of 'int' and 'NoneType'")]
    [InlineData(">", "'>' not supported between instances of 'int' and 'NoneType'")]
    public void Comparison_NoneRight_NamesBothOperands_AndItsOwnSymbol(string op, string expected)
    {
        object? none = null;
        TypeErrorOf(() => _ = op == "<=" ? Operator.Le<object?>(1, none) : Operator.Gt<object?>(1, none))
            .Should().Be(expected);
    }

    // python3: class Plain: pass; Plain() < Plain() / Plain() >= Plain() / Plain() <= Plain()
    [Fact]
    public void Comparison_NotComparable_UsesPythonWordingAndSymbol()
    {
        TypeErrorOf(() => _ = Operator.Lt(new Plain(), new Plain()))
            .Should().Be("'<' not supported between instances of 'Plain' and 'Plain'");
        TypeErrorOf(() => _ = Operator.Ge(new Plain(), new Plain()))
            .Should().Be("'>=' not supported between instances of 'Plain' and 'Plain'");
        TypeErrorOf(() => _ = Operator.Le(new Plain(), new Plain()))
            .Should().Be("'<=' not supported between instances of 'Plain' and 'Plain'");
    }

    // python3: s = 'a'; operator.le(s, s) True, operator.ge(s, s) True, operator.lt(s, s) False;
    // p = Plain(); operator.le(p, p) -> TypeError; operator.lt(None, None) -> TypeError
    [Fact]
    public void Comparison_SameObject_IsComparedNotShortCircuited()
    {
        string s = "a";
        Operator.Le(s, s).Should().BeTrue();
        Operator.Ge(s, s).Should().BeTrue();
        Operator.Lt(s, s).Should().BeFalse();
        Operator.Gt(s, s).Should().BeFalse();

        var p = new Plain();
        TypeErrorOf(() => _ = Operator.Le(p, p))
            .Should().Be("'<=' not supported between instances of 'Plain' and 'Plain'");
        object? none = null;
        TypeErrorOf(() => _ = Operator.Lt<object?>(none, none))
            .Should().Be("'<' not supported between instances of 'NoneType' and 'NoneType'");
    }

    // python3: sorted([]) == []; len(sorted([Plain()])) == 1; sorted([Plain(), Plain()]) -> TypeError (catchable)
    [Fact]
    public void Sorted_NotComparable_RefusesAtTheFirstComparison_Catchably()
    {
        Builtins.Len(Builtins.Sorted(Array.Empty<Plain>())).Should().Be(0);
        Builtins.Len(Builtins.Sorted(new[] { new Plain() })).Should().Be(1);
        TypeErrorOf(() => Builtins.Sorted(new[] { new Plain(), new Plain() }))
            .Should().Be("'<' not supported between instances of 'Plain' and 'Plain'");
    }

    // python3: sorted([None]) == [None]; sorted([None, None]) -> TypeError 'NoneType' and 'NoneType'
    [Fact]
    public void Sorted_None_IsComparedOnlyWhenAComparisonHappens()
    {
        Builtins.Sorted(new string?[] { null })[0].Should().BeNull();
        TypeErrorOf(() => Builtins.Sorted(new string?[] { null, null }))
            .Should().Be("'<' not supported between instances of 'NoneType' and 'NoneType'");
    }

    // python3: sorted([None, 1], key=lambda v: 0) == [None, 1]
    [Fact]
    public void SortedWithKey_ComparesKeys_NeverElements()
    {
        var sorted = Builtins.Sorted(new int?[] { null, 1 }, v => 0);
        sorted[0].Should().BeNull();
        sorted[1].Should().Be(1);
    }

    // python3: max([None]) is None; min([None]) is None; max([None], default=5) is None
    [Fact]
    public void MaxMin_SingleNone_IsReturned_NeverRefused()
    {
        Builtins.Max(new string?[] { null }).Should().BeNull();
        Builtins.Min(new string?[] { null }).Should().BeNull();
        Builtins.Max(new string?[] { null }, "d").Should().BeNull();
    }

    // python3: max([1, None]) / max([None, 1]) / min([1, None]) / max(1, None) / min(None, 1)
    [Fact]
    public void MaxMin_CompareInPythonsOperatorAndOperandOrder()
    {
        TypeErrorOf(() => Builtins.Max(new object?[] { 1, null }))
            .Should().Be("'>' not supported between instances of 'NoneType' and 'int'");
        TypeErrorOf(() => Builtins.Max(new object?[] { null, 1 }))
            .Should().Be("'>' not supported between instances of 'int' and 'NoneType'");
        TypeErrorOf(() => Builtins.Min(new object?[] { 1, null }))
            .Should().Be("'<' not supported between instances of 'NoneType' and 'int'");
        // The explicit params array pins the variadic overload: a bare `null` first argument would
        // bind Min(IEnumerable<T> iterable, T @default) instead.
        TypeErrorOf(() => Builtins.Max<object?>(1, null, Array.Empty<object?>()))
            .Should().Be("'>' not supported between instances of 'NoneType' and 'int'");
        TypeErrorOf(() => Builtins.Min<object?>(null, 1, Array.Empty<object?>()))
            .Should().Be("'<' not supported between instances of 'int' and 'NoneType'");
    }
}
