using System.Text.RegularExpressions;
using Sharpy.Stdlib.Tests.Integration;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;
using Sharpy.TestInfrastructure.Oracles;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance.OSErrorPathState;

/// <summary>
/// A1 — G1, the filesystem differential (plan-638e8b C2, #2476). Every probe row of
/// <see cref="ProbeRoster"/> × the full product of <see cref="PathState"/> over its placeholders runs
/// as Sharpy and as python3 on the same state; outcome classes are compared under C2's Rule, keyed on
/// the ORACLE's outcome, never on a cell:
/// <list type="bullet">
/// <item>python raises nothing, or a class with <c>OSError</c> in its MRO → the cell is in contract and
/// Sharpy's outcome is equal (an <c>OSError</c>-family outcome is never a CLR type, a python success is
/// never a raise). A divergence is red unless it is a row of <c>oserror-path-state-allowlist.txt</c>
/// (each citing #2476, drained by D1/D2); a row no divergence produces is red (stale).</item>
/// <item>python raises any other class → the cell is outside the contract and must match a row of
/// <see cref="ExemptionRoster"/>; an exempt outcome with no row is red (a twin that fails for its own
/// reason), a row no cell produces is red, and a class python raises for a malformed program (a
/// subclass of SyntaxError, NameError, AttributeError, ImportError or TypeError) is red with or without
/// a row.</item>
/// <item>a probe none of whose cells is in contract asserts nothing and is red.</item>
/// </list>
/// The oracle is CPython on Linux (D4): off Linux the differential is SKIPPED with that reason; with no
/// python3 it is SKIPPED, and FAILS under <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c> (CI's state, A7).
/// </summary>
public class PathStateDifferentialTests
{
    private readonly ITestOutputHelper _output;

    public PathStateDifferentialTests(ITestOutputHelper output) => _output = output;

    private static readonly Regex AllowlistRow = new(@"^(?<key>\S+) python=(?<py>.+?) sharpy=(?<sh>.+?) # #2476$", RegexOptions.Compiled);

    public static string AllowlistPath => SysPath.Combine(FixtureRoots.RepositoryRoot,
        "src", "Sharpy.Stdlib.Tests", "Conformance", "OSErrorPathState", "oserror-path-state-allowlist.txt");

    private static readonly object Gate = new();
    private static DifferentialRun? _run;

    /// <summary>The run, once per process; each fact opens with the D4 gate first.</summary>
    private static DifferentialRun Run()
    {
        TestPreconditions.RequireOrSkip(OperatingSystem.IsLinux(),
            "G1's oracle is CPython on Linux (plan-638e8b D4): the kernel's errno decides python's verdict, so the differential is SKIPPED off Linux.");
        var python = PythonInterpreter.RequireOrSkip();
        lock (Gate)
            return _run ??= DifferentialRun.Execute(python, ProbeRoster.Probes);
    }

    [SkippableFact]
    public void InContractCells_EqualPython_ModuloTheAllowlist()
    {
        var run = Run();
        _output.WriteLine(run.Summary());
        var verdict = run.Judge(ExemptionRoster.Rows, ReadAllowlist());
        Assert.True(verdict.ModuleFailures.Count == 0, "modules that did not compile or run:\n" + string.Join("\n", verdict.ModuleFailures));
        Assert.True(verdict.Unlisted.Count == 0 && verdict.StaleAllowlist.Count == 0,
            "G1: in-contract cells where Sharpy's outcome class is not python3's.\n"
            + "Unlisted divergences (fix at the seam — D1's PathErrors — never by a row):\n  " + string.Join("\n  ", verdict.Unlisted)
            + "\nStale allowlist rows (the cell now agrees or no longer exists — delete the row):\n  " + string.Join("\n  ", verdict.StaleAllowlist));
    }

    [SkippableFact]
    public void ExemptOutcomes_MatchTheExemptionRoster_InBothDirections()
    {
        var verdict = Run().Judge(ExemptionRoster.Rows, ReadAllowlist());
        Assert.True(verdict.Malformed.Count == 0 && verdict.Unexempted.Count == 0 && verdict.UnusedExemptions.Count == 0,
            "G1 exemptions (python's outcome outside the OSError family):\n"
            + "Malformed-program classes (a twin failing for its own reason — fix the twin):\n  " + string.Join("\n  ", verdict.Malformed)
            + "\nExempt outcomes with no ExemptionRoster row:\n  " + string.Join("\n  ", verdict.Unexempted)
            + "\nExemptionRoster rows no cell produces:\n  " + string.Join("\n  ", verdict.UnusedExemptions));
    }

    [SkippableFact]
    public void EveryProbe_HasAnInContractCell()
    {
        var verdict = Run().Judge(ExemptionRoster.Rows, ReadAllowlist());
        Assert.True(verdict.ProbesWithNoInContractCell.Count == 0,
            "G1: probe rows none of whose cells is in contract (they assert nothing):\n  " + string.Join("\n  ", verdict.ProbesWithNoInContractCell));
    }

