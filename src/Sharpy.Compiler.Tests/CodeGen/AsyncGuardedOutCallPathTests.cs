using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// plan-775c50 D4/D6 crossed with how a call is emitted and how its Guarded operand is spelled, run:
/// a call with an <c>out</c> operand on a Guarded value commits it through the one seam
/// (<c>GenerateCallWithOutCommits</c>) whichever route emits the call and whatever it calls, the
/// operand is known as Guarded under every spelling of the value, and in an <c>async def</c> body
/// such a call is emitted once per arm, an inline <c>out</c> declaration beside the operand declared
/// once, before the statement, typed as the checker typed the binding (the callee's resolved,
/// substituted parameter).
///
/// <para><b>Generator.</b> <see cref="Route"/> (the emitter route that generates the call's
/// arguments) × <see cref="Callee"/> (what the call calls) × <see cref="Form"/> (the call valued, as
/// its statement, a void callee's as its statement, and nested in another call that reads the
/// stored values after it) × <see cref="Inline"/> (the operand beside the Guarded one: another
/// Guarded value, or an inline <c>out let</c> / <c>auto</c> / annotated declaration) ×
/// <see cref="Spelling"/> (the Guarded operand: a module value bare and module-qualified, a
/// <c>@static</c> through its own class, a subclass, a type alias, <c>self</c>, a nested type and a
/// nested subclass, a subclass of a generic base, a constructed generic and its alias, a subclass
/// imported by name, a local subclass of another module's class, and a struct), each tuple a cell
/// unless <see cref="Excluded"/> says why not. Each cell is an <c>async def</c> whose call holds an
/// <c>await</c>, and its plain <c>def</c> twin, the control, on values of their own; both must print
/// the model. One project per route and callee.</para>
/// <para><b>Python.</b> No twin: Python has no <c>out</c>.</para>
/// </summary>
public class AsyncGuardedOutCallPathTests
{
    private readonly ITestOutputHelper _output;

    public AsyncGuardedOutCallPathTests(ITestOutputHelper output) => _output = output;

    public enum Route
    {
        Direct,
        Pipe,
        ConstructorChain,
        KeywordReordered,
    }

    public enum Callee
    {
        Function,
        Method,
        StaticMethod,
        GenericFunction,
        GenericClassMethod,
        Constructor,
        ModuleQualified,
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

    public enum Spelling
    {
        ModuleBare,
        ModuleQualified,
        OwnClass,
        Subclass,
        Alias,
        Self,
        NestedOwn,
        NestedSubclass,
        GenericBaseSubclass,
        ClosedGeneric,
        ClosedGenericAlias,
        FromImportSubclass,
        LocalSubclassOfLib,
        Struct,
    }

    /// <summary>Why a tuple has no cell, or null.</summary>
    internal static string? Excluded(Route route, Callee callee, Form form, Inline inline, Spelling spelling)
    {
        if (route == Route.KeywordReordered)
            return "#2025: a call whose keyword argument reorders its arguments drops the `out` modifier, in a plain def too (CS1620)";
        if (route == Route.ConstructorChain && callee != Callee.Constructor)
            return "a chaining call `super().__init__(...)` calls a base constructor";
        if (route == Route.ConstructorChain && form != Form.Statement)
            return "a chaining call `super().__init__(...)` is its constructor's first statement only";
        if (callee == Callee.Constructor && form == Form.VoidStatement)
            return "a constructor call has a value";
        if (route == Route.Pipe && form == Form.VoidStatement)
            return "#2745: a pipe statement `x |> f(...)` into a void callee is refused (SPY0603), in a plain def too";
        if (route == Route.Pipe && callee == Callee.Constructor)
            return "piping to a constructor is refused (SPY0231, \"not yet supported\"), in a plain def too";
        if (route == Route.Pipe && callee == Callee.Function && spelling == Spelling.Self)
            return "a pipe into a bare module function from a type's method emits the unqualified name (CS0103), with no `out` operand and in a plain def too (findings ledger)";
        if (route == Route.Pipe && callee == Callee.GenericFunction)
            return "the pipe route infers no method-level type argument from the piped value: `\"s\" |> gfill(...)` is refused (SPY0220), in a plain def too";
        return null;
    }

