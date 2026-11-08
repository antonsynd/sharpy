using System.Collections.Generic;
using System.IO;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// Totality guards for the E2 lowering IR (#1056): the lowering pass must produce exactly one IR
/// node per AST expression/statement, and — until migration replaces them — those nodes are opaque
/// wrappers. The opaque-wrapper census pins how many AST types still lower to opaque so migration
/// progress is visible.
/// </summary>
public class IrTotalityTests
{
    private readonly ITestOutputHelper _output;

    public IrTotalityTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// A representative module exercising the tranche-1 constructs (equality, index access, calls,
    /// literals) plus the common statement forms, all of which currently lower to opaque wrappers.
    /// </summary>
    private const string RepresentativeSource = """
        class Box:
            value: int

            def __init__(self, start: int) -> None:
                self.value = start

            def total(self, extra: int) -> int:
                return self.value + extra


        def compute(n: int) -> int:
            numbers: list[int] = [1, 2, 3]
            labels: dict[str, int] = {"a": 1}
            running: int = 0
            for item in numbers:
                running = running + item
            counter: int = 0
            while counter < n:
                running = running + counter
                counter = counter + 1
            if running == 6:
                running = running * 2
            elif running < 6:
                running = 0
            else:
                running = running - 1
            head: int = numbers[0]
            running = running + labels["a"]
            box: Box = Box(running)
            return box.total(head)


        def main() -> None:
            print(compute(3))

        """;

    [Fact]
    public void EveryExpressionAndStatement_LowersToExactlyOneIrNode()
    {
        var (model, ir) = CompileToIr(RepresentativeSource);
        var unit = model.Units.Values.Single(u => u.Ast != null);
        var ast = unit.Ast!;
        var index = ir.Index;

        // Every Expression/Statement reachable in the AST must have lowered to exactly one IR node,
        // keyed by identity, with a matching span.
        var astNodes = new HashSet<Node>(ReferenceEqualityComparer.Instance);
        foreach (var node in WalkExpressionsAndStatements(ast))
        {
            Assert.True(astNodes.Add(node), $"AST node visited more than once: {node.GetType().Name}");
            Assert.True(index.ContainsKey(node), $"AST {node.GetType().Name} @ {SpanOf(node)} did not lower to an IR node");
            Assert.Equal(SpanOf(node), index[node].Span);
        }

        // No drops and no extras: the index holds exactly one entry per AST expression/statement.
        Assert.Equal(astNodes.Count, index.Count);

        // The IR tree itself carries exactly one IrExpression/IrStatement per AST node.
        Assert.Equal(astNodes.Count, CountExpressionAndStatementNodes(ir));
    }

    [Fact]
    public void OpaqueCensus_PinsTheSetOfUnmigratedAstTypes()
    {
        var (_, ir) = CompileToIr(RepresentativeSource);
        var opaque = CollectOpaqueAstTypeNames(ir);

        _output.WriteLine("Opaque AST census: " + string.Join(", ", opaque));

        // The set of AST types still lowering to opaque wrappers. In E2 Phase 1 nothing is migrated,
        // so this is every expression/statement type the representative module contains. As tranche
        // migrations land (Phase 2/3), a type that FULLY migrates drops from this list — update it
        // deliberately then (the same discipline as snapshot regeneration), never to mask a bug.
        string[] expected =
        {
            "Assignment",
            "BinaryOp",
            "ClassDef",
            "DictLiteral",
            "ExpressionStatement",
            "ForStatement",
            "FunctionCall",
            "FunctionDef",
            "Identifier",
            "IfStatement",
            "IntegerLiteral",
            "ListLiteral",
            "MemberAccess",
            "ReturnStatement",
            "StringLiteral",
            "VariableDeclaration",
            "WhileStatement",
        };

        Assert.Equal(expected, opaque);
    }