    /// <summary>Positive control (A1): open(missing), open(dir) and os.mkdir(existing dir) are cells, in contract, and equal.</summary>
    [SkippableFact]
    public void PositiveControl_KnownCellsAreInContractAndEqual()
    {
        var run = Run();
        var allowlist = ReadAllowlist();
        foreach (var key in new[] { "open(r)[Missing]", "open(r)[EmptyDir]", "os.mkdir[EmptyDir]" })
        {
            var cell = run.Outcomes.SingleOrDefault(o => o.Cell.Key == key);
            Assert.True(cell != null, $"{key} is not a cell");
            Assert.True(cell!.Python != null && cell.Python.StartsWith("OS ", StringComparison.Ordinal), $"{key}: python {cell.Python}");
            Assert.Equal(cell.Python, cell.Sharpy);
            Assert.DoesNotContain(key, allowlist.Keys);
        }
    }

    /// <summary>No exemption row may name a class python raises for a malformed program (static half; the dynamic half reads python's MRO).</summary>
    [Fact]
    public void ExemptionRoster_NamesNoMalformedProgramClass()
    {
        var forbidden = new[] { "SyntaxError", "IndentationError", "TabError", "NameError", "UnboundLocalError", "AttributeError", "ImportError", "ModuleNotFoundError", "TypeError" };
        foreach (var row in ExemptionRoster.Rows)
        {
            Assert.DoesNotContain(row.PythonClass.Split('.').Last(), forbidden);
            Assert.False(string.IsNullOrWhiteSpace(row.Reason), row.ProbeId);
            Assert.Contains(ProbeRoster.Probes, p => p.Id == row.ProbeId);
        }
    }

