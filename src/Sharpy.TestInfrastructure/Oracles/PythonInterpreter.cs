using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Sharpy.TestInfrastructure.Oracles;

/// <summary>
/// The python3 interpreter a differential twin uses as its oracle (#2300, P68). One locator so
/// a new twin does not add another private copy (<c>CPythonDifferentialParseTests.PythonOracle</c>,
/// <c>FormatSpecDifferentialTests.PyOracle</c> and <c>DifferentialExecutionTests</c> each carry
/// one today; retrofitting them is a chore note on #2300, not this helper's seam).
/// </summary>
/// <remarks>
/// <para>Absence is a SKIP on a developer machine (xunit's <c>skipped</c> count, through
/// <see cref="RequireOrSkip(int)"/>, #2342) and a FAIL where the oracle is required:
/// <see cref="Required"/> reads <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c>, which CI sets on the
/// Compiler shards, so a runner that lost its <c>setup-python</c> step cannot pass the twin
/// vacuously. The <see cref="Locate(IReadOnlyList{string}, bool, int)"/> overload takes the
/// required flag explicitly so a test can exercise the failing arm without setting the
/// process-global environment variable inside a testhost it shares with every other oracle
/// consumer.</para>
/// </remarks>
public sealed class PythonInterpreter
{
    /// <summary>The environment variable that turns a missing interpreter into a failure.</summary>
    public const string RequireEnvironmentVariable = "SHARPY_REQUIRE_PYTHON_ORACLE";

    /// <summary>The executables tried, in order, when no candidate list is given.</summary>
    public static readonly IReadOnlyList<string> DefaultCandidates = new[] { "python3", "python" };

