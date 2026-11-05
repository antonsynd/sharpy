using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Differential;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>What the python twin of a cell is predicted to do (D2): computed from the axes, never read back from python.</summary>
public enum PyKind
{
    Ran,
    NameError,
    UnboundLocalError,
    OtherError,
    SyntaxError,
    TimedOut,
}

/// <summary>A python outcome. <see cref="Value"/> is the stdout for <see cref="PyKind.Ran"/>, the name for the two name
/// errors and the exception type for <see cref="PyKind.OtherError"/>.</summary>
internal sealed record PyOutcome(PyKind Kind, string Value)
{
    public static PyOutcome Ran(object stdout) => new(PyKind.Ran, Convert.ToString(stdout, CultureInfo.InvariantCulture)!);
    public static PyOutcome Name(string name) => new(PyKind.NameError, name);
    public static PyOutcome Unbound(string name) => new(PyKind.UnboundLocalError, name);
    public static PyOutcome Other(string type) => new(PyKind.OtherError, type);
    public override string ToString() => $"{Kind}({Value})";
}

/// <summary>The Sharpy tag of a cell (D2's table). Set by <c>Expect</c>, never per cell.</summary>
internal enum SharpyTag
{
    MustRun,
    MustRefuse,
    MustRefuseOrThrow,
    MayRefuse,
    Unruled,
}

internal enum Family
{
    Read,
    Store,
    TypeMember,
    Local,
    Seed,
}

/// <summary>The Sharpy outcome classes of D2.</summary>
internal enum SharpyKind
{
    Ran,
    Refused,
    Ice,
    RuntimeThrow,
    TimedOut,
    Harness,
}

/// <summary>The verdict classes of D2. Red = FalseRefusal, SilentWrong, RuntimeThrow, Ice when not allowlisted.</summary>
internal enum VerdictClass
{
    Match,
    RefuseOk,
    Unruled,
    FalseRefusal,
    SilentWrong,
    RuntimeThrow,
    Ice,
    GeneratorBug,
    Harness,
}

/// <summary>One generated cell: one Sharpy program, its twin (rendered from the source) and both predictions.</summary>
internal sealed record NameCell
{
    public required string Id { get; init; }
    public required Family Family { get; init; }
    public required string Source { get; init; }
    public required SharpyTag Tag { get; init; }

    /// <summary>The stdout a <see cref="SharpyTag.MustRun"/> cell must print.</summary>
    public string? Model { get; init; }
    public required PyOutcome Python { get; init; }

    /// <summary>The rule the tag cites (N1…N7, S1…S3, R-Y, R-KG, …).</summary>
    public required string Rule { get; init; }

    /// <summary>A <c>docs/deviations.yaml</c> row id when the python prediction differs from the model by design.</summary>
    public string? Deviation { get; init; }

    // Axes the closed-set assertions and the roster-fed assertions read.
    public BindingKind Target { get; init; } = BindingKind.None;
    public BindingKind Outer { get; init; } = BindingKind.None;
    public ReadPosition? Position { get; init; }
    public StoreForm? Form { get; init; }
    public LocalTarget? Construct { get; init; }
    public string? Shape { get; init; }

    /// <summary>Set by SEED cells: the hand twin's text, when the cell has one. Null means "render the source".</summary>
    public string? HandTwin { get; init; }

    /// <summary>The Sharpy source the twin renders from when python has no spelling of the construct (a module accessor).</summary>
    public string? TwinSource { get; init; }

    /// <summary>Set by SEED cells: the table's issue cite for a red.</summary>
    public string? SeedCite { get; init; }

    /// <summary>The inventory's python verdict, compared in the runner's normal form instead of <see cref="Python"/>.</summary>
    public string? SeedPythonVerdict { get; init; }
}

public partial class NameResolutionDifferentialTests
{
    private const int SharpyExecTimeoutMs = 12_000;
    private const int PythonBatchTimeoutMs = 3_600_000;
    private const string AllowlistFileName = "name-resolution-differential-allowlist.txt";
    private const string StrideEnv = "NAME_RES_DIFF_LIMIT";

    /// <summary>D3's budget: the pool's size, with the 13-minute wall as its other half.</summary>
    private const int CellBudget = 6500;

