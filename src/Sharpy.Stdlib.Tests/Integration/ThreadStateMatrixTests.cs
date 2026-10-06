using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// #2261: every <c>threading.Thread</c> operation gives python's value, or raises python's exception
/// type, which a Sharpy <c>except RuntimeError</c> catches. Matrix: operation {<c>start()</c>,
/// <c>join()</c>, <c>join(0.01)</c>, <c>daemon</c> get, <c>daemon = True</c>, <c>is_alive()</c>,
/// <c>name = "x"</c> read back} × state {initial, alive, finished} × host {<c>Thread</c>,
/// <c>Timer</c>, a subclass}, plus the <c>current_thread()</c> wrapper on the main thread and on a
/// worker, and a worker that operates on its own <c>Thread</c> object. Every cell constructs a fresh
/// thread; the alive state is held by an <c>Event</c> (a <c>Timer</c> by its 30 s interval), so no
/// cell depends on timing. <c>join()</c> on an alive thread is not a cell: it waits for the thread
/// to finish, which <c>join(0.01)</c> covers without blocking.
/// <para>Expected lines are the output of the same program written in python, run by python3 3.12.13
/// on 2026-10-06 (python is the generator; the twin is this program line for line with python
/// spellings). Before #2261 the first <c>join()</c> before <c>start()</c> escaped as an unhandled
/// <c>System.Threading.ThreadStateException</c>, as did <c>daemon = True</c> and the <c>daemon</c>
/// read on a finished thread; <c>join()</c> on the current thread hung; and <c>is_alive</c> was a
/// property, so <c>t.is_alive()</c> was refused (SPY0230). The default <c>name</c>,
/// <c>main_thread()</c>, <c>enumerate()</c> and <c>active_count()</c> are #2261 part 2.</para>
/// </summary>
public class ThreadStateMatrixTests : StdlibIntegrationTestBase
{
    public ThreadStateMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    private static readonly string[] Program =
    {
        "import threading",
        "",
        "",
        "class Sub(threading.Thread):",
        "    hold: threading.Event",
        "",
        "    def __init__(self, hold: threading.Event):",
        "        super().__init__()",
        "        self.hold = hold",
        "",
        "    @override",
        "    def run(self) -> None:",
        "        self.hold.wait()",
        "",
        "",
        "def mk(host: str, hold: threading.Event) -> threading.Thread:",
        "    if host == \"Thread\":",
        "        return threading.Thread(lambda: hold.wait())",
        "    if host == \"Timer\":",
        "        return threading.Timer(30.0, lambda: None)",
        "    return Sub(hold)",
        "",
        "",
        "def release(t: threading.Thread, hold: threading.Event) -> None:",
        "    hold.set()",
        "    if isinstance(t, threading.Timer):",
        "        t.cancel()",
        "",
        "",
        "def apply(t: threading.Thread, op: str) -> str:",
        "    try:",
        "        if op == \"start()\":",
        "            t.start()",
        "            return \"None\"",
        "        if op == \"join()\":",
        "            t.join()",
        "            return \"None\"",
        "        if op == \"join(0.01)\":",
        "            t.join(0.01)",
        "            return \"None\"",
        "        if op == \"daemon\":",
        "            return str(t.daemon)",
        "        if op == \"daemon = True\":",
        "            t.daemon = True",
        "            return \"None\"",
        "        if op == \"is_alive()\":",
        "            return str(t.is_alive())",
        "        t.name = \"x\"",
        "        return str(t.name)",
        "    except RuntimeError:",
        "        return \"RuntimeError\"",
        "",
        "",
        "def main() -> None:",
        "    ops = [\"start()\", \"join()\", \"join(0.01)\", \"daemon\", \"daemon = True\", \"is_alive()\", \"name = \\\"x\\\"\"]",
        "    for host in [\"Thread\", \"Timer\", \"Sub\"]:",
        "        for st in [\"initial\", \"alive\", \"finished\"]:",
        "            for op in ops:",
        "                if st == \"alive\" and op == \"join()\":",
        "                    continue",
        "                hold = threading.Event()",
        "                t = mk(host, hold)",
        "                if st != \"initial\":",
        "                    t.start()",
        "                if st == \"finished\":",
        "                    release(t, hold)",
        "                    t.join()",
        "                print(f\"{host} {st} {op} -> {apply(t, op)}\")",
        "                release(t, hold)",
        "                if st != \"initial\":",
        "                    t.join()",
        "    for op in ops:",
        "        print(f\"main current_thread() {op} -> {apply(threading.current_thread(), op)}\")",
        "    for op in ops:",
        "        out: list[str] = []",
        "        w = threading.Thread(lambda: out.append(apply(threading.current_thread(), op)))",
        "        w.start()",
        "        w.join()",
        "        print(f\"worker current_thread() {op} -> {out[0]}\")",
        "    for op in ops:",
        "        out2: list[str] = []",
        "        box: list[threading.Thread] = []",
        "        w2 = threading.Thread(lambda: out2.append(apply(box[0], op)))",
        "        box.append(w2)",
        "        w2.start()",
        "        w2.join()",
        "        print(f\"worker self {op} -> {out2[0]}\")"
    };

