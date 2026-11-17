using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Tests.Infrastructure;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// plan-775c50 D6 (P77 T6): inside an <c>async def</c> body, a use of a Guarded value that is neither a
/// plain read nor a plain store is a <see cref="SemanticInfo.GetAsyncGuardedUse"/> fact, node-keyed on
/// the Identifier or MemberAccess that names the value, and carried through <c>MergeFrom</c>.
///
/// <para><b>Generator.</b> <see cref="Uses"/> (D4's taxonomy: read, plain store, <c>ref</c> / <c>in</c> /
/// <c>out</c> operand, member store and augmented store, method call, member operand, index store and
/// read, a parenthesized receiver) × <see cref="Spellings"/> (a module variable by its bare name, a
/// <c>@static</c> field <c>H.s</c>, another module's variable as <c>lib.u</c>, <c>L.u</c> after
/// <c>import lib as L</c>, <c>u</c> from-imported and <c>uu</c> from-imported under an alias, a
/// computed const), each pair a cell unless <see cref="Excluded"/> says why not. Every cell is one
/// <c>async def</c> holding the use beside an <c>await</c>, and the same statement in a plain
/// <c>def</c>: the positive cells assert the recorded kind and value, the plain-store and read cells
/// assert no record on the same node shape (positive controls beside them).</para>
/// <para><b>Negative controls.</b> The plain <c>def</c> twin of every cell; a local of the same type;
/// a Guarded module PROPERTY (its accessor returns a value). The table holds exactly the expected
/// nodes: a record anywhere else in the file is a failure.</para>
/// <para><b>Views.</b> The project-merged <see cref="SemanticInfo"/> (what the emitter reads, through
/// <c>MergeFrom</c>) and the per-file one.</para>
/// </summary>
public class AsyncGuardedUseFactTests
{
    private readonly ITestOutputHelper _output;

    public AsyncGuardedUseFactTests(ITestOutputHelper output) => _output = output;

    /// <summary>A use of value <c>{v}</c>; <c>{w}</c> is the awaited operand. Expected: the recorded kind, or null.</summary>
    public sealed record Use(string Name, string Statement, AsyncGuardedUseKind? Expected, bool StoresWhole = false, bool NeedsList = false);

    internal static readonly Use[] Uses =
    {
        new("Read", "r0 = {v}.x + {w}", null),
        new("WholeRead", "q0: P = {v}\n    r0 = q0.x + {w}", null),
        new("PassByValue", "r0 = takep({v}, {w})", null),
        new("Assign", "{v} = P(5 * {w})", null, StoresWhole: true),
        new("TupleLeaf", "{v}, n0 = P(5), {w}", null, StoresWhole: true),
        new("ForTarget", "for {v} in [P({w})]:\n        pass", null, StoresWhole: true),
        new("RefOperand", "r0 = growp(ref {v}, {w})", AsyncGuardedUseKind.Ensure),
        new("InOperand", "r0 = peekp(in {v}, {w})", AsyncGuardedUseKind.Ensure),
        new("OutOperand", "r0 = fillp(out {v}, {w})", AsyncGuardedUseKind.OutOperand),
        new("MemberAssign", "{v}.x = 5 * {w}", AsyncGuardedUseKind.Ensure),
        new("MemberAugmented", "{v}.x += {w}", AsyncGuardedUseKind.Ensure),
        new("MutatingCall", "{v}.add({w})", AsyncGuardedUseKind.Ensure),
        new("ParenthesizedCall", "({v}).add({w})", AsyncGuardedUseKind.Ensure),
        new("MemberRefOperand", "r0 = grow(ref {v}.x, {w})", AsyncGuardedUseKind.Ensure),
        new("MemberInOperand", "r0 = peek2(in {v}.x, {w})", AsyncGuardedUseKind.Ensure),
        new("MemberOutOperand", "r0 = fill2(out {v}.x, {w})", AsyncGuardedUseKind.Ensure),
        new("IndexAssign", "{v}[0] = {w}", AsyncGuardedUseKind.Ensure, NeedsList: true),
        new("IndexRead", "r0 = {v}[0] + {w}", null, NeedsList: true),
    };

    /// <summary>A spelling of a Guarded value: its text, and whether the value is a list (else a struct P)
    /// or a computed const (int).</summary>
    public sealed record Spelling(string Name, string Text, string ListText, bool IsConst = false);

    internal static readonly Spelling[] Spellings =
    {
        new("ModuleBare", "t", "ts"),
        new("StaticQualified", "H.s", "H.ss"),
        new("ModuleQualified", "lib.u", "lib.us"),
        new("ModuleAliased", "L.u", "L.us"),
        new("FromImport", "u", "us"),
        new("FromImportAliased", "uu", "uus"),
        new("ComputedConst", "K", "K", IsConst: true),
    };

