using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2235 — an async generator expression is refused with SPY0358 at the expression, never an
/// internal error (deviation <c>no-async-generator-expressions</c>). Matrix: <b>what makes it
/// async</b> {an <c>async for</c> first clause, an <c>async for</c> later clause, an <c>await</c> in
/// the element, in a filter, in a non-first iterator, a nested async list comprehension in the
/// element} (CPython 3.12's rule, measured: each is an <c>async_generator</c>) × <b>form</b>
/// {assigned, assigned with a filter, list/set/sum/any argument, plain <c>for</c>, returned,
/// call argument, parenthesis-free sole argument, expression statement, and — in <c>async def</c>
/// — iterated by <c>async for</c> with and without a filter} × <b>source</b> {typed, untyped async
/// generator; the await cells have no async-generator source} × <b>enclosing function</b>
/// {<c>async def</c>, <c>def</c> — CPython accepts an async generator expression in a plain
/// <c>def</c>} = 216 cells.
///
/// <para><b>Base classification (measured with <c>sharpyc run</c> at b4d268dc1):</b> 111 cells
/// were internal errors (SPY0908 CS1503/CS8415, SPY0599 "Keyword 'void'", SPY0524), 105 were
/// refused by something else — every <c>def</c> cell by SPY0273 "'async for' can only be used
/// inside 'async def'", some with a cascade — and none ran.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class AsyncGeneratorExpressionRefusalMatrixTests : IntegrationTestBase
{
    public AsyncGeneratorExpressionRefusalMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Prelude =
        "async def aw(x: int) -> int:\n    return x\n\n"
        + "async def alist() -> list[int]:\n    return [0, 1, 2]\n\n"
        + "def consume(x: object) -> None:\n    print(\"consumed\")\n\n";

    private static readonly Dictionary<string, string> Sources = new()
    {
        ["typed"] = "async def agen() -> int:\n    for i in range(3):\n        yield i\n\n",
        ["untyped"] = "async def agen():\n    for i in range(3):\n        yield i\n\n",
    };

    /// <summary>Generator body without its parentheses, and the same body with a filter.</summary>
    private static readonly Dictionary<string, (string Plain, string Filtered)> AsyncKinds = new()
    {
        ["async_for_first"] = ("i async for i in agen()", "i async for i in agen() if i > 0"),
        ["async_for_later"] = ("j for i in range(1) async for j in agen()", "j for i in range(1) async for j in agen() if j > 0"),
        ["await_element"] = ("await aw(i) for i in range(3)", "await aw(i) for i in range(3) if i > 0"),
        ["await_filter"] = ("i for i in range(3) if await aw(i)", "i for i in range(3) if await aw(i) if i > 0"),
        ["await_later_iterator"] = ("j for i in range(1) for j in await alist()", "j for i in range(1) for j in await alist() if j > 0"),
        ["nested_async_comprehension"] = ("[j async for j in agen()] for i in range(2)", "[j async for j in agen()] for i in range(2) if i > 0"),
    };

    /// <summary>Kinds whose async-ness does not come from an async generator source.</summary>
    private static readonly HashSet<string> SourcelessKinds = new() { "await_element", "await_filter", "await_later_iterator" };

    private static readonly string[] Forms =
    {
        "assign", "assign_filter", "list", "set", "sum", "any", "sync_for", "return", "arg", "parenfree",
        "exprstmt", "async_for", "async_for_filter",
    };

    public static IEnumerable<object[]> Cells() =>
        from kind in AsyncKinds.Keys
        from form in Forms
        from source in Sources.Keys
        from enclosing in new[] { "async", "sync" }
        where !(SourcelessKinds.Contains(kind) && source == "untyped")
        where !(form.StartsWith("async_for", StringComparison.Ordinal) && enclosing == "sync")
        select new object[] { kind, form, source, enclosing };

    /// <summary>The cell's program and the exact text of its generator expression.</summary>
    private static (string Source, string GenExpr) Program(string kind, string form, string source, string enclosing)
    {
        var (plain, filtered) = AsyncKinds[kind];
        var g = $"({plain})";
        var gf = $"({filtered})";
        var (body, genExpr) = form switch
        {
            "assign" => ($"    g = {g}\n    print(\"made\")\n", g),
            "assign_filter" => ($"    g = {gf}\n    print(\"made\")\n", gf),
            "list" => ($"    print(list({g}))\n", g),
            "set" => ($"    print(set({g}))\n", g),
            "sum" => ($"    print(sum({g}))\n", g),
            "any" => ($"    print(any({g}))\n", g),
            "sync_for" => ($"    out: list[object] = []\n    for v in {g}:\n        out.append(v)\n    print(out)\n", g),
            "return" => ($"    return {g}\n", g),
            "arg" => ($"    consume({g})\n", g),
            "parenfree" => ($"    print(list({plain}))\n", plain),
            "exprstmt" => ($"    {g}\n    print(\"made\")\n", g),
            "async_for" => ($"    out: list[object] = []\n    async for v in {g}:\n        out.append(v)\n    print(out)\n", g),
            "async_for_filter" => ($"    out: list[object] = []\n    async for v in {gf}:\n        out.append(v)\n    print(out)\n", gf),
            _ => throw new ArgumentOutOfRangeException(nameof(form)),
        };
        var def = enclosing == "async" ? "async def" : "def";
        var ret = form == "return" ? " -> object" : "";
        var call = (form == "return" ? "r = " : "") + (enclosing == "async" ? "await cell()" : "cell()");
        var program = Sources[source] + Prelude
            + $"{def} cell(){ret}:\n" + body + "\n"
            + $"async def main():\n    {call}\n";
        return (program, genExpr);
    }

    private static (int Line, int Column) LocationOf(string program, string text)
    {
        var lines = program.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var col = lines[i].IndexOf(text, StringComparison.Ordinal);
            if (col >= 0)
                return (i + 1, col + 1);
        }

        throw new InvalidOperationException($"'{text}' not found in the program");
    }

    private static string Render(ExecutionResult result) =>
        string.Join("\n", result.RawDiagnostics.Select(d => $"{d.Code} @{d.Line}:{d.Column} {d.Message}"))
        + "\n" + string.Join("\n", result.CompilationErrors);

    [Theory]
    [MemberData(nameof(Cells))]
    public void AsyncGeneratorExpression_IsRefusedAtTheExpression(string kind, string form, string source, string enclosing)
    {
        var (program, genExpr) = Program(kind, form, source, enclosing);
        var (line, column) = LocationOf(program, genExpr);

        var result = CompileAndExecute(program);
        var because = $"{program}\n--- diagnostics ---\n{Render(result)}";

        result.Success.Should().BeFalse(because);
        var errors = result.RawDiagnostics.Where(d => d.IsError).ToList();

        // No internal error and no "'async for'/'await' only inside async def" in its place.
        errors.Should().NotContain(d => d.Code == "SPY0599" || d.Code == "SPY0908" || d.Code == "SPY0524", because);
        errors.Should().NotContain(d => d.Code == "SPY0273", because);
        result.CompilationErrors.Should().NotContain(e => e.Contains("nternal error", StringComparison.Ordinal), because);

        errors.Should().NotBeEmpty(because);
        var first = errors[0];
        first.Code.Should().Be("SPY0358", because);
        first.Message.Should().StartWith("Async generator expressions are not supported", because);
        first.Message.Should().Contain("[x async for x in source]", because);
        first.Line.Should().Be(line, because);
        first.Column.Should().Be(column, because);

        // any()/sum() of an error-recovery Unknown argument add their own refusal — the same
        // second diagnostic `any(undefined_name)` produces, a pre-existing cascade of the generic and
        // overloaded builtin call seams outside this refusal. Every other form reports exactly one.
        if (form is "any" or "sum")
            errors.Skip(1).Select(d => d.Code).Should().BeEquivalentTo(new[] { form == "any" ? "SPY0237" : "SPY0354" }, because);
        else
            errors.Should().HaveCount(1, because);
    }

    /// <summary>
    /// A nested async generator expression is its own scope: the outer generator stays synchronous
    /// (CPython: <c>generator</c>), so the one refusal is at the INNER expression.
    /// </summary>
    [Fact]
    public void NestedAsyncGeneratorExpression_IsRefusedOnceAtTheInnerExpression()
    {
        var program = Sources["typed"]
            + "async def main():\n    g = ((j async for j in agen()) for i in range(2))\n    print(\"made\")\n";

        var result = CompileAndExecute(program);
        var because = $"{program}\n--- diagnostics ---\n{Render(result)}";

        var errors = result.RawDiagnostics.Where(d => d.IsError).ToList();
        errors.Should().ContainSingle(because);
        errors[0].Code.Should().Be("SPY0358", because);
        errors[0].Line.Should().Be(6, because);
        errors[0].Column.Should().Be(10, because);
    }

    /// <summary>
    /// An <c>await</c> in the FIRST iterator is evaluated in the enclosing function, so the
    /// generator expression is synchronous (CPython: <c>generator</c>) and is not refused. Positive
    /// control: the matrix's <c>await_later_iterator</c> cells, where the same <c>await</c> one
    /// clause later is refused.
    /// </summary>
    [Fact]
    public void AwaitInTheFirstIterator_IsNotAnAsyncGeneratorExpression()
    {
        var program = Prelude + "async def main():\n    g = (x + 1 for x in await alist())\n    print(\"made\")\n";

        var result = CompileAndExecute(program);

        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0358", Render(result));
    }

    [Fact]
    public void AsyncComprehensions_StillRun()
    {
        var program = Sources["typed"] + Prelude + """
            async def main():
                print([i async for i in agen()])
                print([i async for i in agen() if i > 0])
                print(sorted({i % 2 async for i in agen()}))
                print(sorted({i async for i in agen() if i != 1}))
                print({i: i * i async for i in agen()})
                print({i: i * i async for i in agen() if i % 2 == 0})
                print([await aw(i) + 100 for i in range(3)])
                print([i for i in range(5) if await aw(i) > 2])
                print([j for i in range(2) async for j in agen()])
                print([sum(j for j in range(i)) async for i in agen()])
            """;

        var result = CompileAndExecute(program);

        result.Success.Should().BeTrue(Render(result));
        result.StandardOutput.Should().Be(
            "[0, 1, 2]\n[1, 2]\n[0, 1]\n[0, 2]\n{0: 0, 1: 1, 2: 4}\n{0: 0, 2: 4}\n"
            + "[100, 101, 102]\n[3, 4]\n[0, 1, 2, 0, 1, 2]\n[0, 0, 1]\n");
    }

    [Fact]
    public void SynchronousGeneratorExpressions_StillRun()
    {
        var program = Prelude + """
            def mk() -> object:
                return (i * 3 for i in range(3))

            async def main():
                g = (i * 2 for i in range(4))
                print(list(g))
                print(list((i for i in range(6) if i % 2 == 0)))
                print(set((i % 3 for i in range(7))))
                print(sum((i for i in range(5))))
                print(any((i > 3 for i in range(5))))
                print(sum(i * i for i in range(4)))
                out: list[int] = []
                for v in (i + 1 for i in range(3)):
                    out.append(v)
                print(out)
                consume((i for i in range(2)))
                print(mk() is not None)
                f = lambda n: int: (k for k in range(n))
                print(list(f(3)))
            """;

        var result = CompileAndExecute(program);

        result.Success.Should().BeTrue(Render(result));
        result.StandardOutput.Should().Be(
            "[0, 2, 4, 6]\n[0, 2, 4]\n{0, 1, 2}\n10\nTrue\n14\n[1, 2, 3]\nconsumed\nTrue\n[0, 1, 2]\n");
    }
}