    /// <summary>Edit-loop only: a JSON file of Sharpy arm results keyed by source hash. A run that reads it prints the
    /// CACHED banner and is never a gate: a cached verdict cannot see a compiler change.</summary>
    private const string CacheEnv = "NAME_RES_DIFF_SHARPY_CACHE";

    /// <summary>D2's ICE set: any of these in <c>RawDiagnostics</c> is an internal failure, never a refusal.</summary>
    internal static readonly HashSet<string> IceCodes = new(StringComparer.Ordinal)
    {
        "SPY0904", "SPY0906", "SPY0907", "SPY0908", "SPY0909", "SPY0510",
    };

    /// <summary>Infrastructure failures: the harness could not judge the program. SPY0901 is cancellation-only and
    /// SPY0912 is formatter-only; neither can surface under <c>CompileAndExecute</c>.</summary>
    private static readonly HashSet<string> InfrastructureCodes = new(StringComparer.Ordinal)
    {
        "SPY0902", "SPY0903", "SPY0910", "SPY0911", "SPY0913",
    };

    /// <summary>Summary rows that never classify: SPY0905 sits beside real errors, SPY0900 is the REPL's.</summary>
    private static readonly HashSet<string> IgnoredCodes = new(StringComparer.Ordinal) { "SPY0905", "SPY0900" };

    private static readonly Regex NameErrorLine = new(@"^NameError: name '(\w+)' is not defined", RegexOptions.CultureInvariant);
    private static readonly Regex FreeVariableLine = new(@"^NameError: cannot access free variable '(\w+)'", RegexOptions.CultureInvariant);
    private static readonly Regex UnboundLine = new(@"^UnboundLocalError: cannot access local variable '(\w+)'", RegexOptions.CultureInvariant);
    private static readonly Regex ExceptionTypeLine = new(@"^([A-Za-z_][\w.]*)(?::|$)", RegexOptions.CultureInvariant);
    private static readonly Regex IssueCite = new(@"#\d+", RegexOptions.CultureInvariant);

    /// <summary>What the Sharpy arm observed, the only input of <see cref="ClassifySharpy"/>. It is plain data so an edit
    /// loop can cache it (<see cref="CacheEnv"/>); the classifier still runs over it on every run.</summary>
    internal sealed record SharpyRun(
        bool Success,
        int? ExitCode,
        bool ThrewInHarness,
        bool TimedOut,
        string Stdout,
        string Stderr,
        IReadOnlyList<string> ErrorCodes,
        IReadOnlyList<string> AllCodes,
        string? CsCode);

    /// <summary>D2's Sharpy outcome: reads codes AND the exit path. A run-time throw is read positively from
    /// <c>ExitCode</c> and any other combination is Harness, never a verdict.</summary>
    internal static SharpyKind ClassifySharpy(SharpyRun run)
    {
        if (run.TimedOut)
            return SharpyKind.TimedOut;
        if (run.ThrewInHarness || run.AllCodes.Any(IceCodes.Contains))
            return SharpyKind.Ice;
        if (run.AllCodes.Any(InfrastructureCodes.Contains))
            return SharpyKind.Harness;
        if (run.ErrorCodes.Any(c => !IgnoredCodes.Contains(c)))
            return SharpyKind.Refused;
        if (run.ExitCode == 0 && run.Success)
            return SharpyKind.Ran;
        if (run.ExitCode is int code && code != 0)
            return SharpyKind.RuntimeThrow;
        return SharpyKind.Harness;
    }

    /// <summary>D2's python outcome: kind and name from the LAST stderr line, prefix-anchored because python 3.12
    /// appends a <c>Did you mean</c> suggestion to the same line.</summary>
    internal static PyOutcome ClassifyPython(PyResult result)
    {
        if (result.TimedOut)
            return new PyOutcome(PyKind.TimedOut, "");
        if (result.SyntaxError)
            return new PyOutcome(PyKind.SyntaxError, LastLine(result.Stderr));
        if (result.Ok)
            return PyOutcome.Ran(Normalize(result.Stdout));

        var last = LastLine(result.Stderr);
        var match = NameErrorLine.Match(last);
        if (match.Success)
            return PyOutcome.Name(match.Groups[1].Value);
        match = FreeVariableLine.Match(last);
        if (match.Success)
            return PyOutcome.Name(match.Groups[1].Value);
        match = UnboundLine.Match(last);
        if (match.Success)
            return PyOutcome.Unbound(match.Groups[1].Value);
        match = ExceptionTypeLine.Match(last);
        return PyOutcome.Other(match.Success ? match.Groups[1].Value : last);
    }

