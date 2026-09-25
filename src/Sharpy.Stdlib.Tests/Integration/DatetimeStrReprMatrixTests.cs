using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// #2043: <c>str()</c> and <c>repr()</c> of <c>date</c>/<c>time</c>/<c>datetime</c>/<c>timedelta</c>/
/// <c>timezone</c> are python's, in separate columns. The str column covers every str route —
/// <c>str(v)</c>, <c>print(v)</c>, a plain f-string hole and <c>"{}".format(v)</c> — with zero and
/// non-zero microseconds: <c>time</c>/<c>datetime</c> omit <c>.ffffff</c> when the microseconds are 0
/// (their <c>isoformat</c> rule, checked as a control), and <c>timedelta</c> is
/// <c>[D day[s], ]H:MM:SS[.ffffff]</c> over floored days. The repr column (the <c>IRepr</c> channel)
/// covers <c>repr(v)</c>, <c>f"{v!r}"</c> and the element of a list (a nested repr) — the constructor
/// spelling, <c>datetime.datetime(2020, 1, 2, 3, 4, 5)</c>. Expected values are python3 3.12's
/// (identical on 3.14).
/// </summary>
public class DatetimeStrReprMatrixTests : StdlibIntegrationTestBase
{
    public DatetimeStrReprMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData("datetime.date(2020, 1, 2)", "2020-01-02", "datetime.date(2020, 1, 2)", "2020-01-02")]
    [InlineData("datetime.time(3, 4, 5)", "03:04:05", "datetime.time(3, 4, 5)", "03:04:05")]
    [InlineData("datetime.time(3, 4, 5, 7)", "03:04:05.000007", "datetime.time(3, 4, 5, 7)", "03:04:05.000007")]
    [InlineData("datetime.time(3, 4)", "03:04:00", "datetime.time(3, 4)", "03:04:00")]
    [InlineData("datetime.time(3, 4, 0, 7)", "03:04:00.000007", "datetime.time(3, 4, 0, 7)", "03:04:00.000007")]
    [InlineData("datetime.datetime(2020, 1, 2, 3, 4, 5)", "2020-01-02 03:04:05", "datetime.datetime(2020, 1, 2, 3, 4, 5)", "2020-01-02T03:04:05")]
    [InlineData("datetime.datetime(2020, 1, 2, 3, 4, 5, 6)", "2020-01-02 03:04:05.000006", "datetime.datetime(2020, 1, 2, 3, 4, 5, 6)", "2020-01-02T03:04:05.000006")]
    [InlineData("datetime.datetime(2020, 1, 2)", "2020-01-02 00:00:00", "datetime.datetime(2020, 1, 2, 0, 0)", "2020-01-02T00:00:00")]
    [InlineData("datetime.datetime(2020, 1, 2, 3, 4, 0, 6)", "2020-01-02 03:04:00.000006", "datetime.datetime(2020, 1, 2, 3, 4, 0, 6)", "2020-01-02T03:04:00.000006")]
    [InlineData("datetime.datetime(2020, 1, 2, 3, 4, 5, tzinfo=datetime.timezone.utc)", "2020-01-02 03:04:05+00:00", "datetime.datetime(2020, 1, 2, 3, 4, 5, tzinfo=datetime.timezone.utc)", "2020-01-02T03:04:05+00:00")]
    [InlineData("datetime.timedelta(days=1)", "1 day, 0:00:00", "datetime.timedelta(days=1)", null)]
    [InlineData("datetime.timedelta(days=1, microseconds=5)", "1 day, 0:00:00.000005", "datetime.timedelta(days=1, microseconds=5)", null)]
    [InlineData("datetime.timedelta(hours=-1)", "-1 day, 23:00:00", "datetime.timedelta(days=-1, seconds=82800)", null)]
    [InlineData("datetime.timedelta(days=-3, seconds=5)", "-3 days, 0:00:05", "datetime.timedelta(days=-3, seconds=5)", null)]
    [InlineData("datetime.timedelta(seconds=90061)", "1 day, 1:01:01", "datetime.timedelta(days=1, seconds=3661)", null)]
    [InlineData("datetime.timedelta(seconds=5, microseconds=3)", "0:00:05.000003", "datetime.timedelta(seconds=5, microseconds=3)", null)]
    [InlineData("datetime.timedelta(0)", "0:00:00", "datetime.timedelta(0)", null)]
    [InlineData("datetime.timezone.utc", "UTC", "datetime.timezone.utc", null)]
    [InlineData("datetime.timezone(datetime.timedelta(hours=1))", "UTC+01:00", "datetime.timezone(datetime.timedelta(seconds=3600))", null)]
    [InlineData("datetime.timezone(datetime.timedelta(hours=1), \"X\")", "X", "datetime.timezone(datetime.timedelta(seconds=3600), 'X')", null)]
    public void StrAndRepr_OnEveryRoute_MatchPython(string value, string str, string repr, string? isoformat)
    {
        var source = "import datetime\n\n\ndef main() -> None:\n"
            + $"    v = {value}\n"
            + "    print(str(v))\n"
            + "    print(v)\n"
            + "    print(f\"{v}\")\n"
            + "    print(\"{}\".format(v))\n"
            + "    print(repr(v))\n"
            + "    print(f\"{v!r}\")\n"
            + "    print([v])\n"
            + (isoformat != null ? "    print(v.isoformat())\n" : "");
        var result = CompileAndExecute(source);

        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        var expected = new List<string> { str, str, str, str, repr, repr, "[" + repr + "]" };
        if (isoformat != null)
            expected.Add(isoformat);
        Assert.Equal(expected, result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
    }
}
