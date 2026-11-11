using SCG = System.Collections.Generic;
using System.Text.Json;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// The VALUES of datetime arithmetic and ISO parsing, against python as the oracle. The exception-escape sweep sees
/// only exceptions; a wrong value (<c>date - timedelta(hours=1)</c> moving a day, <c>timedelta(hours=-1).days</c>
/// truncating, <c>fromisoformat</c> accepting non-ISO text) is invisible to it.
///
/// <para><b>Generated.</b> Operands are crossed, not listed per cell: dates at <c>date.min/max</c> and month, year
/// and leap boundaries; timedeltas with sub-day parts of both signs and at Sharpy's range edge; integers for
/// <c>*</c> and <c>/</c>; and the ISO grammar (date forms × separators × time forms × offset forms, plus non-ISO
/// text). Every case goes to python in one batch; both sides render the result as <c>repr()</c> (attributes for a
/// timedelta) or <c>!ExceptionType</c>, and the renderings must be equal.</para>
///
/// <para><b>The one admitted divergence</b> is the documented range deviation <c>timedelta-range-timespan</c>
/// (#2109): where python's result is a timedelta past ±10675199 days, 2:48:05.477580, Sharpy raises
/// <c>OverflowError</c>. Such a case is counted and must be an <c>OverflowError</c>; nothing else may differ.</para>
/// </summary>
public class DatetimeValueDifferentialTests
{
    private const string OracleScript = """
        import json, sys
        from datetime import date, datetime, timedelta, timezone

        # Sharpy's timedelta is a TimeSpan: |ticks| <= 2**63 - 1, at microsecond resolution.
        LIMIT = timedelta(microseconds=(2**63 - 1) // 10)

        def td(a):
            return timedelta(days=a[0], seconds=a[1], microseconds=a[2])

        def tdattrs(t):
            return "|".join([str(t.days), str(t.seconds), str(t.microseconds), str(t), repr(t), repr(t.total_seconds())])

        def big(v):
            return isinstance(v, timedelta) and abs(v) > LIMIT

        for line in sys.stdin:
            c = json.loads(line)
            k = c["k"]
            beyond = False
            try:
                if k == "date_op":
                    d = date(*c["d"]); t = td(c["t"])
                    v = d + t if c["op"] == "+" else d - t
                    r = repr(v)
                elif k == "dt_op":
                    d = datetime(*c["d"]); t = td(c["t"])
                    v = d + t if c["op"] == "+" else d - t
                    r = repr(v)
                elif k == "date_diff":
                    v = date(*c["a"]) - date(*c["b"]); r = tdattrs(v)
                elif k == "td":
                    v = td(c["t"]); beyond = big(v); r = tdattrs(v)
                elif k == "td_op":
                    a = td(c["a"]); b = td(c["b"])
                    v = a + b if c["op"] == "+" else a - b
                    beyond = big(v); r = tdattrs(v)
                elif k == "td_mul":
                    v = td(c["t"]) * c["n"]; beyond = big(v); r = tdattrs(v)
                elif k == "td_div":
                    v = td(c["t"]) / c["n"]; r = tdattrs(v)
                elif k == "td_neg":
                    v = -td(c["t"]); r = tdattrs(v)
                elif k == "td_abs":
                    v = abs(td(c["t"])); r = tdattrs(v)
                elif k == "date_iso":
                    r = repr(date.fromisoformat(c["s"]))
                elif k == "dt_iso":
                    r = repr(datetime.fromisoformat(c["s"]))
                else:
                    raise SystemExit("unknown case kind " + k)
            except (ArithmeticError, ValueError, TypeError) as e:
                r = "!" + type(e).__name__
            print(json.dumps({"r": r, "beyond": beyond}))
        """;

    private static readonly int[][] Dates =
    {
        new[] { 1, 1, 1 }, new[] { 1, 1, 2 }, new[] { 1, 12, 31 }, new[] { 2, 1, 1 }, new[] { 1999, 12, 31 },
        new[] { 2000, 2, 28 }, new[] { 2000, 2, 29 }, new[] { 2000, 3, 1 }, new[] { 2020, 1, 1 }, new[] { 2020, 12, 31 },
        new[] { 2100, 2, 28 }, new[] { 9999, 1, 1 }, new[] { 9999, 12, 30 }, new[] { 9999, 12, 31 },
    };

