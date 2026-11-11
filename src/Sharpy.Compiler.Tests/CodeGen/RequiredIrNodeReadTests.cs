using FluentAssertions;
using Microsoft.CodeAnalysis;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The emitter's required IR read (<c>RoslynEmitter.RequiredIrNodeFor&lt;TIr&gt;</c>, #2443): for an
/// AST shape the lowering pass lowers to one IR kind unconditionally, an index miss is a compiler
/// bug and throws <see cref="InternalCompilerErrorException"/> (reported as SPY0909 by the
/// project compiler's last-chance handler) instead of reading as "fact absent". Each row drops the
/// shape's index entry from a real lowering and expects the throw; the control emits the same
/// source with the index intact.
/// </summary>
public class RequiredIrNodeReadTests
{
    public static TheoryData<string, string, string> RequiredShapes => new()
    {
        {
            "IrIndexAccess", nameof(IndexAccess), """
            def main() -> None:
                xs: list[int] = [1, 2, 3]
                print(xs[0])
            """
        },
        {
            "IrEqualityComparison", nameof(BinaryOp), """
            def main() -> None:
                a: int = 1
                print(a == 2)
            """
        },
        {
            "IrLoweredLoop", nameof(ListComprehension), """
            def main() -> None:
                xs: list[int] = [1, 2, 3]
                print([x * 2 for x in xs])
            """
        },
        {
            "IrLoweredGenerator", nameof(GeneratorExpression), """
            def main() -> None:
                xs: list[int] = [1, 2, 3]
                print(sum(x for x in xs))
            """
        },
    };

    [Theory]
    [MemberData(nameof(RequiredShapes))]
    public void MissingIrNode_ForAnUnconditionallyLoweredShape_IsAnInternalCompilerError(
        string irKind, string astKind, string source)
    {
        // Control: the intact index emits.
        var intact = EmitterTestPipeline.Analyze(EmitterTestPipeline.Parse(source), isEntryPoint: true);
        intact.TypeChecker.Diagnostics.GetErrors().Should().BeEmpty();
        intact.Emitter.Invoking(e => e.GenerateCompilationUnit(intact.Module)).Should().NotThrow();

        var module = EmitterTestPipeline.Parse(source);
        Node? dropped = null;
        var analysis = EmitterTestPipeline.Analyze(module, isEntryPoint: true, rewriteIr: ir =>
        {
            dropped = Walk(module).Single(n => n.GetType().Name == astKind);
            ir.Index[dropped].GetType().Name.Should().Be(irKind, "the pass lowers the shape to this kind");
            var map = new Dictionary<Node, IrNode>(ReferenceEqualityComparer.Instance);
            foreach (var entry in ir.Index.Where(e => !ReferenceEquals(e.Key, dropped)))
                map[entry.Key] = entry.Value;
            return ir with { Index = new IrIndex(map) };
        });

        var thrown = analysis.Emitter.Invoking(e => e.GenerateCompilationUnit(analysis.Module))
            .Should().Throw<InternalCompilerErrorException>().Which;
        thrown.Message.Should().Contain(irKind).And.Contain("no index entry");
        thrown.Node.Should().BeSameAs(dropped, "the crash bundle locates the node the walk never lowered");
    }

    /// <summary>
    /// The one exempt context: a direct-emitter unit context over <see cref="IrCompilation.Empty"/>
    /// keeps the optional read and the caller's fallback (no production path builds one;
    /// <c>OptionalStoreWrapAgreementTests</c> asserts no corpus emitter ran over it).
    /// </summary>
    [Fact]
    public void EmptyIrCompilation_KeepsTheReadOptional()
    {
        var source = """
            def main() -> None:
                xs: list[int] = [1, 2, 3]
                print(xs[0] == 1)
            """;
        var module = EmitterTestPipeline.Parse(source);
        var analysis = EmitterTestPipeline.Analyze(module, isEntryPoint: true, rewriteIr: _ => IrCompilation.Empty);
        analysis.Emitter.Invoking(e => e.GenerateCompilationUnit(analysis.Module)).Should().NotThrow();
    }

    private static IEnumerable<Node> Walk(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildNodes())
            foreach (var descendant in Walk(child))
                yield return descendant;
    }
}
