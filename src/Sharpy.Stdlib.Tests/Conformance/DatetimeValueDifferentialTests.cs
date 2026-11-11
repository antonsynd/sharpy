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
        import json, struct, sys
        from datetime import date, datetime, timedelta, timezone

        # Sharpy's timedelta is a TimeSpan: |ticks| <= 2**63 - 1, at microsecond resolution.
        LIMIT = timedelta(microseconds=(2**63 - 1) // 10)

        def td(a):
            return timedelta(days=a[0], seconds=a[1], microseconds=a[2])

        def tdattrs(t):
            return "|".join([str(t.days), str(t.seconds), str(t.microseconds), str(t), repr(t), repr(t.total_seconds())])

        def text(units):
            # UTF-16 code units, so a lone surrogate survives the trip (JSON text would replace it).
            return struct.pack("<%dH" % len(units), *units).decode("utf-16-le", "surrogatepass")

        def iso(units):
            return datetime.fromisoformat(text(units))

        def typeerror(f):
            try:
                return f()
            except TypeError:
                return "!TypeError"

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
                    r = repr(date.fromisoformat(text(c["u"])))
                elif k == "dt_iso":
                    r = repr(datetime.fromisoformat(text(c["u"])))
                elif k == "dt_diff":
                    r = tdattrs(datetime(*c["a"]) - datetime(*c["b"]))
                elif k == "aw_pair":
                    a = iso(c["a"]); b = iso(c["b"])
                    r = "|".join([typeerror(lambda: tdattrs(a - b)), str(a == b), str(a != b),
                                  typeerror(lambda: str(a < b)), typeerror(lambda: str(a >= b)),
                                  str(hash(a) == hash(b)), typeerror(lambda: repr(sorted([a, b])))])
                elif k == "aw_op":
                    a = iso(c["a"]); t = td(c["t"])
                    r = repr(a + t if c["op"] == "+" else a - t)
                elif k == "aw_attrs":
                    a = iso(c["a"])
                    r = "|".join([repr(a.timestamp()), str(a.utcoffset())])
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
        "2020-01-01\uD83D\uDE0012:00", "2020-01-01\uD80012:00", "2020-01-01\uD800", "2020-01-01T12:00\u0000",
        "2020-01-01T12\u0000:00", "2020-01-01T12:00+05:30:15.1234567", "2020-01-01T12:00:00.1234567-01:00",
    };

    private readonly ITestOutputHelper _output;

    public DatetimeValueDifferentialTests(ITestOutputHelper output) => _output = output;

    private sealed record Case(Dictionary<string, object> Spec, Func<string> Sharpy, bool TimedeltaResult);

    [SkippableFact]
    public void DatetimeValues_MatchPython()
    {
        var cases = Cases();
        // 3.15's grammar: 3.12 and 3.13 refuse `24:00` and accept `12:30:45.Z`, so an older oracle skips.
        var python = PythonInterpreter.RequireOrSkip(15);
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

    /// <summary>Aware and naive instants: equal instants at different offsets, and the calendar's edges.</summary>
    private static readonly string[] Instants =
    {
        "2020-01-01T12:00+05:00", "2020-01-01T12:00Z", "2020-01-01T07:00+00:00", "2020-01-01T12:00",
        "2019-12-31T23:00-01:00", "2020-01-01T07:00", "0001-01-01T00:00+01:00", "9999-12-31T23:59-02:00",
    };

    private static int[] Units(string s) => s.Select(c => (int)c).ToArray();

    private static string Bool(bool b) => b ? "True" : "False";

    private static string TypeErrorOr(Func<string> f)
    {
        try
        {
            return f();
        }
        catch (TypeError)
        {
            return "!TypeError";
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
            Add(new() { ["k"] = "date_iso", ["u"] = Units(s) }, () => Repr(Date.Fromisoformat(s)));
            Add(new() { ["k"] = "dt_iso", ["u"] = Units(s) }, () => Repr(DateTime.Fromisoformat(s)));
        }

        foreach (var a in DateTimes)
        {
            foreach (var b in DateTimes)
            {
                Add(new() { ["k"] = "dt_diff", ["a"] = a, ["b"] = b }, () =>
                    Attrs(new DateTime(a[0], a[1], a[2], a[3], a[4], a[5], a[6]) - new DateTime(b[0], b[1], b[2], b[3], b[4], b[5], b[6])));
            }
        }

        // timestamp() of a naive value reads the host time zone, so the attributes are compared for aware values only.
        foreach (var a in Instants.Where(i => i.EndsWith('Z') || (i.Length > 16 && (i[16] == '+' || i[16] == '-'))))
        {
            Add(new() { ["k"] = "aw_attrs", ["a"] = Units(a) }, () =>
            {
                var dt = DateTime.Fromisoformat(a);
                return Repr(dt.Timestamp()) + "|" + dt.Tzinfo!.Utcoffset(dt);
            });
        }
        foreach (var a in Instants)
        {
            foreach (var b in Instants)
            {
                Add(new() { ["k"] = "aw_pair", ["a"] = Units(a), ["b"] = Units(b) }, () =>
                {
                    var x = DateTime.Fromisoformat(a);
                    var y = DateTime.Fromisoformat(b);
                    return string.Join("|", TypeErrorOr(() => Attrs(x - y)), Bool(x == y), Bool(x != y),
                        TypeErrorOr(() => Bool(x < y)), TypeErrorOr(() => Bool(x >= y)), Bool(x.GetHashCode() == y.GetHashCode()),
                        TypeErrorOr(() => Repr(Sorted(new List<DateTime> { x, y }))));
                });
            }
            foreach (var t in new[] { Deltas[3], Deltas[4], Deltas[9], Deltas[17] })
            {
                foreach (var op in new[] { "+", "-" })
                {
                    Add(new() { ["k"] = "aw_op", ["a"] = Units(a), ["t"] = t, ["op"] = op }, () =>
                    {
                        var dt = DateTime.Fromisoformat(a);
                        return Repr(op == "+" ? dt + Td(t) : dt - Td(t));
                    });
                }
            }
        }
        return cases;
    }
}