    private static bool IsGeneric(Callee callee) => callee is Callee.GenericFunction or Callee.GenericClassMethod;

    internal sealed record Cell(Route Route, Callee Callee, Form Form, Inline Inline, Spelling Spelling)
    {
        public string Id => $"{Route}/{Callee}/{Form}/{Inline}/{Spelling}";
    }

    internal static IReadOnlyList<Cell> Cells() =>
        (from route in Enum.GetValues<Route>()
         from callee in Enum.GetValues<Callee>()
         from form in Enum.GetValues<Form>()
         from inline in Enum.GetValues<Inline>()
         from spelling in Enum.GetValues<Spelling>()
         where Excluded(route, callee, form, inline, spelling) == null
         select new Cell(route, callee, form, inline, spelling)).ToList();

    /// <summary>One function of a cell (its async use's or its plain twin's) and the values it stores to.</summary>
    private sealed record Use(int Key, Cell Cell, bool IsAsync)
    {
        private string X => IsGeneric(Cell.Callee) ? "str" : "int";
        private string Argument => IsGeneric(Cell.Callee) ? "\"s\"" : "3";

        /// <summary>The argument after the Guarded operand: in an <c>async def</c> an <c>await</c>, so a
        /// reference to the operand's storage taken before it would live across it (CS8178); a
        /// constructor's chaining call is in no <c>async def</c>.</summary>
        private string Await => IsAsync && Cell.Route != Route.ConstructorChain ? "await one()" : "1";

        private string A => IsGeneric(Cell.Callee) ? "s" : "3";
        private string H => $"h{Key}";
        private string AName => Cell.Inline == Inline.Guarded ? H : "v";
        private string Host => $"H{Key}";

        /// <summary>The Guarded operand, as the cell spells it.</summary>
        private string G => Cell.Spelling switch
        {
            Spelling.ModuleBare => $"g{Key}",
            Spelling.ModuleQualified => $"lib.g{Key}",
            Spelling.OwnClass => $"{Host}.s",
            Spelling.Subclass => $"D{Key}.s",
            Spelling.Alias => $"E{Key}.s",
            Spelling.Self => "self.s",
            Spelling.NestedOwn => $"O{Key}.C.s",
            Spelling.NestedSubclass => $"O{Key}.D.s",
            Spelling.GenericBaseSubclass => $"GD{Key}.s",
            Spelling.ClosedGeneric => $"G{Key}[int].s",
            Spelling.ClosedGenericAlias => $"GA{Key}.s",
            Spelling.FromImportSubclass => $"LD{Key}.s",
            Spelling.LocalSubclassOfLib => $"SL{Key}.s",
            Spelling.Struct => $"St{Key}.s",
            _ => throw new ArgumentOutOfRangeException(),
        };

        /// <summary>Whether the value is a <c>@static</c> of the host class <c>H</c> (its own, a
        /// subclass's, an alias's or <c>self</c>'s spelling).</summary>
        private bool OnHost => Cell.Spelling is Spelling.OwnClass or Spelling.Subclass or Spelling.Alias
            || (Cell.Spelling == Spelling.Self && Cell.Route != Route.ConstructorChain);

        /// <summary>The import the cell's module needs: a subclass imported by name.</summary>
        public IEnumerable<string> Imports()
        {
            if (Cell.Spelling == Spelling.FromImportSubclass)
                yield return $"from lib import LD{Key}";
        }

        private bool IsMethodCell => Cell.Spelling == Spelling.Self && Cell.Route != Route.ConstructorChain;

