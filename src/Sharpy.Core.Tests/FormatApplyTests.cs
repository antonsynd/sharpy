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
    // --- #1944: the sign applies to EVERY numeric presentation type, including '%' ---
    [InlineData(2, "+%", "+200.000000%")]      // format(2, '+%')
    [InlineData(true, "+%", "+100.000000%")]   // format(True, '+%')
    [InlineData(42, "+x", "+2a")]              // format(42, '+x') — sign with a radix type
    [InlineData(42, "+o", "+52")]              // format(42, '+o')
    [InlineData(42, "+b", "+101010")]          // format(42, '+b')
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
    // --- #1944: sign x '%' on floats, precision, space and minus ---
    [InlineData(1.5, "+%", "+150.000000%")]    // format(1.5, '+%')
    [InlineData(1.5, " %", " 150.000000%")]    // format(1.5, ' %')
    [InlineData(1.5, "-%", "150.000000%")]     // format(1.5, '-%')
    [InlineData(1.5, "+.1%", "+150.0%")]       // format(1.5, '+.1%')
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
    // --- #1945: the '0' flag on a string is fill='0' at the string default align '<' ---
    [InlineData("ab", "05", "ab000")]          // format('ab', '05')  — NOT '000ab'
    [InlineData("ab", ">05", "000ab")]         // format('ab', '>05')
    [InlineData("ab", "<05", "ab000")]         // format('ab', '<05')
    [InlineData("ab", "05.1", "a0000")]        // format('ab', '05.1')
    [InlineData("abcdef", ".3", "abc")]        // format('abcdef', '.3')
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

    /// <summary>
    /// #1958: the alternate form (<c>#</c>) is ONE rule across the whole float presentation family
    /// (<c>e E f F g G n %</c> and the absent type on a float): the result always carries a decimal
    /// point, and <c>g</c>/<c>G</c>/<c>n</c>/absent-type keep their trailing zeros. Integral
    /// presentations stay integral; non-finite values stay pointless. Every row was generated with
    /// <c>python3 -c "print(repr(format(VALUE, 'SPEC')))"</c> (CPython 3.12) — the call is quoted on
    /// the row.
    /// </summary>
    [Theory]
    // --- '#' x type 'e' x {42, 3.5, 3.0, True} x precision {absent, .0, .4} ---
    [InlineData(42, "#e", "4.200000e+01")]               // format(42, '#e')
    [InlineData(3.5, "#e", "3.500000e+00")]              // format(3.5, '#e')
    [InlineData(3.0, "#e", "3.000000e+00")]              // format(3.0, '#e')
    [InlineData(true, "#e", "1.000000e+00")]             // format(True, '#e')
    [InlineData(42, "#.0e", "4.e+01")]                   // format(42, '#.0e')
    [InlineData(3.5, "#.0e", "4.e+00")]                  // format(3.5, '#.0e')
    [InlineData(3.0, "#.0e", "3.e+00")]                  // format(3.0, '#.0e')
    [InlineData(true, "#.0e", "1.e+00")]                 // format(True, '#.0e')
    [InlineData(42, "#.4e", "4.2000e+01")]               // format(42, '#.4e')
    [InlineData(3.5, "#.4e", "3.5000e+00")]              // format(3.5, '#.4e')
    [InlineData(3.0, "#.4e", "3.0000e+00")]              // format(3.0, '#.4e')
    [InlineData(true, "#.4e", "1.0000e+00")]             // format(True, '#.4e')
    // --- '#' x type 'E' x {42, 3.5, 3.0, True} x precision {absent, .0, .4} ---
    [InlineData(42, "#E", "4.200000E+01")]               // format(42, '#E')
    [InlineData(3.5, "#E", "3.500000E+00")]              // format(3.5, '#E')
    [InlineData(3.0, "#E", "3.000000E+00")]              // format(3.0, '#E')
    [InlineData(true, "#E", "1.000000E+00")]             // format(True, '#E')
    [InlineData(42, "#.0E", "4.E+01")]                   // format(42, '#.0E')
    [InlineData(3.5, "#.0E", "4.E+00")]                  // format(3.5, '#.0E')
    [InlineData(3.0, "#.0E", "3.E+00")]                  // format(3.0, '#.0E')
    [InlineData(true, "#.0E", "1.E+00")]                 // format(True, '#.0E')
    [InlineData(42, "#.4E", "4.2000E+01")]               // format(42, '#.4E')
    [InlineData(3.5, "#.4E", "3.5000E+00")]              // format(3.5, '#.4E')
    [InlineData(3.0, "#.4E", "3.0000E+00")]              // format(3.0, '#.4E')
    [InlineData(true, "#.4E", "1.0000E+00")]             // format(True, '#.4E')
    // --- '#' x type 'f' x {42, 3.5, 3.0, True} x precision {absent, .0, .4} ---
    [InlineData(42, "#f", "42.000000")]                  // format(42, '#f')
    [InlineData(3.5, "#f", "3.500000")]                  // format(3.5, '#f')
    [InlineData(3.0, "#f", "3.000000")]                  // format(3.0, '#f')
    [InlineData(true, "#f", "1.000000")]                 // format(True, '#f')
    [InlineData(42, "#.0f", "42.")]                      // format(42, '#.0f')
    [InlineData(3.5, "#.0f", "4.")]                      // format(3.5, '#.0f')
    [InlineData(3.0, "#.0f", "3.")]                      // format(3.0, '#.0f')
    [InlineData(true, "#.0f", "1.")]                     // format(True, '#.0f')
    [InlineData(42, "#.4f", "42.0000")]                  // format(42, '#.4f')
    [InlineData(3.5, "#.4f", "3.5000")]                  // format(3.5, '#.4f')
    [InlineData(3.0, "#.4f", "3.0000")]                  // format(3.0, '#.4f')
    [InlineData(true, "#.4f", "1.0000")]                 // format(True, '#.4f')
    // --- '#' x type 'F' x {42, 3.5, 3.0, True} x precision {absent, .0, .4} ---
    [InlineData(42, "#F", "42.000000")]                  // format(42, '#F')
    [InlineData(3.5, "#F", "3.500000")]                  // format(3.5, '#F')
    [InlineData(3.0, "#F", "3.000000")]                  // format(3.0, '#F')
    [InlineData(true, "#F", "1.000000")]                 // format(True, '#F')
    [InlineData(42, "#.0F", "42.")]                      // format(42, '#.0F')
    [InlineData(3.5, "#.0F", "4.")]                      // format(3.5, '#.0F')
    [InlineData(3.0, "#.0F", "3.")]                      // format(3.0, '#.0F')
    [InlineData(true, "#.0F", "1.")]                     // format(True, '#.0F')
    [InlineData(42, "#.4F", "42.0000")]                  // format(42, '#.4F')
    [InlineData(3.5, "#.4F", "3.5000")]                  // format(3.5, '#.4F')
    [InlineData(3.0, "#.4F", "3.0000")]                  // format(3.0, '#.4F')
    [InlineData(true, "#.4F", "1.0000")]                 // format(True, '#.4F')
    // --- '#' x type 'g' x {42, 3.5, 3.0, True} x precision {absent, .0, .4} ---
    [InlineData(42, "#g", "42.0000")]                    // format(42, '#g')
    [InlineData(3.5, "#g", "3.50000")]                   // format(3.5, '#g')
    [InlineData(3.0, "#g", "3.00000")]                   // format(3.0, '#g')
    [InlineData(true, "#g", "1.00000")]                  // format(True, '#g')
    [InlineData(42, "#.0g", "4.e+01")]                   // format(42, '#.0g')
    [InlineData(3.5, "#.0g", "4.")]                      // format(3.5, '#.0g')
    [InlineData(3.0, "#.0g", "3.")]                      // format(3.0, '#.0g')
    [InlineData(true, "#.0g", "1.")]                     // format(True, '#.0g')
    [InlineData(42, "#.4g", "42.00")]                    // format(42, '#.4g')
    [InlineData(3.5, "#.4g", "3.500")]                   // format(3.5, '#.4g')
    [InlineData(3.0, "#.4g", "3.000")]                   // format(3.0, '#.4g')
    [InlineData(true, "#.4g", "1.000")]                  // format(True, '#.4g')
    // --- '#' x type 'G' x {42, 3.5, 3.0, True} x precision {absent, .0, .4} ---
    [InlineData(42, "#G", "42.0000")]                    // format(42, '#G')
    [InlineData(3.5, "#G", "3.50000")]                   // format(3.5, '#G')
    [InlineData(3.0, "#G", "3.00000")]                   // format(3.0, '#G')
    [InlineData(true, "#G", "1.00000")]                  // format(True, '#G')
    [InlineData(42, "#.0G", "4.E+01")]                   // format(42, '#.0G')
    [InlineData(3.5, "#.0G", "4.")]                      // format(3.5, '#.0G')
    [InlineData(3.0, "#.0G", "3.")]                      // format(3.0, '#.0G')
    [InlineData(true, "#.0G", "1.")]                     // format(True, '#.0G')
    [InlineData(42, "#.4G", "42.00")]                    // format(42, '#.4G')
    [InlineData(3.5, "#.4G", "3.500")]                   // format(3.5, '#.4G')
    [InlineData(3.0, "#.4G", "3.000")]                   // format(3.0, '#.4G')
    [InlineData(true, "#.4G", "1.000")]                  // format(True, '#.4G')
    // --- '#' x type 'n' x {42, 3.5, 3.0, True} x precision {absent, .0, .4} ---
    [InlineData(42, "#n", "42")]                         // format(42, '#n')
    [InlineData(3.5, "#n", "3.50000")]                   // format(3.5, '#n')
    [InlineData(3.0, "#n", "3.00000")]                   // format(3.0, '#n')
    [InlineData(true, "#n", "1")]                        // format(True, '#n')
    [InlineData(3.5, "#.0n", "4.")]                      // format(3.5, '#.0n')
    [InlineData(3.0, "#.0n", "3.")]                      // format(3.0, '#.0n')
    [InlineData(3.5, "#.4n", "3.500")]                   // format(3.5, '#.4n')
    [InlineData(3.0, "#.4n", "3.000")]                   // format(3.0, '#.4n')
    // --- '#' x type '%' x {42, 3.5, 3.0, True} x precision {absent, .0, .4} ---
    [InlineData(42, "#%", "4200.000000%")]               // format(42, '#%')
    [InlineData(3.5, "#%", "350.000000%")]               // format(3.5, '#%')
    [InlineData(3.0, "#%", "300.000000%")]               // format(3.0, '#%')
    [InlineData(true, "#%", "100.000000%")]              // format(True, '#%')
    [InlineData(42, "#.0%", "4200.%")]                   // format(42, '#.0%')
    [InlineData(3.5, "#.0%", "350.%")]                   // format(3.5, '#.0%')
    [InlineData(3.0, "#.0%", "300.%")]                   // format(3.0, '#.0%')
    [InlineData(true, "#.0%", "100.%")]                  // format(True, '#.0%')
    [InlineData(42, "#.4%", "4200.0000%")]               // format(42, '#.4%')
    [InlineData(3.5, "#.4%", "350.0000%")]               // format(3.5, '#.4%')
    [InlineData(3.0, "#.4%", "300.0000%")]               // format(3.0, '#.4%')
    [InlineData(true, "#.4%", "100.0000%")]              // format(True, '#.4%')
    // --- '#' x type '(none)' x {42, 3.5, 3.0, True} x precision {absent, .0, .4} ---
    [InlineData(42, "#", "42")]                          // format(42, '#')
    [InlineData(3.5, "#", "3.5")]                        // format(3.5, '#')
    [InlineData(3.0, "#", "3.0")]                        // format(3.0, '#')
    [InlineData(true, "#", "1")]                         // format(True, '#')
    [InlineData(3.5, "#.0", "4.e+00")]                   // format(3.5, '#.0')
    [InlineData(3.0, "#.0", "3.e+00")]                   // format(3.0, '#.0')
    [InlineData(3.5, "#.4", "3.500")]                    // format(3.5, '#.4')
    [InlineData(3.0, "#.4", "3.000")]                    // format(3.0, '#.4')
    // --- width, '0'-fill, grouping, sign, z: the forced point is part of the tail ---
    [InlineData(42, "#012.3g", "0000000042.0")]          // format(42, '#012.3g')
    [InlineData(42, "#012.0f", "00000000042.")]          // format(42, '#012.0f')
    [InlineData(3.5, "#010.0e", "00004.e+00")]           // format(3.5, '#010.0e')
    [InlineData(1234.5, "#_.0f", "1_234.")]              // format(1234.5, '#_.0f')
    [InlineData(1234.5, "#,.0f", "1,234.")]              // format(1234.5, '#,.0f')
    [InlineData(1234.5, "#012,.0f", "000,001,234.")]     // format(1234.5, '#012,.0f')
    [InlineData(1234567.0, "#_.0f", "1_234_567.")]       // format(1234567.0, '#_.0f')
    [InlineData(12345.0, "#_.0%", "1_234_500.%")]        // format(12345.0, '#_.0%')
    [InlineData(42, "#010.0%", "00004200.%")]            // format(42, '#010.0%')
    [InlineData(3.5, "#5n", "3.50000")]                  // format(3.5, '#5n')
    [InlineData(3.5, "#8.0f", "      4.")]               // format(3.5, '#8.0f')
    [InlineData(3.5, "*<#8.0f", "4.******")]             // format(3.5, '*<#8.0f')
    [InlineData(-3.5, "#08.0f", "-000004.")]             // format(-3.5, '#08.0f')
    [InlineData(3.5, "+#.0f", "+4.")]                    // format(3.5, '+#.0f')
    [InlineData(-0.0, "#.0f", "-0.")]                    // format(-0.0, '#.0f')
    [InlineData(-0.0, "z#.0f", "0.")]                    // format(-0.0, 'z#.0f')
    [InlineData(0.0, "#.0", "0.e+00")]                   // format(0.0, '#.0')
    [InlineData(0.0, "#g", "0.00000")]                   // format(0.0, '#g')
    [InlineData(9.99, "#.0f", "10.")]                    // format(9.99, '#.0f')
    [InlineData(0.5, "#.0g", "0.5")]                     // format(0.5, '#.0g')
    [InlineData(2.5, "#.0f", "2.")]                      // format(2.5, '#.0f')
    // --- absent type, absent precision: str(value) plus the point when it has none ---
    [InlineData(1e+20, "#", "1.e+20")]                   // format(1e+20, '#')
    [InlineData(1e+16, "#", "1.e+16")]                   // format(1e+16, '#')
    [InlineData(1e-05, "#", "1.e-05")]                   // format(1e-05, '#')
    [InlineData(0.0001, "#", "0.0001")]                  // format(0.0001, '#')
    [InlineData(123456789.0, "#", "123456789.0")]        // format(123456789.0, '#')
    [InlineData(1e+100, "#", "1.e+100")]                 // format(1e+100, '#')
    [InlineData(0.0, "#", "0.0")]                        // format(0.0, '#')
    [InlineData(1e+20, "#g", "1.00000e+20")]             // format(1e+20, '#g')
    [InlineData(1e-05, "#g", "1.00000e-05")]             // format(1e-05, '#g')
    [InlineData(100000.0, "#g", "100000.")]              // format(100000.0, '#g')
    [InlineData(1000000.0, "#g", "1.00000e+06")]         // format(1000000.0, '#g')
    [InlineData(1e+22, "#.3", "1.00e+22")]               // format(1e+22, '#.3')
    [InlineData(1.234e-05, "#.2", "1.2e-05")]            // format(1.234e-05, '#.2')
    [InlineData(3.0, "#.1", "3.e+00")]                   // format(3.0, '#.1')
    // --- integral presentations stay integral under '#' ---
    [InlineData(42, "#d", "42")]                         // format(42, '#d')
    [InlineData(42, "#010d", "0000000042")]              // format(42, '#010d')
    [InlineData(42, "#_d", "42")]                        // format(42, '#_d')
    [InlineData(true, "#d", "1")]                        // format(True, '#d')
    // --- non-finite stays pointless under '#' ---
    [InlineData(double.PositiveInfinity, "#", "inf")]    // format(float('inf'), '#')
    [InlineData(double.PositiveInfinity, "#.0f", "inf")] // format(float('inf'), '#.0f')
    [InlineData(double.NaN, "#g", "nan")]                // format(float('nan'), '#g')
    [InlineData(double.NegativeInfinity, "#.0e", "-inf")] // format(float('-inf'), '#.0e')
    [InlineData(double.PositiveInfinity, "#.0%", "inf%")] // format(float('inf'), '#.0%')
    [InlineData(double.PositiveInfinity, "#010.0f", "0000000inf")] // format(float('inf'), '#010.0f')
    [InlineData(double.NaN, "#.0", "nan")]               // format(float('nan'), '#.0')
    [InlineData(double.PositiveInfinity, "#.0G", "INF")] // format(float('inf'), '#.0G')
    public void Apply_AlternateForm_FloatFamily_MatchesCPython(object value, string spec, string expected)
    {
        PyFormat.Apply(value, spec).Should().Be(expected);
    }

    /// <summary>
    /// #1958 refusal rows: '#' does not lift the integer precision refusal — an int or bool with a
    /// precision on an integral presentation (<c>n</c>, absent type) is still a ValueError.
    /// python3 -c "format(42, '#.0n')"  =>  ValueError: Precision not allowed in integer format specifier
    /// </summary>
    [Theory]
    [InlineData(42, "#.0n")]         // format(42, '#.0n')
    [InlineData(true, "#.0n")]       // format(True, '#.0n')
    [InlineData(42, "#.4n")]         // format(42, '#.4n')
    [InlineData(true, "#.4n")]       // format(True, '#.4n')
    [InlineData(42, "#.0")]          // format(42, '#.0')
    [InlineData(true, "#.0")]        // format(True, '#.0')
    [InlineData(42, "#.4")]          // format(42, '#.4')
    [InlineData(true, "#.4")]        // format(True, '#.4')
    public void Apply_AlternateForm_IntPrecisionOnIntegralType_StillRefused(object value, string spec)
    {
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(value, spec));
        ex.Message.Should().Be("Precision not allowed in integer format specifier");
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

    [Fact]
    public void Apply_NonFinite_Percent_KeepsTheSign()
    {
        // python3 -c "print(repr(format(float('inf'), '+%')))"  =>  '+inf%'
        PyFormat.Apply(double.PositiveInfinity, "+%").Should().Be("+inf%");
    }

    // ---- #1944: a sign with the 'c' presentation type is refused, statically and at runtime ----

    [Theory]
    [InlineData(65, "+c")]  // format(65, '+c')
    [InlineData(65, " c")]  // format(65, ' c')
    [InlineData(65, "-c")]  // format(65, '-c')
    public void Apply_Sign_WithCharacterType_RaisesValueError(object value, string spec)
    {
        // python3 -c "format(65, '+c')"
        //   =>  ValueError: Sign not allowed with integer format specifier 'c'
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply(value, spec));
        ex.Message.Should().Be("Sign not allowed with integer format specifier 'c'");
    }

    [Fact]
    public void Apply_NoSign_WithCharacterType_StillRenders()
    {
        // The positive control: 'c' without a sign is unaffected.
        // python3 -c "print(format(65, 'c'))"  =>  A
        PyFormat.Apply(65, "c").Should().Be("A");
    }

    // ---- #1945: string operands refuse sign / z / '#' / '=' in CPython's order and wording ----

    [Theory]
    [InlineData("=5", "'=' alignment not allowed in string format specifier")]        // format('ab','=5')
    [InlineData("0=5", "'=' alignment not allowed in string format specifier")]       // format('ab','0=5')
    [InlineData("x=5", "'=' alignment not allowed in string format specifier")]       // format('ab','x=5')
    [InlineData("^=5", "'=' alignment not allowed in string format specifier")]       // format('ab','^=5')
    [InlineData("+5", "Sign not allowed in string format specifier")]                 // format('ab','+5')
    [InlineData("-5", "Sign not allowed in string format specifier")]                 // format('ab','-5')
    [InlineData(" 5", "Space not allowed in string format specifier")]                // format('ab',' 5')
    [InlineData("#5", "Alternate form (#) not allowed in string format specifier")]   // format('ab','#5')
    [InlineData("z5", "Negative zero coercion (z) not allowed in string format specifier")] // format('ab','z5')
    // Ordering: with '+' as a real sign and '=' as align, the sign refusal wins ('=+5'); with '+'
    // as a fill and '=' as align, no sign is parsed so the '=' refusal wins ('+=5').
    [InlineData("=+5", "Sign not allowed in string format specifier")]                // format('ab','=+5')
    [InlineData("+=5", "'=' alignment not allowed in string format specifier")]       // format('ab','+=5')
    public void Apply_StringOperand_RefusesWithCPythonWording(string spec, string message)
    {
        var ex = Assert.Throws<ValueError>(() => PyFormat.Apply("ab", spec));
        ex.Message.Should().Be(message);
    }
}
