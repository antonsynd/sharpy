using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// Every strftime directive (<see cref="StrftimePythonTable"/>, python3 3.12 on macOS) on each of
/// <c>date</c>/<c>time</c>/naive and aware <c>datetime</c>, through every route that reaches
/// python's <c>strftime</c>: <c>.strftime()</c>, an f-string spec, <c>format()</c> and
/// <c>str.format</c> (<c>__format__</c>, spelled <c>IFormattable</c>, #2019). The contract: the
/// output is python's, computed from the value and its tzinfo — never a .NET custom format string
/// (<c>%z</c> was the host offset) and never a post-replace (<c>%j now</c> was <c>003 no0</c>).
/// </summary>
public class StrftimeDirectiveMatrixTests : StdlibIntegrationTestBase
{
    public StrftimeDirectiveMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    private static readonly (string Route, Func<string, string> Expr)[] Routes =
    {
        ("strftime", f => $"v.strftime(\"{f}\")"),
        ("fstring", f => $"f\"{{v:{f}}}\""),
        ("builtin", f => $"format(v, \"{f}\")"),
        ("strformat", f => $"\"{{:{f}}}\".format(v)"),
    };

    [Theory]
    [InlineData("naive", "datetime.datetime(2021, 1, 3, 13, 4, 5, 60)")]
    [InlineData("aware", "datetime.datetime(2021, 1, 3, 13, 4, 5, 60, tzinfo=datetime.timezone.utc)")]
    [InlineData("date", "datetime.date(2021, 1, 3)")]
    [InlineData("time", "datetime.time(13, 4, 5, 60)")]
    public void EveryDirective_OnEveryRoute_MatchesPython(string kind, string value)
    {
        var rows = StrftimePythonTable.Rows;
        var source = new System.Text.StringBuilder("import datetime\n\n\ndef main() -> None:\n")
            .Append("    v = ").Append(value).Append('\n');
        foreach (var row in rows)
        {
            foreach (var (_, expr) in Routes)
                source.Append("    print(repr(").Append(expr(row.Format)).Append("))\n");
        }
        var result = CompileAndExecute(source.ToString());

        Assert.True(result.Success,
            $"{kind}: did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        var lines = result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal(rows.Length * Routes.Length, lines.Length);
        var mismatches = new System.Collections.Generic.List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            var python = kind switch
            {
                "naive" => rows[r].Naive,
                "aware" => rows[r].Aware,
                "date" => rows[r].Date,
                _ => rows[r].Time,
            };
            for (int k = 0; k < Routes.Length; k++)
            {
                var actual = lines[r * Routes.Length + k];
                if (actual != Repr(python))
                    mismatches.Add($"{kind} {Routes[k].Route} {Repr(rows[r].Format)}: python {Repr(python)}, got {actual}");
            }
        }
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} cells differ from python:\n" + string.Join("\n", mismatches));
    }
}
