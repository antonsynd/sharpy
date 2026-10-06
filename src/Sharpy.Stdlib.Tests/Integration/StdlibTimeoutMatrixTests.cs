using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// #2263: for every Stdlib parameter that takes seconds, each value class gives python's value or
/// raises python's exception type, and never hangs where python returns. One program per module
/// (<c>Integration/TimeoutMatrix/timeout_matrix_*.spy</c>) enumerates the cross product in its own
/// loops: callable × value class {<c>-1</c>, <c>-1e10</c>, <c>-2.0</c>, <c>-0.5</c>, <c>-0.001</c>
/// (truncated to exactly -1 ms, .NET's "wait forever"), <c>-0.0005</c> (truncated to 0 ms), <c>0</c>,
/// <c>0.01</c>, <c>3e6</c> (past <c>int.MaxValue</c> ms), <c>1e10</c> (past python's int64
/// nanoseconds)} × the state that decides python's verdict (a lock free / held / owned, a semaphore
/// with or without a slot, an event set or not, the last barrier party or not, a thread alive /
/// released / joined, a process running or exited, a selectable pipe or none; subprocess adds
/// <c>2147483.647</c> / <c>2147483.648</c>, the edge of the int32 milliseconds python's
/// <c>poll()</c> takes) × <c>blocking</c> where the callable has it.
/// <para>Expected lines are the output of the python twin beside each program
/// (<c>timeout_matrix_*.py</c>: the same program with python spellings), run by python3 3.12.13 on
/// 2026-10-06 — python is the generator. A cell python itself waits out (forever for a held lock's
/// <c>-1</c>; about 35 days for <c>3e6</c>; a 5 s child for a long subprocess timeout) is not run:
/// the deviation row <c>stdlib-timeout-wait-saturates</c> records how such a wait is capped.
/// <c>http</c> and <c>requests</c> run only the values python refuses before it connects.
/// Not cells: NaN (python is not uniform — <c>Semaphore(0).acquire(timeout=nan)</c> hangs);
/// <c>Timer(1e10)</c> (python ends the timer thread with <c>OverflowError</c>, and an exception on a
/// Sharpy thread ends the process, a #2261 cell); the value <c>Barrier.wait</c> returns (the phase
/// number, not python's arrival index — filed by class). One cell is printed in a narrower form:
/// after <c>join(1e10)</c> raises on an alive thread, CPython 3.12 reports <c>is_alive()</c> False
/// although the thread still runs (its bpo-45274 handler releases the thread's own state lock), an
/// implementation defect the program does not ask about.</para>
/// <para>Before #2263 each primitive cast <c>timeout * 1000</c> to an int .NET wait: a negative
/// timeout escaped as <c>System.ArgumentOutOfRangeException</c> or hung (<c>-0.001</c> became -1,
/// "wait forever"), <c>1e10</c> hung instead of raising <c>OverflowError</c>, and a timeout on a
/// non-blocking <c>Lock.acquire</c> was accepted.</para>
/// </summary>
public class StdlibTimeoutMatrixTests : StdlibIntegrationTestBase
{
    public StdlibTimeoutMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    [Theory]
    [InlineData("threading")]
    [InlineData("time")]
    [InlineData("subprocess")]
    [InlineData("http")]
    public void EverySecondsParameter_ForEveryValueClass_GivesPythonsVerdict(string module)
    {
        var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Integration", "TimeoutMatrix");
        var source = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, $"timeout_matrix_{module}.spy"));
        var expected = Lines(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, $"timeout_matrix_{module}.expected")));

        // The failure mode is a hang, so the run is bounded; each table takes a few seconds.
        var result = CompileAndExecute(source, executionTimeoutMs: 60_000);

        var printed = Lines(result.StandardOutput);
        Assert.False(result.TimedOut,
            $"hung after {printed.Length} of {expected.Length} cells; last printed: {printed[^1]}");
        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal(expected, printed);
    }
}
