using System.Text;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// Evaluates <c>docs/design/nullability-ledger.tsv</c>, the cell table of <c>docs/design/nullability.md</c>
/// (owner ruling 2026-11-12): what <c>T | None</c> (loose) and <c>T?</c> (strict) do at each use.
///
/// <para>Every row is regenerated on each run: the slot × use × value program is run by CPython and by Sharpy,
/// the target is derived from the rules (<see cref="Target"/>), and the status from the two. The regenerated
/// table must equal the committed one byte for byte, so the ledger cannot drift in either direction: a gap that
/// closes and a matching cell that regresses are both red until the row is updated, and the diff of the ledger
/// is the record of what moved. The regenerated table is written to <c>.claude/tmp</c> on every run.</para>
///
/// <para>Skips when no python3 ≥ 3.15 answers, except under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI).</para>
/// </summary>
public class NullabilityLedgerTests : IntegrationTestBase
{
    public NullabilityLedgerTests(ITestOutputHelper output) : base(output) => MuteCompilePath = true;

    private const string LedgerFileName = "nullability-ledger.tsv";

    private enum Payload { Int, Str, K, List }

    private sealed record Slot(string Name, Payload Payload, bool Strict)
    {
        public string Annotation => Name;
        public string PayloadType => TypeName[Payload];
    }

    private static readonly Dictionary<Payload, string> TypeName = new()
    {
        [Payload.Int] = "int",
        [Payload.Str] = "str",
        [Payload.K] = "K",
        [Payload.List] = "list[int]",
    };

    /// <summary>Per payload: a value, a second value for <c>??</c>, and the falsy value (none for <c>K</c>, which
    /// has no falsy case).</summary>
    private static readonly Dictionary<Payload, (string Value, string Default, string? Falsy)> Values = new()
    {
        [Payload.Int] = ("5", "0", "0"),
        [Payload.Str] = ("\"ab\"", "\"\"", "\"\""),
        [Payload.K] = ("K(5)", "K(0)", null),
        [Payload.List] = ("[5]", "[0]", "[]"),
    };

    private static IEnumerable<Slot> Slots() =>
        Enum.GetValues<Payload>().Select(p => new Slot(TypeName[p] + " | None", p, Strict: false))
            .Concat(Enum.GetValues<Payload>().Select(p => new Slot(TypeName[p] + "?", p, Strict: true)));

    /// <summary>How the rules class a use on a <c>T | None</c>: always allowed, allowed with a warning and
    /// CPython's failure on None, or a store into a plain <c>T</c> (warning, TypeError on None).</summary>
    private enum Rule { Safe, Unsafe, Boundary, Truthiness }

    /// <summary>A use: its rule class, whether a <c>T?</c> refuses it, and its body per payload as (Sharpy, python).
    /// A payload with no entry does not have the use (<c>-x</c> on a <c>str</c>).</summary>
    private sealed record Use(string Name, Rule Rule, bool StrictRefuses, Dictionary<Payload, (string Sharpy, string Python)> Body);

    private static Dictionary<Payload, (string, string)> All(string sharpy, string? python = null) =>
        Enum.GetValues<Payload>().ToDictionary(p => p, _ => (sharpy, python ?? sharpy));

