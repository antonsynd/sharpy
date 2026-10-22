using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Integration;

/// <summary>
/// #2261: every <c>threading.Thread</c> operation gives python's value, or raises python's exception
/// type, which a Sharpy <c>except RuntimeError</c> catches. Matrix: operation {<c>start()</c>,
/// <c>join()</c>, <c>join(0.01)</c>, <c>daemon</c> get, <c>daemon = True</c>, <c>is_alive()</c>,
/// <c>name</c> get, <c>name = "x"</c> read back} × state {initial, alive, finished} × host
/// {<c>Thread</c>, <c>Timer</c>, a subclass}, plus the <c>current_thread()</c> wrapper on the main
/// thread and on a worker, and a worker that operates on its own <c>Thread</c> object. Every cell
/// constructs a fresh thread; the alive state is held by an <c>Event</c> (a <c>Timer</c> by its
/// 30 s interval), so no cell depends on timing. <c>join()</c> on an alive thread is not a cell: it
/// waits for the thread to finish, which <c>join(0.01)</c> covers without blocking.
/// <para>Part 2 (process-wide state) adds: module function {<c>current_thread()</c>,
/// <c>main_thread()</c>, <c>enumerate()</c>, <c>active_count()</c>} × caller {main thread, worker}
/// and after the worker's join; membership in <c>enumerate()</c> and <c>active_count()</c> for each
/// host in each state; and, on each host's own thread, that <c>current_thread()</c> IS the host
/// object. Every default name is checked as a number: <c>Thread-N</c> counts the unnamed threads
/// constructed, so a construction that consumed no number, or two, shifts every later row.</para>
/// <para>Expected lines are the output of the same program written in python, run by python3 3.12.13
/// (part 1 on 2026-10-06, part 2 on 2026-10-22; python is the generator; the twin is this program
/// line for line with python spellings: <c>target=</c> for the positional target, no
/// <c>@override</c>). Python names a target thread <c>Thread-N (target)</c>; Sharpy names it
/// <c>Thread-N</c> until the <c> (target)</c> suffix lands, which is a cell on #2105 (ruling R-FD), so
/// the assertion removes exactly <c> (&lt;lambda&gt;)</c> — every target here is a lambda — from the
/// python lines and nothing else. Before #2261 the first <c>join()</c> before <c>start()</c>
/// escaped as an unhandled <c>System.Threading.ThreadStateException</c>, as did
/// <c>daemon = True</c> and the <c>daemon</c> read on a finished thread; <c>join()</c> on the
/// current thread hung; and <c>is_alive</c> was a property, so <c>t.is_alive()</c> was refused
/// (SPY0230). Before part 2, <c>name</c> was <c>None</c> on every host and on both wrappers,
/// <c>main_thread()</c> on a worker was the worker, <c>enumerate()</c> held only the caller, and
/// <c>active_count()</c> counted the process's OS threads (9 after the join, python 1).</para>
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
        "class Rec(threading.Thread):",
        "    seen: list[str]",
        "",
        "    def __init__(self, seen: list[str]):",
        "        super().__init__()",
        "        self.seen = seen",
        "",
        "    @override",
        "    def run(self) -> None:",
        "        self.seen.append(inside_row(self))",
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
        "        if op == \"name\":",
        "            return t.name",
        "        t.name = \"x\"",
        "        return str(t.name)",
        "    except RuntimeError:",
        "        return \"RuntimeError\"",
        "",
        "",
        "def module_rows(caller: str) -> list[str]:",
        "    rows: list[str] = []",
        "    rows.append(f\"{caller} current_thread().name -> {threading.current_thread().name}\")",
        "    rows.append(f\"{caller} main_thread().name -> {threading.main_thread().name}\")",
        "    rows.append(f\"{caller} main_thread() is current_thread() -> {threading.main_thread() is threading.current_thread()}\")",
        "    rows.append(f\"{caller} main_thread().ident == current_thread().ident -> {threading.main_thread().ident == threading.current_thread().ident}\")",
        "    rows.append(f\"{caller} main_thread().is_alive() -> {threading.main_thread().is_alive()}\")",
        "    rows.append(f\"{caller} main_thread().daemon -> {threading.main_thread().daemon}\")",
        "    rows.append(f\"{caller} active_count() -> {threading.active_count()}\")",
        "    rows.append(f\"{caller} len(enumerate()) -> {len(threading.enumerate())}\")",
        "    names: list[str] = []",
        "    for th in threading.enumerate():",
        "        names.append(th.name)",
        "    names.sort()",
        "    rows.append(f\"{caller} enumerate() names -> {', '.join(names)}\")",
        "    return rows",
        "",
        "",
        "def inside_row(t: threading.Thread) -> str:",
        "    me = threading.current_thread()",
        "    return f\"current_thread() is it {me is t}, in enumerate() {t in threading.enumerate()}, name {me.name}, active_count() {threading.active_count()}\"",
        "",
        "",
        "def inside(host: str) -> str:",
        "    seen: list[str] = []",
        "    box: list[threading.Thread] = []",
        "    t: threading.Thread = Rec(seen)",
        "    if host == \"Thread\":",
        "        t = threading.Thread(lambda: seen.append(inside_row(box[0])))",
        "    if host == \"Timer\":",
        "        t = threading.Timer(0.0, lambda: seen.append(inside_row(box[0])))",
        "    box.append(t)",
        "    t.start()",
        "    t.join()",
        "    return seen[0]",
        "",
        "",
        "def main() -> None:",
        "    for row in module_rows(\"main\"):",
        "        print(row)",
        "    from_worker: list[list[str]] = []",
        "    w0 = threading.Thread(lambda: from_worker.append(module_rows(\"worker\")))",
        "    w0.start()",
        "    w0.join()",
        "    for row in from_worker[0]:",
        "        print(row)",
        "    print(f\"main after join active_count() -> {threading.active_count()}\")",
        "    print(f\"main after join len(enumerate()) -> {len(threading.enumerate())}\")",
        "    for host in [\"Thread\", \"Timer\", \"Sub\"]:",
        "        hold = threading.Event()",
        "        t = mk(host, hold)",
        "        print(f\"{host} initial in enumerate() -> {t in threading.enumerate()}, active_count() {threading.active_count()}\")",
        "        t.start()",
        "        print(f\"{host} alive in enumerate() -> {t in threading.enumerate()}, active_count() {threading.active_count()}\")",
        "        release(t, hold)",
        "        t.join()",
        "        print(f\"{host} finished in enumerate() -> {t in threading.enumerate()}, active_count() {threading.active_count()}\")",
        "    for host in [\"Thread\", \"Timer\", \"Sub\"]:",
        "        print(f\"{host} current thread -> {inside(host)}\")",
        "    ops = [\"start()\", \"join()\", \"join(0.01)\", \"daemon\", \"daemon = True\", \"is_alive()\", \"name\", \"name = \\\"x\\\"\"]",
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
        "        print(f\"worker self {op} -> {out2[0]}\")",
        "    for op in ops:",
        "        print(f\"main current_thread() {op} -> {apply(threading.current_thread(), op)}\")",
        "    print(f\"main main_thread().name after the rename -> {threading.main_thread().name}\")"
    };

    private static readonly string[] PythonOutput =
    {
        "main current_thread().name -> MainThread",
        "main main_thread().name -> MainThread",
        "main main_thread() is current_thread() -> True",
        "main main_thread().ident == current_thread().ident -> True",
        "main main_thread().is_alive() -> True",
        "main main_thread().daemon -> False",
        "main active_count() -> 1",
        "main len(enumerate()) -> 1",
        "main enumerate() names -> MainThread",
        "worker current_thread().name -> Thread-1 (<lambda>)",
        "worker main_thread().name -> MainThread",
        "worker main_thread() is current_thread() -> False",
        "worker main_thread().ident == current_thread().ident -> False",
        "worker main_thread().is_alive() -> True",
        "worker main_thread().daemon -> False",
        "worker active_count() -> 2",
        "worker len(enumerate()) -> 2",
        "worker enumerate() names -> MainThread, Thread-1 (<lambda>)",
        "main after join active_count() -> 1",
        "main after join len(enumerate()) -> 1",
        "Thread initial in enumerate() -> False, active_count() 1",
        "Thread alive in enumerate() -> True, active_count() 2",
        "Thread finished in enumerate() -> False, active_count() 1",
        "Timer initial in enumerate() -> False, active_count() 1",
        "Timer alive in enumerate() -> True, active_count() 2",
        "Timer finished in enumerate() -> False, active_count() 1",
        "Sub initial in enumerate() -> False, active_count() 1",
        "Sub alive in enumerate() -> True, active_count() 2",
        "Sub finished in enumerate() -> False, active_count() 1",
        "Thread current thread -> current_thread() is it True, in enumerate() True, name Thread-6 (<lambda>), active_count() 2",
        "Timer current thread -> current_thread() is it True, in enumerate() True, name Thread-8, active_count() 2",
        "Sub current thread -> current_thread() is it True, in enumerate() True, name Thread-9, active_count() 2",
        "Thread initial start() -> None",
        "Thread initial join() -> RuntimeError",
        "Thread initial join(0.01) -> RuntimeError",
        "Thread initial daemon -> False",
        "Thread initial daemon = True -> None",
        "Thread initial is_alive() -> False",
        "Thread initial name -> Thread-16 (<lambda>)",
        "Thread initial name = \"x\" -> x",
        "Thread alive start() -> RuntimeError",
        "Thread alive join(0.01) -> None",
        "Thread alive daemon -> False",
        "Thread alive daemon = True -> RuntimeError",
        "Thread alive is_alive() -> True",
        "Thread alive name -> Thread-23 (<lambda>)",
        "Thread alive name = \"x\" -> x",
        "Thread finished start() -> RuntimeError",
        "Thread finished join() -> None",
        "Thread finished join(0.01) -> None",
        "Thread finished daemon -> False",
        "Thread finished daemon = True -> RuntimeError",
        "Thread finished is_alive() -> False",
        "Thread finished name -> Thread-31 (<lambda>)",
        "Thread finished name = \"x\" -> x",
        "Timer initial start() -> None",
        "Timer initial join() -> RuntimeError",
        "Timer initial join(0.01) -> RuntimeError",
        "Timer initial daemon -> False",
        "Timer initial daemon = True -> None",
        "Timer initial is_alive() -> False",
        "Timer initial name -> Thread-39",
        "Timer initial name = \"x\" -> x",
        "Timer alive start() -> RuntimeError",
        "Timer alive join(0.01) -> None",
        "Timer alive daemon -> False",
        "Timer alive daemon = True -> RuntimeError",
        "Timer alive is_alive() -> True",
        "Timer alive name -> Thread-46",
        "Timer alive name = \"x\" -> x",
        "Timer finished start() -> RuntimeError",
        "Timer finished join() -> None",
        "Timer finished join(0.01) -> None",
        "Timer finished daemon -> False",
        "Timer finished daemon = True -> RuntimeError",
        "Timer finished is_alive() -> False",
        "Timer finished name -> Thread-54",
        "Timer finished name = \"x\" -> x",
        "Sub initial start() -> None",
        "Sub initial join() -> RuntimeError",
        "Sub initial join(0.01) -> RuntimeError",
        "Sub initial daemon -> False",
        "Sub initial daemon = True -> None",
        "Sub initial is_alive() -> False",
        "Sub initial name -> Thread-62",
        "Sub initial name = \"x\" -> x",
        "Sub alive start() -> RuntimeError",
        "Sub alive join(0.01) -> None",
        "Sub alive daemon -> False",
        "Sub alive daemon = True -> RuntimeError",
        "Sub alive is_alive() -> True",
        "Sub alive name -> Thread-69",
        "Sub alive name = \"x\" -> x",
        "Sub finished start() -> RuntimeError",
        "Sub finished join() -> None",
        "Sub finished join(0.01) -> None",
        "Sub finished daemon -> False",
        "Sub finished daemon = True -> RuntimeError",
        "Sub finished is_alive() -> False",
        "Sub finished name -> Thread-77",
        "Sub finished name = \"x\" -> x",
        "worker current_thread() start() -> RuntimeError",
        "worker current_thread() join() -> RuntimeError",
        "worker current_thread() join(0.01) -> RuntimeError",
        "worker current_thread() daemon -> False",
        "worker current_thread() daemon = True -> RuntimeError",
        "worker current_thread() is_alive() -> True",
        "worker current_thread() name -> Thread-85 (<lambda>)",
        "worker current_thread() name = \"x\" -> x",
        "worker self start() -> RuntimeError",
        "worker self join() -> RuntimeError",
        "worker self join(0.01) -> RuntimeError",
        "worker self daemon -> False",
        "worker self daemon = True -> RuntimeError",
        "worker self is_alive() -> True",
        "worker self name -> Thread-93 (<lambda>)",
        "worker self name = \"x\" -> x",
        "main current_thread() start() -> RuntimeError",
        "main current_thread() join() -> RuntimeError",
        "main current_thread() join(0.01) -> RuntimeError",
        "main current_thread() daemon -> False",
        "main current_thread() daemon = True -> RuntimeError",
        "main current_thread() is_alive() -> True",
        "main current_thread() name -> MainThread",
        "main current_thread() name = \"x\" -> x",
        "main main_thread().name after the rename -> x"
    };

    /// <summary>
    /// #2261 part 3 (R-IG): an exception that escapes a thread's target or <c>run()</c> is reported
    /// on stderr and the process continues — python's <c>threading.excepthook</c>. On .NET an
    /// unhandled exception on any thread ended the process: <c>Unhandled exception.
    /// Sharpy.ValueError: boom</c>, exit 134, <c>main</c>'s next line never printed.
    /// Matrix: host {<c>Thread</c> target, <c>Timer</c> function, subclass <c>run</c>} with a Sharpy
    /// exception, a CLR exception on a target, <c>SystemExit</c> (python's hook is silent), and the
    /// <c>Timer(1e10)</c> cell (its wait raises <c>OverflowError</c> inside the timer thread,
    /// #2263); plus daemon inheritance — <c>daemon</c> left as <c>None</c> takes the creating
    /// thread's flag. COMPARED: the exit code, every <c>main</c> line, and each report's first and
    /// last line. The traceback frames between them are deviation text (R-CI) and Sharpy prints
    /// none.
    /// <para>Expected lines are python3 3.12.13's (2026-10-22) for the same program less the CLR row
    /// (python cannot raise one). That row adds one thread, so the later default name is
    /// <c>Thread-6</c> where python's is <c>Thread-5</c>. Python's <c> (boom)</c> target suffix is
    /// removed, as in <see cref="EveryThreadOperation_InEveryState_GivesPythonsVerdict"/> (R-FD,
    /// #2105).</para>
    /// </summary>
    [Fact]
    public void AnExceptionInAThread_IsReportedOnStderr_AndTheProcessContinues()
    {
        var program = string.Join("\n", new[]
        {
            "import threading",
            "from system import InvalidOperationException",
            "",
            "",
            "class Boom(threading.Thread):",
            "    @override",
            "    def run(self) -> None:",
            "        raise KeyError(\"k\")",
            "",
            "",
            "def boom() -> None:",
            "    raise ValueError(\"boom\")",
            "",
            "",
            "def clr() -> None:",
            "    raise InvalidOperationException(\"clr\")",
            "",
            "",
            "def leave() -> None:",
            "    raise SystemExit(3)",
            "",
            "",
            "def main() -> None:",
            "    t = threading.Thread(boom)",
            "    t.start()",
            "    t.join()",
            "    print(\"after Thread target\")",
            "    tm = threading.Timer(0.01, boom)",
            "    tm.start()",
            "    tm.join()",
            "    print(\"after Timer function\")",
            "    s = Boom()",
            "    s.start()",
            "    s.join()",
            "    print(\"after subclass run\")",
            "    c = threading.Thread(clr)",
            "    c.start()",
            "    c.join()",
            "    print(\"after CLR exception\")",
            "    e = threading.Thread(leave)",
            "    e.start()",
            "    e.join()",
            "    print(\"after SystemExit\")",
            "    big = threading.Timer(1e10, lambda: None)",
            "    big.start()",
            "    big.join()",
            "    print(\"after Timer(1e10)\")",
            "    seen: list[bool] = []",
            "    d = threading.Thread(lambda: seen.append(threading.Thread(lambda: None).daemon), daemon=True)",
            "    d.start()",
            "    d.join()",
            "    print(f\"child of a daemon thread: daemon {seen[0]}\")",
            "    print(f\"child of main: daemon {threading.Thread(lambda: None).daemon}\")",
        }) + "\n";

        var result = CompileAndExecute(program, executionTimeoutMs: 30_000);

        Assert.True(result.Success,
            $"the process must continue past every thread's exception and exit 0: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        Assert.Equal(new[]
        {
            "after Thread target",
            "after Timer function",
            "after subclass run",
            "after CLR exception",
            "after SystemExit",
            "after Timer(1e10)",
            "child of a daemon thread: daemon True",
            "child of main: daemon False",
        }, result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
        Assert.Equal(new[]
        {
            "Exception in thread Thread-1:",
            "ValueError: boom",
            "Exception in thread Thread-2:",
            "ValueError: boom",
            "Exception in thread Thread-3:",
            "KeyError: 'k'",
            "Exception in thread Thread-4:",
            "InvalidOperationException: clr",
            "Exception in thread Thread-6:",
            "OverflowError: timestamp out of range for platform time_t",
        }, result.StandardError.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
    }

    [Fact]
    public void EveryThreadOperation_InEveryState_GivesPythonsVerdict()
    {
        // A regression in the current-thread check deadlocks the program (join() waits on itself),
        // so the run is bounded; the whole table takes about a second.
        var result = CompileAndExecute(string.Join("\n", Program) + "\n", executionTimeoutMs: 30_000);

        Assert.True(result.Success,
            $"did not compile/run: {string.Join("; ", result.CompilationErrors)}\n{result.StandardError}");
        // R-FD: the ` (target)` suffix of a target thread's default name is a cell on #2105.
        var expected = PythonOutput.Select(line => line.Replace(" (<lambda>)", "")).ToArray();
        Assert.Equal(expected, result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
    }
}