    private static readonly int[][] DateTimes =
    {
        new[] { 1, 1, 1, 0, 0, 0, 0 }, new[] { 1, 1, 1, 23, 59, 59, 999999 }, new[] { 2000, 2, 29, 12, 0, 0, 0 },
        new[] { 1999, 12, 31, 23, 59, 59, 999999 }, new[] { 2020, 1, 1, 0, 0, 0, 1 }, new[] { 9999, 12, 31, 0, 0, 0, 0 },
        new[] { 9999, 12, 31, 23, 59, 59, 999999 },
    };

    /// <summary>(days, seconds, microseconds) as passed to the constructor, unnormalised.</summary>
    private static readonly int[][] Deltas =
    {
        new[] { 0, 0, 0 }, new[] { 0, 0, 1 }, new[] { 0, 0, -1 }, new[] { 0, 3600, 0 }, new[] { 0, -3600, 0 },
        new[] { 0, 82800, 0 }, new[] { 0, -90000, 0 }, new[] { 1, -3600, 0 }, new[] { -1, 3600, 0 }, new[] { -1, -43200, 0 },
        new[] { 1, 0, 0 }, new[] { -1, 0, 0 }, new[] { 0, 86399, 999999 }, new[] { 0, -86399, -999999 }, new[] { 0, 59, 999999 },
        new[] { 365, 0, 0 }, new[] { -366, 1, 0 }, new[] { 3652058, 0, 0 }, new[] { -3652058, 0, 0 }, new[] { 3652059, 0, 0 },
        new[] { 10675199, 0, 0 }, new[] { -10675199, 0, 0 }, new[] { 10675199, 10085, 477580 }, new[] { -10675199, -10085, -477580 },
    };

    private static readonly int[] Factors = { 0, 1, -1, 2, 3, 7, -2, 1_000_000, int.MaxValue };

    private static readonly string[] IsoDates =
    {
        "2020-01-02", "20200102", "2020-W01-1", "2020W011", "2020-W01", "2020W01", "2020-W53-7", "2021-W53-1", "2015-W53-1",
        "9999-W52-6", "0001-W01-1", "2020-13-01", "2020-02-30", "0000-01-01", "9999-12-31", "2020-12-31", "2020-1-02",
        "2020-01-2a", "202001",
    };

    private static readonly string[] IsoSeparators = { "T", " ", "x", "é" };

    private static readonly string[] IsoTimes =
    {
        "12", "12:30", "1230", "12:30:45", "123045", "12:30:45.5", "12:30:45,123", "12:30:45.1234567", "12:30:45.",
        "24:00", "24:00:01", "23:59:60", "12:3", "12:", "1", "", "12:30:4", "12.5",
    };

    private static readonly string[] IsoOffsets = { "", "Z", "+05:30", "-0800", "+05", "+05:30:15.5", "-00:00", "+24:00", "+5", "Zx", "+05:30x", "-23:59:59.999999" };

    private static readonly string[] NonIso =
    {
        "", "a", "not-a-date", "January 2, 2020", "1/2/2020", "2020-01-02 3pm", "12:30:45", "２０２０-01-01",
        "2020-01-02T03:04:05+02:00", "20200101xx", "2020-01-01T12:00z", "2020-01-01T12:00Z+01:00", "2020-01-01T-01:00",
        "2020-W011", "2020W01-1", "2020-0101", "20-01-01", "2020-01", "2020-01-01T", "  2020-01-01",
    };

    private readonly ITestOutputHelper _output;

    public DatetimeValueDifferentialTests(ITestOutputHelper output) => _output = output;

    private sealed record Case(Dictionary<string, object> Spec, Func<string> Sharpy, bool TimedeltaResult);

    [SkippableFact]
    public void DatetimeValues_MatchPython()
    {
        var cases = Cases();
        var python = PythonInterpreter.RequireOrSkip();
        var script = SysPath.Combine(SysPath.GetTempPath(), $"sharpy-datetime-values-{Guid.NewGuid():N}.py");
        File.WriteAllText(script, OracleScript);
        SCG.List<string> lines;
        try
        {
            lines = python.RunBatch(script, cases.Select(c => JsonSerializer.Serialize(c.Spec)));
        }
        finally
        {
            File.Delete(script);
        }
        Assert.Equal(cases.Count, lines.Count);

        var mismatches = new SCG.List<string>();
        int deviations = 0;
        for (int i = 0; i < cases.Count; i++)
        {
            using var doc = JsonDocument.Parse(lines[i]);
            var expected = doc.RootElement.GetProperty("r").GetString()!;
            var beyond = doc.RootElement.GetProperty("beyond").GetBoolean();
            var actual = Render(cases[i].Sharpy);
            if (beyond && !expected.StartsWith('!'))
            {
                deviations++;
                if (actual != "!OverflowError")
                    mismatches.Add($"{JsonSerializer.Serialize(cases[i].Spec)}: past Sharpy's timedelta range python gives {expected}; Sharpy must raise OverflowError (#2109), got {actual}");
                continue;
            }
            if (expected != actual)
                mismatches.Add($"{JsonSerializer.Serialize(cases[i].Spec)}: python {expected} | sharpy {actual}");
        }

        _output.WriteLine($"python 3.{python.Minor}: {cases.Count} cases, {deviations} on the #2109 range deviation, {mismatches.Count} mismatches");
        Assert.True(deviations > 0, "no case reached the #2109 range deviation: the operand set lost its edge values");
        Assert.True(mismatches.Count == 0,
            $"{mismatches.Count} datetime values differ from python 3.{python.Minor}:\n  " + string.Join("\n  ", mismatches.Take(150)));
    }

