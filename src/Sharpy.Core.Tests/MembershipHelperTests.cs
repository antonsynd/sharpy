using System;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

// Ground truth: CPython 3.15.2.
//   x = None; x in [1, 2] / {1} / range(3)  -> False
//   x = None; x in "abc" / b"abc"           -> TypeError
// The call sites below are spelled as the compiler emits them (needle, container, static
// lambda), so that they compiling is part of what is tested.
public class MembershipHelperTests
{
    private sealed class Tone
    {
        private readonly string _value;

        public Tone(string value) => _value = value;

        public static implicit operator string(Tone tone) => tone._value;
    }

    private sealed class Evens
    {
        public bool Contains(int n) => n % 2 == 0;
    }

    private sealed class CountingContainer
    {
        public int Calls { get; private set; }

        public bool Contains(int n)
        {
            Calls++;
            return true;
        }
    }

    // ---- ContainsOptional -------------------------------------------------------------

    [Fact]
    public void ContainsOptional_NoneNeedle_IsFalse_ForEveryContainerKind()
    {
        int? none = null;

        ContainsOptional(none, new Sharpy.List<int> { 0, 1, 2 }, static (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsOptional(none, new Sharpy.Set<int> { 0, 1, 2 }, static (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsOptional(none, new Sharpy.Dict<int, string> { [0] = "a" }, static (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsOptional(none, Range(3), static (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsOptional(none, new Evens(), static (c, n) => c.Contains(n)).Should().BeFalse();
    }

    [Fact]
    public void ContainsOptional_NoneNeedle_DoesNotAskTheContainer()
    {
        var container = new CountingContainer();

        ContainsOptional((int?)null, container, static (c, n) => c.Contains(n)).Should().BeFalse();
        container.Calls.Should().Be(0);

        // Positive control: the same container is asked for a present needle.
        ContainsOptional((int?)0, container, static (c, n) => c.Contains(n)).Should().BeTrue();
        container.Calls.Should().Be(1);
    }

    [Fact]
    public void ContainsOptional_NoneNeedle_IsFalse_EvenWhenDefaultValueIsPresent()
    {
        // default(int) == 0 is an element: None must not be read as its default.
        var xs = new Sharpy.List<int> { 0 };
        ContainsOptional((int?)null, xs, static (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsOptional((int?)0, xs, static (c, n) => c.Contains(n)).Should().BeTrue();

        var flags = new Sharpy.Set<bool> { false };
        ContainsOptional((bool?)null, flags, static (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsOptional((bool?)false, flags, static (c, n) => c.Contains(n)).Should().BeTrue();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(5, false)]
    [InlineData(-1, false)]
    public void ContainsOptional_ValueNeedle_AsksTheContainer(int value, bool expected)
    {
        int? needle = value;

        ContainsOptional(needle, new Sharpy.List<int> { 0, 1, 2 }, static (c, n) => c.Contains(n)).Should().Be(expected);
        ContainsOptional(needle, new Sharpy.Set<int> { 0, 1, 2 }, static (c, n) => c.Contains(n)).Should().Be(expected);
        ContainsOptional(needle, new Sharpy.Dict<int, string> { [0] = "a", [1] = "b", [2] = "c" }, static (c, n) => c.Contains(n)).Should().Be(expected);
        ContainsOptional(needle, Range(3), static (c, n) => c.Contains(n)).Should().Be(expected);
    }

    [Fact]
    public void ContainsOptional_UserContainsClass()
    {
        ContainsOptional((int?)4, new Evens(), static (c, n) => c.Contains(n)).Should().BeTrue();
        ContainsOptional((int?)3, new Evens(), static (c, n) => c.Contains(n)).Should().BeFalse();
    }

    [Fact]
    public void ContainsOptional_NeedleConvertsImplicitlyToTheContainersParameter()
    {
        // T is inferred from the needle (int); the lambda body converts it to Contains(long).
        var listOfLong = new Sharpy.List<long> { 1L, 2L };

        ContainsOptional((int?)1, listOfLong, (c, n) => c.Contains(n)).Should().BeTrue();
        ContainsOptional((int?)3, listOfLong, (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsOptional((int?)null, listOfLong, (c, n) => c.Contains(n)).Should().BeFalse();
    }

    [Fact]
    public void ContainsOptional_OtherValueNeedles()
    {
        ContainsOptional((double?)1.5, new Sharpy.List<double> { 1.5 }, static (c, n) => c.Contains(n)).Should().BeTrue();
        ContainsOptional((double?)null, new Sharpy.List<double> { 1.5 }, static (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsOptional((DayOfWeek?)DayOfWeek.Monday, new Sharpy.Set<DayOfWeek> { DayOfWeek.Monday }, static (c, n) => c.Contains(n)).Should().BeTrue();
        ContainsOptional((DayOfWeek?)null, new Sharpy.Set<DayOfWeek> { DayOfWeek.Sunday }, static (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsOptional(((int, int)?)(1, 2), new Sharpy.List<(int, int)> { (1, 2) }, static (c, n) => c.Contains(n)).Should().BeTrue();
    }

    [Fact]
    public void ContainsOptional_ArgumentOrder_EvaluatesNeedleBeforeContainer()
    {
        var order = new System.Collections.Generic.List<string>();
        int? Needle()
        {
            order.Add("needle");
            return 1;
        }

        Sharpy.List<int> Container()
        {
            order.Add("container");
            return new Sharpy.List<int> { 1 };
        }

        ContainsOptional(Needle(), Container(), static (c, n) => c.Contains(n)).Should().BeTrue();
        order.Should().Equal("needle", "container");
    }

    [Fact]
    public void ContainsOptional_NoneNeedle_StillEvaluatesTheContainerExpression()
    {
        var order = new System.Collections.Generic.List<string>();
        int? Needle()
        {
            order.Add("needle");
            return null;
        }

        Sharpy.List<int> Container()
        {
            order.Add("container");
            return new Sharpy.List<int> { 1 };
        }

        ContainsOptional(Needle(), Container(), static (c, n) => c.Contains(n)).Should().BeFalse();
        order.Should().Equal("needle", "container");
    }

    // ---- ContainsNoneRaises: reference needle -----------------------------------------

    [Fact]
    public void ContainsNoneRaises_NoneStringNeedle_RaisesTypeError()
    {
        Action act = () => ContainsNoneRaises((string?)null, "abc", static (c, n) => c.Contains(n));

        act.Should().ThrowExactly<Sharpy.TypeError>().WithMessage("*'in'*None*");
    }

    [Fact]
    public void ContainsNoneRaises_NoneNeedle_IsCatchableAsSharpyTypeError()
    {
        string? needle = null;
        var caught = false;
        try
        {
            ContainsNoneRaises(needle, "abc", static (c, n) => c.Contains(n));
        }
        catch (Sharpy.TypeError)
        {
            caught = true;
        }

        caught.Should().BeTrue();
    }

    [Theory]
    [InlineData("b", true)]
    [InlineData("bc", true)]
    [InlineData("", true)]
    [InlineData("x", false)]
    [InlineData("ca", false)]
    public void ContainsNoneRaises_StringNeedle_AsksTheContainer(string value, bool expected)
    {
        string? needle = value;

        ContainsNoneRaises(needle, "abc", static (c, n) => c.Contains(n)).Should().Be(expected);
    }

    [Fact]
    public void ContainsNoneRaises_NonNullableStringNeedle_Binds()
    {
        ContainsNoneRaises("b", "abc", static (c, n) => c.Contains(n)).Should().BeTrue();
    }

    [Fact]
    public void ContainsNoneRaises_BytesNeedle()
    {
        var haystack = new Sharpy.Bytes(new byte[] { 1, 2, 3 });
        Sharpy.Bytes? present = new Sharpy.Bytes(new byte[] { 2, 3 });
        Sharpy.Bytes? absent = new Sharpy.Bytes(new byte[] { 3, 2 });
        Sharpy.Bytes? none = null;

        ContainsNoneRaises(present, haystack, static (c, n) => c.Contains(n)).Should().BeTrue();
        ContainsNoneRaises(absent, haystack, static (c, n) => c.Contains(n)).Should().BeFalse();

        Action act = () => ContainsNoneRaises(none, haystack, static (c, n) => c.Contains(n));
        act.Should().ThrowExactly<Sharpy.TypeError>();
    }

    [Fact]
    public void ContainsNoneRaises_ClassWithImplicitStringConversion()
    {
        // A string-backed enum class: T is the class, the lambda body converts it to string.
        Tone? present = new Tone("b");
        Tone? absent = new Tone("x");
        Tone? none = null;

        ContainsNoneRaises(present, "abc", static (c, n) => c.Contains(n)).Should().BeTrue();
        ContainsNoneRaises(absent, "abc", static (c, n) => c.Contains(n)).Should().BeFalse();
        ContainsNoneRaises(new Tone("c"), "abc", static (c, n) => c.Contains(n)).Should().BeTrue();

        Action act = () => ContainsNoneRaises(none, "abc", static (c, n) => c.Contains(n));
        act.Should().ThrowExactly<Sharpy.TypeError>();
    }

    // ---- ContainsNoneRaises: value needle ---------------------------------------------

    [Fact]
    public void ContainsNoneRaises_NoneIntNeedle_RaisesTypeError()
    {
        var haystack = new Sharpy.Bytes(new byte[] { 0, 1, 2 });

        Action act = () => ContainsNoneRaises((int?)null, haystack, static (c, n) => c.Contains(n));

        // 0 == default(int) is an element: None must raise, not be read as its default.
        act.Should().ThrowExactly<Sharpy.TypeError>().WithMessage("*'in'*None*");
        ContainsNoneRaises((int?)0, haystack, static (c, n) => c.Contains(n)).Should().BeTrue();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(7, false)]
    public void ContainsNoneRaises_IntNeedle_AsksTheContainer(int value, bool expected)
    {
        int? needle = value;
        var haystack = new Sharpy.Bytes(new byte[] { 1, 2, 3 });

        ContainsNoneRaises(needle, haystack, static (c, n) => c.Contains(n)).Should().Be(expected);
    }

    [Fact]
    public void ContainsNoneRaises_ByteNeedle()
    {
        var haystack = new Sharpy.Bytes(new byte[] { 1, 2, 3 });

        // byte converts implicitly to Contains(int) in the lambda body.
        ContainsNoneRaises((byte?)2, haystack, static (c, n) => c.Contains(n)).Should().BeTrue();
        ContainsNoneRaises((byte?)9, haystack, static (c, n) => c.Contains(n)).Should().BeFalse();

        Action act = () => ContainsNoneRaises((byte?)null, haystack, static (c, n) => c.Contains(n));
        act.Should().ThrowExactly<Sharpy.TypeError>();
    }

    [Fact]
    public void ContainsNoneRaises_NoneNeedle_DoesNotAskTheContainer()
    {
        var container = new CountingContainer();

        Action act = () => ContainsNoneRaises((int?)null, container, static (c, n) => c.Contains(n));

        act.Should().ThrowExactly<Sharpy.TypeError>();
        container.Calls.Should().Be(0);

        // Positive control: the same container is asked for a present needle.
        ContainsNoneRaises((int?)1, container, static (c, n) => c.Contains(n)).Should().BeTrue();
        container.Calls.Should().Be(1);
    }

    [Fact]
    public void ContainsNoneRaises_ArgumentOrder_EvaluatesNeedleThenContainerThenRaises()
    {
        var order = new System.Collections.Generic.List<string>();
        string? Needle()
        {
            order.Add("needle");
            return null;
        }

        string Container()
        {
            order.Add("container");
            return "abc";
        }

        Action act = () => ContainsNoneRaises(Needle(), Container(), static (c, n) => c.Contains(n));

        // Python evaluates both operands before the membership test raises.
        act.Should().ThrowExactly<Sharpy.TypeError>();
        order.Should().Equal("needle", "container");
    }
}
