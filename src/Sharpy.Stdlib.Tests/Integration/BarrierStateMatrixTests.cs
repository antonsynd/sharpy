using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// #2265: every <c>threading.Barrier</c> operation gives python's value, or raises python's
/// exception type, in every state of the barrier. One program
/// (<c>Integration/BarrierMatrix/barrier_matrix.spy</c>) runs the cells: the multiset of
/// <c>wait()</c>'s returned arrival indices for parties {1, 2, 3} × phase {first, second}; the index
/// is the arrival ORDER (the first party 0, the last <c>parties - 1</c>); a timed wait that completes;
/// <c>n_waiting</c> with one waiter; <c>reset()</c> with a waiter (it raises, the barrier is not broken,
/// the next phase indexes from 0); <c>abort()</c> with a waiter (it raises, a later wait raises,
/// broken); a timeout while another party waits (both raise, broken); the action once per phase; the
/// constructor's <c>timeout=</c> as every wait's default; and an action that raises (its party gets the
/// exception, the waiter <c>BrokenBarrierError</c>, broken).
/// <para>The expected lines are the output of the python twin beside the program
/// (<c>barrier_matrix.py</c>), run by python3 3.12.13 on 2026-10-07 — python is the generator.
/// Measured before #2265 @ a7595626f: every party got the .NET phase number (<c>p=1 p=1</c>, then
/// <c>p=2 p=2</c>; <c>Barrier(1).wait(0.5)</c> → 1), and <c>abort()</c> under a waiting party ended the
/// process with <c>System.ObjectDisposedException</c>; <c>Barrier(2, timeout=0.05)</c> did not
/// compile (no <c>timeout</c> parameter).</para>
/// </summary>
public class BarrierStateMatrixTests : StdlibIntegrationTestBase
{
    public BarrierStateMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    [Fact]
    public void EveryBarrierOperation_InEveryState_GivesPythonsVerdict()
    {
        var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Integration", "BarrierMatrix");
        var source = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "barrier_matrix.spy"));
        var expected = Lines(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "barrier_matrix.expected")));

        // A broken wake-up is a hang, so the run is bounded; the table takes well under a second.
        var result = CompileAndExecute(source, executionTimeoutMs: 60_000);

        var printed = Lines(result.StandardOutput);
        Assert.False(result.TimedOut,
            $"hung after {printed.Length} of {expected.Length} cells; last printed: {printed[^1]}");
        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal(expected, printed);
    }
}