        public IEnumerable<string> Declarations()
        {
            if (Cell.Spelling == Spelling.ModuleBare)
                yield return $"g{Key}: int = 1";
            if (Cell.Inline == Inline.Guarded)
                yield return $"{H}: {X} = {(IsGeneric(Cell.Callee) ? "\"i\"" : "0")}";
            if (OnHost)
            {
                yield return $"class {Host}:";
                yield return "    @static";
                yield return "    s: int = 1";
                if (IsMethodCell)
                {
                    yield return "";
                    yield return $"    {(IsAsync ? "async def" : "def")} c(self) -> str:";
                    foreach (var line in Body())
                        yield return "        " + line;
                }
            }
            if (Cell.Spelling == Spelling.Subclass)
            {
                yield return $"class D{Key}({Host}):";
                yield return "    pass";
            }
            if (Cell.Spelling == Spelling.Alias)
                yield return $"type E{Key} = {Host}";
            if (Cell.Spelling is Spelling.NestedOwn or Spelling.NestedSubclass)
            {
                yield return $"class O{Key}:";
                yield return "    class C:";
                yield return "        @static";
                yield return "        s: int = 1";
                if (Cell.Spelling == Spelling.NestedSubclass)
                {
                    yield return "    class D(C):";
                    yield return "        pass";
                }
            }
            if (Cell.Spelling is Spelling.GenericBaseSubclass or Spelling.ClosedGeneric or Spelling.ClosedGenericAlias)
            {
                yield return $"class G{Key}[T]:";
                yield return "    @static";
                yield return "    s: int = 1";
                if (Cell.Spelling == Spelling.GenericBaseSubclass)
                {
                    yield return $"class GD{Key}(G{Key}[int]):";
                    yield return "    pass";
                }
                if (Cell.Spelling == Spelling.ClosedGenericAlias)
                    yield return $"type GA{Key} = G{Key}[int]";
            }
            if (Cell.Spelling == Spelling.LocalSubclassOfLib)
            {
                yield return $"class SL{Key}(lib.LC{Key}):";
                yield return "    pass";
            }
            if (Cell.Spelling == Spelling.Struct)
            {
                yield return $"struct St{Key}:";
                yield return "    @static";
                yield return "    s: int = 1";
            }
            if (Cell.Route == Route.ConstructorChain)
            {
                yield return $"class Chain{Key}(Base):";
                yield return "    shown: str";
                if (Cell.Spelling == Spelling.Self)
                {
                    yield return "    @static";
                    yield return "    s: int = 1";
                }
                yield return "    def __init__(self) -> None:";
                yield return $"        super().__init__({Argument}, out {G}, {Await}, {Operand})";
                yield return $"        self.shown = show(0, {AName}, {G})";
            }
        }

        /// <summary>The declarations the cell's other module holds: a module-qualified value.</summary>
        public IEnumerable<string> LibDeclarations()
        {
            if (Cell.Spelling == Spelling.ModuleQualified)
                yield return $"g{Key}: int = 1";
            if (Cell.Spelling is Spelling.FromImportSubclass or Spelling.LocalSubclassOfLib)
            {
                yield return $"class LC{Key}:";
                yield return "    @static";
                yield return "    s: int = 1";
                yield return $"class LD{Key}(LC{Key}):";
                yield return "    pass";
            }
        }

        /// <summary>The cell's function: a module function, or for <c>self</c> its host's method.</summary>
        public IEnumerable<string> Function()
        {
            if (IsMethodCell)
                yield break;
            yield return $"{(IsAsync ? "async def" : "def")} c{Key}() -> str:";
            foreach (var line in Body())
                yield return "    " + line;
        }

        public string Invocation => IsMethodCell ? $"{Host}().c()" : $"c{Key}()";

        private string Operand => Cell.Inline switch
        {
            Inline.Guarded => $"out {H}",
            Inline.Let => "out let v",
            Inline.Auto => "out v: auto",
            Inline.Annotated => $"out v: {X}",
            _ => throw new ArgumentOutOfRangeException(),
        };

