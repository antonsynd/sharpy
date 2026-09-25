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
    /// then sign-with-<c>c</c>, then <c>#</c>-with-<c>c</c> (#1978). Each row carries two or more violations, so a reordered arm names
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
            // #1978: '#' with 'c' is refused after the sign rule.
            // format(65, '#c')       => ValueError: Alternate form (#) not allowed with integer format specifier 'c'
            yield return new object[] { value, "#c", "Alternate form (#) not allowed with integer format specifier 'c'" };
            // format(65, '*=#5c')    => ValueError: Alternate form (#) not allowed with integer format specifier 'c'
            yield return new object[] { value, "*=#5c", "Alternate form (#) not allowed with integer format specifier 'c'" };
            // format(1, '<#10c')     => ValueError: Alternate form (#) not allowed with integer format specifier 'c'
            yield return new object[] { value, "<#10c", "Alternate form (#) not allowed with integer format specifier 'c'" };
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

    // ---- complex.__format__ (#2018): Complex implements IFormattable; one port of
    // CPython's format_complex_internal over the shared parser and float renderer ----

    /// <summary>
    /// Every row is python3 3.12.13: <c>format(complex(RE, IM), SPEC)</c>, quoted on the row. With
    /// no type the parts are the repr's (parenthesised unless the real part is +0); with a type both
    /// parts use it, the imaginary part always signed; the default alignment is <c>&gt;</c>.
    /// </summary>
    [Theory]
    [InlineData(1.0, 2.0, ">10", "    (1+2j)")] // format(complex(1, 2), '>10') => '    (1+2j)'
    [InlineData(1.0, 2.0, "10", "    (1+2j)")] // format(complex(1, 2), '10') => '    (1+2j)'
    [InlineData(1.0, 2.0, "<12", "(1+2j)      ")] // format(complex(1, 2), '<12') => '(1+2j)      '
    [InlineData(1.0, 2.0, "^14", "    (1+2j)    ")] // format(complex(1, 2), '^14') => '    (1+2j)    '
    [InlineData(1.0, 2.0, "*^16.2f", "***1.00+2.00j***")] // format(complex(1, 2), '*^16.2f') => '***1.00+2.00j***'
    [InlineData(1.0, 2.0, "", "(1+2j)")] // format(complex(1, 2), '') => '(1+2j)'
    [InlineData(1.0, 2.0, "+", "(+1+2j)")] // format(complex(1, 2), '+') => '(+1+2j)'
    [InlineData(1.0, 2.0, " ", "( 1+2j)")] // format(complex(1, 2), ' ') => '( 1+2j)'
    [InlineData(1.0, 2.0, "-", "(1+2j)")] // format(complex(1, 2), '-') => '(1+2j)'
    [InlineData(1.0, 2.0, ".2", "(1+2j)")] // format(complex(1, 2), '.2') => '(1+2j)'
    [InlineData(1.0, 2.0, ".0f", "1+2j")] // format(complex(1, 2), '.0f') => '1+2j'
    [InlineData(1.0, 2.0, ".2f", "1.00+2.00j")] // format(complex(1, 2), '.2f') => '1.00+2.00j'
    [InlineData(1.0, 2.0, "+.1f", "+1.0+2.0j")] // format(complex(1, 2), '+.1f') => '+1.0+2.0j'
    [InlineData(1.0, 2.0, "e", "1.000000e+00+2.000000e+00j")] // format(complex(1, 2), 'e') => '1.000000e+00+2.000000e+00j'
    [InlineData(1.0, 2.0, ".3e", "1.000e+00+2.000e+00j")] // format(complex(1, 2), '.3e') => '1.000e+00+2.000e+00j'
    [InlineData(1.0, 2.0, "E", "1.000000E+00+2.000000E+00j")] // format(complex(1, 2), 'E') => '1.000000E+00+2.000000E+00j'
    [InlineData(1.0, 2.0, "g", "1+2j")] // format(complex(1, 2), 'g') => '1+2j'
    [InlineData(1.0, 2.0, ".3g", "1+2j")] // format(complex(1, 2), '.3g') => '1+2j'
    [InlineData(1.0, 2.0, "G", "1+2j")] // format(complex(1, 2), 'G') => '1+2j'
    [InlineData(1.0, 2.0, "n", "1+2j")] // format(complex(1, 2), 'n') => '1+2j'
    [InlineData(1.0, 2.0, "z.1f", "1.0+2.0j")] // format(complex(1, 2), 'z.1f') => '1.0+2.0j'
    [InlineData(1.0, 2.0, "#", "(1.+2.j)")] // format(complex(1, 2), '#') => '(1.+2.j)'
    [InlineData(1.0, 2.0, "#g", "1.00000+2.00000j")] // format(complex(1, 2), '#g') => '1.00000+2.00000j'
    [InlineData(1.0, 2.0, ",.2f", "1.00+2.00j")] // format(complex(1, 2), ',.2f') => '1.00+2.00j'
    [InlineData(1.0, 2.0, "_f", "1.000000+2.000000j")] // format(complex(1, 2), '_f') => '1.000000+2.000000j'
    [InlineData(1.0, 2.0, ",", "(1+2j)")] // format(complex(1, 2), ',') => '(1+2j)'
    [InlineData(1.0, 2.0, ">", "(1+2j)")] // format(complex(1, 2), '>') => '(1+2j)'
    [InlineData(1.0, 2.0, ".3", "(1+2j)")] // format(complex(1, 2), '.3') => '(1+2j)'
    [InlineData(1.0, 2.0, "#.0f", "1.+2.j")] // format(complex(1, 2), '#.0f') => '1.+2.j'
    [InlineData(0.0, 2.0, ">10", "        2j")] // format(complex(0, 2), '>10') => '        2j'
    [InlineData(0.0, 2.0, "10", "        2j")] // format(complex(0, 2), '10') => '        2j'
    [InlineData(0.0, 2.0, "<12", "2j          ")] // format(complex(0, 2), '<12') => '2j          '
    [InlineData(0.0, 2.0, "^14", "      2j      ")] // format(complex(0, 2), '^14') => '      2j      '
    [InlineData(0.0, 2.0, "*^16.2f", "***0.00+2.00j***")] // format(complex(0, 2), '*^16.2f') => '***0.00+2.00j***'
    [InlineData(0.0, 2.0, "", "2j")] // format(complex(0, 2), '') => '2j'
    [InlineData(0.0, 2.0, "+", "+2j")] // format(complex(0, 2), '+') => '+2j'
    [InlineData(0.0, 2.0, " ", " 2j")] // format(complex(0, 2), ' ') => ' 2j'
    [InlineData(0.0, 2.0, "-", "2j")] // format(complex(0, 2), '-') => '2j'
    [InlineData(0.0, 2.0, ".2", "2j")] // format(complex(0, 2), '.2') => '2j'
    [InlineData(0.0, 2.0, ".0f", "0+2j")] // format(complex(0, 2), '.0f') => '0+2j'
    [InlineData(0.0, 2.0, ".2f", "0.00+2.00j")] // format(complex(0, 2), '.2f') => '0.00+2.00j'
    [InlineData(0.0, 2.0, "+.1f", "+0.0+2.0j")] // format(complex(0, 2), '+.1f') => '+0.0+2.0j'
    [InlineData(0.0, 2.0, "e", "0.000000e+00+2.000000e+00j")] // format(complex(0, 2), 'e') => '0.000000e+00+2.000000e+00j'
    [InlineData(0.0, 2.0, ".3e", "0.000e+00+2.000e+00j")] // format(complex(0, 2), '.3e') => '0.000e+00+2.000e+00j'
    [InlineData(0.0, 2.0, "E", "0.000000E+00+2.000000E+00j")] // format(complex(0, 2), 'E') => '0.000000E+00+2.000000E+00j'
    [InlineData(0.0, 2.0, "g", "0+2j")] // format(complex(0, 2), 'g') => '0+2j'
    [InlineData(0.0, 2.0, ".3g", "0+2j")] // format(complex(0, 2), '.3g') => '0+2j'
    [InlineData(0.0, 2.0, "G", "0+2j")] // format(complex(0, 2), 'G') => '0+2j'
    [InlineData(0.0, 2.0, "n", "0+2j")] // format(complex(0, 2), 'n') => '0+2j'
    [InlineData(0.0, 2.0, "z.1f", "0.0+2.0j")] // format(complex(0, 2), 'z.1f') => '0.0+2.0j'
    [InlineData(0.0, 2.0, "#", "2.j")] // format(complex(0, 2), '#') => '2.j'
    [InlineData(0.0, 2.0, "#g", "0.00000+2.00000j")] // format(complex(0, 2), '#g') => '0.00000+2.00000j'
    [InlineData(0.0, 2.0, ",.2f", "0.00+2.00j")] // format(complex(0, 2), ',.2f') => '0.00+2.00j'
    [InlineData(0.0, 2.0, "_f", "0.000000+2.000000j")] // format(complex(0, 2), '_f') => '0.000000+2.000000j'
    [InlineData(0.0, 2.0, ",", "2j")] // format(complex(0, 2), ',') => '2j'
    [InlineData(0.0, 2.0, ">", "2j")] // format(complex(0, 2), '>') => '2j'
    [InlineData(0.0, 2.0, ".3", "2j")] // format(complex(0, 2), '.3') => '2j'
    [InlineData(0.0, 2.0, "#.0f", "0.+2.j")] // format(complex(0, 2), '#.0f') => '0.+2.j'
    [InlineData(1.5, -0.0, ">10", "  (1.5-0j)")] // format(complex(1.5, -0.0), '>10') => '  (1.5-0j)'
    [InlineData(1.5, -0.0, "10", "  (1.5-0j)")] // format(complex(1.5, -0.0), '10') => '  (1.5-0j)'
    [InlineData(1.5, -0.0, "<12", "(1.5-0j)    ")] // format(complex(1.5, -0.0), '<12') => '(1.5-0j)    '
    [InlineData(1.5, -0.0, "^14", "   (1.5-0j)   ")] // format(complex(1.5, -0.0), '^14') => '   (1.5-0j)   '
    [InlineData(1.5, -0.0, "*^16.2f", "***1.50-0.00j***")] // format(complex(1.5, -0.0), '*^16.2f') => '***1.50-0.00j***'
    [InlineData(1.5, -0.0, "", "(1.5-0j)")] // format(complex(1.5, -0.0), '') => '(1.5-0j)'
    [InlineData(1.5, -0.0, "+", "(+1.5-0j)")] // format(complex(1.5, -0.0), '+') => '(+1.5-0j)'
    [InlineData(1.5, -0.0, " ", "( 1.5-0j)")] // format(complex(1.5, -0.0), ' ') => '( 1.5-0j)'
    [InlineData(1.5, -0.0, "-", "(1.5-0j)")] // format(complex(1.5, -0.0), '-') => '(1.5-0j)'
    [InlineData(1.5, -0.0, ".2", "(1.5-0j)")] // format(complex(1.5, -0.0), '.2') => '(1.5-0j)'
    [InlineData(1.5, -0.0, ".0f", "2-0j")] // format(complex(1.5, -0.0), '.0f') => '2-0j'
    [InlineData(1.5, -0.0, ".2f", "1.50-0.00j")] // format(complex(1.5, -0.0), '.2f') => '1.50-0.00j'
    [InlineData(1.5, -0.0, "+.1f", "+1.5-0.0j")] // format(complex(1.5, -0.0), '+.1f') => '+1.5-0.0j'
    [InlineData(1.5, -0.0, "e", "1.500000e+00-0.000000e+00j")] // format(complex(1.5, -0.0), 'e') => '1.500000e+00-0.000000e+00j'
    [InlineData(1.5, -0.0, ".3e", "1.500e+00-0.000e+00j")] // format(complex(1.5, -0.0), '.3e') => '1.500e+00-0.000e+00j'
    [InlineData(1.5, -0.0, "E", "1.500000E+00-0.000000E+00j")] // format(complex(1.5, -0.0), 'E') => '1.500000E+00-0.000000E+00j'
    [InlineData(1.5, -0.0, "g", "1.5-0j")] // format(complex(1.5, -0.0), 'g') => '1.5-0j'
    [InlineData(1.5, -0.0, ".3g", "1.5-0j")] // format(complex(1.5, -0.0), '.3g') => '1.5-0j'
    [InlineData(1.5, -0.0, "G", "1.5-0j")] // format(complex(1.5, -0.0), 'G') => '1.5-0j'
    [InlineData(1.5, -0.0, "n", "1.5-0j")] // format(complex(1.5, -0.0), 'n') => '1.5-0j'
    [InlineData(1.5, -0.0, "z.1f", "1.5+0.0j")] // format(complex(1.5, -0.0), 'z.1f') => '1.5+0.0j'
    [InlineData(1.5, -0.0, "#", "(1.5-0.j)")] // format(complex(1.5, -0.0), '#') => '(1.5-0.j)'
    [InlineData(1.5, -0.0, "#g", "1.50000-0.00000j")] // format(complex(1.5, -0.0), '#g') => '1.50000-0.00000j'
    [InlineData(1.5, -0.0, ",.2f", "1.50-0.00j")] // format(complex(1.5, -0.0), ',.2f') => '1.50-0.00j'
    [InlineData(1.5, -0.0, "_f", "1.500000-0.000000j")] // format(complex(1.5, -0.0), '_f') => '1.500000-0.000000j'
    [InlineData(1.5, -0.0, ",", "(1.5-0j)")] // format(complex(1.5, -0.0), ',') => '(1.5-0j)'
    [InlineData(1.5, -0.0, ">", "(1.5-0j)")] // format(complex(1.5, -0.0), '>') => '(1.5-0j)'
    [InlineData(1.5, -0.0, ".3", "(1.5-0j)")] // format(complex(1.5, -0.0), '.3') => '(1.5-0j)'
    [InlineData(1.5, -0.0, "#.0f", "2.-0.j")] // format(complex(1.5, -0.0), '#.0f') => '2.-0.j'
    [InlineData(0.0, 0.0, ">10", "        0j")] // format(complex(0, 0), '>10') => '        0j'
    [InlineData(0.0, 0.0, "10", "        0j")] // format(complex(0, 0), '10') => '        0j'
    [InlineData(0.0, 0.0, "<12", "0j          ")] // format(complex(0, 0), '<12') => '0j          '
    [InlineData(0.0, 0.0, "^14", "      0j      ")] // format(complex(0, 0), '^14') => '      0j      '
    [InlineData(0.0, 0.0, "*^16.2f", "***0.00+0.00j***")] // format(complex(0, 0), '*^16.2f') => '***0.00+0.00j***'
    [InlineData(0.0, 0.0, "", "0j")] // format(complex(0, 0), '') => '0j'
    [InlineData(0.0, 0.0, "+", "+0j")] // format(complex(0, 0), '+') => '+0j'
    [InlineData(0.0, 0.0, " ", " 0j")] // format(complex(0, 0), ' ') => ' 0j'
    [InlineData(0.0, 0.0, "-", "0j")] // format(complex(0, 0), '-') => '0j'
    [InlineData(0.0, 0.0, ".2", "0j")] // format(complex(0, 0), '.2') => '0j'
    [InlineData(0.0, 0.0, ".0f", "0+0j")] // format(complex(0, 0), '.0f') => '0+0j'
    [InlineData(0.0, 0.0, ".2f", "0.00+0.00j")] // format(complex(0, 0), '.2f') => '0.00+0.00j'
    [InlineData(0.0, 0.0, "+.1f", "+0.0+0.0j")] // format(complex(0, 0), '+.1f') => '+0.0+0.0j'
    [InlineData(0.0, 0.0, "e", "0.000000e+00+0.000000e+00j")] // format(complex(0, 0), 'e') => '0.000000e+00+0.000000e+00j'
    [InlineData(0.0, 0.0, ".3e", "0.000e+00+0.000e+00j")] // format(complex(0, 0), '.3e') => '0.000e+00+0.000e+00j'
    [InlineData(0.0, 0.0, "E", "0.000000E+00+0.000000E+00j")] // format(complex(0, 0), 'E') => '0.000000E+00+0.000000E+00j'
    [InlineData(0.0, 0.0, "g", "0+0j")] // format(complex(0, 0), 'g') => '0+0j'
    [InlineData(0.0, 0.0, ".3g", "0+0j")] // format(complex(0, 0), '.3g') => '0+0j'
    [InlineData(0.0, 0.0, "G", "0+0j")] // format(complex(0, 0), 'G') => '0+0j'
    [InlineData(0.0, 0.0, "n", "0+0j")] // format(complex(0, 0), 'n') => '0+0j'
    [InlineData(0.0, 0.0, "z.1f", "0.0+0.0j")] // format(complex(0, 0), 'z.1f') => '0.0+0.0j'
    [InlineData(0.0, 0.0, "#", "0.j")] // format(complex(0, 0), '#') => '0.j'
    [InlineData(0.0, 0.0, "#g", "0.00000+0.00000j")] // format(complex(0, 0), '#g') => '0.00000+0.00000j'
    [InlineData(0.0, 0.0, ",.2f", "0.00+0.00j")] // format(complex(0, 0), ',.2f') => '0.00+0.00j'
    [InlineData(0.0, 0.0, "_f", "0.000000+0.000000j")] // format(complex(0, 0), '_f') => '0.000000+0.000000j'
    [InlineData(0.0, 0.0, ",", "0j")] // format(complex(0, 0), ',') => '0j'
    [InlineData(0.0, 0.0, ">", "0j")] // format(complex(0, 0), '>') => '0j'
    [InlineData(0.0, 0.0, ".3", "0j")] // format(complex(0, 0), '.3') => '0j'
    [InlineData(0.0, 0.0, "#.0f", "0.+0.j")] // format(complex(0, 0), '#.0f') => '0.+0.j'
    [InlineData(-1.0, -2.0, ">10", "   (-1-2j)")] // format(complex(-1, -2), '>10') => '   (-1-2j)'
    [InlineData(-1.0, -2.0, "10", "   (-1-2j)")] // format(complex(-1, -2), '10') => '   (-1-2j)'
    [InlineData(-1.0, -2.0, "<12", "(-1-2j)     ")] // format(complex(-1, -2), '<12') => '(-1-2j)     '
    [InlineData(-1.0, -2.0, "^14", "   (-1-2j)    ")] // format(complex(-1, -2), '^14') => '   (-1-2j)    '
    [InlineData(-1.0, -2.0, "*^16.2f", "**-1.00-2.00j***")] // format(complex(-1, -2), '*^16.2f') => '**-1.00-2.00j***'
    [InlineData(-1.0, -2.0, "", "(-1-2j)")] // format(complex(-1, -2), '') => '(-1-2j)'
    [InlineData(-1.0, -2.0, "+", "(-1-2j)")] // format(complex(-1, -2), '+') => '(-1-2j)'
    [InlineData(-1.0, -2.0, " ", "(-1-2j)")] // format(complex(-1, -2), ' ') => '(-1-2j)'
    [InlineData(-1.0, -2.0, "-", "(-1-2j)")] // format(complex(-1, -2), '-') => '(-1-2j)'
    [InlineData(-1.0, -2.0, ".2", "(-1-2j)")] // format(complex(-1, -2), '.2') => '(-1-2j)'
    [InlineData(-1.0, -2.0, ".0f", "-1-2j")] // format(complex(-1, -2), '.0f') => '-1-2j'
    [InlineData(-1.0, -2.0, ".2f", "-1.00-2.00j")] // format(complex(-1, -2), '.2f') => '-1.00-2.00j'
    [InlineData(-1.0, -2.0, "+.1f", "-1.0-2.0j")] // format(complex(-1, -2), '+.1f') => '-1.0-2.0j'
    [InlineData(-1.0, -2.0, "e", "-1.000000e+00-2.000000e+00j")] // format(complex(-1, -2), 'e') => '-1.000000e+00-2.000000e+00j'
    [InlineData(-1.0, -2.0, ".3e", "-1.000e+00-2.000e+00j")] // format(complex(-1, -2), '.3e') => '-1.000e+00-2.000e+00j'
    [InlineData(-1.0, -2.0, "E", "-1.000000E+00-2.000000E+00j")] // format(complex(-1, -2), 'E') => '-1.000000E+00-2.000000E+00j'
    [InlineData(-1.0, -2.0, "g", "-1-2j")] // format(complex(-1, -2), 'g') => '-1-2j'
    [InlineData(-1.0, -2.0, ".3g", "-1-2j")] // format(complex(-1, -2), '.3g') => '-1-2j'
    [InlineData(-1.0, -2.0, "G", "-1-2j")] // format(complex(-1, -2), 'G') => '-1-2j'
    [InlineData(-1.0, -2.0, "n", "-1-2j")] // format(complex(-1, -2), 'n') => '-1-2j'
    [InlineData(-1.0, -2.0, "z.1f", "-1.0-2.0j")] // format(complex(-1, -2), 'z.1f') => '-1.0-2.0j'
    [InlineData(-1.0, -2.0, "#", "(-1.-2.j)")] // format(complex(-1, -2), '#') => '(-1.-2.j)'
    [InlineData(-1.0, -2.0, "#g", "-1.00000-2.00000j")] // format(complex(-1, -2), '#g') => '-1.00000-2.00000j'
    [InlineData(-1.0, -2.0, ",.2f", "-1.00-2.00j")] // format(complex(-1, -2), ',.2f') => '-1.00-2.00j'
    [InlineData(-1.0, -2.0, "_f", "-1.000000-2.000000j")] // format(complex(-1, -2), '_f') => '-1.000000-2.000000j'
    [InlineData(-1.0, -2.0, ",", "(-1-2j)")] // format(complex(-1, -2), ',') => '(-1-2j)'
    [InlineData(-1.0, -2.0, ">", "(-1-2j)")] // format(complex(-1, -2), '>') => '(-1-2j)'
    [InlineData(-1.0, -2.0, ".3", "(-1-2j)")] // format(complex(-1, -2), '.3') => '(-1-2j)'
    [InlineData(-1.0, -2.0, "#.0f", "-1.-2.j")] // format(complex(-1, -2), '#.0f') => '-1.-2.j'
    [InlineData(1e+20, 1.0, ">10", "(1e+20+1j)")] // format(complex(1e+20, 1), '>10') => '(1e+20+1j)'
    [InlineData(1e+20, 1.0, "10", "(1e+20+1j)")] // format(complex(1e+20, 1), '10') => '(1e+20+1j)'
    [InlineData(1e+20, 1.0, "<12", "(1e+20+1j)  ")] // format(complex(1e+20, 1), '<12') => '(1e+20+1j)  '
    [InlineData(1e+20, 1.0, "^14", "  (1e+20+1j)  ")] // format(complex(1e+20, 1), '^14') => '  (1e+20+1j)  '
    [InlineData(1e+20, 1.0, "*^16.2f", "100000000000000000000.00+1.00j")] // format(complex(1e+20, 1), '*^16.2f') => '100000000000000000000.00+1.00j'
    [InlineData(1e+20, 1.0, "", "(1e+20+1j)")] // format(complex(1e+20, 1), '') => '(1e+20+1j)'
    [InlineData(1e+20, 1.0, "+", "(+1e+20+1j)")] // format(complex(1e+20, 1), '+') => '(+1e+20+1j)'
    [InlineData(1e+20, 1.0, " ", "( 1e+20+1j)")] // format(complex(1e+20, 1), ' ') => '( 1e+20+1j)'
    [InlineData(1e+20, 1.0, "-", "(1e+20+1j)")] // format(complex(1e+20, 1), '-') => '(1e+20+1j)'
    [InlineData(1e+20, 1.0, ".2", "(1e+20+1j)")] // format(complex(1e+20, 1), '.2') => '(1e+20+1j)'
    [InlineData(1e+20, 1.0, ".0f", "100000000000000000000+1j")] // format(complex(1e+20, 1), '.0f') => '100000000000000000000+1j'
    [InlineData(1e+20, 1.0, ".2f", "100000000000000000000.00+1.00j")] // format(complex(1e+20, 1), '.2f') => '100000000000000000000.00+1.00j'
    [InlineData(1e+20, 1.0, "+.1f", "+100000000000000000000.0+1.0j")] // format(complex(1e+20, 1), '+.1f') => '+100000000000000000000.0+1.0j'
    [InlineData(1e+20, 1.0, "e", "1.000000e+20+1.000000e+00j")] // format(complex(1e+20, 1), 'e') => '1.000000e+20+1.000000e+00j'
    [InlineData(1e+20, 1.0, ".3e", "1.000e+20+1.000e+00j")] // format(complex(1e+20, 1), '.3e') => '1.000e+20+1.000e+00j'
    [InlineData(1e+20, 1.0, "E", "1.000000E+20+1.000000E+00j")] // format(complex(1e+20, 1), 'E') => '1.000000E+20+1.000000E+00j'
    [InlineData(1e+20, 1.0, "g", "1e+20+1j")] // format(complex(1e+20, 1), 'g') => '1e+20+1j'
    [InlineData(1e+20, 1.0, ".3g", "1e+20+1j")] // format(complex(1e+20, 1), '.3g') => '1e+20+1j'
    [InlineData(1e+20, 1.0, "G", "1E+20+1j")] // format(complex(1e+20, 1), 'G') => '1E+20+1j'
    [InlineData(1e+20, 1.0, "n", "1e+20+1j")] // format(complex(1e+20, 1), 'n') => '1e+20+1j'
    [InlineData(1e+20, 1.0, "z.1f", "100000000000000000000.0+1.0j")] // format(complex(1e+20, 1), 'z.1f') => '100000000000000000000.0+1.0j'
    [InlineData(1e+20, 1.0, "#", "(1.e+20+1.j)")] // format(complex(1e+20, 1), '#') => '(1.e+20+1.j)'
    [InlineData(1e+20, 1.0, "#g", "1.00000e+20+1.00000j")] // format(complex(1e+20, 1), '#g') => '1.00000e+20+1.00000j'
    [InlineData(1e+20, 1.0, ",.2f", "100,000,000,000,000,000,000.00+1.00j")] // format(complex(1e+20, 1), ',.2f') => '100,000,000,000,000,000,000.00+1.00j'
    [InlineData(1e+20, 1.0, "_f", "100_000_000_000_000_000_000.000000+1.000000j")] // format(complex(1e+20, 1), '_f') => '100_000_000_000_000_000_000.000000+1.000000j'
    [InlineData(1e+20, 1.0, ",", "(1e+20+1j)")] // format(complex(1e+20, 1), ',') => '(1e+20+1j)'
    [InlineData(1e+20, 1.0, ">", "(1e+20+1j)")] // format(complex(1e+20, 1), '>') => '(1e+20+1j)'
    [InlineData(1e+20, 1.0, ".3", "(1e+20+1j)")] // format(complex(1e+20, 1), '.3') => '(1e+20+1j)'
    [InlineData(1e+20, 1.0, "#.0f", "100000000000000000000.+1.j")] // format(complex(1e+20, 1), '#.0f') => '100000000000000000000.+1.j'
    [InlineData(0.1, 0.2, ">10", "(0.1+0.2j)")] // format(complex(0.1, 0.2), '>10') => '(0.1+0.2j)'
    [InlineData(0.1, 0.2, "10", "(0.1+0.2j)")] // format(complex(0.1, 0.2), '10') => '(0.1+0.2j)'
    [InlineData(0.1, 0.2, "<12", "(0.1+0.2j)  ")] // format(complex(0.1, 0.2), '<12') => '(0.1+0.2j)  '
    [InlineData(0.1, 0.2, "^14", "  (0.1+0.2j)  ")] // format(complex(0.1, 0.2), '^14') => '  (0.1+0.2j)  '
    [InlineData(0.1, 0.2, "*^16.2f", "***0.10+0.20j***")] // format(complex(0.1, 0.2), '*^16.2f') => '***0.10+0.20j***'
    [InlineData(0.1, 0.2, "", "(0.1+0.2j)")] // format(complex(0.1, 0.2), '') => '(0.1+0.2j)'
    [InlineData(0.1, 0.2, "+", "(+0.1+0.2j)")] // format(complex(0.1, 0.2), '+') => '(+0.1+0.2j)'
    [InlineData(0.1, 0.2, " ", "( 0.1+0.2j)")] // format(complex(0.1, 0.2), ' ') => '( 0.1+0.2j)'
    [InlineData(0.1, 0.2, "-", "(0.1+0.2j)")] // format(complex(0.1, 0.2), '-') => '(0.1+0.2j)'
    [InlineData(0.1, 0.2, ".2", "(0.1+0.2j)")] // format(complex(0.1, 0.2), '.2') => '(0.1+0.2j)'
    [InlineData(0.1, 0.2, ".0f", "0+0j")] // format(complex(0.1, 0.2), '.0f') => '0+0j'
    [InlineData(0.1, 0.2, ".2f", "0.10+0.20j")] // format(complex(0.1, 0.2), '.2f') => '0.10+0.20j'
    [InlineData(0.1, 0.2, "+.1f", "+0.1+0.2j")] // format(complex(0.1, 0.2), '+.1f') => '+0.1+0.2j'
    [InlineData(0.1, 0.2, "e", "1.000000e-01+2.000000e-01j")] // format(complex(0.1, 0.2), 'e') => '1.000000e-01+2.000000e-01j'
    [InlineData(0.1, 0.2, ".3e", "1.000e-01+2.000e-01j")] // format(complex(0.1, 0.2), '.3e') => '1.000e-01+2.000e-01j'
    [InlineData(0.1, 0.2, "E", "1.000000E-01+2.000000E-01j")] // format(complex(0.1, 0.2), 'E') => '1.000000E-01+2.000000E-01j'
    [InlineData(0.1, 0.2, "g", "0.1+0.2j")] // format(complex(0.1, 0.2), 'g') => '0.1+0.2j'
    [InlineData(0.1, 0.2, ".3g", "0.1+0.2j")] // format(complex(0.1, 0.2), '.3g') => '0.1+0.2j'
    [InlineData(0.1, 0.2, "G", "0.1+0.2j")] // format(complex(0.1, 0.2), 'G') => '0.1+0.2j'
    [InlineData(0.1, 0.2, "n", "0.1+0.2j")] // format(complex(0.1, 0.2), 'n') => '0.1+0.2j'
    [InlineData(0.1, 0.2, "z.1f", "0.1+0.2j")] // format(complex(0.1, 0.2), 'z.1f') => '0.1+0.2j'
    [InlineData(0.1, 0.2, "#", "(0.1+0.2j)")] // format(complex(0.1, 0.2), '#') => '(0.1+0.2j)'
    [InlineData(0.1, 0.2, "#g", "0.100000+0.200000j")] // format(complex(0.1, 0.2), '#g') => '0.100000+0.200000j'
    [InlineData(0.1, 0.2, ",.2f", "0.10+0.20j")] // format(complex(0.1, 0.2), ',.2f') => '0.10+0.20j'
    [InlineData(0.1, 0.2, "_f", "0.100000+0.200000j")] // format(complex(0.1, 0.2), '_f') => '0.100000+0.200000j'
    [InlineData(0.1, 0.2, ",", "(0.1+0.2j)")] // format(complex(0.1, 0.2), ',') => '(0.1+0.2j)'
    [InlineData(0.1, 0.2, ">", "(0.1+0.2j)")] // format(complex(0.1, 0.2), '>') => '(0.1+0.2j)'
    [InlineData(0.1, 0.2, ".3", "(0.1+0.2j)")] // format(complex(0.1, 0.2), '.3') => '(0.1+0.2j)'
    [InlineData(0.1, 0.2, "#.0f", "0.+0.j")] // format(complex(0.1, 0.2), '#.0f') => '0.+0.j'
    [InlineData(12345.5, -6789.0, ">10", "(12345.5-6789j)")] // format(complex(12345.5, -6789), '>10') => '(12345.5-6789j)'
    [InlineData(12345.5, -6789.0, "10", "(12345.5-6789j)")] // format(complex(12345.5, -6789), '10') => '(12345.5-6789j)'
    [InlineData(12345.5, -6789.0, "<12", "(12345.5-6789j)")] // format(complex(12345.5, -6789), '<12') => '(12345.5-6789j)'
    [InlineData(12345.5, -6789.0, "^14", "(12345.5-6789j)")] // format(complex(12345.5, -6789), '^14') => '(12345.5-6789j)'
    [InlineData(12345.5, -6789.0, "*^16.2f", "12345.50-6789.00j")] // format(complex(12345.5, -6789), '*^16.2f') => '12345.50-6789.00j'
    [InlineData(12345.5, -6789.0, "", "(12345.5-6789j)")] // format(complex(12345.5, -6789), '') => '(12345.5-6789j)'
    [InlineData(12345.5, -6789.0, "+", "(+12345.5-6789j)")] // format(complex(12345.5, -6789), '+') => '(+12345.5-6789j)'
    [InlineData(12345.5, -6789.0, " ", "( 12345.5-6789j)")] // format(complex(12345.5, -6789), ' ') => '( 12345.5-6789j)'
    [InlineData(12345.5, -6789.0, "-", "(12345.5-6789j)")] // format(complex(12345.5, -6789), '-') => '(12345.5-6789j)'
    [InlineData(12345.5, -6789.0, ".2", "(1.2e+04-6.8e+03j)")] // format(complex(12345.5, -6789), '.2') => '(1.2e+04-6.8e+03j)'
    [InlineData(12345.5, -6789.0, ".0f", "12346-6789j")] // format(complex(12345.5, -6789), '.0f') => '12346-6789j'
    [InlineData(12345.5, -6789.0, ".2f", "12345.50-6789.00j")] // format(complex(12345.5, -6789), '.2f') => '12345.50-6789.00j'
    [InlineData(12345.5, -6789.0, "+.1f", "+12345.5-6789.0j")] // format(complex(12345.5, -6789), '+.1f') => '+12345.5-6789.0j'
    [InlineData(12345.5, -6789.0, "e", "1.234550e+04-6.789000e+03j")] // format(complex(12345.5, -6789), 'e') => '1.234550e+04-6.789000e+03j'
    [InlineData(12345.5, -6789.0, ".3e", "1.235e+04-6.789e+03j")] // format(complex(12345.5, -6789), '.3e') => '1.235e+04-6.789e+03j'
    [InlineData(12345.5, -6789.0, "E", "1.234550E+04-6.789000E+03j")] // format(complex(12345.5, -6789), 'E') => '1.234550E+04-6.789000E+03j'
    [InlineData(12345.5, -6789.0, "g", "12345.5-6789j")] // format(complex(12345.5, -6789), 'g') => '12345.5-6789j'
    [InlineData(12345.5, -6789.0, ".3g", "1.23e+04-6.79e+03j")] // format(complex(12345.5, -6789), '.3g') => '1.23e+04-6.79e+03j'
    [InlineData(12345.5, -6789.0, "G", "12345.5-6789j")] // format(complex(12345.5, -6789), 'G') => '12345.5-6789j'
    [InlineData(12345.5, -6789.0, "n", "12345.5-6789j")] // format(complex(12345.5, -6789), 'n') => '12345.5-6789j'
    [InlineData(12345.5, -6789.0, "z.1f", "12345.5-6789.0j")] // format(complex(12345.5, -6789), 'z.1f') => '12345.5-6789.0j'
    [InlineData(12345.5, -6789.0, "#", "(12345.5-6789.j)")] // format(complex(12345.5, -6789), '#') => '(12345.5-6789.j)'
    [InlineData(12345.5, -6789.0, "#g", "12345.5-6789.00j")] // format(complex(12345.5, -6789), '#g') => '12345.5-6789.00j'
    [InlineData(12345.5, -6789.0, ",.2f", "12,345.50-6,789.00j")] // format(complex(12345.5, -6789), ',.2f') => '12,345.50-6,789.00j'
    [InlineData(12345.5, -6789.0, "_f", "12_345.500000-6_789.000000j")] // format(complex(12345.5, -6789), '_f') => '12_345.500000-6_789.000000j'
    [InlineData(12345.5, -6789.0, ",", "(12,345.5-6,789j)")] // format(complex(12345.5, -6789), ',') => '(12,345.5-6,789j)'
    [InlineData(12345.5, -6789.0, ">", "(12345.5-6789j)")] // format(complex(12345.5, -6789), '>') => '(12345.5-6789j)'
    [InlineData(12345.5, -6789.0, ".3", "(1.23e+04-6.79e+03j)")] // format(complex(12345.5, -6789), '.3') => '(1.23e+04-6.79e+03j)'
    [InlineData(12345.5, -6789.0, "#.0f", "12346.-6789.j")] // format(complex(12345.5, -6789), '#.0f') => '12346.-6789.j'
    [InlineData(double.PositiveInfinity, double.NaN, ">10", "(inf+nanj)")]            // format(complex(inf, nan), '>10') => '(inf+nanj)'
    [InlineData(double.PositiveInfinity, double.NaN, "E", "INF+NANj")]                // format(complex(inf, nan), 'E') => 'INF+NANj'
    [InlineData(double.PositiveInfinity, double.NaN, "*^16.2f", "****inf+nanj****")]  // format(complex(inf, nan), '*^16.2f') => '****inf+nanj****'
    [InlineData(double.PositiveInfinity, double.NaN, "+", "(+inf+nanj)")]             // format(complex(inf, nan), '+') => '(+inf+nanj)'
    public void Apply_Complex_MatchesCPython(double re, double im, string spec, string expected)
    {
        PyFormat.Apply(new Complex(re, im), spec).Should().Be(expected);
        // The same rendering through the CLR spelling of __format__.
        ((System.IFormattable)new Complex(re, im)).ToString(spec, null).Should().Be(expected);
    }

    /// <summary>
    /// A negative-zero real part keeps the parentheses (<c>str(complex(-0.0, 2))</c> is
    /// <c>(-0+2j)</c>). xUnit treats <c>-0.0</c> and <c>0.0</c> rows as duplicates, so these rows
    /// negate a positive-zero real inside the test.
    /// </summary>
    [Theory]
    [InlineData(2.0, ">10", "   (-0+2j)")] // format(complex(-0.0, 2), '>10') => '   (-0+2j)'
    [InlineData(2.0, "10", "   (-0+2j)")] // format(complex(-0.0, 2), '10') => '   (-0+2j)'
    [InlineData(2.0, "<12", "(-0+2j)     ")] // format(complex(-0.0, 2), '<12') => '(-0+2j)     '
    [InlineData(2.0, "^14", "   (-0+2j)    ")] // format(complex(-0.0, 2), '^14') => '   (-0+2j)    '
    [InlineData(2.0, "*^16.2f", "**-0.00+2.00j***")] // format(complex(-0.0, 2), '*^16.2f') => '**-0.00+2.00j***'
    [InlineData(2.0, "", "(-0+2j)")] // format(complex(-0.0, 2), '') => '(-0+2j)'
    [InlineData(2.0, "+", "(-0+2j)")] // format(complex(-0.0, 2), '+') => '(-0+2j)'
    [InlineData(2.0, " ", "(-0+2j)")] // format(complex(-0.0, 2), ' ') => '(-0+2j)'
    [InlineData(2.0, "-", "(-0+2j)")] // format(complex(-0.0, 2), '-') => '(-0+2j)'
    [InlineData(2.0, ".2", "(-0+2j)")] // format(complex(-0.0, 2), '.2') => '(-0+2j)'
    [InlineData(2.0, ".0f", "-0+2j")] // format(complex(-0.0, 2), '.0f') => '-0+2j'
    [InlineData(2.0, ".2f", "-0.00+2.00j")] // format(complex(-0.0, 2), '.2f') => '-0.00+2.00j'
    [InlineData(2.0, "+.1f", "-0.0+2.0j")] // format(complex(-0.0, 2), '+.1f') => '-0.0+2.0j'
    [InlineData(2.0, "e", "-0.000000e+00+2.000000e+00j")] // format(complex(-0.0, 2), 'e') => '-0.000000e+00+2.000000e+00j'
    [InlineData(2.0, ".3e", "-0.000e+00+2.000e+00j")] // format(complex(-0.0, 2), '.3e') => '-0.000e+00+2.000e+00j'
    [InlineData(2.0, "E", "-0.000000E+00+2.000000E+00j")] // format(complex(-0.0, 2), 'E') => '-0.000000E+00+2.000000E+00j'
    [InlineData(2.0, "g", "-0+2j")] // format(complex(-0.0, 2), 'g') => '-0+2j'
    [InlineData(2.0, ".3g", "-0+2j")] // format(complex(-0.0, 2), '.3g') => '-0+2j'
    [InlineData(2.0, "G", "-0+2j")] // format(complex(-0.0, 2), 'G') => '-0+2j'
    [InlineData(2.0, "n", "-0+2j")] // format(complex(-0.0, 2), 'n') => '-0+2j'
    [InlineData(2.0, "z.1f", "0.0+2.0j")] // format(complex(-0.0, 2), 'z.1f') => '0.0+2.0j'
    [InlineData(2.0, "#", "(-0.+2.j)")] // format(complex(-0.0, 2), '#') => '(-0.+2.j)'
    [InlineData(2.0, "#g", "-0.00000+2.00000j")] // format(complex(-0.0, 2), '#g') => '-0.00000+2.00000j'
    [InlineData(2.0, ",.2f", "-0.00+2.00j")] // format(complex(-0.0, 2), ',.2f') => '-0.00+2.00j'
    [InlineData(2.0, "_f", "-0.000000+2.000000j")] // format(complex(-0.0, 2), '_f') => '-0.000000+2.000000j'
    [InlineData(2.0, ",", "(-0+2j)")] // format(complex(-0.0, 2), ',') => '(-0+2j)'
    [InlineData(2.0, ">", "(-0+2j)")] // format(complex(-0.0, 2), '>') => '(-0+2j)'
    [InlineData(2.0, ".3", "(-0+2j)")] // format(complex(-0.0, 2), '.3') => '(-0+2j)'
    [InlineData(2.0, "#.0f", "-0.+2.j")] // format(complex(-0.0, 2), '#.0f') => '-0.+2.j'
    public void Apply_Complex_NegativeZeroReal_MatchesCPython(double im, string spec, string expected)
    {
        var value = new Complex(-0.0, im);
        PyFormat.Apply(value, spec).Should().Be(expected);
        ((System.IFormattable)value).ToString(spec, null).Should().Be(expected);
    }

    [Theory]
    [InlineData("d", "Unknown format code 'd' for object of type 'complex'")]              // format(1+2j, 'd')
    [InlineData("%", "Unknown format code '%' for object of type 'complex'")]              // format(1+2j, '%')
    [InlineData("s", "Unknown format code 's' for object of type 'complex'")]              // format(1+2j, 's')
    [InlineData("0>10", "Zero padding is not allowed in complex format specifier")]        // format(1+2j, '0>10')
    [InlineData("010", "Zero padding is not allowed in complex format specifier")]         // format(1+2j, '010')
    [InlineData("=10", "'=' alignment flag is not allowed in complex format specifier")]   // format(1+2j, '=10')
    [InlineData("*=10", "'=' alignment flag is not allowed in complex format specifier")]  // format(1+2j, '*=10')
    [InlineData(",n", "Cannot specify ',' with 'n'.")]                                     // format(1+2j, ',n')
    [InlineData(".f", "Format specifier missing precision")]                               // format(1+2j, '.f')
    public void Apply_Complex_RefusesWithCPythonWording(string spec, string message)
    {
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(new Complex(1, 2), spec));
        ex.Message.Should().Be(message);
        var error = PyFormatSpec.Validate(spec, FormatOperandKind.Complex, "complex", out _);
        error.Should().NotBeNull();
        error!.Message.Should().Be(message);
    }

    // ---- #1988 (R-BY): the operand-kind axis — no __format__ is a TypeError, IFormattable owns
    // its spec, an enum formats as its str ----

    /// <summary>A class with no <c>__format__</c> spelling (python: <c>class C: pass</c>).</summary>
    private sealed class C
    {
    }

    /// <summary>
    /// The CLR spelling of python's <c>class F: def __format__(self, s): return "F&lt;" + s + "&gt;"</c>.
    /// </summary>
    private sealed class F : System.IFormattable
    {
        public string ToString(string? format, System.IFormatProvider? formatProvider) => "F<" + format + ">";

        public override string ToString() => "F()";
    }

    private static class Outer
    {
        public sealed class Inner<T>
        {
        }
    }

    public static IEnumerable<object[]> NoFormatOperands()
    {
        // python3: format(v, '>10') / format(v, 'd') / format(v, 'garbage{{')
        //   => TypeError: unsupported format string passed to <name>.__format__   (every row)
        yield return new object[] { new List<int>(new[] { 1, 2 }), "list" };            // [1, 2]
        yield return new object[] { new Dict<string, int> { ["a"] = 1 }, "dict" };      // {'a': 1}
        yield return new object[] { new Set<int>(new[] { 1 }), "set" };                 // {1}
        yield return new object[] { (1, 2), "tuple" };                                  // (1, 2)
        yield return new object[] { new FrozenSet<int>(new[] { 1 }), "frozenset" };     // frozenset([1])
        yield return new object[] { new Bytes(new byte[] { 97, 98 }), "bytes" };        // b'ab'
        yield return new object[] { new C(), "C" };                                     // class C: pass
        yield return new object[] { new ValueError("x"), "ValueError" };                // ValueError('x')
        // Sharpy-only rows (no python twin): Optional has no __format__, a nested generic class
        // prints its own name without the arity.
        yield return new object[] { Optional<int>.Some(1), "Optional" };
        yield return new object[] { new Outer.Inner<int>(), "Inner" };
    }

    [Theory]
    [MemberData(nameof(NoFormatOperands))]
    public void Apply_NoFormatOperand_NonEmptySpec_IsATypeError(object value, string pyName)
    {
        foreach (var spec in new[] { ">10", "d", "garbage{{" })
        {
            var ex = Assert.Throws<TypeError>(() => PyFormat.Apply(value, spec));
            ex.Message.Should().Be("unsupported format string passed to " + pyName + ".__format__");
        }
        // python3: format(v, '') == str(v) — never refused.
        PyFormat.Apply(value, "").Should().Be(Builtins.Str(value));
    }

    [Theory]
    [InlineData(">10", "F<>10>")]         // python3: format(F(), '>10') => 'F<>10>'
    [InlineData("d", "F<d>")]             // python3: format(F(), 'd') => 'F<d>'
    [InlineData("garbage{{", "F<garbage{{>")] // python3: format(F(), 'garbage{{') => 'F<garbage{{>'
    public void Apply_FormattableOperand_OwnsItsSpec(string spec, string expected)
    {
        PyFormat.Apply(new F(), spec).Should().Be(expected);
        "{0:>10}".Format(new F()).Should().Be("F<>10>");
    }

    [Fact]
    public void Apply_FormattableOperand_EmptySpec_IsItsOwnRendering()
    {
        // #2031 (R-CC): an empty spec is __format__(''), so a type that owns its spec is asked —
        // python3: format(F(), '') == 'F<>' — not str(value) ('F()').
        PyFormat.Apply(new F(), "").Should().Be("F<>");
    }

    [Fact]
    public void Apply_ClrFormattable_IsDelegated()
    {
        // Sharpy-only (a CLR type, no python twin): the spec is the type's own.
        PyFormat.Apply(new System.DateTime(2020, 1, 2), "yyyy").Should().Be("2020");
    }

    [Fact]
    public void Apply_Enum_FormatsAsItsStr()
    {
        // python3: format(Color.RED, '>10') => ' Color.RED' (str.__format__ of str(self)). Every
        // System.Enum takes python's enum spelling, a CLR interop enum included (#2007, owner ruling
        // "all"): str(DayOfWeek.Monday) is 'DayOfWeek.Monday', and the spec applies to that text.
        PyFormat.Apply(System.DayOfWeek.Monday, ">18").Should().Be("  DayOfWeek.Monday");
        // python3: format(Color.RED, 'd') => ValueError: Unknown format code 'd' for object of type 'str'
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(System.DayOfWeek.Monday, "d"));
        ex.Message.Should().Be("Unknown format code 'd' for object of type 'str'");
    }

    [Fact]
    public void StrFormat_NoFormatOperand_IsATypeError()
    {
        // python3: '{0:>10}'.format([1, 2]) => TypeError: unsupported format string passed to list.__format__
        var ex = Assert.Throws<TypeError>(() => "{0:>10}".Format(new List<int>(new[] { 1, 2 })));
        ex.Message.Should().Be("unsupported format string passed to list.__format__");
    }

    // ---- #1989: 'z' coerces every all-zero rendering, read off the rendered mantissa ----

    /// <summary>
    /// PEP 682 coerces on the RENDERED text: a <c>-</c> whose mantissa digits are all zero loses its
    /// sign under every float presentation — <c>e</c>/<c>E</c> exponent forms and <c>%</c> included,
    /// and <c>-1e-9</c> under <c>z.1%</c> (it renders <c>-0.0%</c>). A non-zero mantissa keeps it.
    /// Every row is python3 3.12.13, quoted with the z-less rendering beside it.
    /// </summary>
    [Theory]
    [InlineData(-0.0, "z.0e", "0e+00")]              // format(-0.0, 'z.0e') => '0e+00' (without z: '-0e+00')
    [InlineData(-0.0, "z.1e", "0.0e+00")]            // format(-0.0, 'z.1e') => '0.0e+00' (without z: '-0.0e+00')
    [InlineData(-0.0, "ze", "0.000000e+00")]         // format(-0.0, 'ze') => '0.000000e+00' (without z: '-0.000000e+00')
    [InlineData(-0.0, "zE", "0.000000E+00")]         // format(-0.0, 'zE') => '0.000000E+00' (without z: '-0.000000E+00')
    [InlineData(-0.0, "z.2E", "0.00E+00")]           // format(-0.0, 'z.2E') => '0.00E+00' (without z: '-0.00E+00')
    [InlineData(-0.0, "z%", "0.000000%")]            // format(-0.0, 'z%') => '0.000000%' (without z: '-0.000000%')
    [InlineData(-0.0, "z.1%", "0.0%")]               // format(-0.0, 'z.1%') => '0.0%' (without z: '-0.0%')
    [InlineData(-0.0, "z.0%", "0%")]                 // format(-0.0, 'z.0%') => '0%' (without z: '-0%')
    [InlineData(-0.0, "z.1", "0e+00")]               // format(-0.0, 'z.1') => '0e+00' (without z: '-0e+00')
    [InlineData(-0.0, "z.3", "0.0")]                 // format(-0.0, 'z.3') => '0.0' (without z: '-0.0')
    [InlineData(-0.0, "zg", "0")]                    // format(-0.0, 'zg') => '0' (without z: '-0')
    [InlineData(-0.0, "z.1g", "0")]                  // format(-0.0, 'z.1g') => '0' (without z: '-0')
    [InlineData(-0.0, "zG", "0")]                    // format(-0.0, 'zG') => '0' (without z: '-0')
    [InlineData(-0.0, "z.2G", "0")]                  // format(-0.0, 'z.2G') => '0' (without z: '-0')
    [InlineData(-0.0, "zn", "0")]                    // format(-0.0, 'zn') => '0' (without z: '-0')
    [InlineData(-0.0, "z.1n", "0")]                  // format(-0.0, 'z.1n') => '0' (without z: '-0')
    [InlineData(-0.0, "z", "0.0")]                   // format(-0.0, 'z') => '0.0' (without z: '-0.0')
    [InlineData(-0.0, "+z.0e", "+0e+00")]            // format(-0.0, '+z.0e') => '+0e+00' (without z: '-0e+00')
    [InlineData(-0.0, "z.1f", "0.0")]                // format(-0.0, 'z.1f') => '0.0' (without z: '-0.0')
    [InlineData(-0.0, "z,.1f", "0.0")]               // format(-0.0, 'z,.1f') => '0.0' (without z: '-0.0')
    [InlineData(-0.0, "z_.3%", "0.000%")]            // format(-0.0, 'z_.3%') => '0.000%' (without z: '-0.000%')
    [InlineData(-0.0, "z#.0e", "0.e+00")]            // format(-0.0, 'z#.0e') => '0.e+00' (without z: '-0.e+00')
    [InlineData(-0.0, "z#.0%", "0.%")]               // format(-0.0, 'z#.0%') => '0.%' (without z: '-0.%')
    [InlineData(-0.0, " z.0e", " 0e+00")]            // format(-0.0, ' z.0e') => ' 0e+00' (without z: '-0e+00')
    [InlineData(-0.0, "z>10.1e", "zz-0.0e+00")]      // format(-0.0, 'z>10.1e') => 'zz-0.0e+00' (without z: '  -0.0e+00')
    [InlineData(-1e-09, "z.0e", "-1e-09")]           // format(-1e-09, 'z.0e') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "z.1e", "-1.0e-09")]         // format(-1e-09, 'z.1e') => '-1.0e-09' (without z: '-1.0e-09')
    [InlineData(-1e-09, "ze", "-1.000000e-09")]      // format(-1e-09, 'ze') => '-1.000000e-09' (without z: '-1.000000e-09')
    [InlineData(-1e-09, "zE", "-1.000000E-09")]      // format(-1e-09, 'zE') => '-1.000000E-09' (without z: '-1.000000E-09')
    [InlineData(-1e-09, "z.2E", "-1.00E-09")]        // format(-1e-09, 'z.2E') => '-1.00E-09' (without z: '-1.00E-09')
    [InlineData(-1e-09, "z%", "0.000000%")]          // format(-1e-09, 'z%') => '0.000000%' (without z: '-0.000000%')
    [InlineData(-1e-09, "z.1%", "0.0%")]             // format(-1e-09, 'z.1%') => '0.0%' (without z: '-0.0%')
    [InlineData(-1e-09, "z.0%", "0%")]               // format(-1e-09, 'z.0%') => '0%' (without z: '-0%')
    [InlineData(-1e-09, "z.1", "-1e-09")]            // format(-1e-09, 'z.1') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "z.3", "-1e-09")]            // format(-1e-09, 'z.3') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "zg", "-1e-09")]             // format(-1e-09, 'zg') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "z.1g", "-1e-09")]           // format(-1e-09, 'z.1g') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "zG", "-1E-09")]             // format(-1e-09, 'zG') => '-1E-09' (without z: '-1E-09')
    [InlineData(-1e-09, "z.2G", "-1E-09")]           // format(-1e-09, 'z.2G') => '-1E-09' (without z: '-1E-09')
    [InlineData(-1e-09, "zn", "-1e-09")]             // format(-1e-09, 'zn') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "z.1n", "-1e-09")]           // format(-1e-09, 'z.1n') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "z", "-1e-09")]              // format(-1e-09, 'z') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "+z.0e", "-1e-09")]          // format(-1e-09, '+z.0e') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "z.1f", "0.0")]              // format(-1e-09, 'z.1f') => '0.0' (without z: '-0.0')
    [InlineData(-1e-09, "z,.1f", "0.0")]             // format(-1e-09, 'z,.1f') => '0.0' (without z: '-0.0')
    [InlineData(-1e-09, "z_.3%", "0.000%")]          // format(-1e-09, 'z_.3%') => '0.000%' (without z: '-0.000%')
    [InlineData(-1e-09, "z#.0e", "-1.e-09")]         // format(-1e-09, 'z#.0e') => '-1.e-09' (without z: '-1.e-09')
    [InlineData(-1e-09, "z#.0%", "0.%")]             // format(-1e-09, 'z#.0%') => '0.%' (without z: '-0.%')
    [InlineData(-1e-09, " z.0e", "-1e-09")]          // format(-1e-09, ' z.0e') => '-1e-09' (without z: '-1e-09')
    [InlineData(-1e-09, "z>10.1e", "zz-1.0e-09")]    // format(-1e-09, 'z>10.1e') => 'zz-1.0e-09' (without z: '  -1.0e-09')
    [InlineData(-1e-05, "z.0e", "-1e-05")]           // format(-1e-05, 'z.0e') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "z.1e", "-1.0e-05")]         // format(-1e-05, 'z.1e') => '-1.0e-05' (without z: '-1.0e-05')
    [InlineData(-1e-05, "ze", "-1.000000e-05")]      // format(-1e-05, 'ze') => '-1.000000e-05' (without z: '-1.000000e-05')
    [InlineData(-1e-05, "zE", "-1.000000E-05")]      // format(-1e-05, 'zE') => '-1.000000E-05' (without z: '-1.000000E-05')
    [InlineData(-1e-05, "z.2E", "-1.00E-05")]        // format(-1e-05, 'z.2E') => '-1.00E-05' (without z: '-1.00E-05')
    [InlineData(-1e-05, "z%", "-0.001000%")]         // format(-1e-05, 'z%') => '-0.001000%' (without z: '-0.001000%')
    [InlineData(-1e-05, "z.1%", "0.0%")]             // format(-1e-05, 'z.1%') => '0.0%' (without z: '-0.0%')
    [InlineData(-1e-05, "z.0%", "0%")]               // format(-1e-05, 'z.0%') => '0%' (without z: '-0%')
    [InlineData(-1e-05, "z.1", "-1e-05")]            // format(-1e-05, 'z.1') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "z.3", "-1e-05")]            // format(-1e-05, 'z.3') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "zg", "-1e-05")]             // format(-1e-05, 'zg') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "z.1g", "-1e-05")]           // format(-1e-05, 'z.1g') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "zG", "-1E-05")]             // format(-1e-05, 'zG') => '-1E-05' (without z: '-1E-05')
    [InlineData(-1e-05, "z.2G", "-1E-05")]           // format(-1e-05, 'z.2G') => '-1E-05' (without z: '-1E-05')
    [InlineData(-1e-05, "zn", "-1e-05")]             // format(-1e-05, 'zn') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "z.1n", "-1e-05")]           // format(-1e-05, 'z.1n') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "z", "-1e-05")]              // format(-1e-05, 'z') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "+z.0e", "-1e-05")]          // format(-1e-05, '+z.0e') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "z.1f", "0.0")]              // format(-1e-05, 'z.1f') => '0.0' (without z: '-0.0')
    [InlineData(-1e-05, "z,.1f", "0.0")]             // format(-1e-05, 'z,.1f') => '0.0' (without z: '-0.0')
    [InlineData(-1e-05, "z_.3%", "-0.001%")]         // format(-1e-05, 'z_.3%') => '-0.001%' (without z: '-0.001%')
    [InlineData(-1e-05, "z#.0e", "-1.e-05")]         // format(-1e-05, 'z#.0e') => '-1.e-05' (without z: '-1.e-05')
    [InlineData(-1e-05, "z#.0%", "0.%")]             // format(-1e-05, 'z#.0%') => '0.%' (without z: '-0.%')
    [InlineData(-1e-05, " z.0e", "-1e-05")]          // format(-1e-05, ' z.0e') => '-1e-05' (without z: '-1e-05')
    [InlineData(-1e-05, "z>10.1e", "zz-1.0e-05")]    // format(-1e-05, 'z>10.1e') => 'zz-1.0e-05' (without z: '  -1.0e-05')
    public void Apply_NegativeZeroCoercion_ReadsTheRenderedMantissa(double value, string spec, string expected)
    {
        PyFormat.Apply(value, spec).Should().Be(expected);
    }

    // ---- The ONE python-type-name table, keyed on a CLR Type (the static twin's entry) ----

    public static IEnumerable<object[]> TypeNameCells()
    {
        yield return new object[] { typeof(bool), "bool" };
        yield return new object[] { typeof(double), "float" };
        yield return new object[] { typeof(float), "float" };
        yield return new object[] { typeof(decimal), "decimal" };   // Sharpy's own name: python's Decimal has another grammar (#2014)
        foreach (var t in new[] { typeof(int), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint), typeof(ulong), typeof(ushort) })
        {
            yield return new object[] { t, "int" };
        }
        yield return new object[] { typeof(string), "str" };
        yield return new object[] { typeof(char), "str" };
        yield return new object[] { typeof(System.Numerics.BigInteger), "int" };
        yield return new object[] { typeof(Complex), "complex" };
        yield return new object[] { typeof(Bytes), "bytes" };
        yield return new object[] { typeof((int, string)), "tuple" };
        yield return new object[] { typeof(List<int>), "list" };
        yield return new object[] { typeof(Dict<string, int>), "dict" };
        yield return new object[] { typeof(Set<int>), "set" };
        yield return new object[] { typeof(FrozenSet<int>), "frozenset" };
        yield return new object[] { typeof(Optional<int>), "Optional" };
        yield return new object[] { typeof(C), "C" };
        yield return new object[] { typeof(Outer.Inner<int>), "Inner" };
        yield return new object[] { typeof(ValueError), "ValueError" };
    }

    [Theory]
    [MemberData(nameof(TypeNameCells))]
    public void PyTypeName_OfType_IsThePythonName(System.Type type, string expected)
    {
        PyFormat.PyTypeName(type).Should().Be(expected);
    }

    [Fact]
    public void PyTypeName_OfValue_IsTheTypeTable()
    {
        // The runtime engine names a value by the same Type-keyed table the static twin uses.
        var ex = Assert.Throws<TypeError>(() => PyFormat.Apply((1, 2), ">3"));
        ex.Message.Should().Be("unsupported format string passed to " + PyFormat.PyTypeName(typeof((int, int))) + ".__format__");
        Assert.Throws<ValueError>(() => PyFormat.Apply('a', "d")).Message
            .Should().Be("Unknown format code 'd' for object of type '" + PyFormat.PyTypeName(typeof(char)) + "'");
    }

    // ---- #1988 regression (plan-bf0244 verify): every CLR integer is a python int ----------------

    /// <summary>
    /// Every CLR integer python would call <c>int</c> formats with <c>int.__format__</c> at any
    /// magnitude (#1988 regression R1: a <see cref="System.Numerics.BigInteger"/> fell to the
    /// IFormattable arm, so <c>format(big, '>6')</c> was <c>'>6'</c>; and the renderer narrowed to
    /// <c>long</c>, so a value beyond it threw a raw OverflowException). The value column is the
    /// decimal digits of a BigInteger; each row is CPython 3.12's rendering, quoted.
    /// </summary>
    public static IEnumerable<object[]> BigIntegerCells()
    {
        // python3 -c "print(repr(format(1, '>6')))" => '     1'
        yield return new object[] { "1", ">6", "     1" };
        // python3 -c "print(repr(format(1, 'd')))" => '1'
        yield return new object[] { "1", "d", "1" };
        // python3 -c "print(repr(format(1, ',')))" => '1'
        yield return new object[] { "1", ",", "1" };
        // python3 -c "print(repr(format(1, '_x')))" => '1'
        yield return new object[] { "1", "_x", "1" };
        // python3 -c "print(repr(format(1, '#b')))" => '0b1'
        yield return new object[] { "1", "#b", "0b1" };
        // python3 -c "print(repr(format(1, '+040')))" => '+000000000000000000000000000000000000001'
        yield return new object[] { "1", "+040", "+000000000000000000000000000000000000001" };
        // python3 -c "print(repr(format(1, '=^20')))" => '=========1=========='
        yield return new object[] { "1", "=^20", "=========1==========" };
        // python3 -c "print(repr(format(1, 'e')))" => '1.000000e+00'
        yield return new object[] { "1", "e", "1.000000e+00" };
        // python3 -c "print(repr(format(1, '.2%')))" => '100.00%'
        yield return new object[] { "1", ".2%", "100.00%" };
        // python3 -c "print(repr(format(10**30, '>6')))" => '1000000000000000000000000000000'
        yield return new object[] { "1000000000000000000000000000000", ">6", "1000000000000000000000000000000" };
        // python3 -c "print(repr(format(10**30, 'd')))" => '1000000000000000000000000000000'
        yield return new object[] { "1000000000000000000000000000000", "d", "1000000000000000000000000000000" };
        // python3 -c "print(repr(format(10**30, ',')))" => '1,000,000,000,000,000,000,000,000,000,000'
        yield return new object[] { "1000000000000000000000000000000", ",", "1,000,000,000,000,000,000,000,000,000,000" };
        // python3 -c "print(repr(format(10**30, '_x')))" => 'c_9f2c_9cd0_4674_edea_4000_0000'
        yield return new object[] { "1000000000000000000000000000000", "_x", "c_9f2c_9cd0_4674_edea_4000_0000" };
        // python3 -c "print(repr(format(10**30, '#b')))" => '0b1100100111110010110010011100110100000100011001110100111011011110101001000000000000000000000000000000'
        yield return new object[] { "1000000000000000000000000000000", "#b", "0b1100100111110010110010011100110100000100011001110100111011011110101001000000000000000000000000000000" };
        // python3 -c "print(repr(format(10**30, '+040')))" => '+000000001000000000000000000000000000000'
        yield return new object[] { "1000000000000000000000000000000", "+040", "+000000001000000000000000000000000000000" };
        // python3 -c "print(repr(format(10**30, '=^20')))" => '1000000000000000000000000000000'
        yield return new object[] { "1000000000000000000000000000000", "=^20", "1000000000000000000000000000000" };
        // python3 -c "print(repr(format(10**30, 'e')))" => '1.000000e+30'
        yield return new object[] { "1000000000000000000000000000000", "e", "1.000000e+30" };
        // python3 -c "print(repr(format(10**30, '.2%')))" => '100000000000000005366162204393472.00%'
        yield return new object[] { "1000000000000000000000000000000", ".2%", "100000000000000005366162204393472.00%" };
        // python3 -c "print(repr(format(-(10**30), '>6')))" => '-1000000000000000000000000000000'
        yield return new object[] { "-1000000000000000000000000000000", ">6", "-1000000000000000000000000000000" };
        // python3 -c "print(repr(format(-(10**30), 'd')))" => '-1000000000000000000000000000000'
        yield return new object[] { "-1000000000000000000000000000000", "d", "-1000000000000000000000000000000" };
        // python3 -c "print(repr(format(-(10**30), ',')))" => '-1,000,000,000,000,000,000,000,000,000,000'
        yield return new object[] { "-1000000000000000000000000000000", ",", "-1,000,000,000,000,000,000,000,000,000,000" };
        // python3 -c "print(repr(format(-(10**30), '_x')))" => '-c_9f2c_9cd0_4674_edea_4000_0000'
        yield return new object[] { "-1000000000000000000000000000000", "_x", "-c_9f2c_9cd0_4674_edea_4000_0000" };
        // python3 -c "print(repr(format(-(10**30), '#b')))" => '-0b1100100111110010110010011100110100000100011001110100111011011110101001000000000000000000000000000000'
        yield return new object[] { "-1000000000000000000000000000000", "#b", "-0b1100100111110010110010011100110100000100011001110100111011011110101001000000000000000000000000000000" };
        // python3 -c "print(repr(format(-(10**30), '+040')))" => '-000000001000000000000000000000000000000'
        yield return new object[] { "-1000000000000000000000000000000", "+040", "-000000001000000000000000000000000000000" };
        // python3 -c "print(repr(format(-(10**30), '=^20')))" => '-1000000000000000000000000000000'
        yield return new object[] { "-1000000000000000000000000000000", "=^20", "-1000000000000000000000000000000" };
        // python3 -c "print(repr(format(-(10**30), 'e')))" => '-1.000000e+30'
        yield return new object[] { "-1000000000000000000000000000000", "e", "-1.000000e+30" };
        // python3 -c "print(repr(format(-(10**30), '.2%')))" => '-100000000000000005366162204393472.00%'
        yield return new object[] { "-1000000000000000000000000000000", ".2%", "-100000000000000005366162204393472.00%" };
        // python3 -c "print(repr(format(2**64, '>6')))" => '18446744073709551616'
        yield return new object[] { "18446744073709551616", ">6", "18446744073709551616" };
        // python3 -c "print(repr(format(2**64, 'd')))" => '18446744073709551616'
        yield return new object[] { "18446744073709551616", "d", "18446744073709551616" };
        // python3 -c "print(repr(format(2**64, ',')))" => '18,446,744,073,709,551,616'
        yield return new object[] { "18446744073709551616", ",", "18,446,744,073,709,551,616" };
        // python3 -c "print(repr(format(2**64, '_x')))" => '1_0000_0000_0000_0000'
        yield return new object[] { "18446744073709551616", "_x", "1_0000_0000_0000_0000" };
        // python3 -c "print(repr(format(2**64, '#b')))" => '0b10000000000000000000000000000000000000000000000000000000000000000'
        yield return new object[] { "18446744073709551616", "#b", "0b10000000000000000000000000000000000000000000000000000000000000000" };
        // python3 -c "print(repr(format(2**64, '+040')))" => '+000000000000000000018446744073709551616'
        yield return new object[] { "18446744073709551616", "+040", "+000000000000000000018446744073709551616" };
        // python3 -c "print(repr(format(2**64, '=^20')))" => '18446744073709551616'
        yield return new object[] { "18446744073709551616", "=^20", "18446744073709551616" };
        // python3 -c "print(repr(format(2**64, 'e')))" => '1.844674e+19'
        yield return new object[] { "18446744073709551616", "e", "1.844674e+19" };
        // python3 -c "print(repr(format(2**64, '.2%')))" => '1844674407370955161600.00%'
        yield return new object[] { "18446744073709551616", ".2%", "1844674407370955161600.00%" };
        // python3 -c "print(repr(format(-(2**63), '>6')))" => '-9223372036854775808'
        yield return new object[] { "-9223372036854775808", ">6", "-9223372036854775808" };
        // python3 -c "print(repr(format(-(2**63), 'd')))" => '-9223372036854775808'
        yield return new object[] { "-9223372036854775808", "d", "-9223372036854775808" };
        // python3 -c "print(repr(format(-(2**63), ',')))" => '-9,223,372,036,854,775,808'
        yield return new object[] { "-9223372036854775808", ",", "-9,223,372,036,854,775,808" };
        // python3 -c "print(repr(format(-(2**63), '_x')))" => '-8000_0000_0000_0000'
        yield return new object[] { "-9223372036854775808", "_x", "-8000_0000_0000_0000" };
        // python3 -c "print(repr(format(-(2**63), '#b')))" => '-0b1000000000000000000000000000000000000000000000000000000000000000'
        yield return new object[] { "-9223372036854775808", "#b", "-0b1000000000000000000000000000000000000000000000000000000000000000" };
        // python3 -c "print(repr(format(-(2**63), '+040')))" => '-000000000000000000009223372036854775808'
        yield return new object[] { "-9223372036854775808", "+040", "-000000000000000000009223372036854775808" };
        // python3 -c "print(repr(format(-(2**63), '=^20')))" => '-9223372036854775808'
        yield return new object[] { "-9223372036854775808", "=^20", "-9223372036854775808" };
        // python3 -c "print(repr(format(-(2**63), 'e')))" => '-9.223372e+18'
        yield return new object[] { "-9223372036854775808", "e", "-9.223372e+18" };
        // python3 -c "print(repr(format(-(2**63), '.2%')))" => '-922337203685477580800.00%'
        yield return new object[] { "-9223372036854775808", ".2%", "-922337203685477580800.00%" };
        // python3 -c "print(repr(format(65, 'c')))" => 'A'
        yield return new object[] { "65", "c", "A" };
        // python3 -c "print(repr(format(2**64, 'X')))" => '10000000000000000'
        yield return new object[] { "18446744073709551616", "X", "10000000000000000" };
        // python3 -c "print(repr(format(-(2**64), '#o')))" => '-0o2000000000000000000000'
        yield return new object[] { "-18446744073709551616", "#o", "-0o2000000000000000000000" };
        // python3 -c "print(repr(format(10**30, 'f')))" => '1000000000000000019884624838656.000000'
        yield return new object[] { "1000000000000000000000000000000", "f", "1000000000000000019884624838656.000000" };
        // python3 -c "print(repr(format(10**30, 'g')))" => '1e+30'
        yield return new object[] { "1000000000000000000000000000000", "g", "1e+30" };
        // python3 -c "print(repr(format(10**30, 'n')))" => '1000000000000000000000000000000'
        yield return new object[] { "1000000000000000000000000000000", "n", "1000000000000000000000000000000" };
        // python3 -c "print(repr(format(-(10**30), '*=+45,')))" => '-***1,000,000,000,000,000,000,000,000,000,000'
        yield return new object[] { "-1000000000000000000000000000000", "*=+45,", "-***1,000,000,000,000,000,000,000,000,000,000" };
    }

    [Theory]
    [MemberData(nameof(BigIntegerCells))]
    public void Apply_BigInteger_IsAPythonInt(string digits, string spec, string expected)
    {
        var value = System.Numerics.BigInteger.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
        PyFormat.Apply(value, spec).Should().Be(expected);
    }

    /// <summary>
    /// <c>ulong.MaxValue</c> as a <c>ulong</c> — a pre-existing cell of the same class: at d17ddb956
    /// and d1190a568 <c>format(ulong.MaxValue, 'd')</c> threw a raw OverflowException from
    /// <c>Convert.ToInt64</c>. <c>long.MinValue</c> as a <c>long</c> is the long path's edge.
    /// </summary>
    public static IEnumerable<object[]> ULongMaxCells()
    {
        // python3 -c "print(repr(format(2**64-1, '>6')))" => '18446744073709551615'
        yield return new object[] { ">6", "18446744073709551615" };
        // python3 -c "print(repr(format(2**64-1, 'd')))" => '18446744073709551615'
        yield return new object[] { "d", "18446744073709551615" };
        // python3 -c "print(repr(format(2**64-1, ',')))" => '18,446,744,073,709,551,615'
        yield return new object[] { ",", "18,446,744,073,709,551,615" };
        // python3 -c "print(repr(format(2**64-1, '_x')))" => 'ffff_ffff_ffff_ffff'
        yield return new object[] { "_x", "ffff_ffff_ffff_ffff" };
        // python3 -c "print(repr(format(2**64-1, '#b')))" => '0b1111111111111111111111111111111111111111111111111111111111111111'
        yield return new object[] { "#b", "0b1111111111111111111111111111111111111111111111111111111111111111" };
        // python3 -c "print(repr(format(2**64-1, '+040')))" => '+000000000000000000018446744073709551615'
        yield return new object[] { "+040", "+000000000000000000018446744073709551615" };
        // python3 -c "print(repr(format(2**64-1, '=^20')))" => '18446744073709551615'
        yield return new object[] { "=^20", "18446744073709551615" };
        // python3 -c "print(repr(format(2**64-1, 'e')))" => '1.844674e+19'
        yield return new object[] { "e", "1.844674e+19" };
        // python3 -c "print(repr(format(2**64-1, '.2%')))" => '1844674407370955161600.00%'
        yield return new object[] { ".2%", "1844674407370955161600.00%" };
    }

    [Theory]
    [MemberData(nameof(ULongMaxCells))]
    public void Apply_ULongMax_IsAPythonInt(string spec, string expected)
    {
        PyFormat.Apply(ulong.MaxValue, spec).Should().Be(expected);
    }

    [Fact]
    public void Apply_LongMin_IsAPythonInt()
    {
        // python3 -c "print(repr(format(-2**63, '_x')))" => '-8000_0000_0000_0000'
        PyFormat.Apply(long.MinValue, "_x").Should().Be("-8000_0000_0000_0000");
        // python3 -c "print(repr(format(-2**63, ',')))" => '-9,223,372,036,854,775,808'
        PyFormat.Apply(long.MinValue, ",").Should().Be("-9,223,372,036,854,775,808");
    }

    [Fact]
    public void Apply_IntBeyondItsConversion_RaisesCPythonsOverflowError()
    {
        var huge = System.Numerics.BigInteger.Pow(10, 400);
        // python3 -c "format(10**400, 'e')" => OverflowError: int too large to convert to float
        Assert.Throws<OverflowError>(() => PyFormat.Apply(huge, "e")).Message
            .Should().Be("int too large to convert to float");
        // python3 -c "format(10**400, '%')" => OverflowError: int too large to convert to float
        Assert.Throws<OverflowError>(() => PyFormat.Apply(huge, "%")).Message
            .Should().Be("int too large to convert to float");
        // python3 -c "format(2**70, 'c')" => OverflowError: Python int too large to convert to C long
        Assert.Throws<OverflowError>(() => PyFormat.Apply(System.Numerics.BigInteger.Pow(2, 70), "c")).Message
            .Should().Be("Python int too large to convert to C long");
        // python3 -c "format(-1, 'c')" => OverflowError: %c arg not in range(0x110000)
        Assert.Throws<OverflowError>(() => PyFormat.Apply(new System.Numerics.BigInteger(-1), "c")).Message
            .Should().Be("%c arg not in range(0x110000)");
    }

    [Fact]
    public void Apply_BigInteger_RefusalNamesInt()
    {
        // python3 -c "format(10**30, 's')" => ValueError: Unknown format code 's' for object of type 'int'
        Assert.Throws<ValueError>(() => PyFormat.Apply(System.Numerics.BigInteger.Pow(10, 30), "s")).Message
            .Should().Be("Unknown format code 's' for object of type 'int'");
    }

