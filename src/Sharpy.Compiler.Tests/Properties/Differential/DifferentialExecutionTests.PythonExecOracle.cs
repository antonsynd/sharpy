using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sharpy.Compiler.Tests.Properties.Differential;

// The CPython oracle's process plumbing (#2324, P63b): spawning python3, a version probe, a batch file and its
// cleanup. It judges no Sharpy program, so it lives outside the oracle scan's universe (this file does not
// name the typed generator namespace); its catches guard process and file IO only.
public partial class DifferentialExecutionTests
{
    // ----------------------------------------------------------------------------------------- //
    // Python oracle process wrapper
    // ----------------------------------------------------------------------------------------- //

    private sealed class PythonExecOracle
    {
        private readonly string _pythonExe;
        private readonly string _scriptPath;

        private PythonExecOracle(string pythonExe, string scriptPath)
        {
            _pythonExe = pythonExe;
            _scriptPath = scriptPath;
        }

        public static PythonExecOracle? TryLocate()
        {
            string? script = FindRunnerScript();
            if (script is null)
                return null;
            foreach (var candidate in new[] { "python3", "python" })
            {
                if (HasSupportedVersion(candidate))
                    return new PythonExecOracle(candidate, script);
            }
            return null;
        }

        private static string? FindRunnerScript()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
                {
                    var candidate = Path.Combine(
                        dir.FullName, "build_tools", "differential_exec", "run_programs.py");
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

        public IReadOnlyDictionary<int, PyResult> RunBatch(IReadOnlyList<PyRequest> requests)
        {
            string batchPath = Path.Combine(Path.GetTempPath(), $"sharpy-diffexec-{Guid.NewGuid():N}.jsonl");
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

            // Generous ceiling: each program has its own per-program timeout inside the runner.
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
}
