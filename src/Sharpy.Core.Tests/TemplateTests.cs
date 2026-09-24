using Xunit;

namespace Sharpy.Core.Tests;

public class TemplateTests
{
    [Fact]
    public void Constructor_ValidArguments_Succeeds()
    {
        var template = new Template(
            new[] { "Hello ", "!" },
            new[] { new Interpolation("world", "name", "") });

        Assert.Equal(2, template.Strings.Length);
        Assert.Single(template.Interpolations);
    }

    [Fact]
    public void Constructor_MismatchedLengths_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            new Template(
                new[] { "Hello" },
                new[] { new Interpolation("world", "name", "") }));
    }

    [Fact]
    public void Constructor_NullStrings_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new Template(null!, new Interpolation[0]));
    }

    [Fact]
    public void Constructor_NullInterpolations_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new Template(new[] { "hello" }, null!));
    }

    [Fact]
    public void ToString_NoInterpolations_ReturnsLiteralString()
    {
        var template = new Template(
            new[] { "Hello world" },
            System.Array.Empty<Interpolation>());

        Assert.Equal("Hello world", template.ToString());
    }

    [Fact]
    public void ToString_SingleInterpolation_FormatsLikeFString()
    {
        var template = new Template(
            new[] { "Hello ", "" },
            new[] { new Interpolation("world", "name", "") });

        Assert.Equal("Hello world", template.ToString());
    }

    [Fact]
    public void ToString_MultipleInterpolations_FormatsCorrectly()
    {
        var template = new Template(
            new[] { "", " is ", " years old" },
            new[]
            {
                new Interpolation("Alice", "name", ""),
                new Interpolation(30, "age", "")
            });

        Assert.Equal("Alice is 30 years old", template.ToString());
    }

    [Fact]
    public void Values_ReturnsInterpolationValues()
    {
        var template = new Template(
            new[] { "", " + ", "" },
            new[]
            {
                new Interpolation(1, "a", ""),
                new Interpolation(2, "b", "")
            });

        Assert.Equal(new object[] { 1, 2 }, template.Values);
    }

    [Fact]
    public void Concat_TwoTemplates_MergesCorrectly()
    {
        var left = new Template(
            new[] { "Hello ", "" },
            new[] { new Interpolation("world", "name", "") });

        var right = new Template(
            new[] { "! You are ", "." },
            new[] { new Interpolation(42, "age", "") });

        var result = left + right;

        Assert.Equal("Hello world! You are 42.", result.ToString());
        Assert.Equal(3, result.Strings.Length);
        Assert.Equal(2, result.Interpolations.Length);
        Assert.Equal("Hello ", result.Strings[0]);
        Assert.Equal("! You are ", result.Strings[1]);
        Assert.Equal(".", result.Strings[2]);
    }

    [Fact]
    public void Concat_BothNoInterpolations_MergesStrings()
    {
        var left = new Template(new[] { "Hello " }, System.Array.Empty<Interpolation>());
        var right = new Template(new[] { "world" }, System.Array.Empty<Interpolation>());

        var result = left + right;

        Assert.Equal("Hello world", result.ToString());
        Assert.Single(result.Strings);
        Assert.Equal("Hello world", result.Strings[0]);
    }

    [Fact]
    public void Repr_NoInterpolations_FormatsCorrectly()
    {
        var template = new Template(
            new[] { "hello" },
            System.Array.Empty<Interpolation>());

        // python3.14: repr(t"hello")  =>  Template(strings=('hello',), interpolations=())
        Assert.Equal("Template(strings=('hello',), interpolations=())", template.Repr());
    }

    [Fact]
    public void Repr_WithInterpolation_FormatsCorrectly()
    {
        var template = new Template(
            new[] { "Hello ", "" },
            new[] { new Interpolation("world", "name", "") });

        // python3.14: name = 'world'; repr(t"Hello {name}")
        //   =>  Template(strings=('Hello ', ''), interpolations=(Interpolation('world', 'name', None, ''),))
        Assert.Equal("Template(strings=('Hello ', ''), interpolations=(Interpolation('world', 'name', None, ''),))", template.Repr());
    }

    [Fact]
    public void GetEnumerator_YieldsInterleavedParts()
    {
        var template = new Template(
            new[] { "Hello ", " and ", "" },
            new[]
            {
                new Interpolation("world", "name", ""),
                new Interpolation("goodbye", "farewell", "")
            });

        var parts = new System.Collections.Generic.List<object>();
        foreach (var part in template)
        {
            parts.Add(part);
        }

        Assert.Equal(4, parts.Count);
        Assert.Equal("Hello ", parts[0]);
        Assert.IsType<Interpolation>(parts[1]);
        Assert.Equal(" and ", parts[2]);
        Assert.IsType<Interpolation>(parts[3]);
    }

    [Fact]
    public void GetEnumerator_SkipsEmptyStrings()
    {
        var template = new Template(
            new[] { "", "" },
            new[] { new Interpolation("value", "x", "") });

        var parts = new System.Collections.Generic.List<object>();
        foreach (var part in template)
        {
            parts.Add(part);
        }

        // Empty strings at start and end are skipped
        Assert.Single(parts);
        Assert.IsType<Interpolation>(parts[0]);
    }
}

