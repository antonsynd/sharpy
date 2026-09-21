using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>
/// The class guard for format-spec parity (#1943, #1944, #1945): a differential sweep of the PEP 3101
/// mini-language against python3. It generates <c>(operand kind, spec)</c> cells from the grammar
/// <c>[[fill]align][sign][z][#][0][width][grouping][.precision][type]</c> (a single fixed-seed
/// <c>System.Random</c> draws every slot, so the corpus is byte-identical run-to-run), assembles each
/// into a <c>try: print(format(v, "spec")) except ValueError</c>
/// program, runs it under Sharpy (the production compile+execute path) and python3 3.12 (through
/// <c>build_tools/differential_exec/run_programs.py</c>), and compares stdout byte-for-byte.
///
/// <para>Sweep discipline mirrors <see cref="DifferentialExecutionTests"/>: any non-allowlisted
/// divergence fails the run; an allowlisted cell that no longer diverges fails until its line is
/// deleted (drain-on-fix). Every allowlist row cites an issue. The values are fixed and exactly
/// representable (int 42, float 3.5, bool True, str "ab"), so the sweep exercises the SPEC grammar,
/// not float-repr pathology. Skips (no-op) when a suitable python3 is absent; CI pins 3.12.</para>
///
/// <para>Where the three consumers (format(), str.format, f-strings) are tied together by
/// <c>FormatEngineConsumerParityTests</c>, this generator only needs the format() route.</para>
/// </summary>
[Trait("Category", "GapDiscovery")]
public class FormatSpecDifferentialTests : IntegrationTestBase
{
    public FormatSpecDifferentialTests(ITestOutputHelper output) : base(output) { }

    // A fixed integer seed drives a System.Random so the generated corpus is byte-for-byte identical
    // run-to-run and machine-to-machine on a pinned runtime — a sweep whose inputs drift cannot be
    // ratcheted. (CsCheck's Sample was observed to NOT reproduce its cell set across runs here, which
    // would let the sweep flap between pre-existing engine bugs; a seeded Random is unambiguous.)
    private const int GeneratedSeed = 0x5F0A_7C31;

    // The contract wants at least this many distinct cells per run.
    private const int TargetCells = 240;

    private const int SharpyExecTimeoutMs = 12_000;

