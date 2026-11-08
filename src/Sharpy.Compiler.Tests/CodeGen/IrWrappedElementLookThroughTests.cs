using System.Collections.Immutable;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The emitter's by-key IR read (<c>RoslynEmitter.IrNodeFor&lt;TIr&gt;</c>) looks through an
/// <see cref="IrOptionalWrappedElement"/> to its single child, so wrapping a tuple element stored into
/// a narrowed Optional slot hides no fact the element's own node carries (P82 #2443; plan-1faae3 D5).
/// Every IR expression kind the emitter reads by key has a pin row that goes red when the look-through
/// is removed, or is an excluded kind with its reason; <see cref="Roster_IsClosed"/> keeps the table
/// total against the reflected IR vocabulary and the CodeGen read census.
/// </summary>
public class IrWrappedElementLookThroughTests
{
    /// <summary>
    /// The pin row of each kind: where a removed look-through goes red. Output rows are fixture
    /// snapshots or runs; constructed rows are the <c>[Fact]</c>s below, needed where no program
    /// discriminates (the absent path recomputes the same output, or the fact is the spelled name).
    /// </summary>
    private static readonly Dictionary<string, string> Pinned = new(StringComparer.Ordinal)
    {
        ["IrConstant"] = "output: type_system/optional_walrus_tuple_element cell 6 (`2 ** 3` folded; absent: SPY0909)",
        ["IrOptionalWalrusStore"] = "output: type_system/optional_walrus_tuple_element cell 4",
        ["IrEqualityComparison"] = "output: type_system/optional_wrapped_element_children `t.Equals((1, \"a\"))` snapshot (absent: tuple ==)",
        ["IrIndexAccess"] = "output: type_system/optional_wrapped_element_children `w.Item1` (absent: `w[0]`, CS0021 under run)",
        ["IrLoweredLoop"] = "constructed: " + nameof(WrappedLoweredLoop_SizingComesFromTheChild),
        ["IrLoweredGenerator"] = "constructed: " + nameof(WrappedLoweredGenerator_ClausesComeFromTheChild),
        ["IrMemberAccess"] = "constructed: " + nameof(WrappedMemberAccess_ResolvedClrNameComesFromTheChild),
    };

    /// <summary>Kinds the emitter reads by key but never through a wrapped element, with the reason.</summary>
    private static readonly Dictionary<string, string> Excluded = new(StringComparer.Ordinal)
    {
        ["IrElidedNoneBody"] = "read only on a lambda's body, never a tuple element",
        ["IrTypedNullScrutinee"] = "read only on a match scrutinee, never a tuple element",
    };

    /// <summary>
    /// M10's guard: the roster — <see cref="IrExpression"/> subclasses by reflection (minus the wrap
    /// itself) that CodeGen reads with <c>IrNodeFor&lt;T&gt;</c> — equals the pinned ∪ excluded
    /// kinds, and every table entry is a member of the vocabulary.
    /// </summary>
    [Fact]
    public void Roster_IsClosed()
    {
        var vocabulary = typeof(IrExpression).Assembly.GetTypes()
            .Where(t => typeof(IrExpression).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(IrOptionalWrappedElement))
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var directory = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();
        var read = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\bIrNodeFor<(\w+)>").Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        read.Should().Contain("IrIndexAccess", "the census sees the emitter's by-key reads");

        var roster = vocabulary.Where(read.Contains).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var tabled = Pinned.Keys.Concat(Excluded.Keys).OrderBy(n => n, StringComparer.Ordinal).ToList();

        tabled.Except(vocabulary).Should().BeEmpty("every pinned or excluded entry names an IrExpression kind");
        roster.Except(tabled).Should().BeEmpty(
            "every IR expression kind the emitter reads by key needs a look-through pin row or an excluded reason");
        tabled.Except(roster).Should().BeEmpty(
            "a table entry whose kind the emitter no longer reads by key is stale; delete it");
    }

    /// <summary>
    /// <see cref="IrLoweredLoop"/>: the wrapped child says "not presized" (Capacity null) over a sized
    /// list source, where the absent path recomputes the presize and hoists <c>__src_N</c>.
    /// </summary>
    [Fact]
    public void WrappedLoweredLoop_SizingComesFromTheChild()
    {
        const string source = """
            def main() -> None:
                xs: list[int] = [1, 2, 3]
                ys = [x * 2 for x in xs]
                print(ys)
            """;
        var baseline = Emit(source, rewrite: null);
        baseline.Should().Contain("__src_", "the control: a sized single-for source is presized");

        var wrapped = Emit(source, (ast, ir) =>
        {
            var comp = Find<ListComprehension>(ast);
            var loop = (IrLoweredLoop)ir.Index[comp];
            return (comp, loop with { Capacity = null });
        });
        wrapped.Should().NotContain("__src_", "the wrapped child's Capacity is read through the wrap");
    }