    /// <summary>Why a (use, spelling) pair has no cell, or null.</summary>
    internal static string? Excluded(Use use, Spelling spelling)
    {
        if (spelling.IsConst)
        {
            // A computed const (an int) takes no store and no `ref` / `out` (D7: SPY0389); its `in`
            // operand runs, and is the const's one recorded use.
            return use.Name is "InOperand" or "PassByValue" ? null : "a computed const is an int with no store, member or ref / out operand (D7)";
        }
        if (use.StoresWhole && spelling.Name is "FromImport" or "FromImportAliased" or "ModuleQualified" or "ModuleAliased")
            return "a store to another module's value is #2435's class (a store through a qualified or imported spelling), out of P77's contract";
        return null;
    }

    private const string Helpers =
        "async def one() -> int:\n    return 1\n"
        + "def seven() -> int:\n    return 7\n"
        + "def takep(a: P, by: int) -> int:\n    return a.x + by\n"
        + "def takei(a: int, by: int) -> int:\n    return a + by\n"
        + "def growp(a: ref P, by: int) -> int:\n    a.x = a.x + by\n    return a.x\n"
        + "def peekp(a: in P, by: int) -> int:\n    return a.x + by\n"
        + "def fillp(a: out P, by: int) -> int:\n    a = P(41 + by)\n    return 1\n"
        + "def grow(a: ref int, by: int) -> int:\n    a = a + by\n    return a\n"
        + "def peek2(a: in int, by: int) -> int:\n    return a + by\n"
        + "def fill2(a: out int, by: int) -> int:\n    a = 41 + by\n    return 1\n";

    private const string LibSource =
        "struct P:\n"
        + "    x: int\n"
        + "    def __init__(self, x: int) -> None:\n"
        + "        self.x = x\n"
        + "    def add(self, by: int) -> None:\n"
        + "        self.x = self.x + by\n"
        + "\n"
        + "u: P = P(3)\n"
        + "us: list[int] = [3]\n";

    internal sealed record Cell(Use Use, Spelling Spelling)
    {
        public string Id => $"{Use.Name}/{Spelling.Name}";
        public string Value => Use.NeedsList ? Spelling.ListText : Spelling.Text;

        /// <summary>The use's statement on this value: a computed const is an int, so its operand's
        /// callee takes an int.</summary>
        public string Statement(string awaited)
        {
            var text = Use.Statement;
            if (Spelling.IsConst)
                text = text.Replace("peekp(", "peek2(").Replace("takep(", "takei(");
            return text.Replace("{v}", Value).Replace("{w}", awaited);
        }
        public string Function(bool isAsync) => $"{(isAsync ? "a" : "s")}_{Use.Name}_{Spelling.Name}";
    }

    internal static IReadOnlyList<Cell> Cells() =>
        (from use in Uses from spelling in Spellings where Excluded(use, spelling) == null select new Cell(use, spelling)).ToList();

    private static string MainSource()
    {
        var lines = new List<string>
        {
            "import lib",
            "import lib as L",
            "from lib import P, u, us",
            "from lib import u as uu, us as uus",
            "",
            "t: P = P(1)",
            "ts: list[int] = [1]",
            "const K: int = seven()",
            "property pp: P = P(4)",
            "",
            "class H:",
            "    @static",
            "    s: P = P(2)",
            "    @static",
            "    ss: list[int] = [2]",
            "",
        };
        lines.Add(Helpers);
        foreach (var cell in Cells())
        {
            foreach (var isAsync in new[] { true, false })
            {
                var await = isAsync ? "await one()" : "1";
                lines.Add($"{(isAsync ? "async def" : "def")} {cell.Function(isAsync)}() -> int:");
                lines.Add("    " + cell.Statement(await));
                lines.Add("    return 0");
                lines.Add("");
            }
        }
        // Negative controls: a local of the same type, and a Guarded module property.
        lines.Add("async def a_local() -> int:");
        lines.Add("    p = P(1)");
        lines.Add("    p.add(await one())");
        lines.Add("    return p.x");
        lines.Add("");
        lines.Add("async def a_property() -> int:");
        lines.Add("    r0 = pp.x + await one()");
        lines.Add("    return r0");
        lines.Add("");
        lines.Add("def main() -> None:");
        lines.Add("    pass");
        return string.Join("\n", lines) + "\n";
    }

    public static IEnumerable<object[]> Views() => new[] { new object[] { "project-merged" }, new object[] { "project-file" } };