    private static readonly Use[] Uses =
    {
        new("print", Rule.Safe, false, All("print(x)")),
        new("fstring", Rule.Safe, false, All("print(f\"{x}\")")),
        new("str()", Rule.Safe, false, All("print(str(x))")),
        new("is_None", Rule.Safe, false, All("print(x is None)")),
        new("narrow", Rule.Safe, false, All("if x is not None:\n    print(take(x))\nelse:\n    print(\"none\")")),
        new("coalesce", Rule.Safe, false, All("print(take(x ?? d))", "print(take(x if x is not None else d))")),
        new("truthy", Rule.Truthiness, false, All("print(\"t\" if x else \"f\")")),
        new("bool()", Rule.Truthiness, false, All("print(bool(x))")),
        new("not", Rule.Truthiness, false, All("print(not x)")),
        new("eq_None", Rule.Safe, true, All("print(x == None)")),
        new("eq_value", Rule.Safe, true, new()
        {
            [Payload.Int] = ("print(x == 5)", "print(x == 5)"),
            [Payload.Str] = ("print(x == \"ab\")", "print(x == \"ab\")"),
            [Payload.K] = ("print(x == K(5))", "print(x == K(5))"),
            [Payload.List] = ("print(x == [5])", "print(x == [5])"),
        }),
        new("in_list", Rule.Safe, true, new()
        {
            [Payload.Int] = ("print(x in [5, 6])", "print(x in [5, 6])"),
            [Payload.Str] = ("print(x in [\"ab\"])", "print(x in [\"ab\"])"),
            [Payload.K] = ("print(x in [K(5)])", "print(x in [K(5)])"),
        }),
        new("dict_key", Rule.Safe, false, new()
        {
            [Payload.Int] = ("m = {x: 1}\nprint(len(m))", "m = {x: 1}\nprint(len(m))"),
            [Payload.Str] = ("m = {x: 1}\nprint(len(m))", "m = {x: 1}\nprint(len(m))"),
            [Payload.K] = ("m = {x: 1}\nprint(len(m))", "m = {x: 1}\nprint(len(m))"),
        }),
        new("qdot", Rule.Safe, false, new()
        {
            [Payload.Int] = ("print(x?.to_string())", "print(None if x is None else x.numerator)"),
            [Payload.Str] = ("print(x?.upper())", "print(None if x is None else x.upper())"),
            [Payload.K] = ("print(x?.v)", "print(None if x is None else x.v)"),
            [Payload.List] = ("print(x?.count(5))", "print(None if x is None else x.count(5))"),
        }),
        new("member", Rule.Unsafe, true, new()
        {
            [Payload.Int] = ("print(x.to_string())", "print(x.numerator)"),
            [Payload.Str] = ("print(x.upper())", "print(x.upper())"),
            [Payload.K] = ("print(x.v)", "print(x.v)"),
            [Payload.List] = ("print(x.count(5))", "print(x.count(5))"),
        }),
        new("lt", Rule.Unsafe, true, new()
        {
            [Payload.Int] = ("print(x < 9)", "print(x < 9)"),
            [Payload.Str] = ("print(x < \"z\")", "print(x < \"z\")"),
            [Payload.K] = ("print(x < K(9))", "print(x < K(9))"),
        }),
        new("arith", Rule.Unsafe, true, new()
        {
            [Payload.Int] = ("print(x + 1)", "print(x + 1)"),
            [Payload.Str] = ("print(x + \"c\")", "print(x + \"c\")"),
            [Payload.K] = ("print(x + K(1))", "print(x + K(1))"),
            [Payload.List] = ("print(x + [1])", "print(x + [1])"),
        }),
        new("neg", Rule.Unsafe, true, new()
        {
            [Payload.Int] = ("print(-x)", "print(-x)"),
            [Payload.K] = ("print(-x)", "print(-x)"),
        }),
        new("len", Rule.Unsafe, true, new()
        {
            [Payload.Str] = ("print(len(x))", "print(len(x))"),
            [Payload.List] = ("print(len(x))", "print(len(x))"),
        }),
        new("for", Rule.Unsafe, true, new()
        {
            [Payload.Str] = ("for c in x:\n    print(c)", "for c in x:\n    print(c)"),
            [Payload.List] = ("for c in x:\n    print(c)", "for c in x:\n    print(c)"),
        }),
        new("index", Rule.Unsafe, true, new()
        {
            [Payload.Str] = ("print(x[0])", "print(x[0])"),
            [Payload.List] = ("print(x[0])", "print(x[0])"),
        }),
        new("pass_T", Rule.Boundary, true, All("print(take(x))")),
        new("store_T", Rule.Boundary, true, new()
        {
            [Payload.Int] = ("y: int = x\nprint(y)", "y = x\nprint(y)"),
            [Payload.Str] = ("y: str = x\nprint(y)", "y = x\nprint(y)"),
            [Payload.K] = ("y: K = x\nprint(y)", "y = x\nprint(y)"),
            [Payload.List] = ("y: list[int] = x\nprint(y)", "y = x\nprint(y)"),
        }),
    };

    private enum ValueKind { Value, Falsy, None }

    private sealed record Cell(Slot Slot, Use Use, ValueKind Value)
    {
        public string ValueName => Value switch { ValueKind.Value => "value", ValueKind.Falsy => "falsy", _ => "none" };
    }

    /// <summary>Slot × use × {a value, None}, plus the falsy payload for the truthiness uses.</summary>
    private static List<Cell> GenerateCells()
    {
        var cells = new List<Cell>();
        foreach (var slot in Slots())
            foreach (var use in Uses.Where(u => u.Body.ContainsKey(slot.Payload)))
            {
                cells.Add(new Cell(slot, use, ValueKind.Value));
                if (use.Rule == Rule.Truthiness && Values[slot.Payload].Falsy != null)
                    cells.Add(new Cell(slot, use, ValueKind.Falsy));
                cells.Add(new Cell(slot, use, ValueKind.None));
            }
        return cells;
    }