    private static readonly string[] PythonOutput =
    {
        "Thread initial start() -> None",
        "Thread initial join() -> RuntimeError",
        "Thread initial join(0.01) -> RuntimeError",
        "Thread initial daemon -> False",
        "Thread initial daemon = True -> None",
        "Thread initial is_alive() -> False",
        "Thread initial name = \"x\" -> x",
        "Thread alive start() -> RuntimeError",
        "Thread alive join(0.01) -> None",
        "Thread alive daemon -> False",
        "Thread alive daemon = True -> RuntimeError",
        "Thread alive is_alive() -> True",
        "Thread alive name = \"x\" -> x",
        "Thread finished start() -> RuntimeError",
        "Thread finished join() -> None",
        "Thread finished join(0.01) -> None",
        "Thread finished daemon -> False",
        "Thread finished daemon = True -> RuntimeError",
        "Thread finished is_alive() -> False",
        "Thread finished name = \"x\" -> x",
        "Timer initial start() -> None",
        "Timer initial join() -> RuntimeError",
        "Timer initial join(0.01) -> RuntimeError",
        "Timer initial daemon -> False",
        "Timer initial daemon = True -> None",
        "Timer initial is_alive() -> False",
        "Timer initial name = \"x\" -> x",
        "Timer alive start() -> RuntimeError",
        "Timer alive join(0.01) -> None",
        "Timer alive daemon -> False",
        "Timer alive daemon = True -> RuntimeError",
        "Timer alive is_alive() -> True",
        "Timer alive name = \"x\" -> x",
        "Timer finished start() -> RuntimeError",
        "Timer finished join() -> None",
        "Timer finished join(0.01) -> None",
        "Timer finished daemon -> False",
        "Timer finished daemon = True -> RuntimeError",
        "Timer finished is_alive() -> False",
        "Timer finished name = \"x\" -> x",
        "Sub initial start() -> None",
        "Sub initial join() -> RuntimeError",
        "Sub initial join(0.01) -> RuntimeError",
        "Sub initial daemon -> False",
        "Sub initial daemon = True -> None",
        "Sub initial is_alive() -> False",
        "Sub initial name = \"x\" -> x",
        "Sub alive start() -> RuntimeError",
        "Sub alive join(0.01) -> None",
        "Sub alive daemon -> False",
        "Sub alive daemon = True -> RuntimeError",
        "Sub alive is_alive() -> True",
        "Sub alive name = \"x\" -> x",
        "Sub finished start() -> RuntimeError",
        "Sub finished join() -> None",
        "Sub finished join(0.01) -> None",
        "Sub finished daemon -> False",
        "Sub finished daemon = True -> RuntimeError",
        "Sub finished is_alive() -> False",
        "Sub finished name = \"x\" -> x",
        "main current_thread() start() -> RuntimeError",
        "main current_thread() join() -> RuntimeError",
        "main current_thread() join(0.01) -> RuntimeError",
        "main current_thread() daemon -> False",
        "main current_thread() daemon = True -> RuntimeError",
        "main current_thread() is_alive() -> True",
        "main current_thread() name = \"x\" -> x",
        "worker current_thread() start() -> RuntimeError",
        "worker current_thread() join() -> RuntimeError",
        "worker current_thread() join(0.01) -> RuntimeError",
        "worker current_thread() daemon -> False",
        "worker current_thread() daemon = True -> RuntimeError",
        "worker current_thread() is_alive() -> True",
        "worker current_thread() name = \"x\" -> x",
        "worker self start() -> RuntimeError",
        "worker self join() -> RuntimeError",
        "worker self join(0.01) -> RuntimeError",
        "worker self daemon -> False",
        "worker self daemon = True -> RuntimeError",
        "worker self is_alive() -> True",
        "worker self name = \"x\" -> x"
    };

    [Fact]
    public void EveryThreadOperation_InEveryState_GivesPythonsVerdict()
    {
        // A regression in the current-thread check deadlocks the program (join() waits on itself),
        // so the run is bounded; the whole table takes about a second.
        var result = CompileAndExecute(string.Join("\n", Program) + "\n", executionTimeoutMs: 30_000);

        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal(PythonOutput, result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
    }
}
