using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Context-manager exit-model matrix (#1691, #1704, #1745, #1690, plan-0667c5 Phase 1).
///
/// <para><b>The class contract.</b> A <c>with</c> statement's exit behaviour is decided by ONE
/// recorded fact — the exit shape the checker resolved for the manager — and every consumer
/// (reachability, definite assignment, the emitter) reads that fact. Two consequences are the whole
/// matrix: (1) a manager whose <c>__exit__</c> <b>can suppress</b> makes the statement's successor
/// reachable, so a <c>return</c> inside the body is not the function's only exit and a value-returning
/// function needs another one — refused by name with SPY0266, never CS0161 behind SPY0908; (2) a
/// manager that <b>cannot</b> suppress leaves the body's exit unconditional, so the same program
/// runs. The exit runs exactly once on every exit path, in the right order, in all four host
/// constructs.</para>
///
/// <para><b>Axes.</b> exit shape {1-param <c>__exit__</c>, suppression-capable 4-param,
/// <c>IDisposable</c>, async 1-param, async suppression-capable} × body exit {fall-through, return,
/// raise, break, continue, yield} × function return {None, value with another exit, value as the
/// SOLE exit} × host construct {plain, inside a loop, nested <c>with</c>, inside <c>try</c>}.
/// Inexpressible combinations are rostered in <see cref="NotApplicableCells"/> with the reason.</para>
///
/// <para><b>Why these axes are independent.</b> The exit shape is a property of the MANAGER; the
/// body exit is a property of the BODY; the host construct decides which C# construct the lowering
/// nests inside. A defect that keys on one of them (the emitter special-casing the 4-param form, the
/// CFG omitting the suppression edge only in a loop) shows up as a row or a column, not a cell.
/// SPY0266 on the <c>inside-try</c> × <c>sole</c> rows is the discriminator that the refusal follows
/// the reachable-successor RULE rather than the shape: it fires for every shape there, because it is
/// the <c>except</c> path — not the manager — that reaches the end of the function.</para>
///
/// <para><b>Traces are measured, not derived.</b> Every expected trace below was taken with
/// <c>sharpyc run</c> and cross-checked against the python3 3.12 twin for the two shapes that have
/// one (the 1-param and 4-param <c>__exit__</c> forms map onto CPython's
/// <c>__exit__(self, t, v, tb)</c>; <c>IDisposable</c> and the async forms have no CPython twin, and
/// their traces are the 1-param trace minus the <c>enter</c> line / plus the await, which is the
/// whole semantic difference — see <c>TestFixtures/context_managers/exit_order_disposable_normal.spy</c>).</para>
///
/// <para><b>Mutations (recorded in the commit body).</b> Removing the suppression edge from
/// <c>ControlFlowGraphBuilder.BuildWith</c> turns every <c>suppressing.return.*.sole</c> and
/// <c>async-suppressing.return.*.sole</c> cell red (they stop refusing). Removing
/// <c>GeneratorValidator.FindYieldInSuppressingWith</c> turns the three
/// <c>suppressing.yield.*</c> cells red.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ContextManagerExitModelMatrixTests : IntegrationTestBase
{
    public ContextManagerExitModelMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── Axes ──

    private enum Shape { Simple, Suppressing, Disposable, AsyncSimple, AsyncSuppressing }

    private enum BodyExit { FallThrough, Return, Raise, Break, Continue, Yield }

    /// <summary>
    /// <c>Sole</c> is the discriminating value: the <c>return</c> in the body is the only
    /// <c>return</c> in a value-returning function, so whether the program is legal depends entirely
    /// on whether the <c>with</c>'s successor is reachable.
    /// </summary>
    private enum FunctionReturn { None, ValueWithTail, Sole }

    private enum Host { Plain, InLoop, NestedWith, InsideTry }

    /// <summary>What a cell must do: a trace to match, or a diagnostic code to be refused with.</summary>
    private sealed record Cell(string Label, string Source, string? ExpectedTrace, string? RefusalCode);

    [Fact]
    [Trait("Category", "Conformance")]
    public void ExitModelMatrix_AllCellsPass()
    {
        var cells = GenerateCells().ToList();
        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        var failures = new List<string>();
        foreach (var cell in cells)
        {
            var result = CompileAndExecute(cell.Source, executionTimeoutMs: 15_000);

            if (cell.RefusalCode != null)
            {
                if (result.Success)
                {
                    failures.Add($"{cell.Label}: expected {cell.RefusalCode} but compiled and ran "
                                 + $"('{result.StandardOutput.Trim().Replace("\n", " | ")}')");
                    continue;
                }
                if (!result.RawDiagnostics.Any(d => d.Code == cell.RefusalCode))
                {
                    failures.Add($"{cell.Label}: expected {cell.RefusalCode} but got "
                                 + string.Join("; ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
                }
                // Never a C#-compile refusal wearing a diagnostic: SPY0908 means the generated C#
                // did not compile, which is the failure mode the named refusals replace.
                if (result.RawDiagnostics.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError))
                    failures.Add($"{cell.Label}: refused with SPY0908 (generated C# did not compile)");
                continue;
            }

            if (!result.Success)
            {
                failures.Add($"{cell.Label}: expected to run but failed: "
                             + string.Join("; ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
                continue;
            }

            var actual = string.Join(" | ", result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.TrimEnd('\r')));
            if (actual != cell.ExpectedTrace)
                failures.Add($"{cell.Label}: trace mismatch\n    expected: {cell.ExpectedTrace}\n    actual:   {actual}");
        }

        Output.WriteLine($"Exit-model cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var (label, reason) in NotApplicableCells())
            Output.WriteLine($"  N/A {label}: {reason}");
        foreach (var f in failures)
            Output.WriteLine($"  {f}");

        Assert.True(failures.Count == 0,
            $"Context-manager exit model (#1691, #1704, #1745): {failures.Count} of {cells.Count} "
            + "cells failed.\n" + string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// The matrix has the size its axes say, so a cell cannot vanish through a silent change to
    /// <see cref="NotApplicable"/>. Pinned to a literal, not to the generator's own count (§2).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void CellCount_IsPinned()
    {
        Assert.Equal(114, GenerateCells().Count());
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void NotApplicableReasons_AreNonEmpty()
    {
        var na = NotApplicableCells().ToList();
        Assert.NotEmpty(na);
        foreach (var (label, reason) in na)
            Assert.False(string.IsNullOrWhiteSpace(reason), $"N/A cell '{label}' has no reason.");
    }

    // ── Cell generation ──

    private static IEnumerable<(string label, string reason)> NotApplicableCells()
    {
        yield return ("*.break/continue.plain|nested-with|inside-try.*",
            "break and continue need an enclosing loop; the host axis supplies it only at in-loop");
        yield return ("async-*.yield.*",
            "an async generator is a separate protocol (IAsyncEnumerable), not an exit shape of this matrix");
        yield return ("*.yield.*.value|sole",
            "a generator declares its ELEMENT type, so the function-return axis does not apply");
        yield return ("*.fallthrough|raise|break|continue.*.value|sole",
            "the function-return axis only changes the rule on a `return` exit; the other exits are "
            + "measured on the None row");
        yield return ("*.return.in-loop.sole",
            "a return inside a loop is never the function's sole exit — the loop can run zero times");
        yield return ("*.yield.inside-try.*",
            "yield inside try/except is its own refusal family (SPY0272), measured by the generator suite");
    }

    private static string? NotApplicable(Shape shape, BodyExit exit, Host host, FunctionReturn ret)
    {
        if (exit is BodyExit.Break or BodyExit.Continue && host != Host.InLoop)
            return "break/continue need an enclosing loop";
        if (exit == BodyExit.Yield && UsesAsyncProtocol(shape))
            return "an async generator is a separate protocol, not an exit shape";
        if (exit == BodyExit.Yield && ret != FunctionReturn.None)
            return "a generator declares its element type; the function-return axis does not apply";
        if (exit != BodyExit.Return && ret != FunctionReturn.None)
            return "the function-return axis only changes the rule on a `return` exit";
        if (ret == FunctionReturn.Sole && host == Host.InLoop)
            return "a return inside a loop is never the function's sole exit";
        if (exit == BodyExit.Yield && host == Host.InsideTry)
            return "yield inside try/except is a separate refusal family (SPY0272)";
        return null;
    }

    private static bool UsesAsyncProtocol(Shape shape)
        => shape is Shape.AsyncSimple or Shape.AsyncSuppressing;

    private static bool CanSuppress(Shape shape)
        => shape is Shape.Suppressing or Shape.AsyncSuppressing;

    /// <summary>The 1-param and async-1-param forms print <c>enter</c>; IDisposable has no enter.</summary>
    private static bool HasEnter(Shape shape) => shape != Shape.Disposable;

    private static IEnumerable<Cell> GenerateCells()
    {
        foreach (var shape in Enum.GetValues<Shape>())
            foreach (var exit in Enum.GetValues<BodyExit>())
                foreach (var host in Enum.GetValues<Host>())
                    foreach (var ret in Enum.GetValues<FunctionReturn>())
                    {
                        if (NotApplicable(shape, exit, host, ret) != null)
                            continue;

                        var label = $"{Label(shape)}.{Label(exit)}.{Label(host)}.{Label(ret)}";
                        yield return new Cell(label, BuildSource(shape, exit, host, ret),
                            ExpectedTrace(shape, exit, host, ret), RefusalCode(shape, exit, host, ret));
                    }
    }

    private static string Label(Shape s) => s switch
    {
        Shape.Simple => "simple",
        Shape.Suppressing => "suppressing",
        Shape.Disposable => "disposable",
        Shape.AsyncSimple => "async-simple",
        Shape.AsyncSuppressing => "async-suppressing",
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };

    private static string Label(BodyExit e) => e.ToString().ToLowerInvariant();

    private static string Label(Host h) => h switch
    {
        Host.Plain => "plain",
        Host.InLoop => "in-loop",
        Host.NestedWith => "nested-with",
        Host.InsideTry => "inside-try",
        _ => throw new ArgumentOutOfRangeException(nameof(h)),
    };

    private static string Label(FunctionReturn r) => r switch
    {
        FunctionReturn.None => "none",
        FunctionReturn.ValueWithTail => "value",
        FunctionReturn.Sole => "sole",
        _ => throw new ArgumentOutOfRangeException(nameof(r)),
    };

    /// <summary>
    /// The refusal rule, stated once: a value-returning function whose ONLY <c>return</c> sits
    /// where the successor is reachable is SPY0266, and <c>yield</c> under a suppression-capable
    /// manager is SPY0703. The successor is reachable when the manager can suppress, and — for
    /// every shape — when an <c>except</c> handler can take over.
    /// </summary>
    private static string? RefusalCode(Shape shape, BodyExit exit, Host host, FunctionReturn ret)
    {
        if (exit == BodyExit.Yield && CanSuppress(shape))
            return DiagnosticCodes.ValidationOverflow.YieldInSuppressingWith;

        if (ret == FunctionReturn.Sole && (CanSuppress(shape) || host == Host.InsideTry))
            return DiagnosticCodes.Semantic.NotAllPathsReturn;

        return null;
    }

    /// <summary>
    /// The expected stdout of a running cell, as <c>" | "</c>-joined lines. Built from the shape's
    /// marker set and the host construct; every trace was verified by execution (and against
    /// python3 for the two shapes with a CPython twin).
    /// </summary>
    private static string? ExpectedTrace(Shape shape, BodyExit exit, Host host, FunctionReturn ret)
    {
        if (RefusalCode(shape, exit, host, ret) != null)
            return null;

        var enter = HasEnter(shape) ? new[] { "enter" } : Array.Empty<string>();
        var parts = new List<string>();

        // One pass of the with statement, from `enter` to `exit`.
        IEnumerable<string> OnePass()
        {
            var pass = new List<string>();
            pass.AddRange(enter);
            if (host == Host.NestedWith)
                pass.AddRange(enter);
            pass.Add("inside");
            if (exit == BodyExit.Yield)
                pass.Add("got 1");
            pass.Add("exit");
            if (host == Host.NestedWith)
                pass.Add("exit");
            return pass;
        }

        // The loop host runs the statement twice, except when the body leaves the loop.
        var passes = host == Host.InLoop && exit is not (BodyExit.Return or BodyExit.Break or BodyExit.Raise)
            ? 2
            : 1;
        for (var i = 0; i < passes; i++)
            parts.AddRange(OnePass());

        // The raise leaves the host; `inside-try` catches it in the host, everything else in main.
        if (exit == BodyExit.Raise)
            parts.Add("caught");

        // The tail after the with/loop runs unless the body already left the function. A generator
        // has no tail at all (its host yields; BuildSource emits none), so the None row of the yield
        // column ends at the exit.
        var bodyLeavesTheFunction = exit == BodyExit.Return
                                    || (exit == BodyExit.Raise && host != Host.InsideTry);
        if (!bodyLeavesTheFunction && exit != BodyExit.Yield && ret == FunctionReturn.None)
            parts.Add("tail");

        if (ret == FunctionReturn.ValueWithTail || ret == FunctionReturn.Sole)
            parts.Add(exit == BodyExit.Return ? "r 9" : "r 0");

        parts.Add("after");
        return string.Join(" | ", parts);
    }

    private static string Indent(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        return string.Join("\n", text.Split('\n').Select(l => l.Length == 0 ? l : pad + l));
    }

    private static string Declaration(Shape shape) => shape switch
    {
        Shape.Simple => """
            class Cm:
                def __enter__(self) -> int:
                    print("enter")
                    return 1

                def __exit__(self) -> None:
                    print("exit")
            """,
        Shape.Suppressing => """
            class Cm:
                def __enter__(self) -> int:
                    print("enter")
                    return 1

                def __exit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool:
                    print("exit")
                    return False
            """,
        Shape.Disposable => """
            from System import IDisposable

            class Cm(IDisposable):
                def dispose(self) -> None:
                    print("exit")
            """,
        Shape.AsyncSimple => """
            class Cm:
                async def __aenter__(self) -> int:
                    print("enter")
                    return 1

                async def __aexit__(self) -> None:
                    print("exit")
            """,
        Shape.AsyncSuppressing => """
            class Cm:
                async def __aenter__(self) -> int:
                    print("enter")
                    return 1

                async def __aexit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool:
                    print("exit")
                    return False
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    private static string BuildSource(Shape shape, BodyExit exit, Host host, FunctionReturn ret)
    {
        var withKeyword = UsesAsyncProtocol(shape) ? "async with" : "with";
        var isGenerator = exit == BodyExit.Yield;

        // A generator declares its ELEMENT type (TestFixtures/generators/bare_tuple_yield.spy).
        var returnType = isGenerator || ret != FunctionReturn.None ? "int" : "None";

        var body = new List<string> { "print(\"inside\")" };
        switch (exit)
        {
            case BodyExit.Return:
                body.Add(ret == FunctionReturn.None ? "return" : "return 9");
                break;
            case BodyExit.Raise:
                body.Add("raise ValueError(\"boom\")");
                break;
            case BodyExit.Break:
                body.Add("break");
                break;
            case BodyExit.Continue:
                body.Add("continue");
                break;
            case BodyExit.Yield:
                body.Add("yield 1");
                break;
        }

        var withBlock = $"{withKeyword} Cm():\n" + Indent(string.Join("\n", body), 4);
        if (host == Host.NestedWith)
            withBlock = $"{withKeyword} Cm():\n" + Indent(withBlock, 4);

        var inner = host switch
        {
            Host.InLoop => "for i in range(2):\n" + Indent(withBlock, 4),
            Host.InsideTry => "try:\n" + Indent(withBlock, 4)
                              + "\nexcept ValueError as e:\n    print(\"caught\")",
            _ => withBlock,
        };

        var tail = ret switch
        {
            FunctionReturn.Sole => null,                    // no exit after the with — the point
            FunctionReturn.ValueWithTail => "return 0",
            _ => isGenerator ? null : "print(\"tail\")",
        };

        var hostKeyword = UsesAsyncProtocol(shape) ? "async def host() -> " : "def host() -> ";
        var hostBody = tail == null ? inner : inner + "\n" + tail;
        var hostDef = hostKeyword + returnType + ":\n" + Indent(hostBody, 4);

        var call = UsesAsyncProtocol(shape) ? "await host()" : "host()";
        string mainBody;
        if (isGenerator)
            mainBody = $"for v in {call}:\n    print(\"got \" + str(v))\nprint(\"after\")";
        else if (ret != FunctionReturn.None)
            mainBody = $"print(\"r \" + str({call}))\nprint(\"after\")";
        else
            mainBody = $"{call}\nprint(\"after\")";

        if (exit == BodyExit.Raise && host != Host.InsideTry)
        {
            mainBody = "try:\n" + Indent(mainBody.Split('\n')[0], 4)
                       + "\nexcept ValueError as e:\n    print(\"caught\")\nprint(\"after\")";
        }

        var mainDef = (UsesAsyncProtocol(shape) ? "async def main() -> None:\n" : "def main() -> None:\n")
                      + Indent(mainBody, 4);

        return Declaration(shape) + "\n\n" + hostDef + "\n\n" + mainDef + "\n";
    }
}
