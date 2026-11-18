using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// plan-775c50 D6 crossed with narrowing, run: in an <c>async def</c> body a use of a Guarded value
/// that is neither a plain read nor a plain store names the value's helpers (<c>N__ensure</c>,
/// <c>N__field</c>, and for an <c>out</c> operand <c>N__pick</c> / <c>N__commit</c>), and so does an
/// <c>out</c> operand in any body (D4). Each is named from the value's own spelling, never from the
/// narrowed read the checker records (<c>N!</c>, <c>N.Value</c>, <c>((T)N!)</c>), which has no member
/// spelling: before the fix every such cell was SPY0909.
///
/// <para><b>Generator.</b> <see cref="Narrowing"/> (every form the checker narrows a value by) ×
/// <see cref="Use"/> (D4's taxonomy: a read, the D6 <c>Ensure</c> uses, and the whole <c>out</c> /
/// <c>ref</c> operand) × <see cref="Spelling"/> (a module variable, a <c>@static</c> field on a plain
/// and on a generic type, another module's variable qualified and from-imported), each triple a cell
/// unless <see cref="Excluded"/> says why not. Each cell is an <c>async def</c> holding the use with an
/// <c>await</c> operand, and its plain <c>def</c> twin on a value of its own: the twin is the control,
/// and both must print the cell's model (the value read back after the use). One project per spelling,
/// compiled and run once.</para>
/// <para><b>Python.</b> No twin here: Python has no <c>@static</c>, no generic host and no modifier.
/// The fixture <c>async/async_guarded_narrowed_uses</c> holds the forms python3 runs, with its twin's
/// output.</para>
/// </summary>
public class AsyncGuardedNarrowedUseTests
{
    private readonly ITestOutputHelper _output;

    public AsyncGuardedNarrowedUseTests(ITestOutputHelper output) => _output = output;

    public enum Narrowing
    {
        IsNotNone,
        Truthiness,
        Isinstance,
        Walrus,
        EarlyReturn,
        Assert,
        Match,
    }

    public enum Use
    {
        Read,
        MutatingCall,
        MemberAssign,
        MemberAugmented,
        MemberOutOperand,
        MemberRefOperand,
        IndexAssign,
        OutOperand,
        RefOperand,
    }

    public enum Spelling
    {
        ModuleBare,
        StaticQualified,
        GenericStatic,
        ModuleQualified,
        FromImport,
    }

    /// <summary>The value read back after the use: the initial 3, as the use leaves it (n is 1).</summary>
    private static int Model(Use use) => use switch
    {
        Use.Read => 3,
        Use.MutatingCall => 4,
        Use.MemberAssign => 5,
        Use.MemberAugmented => 4,
        Use.MemberOutOperand => 42,
        Use.MemberRefOperand => 4,
        Use.IndexAssign => 5,
        Use.OutOperand => 42,
        Use.RefOperand => 11,
        _ => throw new ArgumentOutOfRangeException(nameof(use)),
    };

    private static bool IsImported(Spelling spelling) => spelling is Spelling.ModuleQualified or Spelling.FromImport;

    /// <summary>Why a triple has no cell, or null.</summary>
    internal static string? Excluded(Narrowing narrowing, Use use, Spelling spelling)
    {
        if (narrowing == Narrowing.Walrus && spelling != Spelling.ModuleBare)
            return spelling == Spelling.FromImport
                ? "a walrus on a from-imported name stores to another module's value: #2435's class, out of P77's contract"
                : "a walrus binds a bare name only (as Python's `:=`)";
        if (use == Use.OutOperand && IsImported(spelling))
            return "a store to another module's value is #2435's class (a store through a qualified or imported spelling), out of P77's contract";
        if (narrowing == Narrowing.Isinstance && use == Use.Read && IsQualified(spelling))
            return QualifiedCastDropped + " (a read has no D6 arm, so the async def's read drops it too)";
        return null;
    }

    /// <summary>A spelling the emitter reads through a member access it resolves before any narrowing.</summary>
    private static bool IsQualified(Spelling spelling) => spelling is Spelling.StaticQualified or Spelling.GenericStatic or Spelling.ModuleQualified;

    private const string QualifiedCastDropped =
        "pre-existing, outside P77 (findings ledger): a read of `H.v` or `lib.v` narrowed by isinstance emits no cast, CS1061 behind SPY0908";

    /// <summary>Why a cell's plain <c>def</c> twin is not generated, or null: the twin fails today for a
    /// reason outside D6, and the async cell alone is held to the model.</summary>
    internal static string? TwinExcluded(Cell cell)
    {
        if (cell.Narrowing == Narrowing.Isinstance && IsQualified(cell.Spelling) && cell.Use is not (Use.OutOperand or Use.RefOperand))
            return QualifiedCastDropped;
        if (cell.Narrowing == Narrowing.Isinstance && cell.Use == Use.RefOperand && !IsQualified(cell.Spelling))
            return "pre-existing, outside P77 (findings ledger): a `ref` operand narrowed by isinstance is the cast `ref ((Q)v!)`, CS1510 behind SPY0908";
        return null;
    }

