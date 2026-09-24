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
}