    private const string SharpyK =
        "class K:\n    v: int\n    def __init__(self, v: int):\n        self.v = v\n"
        + "    def __eq__(self, o: object) -> bool:\n        return isinstance(o, K) and o.v == self.v\n"
        + "    def __lt__(self, o: K) -> bool:\n        return self.v < o.v\n"
        + "    def __add__(self, o: K) -> K:\n        return K(self.v + o.v)\n"
        + "    def __neg__(self) -> K:\n        return K(-self.v)\n"
        + "    def __hash__(self) -> int:\n        return self.v\n"
        + "    def __str__(self) -> str:\n        return f\"K({self.v})\"\n";

    private const string PythonK =
        "class K:\n    def __init__(self, v):\n        self.v = v\n"
        + "    def __eq__(self, o):\n        return isinstance(o, K) and o.v == self.v\n"
        + "    def __lt__(self, o):\n        return self.v < o.v\n"
        + "    def __add__(self, o):\n        return K(self.v + o.v)\n"
        + "    def __neg__(self):\n        return K(-self.v)\n"
        + "    def __hash__(self):\n        return self.v\n"
        + "    def __str__(self):\n        return f\"K({self.v})\"\n";

    private static string Indent(string body) => "    " + body.Replace("\n", "\n    ", StringComparison.Ordinal);

    private static string SharpyProgram(Cell cell)
    {
        var (value, fallback, falsy) = Values[cell.Slot.Payload];
        var payload = cell.Value == ValueKind.Falsy ? falsy! : value;
        var argument = cell.Value == ValueKind.None
            ? (cell.Slot.Strict ? "None()" : "None")
            : (cell.Slot.Strict ? $"Some({payload})" : payload);
        var t = cell.Slot.PayloadType;
        return SharpyK
            + $"def take(x: {t}) -> str:\n    return \"took\"\n"
            + $"def run(x: {cell.Slot.Annotation}, d: {t}) -> None:\n{Indent(cell.Use.Body[cell.Slot.Payload].Sharpy)}\n"
            + $"def main() -> None:\n    run({argument}, {fallback})\n";
    }

    private static string PythonProgram(Cell cell)
    {
        var (value, fallback, falsy) = Values[cell.Slot.Payload];
        var argument = cell.Value switch { ValueKind.None => "None", ValueKind.Falsy => falsy!, _ => value };
        return PythonK
            + "def take(x):\n    return \"took\"\n"
            + $"def run(x, d):\n{Indent(cell.Use.Body[cell.Slot.Payload].Python)}\n"
            + $"run({argument}, {fallback})\n";
    }

    private static string Printed(string stdout) =>
        "[" + string.Join(" ", stdout.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) + "]";

    private static readonly Regex ExceptionName = new(@"\b([A-Za-z_][A-Za-z0-9_.]*(?:Error|Exception))\b", RegexOptions.Compiled);

    private static string Raised(string text)
    {
        // The last exception named is the one that escaped (a traceback names it on its final line).
        var matches = ExceptionName.Matches(text);
        return "!" + (matches.Count == 0 ? "?" : matches[^1].Groups[1].Value.Split('.')[^1]);
    }

    private static string RaisedBySharpy(string text)
    {
        var unhandled = Regex.Match(text, @"Unhandled exception\. ([A-Za-z0-9_.]+)");
        return unhandled.Success ? "!" + unhandled.Groups[1].Value.Split('.')[^1] : Raised(text);
    }

    private string Observe(Cell cell)
    {
        var run = CompileAndExecute(SharpyProgram(cell), "nullability_ledger.spy", executionTimeoutMs: 120_000);
        var warning = run.RawDiagnostics.Any(d => d.IsWarning) ? " + warning" : "";
        if (RanToCompletion(run))
            return Printed(run.StandardOutput) + warning;
        var codes = run.RawDiagnostics.Where(d => d.IsError).Select(d => d.Code ?? "?").Distinct()
            .OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (codes.Count > 0)
            return "refused " + string.Join(",", codes);
        if (run.TimedOut)
            return "timed out";
        return RaisedBySharpy(run.StandardError + "\n" + run.Exception?.Message) + warning;
    }

    /// <summary>The rules of docs/design/nullability.md as a function of the cell and CPython's answer.</summary>
    private static string Target(Cell cell, string python)
    {
        if (cell.Slot.Strict)
        {
            if (cell.Use.StrictRefuses)
                return "refused";
            if (cell.Use.Rule == Rule.Truthiness)
            {
                // A T? is true when it is Some, whatever the payload.
                var some = cell.Value != ValueKind.None;
                return cell.Use.Name switch
                {
                    "truthy" => some ? "[t]" : "[f]",
                    "bool()" => some ? "[True]" : "[False]",
                    _ => some ? "[False]" : "[True]",
                };
            }
            return python;
        }

        return cell.Use.Rule switch
        {
            Rule.Safe or Rule.Truthiness => python,
            Rule.Unsafe => python + " + warning",
            _ => (cell.Value == ValueKind.None ? "!TypeError" : python) + " + warning",
        };
    }