        /// <summary>The callee, the void twin's (<c>fillv</c>, ...) when <paramref name="isVoid"/>.</summary>
        private string Target(bool isVoid)
        {
            var v = isVoid ? "v" : "";
            return Cell.Callee switch
            {
                Callee.Function => $"fill{v}",
                Callee.Method => $"Box().fill{v}",
                Callee.StaticMethod => $"Box.sfill{v}",
                Callee.GenericFunction => $"gfill{v}",
                Callee.GenericClassMethod => $"Pair[str]().fill{v}",
                Callee.Constructor => "Made",
                Callee.ModuleQualified => $"lib.fill{v}",
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        /// <summary>The call, by its route.</summary>
        private string Call(bool isVoid) => Cell.Route == Route.Pipe
            ? $"{Argument} |> {Target(isVoid)}(out {G}, {Await}, {Operand})"
            : $"{Target(isVoid)}({Argument}, out {G}, {Await}, {Operand})";

        /// <summary>The call's value: a constructor call is read for it.</summary>
        private string ValuedCall => Cell.Callee == Callee.Constructor ? $"{Call(false)}.r" : Call(false);

        /// <summary>The cell's body: the call in its form, then the values it stored.</summary>
        private IEnumerable<string> Body()
        {
            if (Cell.Route == Route.ConstructorChain)
                return new[] { $"return Chain{Key}().shown" };
            return Cell.Form switch
            {
                Form.Valued => new[] { $"r = {ValuedCall}", $"return show(r, {AName}, {G})" },
                Form.Statement => new[] { Call(false), $"return show(0, {AName}, {G})" },
                Form.VoidStatement => new[] { Call(true), $"return show(0, {AName}, {G})" },
                Form.Nested => new[] { $"return show({ValuedCall}, {AName}, {G})" },
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        public string Model => $"{(Cell.Form is Form.Statement or Form.VoidStatement ? 0 : 10)} {A} 7";
    }

    private const string Fills =
        "def fill(x: int, a: out int, n: int, b: out int) -> int:\n    a = 7\n    b = x\n    return 10\n"
        + "def fillv(x: int, a: out int, n: int, b: out int) -> None:\n    a = 7\n    b = x\n";

    private const string Helpers =
        "import lib\n"
        + "def show(r: int, a: object, b: int) -> str:\n    return f\"{r} {a} {b}\"\n"
        + "async def one() -> int:\n    return 1\n"
        + Fills
        + "def gfill[T](x: T, a: out int, n: int, b: out T) -> int:\n    a = 7\n    b = x\n    return 10\n"
        + "def gfillv[T](x: T, a: out int, n: int, b: out T) -> None:\n    a = 7\n    b = x\n"
        + "class Box:\n"
        + "    def fill(self, x: int, a: out int, n: int, b: out int) -> int:\n        a = 7\n        b = x\n        return 10\n"
        + "    def fillv(self, x: int, a: out int, n: int, b: out int) -> None:\n        a = 7\n        b = x\n"
        + "    @static\n"
        + "    def sfill(x: int, a: out int, n: int, b: out int) -> int:\n        a = 7\n        b = x\n        return 10\n"
        + "    @static\n"
        + "    def sfillv(x: int, a: out int, n: int, b: out int) -> None:\n        a = 7\n        b = x\n"
        + "class Pair[T]:\n"
        + "    def fill(self, x: T, a: out int, n: int, b: out T) -> int:\n        a = 7\n        b = x\n        return 10\n"
        + "    def fillv(self, x: T, a: out int, n: int, b: out T) -> None:\n        a = 7\n        b = x\n"
        + "class Made:\n"
        + "    r: int\n"
        + "    def __init__(self, x: int, a: out int, n: int, b: out int) -> None:\n        a = 7\n        b = x\n        self.r = 10\n"
        + "class Base:\n"
        + "    r: int\n"
        + "    def __init__(self, x: int, a: out int, n: int, b: out int) -> None:\n        a = 7\n        b = x\n        self.r = 10\n";

    public static IEnumerable<object[]> RouteCalleeCases()
        => Cells().Select(c => (c.Route, c.Callee)).Distinct().Select(p => new object[] { p.Route, p.Callee });

    [Theory]
    [MemberData(nameof(RouteCalleeCases))]
    public void OutCalls_OnEveryRouteCalleeAndSpelling_RunAsTheirPlainTwin(Route route, Callee callee)
    {
        var cells = Cells().Where(c => c.Route == route && c.Callee == callee).ToList();
        var key = 0;
        var uses = cells.SelectMany(c => new[] { new Use(key++, c, true), new Use(key++, c, false) }).ToList();

        var main = uses.SelectMany(u => u.Imports()).ToList();
        main.Add(Helpers);
        foreach (var use in uses)
        {
            main.AddRange(use.Declarations());
            main.Add("");
            main.AddRange(use.Function());
            main.Add("");
        }
        main.Add("async def main() -> None:");
        foreach (var use in uses)
            main.Add($"    print(\"{use.Cell.Id}{(use.IsAsync ? "" : "/sync")}\", {(use.IsAsync ? "await " : "")}{use.Invocation})");

        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("AsyncGuardedOutCallPath")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("lib.spy", Fills + string.Join("\n", uses.SelectMany(u => u.LibDeclarations())) + "\n")
            .AddSourceFile("main.spy", string.Join("\n", main) + "\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();
        exec.CompilationErrors.Should().BeEmpty($"every {route}/{callee} cell compiles");
        exec.Success.Should().BeTrue(exec.StandardError);

        var expected = uses.Select(u => $"{u.Cell.Id}{(u.IsAsync ? "" : "/sync")} {u.Model}").ToList();
        var actual = exec.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();
        var failures = expected.Zip(actual, (e, a) => (e, a)).Where(p => p.e != p.a).Select(p => $"expected '{p.e}', got '{p.a}'").ToList();
        if (expected.Count != actual.Count)
            failures.Add($"{expected.Count} lines expected, {actual.Count} printed");
        _output.WriteLine($"{route}/{callee}: {cells.Count} cells, {uses.Count} functions");
        failures.Should().BeEmpty($"each {route}/{callee} cell prints its model, async and plain");
    }

    /// <summary>The roster: every route, callee, form, inline operand and spelling has a cell or a named
    /// exclusion, and the cells and exclusions partition the product.</summary>
    [Fact]
    public void Generator_IsTotal_OverRoutesCalleesFormsInlineOperandsAndSpellings()
    {
        var cells = Cells();
        Enum.GetValues<Callee>().Should().OnlyContain(c => cells.Any(x => x.Callee == c));
        Enum.GetValues<Form>().Should().OnlyContain(f => cells.Any(x => x.Form == f));
        Enum.GetValues<Inline>().Should().OnlyContain(i => cells.Any(x => x.Inline == i));
        Enum.GetValues<Route>().Where(r => r != Route.KeywordReordered).Should().OnlyContain(r => cells.Any(x => x.Route == r));
        Enum.GetValues<Spelling>().Should().OnlyContain(s => cells.Any(x => x.Spelling == s));
        var excluded = (from r in Enum.GetValues<Route>()
                        from c in Enum.GetValues<Callee>()
                        from f in Enum.GetValues<Form>()
                        from i in Enum.GetValues<Inline>()
                        from s in Enum.GetValues<Spelling>()
                        where Excluded(r, c, f, i, s) != null
                        select 1).Count();
        (Enum.GetValues<Route>().Length * Enum.GetValues<Callee>().Length * Enum.GetValues<Form>().Length
            * Enum.GetValues<Inline>().Length * Enum.GetValues<Spelling>().Length)
            .Should().Be(cells.Count + excluded);
        _output.WriteLine($"{cells.Count} cells, {excluded} excluded");
    }
}
