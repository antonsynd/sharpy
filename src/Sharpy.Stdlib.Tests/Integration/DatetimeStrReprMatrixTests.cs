using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// #2043 (str column): <c>str()</c> of a <c>date</c>/<c>time</c>/<c>datetime</c>/<c>timedelta</c> is
/// python's on every str route — <c>str(v)</c>, <c>print(v)</c>, a plain f-string hole and
/// <c>"{}".format(v)</c> — with zero and non-zero microseconds: <c>time</c>/<c>datetime</c> omit
/// <c>.ffffff</c> when the microseconds are 0 (their <c>isoformat</c> rule, checked as a control), and
/// <c>timedelta</c> is <c>[D day[s], ]H:MM:SS[.ffffff]</c> over floored days. Expected values are
/// python3 3.12's (identical on 3.14). The repr column lands with the python-name channel (P11f).
/// </summary>
public class DatetimeStrReprMatrixTests : StdlibIntegrationTestBase
{
    public DatetimeStrReprMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData("datetime.date(2020, 1, 2)", "2020-01-02", "2020-01-02")]
    [InlineData("datetime.time(3, 4, 5)", "03:04:05", "03:04:05")]
    [InlineData("datetime.time(3, 4, 5, 7)", "03:04:05.000007", "03:04:05.000007")]
    [InlineData("datetime.datetime(2020, 1, 2, 3, 4, 5)", "2020-01-02 03:04:05", "2020-01-02T03:04:05")]
    [InlineData("datetime.datetime(2020, 1, 2, 3, 4, 5, 6)", "2020-01-02 03:04:05.000006", "2020-01-02T03:04:05.000006")]
    [InlineData("datetime.timedelta(days=1)", "1 day, 0:00:00", null)]
    [InlineData("datetime.timedelta(days=1, microseconds=5)", "1 day, 0:00:00.000005", null)]
    [InlineData("datetime.timedelta(hours=-1)", "-1 day, 23:00:00", null)]
    [InlineData("datetime.timedelta(days=-3, seconds=5)", "-3 days, 0:00:05", null)]
    [InlineData("datetime.timedelta(seconds=90061)", "1 day, 1:01:01", null)]
    [InlineData("datetime.timedelta(0)", "0:00:00", null)]
    public void Str_OnEveryStrRoute_MatchesPython(string value, string str, string? isoformat)
    {
        var source = "import datetime\n\n\ndef main() -> None:\n"
            + $"    v = {value}\n"
            + "    print(str(v))\n"
            + "    print(v)\n"
            + "    print(f\"{v}\")\n"
            + "    print(\"{}\".format(v))\n"
            + (isoformat != null ? "    print(v.isoformat())\n" : "");
        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        var expected = new List<string> { str, str, str, str };
        if (isoformat != null)
            expected.Add(isoformat);
        Assert.Equal(expected, result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
    }
}