    private static string LastLine(string text)
        => text.Split('\n').Select(l => l.TrimEnd()).LastOrDefault(l => l.Length > 0) ?? "";

    internal static string Normalize(string stdout)
        => string.Join("\n", stdout.Replace("\r\n", "\n").TrimEnd().Split('\n').Select(l => l.TrimEnd()));

    /// <summary>D2's table: tag × Sharpy outcome → verdict class, the python control already passed.</summary>
    internal static VerdictClass Judge(NameCell cell, SharpyKind kind, string stdout)
    {
        if (kind is SharpyKind.TimedOut or SharpyKind.Harness)
            return VerdictClass.Harness;
        if (kind == SharpyKind.Ice)
            return VerdictClass.Ice;

        switch (cell.Tag)
        {
            case SharpyTag.Unruled:
                // The question is what the program should mean, and no answer to it is an uncaught CLR exception.
                return kind == SharpyKind.RuntimeThrow ? VerdictClass.RuntimeThrow : VerdictClass.Unruled;
            case SharpyTag.MustRun:
                return kind switch
                {
                    SharpyKind.Ran => Normalize(stdout) == cell.Model ? VerdictClass.Match : VerdictClass.SilentWrong,
                    SharpyKind.Refused => VerdictClass.FalseRefusal,
                    _ => VerdictClass.RuntimeThrow,
                };
            case SharpyTag.MustRefuse:
                return kind switch
                {
                    SharpyKind.Refused => VerdictClass.Match,
                    SharpyKind.Ran => VerdictClass.SilentWrong,
                    _ => VerdictClass.RuntimeThrow,
                };
            case SharpyTag.MustRefuseOrThrow:
                return kind == SharpyKind.Ran ? VerdictClass.SilentWrong : VerdictClass.Match;
            case SharpyTag.MayRefuse:
                return kind switch
                {
                    SharpyKind.Refused => VerdictClass.RefuseOk,
                    SharpyKind.Ran => Normalize(stdout) == cell.Model ? VerdictClass.Match : VerdictClass.SilentWrong,
                    _ => VerdictClass.RuntimeThrow,
                };
            default:
                throw new InvalidOperationException($"Unhandled tag {cell.Tag}");
        }
    }

    /// <summary>The python control (D2): a python outcome the cell did not predict is a generator bug, never allowlistable.</summary>
    private static string? PythonControlFailure(NameCell cell, PyOutcome observed)
    {
        if (cell.SeedPythonVerdict != null)
            return null; // the seed's own control runs in TwinRenderer_SeedPythonVerdicts_Reproduce
        if (cell.Tag == SharpyTag.Unruled)
            return observed.Kind is PyKind.SyntaxError or PyKind.TimedOut ? $"unruled twin: {observed}" : null;
        if (observed != cell.Python)
            return $"predicted {cell.Python}, python {observed}";

        // Acceptance 3: where python runs a must-run program, python and the model agree. Only a named deviation may differ.
        if (cell.Tag is SharpyTag.MustRun or SharpyTag.MayRefuse && observed.Kind == PyKind.Ran
            && observed.Value != cell.Model && cell.Deviation == null)
            return $"model '{cell.Model}' differs from python's '{observed.Value}' and the cell names no deviation";
        return null;
    }

    /// <summary>A rule that names a code. A must-refuse cell refused under another code is the right direction for the wrong
    /// reason, so it is read here: a code drifting (SPY0290 to SPY0200) stays visible, which the verdict class cannot show.</summary>
    private static readonly Regex RuleNamesCode = new(@"^(?:kind:)?(SPY\d{4})$", RegexOptions.CultureInvariant);

    /// <summary>The refusals that fire before the rule's own code today, with their counts. Anything else is red.</summary>
    private static readonly IReadOnlyDictionary<(string Rule, string Seen), int> EarlierRefusals = new Dictionary<(string, string), int>
    {
        // `global x` over a const: SPY0134 refuses the declaration until #2424 reinstates it, then SPY0225 is reached.
        [("kind:SPY0225", "SPY0134")] = 6,
        // A later member in a constant-only initializer: the name is not visible yet, so SPY0401 is not reached.
        [("SPY0401", "SPY0200")] = 9,
        // `x += 1` on a method's name in a method body: the undefined local is reported, so SPY0606 is not reached.
        [("SPY0606", "SPY0200")] = 4,
    };