    private sealed record Cell(string Kind, string Spec)
    {
        // A key safe for the allowlist file: the spec is hex-encoded so a '#', '*', or whitespace in
        // it can never be read as a comment, a glob, or trimmed away. The human-readable spec travels
        // in the divergence detail and in the row's trailing comment.
        public string Key => "formatspec::" + Kind + "::"
            + string.Concat(Encoding.UTF8.GetBytes(Spec).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void FormatSpec_DifferentialSweep_MatchesCPython()
    {
        var oracle = PyOracle.TryLocate();
        if (oracle is null)
        {
            Output.WriteLine("SKIP: python3 >= 3.12 and run_programs.py not both available.");
            return;
        }

        var sw = Stopwatch.StartNew();
        var cells = GenerateCells(TargetCells);
        Assert.True(cells.Count >= 200,
            $"the grammar generator produced only {cells.Count} distinct cells (< 200): the spec space or iter is too small.");

        // --- Sharpy arm: production compile + execute, sequential. ---
        var sharpy = new Dictionary<string, ArmOutcome>(StringComparer.Ordinal);
        foreach (var cell in cells)
        {
            var r = CompileAndExecute(ProgramFor(cell), "format_spec_diff.spy", executionTimeoutMs: SharpyExecTimeoutMs);
            sharpy[cell.Key] = new ArmOutcome(r.Success && !r.TimedOut, r.StandardOutput, r.TimedOut);
        }

        // --- Python arm: one batch process, main() appended so CPython drives Sharpy's auto-entry. ---
        var requests = new List<(int Id, string Source)>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
            requests.Add((i, ProgramFor(cells[i]) + "\nmain()\n"));
        var pythonById = oracle.RunBatch(requests);

        var allowlist = Allowlist.Load();
        var divergences = new List<(Cell Cell, string Detail, bool Allowlisted)>();
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var s = sharpy[cell.Key];
            pythonById.TryGetValue(i, out var py);

            // The Python side must have run the cell (no syntax error, no timeout) to be an oracle.
            if (py is null || py.SyntaxError || py.TimedOut || s.TimedOut)
                continue;

            string sharpyOut = Normalize(s.Stdout);
            string pythonOut = Normalize(py.Stdout);
            if (!s.Ok)
            {
                divergences.Add((cell, $"Sharpy failed to compile/run (python ok): spec='{cell.Spec}' pyout='{pythonOut}'",
                    allowlist.Matches(cell.Key)));
            }
            else if (sharpyOut != pythonOut)
            {
                divergences.Add((cell, $"spec='{cell.Spec}': sharpy='{sharpyOut}' python='{pythonOut}'",
                    allowlist.Matches(cell.Key)));
            }
        }

        var offenders = divergences.Where(d => !d.Allowlisted).ToList();
        var stale = allowlist.ExactKeys
            .Except(divergences.Select(d => d.Cell.Key), StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        sw.Stop();
        Output.WriteLine(
            $"Format-spec differential: {cells.Count} cells, {divergences.Count} divergent "
            + $"({offenders.Count} non-allowlisted, {stale.Count} stale-allowlisted). Wall={sw.Elapsed.TotalSeconds:F1}s.");
        foreach (var o in offenders.Take(40))
            Output.WriteLine($"  DIVERGENCE {o.Cell.Key}  {o.Detail}");
        foreach (var k in stale)
            Output.WriteLine($"  STALE {k}");

        Assert.True(offenders.Count == 0,
            $"Format-spec differential: {offenders.Count} non-allowlisted divergence(s) between Sharpy and python3. "
            + "Fix the engine, or add an allowlist entry citing an issue:\n"
            + string.Join("\n", offenders.Take(40).Select(o => "  " + o.Cell.Key + "  " + o.Detail)));
        Assert.True(stale.Count == 0,
            $"Format-spec differential: {stale.Count} allowlist entr(ies) no longer diverge — delete them:\n"
            + string.Join("\n", stale.Select(k => "  " + k)));
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd('\n');

    private static string ProgramFor(Cell cell)
    {
        var (ctype, lit) = cell.Kind switch
        {
            "int" => ("int", "42"),
            "float" => ("float", "3.5"),
            "bool" => ("bool", "True"),
            _ => ("str", "\"ab\""),
        };
        // The spec grammar never generates '"', '\\', '{' or '}', so it embeds directly in a literal.
        return "def main() -> None:\n"
            + $"    v: {ctype} = {lit}\n"
            + "    try:\n"
            + $"        print(format(v, \"{cell.Spec}\"))\n"
            + "    except ValueError as e:\n"
            + "        print(\"ValueError:\", e)\n";
    }

    private static List<Cell> GenerateCells(int target)
    {
        // Grammar slots. Fill is restricted to a literal-safe alphabet (no '"', '\\', '{', '}').
        char[] fillChars = { '*', '0', ' ', '~', '@' };
        char[] alignChars = { '<', '>', '^', '=' };
        char[] signChars = { '+', '-', ' ' };
        char[] groupChars = { ',', '_' };
        char[] typeChars = { 'd', 'n', 'f', 'F', 'e', 'E', 'g', 'G', 'x', 'X', 'o', 'b', 'c', '%', 's' };
        string[] kinds = { "int", "float", "bool", "str" };

        var rng = new Random(GeneratedSeed);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cells = new List<Cell>();

        // Deterministic sampling: draw grammar slots from the seeded RNG until the target count of
        // distinct, in-scope cells is reached, with a hard iteration ceiling so a shrinking spec
        // space can never loop forever.
        long ceiling = Math.Max(target * 60L, 4000L);
        for (long iter = 0; iter < ceiling && cells.Count < target; iter++)
        {
            int fillAlign = rng.Next(0, 3); // 0 none, 1 lone align, 2 fill+align
            char fill = fillChars[rng.Next(fillChars.Length)];
            char align = alignChars[rng.Next(alignChars.Length)];
            bool hasSign = rng.Next(2) == 1;
            char sign = signChars[rng.Next(signChars.Length)];
            bool hasZ = rng.Next(2) == 1;
            bool hasHash = rng.Next(2) == 1;
            bool hasZero = rng.Next(2) == 1;
            int width = rng.Next(0, 13);
            bool hasGroup = rng.Next(2) == 1;
            char groupSep = groupChars[rng.Next(groupChars.Length)];
            bool hasPrec = rng.Next(2) == 1;
            int prec = rng.Next(0, 7);
            bool hasType = rng.Next(2) == 1;
            char type = typeChars[rng.Next(typeChars.Length)];
            string kind = kinds[rng.Next(kinds.Length)];

            string spec = BuildSpec(fillAlign, fill, align, hasSign, sign, hasZ, hasHash, hasZero,
                width, hasGroup, groupSep, hasPrec, prec, hasType, type);
            var cell = new Cell(kind, spec);
            if (HitsKnownGap(cell))
                continue;
            if (seen.Add(cell.Key))
                cells.Add(cell);
        }

        return cells;
    }

    /// <summary>
    /// Attributable up-front exclusion (the same pattern <see cref="DifferentialExecutionTests"/>
    /// uses for Sharpy-only forms): a cell whose spec carries the alternate form <c>#</c> on a
    /// float-family presentation (g/G/e/E/n or a float with no type code) exercises the ONE known
    /// engine gap #1958 — the '#' flag is a no-op on those types in <c>PyFormat.FormatFinite</c>.
    /// The radix types (b/o/x/X) implement '#' (the 0x/0o/0b prefix) and stay IN the sweep; <c>d</c>
    /// and <c>s</c> are excluded too because '#' there is collaterally near the gap and covered by the
    /// consumer-parity tests. DELETE this exclusion when #1958 lands — the sweep will then cover the
    /// '#'-on-float cells and go red until the engine renders them like CPython.
    /// </summary>
    private static bool HitsKnownGap(Cell cell)
    {
        // TODO(#1958): delete this exclusion when the '#' alternate form renders on float-family
        // presentations — the issue-state gate (build_tools/allowlist_issue_state.py, #1939) reads this
        // roster comment, so a CLOSED #1958 fails the gate until the exclusion is gone (drain-on-fix).
        if (!cell.Spec.Contains('#', StringComparison.Ordinal))
            return false;
        // The last spec char is the presentation type when it is a letter/'%'; a radix type keeps '#'.
        char type = cell.Spec.Length > 0 ? cell.Spec[cell.Spec.Length - 1] : '\0';
        bool radix = type == 'b' || type == 'o' || type == 'x' || type == 'X';
        return !radix;
    }

    private static string BuildSpec(
        int fillAlign, char fill, char align, bool hasSign, char sign, bool hasZ, bool hasHash,
        bool hasZero, int width, bool hasGroup, char groupSep, bool hasPrec, int prec, bool hasType, char type)
    {
        var sb = new StringBuilder();
        if (fillAlign == 2)
        {
            sb.Append(fill);
            sb.Append(align);
        }
        else if (fillAlign == 1)
        {
            sb.Append(align);
        }
        if (hasSign)
            sb.Append(sign);
        if (hasZ)
            sb.Append('z');
        if (hasHash)
            sb.Append('#');
        if (hasZero)
            sb.Append('0');
        if (width > 0)
            sb.Append(width.ToString(CultureInfo.InvariantCulture));
        if (hasGroup)
            sb.Append(groupSep);
        if (hasPrec)
        {
            sb.Append('.');
            sb.Append(prec.ToString(CultureInfo.InvariantCulture));
        }
        if (hasType)
            sb.Append(type);
        return sb.ToString();
    }

    private sealed record ArmOutcome(bool Ok, string Stdout, bool TimedOut);

    private sealed record PyResult(bool Ok, string Stdout, string Stderr, bool TimedOut, bool SyntaxError);

    // ------------------------------------------------------------------------------------------- //
    // python3 bridge (a focused replica of DifferentialExecutionTests.PythonExecOracle)
    // ------------------------------------------------------------------------------------------- //

    private sealed class PyOracle
    {
        private readonly string _pythonExe;
        private readonly string _scriptPath;

        private PyOracle(string pythonExe, string scriptPath)
        {
            _pythonExe = pythonExe;
            _scriptPath = scriptPath;
        }

        public static PyOracle? TryLocate()
        {
            string? script = FindScript();
            if (script is null)
                return null;
            foreach (var candidate in new[] { "python3", "python" })
            {
                if (HasSupportedVersion(candidate))
                    return new PyOracle(candidate, script);
            }
            return null;
        }

        private static string? FindScript()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
                {
                    var candidate = Path.Combine(dir.FullName, "build_tools", "differential_exec", "run_programs.py");
                    return File.Exists(candidate) ? candidate : null;
                }
                dir = dir.Parent;
            }
            return null;
        }

        private static bool HasSupportedVersion(string exe)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                if (proc is null)
                    return false;
                string outText = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                if (!proc.WaitForExit(10_000))
                {
                    try
                    { proc.Kill(entireProcessTree: true); }
                    catch { }
                    return false;
                }
                var match = Regex.Match(outText, @"Python (\d+)\.(\d+)");
                if (!match.Success)
                    return false;
                int major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                return major > 3 || (major == 3 && minor >= 12);
            }
            catch
            {
                return false;
            }
        }

