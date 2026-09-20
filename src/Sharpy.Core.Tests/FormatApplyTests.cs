using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Value-kind x spec-component table for the one Python format-spec engine,
/// <see cref="PyFormat.Apply(object, string)"/>. Every expected value is byte-for-byte what
/// CPython 3.12 prints; regenerate the table with:
/// <code>
/// python3 -c "print(repr(format(VALUE, 'SPEC')))"
/// </code>
/// The refusal cells (None+spec, str+numeric-type, unknown code, int+precision, trailing garbage)
/// mirror the exact ValueError/TypeError wording CPython emits — that wording names the operand type.
/// </summary>
public class FormatApplyTests
{
    [Theory]
    // --- int: fill/align/width/sign/zero ---
    [InlineData(42, "", "42")]                 // format(42, '')
    [InlineData(42, "5", "   42")]             // format(42, '5')
    [InlineData(42, "<5", "42   ")]            // format(42, '<5')
    [InlineData(42, ">5", "   42")]            // format(42, '>5')
    [InlineData(42, "^5", " 42  ")]            // format(42, '^5')
    [InlineData(42, "*>5", "***42")]           // format(42, '*>5')
    [InlineData(42, "05", "00042")]            // format(42, '05')
    [InlineData(-42, "05", "-0042")]           // format(-42, '05')
    [InlineData(42, "+", "+42")]               // format(42, '+')
    [InlineData(42, " ", " 42")]               // format(42, ' ')
    [InlineData(-42, "+", "-42")]              // format(-42, '+')
    // --- int: grouping ---
    [InlineData(42, ",d", "42")]               // format(42, ',d')
    [InlineData(1234567, ",", "1,234,567")]    // format(1234567, ',')
    [InlineData(1234567, "_", "1_234_567")]    // format(1234567, '_')
    // --- int: radix types + alt form ---
    [InlineData(255, "x", "ff")]               // format(255, 'x')
    [InlineData(255, "X", "FF")]               // format(255, 'X')
    [InlineData(255, "#x", "0xff")]            // format(255, '#x')
    [InlineData(255, "#X", "0XFF")]            // format(255, '#X')
    [InlineData(8, "o", "10")]                 // format(8, 'o')
    [InlineData(8, "#o", "0o10")]              // format(8, '#o')
    [InlineData(5, "b", "101")]                // format(5, 'b')
    [InlineData(5, "#b", "0b101")]             // format(5, '#b')
    [InlineData(65, "c", "A")]                 // format(65, 'c')
    // --- int: n / float-codes on an int ---
    [InlineData(42, "n", "42")]                // format(42, 'n')
    [InlineData(1000000, "n", "1000000")]      // format(1000000, 'n')
    [InlineData(42, "f", "42.000000")]         // format(42, 'f')
    [InlineData(42, ".2f", "42.00")]           // format(42, '.2f')
    [InlineData(42, "e", "4.200000e+01")]      // format(42, 'e')
    [InlineData(42, "g", "42")]                // format(42, 'g')
    [InlineData(42, "%", "4200.000000%")]      // format(42, '%')
    // --- bool: empty spec => True/False; non-empty spec => int ---
    [InlineData(true, "", "True")]             // format(True, '')
    [InlineData(false, "", "False")]           // format(False, '')
    [InlineData(true, "d", "1")]               // format(True, 'd')
    [InlineData(true, "5", "    1")]            // format(True, '5')
    [InlineData(false, "d", "0")]              // format(False, 'd')
    [InlineData(true, "x", "1")]               // format(True, 'x')
    // --- float: types ---
    [InlineData(3.14159, "", "3.14159")]       // format(3.14159, '')
    [InlineData(3.14159, ".2f", "3.14")]       // format(3.14159, '.2f')
    [InlineData(3.14159, "f", "3.141590")]     // format(3.14159, 'f')
    [InlineData(3.14159, "e", "3.141590e+00")] // format(3.14159, 'e')
    [InlineData(3.14159, "E", "3.141590E+00")] // format(3.14159, 'E')
    [InlineData(3.14159, ".3e", "3.142e+00")]  // format(3.14159, '.3e')
    [InlineData(3.14159, "g", "3.14159")]      // format(3.14159, 'g')
    [InlineData(3.14159, "G", "3.14159")]      // format(3.14159, 'G')
    [InlineData(3.14159, ".3g", "3.14")]       // format(3.14159, '.3g')
    // --- float: significant-digit precision with NO type (distinct from g and from .Nf) ---
    [InlineData(3.14159, ".3", "3.14")]        // format(3.14159, '.3')
    [InlineData(1.0, ".3", "1.0")]             // format(1.0, '.3')
    [InlineData(0.0, ".3", "0.0")]             // format(0.0, '.3')
    [InlineData(100.0, ".3", "1e+02")]         // format(100.0, '.3')
    [InlineData(1234.0, ".3", "1.23e+03")]     // format(1234.0, '.3')
    // --- float: g exponent switch + trailing-zero strip ---
    [InlineData(1234.5, "g", "1234.5")]        // format(1234.5, 'g')
    [InlineData(1234567.0, "g", "1.23457e+06")] // format(1234567.0, 'g')
    [InlineData(1234567.0, "G", "1.23457E+06")] // format(1234567.0, 'G')
    [InlineData(100000.0, "g", "100000")]      // format(100000.0, 'g')
    [InlineData(1000000.0, "g", "1e+06")]      // format(1000000.0, 'g')
    [InlineData(0.0001, "g", "0.0001")]        // format(0.0001, 'g')
    [InlineData(0.00001, "g", "1e-05")]        // format(0.00001, 'g')
    // --- float: percent ---
    [InlineData(0.5, "%", "50.000000%")]       // format(0.5, '%')
    [InlineData(0.1234, ".1%", "12.3%")]       // format(0.1234, '.1%')
    // --- float: combined width/align/precision/grouping ---
    [InlineData(3.14159, ">8.2f", "    3.14")] // format(3.14159, '>8.2f')
    [InlineData(3.14159, "07.2f", "0003.14")]  // format(3.14159, '07.2f')
    [InlineData(3.14159, "+.2f", "+3.14")]     // format(3.14159, '+.2f')
    [InlineData(1234.5678, "12,.2f", "    1,234.57")] // format(1234.5678, '12,.2f')
    // --- float: z flag (PEP 682 negative-zero coercion) ---
    [InlineData(-0.0, "z.2f", "0.00")]         // format(-0.0, 'z.2f')
    [InlineData(-0.001, "z.1f", "0.0")]        // format(-0.001, 'z.1f')
    [InlineData(-0.0, "+z.2f", "+0.00")]       // format(-0.0, '+z.2f')
    // --- str ---
    [InlineData("hello", "", "hello")]         // format('hello', '')
    [InlineData("hello", "s", "hello")]        // format('hello', 's')
    [InlineData("hello", ".3", "hel")]         // format('hello', '.3')
    [InlineData("hi", ">5", "   hi")]          // format('hi', '>5')
    [InlineData("hi", "*^6", "**hi**")]        // format('hi', '*^6')
    public void Apply_MatchesCPython(object value, string spec, string expected)
    {
        PyFormat.Apply(value, spec).Should().Be(expected);
    }