    /// <summary>
    /// The comprehension transform (E2 #1056) lowers a list/set/dict comprehension to an
    /// <see cref="IrLoweredLoop"/> carrying its emission decisions, and the tree stays total. The
    /// representative module above contains no comprehension, so the opaque census does not track this
    /// migration; this focused case pins it instead. A single-<c>for</c> comprehension over a sized
    /// <c>list[int]</c> source exercises the D4 capacity decision (<see cref="IrLoweredLoop.Capacity"/>
    /// non-null).
    /// </summary>
    [Fact]
    public void Comprehension_LowersToIrLoweredLoop_AndStaysTotal()
    {
        const string source = """
            def build(xs: list[int]) -> list[int]:
                return [x * 2 for x in xs]


            def main() -> None:
                print(len(build([1, 2, 3])))

            """;
        var (model, ir) = CompileToIr(source);

        // Exactly one IrLoweredLoop, carrying the migrated decisions.
        var loop = Assert.Single(CollectNodes<IrLoweredLoop>(ir));
        Assert.Equal(BuiltinNames.List, loop.CollectionKind);
        Assert.NotNull(loop.SoleForClause);   // exactly one for-clause
        Assert.NotNull(loop.Capacity);        // xs: list[int] is a sized source → D4 preallocation
        Assert.False(loop.ElementIsSpread);

        // Totality holds with the migrated node: every AST expression/statement in the build module
        // lowers to exactly one IR node (the Capacity reuses the already-indexed source iterator, so
        // it adds no duplicate node).
        var ast = model.Units.Values.Single(u => u.Ast != null).Ast!;
        var index = ir.Index;
        var astNodes = new HashSet<Node>(ReferenceEqualityComparer.Instance);
        foreach (var node in WalkExpressionsAndStatements(ast))
        {
            Assert.True(astNodes.Add(node), $"AST node visited more than once: {node.GetType().Name}");
            Assert.True(index.ContainsKey(node), $"AST {node.GetType().Name} did not lower to an IR node");
        }

        Assert.Equal(astNodes.Count, index.Count);
        Assert.Equal(astNodes.Count, CountExpressionAndStatementNodes(ir));
    }

    private static readonly string FixturesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Sharpy.Compiler.Tests", "Integration", "TestFixtures"));

    /// <summary>
    /// P82 (#2443): an augmented assignment on a mutable collection lowers to an
    /// <see cref="IrCollectionMutation"/> carrying its CLR mutator (migrates
    /// <c>SemanticInfo._augmentedAssignMutations</c>), over the Phase 0 positive-control fixture:
    /// the seven <c>Classify</c> arms on identifiers, the member and index targets, the narrowed
    /// Optional / nullable receivers. The <c>n += 2</c> int rebind stays opaque, and the tree stays total.
    /// </summary>
    [Fact]
    public void AugmentedCollectionMutation_LowersToIrCollectionMutation_AndStaysTotal()
    {
        var source = File.ReadAllText(Path.Combine(FixturesRoot, "basics", "augmented_collection_mutation_targets.spy"));
        var (model, ir) = CompileToIr(source);
        var ast = model.Units.Values.Single(u => u.Ast != null).Ast!;
        var index = ir.Index;

        // Totality: one IR node per AST expression/statement, keyed by identity.
        var astNodes = new HashSet<Node>(ReferenceEqualityComparer.Instance);
        foreach (var node in WalkExpressionsAndStatements(ast))
        {
            Assert.True(astNodes.Add(node), $"AST node visited more than once: {node.GetType().Name}");
            Assert.True(index.ContainsKey(node), $"AST {node.GetType().Name} @ {SpanOf(node)} did not lower to an IR node");
            Assert.Equal(SpanOf(node), index[node].Span);
        }

        Assert.Equal(astNodes.Count, index.Count);
        Assert.Equal(astNodes.Count, CountExpressionAndStatementNodes(ir));

        // One IrCollectionMutation per mutating store, none for the int rebind.
        var augmented = astNodes.OfType<Assignment>()
            .Where(a => a.Operator != AssignmentOperator.Assign)
            .ToList();
        Assert.Equal(13, augmented.Count);
        foreach (var assignment in augmented)
        {
            var isIntRebind = assignment.Target is Identifier { Name: "n" };
            if (isIntRebind)
                Assert.IsType<IrOpaqueStatement>(index[assignment]);
            else
                Assert.IsType<IrCollectionMutation>(index[assignment]);
        }

        var methods = CollectNodes<IrCollectionMutation>(ir)
            .Select(m => m.ClrMethodName)
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToArray();
        string[] expected =
        {
            "DifferenceUpdate",
            "Extend", "Extend", "Extend", "Extend", "Extend", "Extend",
            "InPlaceRepeat",
            "IntersectionUpdate",
            "SymmetricDifferenceUpdate",
            "Update", "Update",
        };
        Assert.Equal(expected, methods);
    }

