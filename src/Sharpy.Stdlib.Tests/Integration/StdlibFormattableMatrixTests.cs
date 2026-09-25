using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// #2019: <c>date</c>/<c>time</c>/<c>datetime</c> and <c>Fraction</c> own their spec — python's
/// <c>__format__</c>, spelled <c>System.IFormattable</c> — so every route (<c>format()</c>,
/// <c>str.format</c>, f-string, t-string) formats them as python does: a date-like value through
/// <c>strftime</c> (an empty spec is <c>str(self)</c>), a <c>Fraction</c> through an exact port of
/// CPython 3.14's <c>Fraction.__format__</c> (exact rational rounding, half-even; its own ASCII
/// grammar and <c>ValueError: Invalid format specifier '…' for object of type 'Fraction'</c>).
/// <c>timedelta</c>/<c>timezone</c> have no <c>__format__</c> in python: an empty spec is their
/// <c>str</c>, a non-empty literal spec stays refused statically (SPY0609). Each row's expected text
/// is python's, with the oracle version noted (the no-type Fraction rows are 3.13+; 3.12 refuses them).
/// </summary>
public class StdlibFormattableMatrixTests : StdlibIntegrationTestBase
{
    public StdlibFormattableMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    // Route label and the expression that renders `v` under a literal spec (a plain hole / empty
    // spec when the spec is empty). The t-string renders as the f-string does (template_strings.md).
    private static readonly (string Route, Func<string, string> Expr)[] Routes =
    {
        ("builtin", spec => $"\"[\" + format(v, \"{spec}\") + \"]\""),
        ("strformat", spec => spec.Length == 0 ? "\"[{}]\".format(v)" : $"\"[{{:{spec}}}]\".format(v)"),
        ("fstring", spec => spec.Length == 0 ? "f\"[{v}]\"" : $"f\"[{{v:{spec}}}]\""),
        ("tstring", spec => spec.Length == 0 ? "\"[\" + str(t\"{v}\") + \"]\"" : $"\"[\" + str(t\"{{v:{spec}}}\") + \"]\""),
    };

    private const string Prelude = "import datetime\nfrom fractions import Fraction\n\n\ndef main() -> None:\n";