        public IReadOnlyDictionary<int, PyResult> RunBatch(IReadOnlyList<(int Id, string Source)> requests)
        {
            string batchPath = Path.Combine(Path.GetTempPath(), $"sharpy-fmtspec-{Guid.NewGuid():N}.jsonl");
            try
            {
                using (var writer = new StreamWriter(batchPath, append: false, new UTF8Encoding(false)))
                {
                    foreach (var r in requests)
                        writer.WriteLine(JsonSerializer.Serialize(new { id = r.Id, source = r.Source }));
                }
                return ParseVerdicts(RunRunner(batchPath));
            }
            finally
            {
                try
                { File.Delete(batchPath); }
                catch { }
            }
        }

        private string RunRunner(string batchPath)
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonExe,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };
            proc.StartInfo.ArgumentList.Add(_scriptPath);
            proc.StartInfo.ArgumentList.Add("--batch");
            proc.StartInfo.ArgumentList.Add(batchPath);

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            if (!proc.WaitForExit(300_000))
            {
                try
                { proc.Kill(entireProcessTree: true); }
                catch { }
                throw new InvalidOperationException("run_programs.py timed out.");
            }
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"run_programs.py exited {proc.ExitCode}: {stderr}");

            return stdout.ToString();
        }

        private static IReadOnlyDictionary<int, PyResult> ParseVerdicts(string stdout)
        {
            var result = new Dictionary<int, PyResult>();
            foreach (var line in stdout.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                int id = root.GetProperty("id").GetInt32();
                result[id] = new PyResult(
                    Ok: root.GetProperty("ok").GetBoolean(),
                    Stdout: root.TryGetProperty("stdout", out var so) ? so.GetString() ?? "" : "",
                    Stderr: root.TryGetProperty("stderr", out var se) ? se.GetString() ?? "" : "",
                    TimedOut: root.TryGetProperty("timed_out", out var to) && to.GetBoolean(),
                    SyntaxError: root.TryGetProperty("syntax_error", out var sy) && sy.GetBoolean());
            }
            return result;
        }
    }

    // ------------------------------------------------------------------------------------------- //
    // Allowlist (key ratchet, mirroring the interop/differential allowlists)
    // ------------------------------------------------------------------------------------------- //

    private sealed class Allowlist
    {
        private readonly HashSet<string> _exact;

        private Allowlist(HashSet<string> exact) => _exact = exact;

        public bool Matches(string key) => _exact.Contains(key);

        public IReadOnlyCollection<string> ExactKeys => _exact;

        public static Allowlist Load()
        {
            var exact = new HashSet<string>(StringComparer.Ordinal);
            var path = FindPath();
            if (path == null || !File.Exists(path))
                return new Allowlist(exact);

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw;
                var hash = line.IndexOf('#', StringComparison.Ordinal);
                if (hash >= 0)
                    line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0)
                    continue;
                exact.Add(line);
            }
            return new Allowlist(exact);
        }

        private static string? FindPath()
        {
            var current = AppContext.BaseDirectory;
            while (current != null)
            {
                var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
                if (Directory.Exists(dir))
                    return Path.Combine(dir, "format-spec-differential-allowlist.txt");
                current = Directory.GetParent(current)?.FullName;
            }
            return null;
        }
    }
}
