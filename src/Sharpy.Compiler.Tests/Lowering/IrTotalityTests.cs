using System.Collections.Generic;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Parser.Ast;
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