    [Theory]
    // python 3.12 and 3.14
    [InlineData("date_Y", "datetime.date(2020, 1, 2)", "%Y", "[2020]")]
    // python 3.12 and 3.14
    [InlineData("date_dmy", "datetime.date(2020, 1, 2)", "%d/%m/%y", "[02/01/20]")]
    // python 3.12 and 3.14
    [InlineData("date_empty", "datetime.date(2020, 1, 2)", "", "[2020-01-02]")]
    // python 3.12 and 3.14
    [InlineData("time_HM", "datetime.time(3, 4, 5)", "%H:%M", "[03:04]")]
    // python 3.12 and 3.14
    [InlineData("time_empty", "datetime.time(3, 4, 5)", "", "[03:04:05]")]
    // python 3.12 and 3.14
    [InlineData("time_us_empty", "datetime.time(3, 4, 5, 7)", "", "[03:04:05.000007]")]
    // python 3.12 and 3.14
    [InlineData("datetime_YmdH", "datetime.datetime(2020, 1, 2, 3, 4, 5)", "%Y-%m-%d %H", "[2020-01-02 03]")]
    // python 3.12 and 3.14
    [InlineData("datetime_empty", "datetime.datetime(2020, 1, 2, 3, 4, 5)", "", "[2020-01-02 03:04:05]")]
    // python 3.12 and 3.14
    [InlineData("datetime_us_empty", "datetime.datetime(2020, 1, 2, 3, 4, 5, 6)", "", "[2020-01-02 03:04:05.000006]")]
    // python 3.12 and 3.14
    [InlineData("fraction_3f", "Fraction(1, 3)", ".3f", "[0.333]")]
    // python 3.12 and 3.14
    [InlineData("fraction_20f", "Fraction(1, 3)", ".20f", "[0.33333333333333333333]")]
    // python 3.12 and 3.14
    [InlineData("fraction_half_even_0f", "Fraction(5, 2)", ".0f", "[2]")]
    // python 3.12 and 3.14
    [InlineData("fraction_e", "Fraction(-7, 2)", "e", "[-3.500000e+00]")]
    // python 3.12 and 3.14
    [InlineData("fraction_pct", "Fraction(1, 8)", ".2%", "[12.50%]")]
    // python 3.12 and 3.14
    [InlineData("fraction_g", "Fraction(1, 3)", "g", "[0.333333]")]
    // python 3.12 and 3.14
    [InlineData("fraction_G_small", "Fraction(1, 30000)", "G", "[3.33333E-05]")]
    // python 3.12 and 3.14
    [InlineData("fraction_zeropad", "Fraction(1, 3)", "08.2f", "[00000.33]")]
    // python 3.12 and 3.14
    [InlineData("fraction_eq_sign", "Fraction(-1, 3)", "=+10.2f", "[-     0.33]")]
    // python 3.12 and 3.14
    [InlineData("fraction_z", "Fraction(-1, 1000)", "z.1f", "[0.0]")]
    // python 3.12 and 3.14
    [InlineData("fraction_negzero", "Fraction(-1, 1000)", ".1f", "[-0.0]")]
    // python 3.12 and 3.14
    [InlineData("fraction_grouping", "Fraction(1234567, 1)", ",.1f", "[1,234,567.0]")]
    // python 3.12 and 3.14
    [InlineData("fraction_center_e", "Fraction(22, 7)", "*^14.3e", "[**3.143e+00***]")]
    // python 3.12 and 3.14
    [InlineData("fraction_alt_g", "Fraction(1, 2)", "#.3g", "[0.500]")]
    // python 3.12 and 3.14
    [InlineData("fraction_empty", "Fraction(1, 3)", "", "[1/3]")]
    // python 3.14 (3.12: ValueError: Invalid format specifier '>8' for object of type 'Fraction')
    [InlineData("fraction_pad", "Fraction(1, 3)", ">8", "[     1/3]")]
    // python 3.14 (3.12: ValueError: Invalid format specifier '+#8' for object of type 'Fraction')
    [InlineData("fraction_sign_alt", "Fraction(1, 3)", "+#8", "[    +1/3]")]
    // python 3.14 (3.12: ValueError: Invalid format specifier '#' for object of type 'Fraction')
    [InlineData("fraction_alt_int", "Fraction(3, 1)", "#", "[3/1]")]
    // python 3.14 (3.12: ValueError: Invalid format specifier '_' for object of type 'Fraction')
    [InlineData("fraction_under", "Fraction(1234567, 3)", "_", "[1_234_567/3]")]
    // python 3.14 (3.12: ValueError: Invalid format specifier '0>5' for object of type 'Fraction')
    [InlineData("fraction_fill_zero", "Fraction(1, 3)", "0>5", "[001/3]")]
    // python 3.14 (3.12: ValueError: Invalid format specifier '.6_f' for object of type 'Fraction')
    [InlineData("fraction_frac_sep", "Fraction(1, 3)", ".6_f", "[0.333_333]")]
    // python 3.12 and 3.14
    [InlineData("fraction_no_precision", "Fraction(1, 3)", ".3", "ValueError: Invalid format specifier '.3' for object of type 'Fraction'")]
    // python 3.12 and 3.14
    [InlineData("fraction_zero_width", "Fraction(1, 3)", "05", "ValueError: Invalid format specifier '05' for object of type 'Fraction'")]
    // python 3.12 and 3.14
    [InlineData("fraction_d", "Fraction(1, 3)", "d", "ValueError: Invalid format specifier 'd' for object of type 'Fraction'")]
    // python 3.14 (3.12: ValueError: Invalid format specifier '<05.2f' for object of type 'Fraction'; can't use explicit alignment when zero-padding)
    [InlineData("fraction_align_and_zeropad", "Fraction(1, 3)", "<05.2f", "ValueError: Invalid format specifier '<05.2f' for object of type 'Fraction'")]
    // python 3.12 and 3.14
    [InlineData("fraction_nd_digit", "Fraction(1, 3)", "٣.2f", "ValueError: Invalid format specifier '٣.2f' for object of type 'Fraction'")]
    // python 3.12 and 3.14
    [InlineData("timedelta_empty", "datetime.timedelta(days=1)", "", "[1 day, 0:00:00]")]
    // python 3.12 and 3.14
    [InlineData("timedelta_neg_empty", "datetime.timedelta(hours=-1)", "", "[-1 day, 23:00:00]")]
    // python 3.12 and 3.14
    [InlineData("timezone_empty", "datetime.timezone.utc", "", "[UTC]")]
    public void OwnsItsSpec_OnEveryRoute_MatchesPython(string label, string value, string spec, string expected)
    {
        var body = string.Concat(Routes.Select(r =>
            $"    try:\n        print({r.Expr(spec)})\n    except ValueError as e:\n        print(\"ValueError:\", e)\n"));
        var result = CompileAndExecute(Prelude + $"    v = {value}\n" + body);

        Assert.True(result.Success,
            $"{label}: did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        var lines = result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal(Routes.Select(_ => expected), lines);
    }

    /// <summary>
    /// python has no <c>timedelta.__format__</c>/<c>timezone.__format__</c>: a non-empty spec is its
    /// TypeError <c>unsupported format string passed to datetime.timedelta.__format__</c>. Sharpy
    /// refuses the literal spec statically on every route; the type is spelled by its CLR name until
    /// the python-name channel (P11f) renames it, so the message is matched by its prefix.
    /// </summary>
    [Theory]
    // python 3.12 and 3.14: TypeError: unsupported format string passed to datetime.timedelta.__format__
    [InlineData("timedelta_refused", "datetime.timedelta(days=1)", "x")]
    // python 3.12 and 3.14: TypeError: unsupported format string passed to datetime.timezone.__format__
    [InlineData("timezone_refused", "datetime.timezone.utc", ">5")]
    public void NoFormat_NonEmptySpec_StaysRefused(string label, string value, string spec)
    {
        var body = string.Concat(Routes.Select(r => $"    print({r.Expr(spec)})\n"));
        var result = CompileAndExecute(Prelude + $"    v = {value}\n" + body);

        var refusals = result.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification)
            .Select(d => d.Message)
            .ToList();
        Assert.True(refusals.Count == Routes.Length
                && refusals.All(m => m.StartsWith("unsupported format string passed to ", StringComparison.Ordinal)),
            $"{label}: expected {Routes.Length} SPY0609 'unsupported format string passed to …', got: "
            + string.Join("; ", result.RawDiagnostics.Select(d => d.Code + ": " + d.Message)));
    }
}