    private (Module Ast, SemanticInfo Info) Analyze(string view, ProjectCompilationHelper helper)
    {
        var config = ProjectFileParser.Load(Path.Combine(helper.ProjectDirectory, "AsyncGuardedUseFact.spyproj"));
        var analysis = new CompilerApi(null, new[] { SharpyCoreReference.Location }).AnalyzeProject(config);
        var errors = analysis.Diagnostics.GetAll().Where(d => d.IsError).Select(d => $"{d.Code} @ {d.Line}:{d.Column}: {d.Message}").ToList();
        foreach (var error in errors)
            _output.WriteLine(error);
        errors.Should().BeEmpty("the probe program type-checks");
        var mainPath = helper.SourceFiles.Single(p => Path.GetFileName(p) == "main.spy");
        var file = analysis.GetFileResult(mainPath);
        file.Should().NotBeNull();
        var info = view == "project-merged" ? analysis.ProjectModel.SemanticInfo : file!.SemanticInfo;
        if (view == "project-merged")
            ReferenceEquals(info, file!.SemanticInfo).Should().BeFalse("the merged view exercises MergeFrom");
        return (file!.Ast!, info!);
    }

    private static string Spell(Expression e) => e switch
    {
        Identifier id => id.Name,
        MemberAccess { Object: var o } ma => Spell(o) + "." + ma.Member,
        _ => "?",
    };

    [Theory]
    [MemberData(nameof(Views))]
    public void AsyncUses_OfGuardedValues_RecordTheirArm_AndNothingElse(string view)
    {
        using var helper = new ProjectCompilationHelper(_output)
            .WithRootNamespace("AsyncGuardedUseFact")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .AddSourceFile("lib.spy", LibSource)
            .AddSourceFile("main.spy", MainSource());
        helper.CreateProjectFile();
        var (ast, info) = Analyze(view, helper);

        var functions = AstDescendants.SelfAndDescendants(ast).OfType<FunctionDef>().ToDictionary(f => f.Name);
        var expected = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        var failures = new List<string>();
        var positives = 0;
        var negatives = 0;

        foreach (var cell in Cells())
        {
            foreach (var isAsync in new[] { true, false })
            {
                var function = functions[cell.Function(isAsync)];
                var nodes = AstDescendants.SelfAndDescendants(function.Body[0])
                    .OfType<Expression>()
                    .Where(e => e is Identifier or MemberAccess && Spell(e) == cell.Value)
                    .ToList();
                if (nodes.Count != 1)
                {
                    failures.Add($"{cell.Id}: {nodes.Count} nodes spell '{cell.Value}' (the probe is broken)");
                    continue;
                }
                var node = nodes[0];
                var recorded = info.GetAsyncGuardedUse(node);
                var want = isAsync ? cell.Use.Expected : null;
                if (want is { } kind)
                {
                    positives++;
                    expected.Add(node);
                    if (recorded == null)
                        failures.Add($"{cell.Id}: nothing recorded, expected {kind}");
                    else if (recorded.Kind != kind)
                        failures.Add($"{cell.Id}: recorded {recorded.Kind}, expected {kind}");
                    else if (!ReferenceEquals(recorded.Node, node) || recorded.Value is not VariableSymbol)
                        failures.Add($"{cell.Id}: the record names another node or no variable value");
                }
                else
                {
                    negatives++;
                    if (recorded != null)
                        failures.Add($"{cell.Id}{(isAsync ? "" : " (plain def)")}: recorded {recorded.Kind}, expected none");
                }
            }
        }

        // No record anywhere else in the file: the local, the property, the helpers, the plain twins.
        foreach (var node in AstDescendants.SelfAndDescendants(ast).OfType<Expression>())
        {
            if (!expected.Contains(node) && info.GetAsyncGuardedUse(node) is { } stray)
                failures.Add($"{Spell(node)} @ {node.LineStart}:{node.ColumnStart}: stray record {stray.Kind}");
        }

        _output.WriteLine($"{Cells().Count} cells: {positives} recorded, {negatives} unrecorded (the plain-def twins included)");
        failures.Should().BeEmpty($"{view}: every async use takes D6's arm by D4's taxonomy, and nothing else is recorded");
        positives.Should().BeGreaterThan(0, "the generator owes recorded cells");
    }

    /// <summary>The roster: every use kind and spelling has at least one cell, and every excluded pair names
    /// its reason; the recorded kinds cover both arms.</summary>
    [Fact]
    public void Generator_IsTotal_OverUsesAndSpellings()
    {
        var cells = Cells();
        Uses.Should().OnlyContain(u => cells.Any(c => c.Use == u), "each use kind has a cell");
        Spellings.Should().OnlyContain(s => cells.Any(c => c.Spelling == s), "each spelling has a cell");
        (Uses.Length * Spellings.Length).Should().Be(cells.Count
            + (from u in Uses from s in Spellings where Excluded(u, s) != null select 1).Count());
        cells.Select(c => c.Use.Expected).Distinct().Should().BeEquivalentTo(
            new AsyncGuardedUseKind?[] { null, AsyncGuardedUseKind.Ensure, AsyncGuardedUseKind.OutOperand });
    }
}
