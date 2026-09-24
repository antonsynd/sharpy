using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// The ONE format-spec parser+validator, <see cref="PyFormatSpec.Validate"/> (#1984, R-BX), and the
/// engine that renders from its parse, <see cref="PyFormat.Apply(object, string)"/>. Every refusal
/// row is CPython 3.12's first error for that spec, generated with
/// <c>python3 -c "format(VALUE, 'SPEC')"</c> — the call and its output are quoted on the row.
/// </summary>
public class PyFormatTests
{
    /// <summary>
    /// The integer rules in CPython's order (<c>format_long_internal</c>): precision, then <c>z</c>,
    /// then sign-with-<c>c</c>. Each row carries two or more violations, so a reordered arm names
    /// the wrong rule. <c>z</c> precedes <c>#</c> in the grammar, so <c>#zc</c> is not a spec.
    /// </summary>
    public static IEnumerable<object[]> IntRuleOrderCells()
    {
        foreach (object value in new object[] { 65, true })
        {
            string name = value is bool ? "bool" : "int";
            // format(65, '<+z#010c') => ValueError: Negative zero coercion (z) not allowed in integer format specifier
            yield return new object[] { value, "<+z#010c", "Negative zero coercion (z) not allowed in integer format specifier" };
            // format(65, '+.3c')     => ValueError: Precision not allowed in integer format specifier
            yield return new object[] { value, "+.3c", "Precision not allowed in integer format specifier" };
            // format(65, '+zc')      => ValueError: Negative zero coercion (z) not allowed in integer format specifier
            yield return new object[] { value, "+zc", "Negative zero coercion (z) not allowed in integer format specifier" };
            // format(1, 'z.2d')      => ValueError: Precision not allowed in integer format specifier
            yield return new object[] { value, "z.2d", "Precision not allowed in integer format specifier" };
            // format(1, '+z.2c')     => ValueError: Precision not allowed in integer format specifier
            yield return new object[] { value, "+z.2c", "Precision not allowed in integer format specifier" };
            // format(1, 'z#c')       => ValueError: Negative zero coercion (z) not allowed in integer format specifier
            yield return new object[] { value, "z#c", "Negative zero coercion (z) not allowed in integer format specifier" };
            // format(1, '+#c')       => ValueError: Sign not allowed with integer format specifier 'c'
            yield return new object[] { value, "+#c", "Sign not allowed with integer format specifier 'c'" };
            // format(65, '#zc')      => ValueError: Invalid format specifier '#zc' for object of type 'int' ('bool' for True)
            yield return new object[] { value, "#zc", "Invalid format specifier '#zc' for object of type '" + name + "'" };
            // format(1, 'z.2q')      => ValueError: Unknown format code 'q' for object of type 'int'
            yield return new object[] { value, "z.2q", "Unknown format code 'q' for object of type '" + name + "'" };
        }
    }