    /// <summary>
    /// #1883: an empty spec is <c>str(value)</c>, and <c>str</c> means <c>Builtins.Str</c> — the one
    /// authority an f-string's plain hole already uses — not <see cref="object.ToString"/>. The two
    /// disagree for exactly the kinds Python spells specially: a whole float (<c>100.0</c> vs
    /// <c>100</c>), an infinity, a NaN, and a bool. A float with a NON-empty spec but no type code
    /// takes the same route, which is why <c>">10"</c> is here too.
    /// </summary>
    [Theory]
    [InlineData(100.0, "", "100.0")]           // format(100.0, '')  — the #1883 close criterion
    [InlineData(1.0, "", "1.0")]               // format(1.0, '')
    [InlineData(-0.0, "", "-0.0")]             // format(-0.0, '')
    [InlineData(1e20, "", "1e+20")]            // format(1e20, '')
    [InlineData(double.PositiveInfinity, "", "inf")]   // format(float('inf'), '')
    [InlineData(double.NegativeInfinity, "", "-inf")]  // format(float('-inf'), '')
    [InlineData(double.NaN, "", "nan")]        // format(float('nan'), '')
    [InlineData(100.0, ">10", "     100.0")]   // format(100.0, '>10')
    [InlineData(100.0, "<8", "100.0   ")]      // format(100.0, '<8')
    [InlineData(1e20, ">12", "       1e+20")]  // format(1e20, '>12')
    [InlineData(-0.0, ">8", "    -0.0")]       // format(-0.0, '>8')
    public void Apply_EmptySpec_IsStrOfValue(object value, string spec, string expected)
    {
        PyFormat.Apply(value, spec).Should().Be(expected);
    }