    private static IEnumerable<string> RefusalCodeFailures(IReadOnlyList<CellResult> results, bool partial)
    {
        var seen = new Dictionary<(string Rule, string Seen), int>();
        foreach (var r in results.Where(r => r.Kind == SharpyKind.Refused))
        {
            var named = RuleNamesCode.Match(r.Cell.Rule);
            if (!named.Success || r.Run.ErrorCodes.Contains(named.Groups[1].Value))
                continue;
            var first = r.Run.ErrorCodes.First(c => !IgnoredCodes.Contains(c));
            if (!EarlierRefusals.ContainsKey((r.Cell.Rule, first)))
                yield return $"RefusalCode: {r.Cell.Id} names {r.Cell.Rule} and was refused with {first}";
            seen[(r.Cell.Rule, first)] = seen.GetValueOrDefault((r.Cell.Rule, first)) + 1;
        }
        if (partial)
            yield break;
        foreach (var (key, count) in EarlierRefusals.Where(e => seen.GetValueOrDefault(e.Key) != e.Value))
            yield return $"RefusalCode: {key.Rule} refused with {key.Seen} is pinned at {count} cells and measured {seen.GetValueOrDefault(key)}";
    }

    /// <summary>A row of the allowlist: the class it pins, the issue that drains it and its lane.</summary>
    internal sealed record AllowRow(VerdictClass Class, string Cite);

