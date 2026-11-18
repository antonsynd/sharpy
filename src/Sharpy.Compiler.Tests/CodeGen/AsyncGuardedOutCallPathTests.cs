using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// plan-775c50 D4/D6 crossed with the call's emission path, run: a call with an <c>out</c> operand on
/// a Guarded value commits it through the one seam (<c>GenerateCallWithOutCommits</c>) whichever path
/// emits the call, and in an <c>async def</c> body such a call is emitted once per arm, an inline
/// <c>out</c> declaration beside the operand declared once, before the statement, typed as the checker
/// typed the binding (the callee's resolved, substituted parameter).
///
/// <para><b>Generator.</b> <see cref="Path"/> (each emitter path that generates a call's arguments) ×
/// <see cref="Form"/> (the call valued, as its statement, a void callee's as its statement, and nested
/// in another call that reads the stored values after it) × <see cref="Inline"/> (the operand beside
/// the Guarded one: another Guarded value, or an inline <c>out let</c> / <c>auto</c> / annotated
/// declaration), each triple a cell unless
/// <see cref="Excluded"/> says why not. Each cell is an <c>async def</c> and its plain <c>def</c> twin,
/// the control, on values of their own; both must print the model. One project per path.</para>
/// <para><b>Python.</b> No twin: Python has no <c>out</c>.</para>
/// </summary>
public class AsyncGuardedOutCallPathTests
{
    private readonly ITestOutputHelper _output;

    public AsyncGuardedOutCallPathTests(ITestOutputHelper output) => _output = output;

    public enum Path
    {
        Function,
        Pipe,
        Method,
        StaticMethod,
        GenericFunction,
        GenericClassMethod,
        Constructor,
        ConstructorChain,
        KeywordReordered,
    }

    public enum Form
    {
        Valued,
        Statement,
        VoidStatement,
        Nested,
    }

    public enum Inline
    {
        Guarded,
        Let,
        Auto,
        Annotated,
    }

    /// <summary>Why a triple has no cell, or null.</summary>
    internal static string? Excluded(Path path, Form form, Inline inline)
    {
        if (path == Path.KeywordReordered)
            return "#2025: a call whose keyword argument reorders its arguments drops the `out` modifier, in a plain def too (CS1620)";
        if (path == Path.ConstructorChain && form != Form.Statement)
            return "a chaining call `super().__init__(...)` is its constructor's first statement only";
        if (path == Path.Constructor && form == Form.VoidStatement)
            return "a constructor call has a value";
        if (path == Path.Pipe && form == Form.VoidStatement)
            return "a pipe is no call: a statement `x |> f(...)` of type None is refused (SPY0603; findings ledger)";
        if (path == Path.ConstructorChain && inline != Inline.Guarded)
            return "a chaining call's inline `out` local is read by the constructor body only, which the cell does not print";
        return null;
    }

    private static bool IsGeneric(Path path) => path is Path.GenericFunction or Path.GenericClassMethod;

    internal sealed record Cell(Path Path, Form Form, Inline Inline)
    {
        public string Id => $"{Path}/{Form}/{Inline}";
    }

    internal static IReadOnlyList<Cell> Cells() =>
        (from path in Enum.GetValues<Path>()
         from form in Enum.GetValues<Form>()
         from inline in Enum.GetValues<Inline>()
         where Excluded(path, form, inline) == null
         select new Cell(path, form, inline)).ToList();

    /// <summary>One function of a cell (its async use's or its plain twin's) and the values it stores to.</summary>
    private sealed record Use(int Key, Cell Cell)
    {
        private string X => IsGeneric(Cell.Path) ? "str" : "int";
        private string Argument => IsGeneric(Cell.Path) ? "\"s\"" : "3";
        public string A => IsGeneric(Cell.Path) ? "s" : "3";
        private string G => $"g{Key}";
        private string H => $"h{Key}";
        private string AName => Cell.Inline == Inline.Guarded ? H : "v";

