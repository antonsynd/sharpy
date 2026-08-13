using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Sharpy.Cli.Commands;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// #1419 regression net: <c>sharpyc run</c> with no <c>--output</c> must stage its executable in a
/// private directory, never in the temp root itself.
///
/// <para>
/// <c>run</c> copies the whole runtime closure — Sharpy.Core.dll, the used stdlib assemblies, their
/// transitive managed and native dependencies — beside the executable under <em>fixed</em> file
/// names, then removes them afterwards. Staged in the shared temp root, two concurrent runs
/// overwrite each other's already-mapped assemblies (the SIGABRT / exit 134 signature) and delete
/// each other's dependencies (the FileNotFoundException signature). Both come from one line.
/// </para>
///
/// <para>
/// The test observes the layout directly rather than racing two runs against each other: the
/// compiled program parks at a filesystem barrier, and while it is parked its executable and the
/// whole copied closure are on disk to be looked at. Two assertions carry it — no file belonging to
/// this run may appear in the temp root, and (the positive control, without which the first would
/// pass for a run that never staged anything) exactly one private directory holding this run's
/// executable must exist.
/// </para>
/// </summary>
public class RunTempIsolationTests : IDisposable
{
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void Run_StagesTheExecutableInItsOwnDirectory_NotTheSharedTempRoot()
    {
        // Unique per run, so every filesystem observation below is about this run alone and cannot
        // be perturbed by another compilation — or another agent's `sharpyc run` — sharing the box.
        var stem = "run_isolation_" + Guid.NewGuid().ToString("N");
        var readyMarker = _ws.PathFor("ready");
        var goMarker = _ws.PathFor("go");

        var spyPath = _ws.WriteSpy(
            $"""
            import os
            import time


            def main() -> None:
                os.mkdir("{ForSource(readyMarker)}")
                while not os.path.exists("{ForSource(goMarker)}"):
                    time.sleep(0.02)
            """,
            stem + ".spy");

        var tempRoot = Path.GetTempPath();
        using var run = StartRun(spyPath);

        try
        {
            // The arrangement, asserted rather than assumed: until the program reaches its barrier
            // nothing has been staged, and every observation below would be vacuously clean.
            WaitUntil(() => Directory.Exists(readyMarker) || run.Process.HasExited, TimeSpan.FromMinutes(3));
            run.Process.HasExited.Should().BeFalse(
                $"the compiled program must still be parked at its barrier; it exited early with:\n{run.Output}");
            Directory.Exists(readyMarker).Should().BeTrue(
                "the compiled program must reach its barrier, which is what puts its executable and "
                    + $"runtime closure on disk to be observed; output so far:\n{run.Output}");

            var strayFiles = Directory.GetFiles(tempRoot)
                .Select(Path.GetFileName)
                .Where(name => name!.StartsWith(stem, StringComparison.Ordinal))
                .ToArray();
            var privateDirs = StagingDirectoriesHolding(tempRoot, stem);

            strayFiles.Should().BeEmpty(
                "`run` must not write its executable or sidecars into the temp root, where a "
                    + "concurrent run overwrites and deletes them (#1419)");
            privateDirs.Should().ContainSingle(
                "`run` must stage into exactly one private directory under the temp root — this is "
                    + "also the control proving the run really did stage something where the "
                    + "previous assertion was looking");

            Directory.CreateDirectory(goMarker);
            run.Process.WaitForExit(180_000).Should().BeTrue("the released program must not hang");
            run.Process.WaitForExit();
            run.Process.ExitCode.Should().Be(0, $"the program must succeed; output was:\n{run.Output}");

            Directory.Exists(privateDirs[0]).Should().BeFalse(
                "`run` must remove its private directory, contents and all, once the program exits");
        }
        finally
        {
            // Release the barrier however the assertions went, so a failure never leaves the
            // compiled program spinning.
            Directory.CreateDirectory(goMarker);
            if (!run.Process.WaitForExit(30_000))
            {
                run.Process.Kill(entireProcessTree: true);
            }
        }
    }

    [Fact]
    public void CreateTemporaryRunDirectory_IsFreshAndUnderTheTempRootWithoutBeingIt()
    {
        var first = RunCommand.CreateTemporaryRunDirectory();
        var second = RunCommand.CreateTemporaryRunDirectory();

        try
        {
            Directory.Exists(first).Should().BeTrue("the directory must exist before anything is staged in it");
            first.Should().NotBe(second, "two runs must never share a staging directory");
            Path.GetDirectoryName(first).Should().Be(
                Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
                "the staging directory sits under the temp root rather than being it");
            Directory.EnumerateFileSystemEntries(first).Should().BeEmpty("a staging directory starts empty");
        }
        finally
        {
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The staging directories under <paramref name="tempRoot"/> that hold a file named for this
    /// run. Matching on the run's own stem rather than on the directory name alone keeps a
    /// concurrent <c>sharpyc run</c> — plausible on a shared machine — out of the result.
    /// </summary>
    private static string[] StagingDirectoriesHolding(string tempRoot, string stem) =>
        Directory.GetDirectories(tempRoot, "sharpy_run_*")
            .Where(dir => HoldsFileNamed(dir, stem))
            .ToArray();

    private static bool HoldsFileNamed(string dir, string stem)
    {
        try
        {
            return Directory.EnumerateFiles(dir)
                .Any(f => Path.GetFileName(f).StartsWith(stem, StringComparison.Ordinal));
        }
        catch (DirectoryNotFoundException)
        {
            // Another run cleaned up between the listing and the read.
            return false;
        }
    }

    /// <summary>
    /// Starts <c>sharpyc run</c> on <paramref name="spyPath"/> with both streams drained in the
    /// background — the test reads the accumulated output while the process is still alive, which a
    /// blocking <c>ReadToEnd</c> could not do.
    /// </summary>
    private static RunningCli StartRun(string spyPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(spyPath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(CliDll);
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add(spyPath);

        var process = Process.Start(psi)!;
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) { lock (output) { output.AppendLine(e.Data); } } };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) { lock (output) { output.AppendLine(e.Data); } } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new RunningCli(process, output);
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !condition())
        {
            Thread.Sleep(25);
        }
    }

    /// <summary>Embeds an absolute path in Sharpy source (forward slashes work on every platform).</summary>
    private static string ForSource(string path) => path.Replace("\\", "/");

    private sealed class RunningCli(Process process, StringBuilder output) : IDisposable
    {
        public Process Process { get; } = process;

        public string Output
        {
            get { lock (output) { return output.ToString(); } }
        }

        public void Dispose() => Process.Dispose();
    }
}