#if NET10_0_OR_GREATER
    [Fact]
    public void Apply_Int128_UInt128_NInt_ArePythonInts_Half_IsAPythonFloat()
    {
        // python3 -c "print(repr(format(2**127-1, ',')))" => '170,141,183,460,469,231,731,687,303,715,884,105,727'
        PyFormat.Apply(System.Int128.MaxValue, ",").Should().Be("170,141,183,460,469,231,731,687,303,715,884,105,727");
        // python3 -c "print(repr(format(-(2**127), 'x')))" => '-80000000000000000000000000000000'
        PyFormat.Apply(System.Int128.MinValue, "x").Should().Be("-80000000000000000000000000000000");
        // python3 -c "print(repr(format(2**128-1, '#X')))" => '0XFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF'
        PyFormat.Apply(System.UInt128.MaxValue, "#X").Should().Be("0XFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");
        // python3 -c "print(repr(format(-5, '+d')))" => '-5'
        PyFormat.Apply((nint)(-5), "+d").Should().Be("-5");
        // python3 -c "print(repr(format(2**64-1, ',')))" => '18,446,744,073,709,551,615'
        PyFormat.Apply(nuint.MaxValue, ",").Should().Be("18,446,744,073,709,551,615");
        // python3 -c "print(repr(format(1.5, '.2f')))" => '1.50'
        PyFormat.Apply((System.Half)1.5, ".2f").Should().Be("1.50");
        // python3 -c "print(repr(format(1.5, '>8')))" => '     1.5'
        PyFormat.Apply((System.Half)1.5, ">8").Should().Be("     1.5");
        // python3 -c "print(repr(format(1.5, 'e')))" => '1.500000e+00'
        PyFormat.Apply((System.Half)1.5, "e").Should().Be("1.500000e+00");
        // python3 -c "print(repr(format(float('inf'), 'f')))" => 'inf'
        PyFormat.Apply(System.Half.PositiveInfinity, "f").Should().Be("inf");
        PyFormat.KindOf(typeof(System.Int128)).Should().Be(FormatOperandKind.Integral);
        PyFormat.KindOf(typeof(System.UInt128)).Should().Be(FormatOperandKind.Integral);
        PyFormat.KindOf(typeof(nint)).Should().Be(FormatOperandKind.Integral);
        PyFormat.KindOf(typeof(nuint)).Should().Be(FormatOperandKind.Integral);
        PyFormat.KindOf(typeof(System.Half)).Should().Be(FormatOperandKind.Float);
    }
