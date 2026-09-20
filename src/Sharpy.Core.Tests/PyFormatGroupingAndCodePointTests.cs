using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// CPython conformance for the three rules <see cref="PyFormat"/> got wrong (#1883 and its two
/// siblings), pinned cell by cell against python3 3.12:
///
/// <list type="number">
/// <item><b>Grouping width.</b> <c>_</c> groups every FOUR digits under the integer presentation
/// types <c>b</c>/<c>o</c>/<c>x</c>/<c>X</c> and every three everywhere else (PEP 515). The engine
/// grouped every three unconditionally, so <c>format(1234567, '_b')</c> was
/// <c>100_101_101_011_010_000_111</c> instead of <c>1_0010_1101_0110_1000_0111</c>.</item>
/// <item><b>Grouping placement.</b> Separators go into the DIGIT RUN, not the <c>0x</c> prefix
/// (<c>format(1234567, '#_x')</c> was <c>0x_12d_687</c>), and they interleave with the <c>0</c> fill
/// rather than sitting after it (<c>format(74565, '#012_x')</c> was <c>000x1_2345</c>-shaped).</item>
/// <item><b>Code points.</b> <c>c</c> outside <c>range(0x110000)</c> is Python's
/// <see cref="OverflowError"/>, not a raw .NET <see cref="System.ArgumentOutOfRangeException"/> that
/// aborted the process.</item>
/// </list>
///
/// <para>Regenerate any row with <c>python3 -c "print(repr(format(VALUE, 'SPEC')))"</c>. Radix
/// rendering is sign-magnitude for the same reason: Python integers are unbounded, so
/// <c>format(-255, 'x')</c> is <c>-ff</c>, and a two's-complement digit run has no correct
/// grouping.</para>
/// </summary>
public class PyFormatGroupingAndCodePointTests
{
    [Theory]
    // --- '_' groups every FOUR digits under b/o/x/X (PEP 515) ---
    [InlineData(1234567, "_b", "1_0010_1101_0110_1000_0111")]
    [InlineData(1234567, "_o", "455_3207")]
    [InlineData(1234567, "_x", "12_d687")]
    [InlineData(1234567, "_X", "12_D687")]
    [InlineData(255, "_b", "1111_1111")]
    [InlineData(74565, "_x", "1_2345")]
    [InlineData(65535, "_x", "ffff")]
    [InlineData(4660, "_x", "1234")]
    [InlineData(255, "_x", "ff")]
    [InlineData(0, "_x", "0")]
    [InlineData(0, "_b", "0")]
    // --- the separators go after the 0x/0b/0o prefix, not through it ---
    [InlineData(1234567, "#_x", "0x12_d687")]
    [InlineData(1234567, "#_b", "0b1_0010_1101_0110_1000_0111")]
    [InlineData(1234567, "#_o", "0o455_3207")]
    // --- agreeing controls: the decimal presentations still group every three ---
    [InlineData(1234567, "_d", "1_234_567")]
    [InlineData(1234567, "_", "1_234_567")]
    [InlineData(1234567, ",d", "1,234,567")]
    [InlineData(1234567, ",", "1,234,567")]
    // --- negatives are sign-magnitude, not two's complement ---
    [InlineData(-255, "x", "-ff")]
    [InlineData(-255, "X", "-FF")]
    [InlineData(-255, "o", "-377")]
    [InlineData(-255, "b", "-11111111")]
    [InlineData(-255, "#x", "-0xff")]
    [InlineData(-1234567, "_x", "-12_d687")]
    [InlineData(-255, "_b", "-1111_1111")]
    [InlineData(-1234567, "#_x", "-0x12_d687")]
    // --- the '0' fill and the separators interleave (CPython InsertThousandsGrouping) ---
    [InlineData(1234567, "012_b", "1_0010_1101_0110_1000_0111")]
    [InlineData(74565, "#012_x", "0x0_0001_2345")]
    [InlineData(255, "020_b", "0_0000_0000_1111_1111")]
    [InlineData(1234, "012_d", "0_000_001_234")]
    [InlineData(1234, "012,d", "0,000,001,234")]
    [InlineData(-1234, "012,d", "-000,001,234")]
    [InlineData(1234, "0=12_x", "00_0000_04d2")]
    [InlineData(74565, "09_x", "0001_2345")]
    [InlineData(74565, "#09_x", "0x01_2345")]
    [InlineData(74565, "010_x", "0_0001_2345")]
    [InlineData(1234, "08,d", "0,001,234")]
    [InlineData(1234, "07,d", "001,234")]
    [InlineData(1234, "06_d", "01_234")]
    [InlineData(1234, "05_d", "1_234")]
    // --- an explicit non-'0' fill is NOT grouping-aware ---
    [InlineData(1234, "*=12_d", "*******1_234")]
    [InlineData(1234, "=12_d", "       1_234")]
    // --- c ---
    [InlineData(65, "c", "A")]
    [InlineData(97, "c", "a")]
    public void Apply_Int_MatchesCPython(int value, string spec, string expected)
    {
        PyFormat.Apply(value, spec).Should().Be(expected);
    }