    private static string Status(string observed, string target)
    {
        var crashed = observed.Contains(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, StringComparison.Ordinal);
        if (target == "refused")
            return crashed ? "gap: compiler crash" : observed.StartsWith("refused ", StringComparison.Ordinal) ? "ok" : "gap: not refused";
        if (observed == target)
            return "ok";
        if (crashed)
            return "gap: compiler crash";
        if (observed.StartsWith("refused ", StringComparison.Ordinal))
            return "gap: refused today";
        if (observed + " + warning" == target)
            return "gap: no warning";
        var raises = target.StartsWith('!');
        if (observed.StartsWith('!'))
            return raises ? "gap: wrong exception" : "gap: throws";
        return raises ? "gap: no raise" : "gap: wrong value";
    }

    private const string Header =
        "# The cell table of docs/design/nullability.md. Generated and checked by NullabilityLedgerTests: do not edit by\n"
        + "# hand. When the test is red, read the diff against .claude/tmp/nullability-ledger.proposed.tsv, and copy that\n"
        + "# file here only if every changed row is intended (a gap that closed, never a matching cell that regressed).\n"
        + "# [..] is printed output, !Name an exception, 'refused SPY…' a compile-time refusal.\n"
        + "slot\tuse\tvalue\tcpython\tsharpy\ttarget\tstatus\n";

    [SkippableFact]
    public void Ledger_IsWhatCPythonAndSharpyAnswerToday()
    {
        // The oracle minor is the repository's .python-version pin (3.15).
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 15));

        // Every rule class is one the target function decides, and every use has a body for some payload.
        Assert.All(Uses, u => Assert.NotEmpty(u.Body));
        Assert.Equal(Uses.Length, Uses.Select(u => u.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Enum.GetValues<Rule>().OrderBy(r => r), Uses.Select(u => u.Rule).Distinct().OrderBy(r => r));

        var cells = GenerateCells();
        var py = oracle.RunBatch(cells.Select((c, i) => new PyRequest(i, PythonProgram(c))).ToList());

        var proposed = new StringBuilder(Header);
        var gaps = 0;
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            Assert.True(py.TryGetValue(i, out var result) && !result.TimedOut && !result.SyntaxError,
                $"python3 did not run {cell.Slot.Name}/{cell.Use.Name}/{cell.ValueName}: " + (result?.Stderr ?? "no verdict"));
            var python = result!.Ok ? Printed(result.Stdout) : Raised(result.Stderr);
            var observed = Observe(cell);
            var target = Target(cell, python);
            var status = Status(observed, target);
            if (status != "ok")
                gaps++;
            proposed.Append(string.Join("\t", cell.Slot.Name, cell.Use.Name, cell.ValueName, python, observed, target, status)).Append('\n');
        }
        Output.WriteLine($"{cells.Count} cells, {gaps} gaps");

        var proposedText = proposed.ToString();
        var reportPath = Path.Combine(ReportDirectory(), "nullability-ledger.proposed.tsv");
        File.WriteAllText(reportPath, proposedText);

        var ledgerPath = PythonInterpreter.FindRepoFile("docs", "design", LedgerFileName);
        Assert.True(ledgerPath != null, $"docs/design/{LedgerFileName} was not found");
        var committed = File.ReadAllText(ledgerPath!).ReplaceLineEndings("\n");

        static Dictionary<string, string> Rows(string text) => text.Split('\n')
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith("slot\t", StringComparison.Ordinal))
            .ToDictionary(l => string.Join("/", l.Split('\t').Take(3)), l => l, StringComparer.Ordinal);
        var before = Rows(committed);
        var after = Rows(proposedText);
        var problems = new List<string>();
        foreach (var (key, row) in after)
        {
            if (!before.TryGetValue(key, out var old))
                problems.Add($"{key}: not in the ledger: {row}");
            else if (old != row)
                problems.Add($"{key}: the ledger says\n    {old}\n  today it is\n    {row}");
        }
        problems.AddRange(before.Keys.Where(k => !after.ContainsKey(k)).Select(k => $"{k}: in the ledger, but no longer generated"));

        Assert.True(problems.Count == 0 && committed == proposedText,
            $"docs/design/{LedgerFileName} is not what runs today ({problems.Count} row(s)); the regenerated table is at "
            + $"{reportPath}:\n" + string.Join("\n", problems.Take(40)));
    }

    private static string ReportDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;
        var tmp = Path.Combine(dir?.FullName ?? Path.GetTempPath(), ".claude", "tmp");
        Directory.CreateDirectory(tmp);
        return tmp;
    }
}