    /// <summary>
    /// <see cref="IrLoweredGenerator"/>: the wrapped child's clause list drops the <c>if</c>, where the
    /// absent path reads the AST's clauses and keeps the filter.
    /// </summary>
    [Fact]
    public void WrappedLoweredGenerator_ClausesComeFromTheChild()
    {
        const string source = """
            def main() -> None:
                xs: list[int] = [1, 2, 3]
                g = (x for x in xs if x > 1)
                print(list(g))
            """;
        var baseline = Emit(source, rewrite: null);
        baseline.Should().Contain(".Where(", "the control: the filter emits a Where");

        var wrapped = Emit(source, (ast, ir) =>
        {
            var gen = Find<GeneratorExpression>(ast);
            var node = (IrLoweredGenerator)ir.Index[gen];
            return (gen, node with { Clauses = node.Clauses.Where(c => c is not IfClause).ToImmutableArray() });
        });
        wrapped.Should().NotContain(".Where(", "the wrapped child's clauses are read through the wrap");
    }

    /// <summary>
    /// <see cref="IrMemberAccess"/>: the wrapped child resolves the member's CLR name to <c>Zz</c>, a
    /// fact no program discriminates (the checker records the spelled member's name).
    /// </summary>
    [Fact]
    public void WrappedMemberAccess_ResolvedClrNameComesFromTheChild()
    {
        const string source = """
            class Box:
                value: int

                def __init__(self) -> None:
                    self.value = 4


            def main() -> None:
                b = Box()
                print(b.value)
            """;
        var baseline = Emit(source, rewrite: null);
        baseline.Should().Contain("b.Value").And.NotContain(".Zz", "the control: the field is PascalCased");

        var wrapped = Emit(source, (ast, ir) =>
        {
            var access = Find<MemberAccess>(ast, m => m.Object is Identifier { Name: "b" } && m.Member == "value");
            var node = ir.Index[access] as IrMemberAccess
                ?? new IrMemberAccess((IrExpression)ir.Index[access.Object], null, null,
                    ((IrExpression)ir.Index[access]).Type, ir.Index[access].Span);
            return (access, node with { ResolvedClrMemberName = "Zz" });
        });
        wrapped.Should().Contain("b.Zz", "the wrapped child's resolved CLR name is read through the wrap");
    }

    /// <summary>
    /// Emits <paramref name="source"/>; when <paramref name="rewrite"/> is given, its key is re-indexed
    /// to an <see cref="IrOptionalWrappedElement"/> whose single child is the returned node — a shape
    /// only the wrap's look-through can read.
    /// </summary>
    private static string Emit(string source, Func<Module, IrCompilation, (Node Key, IrExpression Child)>? rewrite)
    {
        var module = EmitterTestPipeline.Parse(source);
        var analysis = EmitterTestPipeline.Analyze(module, isEntryPoint: true, rewriteIr: rewrite == null
            ? null
            : ir =>
            {
                var (key, child) = rewrite(module, ir);
                var map = new Dictionary<Node, IrNode>(ReferenceEqualityComparer.Instance);
                foreach (var entry in ir.Index)
                    map[entry.Key] = entry.Value;
                map[key] = new IrOptionalWrappedElement(
                    new OptionalType { UnderlyingType = child.Type ?? SemanticType.Unknown },
                    child.Type, child.Span, ImmutableArray.Create<IrNode>(child));
                return ir with { Index = new IrIndex(map) };
            });
        analysis.TypeChecker.Diagnostics.GetErrors().Should().BeEmpty();
        return analysis.Emitter.GenerateCompilationUnit(analysis.Module).NormalizeWhitespace().ToFullString();
    }

    private static T Find<T>(Module module, Func<T, bool>? predicate = null) where T : Node
    {
        IEnumerable<Node> Walk(Node node)
        {
            yield return node;
            foreach (var child in node.GetChildNodes())
                foreach (var descendant in Walk(child))
                    yield return descendant;
        }

        return Walk(module).OfType<T>().Single(n => predicate?.Invoke(n) ?? true);
    }
}
