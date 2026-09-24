using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// <c>str.format</c> <c>[key]</c> fields (#1986): CPython's key rule — an all-decimal-digit key is an
/// <c>int</c>, anything else (including <c>-1</c>) a <c>str</c> — applied to every subscriptable
/// operand kind with its own messages. Every python row is python3 3.12.13
/// (<c>python3 -c "print(repr(TEMPLATE.format(ARG)))"</c>), quoted beside it.
/// </summary>
public class StrFormatItemAccessTests
{
    /// <summary>A CLR indexer — what a Sharpy <c>__getitem__</c> emits.</summary>
    private sealed class IntIndexed
    {
        public string this[int i] => "got:" + i;
    }

    private sealed class StrIndexed
    {
        public string this[string k] => "got:'" + k + "'";
    }

    public static IEnumerable<object[]> RenderingCells()
    {
        yield return new object[] { "{0[0]}", "ab", "a" };                                   // '{0[0]}'.format('ab') => 'a'
        yield return new object[] { "{0[1]}", new List<int>(new[] { 1, 2 }), "2" };          // '{0[1]}'.format([1, 2]) => '2'
        yield return new object[] { "{0[01]}", new List<int>(new[] { 1, 2 }), "2" };         // '{0[01]}'.format([1, 2]) => '2'
        yield return new object[] { "{0[٣]}", new List<int>(new[] { 1, 2, 3, 4 }), "4" }; // '{0[٣]}'.format([1, 2, 3, 4]) => '4' (Arabic-Indic 3 is a decimal digit)
        yield return new object[] { "{0[0]}", (7, 8), "7" };                                  // '{0[0]}'.format((7, 8)) => '7'
        yield return new object[] { "{0[k]}", new Dict<string, int> { ["k"] = 5 }, "5" };    // '{0[k]}'.format({'k': 5}) => '5'
        yield return new object[] { "{0[0]}", new Dict<int, int> { [0] = 5 }, "5" };        // '{0[0]}'.format({0: 5}) => '5'
        yield return new object[] { "{0[1]}", new Dict<double, string> { [1.0] = "x" }, "x" }; // '{0[1]}'.format({1.0: 'x'}) => 'x' (1 == 1.0)
        yield return new object[] { "{0[0][1]}", new List<object>(new object[] { new List<int>(new[] { 1, 2 }) }), "2" }; // '{0[0][1]}'.format([[1, 2]]) => '2'
        yield return new object[] { "{0[0]}", new Bytes(new byte[] { 97, 98 }), "97" };     // '{0[0]}'.format(b'ab') => '97'
        // Sharpy-only: the CLR indexer is the __getitem__ (python's class Ix: __getitem__ returns 'got:0').
        yield return new object[] { "{0[0]}", new IntIndexed(), "got:0" };
        yield return new object[] { "{0[ab]}", new StrIndexed(), "got:'ab'" };
    }

    [Theory]
    [MemberData(nameof(RenderingCells))]
    public void Format_ItemField_MatchesCPython(string template, object arg, string expected)
    {
        template.Format(arg).Should().Be(expected);
    }

    public static IEnumerable<object[]> RefusalCells()
    {
        yield return new object[] { "{0[5]}", "ab", typeof(IndexError), "string index out of range" };                        // '{0[5]}'.format('ab')
        yield return new object[] { "{0[x]}", "ab", typeof(TypeError), "string indices must be integers, not 'str'" };        // '{0[x]}'.format('ab')
        yield return new object[] { "{0[-1]}", "ab", typeof(TypeError), "string indices must be integers, not 'str'" };       // '{0[-1]}'.format('ab')
        yield return new object[] { "{0[-1]}", new List<int>(new[] { 1, 2 }), typeof(TypeError), "list indices must be integers or slices, not str" }; // '{0[-1]}'.format([1, 2])
        yield return new object[] { "{0[x]}", new List<int>(new[] { 1, 2 }), typeof(TypeError), "list indices must be integers or slices, not str" };  // '{0[x]}'.format([1, 2])
        yield return new object[] { "{0[²]}", new List<int>(new[] { 1, 2, 3 }), typeof(TypeError), "list indices must be integers or slices, not str" }; // '{0[²]}'.format([1, 2, 3]) — '²' is a digit but not decimal
        yield return new object[] { "{0[5]}", new List<int>(new[] { 1, 2 }), typeof(IndexError), "list index out of range" }; // '{0[5]}'.format([1, 2])
        yield return new object[] { "{0[x]}", (7, 8), typeof(TypeError), "tuple indices must be integers or slices, not str" }; // '{0[x]}'.format((7, 8))
        yield return new object[] { "{0[9]}", (7, 8), typeof(IndexError), "tuple index out of range" };                      // '{0[9]}'.format((7, 8))
        yield return new object[] { "{0[0]}", new Dict<string, int> { ["0"] = 5 }, typeof(KeyError), "0" };                // '{0[0]}'.format({'0': 5}) => KeyError: 0
        yield return new object[] { "{0[1]}", new Dict<int, int> { [0] = 5 }, typeof(KeyError), "1" };                     // '{0[1]}'.format({0: 5}) => KeyError: 1
        yield return new object[] { "{0[k]}", new Dict<int, int> { [0] = 5 }, typeof(KeyError), "'k'" };                   // '{0[k]}'.format({0: 5}) => KeyError: 'k'
        yield return new object[] { "{0[0]}", new Set<int>(new[] { 1, 2 }), typeof(TypeError), "'set' object is not subscriptable" }; // '{0[0]}'.format({1, 2})
        yield return new object[] { "{0[0]}", 5, typeof(TypeError), "'int' object is not subscriptable" };                   // '{0[0]}'.format(5)
        yield return new object[] { "{0[]}", new List<int>(new[] { 1 }), typeof(ValueError), "Empty attribute in format string" }; // '{0[]}'.format([1])
        yield return new object[] { "{0[99999999999999999999]}", new List<int>(new[] { 1 }), typeof(ValueError), "Too many decimal digits in format string" }; // '{0[99999999999999999999]}'.format([1])
    }

    [Theory]
    [MemberData(nameof(RefusalCells))]
    public void Format_ItemField_RefusesLikeCPython(string template, object arg, Type exceptionType, string message)
    {
        var ex = Assert.ThrowsAny<Exception>(() => template.Format(arg));
        ex.Should().BeOfType(exceptionType);
        ex.Message.Should().Be(message);
    }

    [Fact]
    public void Format_ItemField_OnNone_IsNotSubscriptable()
    {
        // python3 -c "'{0[0]}'.format(None)"  =>  TypeError: 'NoneType' object is not subscriptable
        var ex = Assert.Throws<TypeError>(() => "{0[0]}".Format(new object[] { null! }));
        ex.Message.Should().Be("'NoneType' object is not subscriptable");
    }
}