    internal sealed record Cell(Narrowing Narrowing, Use Use, Spelling Spelling)
    {
        public string Id => $"{Narrowing}/{Use}/{Spelling}";
        public bool IsList => Use == Use.IndexAssign;
        public string Function(bool isAsync) => $"{(isAsync ? "a" : "s")}_{Narrowing}_{Use}";
    }

    internal static IReadOnlyList<Cell> Cells() =>
        (from narrowing in Enum.GetValues<Narrowing>()
         from use in Enum.GetValues<Use>()
         from spelling in Enum.GetValues<Spelling>()
         where Excluded(narrowing, use, spelling) == null
         select new Cell(narrowing, use, spelling)).ToList();

    private const string LibHelpers =
        "class Q:\n"
        + "    x: int\n"
        + "    def __init__(self, x: int) -> None:\n"
        + "        self.x = x\n"
        + "    def add(self, by: int) -> None:\n"
        + "        self.x = self.x + by\n"
        + "\n"
        + "async def one() -> int:\n    return 1\n"
        + "def fill2(a: out int, by: int) -> int:\n    a = 41 + by\n    return 1\n"
        + "def grow(a: ref int, by: int) -> int:\n    a = a + by\n    return a\n"
        + "def fillq(a: out Q | None, by: int) -> int:\n    a = Q(41 + by)\n    return 1\n"
        + "def growq(a: ref Q | None, by: int) -> int:\n    a = Q(10 + by)\n    return 1\n"
        + "def fillo(a: out object, by: int) -> int:\n    a = Q(41 + by)\n    return 1\n"
        + "def growo(a: ref object, by: int) -> int:\n    a = Q(10 + by)\n    return 1\n";

    /// <summary>One value of a cell (its async use's, or its plain twin's): the declaration, where it
    /// lives, and its spelling at the use.</summary>
    private sealed record Value(int Key, Cell Cell)
    {
        private bool IsObject => Cell.Narrowing == Narrowing.Isinstance;
        public string Type => IsObject ? "object" : Cell.IsList ? "list[int] | None" : "Q | None";
        public string Initial => Cell.IsList ? "[3]" : "Q(3)";
        public string Name => $"v{Key}";

        public string Text => Cell.Spelling switch
        {
            Spelling.ModuleBare or Spelling.FromImport => Name,
            Spelling.StaticQualified => $"H{Key}.v",
            Spelling.GenericStatic => $"G{Key}[int].v",
            Spelling.ModuleQualified => $"lib.{Name}",
            _ => throw new ArgumentOutOfRangeException(),
        };

        public IEnumerable<string> Declaration() => Cell.Spelling switch
        {
            Spelling.StaticQualified => new[] { $"class H{Key}:", "    @static", $"    v: {Type} = {Initial}" },
            Spelling.GenericStatic => new[] { $"class G{Key}[E]:", "    @static", $"    v: {Type} = {Initial}" },
            _ => new[] { $"{Name}: {Type} = {Initial}" },
        };

        /// <summary>The narrowing's test, true for the value.</summary>
        public string Test => Cell.Narrowing switch
        {
            Narrowing.Truthiness => Text,
            Narrowing.Isinstance => $"isinstance({Text}, {(Cell.IsList ? "list[int]" : "Q")})",
            Narrowing.Walrus => $"({Text} := {Initial}) is not None",
            _ => $"{Text} is not None",
        };

        /// <summary>The use's statement, its operand <paramref name="n"/>.</summary>
        public string Statement(string n) => Cell.Use switch
        {
            Use.Read => $"r0 = {Text}.x + {n}",
            Use.MutatingCall => $"{Text}.add({n})",
            Use.MemberAssign => $"{Text}.x = 5 * {n}",
            Use.MemberAugmented => $"{Text}.x += {n}",
            Use.MemberOutOperand => $"r0 = fill2(out {Text}.x, {n})",
            Use.MemberRefOperand => $"r0 = grow(ref {Text}.x, {n})",
            Use.IndexAssign => $"{Text}[0] = 5 * {n}",
            Use.OutOperand => $"r0 = {(IsObject ? "fillo" : "fillq")}(out {Text}, {n})",
            Use.RefOperand => $"r0 = {(IsObject ? "growo" : "growq")}(ref {Text}, {n})",
            _ => throw new ArgumentOutOfRangeException(),
        };

        /// <summary>The use's function body: the use where the narrowing holds; 1 when it ran.</summary>
        public IEnumerable<string> Body(string n)
        {
            var use = Statement(n);
            switch (Cell.Narrowing)
            {
                case Narrowing.EarlyReturn:
                    return new[] { $"if {Text} is None:", "    return 0", use, "return 1" };
                case Narrowing.Assert:
                    return new[] { $"assert {Text} is not None", use, "return 1" };
                case Narrowing.Match:
                    return new[]
                    {
                        $"match {Text}:",
                        $"    case {(Cell.IsList ? "list[int]" : "Q")}():",
                        "        " + use,
                        "        return 1",
                        "    case _:",
                        "        return 0",
                    };
                default:
                    return new[] { $"if {Test}:", "    " + use, "    return 1", "return 0" };
            }
        }