public class InterpolationTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var interp = new Interpolation(42, "x", ".2f");

        Assert.Equal(42, interp.Value);
        Assert.Equal("x", interp.Expression);
        Assert.Equal(".2f", interp.FormatSpec);
    }

    [Fact]
    public void Constructor_NullExpression_DefaultsToEmpty()
    {
        var interp = new Interpolation(42, null!, "");

        Assert.Equal(string.Empty, interp.Expression);
    }

    [Fact]
    public void Constructor_NullFormatSpec_DefaultsToEmpty()
    {
        var interp = new Interpolation(42, "x", null!);

        Assert.Equal(string.Empty, interp.FormatSpec);
    }

    [Fact]
    public void ToString_NoFormatSpec_ReturnsValueString()
    {
        var interp = new Interpolation(42, "x", "");

        Assert.Equal("42", interp.ToString());
    }

    [Fact]
    public void ToString_WithFormatSpec_FormatsValue()
    {
        // The spec is a Python format spec (routed through PyFormat.Apply), the same language
        // str.format / format() / f-strings use — not a .NET format string.
        var interp = new Interpolation(3.14159, "pi", ".2f");

        Assert.Equal("3.14", interp.ToString());
    }

    [Fact]
    public void ToString_NullValue_ReturnsNone()
    {
        var interp = new Interpolation(null!, "x", "");

        Assert.Equal("None", interp.ToString());
    }

    [Fact]
    public void Repr_NoFormatSpec_FormatsCorrectly()
    {
        var interp = new Interpolation("hello", "greeting", "");

        // python3.14: greeting = 'hello'; repr(t"{greeting}".interpolations[0])
        //   =>  Interpolation('hello', 'greeting', None, '')
        Assert.Equal("Interpolation('hello', 'greeting', None, '')", interp.Repr());
    }

    [Fact]
    public void Repr_WithFormatSpec_IncludesFormatSpec()
    {
        var interp = new Interpolation(3.14, "pi", ".2f");

        // python3.14: pi = 3.14; repr(t"{pi:.2f}".interpolations[0])  =>  Interpolation(3.14, 'pi', None, '.2f')
        Assert.Equal("Interpolation(3.14, 'pi', None, '.2f')", interp.Repr());
    }

    // ---- #1970: the PEP 750 conversion slot (4th ctor argument) ----
    // Expected renders come from a python3.14 oracle rendering each PEP 750 Interpolation the way an
    // f-string renders the same field — format(convert(value, conversion), format_spec):
    //   python3.14 -c "from string.templatelib import *; s='ab'; i=t'{s!r:>6}'.interpolations[0]; print(repr(format(repr(i.value), i.format_spec)))"
    // This is the t-string RENDER (Sharpy's Template.ToString), which agrees with the f-string —
    // not Python's str(Template).

    [Theory]
    [InlineData("ab", "s", ">6", "r", "  'ab'")]      // f"{s!r:>6}"  (s = 'ab')
    [InlineData("ab", "s", "", "r", "'ab'")]          // f"{s!r}"
    [InlineData("ab", "s", ">6", "s", "    ab")]      // f"{s!s:>6}"
    [InlineData("é", "e", "", "a", "'\\xe9'")]        // f"{e!a}"     (e = 'é')
    [InlineData("é", "e", ">8", "a", "  '\\xe9'")]    // f"{e!a:>8}"
    [InlineData(5, "n", ">4", "r", "   5")]           // f"{n!r:>4}"  (n = 5)
    // A bool discriminates every conversion from none: str/repr/ascii spell True, format() spells 1.
    // python3 -c "b = True; print(repr(f'{b!r:>6}'), repr(f'{b!s:>6}'), repr(f'{b!a:>6}'), repr(f'{b:>6}'))"
    [InlineData(true, "b", ">6", "r", "  True")]      // f"{b!r:>6}"  (b = True)
    [InlineData(true, "b", ">6", "s", "  True")]      // f"{b!s:>6}"
    [InlineData(true, "b", ">6", "a", "  True")]      // f"{b!a:>6}"
    [InlineData(true, "b", ">6", null, "     1")]     // f"{b:>6}"
    [InlineData("ab", "s", ">6", null, "    ab")]     // f"{s:>6}" — no conversion: the value itself
    public void ToString_WithConversion_AppliesItBeforeTheFormatSpec(
        object value, string expression, string spec, string? conversion, string expected)
    {
        var interp = new Interpolation(value, expression, spec, conversion);

        Assert.Equal(conversion, interp.Conversion);
        Assert.Equal(expected, interp.ToString());
    }

    [Fact]
    public void ToString_ReprConversion_NullValue_IsNone()
    {
        // python3.14: nn = None; t"{nn!r}" renders 'None'
        Assert.Equal("None", new Interpolation(null!, "nn", "", "r").ToString());
    }

    [Fact]
    public void ToString_SelfDocumentingTemplate_RendersLikeTheFString()
    {
        // PEP 750 §"Interpolation" / python3.14: x = 1; t"{x=}" has strings ('x=', '') and
        // Interpolation(1, 'x', 'r', ''); f"{x=}" is 'x=1'.
        var template = new Template(new[] { "x=", "" }, new[] { new Interpolation(1, "x", "", "r") });

        Assert.Equal("x=1", template.ToString());
    }

    [Fact]
    public void Constructor_UnknownConversion_RaisesValueError()
    {
        // python3.14 -c "from string.templatelib import Interpolation; Interpolation(1, 'x', 'q')"
        //   =>  ValueError: Interpolation() argument 'conversion' must be one of 's', 'a' or 'r'
        var ex = Assert.Throws<ValueError>(() => new Interpolation(1, "x", "", "q"));
        Assert.Equal("Interpolation() argument 'conversion' must be one of 's', 'a' or 'r'", ex.Message);
    }

    [Fact]
    public void Repr_WithConversion_SpellsEveryPep750Position()
    {
        // PEP 750 §"The Interpolation Type": Interpolation(value, expression, conversion, format_spec).
        // python3.14: repr(t"{s!r:>6}".interpolations[0])  =>  Interpolation('ab', 's', 'r', '>6')
        Assert.Equal("Interpolation('ab', 's', 'r', '>6')", new Interpolation("ab", "s", ">6", "r").Repr());
        // python3.14: repr(t"{x=}".interpolations[0])  =>  Interpolation(1, 'x', 'r', '')
        Assert.Equal("Interpolation(1, 'x', 'r', '')", new Interpolation(1, "x", "", "r").Repr());
    }

    // ---- #1983 (R-BS): PEP 750 repr — python3.14 is the oracle for every expected string.
    //   /opt/homebrew/bin/python3.14 -c "from string.templatelib import Template as T, Interpolation as I; print(repr(...))"

    [Fact]
    public void Repr_StringsAndInterpolations_ArePythonTuplesOfEveryArity()
    {
        // python3.14: repr(T('a'))  =>  Template(strings=('a',), interpolations=())
        Assert.Equal("Template(strings=('a',), interpolations=())",
            new Template(new[] { "a" }, System.Array.Empty<Interpolation>()).Repr());
        // python3.14: repr(T('a', I(1, 'x', None, ''), 'b'))
        //   =>  Template(strings=('a', 'b'), interpolations=(Interpolation(1, 'x', None, ''),))
        Assert.Equal("Template(strings=('a', 'b'), interpolations=(Interpolation(1, 'x', None, ''),))",
            new Template(new[] { "a", "b" }, new[] { new Interpolation(1, "x", "") }).Repr());
        // python3.14: repr(T('a', I(1, 'x', None, ''), 'b', I(2, 'y', None, ''), 'c'))
        Assert.Equal(
            "Template(strings=('a', 'b', 'c'), interpolations=(Interpolation(1, 'x', None, ''), Interpolation(2, 'y', None, '')))",
            new Template(new[] { "a", "b", "c" }, new[] { new Interpolation(1, "x", ""), new Interpolation(2, "y", "") }).Repr());
    }

    private sealed class UserRepr
    {
        // A Sharpy class's __repr__/__str__ is ToString() in the dunder table.
        public override string ToString() => "U<1>";
    }

    public static TheoryData<object?, string> ValueReprRows() => new()
    {
        // python3.14: repr(I(v, 'v', None, '')) for each v
        { "ab", "Interpolation('ab', 'v', None, '')" },
        { 7, "Interpolation(7, 'v', None, '')" },
        { 2.0, "Interpolation(2.0, 'v', None, '')" },
        { new List<object> { 1, "a" }, "Interpolation([1, 'a'], 'v', None, '')" },
        { null, "Interpolation(None, 'v', None, '')" },
        { true, "Interpolation(True, 'v', None, '')" },
        // class U: def __repr__(self): return 'U<1>'
        { new UserRepr(), "Interpolation(U<1>, 'v', None, '')" },
    };

    [Theory]
    [MemberData(nameof(ValueReprRows))]
    public void Repr_Value_IsTheValuesRepr(object? value, string expected)
    {
        Assert.Equal(expected, new Interpolation(value!, "v", "").Repr());
    }

    [Theory]
    // python3.14: repr(I(1, 'x', c, '')) for c in None, 'r', 's', 'a'
    [InlineData(null, "Interpolation(1, 'x', None, '')")]
    [InlineData("r", "Interpolation(1, 'x', 'r', '')")]
    [InlineData("s", "Interpolation(1, 'x', 's', '')")]
    [InlineData("a", "Interpolation(1, 'x', 'a', '')")]
    public void Repr_Conversion_IsNoneOrTheQuotedLetter(string? conversion, string expected)
    {
        Assert.Equal(expected, new Interpolation(1, "x", "", conversion).Repr());
    }

    [Theory]
    // python3.14: repr(I(1, 'x', None, '')) / repr(I(1, 'x', None, '>6'))
    [InlineData("", "Interpolation(1, 'x', None, '')")]
    [InlineData(">6", "Interpolation(1, 'x', None, '>6')")]
    public void Repr_FormatSpec_IsAlwaysTheFourthPosition(string spec, string expected)
    {
        Assert.Equal(expected, new Interpolation(1, "x", spec).Repr());
    }

    [Fact]
    public void Repr_Expression_IsTheConstructedTextRepr()
    {
        // python3.14: repr(I(7, "d['k']", None, ''))  =>  Interpolation(7, "d['k']", None, '')
        Assert.Equal("Interpolation(7, \"d['k']\", None, '')", new Interpolation(7, "d['k']", "").Repr());
    }

    [Fact]
    public void BuiltinsRepr_ReachesTheTemplateAndInterpolationRepr()
    {
        var interp = new Interpolation(5, "x", ">6", "r");
        var template = new Template(new[] { "", "" }, new[] { interp });

        // python3.14: x = 5; repr(t"{x!r:>6}")
        //   =>  Template(strings=('', ''), interpolations=(Interpolation(5, 'x', 'r', '>6'),))
        Assert.Equal("Template(strings=('', ''), interpolations=(Interpolation(5, 'x', 'r', '>6'),))", Builtins.Repr(template));
        Assert.Equal("Interpolation(5, 'x', 'r', '>6')", Builtins.Repr(interp));
        // The renderer is untouched (Decision 11 / D4): str() and print() still render.
        Assert.Equal("     5", template.ToString());
        Assert.Equal("     5", interp.ToString());
    }

    [Fact]
    public void Ascii_EscapesTheRepr()
    {
        // python3.14: ascii(I('é', 'e', None, ''))  =>  Interpolation('\xe9', 'e', None, '')
        Assert.Equal("Interpolation('\\xe9', 'e', None, '')", Builtins.Ascii(new Interpolation("é", "e", "")));
        // python3.14: ascii(T('é', I('é', 'e', None, ''), ''))
        //   =>  Template(strings=('\xe9', ''), interpolations=(Interpolation('\xe9', 'e', None, ''),))
        Assert.Equal("Template(strings=('\\xe9', ''), interpolations=(Interpolation('\\xe9', 'e', None, ''),))",
            Builtins.Ascii(new Template(new[] { "é", "" }, new[] { new Interpolation("é", "e", "") })));
    }

    [Fact]
    public void Repr_NestedInAList_GoesThroughBuiltinsRepr()
    {
        // python3.14: repr([T('a'), I(1, 'x', None, '')])
        //   =>  [Template(strings=('a',), interpolations=()), Interpolation(1, 'x', None, '')]
        var xs = new List<object> { new Template(new[] { "a" }, System.Array.Empty<Interpolation>()), new Interpolation(1, "x", "") };
        Assert.Equal("[Template(strings=('a',), interpolations=()), Interpolation(1, 'x', None, '')]", Builtins.Repr(xs));
    }
}