    [Theory]
    [MemberData(nameof(IntRuleOrderCells))]
    public void Apply_IntegerRules_RefuseInCPythonOrder(object value, string spec, string message)
    {
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(value, spec));
        ex.Message.Should().Be(message);
    }

    /// <summary>
    /// Rules of the grammar itself (<c>parse_internal_render_format_spec</c>) and of CPython's
    /// message spelling for a non-printable presentation type. The missing-precision,
    /// both-separators, too-many-digits and <c>'\x..'</c> rows were absent from both twins (#2017).
    /// </summary>
    public static IEnumerable<object[]> ParseRuleCells()
    {
        // format(1.5, '.f') => ValueError: Format specifier missing precision
        yield return new object[] { 1.5, ".f", "Format specifier missing precision" };
        // format(1, '.')    => ValueError: Format specifier missing precision
        yield return new object[] { 1, ".", "Format specifier missing precision" };
        // format(1, '1.')   => ValueError: Format specifier missing precision
        yield return new object[] { 1, "1.", "Format specifier missing precision" };
        // format('ab', '.s') => ValueError: Format specifier missing precision
        yield return new object[] { "ab", ".s", "Format specifier missing precision" };
        // format(1, ',_')   => ValueError: Cannot specify both ',' and '_'.
        yield return new object[] { 1, ",_", "Cannot specify both ',' and '_'." };
        // format(1, '_,')   => ValueError: Cannot specify both ',' and '_'.
        yield return new object[] { 1, "_,", "Cannot specify both ',' and '_'." };
        // format(1, '_,d')  => ValueError: Cannot specify both ',' and '_'.
        yield return new object[] { 1, "_,d", "Cannot specify both ',' and '_'." };
        // format(5, ',,')   => ValueError: Cannot specify ',' with ','.
        yield return new object[] { 5, ",,", "Cannot specify ',' with ','." };
        // format(5.0, ',,') => ValueError: Cannot specify ',' with ','.
        yield return new object[] { 5.0, ",,", "Cannot specify ',' with ','." };
        // format(1, '99999999999999999999') => ValueError: Too many decimal digits in format string
        yield return new object[] { 1, "99999999999999999999", "Too many decimal digits in format string" };
        // format(1.5, '.99999999999999999999f') => ValueError: Too many decimal digits in format string
        yield return new object[] { 1.5, ".99999999999999999999f", "Too many decimal digits in format string" };
        // format(5, '5 ')   => ValueError: Unknown format code '\x20' for object of type 'int'
        yield return new object[] { 5, "5 ", "Unknown format code '\\x20' for object of type 'int'" };
        // format('a', '5 ') => ValueError: Unknown format code '\x20' for object of type 'str'
        yield return new object[] { "a", "5 ", "Unknown format code '\\x20' for object of type 'str'" };
        // format(1, 'é')    => ValueError: Unknown format code '\xe9' for object of type 'int'
        yield return new object[] { 1, "é", "Unknown format code '\\xe9' for object of type 'int'" };
        // format(1, '\x01') => ValueError: Unknown format code '\x1' for object of type 'int'
        yield return new object[] { 1, "\u0001", "Unknown format code '\\x1' for object of type 'int'" };
        // format(5, '\x7f') => ValueError: Unknown format code '<DEL>' for object of type 'int' (printed as the character)
        yield return new object[] { 5, "\u007f", "Unknown format code '\u007f' for object of type 'int'" };
        // format(1, ',é')   => ValueError: Cannot specify ',' with '\xe9'.
        yield return new object[] { 1, ",é", "Cannot specify ',' with '\\xe9'." };
        // format(1, ',.2q') => ValueError: Cannot specify ',' with 'q'.
        yield return new object[] { 1, ",.2q", "Cannot specify ',' with 'q'." };
        // format(1, '.2_x') => ValueError: Invalid format specifier '.2_x' for object of type 'int'
        yield return new object[] { 1, ".2_x", "Invalid format specifier '.2_x' for object of type 'int'" };
        // format(5, '*08')  => ValueError: Invalid format specifier '*08' for object of type 'int'
        yield return new object[] { 5, "*08", "Invalid format specifier '*08' for object of type 'int'" };
        // format('a', '+z#=5') => ValueError: Invalid format specifier '+z#=5' for object of type 'str'
        yield return new object[] { "a", "+z#=5", "Invalid format specifier '+z#=5' for object of type 'str'" };
        // format('a', '#=5') => ValueError: '=' alignment not allowed in string format specifier ('#' is the fill)
        yield return new object[] { "a", "#=5", "'=' alignment not allowed in string format specifier" };
        // format('a', '.2d') => ValueError: Unknown format code 'd' for object of type 'str'
        yield return new object[] { "a", ".2d", "Unknown format code 'd' for object of type 'str'" };
    }

    [Theory]
    [MemberData(nameof(ParseRuleCells))]
    public void Apply_GrammarRules_MatchCPython(object value, string spec, string message)
    {
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(value, spec));
        ex.Message.Should().Be(message);
    }

    /// <summary>
    /// The rendering cells beside the refusals: <c>=5</c> on an int and <c>08</c> on a str are legal.
    /// </summary>
    [Theory]
    [InlineData(5, "=5", "    5")]            // format(5, '=5')     => '    5'
    [InlineData("ab", "08", "ab000000")]      // format('ab', '08')  => 'ab000000'
    [InlineData(-1, "*=+08d", "-******1")]    // format(-1, '*=+08d') => '-******1'
    [InlineData(1, "z.2e", "1.00e+00")]       // format(1, 'z.2e')   => '1.00e+00' (z is legal on a float presentation)
    [InlineData(1, "#.2%", "100.00%")]        // format(1, '#.2%')   => '100.00%'
    [InlineData(1.5, "z,", "1.5")]            // format(1.5, 'z,')   => '1.5'
    [InlineData(1.5, ",", "1.5")]             // format(1.5, ',')    => '1.5'
    public void Apply_LegalNeighbours_Render(object value, string spec, string expected)
    {
        PyFormat.Apply(value, spec).Should().Be(expected);
    }

    // ---- The validator itself: the surface the compiler's static twin calls ----

    private static FormatOperandKind KindFor(object value) => value switch
    {
        bool => FormatOperandKind.Bool,
        string => FormatOperandKind.Str,
        double => FormatOperandKind.Float,
        _ => FormatOperandKind.Integral,
    };

    private static string NameFor(object value) => value switch
    {
        bool => "bool",
        string => "str",
        double => "float",
        _ => "int",
    };

    /// <summary>
    /// One rule, two consumers: for every refusal row, the value-free validator returns exactly the
    /// message and exception kind the engine raises — the static twin cannot drift from the runtime.
    /// </summary>
    [Theory]
    [MemberData(nameof(IntRuleOrderCells))]
    [MemberData(nameof(ParseRuleCells))]
    public void Validate_ReturnsWhatApplyRaises(object value, string spec, string message)
    {
        var error = PyFormatSpec.Validate(spec, KindFor(value), NameFor(value), out _);
        error.Should().NotBeNull();
        error!.Message.Should().Be(message);
        error.IsTypeError.Should().BeFalse();
        error.ToException().Should().BeOfType<ValueError>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(">10")]
    [InlineData("d")]
    [InlineData("garbage{{")]
    [InlineData(".f")]
    public void Validate_UnknownAndFormattable_AcceptAnySpec(string spec)
    {
        // The static twin never refuses an operand it cannot classify, and an IFormattable operand
        // owns its spec (python: a class with __format__ decides what its spec means).
        PyFormatSpec.Validate(spec, FormatOperandKind.Unknown, "object", out _).Should().BeNull();
        PyFormatSpec.Validate(spec, FormatOperandKind.Formattable, "F", out _).Should().BeNull();
    }

    [Fact]
    public void Validate_NoneValue_IsATypeError_BeforeParsing()
    {
        // python3 -c "format(None, 'garbage{{')"
        //   => TypeError: unsupported format string passed to NoneType.__format__
        var error = PyFormatSpec.Validate("garbage{{", FormatOperandKind.NoneValue, "NoneType", out _);
        error.Should().NotBeNull();
        error!.IsTypeError.Should().BeTrue();
        error.Message.Should().Be("unsupported format string passed to NoneType.__format__");
        error.ToException().Should().BeOfType<TypeError>();
        PyFormatSpec.Validate("", FormatOperandKind.NoneValue, "NoneType", out _).Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsTheParsedSpec()
    {
        // A fill before the align means the '0' is a width digit, not the zero flag.
        PyFormatSpec.Validate("*^+z#012,.3f", FormatOperandKind.Float, "float", out var p).Should().BeNull();
        p.Fill.Should().Be('*');
        p.Align.Should().Be('^');
        p.Sign.Should().Be('+');
        p.NegativeZeroCoercion.Should().BeTrue();
        p.AlternateForm.Should().BeTrue();
        p.Width.Should().Be(12);
        p.Grouping.Should().Be(',');
        p.Precision.Should().Be(3);
        p.HasPrecision.Should().BeTrue();
        p.Type.Should().Be('f');

        // The '0' flag: fill '0' and a synthesised '=' for a number ...
        PyFormatSpec.Validate("010", FormatOperandKind.Integral, "int", out var n).Should().BeNull();
        n.Fill.Should().Be('0');
        n.Align.Should().Be('=');
        n.Width.Should().Be(10);
        n.HasPrecision.Should().BeFalse();
        n.Type.Should().Be('\0');

        // ... but only the fill for a str, whose default alignment is '<' (#1945).
        PyFormatSpec.Validate("05", FormatOperandKind.Str, "str", out var s).Should().BeNull();
        s.Fill.Should().Be('0');
        s.Align.Should().Be('\0');
        s.Width.Should().Be(5);
    }
}