    [Theory]
    [InlineData(1234.5, "020,", "00,000,000,001,234.5")]
    [InlineData(1234.5, "020_", "00_000_000_001_234.5")]
    [InlineData(1234.5, "015_.2f", "0_000_001_234.50")]
    [InlineData(1234.5, "015,.2f", "0,000,001,234.50")]
    [InlineData(1234567.0, "_f", "1_234_567.000000")]
    [InlineData(1234567.0, "_%", "123_456_700.000000%")]
    [InlineData(12345.0, "_e", "1.234500e+04")]
    [InlineData(1234.5, "_g", "1_234.5")]
    public void Apply_Float_MatchesCPython(double value, string spec, string expected)
    {
        PyFormat.Apply(value, spec).Should().Be(expected);
    }

    // ---- grouping x presentation type: CPython's refusal matrix (PEP 378 + PEP 515) ----

    [Theory]
    // ',' is refused by every integer presentation type, '_' only by c/n/s.
    [InlineData(1234567, ",b", "Cannot specify ',' with 'b'.")]
    [InlineData(1234567, ",o", "Cannot specify ',' with 'o'.")]
    [InlineData(1234567, ",x", "Cannot specify ',' with 'x'.")]
    [InlineData(255, ",X", "Cannot specify ',' with 'X'.")]
    [InlineData(1234567, ",n", "Cannot specify ',' with 'n'.")]
    [InlineData(1234567, "_n", "Cannot specify '_' with 'n'.")]
    [InlineData(65, "_c", "Cannot specify '_' with 'c'.")]
    [InlineData(65, ",c", "Cannot specify ',' with 'c'.")]
    // An unknown code with a separator reports the separator first, as CPython does.
    [InlineData(5, "_q", "Cannot specify '_' with 'q'.")]
    [InlineData(5, ",q", "Cannot specify ',' with 'q'.")]
    [InlineData(5, "_z", "Cannot specify '_' with 'z'.")]
    public void Apply_Int_GroupingWithWrongType_RaisesValueError(int value, string spec, string message)
    {
        // python3 -c "format(VALUE, 'SPEC')"  =>  ValueError: MESSAGE
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(value, spec));
        ex.Message.Should().Be(message);
    }

    [Fact]
    public void Apply_Str_BareGrouping_RaisesValueError_NamingS()
    {
        // The absent type stands for the operand's default one, so a str reports 's'.
        // python3 -c "format('abc', '_')"  =>  ValueError: Cannot specify '_' with 's'.
        Assert.Throws<ValueError>(() => PyFormat.Apply("abc", "_")).Message
            .Should().Be("Cannot specify '_' with 's'.");
        Assert.Throws<ValueError>(() => PyFormat.Apply("abc", ",")).Message
            .Should().Be("Cannot specify ',' with 's'.");
        Assert.Throws<ValueError>(() => PyFormat.Apply("abc", "_s")).Message
            .Should().Be("Cannot specify '_' with 's'.");
    }

    [Fact]
    public void Apply_Float_CommaWithRadixType_ReportsTheSeparator_NotTheTypeCode()
    {
        // CPython checks the separator/type pair BEFORE the type/operand pair.
        // python3 -c "format(1.5, ',b')"  =>  ValueError: Cannot specify ',' with 'b'.
        Assert.Throws<ValueError>(() => PyFormat.Apply(1.5, ",b")).Message
            .Should().Be("Cannot specify ',' with 'b'.");

        // '_b' IS a legal separator/type pair, so the float's own refusal is what surfaces.
        // python3 -c "format(1.5, '_b')"  =>  ValueError: Unknown format code 'b' for object of type 'float'
        Assert.Throws<ValueError>(() => PyFormat.Apply(1.5, "_b")).Message
            .Should().Be("Unknown format code 'b' for object of type 'float'");
    }

    [Fact]
    public void Apply_TrailingGarbage_IsReportedBeforeTheSeparatorCheck()
    {
        // python3 -c "format(5, ',bx')"  =>  ValueError: Invalid format specifier ',bx' for object of type 'int'
        Assert.Throws<ValueError>(() => PyFormat.Apply(5, ",bx")).Message
            .Should().Be("Invalid format specifier ',bx' for object of type 'int'");
    }

    // ---- 'c': the code-point range ----

    [Theory]
    [InlineData(1114112)]   // 0x110000, one past the last code point
    [InlineData(-1)]
    [InlineData(2000000)]
    public void Apply_CodePointOutOfRange_RaisesOverflowError_NotADotNetException(int value)
    {
        // python3 -c "format(1114112, 'c')"  =>  OverflowError: %c arg not in range(0x110000)
        // Before the fix this escaped as System.ArgumentOutOfRangeException and aborted the process.
        var ex = Assert.Throws<OverflowError>(() => PyFormat.Apply(value, "c"));
        ex.Message.Should().Be("%c arg not in range(0x110000)");
    }

    [Fact]
    public void Apply_CodePointAtTheBoundaries_Renders()
    {
        // python3 -c "print(format(0x10FFFF,'c') == chr(0x10FFFF), format(0,'c') == chr(0))"  =>  True True
        PyFormat.Apply(0x10FFFF, "c").Should().Be(char.ConvertFromUtf32(0x10FFFF));
        PyFormat.Apply(0, "c").Should().Be("\0");
        // python3 -c "print(repr(format(True, 'c')))"  =>  '\x01'
        PyFormat.Apply(true, "c").Should().Be("\u0001");
    }

    [Fact]
    public void Apply_CodePointInTheSurrogateRange_Renders_AsPythonDoes()
    {
        // A lone surrogate is a legal Python str: python3 -c "print(format(0xD800,'c') == chr(0xD800))" => True
        // char.ConvertFromUtf32 refuses these, so the engine builds them directly.
        PyFormat.Apply(0xD800, "c").Should().Be("\ud800");
        PyFormat.Apply(0xDFFF, "c").Should().Be("\udfff");
    }

    // ---- non-finite floats keep Python's spelling under every presentation type ----

    [Theory]
    [InlineData(double.PositiveInfinity, "f", "inf")]
    [InlineData(double.PositiveInfinity, "F", "INF")]
    [InlineData(double.PositiveInfinity, "e", "inf")]
    [InlineData(double.PositiveInfinity, "E", "INF")]
    [InlineData(double.PositiveInfinity, "g", "inf")]
    [InlineData(double.PositiveInfinity, "G", "INF")]
    [InlineData(double.PositiveInfinity, "%", "inf%")]
    [InlineData(double.PositiveInfinity, "", "inf")]
    [InlineData(double.PositiveInfinity, ".2f", "inf")]
    [InlineData(double.NegativeInfinity, "f", "-inf")]
    [InlineData(double.NegativeInfinity, "F", "-INF")]
    [InlineData(double.NaN, "f", "nan")]
    [InlineData(double.NaN, "F", "NAN")]
    [InlineData(double.NaN, "", "nan")]
    [InlineData(double.PositiveInfinity, ">10f", "       inf")]
    [InlineData(double.PositiveInfinity, "010F", "0000000INF")]
    [InlineData(double.NegativeInfinity, "010", "-000000inf")]
    [InlineData(double.PositiveInfinity, "020,.2f", "00000000000000000inf")]
    [InlineData(double.PositiveInfinity, "+f", "+inf")]
    public void Apply_NonFiniteFloat_MatchesCPython(double value, string spec, string expected)
    {
        // python3 -c "print(repr(format(float('inf'), 'F')))"  =>  'INF'
        // .NET's ToString("F6") says "Infinity"/"NaN" — a second spelling of the same value.
        PyFormat.Apply(value, spec).Should().Be(expected);
    }
}