    private static string Render(Func<string> f)
    {
        try
        {
            return f();
        }
        catch (Exception e) when (e is ArithmeticError or ValueError or TypeError)
        {
            return "!" + e.GetType().Name;
        }
    }

    private static Timedelta Td(int[] t) => new(days: t[0], seconds: t[1], microseconds: t[2]);

    private static string Attrs(Timedelta t)
        => string.Join("|", t.Days.ToString(System.Globalization.CultureInfo.InvariantCulture),
            t.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            t.Microseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            t.ToString(), Repr(t), Repr(t.TotalSeconds));

    private static SCG.List<Case> Cases()
    {
        var cases = new SCG.List<Case>();
        void Add(Dictionary<string, object> spec, Func<string> sharpy, bool tdResult = false) => cases.Add(new Case(spec, sharpy, tdResult));

        foreach (var d in Dates)
        {
            foreach (var t in Deltas)
            {
                foreach (var op in new[] { "+", "-" })
                {
                    Add(new() { ["k"] = "date_op", ["d"] = d, ["t"] = t, ["op"] = op },
                        () => Repr(op == "+" ? new Date(d[0], d[1], d[2]) + Td(t) : new Date(d[0], d[1], d[2]) - Td(t)));
                }
            }
            foreach (var other in Dates)
                Add(new() { ["k"] = "date_diff", ["a"] = d, ["b"] = other }, () => Attrs(new Date(d[0], d[1], d[2]) - new Date(other[0], other[1], other[2])));
        }
        foreach (var d in DateTimes)
        {
            foreach (var t in Deltas)
            {
                foreach (var op in new[] { "+", "-" })
                {
                    Add(new() { ["k"] = "dt_op", ["d"] = d, ["t"] = t, ["op"] = op }, () =>
                    {
                        var dt = new DateTime(d[0], d[1], d[2], d[3], d[4], d[5], d[6]);
                        return Repr(op == "+" ? dt + Td(t) : dt - Td(t));
                    });
                }
            }
        }
        foreach (var t in Deltas)
        {
            Add(new() { ["k"] = "td", ["t"] = t }, () => Attrs(Td(t)), true);
            Add(new() { ["k"] = "td_neg", ["t"] = t }, () => Attrs(-Td(t)));
            Add(new() { ["k"] = "td_abs", ["t"] = t }, () => Attrs(Td(t).Abs()));
            foreach (var u in Deltas)
            {
                foreach (var op in new[] { "+", "-" })
                    Add(new() { ["k"] = "td_op", ["a"] = t, ["b"] = u, ["op"] = op }, () => Attrs(op == "+" ? Td(t) + Td(u) : Td(t) - Td(u)), true);
            }
            foreach (var n in Factors)
            {
                Add(new() { ["k"] = "td_mul", ["t"] = t, ["n"] = n }, () => Attrs(Td(t) * n), true);
                Add(new() { ["k"] = "td_div", ["t"] = t, ["n"] = n }, () => Attrs(Td(t) / n));
            }
        }

        var isoStrings = new SCG.List<string>(NonIso);
        foreach (var d in IsoDates)
        {
            isoStrings.Add(d);
            foreach (var sep in IsoSeparators)
            {
                foreach (var time in IsoTimes)
                {
                    foreach (var offset in IsoOffsets)
                        isoStrings.Add(d + sep + time + offset);
                }
            }
        }
        foreach (var s in isoStrings.Distinct(StringComparer.Ordinal))
        {
            Add(new() { ["k"] = "date_iso", ["s"] = s }, () => Repr(Date.Fromisoformat(s)));
            Add(new() { ["k"] = "dt_iso", ["s"] = s }, () => Repr(DateTime.Fromisoformat(s)));
        }
        return cases;
    }
}