    /// <summary>
    /// P82 (#2443): a None-bodied lambda against a void delegate lowers its body to an
    /// <see cref="IrElidedNoneBody"/> (migrates <c>SemanticInfo._lambdaBodyLowerings</c>), keyed on
    /// the body: <c>f = lambda: None</c> → node; <c>g: () -> str | None = lambda: None</c> → none (the
    /// return is not void, so <c>() => null</c>); <c>k = lambda: (None)</c> → node on the parenthesized
    /// body, holding the inner <c>None</c>. The lambda itself stays opaque, its body child substituted
    /// in place, and the tree stays total.
    /// </summary>
    [Fact]
    public void NoneBodiedVoidLambda_LowersBodyToIrElidedNoneBody_AndStaysTotal()
    {
        const string source = """
            def main():
                f = lambda: None
                g: () -> str | None = lambda: None
                k = lambda: (None)
                f()
                k()
                print(g() is None)
            """;
        var (model, ir) = CompileToIr(source);
        var ast = model.Units.Values.Single(u => u.Ast != null).Ast!;
        var index = ir.Index;

        // Totality: one IR node per AST expression/statement, keyed by identity.
        var astNodes = new HashSet<Node>(ReferenceEqualityComparer.Instance);
        foreach (var node in WalkExpressionsAndStatements(ast))
        {
            Assert.True(astNodes.Add(node), $"AST node visited more than once: {node.GetType().Name}");
            Assert.True(index.ContainsKey(node), $"AST {node.GetType().Name} @ {SpanOf(node)} did not lower to an IR node");
            Assert.Equal(SpanOf(node), index[node].Span);
        }

        Assert.Equal(astNodes.Count, index.Count);
        Assert.Equal(astNodes.Count, CountExpressionAndStatementNodes(ir));

        var lambdas = astNodes.OfType<LambdaExpression>()
            .OrderBy(l => SpanOf(l).Start)
            .ToList();
        Assert.Equal(3, lambdas.Count);
        var (f, g, k) = (lambdas[0], lambdas[1], lambdas[2]);

        // f: the bare None body is the node, and it is the lambda's body child.
        var fBody = Assert.IsType<IrElidedNoneBody>(index[f.Body]);
        Assert.Empty(fBody.Children);
        Assert.Contains(index[f].Children, child => ReferenceEquals(child, fBody));

        // g: a value-returning slot keeps its body.
        Assert.IsType<NoneLiteral>(g.Body);
        Assert.IsType<IrOpaqueExpression>(index[g.Body]);

        // k: the node keys on the parenthesized body and holds the inner None's node.
        var parenthesized = Assert.IsType<Parenthesized>(k.Body);
        var kBody = Assert.IsType<IrElidedNoneBody>(index[k.Body]);
        var inner = Assert.Single(kBody.Children);
        Assert.Same(index[parenthesized.Expression], inner);
        Assert.Contains(index[k].Children, child => ReferenceEquals(child, kBody));

        Assert.Equal(2, CollectNodes<IrElidedNoneBody>(ir).Count);
    }

    private static IReadOnlyList<T> CollectNodes<T>(IrCompilation ir) where T : IrNode
    {
        var result = new List<T>();

        void Visit(IrNode node)
        {
            if (node is T match)
                result.Add(match);
            foreach (var child in node.Children)
                Visit(child);
        }

        foreach (var module in ir.Modules)
            foreach (var statement in module.Body)
                Visit(statement);

        return result;
    }

    private (ProjectModel Model, IrCompilation Ir) CompileToIr(string source)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Test")
            .AddSourceFile("main.spy", source)
            .CreateProjectFile();

        var result = helper.Compile();

        Assert.True(result.Success,
            "representative module must compile:\n"
            + string.Join("\n", result.Diagnostics.GetAll().Select(d => $"[{d.Code}] {d.Message}")));

        var model = result.ProjectModel!;
        Assert.NotNull(model.IrCompilation);
        return (model, model.IrCompilation!);
    }

    /// <summary>
    /// Yields every Expression/Statement descendant of <paramref name="root"/> using the same
    /// <see cref="Node.GetChildNodes"/> traversal the lowering pass walks, so the set matches
    /// exactly what the pass indexes.
    /// </summary>
    private static IEnumerable<Node> WalkExpressionsAndStatements(Node root)
    {
        foreach (var child in root.GetChildNodes())
        {
            if (child is Expression or Statement)
                yield return child;
            foreach (var descendant in WalkExpressionsAndStatements(child))
                yield return descendant;
        }
    }

    private static int CountExpressionAndStatementNodes(IrCompilation ir)
    {
        var count = 0;
        foreach (var module in ir.Modules)
            foreach (var statement in module.Body)
                count += CountExpressionAndStatementNodes(statement);
        return count;
    }

    private static int CountExpressionAndStatementNodes(IrNode node)
    {
        var count = node is IrExpression or IrStatement ? 1 : 0;
        foreach (var child in node.Children)
            count += CountExpressionAndStatementNodes(child);
        return count;
    }

    private static IReadOnlyList<string> CollectOpaqueAstTypeNames(IrCompilation ir)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);

        void Visit(IrNode node)
        {
            switch (node)
            {
                case IrOpaqueExpression opaqueExpression:
                    names.Add(opaqueExpression.Ast.GetType().Name);
                    break;
                case IrOpaqueStatement opaqueStatement:
                    names.Add(opaqueStatement.Ast.GetType().Name);
                    break;
            }

            foreach (var child in node.Children)
                Visit(child);
        }

        foreach (var module in ir.Modules)
            foreach (var statement in module.Body)
                Visit(statement);

        return names.ToList();
    }

    private static TextSpan SpanOf(Node node) => node.Span ?? TextSpan.Empty;
}
