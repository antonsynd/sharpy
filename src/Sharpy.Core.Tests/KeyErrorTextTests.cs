using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Python's KeyError carries the RAW key: its str is the key's repr and its repr is
/// <c>KeyError(&lt;repr of the key&gt;)</c>. The rule lives in <see cref="KeyError"/> alone, so every
/// raiser (dict/frozendict/set, str.format and format_map) must hand it the raw key. A raiser that
/// pre-quotes prints <c>KeyError("'k'")</c>; a KeyError that does not quote prints <c>k</c>. Expected
/// values are python3 3.12's measured output for the same program.
/// </summary>
public class KeyErrorTextTests
{
    private sealed class DerivedKeyError : KeyError
    {
        public DerivedKeyError(string key) : base(key) { }
    }

    public static IEnumerable<object[]> Cells()
    {
        // label, raiser, python str(e), python repr(e)
        yield return Cell("ctor str", () => throw new KeyError("k"), "'k'", "KeyError('k')");
        yield return Cell("ctor int", () => throw new KeyError(1), "1", "KeyError(1)");
        yield return Cell("ctor None", () => throw new KeyError(null), "None", "KeyError(None)");
        yield return Cell("ctor no arg", () => throw new KeyError(), "", "KeyError()");
        yield return Cell("ctor tuple", () => throw new KeyError((1, 2)), "(1, 2)", "KeyError((1, 2))");
        yield return Cell("ctor quote", () => throw new KeyError("it's"), "\"it's\"", "KeyError(\"it's\")");
        yield return Cell("subclass", () => throw new DerivedKeyError("m"), "'m'", "DerivedKeyError('m')");
        yield return Cell("dict[str]", () => _ = new Dict<string, int>()["k"], "'k'", "KeyError('k')");
        yield return Cell("dict[int]", () => _ = new Dict<int, int>()[3], "3", "KeyError(3)");
        yield return Cell("dict[tuple]", () => _ = new Dict<(int, int), int>()[(1, 2)], "(1, 2)", "KeyError((1, 2))");
        yield return Cell("dict.pop", () => new Dict<string, int>().Pop("zz"), "'zz'", "KeyError('zz')");
        yield return Cell("dict.popitem", () => new Dict<string, int>().PopItem(),
            "'popitem(): dictionary is empty'", "KeyError('popitem(): dictionary is empty')");
        yield return Cell("frozendict[str]", () => _ = new FrozenDict<string, int>()["k"], "'k'", "KeyError('k')");
        yield return Cell("set.remove", () => new Set<int>().Remove(5), "5", "KeyError(5)");
        yield return Cell("set.pop", () => new Set<int>().Pop(),
            "'pop from an empty set'", "KeyError('pop from an empty set')");
        yield return Cell("format keyword field", () => "{name}".Format("a"), "'name'", "KeyError('name')");
        yield return Cell("format_map", () => "{name}".FormatMap(new Dict<string, object>()), "'name'", "KeyError('name')");
        yield return Cell("format item str key", () => "{0[zz]}".Format(new Dict<string, int>()), "'zz'", "KeyError('zz')");
        yield return Cell("format item int key", () => "{0[1]}".Format(new Dict<int, int> { [0] = 5 }), "1", "KeyError(1)");
    }

    private static object[] Cell(string label, Action raise, string str, string repr) =>
        new object[] { label, raise, str, repr };

    [Theory]
    [MemberData(nameof(Cells))]
    public void KeyError_StrIsTheKeysRepr_ReprIsTheConstructorSpelling(string label, Action raise, string str, string repr)
    {
        var error = FluentActions.Invoking(raise).Should().Throw<KeyError>().Which;

        error.Message.Should().Be(str, label);
        Builtins.Str(error).Should().Be(str, label);
        Builtins.Repr(error).Should().Be(repr, label);
    }
}