#endif

    // ---- #1988 regression R2: a spec an IFormattable type rejects is a python ValueError ---------

    [Fact]
    public void Apply_FormattableRejectingItsSpec_RaisesValueError()
    {
        // Sharpy-only message: python has no Guid/TimeSpan; the wording is CPython's for a rejected
        // spec (python3 -c "format(1, 'abc')" => ValueError: Invalid format specifier 'abc' for object of type 'int').
        // At d1190a568 both threw a raw System.FormatException, which `except ValueError` cannot catch.
        var guid = Assert.Throws<ValueError>(() => PyFormat.Apply(System.Guid.Empty, ">40"));
        guid.Message.Should().Be("Invalid format specifier '>40' for object of type 'Guid'");
        guid.InnerException.Should().BeOfType<System.FormatException>();
        Assert.Throws<ValueError>(() => PyFormat.Apply(System.TimeSpan.FromSeconds(5.0), ">10")).Message
            .Should().Be("Invalid format specifier '>10' for object of type 'TimeSpan'");
    }

    [Fact]
    public void Apply_FormattableAcceptingItsSpec_StillOwnsIt()
    {
        // The positive control of the Formattable arm (R-BY): the type owns its spec, so DateTime's
        // own "yyyy" renders the year — python's datetime(2020,1,1).__format__('%Y') is '2020'.
        PyFormat.Apply(new System.DateTime(2020, 1, 1), "yyyy").Should().Be("2020");
    }

    /// <summary>An IFormattable whose ToString throws something other than FormatException.</summary>
    private sealed class Throws : System.IFormattable
    {
        public string ToString(string? format, System.IFormatProvider? formatProvider) =>
            throw new System.InvalidOperationException("mine");
    }

    [Fact]
    public void Apply_FormattableThrowingAnythingElse_IsNotTranslated()
    {
        // Only FormatException is a rejected spec; the type's own errors propagate unchanged.
        Assert.Throws<System.InvalidOperationException>(() => PyFormat.Apply(new Throws(), ">3")).Message
            .Should().Be("mine");
    }

    [Fact]
    public void KindOf_Value_IsKindOfItsType()
    {
        PyFormat.KindOf((object?)null).Should().Be(FormatOperandKind.NoneValue);
        foreach (object v in new object[] { 1, 1L, (byte)1, ulong.MaxValue, System.Numerics.BigInteger.One, 1.5, 'a', "s", true,
            System.Guid.Empty, System.DayOfWeek.Monday, new List<int>() })
        {
            PyFormat.KindOf(v).Should().Be(PyFormat.KindOf(v.GetType()), v.GetType().Name);
        }
        PyFormat.KindOf(typeof(System.Numerics.BigInteger)).Should().Be(FormatOperandKind.Integral);
        PyFormat.KindOf(typeof(System.Guid)).Should().Be(FormatOperandKind.Formattable);
        PyFormat.KindOf(typeof(System.DayOfWeek)).Should().Be(FormatOperandKind.Str);
        PyFormat.KindOf(typeof(System.Enum)).Should().Be(FormatOperandKind.Str);
        PyFormat.KindOf(typeof(List<int>)).Should().Be(FormatOperandKind.NoFormat);
    }
}
