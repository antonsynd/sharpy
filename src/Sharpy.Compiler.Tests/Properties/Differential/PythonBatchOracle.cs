using System.Text.Json;
using Sharpy.TestInfrastructure.Oracles;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>One program of a CPython batch: the <see cref="Id"/> keys its verdict.</summary>
internal sealed record PyRequest(int Id, string Source);

/// <summary>CPython's verdict on one program, as <c>build_tools/differential_exec/run_programs.py</c> reports it.</summary>
internal sealed record PyResult(bool Ok, string Stdout, string Stderr, bool TimedOut, bool SyntaxError);

/// <summary>
/// The ONE process wrapper over <c>run_programs.py --batch</c> protocol (stdin form), shared by every differential
/// sweep that compares Sharpy's stdout with CPython's (P77a D5): one python3 for the whole batch, each program in
/// its own isolated child. The interpreter comes from <see cref="PythonInterpreter"/>, the one locator (#2300), so
/// a sweep's <c>RequireOrSkip(minMinor: 12)</c> gate and the runner cannot disagree on which python they mean.
/// </summary>
internal sealed class PythonBatchOracle
{
    /// <summary>A whole-batch ceiling, generous on purpose: each program has its own timeout inside the runner.</summary>
    private const int DefaultBatchTimeoutMs = 300_000;

    private readonly PythonInterpreter _interpreter;
    private readonly string _scriptPath;

    private PythonBatchOracle(PythonInterpreter interpreter, string scriptPath)
    {
        _interpreter = interpreter;
        _scriptPath = scriptPath;
    }

    /// <summary>The oracle over <paramref name="interpreter"/>; a missing runner script is a broken checkout, so it throws.</summary>
    public static PythonBatchOracle Over(PythonInterpreter interpreter)
    {
        var script = PythonInterpreter.FindRepoFile("build_tools", "differential_exec", "run_programs.py")
            ?? throw new FileNotFoundException(
                "python3 >= 3.12 is present but build_tools/differential_exec/run_programs.py was not found.");
        return new PythonBatchOracle(interpreter, script);
    }

    public IReadOnlyDictionary<int, PyResult> RunBatch(
        IReadOnlyList<PyRequest> requests, int batchTimeoutMs = DefaultBatchTimeoutMs)
    {
        var lines = requests.Select(r => JsonSerializer.Serialize(new { id = r.Id, source = r.Source }));
        return ParseVerdicts(_interpreter.RunBatch(_scriptPath, lines, batchTimeoutMs));
    }

    private static IReadOnlyDictionary<int, PyResult> ParseVerdicts(IEnumerable<string> verdictLines)
    {
        var result = new Dictionary<int, PyResult>();
        foreach (var line in verdictLines)
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