    internal static Dictionary<string, (string Python, string Sharpy)> ReadAllowlist()
    {
        var path = AllowlistPath;
        if (!File.Exists(path))
            throw new FileNotFoundException("oserror-path-state-allowlist.txt is missing; its presence arms the #2476 ratchet.", path);
        var rows = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var m = AllowlistRow.Match(line);
            Assert.True(m.Success, $"malformed allowlist row (want `<probe>[<states>] python=<outcome> sharpy=<outcome> # #2476`): {line}");
            Assert.False(rows.ContainsKey(m.Groups["key"].Value), "duplicate allowlist row: " + line);
            rows[m.Groups["key"].Value] = (m.Groups["py"].Value, m.Groups["sh"].Value);
        }
        return rows;
    }

    /// <summary>The executed differential: every cell's outcome on both sides.</summary>
    internal sealed class DifferentialRun
    {
        public System.Collections.Generic.List<CellOutcome> Outcomes { get; } = new();
        public System.Collections.Generic.List<string> ModuleFailures { get; } = new();
        public string PythonVersion { get; private init; } = "";

        public static DifferentialRun Execute(PythonInterpreter python, IReadOnlyList<Probe> probes)
        {
            var run = new DifferentialRun { PythonVersion = python.Version };
            var work = SysPath.Combine(SysPath.GetTempPath(), "sharpy-g1-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cells = PathStateDifferential.Cells(probes);
                using var runner = new Runner();
                foreach (var module in cells.Select(c => c.Probe.Module).Distinct(StringComparer.Ordinal))
                {
                    var moduleCells = cells.Where(c => c.Probe.Module == module).ToList();
                    var pyRoot = SysPath.Combine(work, "python", module);
                    var shRoot = SysPath.Combine(work, "sharpy", module);
                    var pyProgram = PathStateDifferential.PythonProgram(module, moduleCells, PathStateDifferential.Materialize(pyRoot, moduleCells, python: true));
                    var shProgram = PathStateDifferential.SharpyProgram(module, moduleCells, PathStateDifferential.Materialize(shRoot, moduleCells, python: false));

                    var script = SysPath.Combine(work, $"g1_{module.Replace('.', '_')}.py");
                    File.WriteAllText(script, pyProgram);
                    Dictionary<string, string> pyOut;
                    try
                    {
                        pyOut = PathStateDifferential.ParseOutcomes(PathStateDifferential.RunPython(python, script));
                    }
                    catch (InvalidOperationException ex)
                    {
                        run.ModuleFailures.Add($"{module}: the python twin failed: {ex.Message}");
                        pyOut = new Dictionary<string, string>();
                    }

                    var result = runner.Execute(shProgram);
                    if (!result.Success && result.CompilationErrors.Count > 0)
                        run.ModuleFailures.Add($"{module}: the Sharpy program did not compile:\n    " + string.Join("\n    ", result.CompilationErrors.Take(10)));
                    else if (result.TimedOut)
                        run.ModuleFailures.Add($"{module}: the Sharpy program timed out");
                    var shOut = PathStateDifferential.ParseOutcomes(result.StandardOutput.Replace("\r\n", "\n").Split('\n'));

                    foreach (var cell in moduleCells)
                        run.Outcomes.Add(new CellOutcome(cell, pyOut.GetValueOrDefault(cell.Id), shOut.GetValueOrDefault(cell.Id)));
                }
            }
            finally
            {
                try
                { Directory.Delete(work, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return run;
        }

        public string Summary()
        {
            var inContract = Outcomes.Count(o => o.Python is { } p && !p.StartsWith("X ", StringComparison.Ordinal));
            return $"G1 @ {PythonVersion}: {Outcomes.Count} cells over {Outcomes.Select(o => o.Cell.Probe.Id).Distinct().Count()} probes, "
                   + $"{inContract} in contract, {Outcomes.Count - inContract} exempt or missing.";
        }

        public Verdict Judge(IReadOnlyList<ExemptionRow> exemptions, IReadOnlyDictionary<string, (string Python, string Sharpy)> allowlist)
        {
            var verdict = new Verdict();
            verdict.ModuleFailures.AddRange(ModuleFailures);
            var usedExemptions = new HashSet<ExemptionRow>();
            var divergences = new Dictionary<string, (string Python, string Sharpy)>(StringComparer.Ordinal);
            var inContractByProbe = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var probe in Outcomes.Select(o => o.Cell.Probe.Id).Distinct())
                inContractByProbe[probe] = 0;

            foreach (var o in Outcomes)
            {
                var key = o.Cell.Key;
                if (o.Python == null)
                {
                    verdict.Unlisted.Add($"{key}: python printed no outcome");
                    continue;
                }
                if (o.Python.StartsWith("X ", StringComparison.Ordinal))
                {
                    var cls = o.Python[2..];
                    if (cls.EndsWith(" MALFORMED", StringComparison.Ordinal))
                    {
                        verdict.Malformed.Add($"{key}: python raised {cls}");
                        continue;
                    }
                    var row = exemptions.FirstOrDefault(r => r.ProbeId == o.Cell.Probe.Id && r.PythonClass == cls);
                    if (row == null)
                        verdict.Unexempted.Add($"{key}: python {cls} (Sharpy {o.Sharpy ?? "no outcome"})");
                    else
                        usedExemptions.Add(row);
                    continue;
                }

                inContractByProbe[o.Cell.Probe.Id]++;
                if (o.Sharpy != o.Python)
                    divergences[key] = (o.Python, o.Sharpy ?? "no outcome");
            }

            foreach (var (key, d) in divergences.OrderBy(d => d.Key, StringComparer.Ordinal))
            {
                if (!allowlist.TryGetValue(key, out var row) || row != d)
                    verdict.Unlisted.Add($"{key} python={d.Python} sharpy={d.Sharpy} # #2476"
                                         + (allowlist.TryGetValue(key, out var old) ? $"   (allowlist says python={old.Python} sharpy={old.Sharpy})" : ""));
            }
            foreach (var (key, row) in allowlist.OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                if (!divergences.TryGetValue(key, out var d) || d != row)
                    verdict.StaleAllowlist.Add($"{key} python={row.Python} sharpy={row.Sharpy}");
            }
            foreach (var row in exemptions.Where(r => !usedExemptions.Contains(r)))
                verdict.UnusedExemptions.Add($"{row.ProbeId} → {row.PythonClass}");
            verdict.ProbesWithNoInContractCell.AddRange(inContractByProbe.Where(p => p.Value == 0).Select(p => p.Key));
            verdict.Divergences = divergences.Count;
            return verdict;
        }
    }

    internal sealed class Verdict
    {
        public System.Collections.Generic.List<string> ModuleFailures { get; } = new();
        public System.Collections.Generic.List<string> Unlisted { get; } = new();
        public System.Collections.Generic.List<string> StaleAllowlist { get; } = new();
        public System.Collections.Generic.List<string> Malformed { get; } = new();
        public System.Collections.Generic.List<string> Unexempted { get; } = new();
        public System.Collections.Generic.List<string> UnusedExemptions { get; } = new();
        public System.Collections.Generic.List<string> ProbesWithNoInContractCell { get; } = new();
        public int Divergences { get; set; }
    }

    /// <summary>Compiles and runs one generated Sharpy program with the Stdlib referenced.</summary>
    private sealed class Runner : StdlibIntegrationTestBase
    {
        public Runner() : base(new NullOutput())
        {
            MuteCompilePath = true; // one generated program per module: its C# is not left in the result (#2438)
        }

        public (bool Success, IReadOnlyList<string> CompilationErrors, bool TimedOut, string StandardOutput) Execute(string source)
        {
            var r = CompileAndExecute(source, "g1.spy", executionTimeoutMs: 300_000);
            return (r.Success, r.CompilationErrors, r.TimedOut, r.StandardOutput);
        }

        private sealed class NullOutput : ITestOutputHelper
        {
            public void WriteLine(string message) { }
            public void WriteLine(string format, params object[] args) { }
        }
    }
}
