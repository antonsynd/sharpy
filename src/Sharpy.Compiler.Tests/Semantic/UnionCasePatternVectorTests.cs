using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// A union-case pattern records the scrutinee union's SUBSTITUTED type-argument vector beside its case
/// symbol (#1703, Decision 4), so codegen can spell the closed case type — <c>Result&lt;int,
/// string&gt;.Ok</c>, not <c>Result&lt;T, E&gt;.Ok</c> — instead of re-deriving it from the scrutinee.
/// The vector rides in <see cref="SemanticInfo.GetPatternUnionCaseTypeArguments"/>, a sibling accessor
/// to <see cref="SemanticInfo.GetPatternUnionCase"/> whose <see cref="TypeSymbol"/> return shape is
/// unchanged (its 18 consumers keep working).
/// </summary>
public class UnionCasePatternVectorTests
{
    [Fact]
    public void ResultCasePattern_RecordsTheScrutineeSubstitutedVector()
    {
        // `case Ok(v):` over a `Result[int, str]` scrutinee records [int, str]. Mutation guard 2d:
        // dropping the recorded vector (storing null) makes this null -> red. The emitter reads this
        // in Phase 3 to spell Result<int, string>.Ok.
        var (module, info) = Analyze(@"
def describe(r: int !str) -> None:
    match r:
        case Ok(v):
            print(v)
        case Err(e):
            print(e)
");

        var okPattern = Descendants(module)
            .OfType<Pattern>()
            .First(p => info.GetPatternUnionCase(p)?.Name == "Ok");

        var vector = info.GetPatternUnionCaseTypeArguments(okPattern);
        vector.Should().NotBeNull("the Result scrutinee's substituted vector is recorded on the case head");
        vector!.Should().Equal(SemanticType.Int, SemanticType.Str);
    }

    [Fact]
    public void UnionCaseHead_IsRecordedThroughTheAnnotationSeam()
    {
        // #1737/#1735: the union-case head is recorded on the annotation seam (SetTypeAnnotation), the
        // same seam class-pattern heads use, so hover answers for it (Phase 5) and the reference
        // matrix sees it. Mutation guard: dropping RecordUnionCaseHeadReference leaves the head
        // annotation with no recorded type -> red.
        var (module, info) = Analyze(@"
def describe(r: int !str) -> None:
    match r:
        case Ok(v):
            print(v)
        case Err(e):
            print(e)
");

        var okPositional = Descendants(module)
            .OfType<PositionalPattern>()
            .First(p => info.GetPatternUnionCase(p)?.Name == "Ok");

        okPositional.Type.Should().NotBeNull("the case head carries a type annotation");
        info.GetTypeAnnotation(okPositional.Type!).Should().NotBeNull(
            "the union-case head is recorded once through the annotation seam (#1737)");
    }

    [Fact]
    public void GetPatternUnionCase_ShapeUnchanged_ReturnsTheCaseSymbol()
    {
        // The sibling accessor did not disturb the case-symbol return the 18 consumers read.
        var (module, info) = Analyze(@"
def describe(r: int !str) -> None:
    match r:
        case Ok(v):
            print(v)
        case Err(e):
            print(e)
");

        var okPattern = Descendants(module)
            .OfType<Pattern>()
            .First(p => info.GetPatternUnionCase(p)?.Name == "Ok");

        info.GetPatternUnionCase(okPattern)!.Name.Should().Be("Ok");
    }

    private static (Module Module, SemanticInfo Info) Analyze(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var pipeline = ValidationPipelineFactory.CreateDefault(NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance, pipeline)
        {
            SemanticBinding = semanticBinding
        };

        typeChecker.CheckModule(module);

        return (module, semanticInfo);
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