        public IEnumerable<string> Declarations()
        {
            yield return $"{G}: int = 1";
            if (Cell.Inline == Inline.Guarded)
                yield return $"{H}: {X} = {(IsGeneric(Cell.Path) ? "\"i\"" : "0")}";
            if (Cell.Path == Path.ConstructorChain)
            {
                yield return $"class Chain{Key}(Base):";
                yield return "    def __init__(self) -> None:";
                yield return $"        super().__init__(3, out {H}, out {G})";
            }
        }

        private string Operand => Cell.Inline switch
        {
            Inline.Guarded => $"out {H}",
            Inline.Let => "out let v",
            Inline.Auto => "out v: auto",
            Inline.Annotated => $"out v: {X}",
            _ => throw new ArgumentOutOfRangeException(),
        };

        /// <summary>The call, valued 10.</summary>
        private string Call => Cell.Path switch
        {
            Path.Function => $"fill({Argument}, {Operand}, out {G})",
            Path.Pipe => $"{Argument} |> fill({Operand}, out {G})",
            Path.Method => $"Box().fill({Argument}, {Operand}, out {G})",
            Path.StaticMethod => $"Box.sfill({Argument}, {Operand}, out {G})",
            Path.GenericFunction => $"gfill({Argument}, {Operand}, out {G})",
            Path.GenericClassMethod => $"Pair[str]().fill({Argument}, {Operand}, out {G})",
            Path.Constructor => $"Made({Argument}, {Operand}, out {G}).r",
            Path.ConstructorChain => $"Chain{Key}().r",
            _ => throw new ArgumentOutOfRangeException(),
        };

        /// <summary>The call of the void twin of the callee (<c>fillv</c>, ...), as a statement.</summary>
        private string VoidCall => Cell.Path switch
        {
            Path.Function => $"fillv({Argument}, {Operand}, out {G})",
            Path.Pipe => $"{Argument} |> fillv({Operand}, out {G})",
            Path.Method => $"Box().fillv({Argument}, {Operand}, out {G})",
            Path.StaticMethod => $"Box.sfillv({Argument}, {Operand}, out {G})",
            Path.GenericFunction => $"gfillv({Argument}, {Operand}, out {G})",
            Path.GenericClassMethod => $"Pair[str]().fillv({Argument}, {Operand}, out {G})",
            _ => throw new ArgumentOutOfRangeException(),
        };

        /// <summary>The call as its own statement: a constructor call is no member read.</summary>
        private string StatementCall => Cell.Path switch
        {
            Path.Constructor => $"Made({Argument}, {Operand}, out {G})",
            Path.ConstructorChain => $"Chain{Key}()",
            _ => Call,
        };

        /// <summary>The cell's body: the call in its form, then the values it stored.</summary>
        public IEnumerable<string> Body() => Cell.Form switch
        {
            Form.Valued => new[] { $"r = {Call}", $"return show(r, {AName}, {G})" },
            Form.Statement => new[] { StatementCall, $"return show(0, {AName}, {G})" },
            Form.VoidStatement => new[] { VoidCall, $"return show(0, {AName}, {G})" },
            Form.Nested => new[] { $"return show({Call}, {AName}, {G})" },
            _ => throw new ArgumentOutOfRangeException(),
        };

        public string Model => $"{(Cell.Form is Form.Statement or Form.VoidStatement ? 0 : 10)} {A} 7";
    }

    private const string Helpers =
        "def show(r: int, a: object, b: int) -> str:\n    return f\"{r} {a} {b}\"\n"
        + "def fill(x: int, a: out int, b: out int) -> int:\n    a = x\n    b = 7\n    return 10\n"
        + "def gfill[T](x: T, a: out T, b: out int) -> int:\n    a = x\n    b = 7\n    return 10\n"
        + "def fillv(x: int, a: out int, b: out int) -> None:\n    a = x\n    b = 7\n"
        + "def gfillv[T](x: T, a: out T, b: out int) -> None:\n    a = x\n    b = 7\n"
        + "class Box:\n"
        + "    def fill(self, x: int, a: out int, b: out int) -> int:\n        a = x\n        b = 7\n        return 10\n"
        + "    def fillv(self, x: int, a: out int, b: out int) -> None:\n        a = x\n        b = 7\n"
        + "    @static\n"
        + "    def sfill(x: int, a: out int, b: out int) -> int:\n        a = x\n        b = 7\n        return 10\n"
        + "    @static\n"
        + "    def sfillv(x: int, a: out int, b: out int) -> None:\n        a = x\n        b = 7\n"
        + "class Pair[T]:\n"
        + "    def fill(self, x: T, a: out T, b: out int) -> int:\n        a = x\n        b = 7\n        return 10\n"
        + "    def fillv(self, x: T, a: out T, b: out int) -> None:\n        a = x\n        b = 7\n"
        + "class Made:\n"
        + "    r: int\n"
        + "    def __init__(self, x: int, a: out int, b: out int) -> None:\n        a = x\n        b = 7\n        self.r = 10\n"
        + "class Base:\n"
        + "    r: int\n"
        + "    def __init__(self, x: int, a: out int, b: out int) -> None:\n        a = x\n        b = 7\n        self.r = 10\n";

    public static IEnumerable<object[]> PathCases()
        => Enum.GetValues<Path>().Where(p => Cells().Any(c => c.Path == p)).Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(PathCases))]
    public void OutCalls_OnEveryEmissionPath_RunAsTheirPlainTwin(Path path)
    {
        var cells = Cells().Where(c => c.Path == path).ToList();
        var key = 0;
        var uses = cells.SelectMany(c => new[] { (Cell: c, Async: true), (Cell: c, Async: false) })
            .Select(x => (x.Cell, x.Async, Use: new Use(key++, x.Cell)))
            .ToList();

        var main = new List<string> { Helpers };
        foreach (var (_, isAsync, use) in uses)
        {
            main.AddRange(use.Declarations());
            main.Add("");
            main.Add($"{(isAsync ? "async def" : "def")} c{use.Key}() -> str:");
            main.AddRange(use.Body().Select(l => "    " + l));
            main.Add("");
        }
        main.Add("async def main() -> None:");
        foreach (var (cell, isAsync, use) in uses)
            main.Add($"    print(\"{cell.Id}{(isAsync ? "" : "/sync")}\", {(isAsync ? "await " : "")}c{use.Key}())");

        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("AsyncGuardedOutCallPath")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("main.spy", string.Join("\n", main) + "\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();
        exec.CompilationErrors.Should().BeEmpty($"every {path} cell compiles");
        exec.Success.Should().BeTrue(exec.StandardError);

        var expected = uses.Select(u => $"{u.Cell.Id}{(u.Async ? "" : "/sync")} {u.Use.Model}").ToList();
        var actual = exec.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();
        var failures = expected.Zip(actual, (e, a) => (e, a)).Where(p => p.e != p.a).Select(p => $"expected '{p.e}', got '{p.a}'").ToList();
        if (expected.Count != actual.Count)
            failures.Add($"{expected.Count} lines expected, {actual.Count} printed");
        _output.WriteLine($"{path}: {cells.Count} cells, {uses.Count} functions");
        failures.Should().BeEmpty($"each {path} cell prints its model, async and plain");
    }

    /// <summary>The roster: every path, form and inline operand has a cell or a named exclusion.</summary>
    [Fact]
    public void Generator_IsTotal_OverPathsFormsAndInlineOperands()
    {
        var cells = Cells();
        Enum.GetValues<Form>().Should().OnlyContain(f => cells.Any(c => c.Form == f));
        Enum.GetValues<Inline>().Should().OnlyContain(i => cells.Any(c => c.Inline == i));
        Enum.GetValues<Path>().Should().OnlyContain(p => cells.Any(c => c.Path == p)
            || Enum.GetValues<Form>().All(f => Enum.GetValues<Inline>().All(i => Excluded(p, f, i) != null)));
        var excluded = (from p in Enum.GetValues<Path>()
                        from f in Enum.GetValues<Form>()
                        from i in Enum.GetValues<Inline>()
                        where Excluded(p, f, i) != null
                        select 1).Count();
        (Enum.GetValues<Path>().Length * Enum.GetValues<Form>().Length * Enum.GetValues<Inline>().Length)
            .Should().Be(cells.Count + excluded);
        _output.WriteLine($"{cells.Count} cells, {excluded} excluded");
    }
}