    // ---- Refusal cells (each verified against python3 3.12) ----

    [Fact]
    public void Apply_None_EmptySpec_IsNone()
    {
        // python3 -c "print(repr(format(None, '')))"  =>  'None'
        PyFormat.Apply(null, "").Should().Be("None");
    }

    [Fact]
    public void Apply_None_NonEmptySpec_RaisesTypeError()
    {
        // python3 -c "format(None, 'd')"  =>  TypeError: unsupported format string passed to NoneType.__format__
        var ex = Assert.Throws<TypeError>(() => PyFormat.Apply(null, "d"));
        ex.Message.Should().Be("unsupported format string passed to NoneType.__format__");
    }

    [Fact]
    public void Apply_Str_NumericType_RaisesValueError_NamingStr()
    {
        // python3 -c "format('a', 'd')"  =>  ValueError: Unknown format code 'd' for object of type 'str'
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply("a", "d"));
        ex.Message.Should().Be("Unknown format code 'd' for object of type 'str'");
    }

    [Fact]
    public void Apply_Str_NumericType_NeverConvertsSilently()
    {
        // python3 -c "format('42', 'd')"  =>  ValueError (NOT a silent '42' and NOT a raw FormatException)
        Assert.Throws<ValueError>(() => PyFormat.Apply("42", "d"));
    }

    [Fact]
    public void Apply_Int_UnknownCode_RaisesValueError_NamingInt()
    {
        // python3 -c "format(5, 'q')"  =>  ValueError: Unknown format code 'q' for object of type 'int'
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(5, "q"));
        ex.Message.Should().Be("Unknown format code 'q' for object of type 'int'");
    }

    [Fact]
    public void Apply_Float_IntegerCode_RaisesValueError_NamingFloat()
    {
        // python3 -c "format(3.0, 'd')"  =>  ValueError: Unknown format code 'd' for object of type 'float'
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(3.0, "d"));
        ex.Message.Should().Be("Unknown format code 'd' for object of type 'float'");
    }

    [Fact]
    public void Apply_Int_StringCode_RaisesValueError()
    {
        // python3 -c "format(5, 's')"  =>  ValueError: Unknown format code 's' for object of type 'int'
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(5, "s"));
        ex.Message.Should().Be("Unknown format code 's' for object of type 'int'");
    }

    [Fact]
    public void Apply_Int_Precision_RaisesValueError()
    {
        // python3 -c "format(42, '.2')"  =>  ValueError: Precision not allowed in integer format specifier
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(42, ".2"));
        ex.Message.Should().Be("Precision not allowed in integer format specifier");
    }

    [Fact]
    public void Apply_TrailingGarbage_RaisesValueError()
    {
        // python3 -c "format(3.14, '.2fx')"  =>  ValueError: Invalid format specifier '.2fx' for object of type 'float'
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(3.14, ".2fx"));
        ex.Message.Should().Be("Invalid format specifier '.2fx' for object of type 'float'");
    }
}