    private static readonly Regex VersionPattern = new(@"Python (\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.CultureInvariant);

    private PythonInterpreter(string executable, string version, int major, int minor)
    {
        Executable = executable;
        Version = version;
        Major = major;
        Minor = minor;
    }

    /// <summary>The candidate that answered, e.g. <c>python3</c>.</summary>
    public string Executable { get; }

    /// <summary>The interpreter's own version line, e.g. <c>Python 3.12.13</c>.</summary>
    public string Version { get; }

    /// <summary>Major version number.</summary>
    public int Major { get; }

    /// <summary>Minor version number.</summary>
    public int Minor { get; }

    /// <summary>True when <c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c>: a missing oracle fails, never skips.</summary>
    public static bool Required => Environment.GetEnvironmentVariable(RequireEnvironmentVariable) == "1";

    /// <summary>The first default candidate reporting Python 3.<paramref name="minMinor"/> or later, or null.</summary>
    public static PythonInterpreter? TryLocate(int minMinor = 12) => TryLocate(DefaultCandidates, minMinor);

    /// <summary>The first of <paramref name="candidates"/> reporting Python 3.<paramref name="minMinor"/> or later, or null.</summary>
    public static PythonInterpreter? TryLocate(IReadOnlyList<string> candidates, int minMinor = 12)
    {
        foreach (var candidate in candidates)
        {
            var located = Probe(candidate);
            if (located is null)
                continue;
            if (located.Major > 3 || (located.Major == 3 && located.Minor >= minMinor))
                return located;
        }
        return null;
    }

    /// <summary>
    /// <see cref="TryLocate(int)"/> over the default candidates with <see cref="Required"/> read
    /// from the environment: null means "skip loudly", a missing interpreter in required mode
    /// throws <see cref="PythonOracleMissingException"/>.
    /// </summary>
    public static PythonInterpreter? Locate(int minMinor = 12) => Locate(DefaultCandidates, Required, minMinor);

    /// <summary>
    /// <see cref="TryLocate(IReadOnlyList{string}, int)"/>; when nothing answers and
    /// <paramref name="required"/> is set, throws <see cref="PythonOracleMissingException"/>
    /// instead of returning null.
    /// </summary>
    public static PythonInterpreter? Locate(IReadOnlyList<string> candidates, bool required, int minMinor = 12)
    {
        var located = TryLocate(candidates, minMinor);
        if (located is null && required)
        {
            throw new PythonOracleMissingException(
                $"No Python 3.{minMinor}+ interpreter among [{string.Join(", ", candidates)}], and " +
                $"{RequireEnvironmentVariable}=1 makes the python oracle required: the twin FAILS rather than skips.");
        }
        return located;
    }

    /// <summary>
    /// THE gate every python twin opens with (#2342, R-HZ): the located interpreter, or the test
    /// reports SKIPPED — never PASSED — when none answers. Three verdicts: located → returned;
    /// absent and not <see cref="Required"/> → <see cref="SkipException"/> (the test must be a
    /// <c>[SkippableFact]</c>/<c>[SkippableTheory]</c>, which xunit then counts under
    /// <c>skipped</c>); absent and required (<c>SHARPY_REQUIRE_PYTHON_ORACLE=1</c>, CI) →
    /// <see cref="PythonOracleMissingException"/>, a failure naming the oracle.
    /// </summary>
    public static PythonInterpreter RequireOrSkip(int minMinor = 12) => RequireOrSkip(DefaultCandidates, Required, minMinor);

    /// <summary>
    /// <see cref="RequireOrSkip(int)"/> over <paramref name="candidates"/> with the required flag
    /// explicit, so a test can exercise each verdict without setting the process-global
    /// environment variable.
    /// </summary>
    public static PythonInterpreter RequireOrSkip(IReadOnlyList<string> candidates, bool required, int minMinor = 12)
    {
        return Locate(candidates, required, minMinor)
            ?? throw new SkipException(
                $"No Python 3.{minMinor}+ interpreter among [{string.Join(", ", candidates)}] on PATH: the python twin " +
                $"compared nothing and is SKIPPED. Set {RequireEnvironmentVariable}=1 to make this a failure.");
    }

    /// <summary>
    /// The absolute path of <paramref name="segments"/> under the repository root (the first
    /// ancestor of the test binary holding <c>sharpy.sln</c>), or null when the root or the file
    /// is not found.
    /// </summary>
    public static string? FindRepoFile(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "sharpy.sln")))
            {
                var candidate = Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
                return File.Exists(candidate) ? candidate : null;
            }
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Runs <paramref name="scriptPath"/> ONCE with <paramref name="jsonlLines"/> on stdin and
    /// returns its non-empty stdout lines. A non-zero exit, an exit before the whole batch was read, or a timeout throws
    /// <see cref="InvalidOperationException"/> with stderr in the message.
    /// </summary>
    public List<string> RunBatch(string scriptPath, IEnumerable<string> jsonlLines, int timeoutMilliseconds = 120_000)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Executable,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                CreateNoWindow = true,
            },
        };
        proc.StartInfo.ArgumentList.Add(scriptPath);
        proc.StartInfo.Environment["PYTHONUTF8"] = "1";
        proc.StartInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        var stdout = new List<string>();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (stdout)
                    stdout.Add(e.Data);
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (stderr)
                    stderr.AppendLine(e.Data);
            }
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        // A script that exits before reading all of stdin (a crash, an import error) breaks the pipe; the write then
        // throws IOException with no stderr. Stop writing and let the exit-code check below report the script's stderr.
        var stdinBroken = false;
        try
        {
            foreach (var line in jsonlLines)
                proc.StandardInput.WriteLine(line);
            proc.StandardInput.Close();
        }
        catch (IOException)
        {
            stdinBroken = true;
        }

        if (!proc.WaitForExit(timeoutMilliseconds))
        {
            try
            { proc.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            throw new InvalidOperationException(
                $"{Path.GetFileName(scriptPath)} timed out after {timeoutMilliseconds} ms under {Version}. stderr:\n{stderr}");
        }
        proc.WaitForExit(); // drain the async readers

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(scriptPath)} exited {proc.ExitCode} under {Version}. stderr:\n{stderr}");
        }
        if (stdinBroken)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(scriptPath)} exited 0 under {Version} before reading the whole batch. stderr:\n{stderr}");
        }

        lock (stdout)
            return new List<string>(stdout);
    }

    private static PythonInterpreter? Probe(string executable)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (proc is null)
                return null;
            var errTask = proc.StandardError.ReadToEndAsync();
            string outText = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(10_000))
            {
                try
                { proc.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                return null;
            }
            outText += errTask.Result;
            var match = VersionPattern.Match(outText);
            if (!match.Success)
                return null;
            return new PythonInterpreter(
                executable,
                match.Value,
                int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The candidate is not on PATH.
            return null;
        }
    }
}

/// <summary>Thrown when the python oracle is required (<see cref="PythonInterpreter.Required"/>) but absent.</summary>
public sealed class PythonOracleMissingException : Exception
{
    /// <summary>Creates the exception with its loud message.</summary>
    public PythonOracleMissingException(string message) : base(message) { }
}