    /// <summary>D4's loader, in the shape of <c>TypeDefinitionOwnSymbolMatrixTests.LoadAllowlist</c>: a row names a
    /// generated cell, names a red class, cites an issue, appears once. No globs.</summary>
    internal static Dictionary<string, AllowRow> LoadAllowlist(IReadOnlySet<string> universe, out List<string> problems)
    {
        problems = new List<string>();
        var path = PythonInterpreter.FindRepoFile("src", "Sharpy.Compiler.Tests", "Conformance", AllowlistFileName)
            ?? throw new InvalidOperationException($"Conformance/{AllowlistFileName} is missing. Its presence is what arms the ratchet.");
        var rows = new Dictionary<string, AllowRow>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var split = line.IndexOf(" #", StringComparison.Ordinal);
            var head = split < 0 ? line : line[..split].Trim();
            var cite = split < 0 ? "" : line[(split + 2)..].Trim();
            var space = head.LastIndexOf(' ');
            if (space <= 0 || !Enum.TryParse<VerdictClass>(head[(space + 1)..], out var cls)
                || cls is not (VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow))
            {
                problems.Add($"'{line}' must be '<cell-id> <Ice|FalseRefusal|SilentWrong|RuntimeThrow> # #<issue> <lane>'");
                continue;
            }
            var id = head[..space].Trim();
            if (!IssueCite.IsMatch(cite))
                problems.Add($"'{line}' must cite an issue (#N)");
            if (!universe.Contains(id))
                problems.Add($"'{id}' names no generated cell");
            if (!rows.TryAdd(id, new AllowRow(cls, cite)))
                problems.Add($"'{id}' is listed twice");
        }
        return rows;
    }

    /// <summary>The row a red cell would carry (D4): the IMPLEMENTING issue of its class, whose fix turns it green.</summary>
    internal static string ProposedCite(NameCell cell, VerdictClass verdict, SharpyRun run)
    {
        if (cell.SeedCite != null)
            return $"{cell.SeedCite} {Lane(cell, verdict)}";
        return $"{ImplementingIssue(cell, verdict, run)} {Lane(cell, verdict)}";
    }

    private static string Lane(NameCell cell, VerdictClass verdict) => verdict switch
    {
        VerdictClass.Ice => "ice",
        VerdictClass.FalseRefusal => "false-refusal",
        VerdictClass.RuntimeThrow => "runtime-throw",
        VerdictClass.SilentWrong when cell.Tag == SharpyTag.MustRefuse => "missing-refusal",
        _ => "silent-wrong",
    };

    private static string ImplementingIssue(NameCell cell, VerdictClass verdict, SharpyRun run)
    {
        switch (verdict)
        {
            case VerdictClass.Ice:
                if (run.AllCodes.Contains("SPY0909"))
                    return "#2398";
                return run.CsCode switch
                {
                    "CS0176" => "#2147",
                    // Checked first: `x().v` through `type x = T` is CS0103 in a plain main, with no store, lambda or order in it.
                    "CS0103" when IsAliasConstructorCell(cell) => "#1925",
                    "CS0103" when cell.Family == Family.Store => "#2151",
                    "CS0103" when cell.Source.Contains("lambda", StringComparison.Ordinal) => "#2420",
                    _ => cell.Family switch
                    {
                        Family.TypeMember => "#2249",
                        Family.Store => cell.Rule.StartsWith("N6", StringComparison.Ordinal) && !cell.Id.Contains("Builtin", StringComparison.Ordinal) ? "#2424" : "#2423",
                        _ => "#2423",
                    },
                };
            case VerdictClass.FalseRefusal:
                if (cell.Family == Family.TypeMember)
                    return "#2249";
                if (cell.Family == Family.Store)
                    return cell.Form == StoreForm.Walrus && !run.AllCodes.Contains("SPY0134") ? "#2423" : "#2424";
                return cell.Position == ReadPosition.ModuleInitializer && cell.Rule.StartsWith("N3", StringComparison.Ordinal)
                    ? "#2422"
                    : "#2423";
            case VerdictClass.RuntimeThrow:
                return "#2422";
            case VerdictClass.SilentWrong:
                if (cell.Rule == "N3-cycle" && cell.Id.Contains("/Alias/", StringComparison.Ordinal))
                    return "#2412";
                if (cell.Family == Family.TypeMember)
                    return "#2249";
                if (cell.Family == Family.Store)
                    return "#2424";
                if (cell.Family == Family.Local)
                    return cell.Rule.StartsWith("S2", StringComparison.Ordinal) ? "#2425" : "#2423";
                // Only a read with no other binding can print a storage default; values-ready cannot change which binding is read.
                return cell.Tag == SharpyTag.MustRefuse || cell.Outer != BindingKind.None ? "#2423" : "#2422";
            default:
                throw new InvalidOperationException($"{verdict} is not a red class");
        }
    }

    private static bool IsAliasConstructorCell(NameCell cell)
        => cell.Target == BindingKind.ModuleTypeAlias || cell.Outer == BindingKind.ModuleTypeAlias
            || (cell.Family == Family.TypeMember && cell.Id.Contains("/alias/", StringComparison.Ordinal));

    private SharpyRun RunSharpy(string source, int executionTimeoutMs = SharpyExecTimeoutMs)
    {
        _compilePathOutput.Muted = true;
        ExecutionResult r;
        try
        {
            r = CompileAndExecute(source, "name_resolution.spy", executionTimeoutMs: executionTimeoutMs);
        }
        finally
        {
            _compilePathOutput.Muted = false;
        }
        var diagnostics = r.RawDiagnostics;
        var all = diagnostics.Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var errors = diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
            .Select(d => d.Code).Where(c => c != null).Select(c => c!).ToList();
        var cs = diagnostics.Where(d => d.Code == "SPY0908")
            .Select(d => Regex.Match(d.Message, @"\bCS\d{4}\b"))
            .Where(m => m.Success).Select(m => m.Value).FirstOrDefault();
        return new SharpyRun(r.Success, r.ExitCode, r.Exception != null, r.TimedOut,
            r.StandardOutput, r.StandardError, errors, all, cs);
    }

    private static string SourceKey(string source)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

    [SkippableFact]
    public void NameResolutionDifferential_Sweep()
    {
        var oracle = PythonBatchOracle.Over(PythonInterpreter.RequireOrSkip(minMinor: 12));
        var wall = Stopwatch.StartNew();

        var pool = GeneratedCells();
        var failures = new List<string>();

        // The generator's own totality: cell ids are unique, and every exemption tag is in its closed set.
        foreach (var dup in pool.GroupBy(c => c.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Take(20))
            failures.Add($"duplicate cell id {dup.Key}");
        failures.AddRange(ClosedSetFailures(pool));
        failures.AddRange(RosterFedFailures(pool));
        if (pool.Count > CellBudget)
            failures.Add($"the pool is {pool.Count} cells and the budget is {CellBudget}: prune in D3.e's order");

        int limit = int.TryParse(Environment.GetEnvironmentVariable(StrideEnv), out var parsed) ? parsed : 0;
        bool partial = limit > 0 && limit < pool.Count;
        var cells = partial
            ? pool.Where((_, i) => i % Math.Max(1, pool.Count / limit) == 0).ToList()
            : pool;

        // --- Python arm: one batch. A twin the renderer cannot produce is a generator bug, never a skip. ---
        var requests = new List<PyRequest>(cells.Count);
        var twinFailures = new Dictionary<int, string>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].Family == Family.Seed)
                continue; // the seed's python verdicts are TwinRenderer_SeedPythonVerdicts_Reproduce's control
            try
            {
                requests.Add(new PyRequest(i, cells[i].HandTwin ?? TwinRenderer.Render(cells[i].TwinSource ?? cells[i].Source)));
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            {
                twinFailures[i] = $"renderer: {ex.Message}";
            }
        }
        var pythonById = oracle.RunBatch(requests, PythonBatchTimeoutMs);

        // --- Sharpy arm: production compile + execute, sequential, `run` and never `emit`. ---
        var cachePath = Environment.GetEnvironmentVariable(CacheEnv);
        var cache = LoadCache(cachePath);
        int cached = 0;
        var runs = new SharpyRun[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            var key = SourceKey(cells[i].Source);
            if (cachePath != null && cache.TryGetValue(key, out var hit))
            {
                runs[i] = hit;
                cached++;
            }
            else
            {
                runs[i] = RunSharpy(cells[i].Source);
                if (cachePath != null)
                    cache[key] = runs[i];
            }
        }
        if (cachePath != null)
            File.WriteAllText(cachePath, JsonSerializer.Serialize(cache));

        // --- Judge. ---
        var results = new List<CellResult>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            PyOutcome py = cell.Family == Family.Seed
                ? cell.Python
                : pythonById.TryGetValue(i, out var verdict)
                    ? ClassifyPython(verdict)
                    : new PyOutcome(PyKind.SyntaxError, twinFailures.GetValueOrDefault(i, "no verdict from the runner"));
            var run = runs[i];
            var kind = ClassifySharpy(run);
            var control = twinFailures.TryGetValue(i, out var twin) ? twin : PythonControlFailure(cell, py);
            var cls = control != null ? VerdictClass.GeneratorBug : Judge(cell, kind, run.Stdout);
            results.Add(new CellResult(cell, run, kind, py, cls, control));
        }

        // --- Ratchet. ---
        var universe = pool.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var rows = LoadAllowlist(universe, out var loaderProblems);
        failures.AddRange(loaderProblems);

        var offenders = new List<CellResult>();
        var stale = new List<string>();
        var moved = new List<string>();
        var ineffective = new List<string>();
        var generatorBugs = results.Where(r => r.Class == VerdictClass.GeneratorBug).ToList();
        var harness = results.Where(r => r.Class == VerdictClass.Harness).ToList();
        foreach (var r in results)
        {
            var isRed = r.Class is VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow;
            rows.TryGetValue(r.Cell.Id, out var row);
            if (r.Class == VerdictClass.GeneratorBug && row != null)
                ineffective.Add($"{r.Cell.Id}: row names a GeneratorBug cell — not allowlistable");
            if (isRed && row == null)
                offenders.Add(r);
            else if (isRed && row!.Class != r.Class)
                moved.Add($"{r.Cell.Id}: listed as {row.Class}, now {r.Class} — update or delete the row");
            else if (!isRed && row != null && r.Class is not (VerdictClass.GeneratorBug or VerdictClass.Harness))
                stale.Add($"{r.Cell.Id}: listed as {row.Class}, now {r.Class} — STALE: delete the row");

            // D4: a row cites the issue whose fix turns it green, and that route is the code's. A hand-edited cite cannot hide here.
            if (isRed && row != null && row.Class == r.Class && row.Cite != ProposedCite(r.Cell, r.Class, r.Run))
                failures.Add($"{r.Cell.Id}: the row cites '{row.Cite}' and the route says '{ProposedCite(r.Cell, r.Class, r.Run)}'");
        }
        failures.AddRange(RefusalCodeFailures(results, partial));

        wall.Stop();
        var report = WriteReport(pool, results, rows, offenders, stale, moved, ineffective, partial, cached, wall.Elapsed);
        WriteProposedAllowlist(results);

        Output.WriteLine(report);
        if (partial)
            Output.WriteLine($"*** PARTIAL RUN {StrideEnv}={limit}: the allowlist's guarantee is scoped to these cells. ***");
        if (cachePath != null)
            Output.WriteLine($"*** CACHED RUN {CacheEnv}: {cached}/{cells.Count} Sharpy results reused; never a gate. ***");

        foreach (var g in generatorBugs.Take(40))
            failures.Add($"GeneratorBug {g.Cell.Id}: {g.Control}");
        if (generatorBugs.Count > 40)
            failures.Add($"… {generatorBugs.Count - 40} more GeneratorBug cells");
        foreach (var h in harness.Take(20))
            failures.Add($"Harness {h.Cell.Id}: Sharpy {h.Kind}, exit {h.Run.ExitCode}, codes [{string.Join(",", h.Run.AllCodes)}]");
        foreach (var o in offenders.Take(40))
            failures.Add($"{o.Class} {o.Cell.Id} ({o.Cell.Rule}): Sharpy {Describe(o)}; tag {o.Cell.Tag}, model '{o.Cell.Model}'; python {o.Python}");
        if (offenders.Count > 40)
            failures.Add($"… {offenders.Count - 40} more non-allowlisted red cells");
        failures.AddRange(stale);
        failures.AddRange(moved);
        failures.AddRange(ineffective);
        if (failures.Count > 0)
            Assert.Fail($"{failures.Count} failures. {report}\n{string.Join("\n", failures)}");
    }

    /// <summary>A seed cell's python column is its verdict from the seed table (the renderer facts' control), not a batch run.</summary>
    private static string PythonText(CellResult r) => r.Cell.SeedPythonVerdict ?? r.Python.ToString();

    private static string Describe(CellResult r)
        => r.Kind == SharpyKind.Ran
            ? $"Ran '{Normalize(r.Run.Stdout)}'"
            : $"{r.Kind} [{string.Join(",", r.Run.AllCodes.Distinct())}{(r.Run.CsCode != null ? " " + r.Run.CsCode : "")}]";

    internal sealed record CellResult(
        NameCell Cell, SharpyRun Run, SharpyKind Kind, PyOutcome Python, VerdictClass Class, string? Control);

    private static Dictionary<string, SharpyRun> LoadCache(string? path)
    {
        if (path == null || !File.Exists(path))
            return new Dictionary<string, SharpyRun>(StringComparer.Ordinal);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, CachedRun>>(File.ReadAllText(path))!;
        return loaded.ToDictionary(kv => kv.Key, kv => kv.Value.ToRun(), StringComparer.Ordinal);
    }

    private sealed record CachedRun(
        bool Success, int? ExitCode, bool ThrewInHarness, bool TimedOut, string Stdout, string Stderr,
        List<string> ErrorCodes, List<string> AllCodes, string? CsCode)
    {
        public SharpyRun ToRun() => new(Success, ExitCode, ThrewInHarness, TimedOut, Stdout, Stderr, ErrorCodes, AllCodes, CsCode);
    }

    private string WriteReport(
        IReadOnlyList<NameCell> pool,
        IReadOnlyList<CellResult> results,
        IReadOnlyDictionary<string, AllowRow> rows,
        IReadOnlyList<CellResult> offenders,
        IReadOnlyList<string> stale,
        IReadOnlyList<string> moved,
        IReadOnlyList<string> ineffective,
        bool partial,
        int cached,
        TimeSpan wall)
    {
        var perFamily = results.GroupBy(r => r.Cell.Family).OrderBy(g => g.Key).ToDictionary(
            g => g.Key.ToString(),
            g => g.GroupBy(r => r.Class).OrderBy(c => c.Key).ToDictionary(c => c.Key.ToString(), c => c.Count()));
        // The seed's python verdicts are a separate fact's control, and an unruled cell's prediction is not held to account.
        var underControl = results.Where(r => r.Cell.Family != Family.Seed && r.Cell.Tag != SharpyTag.Unruled).ToList();
        int pythonAgreed = underControl.Count(r => r.Control == null);
        var report = new
        {
            commit = CurrentCommit(),
            pool = pool.Count,
            run = results.Count,
            coveragePercent = pool.Count == 0 ? 0 : 100.0 * results.Count / pool.Count,
            partial,
            cachedSharpyResults = cached,
            perFamily,
            perFamilyTargetOuter = results.GroupBy(r => $"{r.Cell.Family}/{r.Cell.Target}/{r.Cell.Outer}")
                .OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()),
            pythonControlAgreed = pythonAgreed,
            pythonControlCells = underControl.Count,
            pythonControlSeedSeparate = results.Count(r => r.Cell.Family == Family.Seed),
            pythonControlUnruledExempt = results.Count(r => r.Cell.Family != Family.Seed && r.Cell.Tag == SharpyTag.Unruled),
            unruledPredictionDiffers = results.Count(r => r.Cell.Family != Family.Seed && r.Cell.Tag == SharpyTag.Unruled && r.Python != r.Cell.Python),
            generatorBugs = results.Count(r => r.Class == VerdictClass.GeneratorBug),
            harness = results.Count(r => r.Class == VerdictClass.Harness),
            allowlistSize = rows.Count,
            nonAllowlistedRed = offenders.Count,
            staleRows = stale,
            movedRows = moved,
            ineffectiveRows = ineffective,
            excludedAxes = Enum.GetNames<ExcludedAxis>(),
            rosters = RosterReport(results),
            unruledCells = results.Where(r => r.Cell.Tag == SharpyTag.Unruled).Select(r => new
            {
                id = r.Cell.Id,
                shape = r.Cell.Shape,
                sharpy = Describe(r),
                python = PythonText(r),
            }).ToList(),
            secondsPerCell = results.Count == 0 ? 0 : wall.TotalSeconds / results.Count,
            wallSeconds = wall.TotalSeconds,
        };

        var dir = ReportDirectory();
        File.WriteAllText(
            Path.Combine(dir, "name-resolution-differential-report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        using (var writer = new StreamWriter(Path.Combine(dir, "name-resolution-differential-cells.jsonl")))
        {
            foreach (var r in results)
            {
                writer.WriteLine(JsonSerializer.Serialize(new
                {
                    id = r.Cell.Id,
                    family = r.Cell.Family.ToString(),
                    verdict = r.Class.ToString(),
                    tag = r.Cell.Tag.ToString(),
                    rule = r.Cell.Rule,
                    model = r.Cell.Model,
                    predicted = r.Cell.SeedPythonVerdict ?? r.Cell.Python.ToString(),
                    python = PythonText(r),
                    control = r.Control,
                    sharpy = Describe(r),
                    cs = r.Run.CsCode,
                    source = r.Cell.Source,
                }));
            }
        }

        var counts = string.Join("; ", perFamily.Select(f => $"{f.Key}: " + string.Join(" ", f.Value.Select(c => $"{c.Key}={c.Value}"))));
        return $"name-resolution-differential: pool {pool.Count}, run {results.Count}, python agreed {pythonAgreed}/{underControl.Count}, "
            + $"allowlist {rows.Count}, non-allowlisted {offenders.Count}, stale {stale.Count}, moved {moved.Count}, "
            + $"wall {wall.TotalSeconds:F1}s ({(results.Count == 0 ? 0 : wall.TotalSeconds / results.Count):F3}s/cell). {counts}";
    }

    private static void WriteProposedAllowlist(IReadOnlyList<CellResult> results)
    {
        var lines = results
            .Where(r => r.Class is VerdictClass.Ice or VerdictClass.FalseRefusal or VerdictClass.SilentWrong or VerdictClass.RuntimeThrow)
            .OrderBy(r => r.Cell.Id, StringComparer.Ordinal)
            .Select(r => $"{r.Cell.Id} {r.Class} # {ProposedCite(r.Cell, r.Class, r.Run)}");
        File.WriteAllLines(Path.Combine(ReportDirectory(), "name-resolution-differential-proposed-allowlist.txt"), lines);
    }

    /// <summary>The report's sha, so its counts read `@ sha`. A tree with no git says so; the sweep's verdict does not depend on it.</summary>
    private static string CurrentCommit()
    {
        try
        {
            using var git = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = ReportDirectory(),
            })!;
            var sha = git.StandardOutput.ReadToEnd().Trim();
            git.WaitForExit();
            return git.ExitCode == 0 ? sha : "unknown";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "unknown";
        }
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