        /// <summary>A plain function reading the value back through a local, so no read of it is narrowed.</summary>
        public IEnumerable<string> ReadBack() => new[]
        {
            $"def rb{Key}() -> int:",
            $"    w = {Text}",
            $"    if isinstance(w, {(Cell.IsList ? "list[int]" : "Q")}):",
            $"        return w{(Cell.IsList ? "[0]" : ".x")}",
            "    return -1",
        };
    }

    public static IEnumerable<object[]> SpellingCases() => Enum.GetValues<Spelling>().Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(SpellingCases))]
    public void NarrowedUses_OfGuardedValues_RunAsTheirPlainTwin(Spelling spelling)
    {
        var cells = Cells().Where(c => c.Spelling == spelling).ToList();
        var key = 0;
        var values = cells.SelectMany(c => TwinExcluded(c) == null ? new[] { (Cell: c, Async: true), (Cell: c, Async: false) } : new[] { (Cell: c, Async: true) })
            .Select(x => (x.Cell, x.Async, Value: new Value(key++, x.Cell)))
            .ToList();

        var lib = new List<string> { LibHelpers };
        var main = new List<string> { "import lib", "from lib import Q, one, fill2, grow, fillq, growq, fillo, growo" };
        if (spelling == Spelling.FromImport)
            main.Add("from lib import " + string.Join(", ", values.Select(v => v.Value.Name)));
        main.Add("");
        foreach (var (cell, isAsync, value) in values)
        {
            (IsImported(spelling) ? lib : main).AddRange(value.Declaration());
            main.Add("");
            main.Add($"{(isAsync ? "async def" : "def")} {cell.Function(isAsync)}_{value.Key}() -> int:");
            main.AddRange(value.Body(isAsync ? "await one()" : "1").Select(l => "    " + l));
            main.Add("");
            main.AddRange(value.ReadBack());
            main.Add("");
        }
        main.Add("async def main() -> None:");
        foreach (var (cell, isAsync, value) in values)
            main.Add($"    print(\"{cell.Id}{(isAsync ? "" : "/sync")}\", {(isAsync ? "await " : "")}{cell.Function(isAsync)}_{value.Key}(), rb{value.Key}())");

        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("AsyncGuardedNarrowed")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("lib.spy", string.Join("\n", lib) + "\n")
            .AddSourceFile("main.spy", string.Join("\n", main) + "\n");
        helper.CreateProjectFile();
        var exec = helper.CompileAndExecute();
        exec.CompilationErrors.Should().BeEmpty($"every {spelling} cell compiles");
        exec.Success.Should().BeTrue(exec.StandardError);

        var expected = values.Select(v => $"{v.Cell.Id}{(v.Async ? "" : "/sync")} 1 {Model(v.Cell.Use)}").ToList();
        var actual = exec.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();
        var failures = expected.Zip(actual, (e, a) => (e, a)).Where(p => p.e != p.a).Select(p => $"expected '{p.e}', got '{p.a}'").ToList();
        if (expected.Count != actual.Count)
            failures.Add($"{expected.Count} lines expected, {actual.Count} printed");
        _output.WriteLine($"{spelling}: {cells.Count} cells, {values.Count} functions ({values.Count(v => v.Async)} async, {values.Count(v => !v.Async)} plain twins)");
        failures.Should().BeEmpty($"each {spelling} cell prints its model, async and plain");
    }

    /// <summary>The roster: every narrowing, use and spelling has a cell; every excluded triple names its
    /// reason; the async uses cover both D6 arms and the plain read.</summary>
    [Fact]
    public void Generator_IsTotal_OverNarrowingsUsesAndSpellings()
    {
        var cells = Cells();
        Enum.GetValues<Narrowing>().Should().OnlyContain(n => cells.Any(c => c.Narrowing == n));
        Enum.GetValues<Use>().Should().OnlyContain(u => cells.Any(c => c.Use == u));
        Enum.GetValues<Spelling>().Should().OnlyContain(s => cells.Any(c => c.Spelling == s));
        var excluded = (from n in Enum.GetValues<Narrowing>()
                        from u in Enum.GetValues<Use>()
                        from s in Enum.GetValues<Spelling>()
                        where Excluded(n, u, s) != null
                        select 1).Count();
        (Enum.GetValues<Narrowing>().Length * Enum.GetValues<Use>().Length * Enum.GetValues<Spelling>().Length)
            .Should().Be(cells.Count + excluded);
        _output.WriteLine($"{cells.Count} cells, {excluded} excluded, {cells.Count(c => TwinExcluded(c) != null)} without a plain twin");
        cells.Should().Contain(c => TwinExcluded(c) == null, "the plain twins are the control");
    }
}
