using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Verifies that the TypeChecker records an <see cref="IterationLowering"/> on the iterator of a
/// for-statement AND of a comprehension for-clause (same fact, two readers — #1623): str iterates
/// its chars, an int-backed enum its values, a string-backed enum its <c>Values</c> member; any
/// other iterable records nothing. The emitter side is <c>RoslynEmitterOperatorLoweringTests</c>.
/// </summary>
public class IterationLoweringRecordingTests
{
    private const string Enums = @"
enum Color:
    RED = 1
    GREEN = 2

enum Tone:
    LOW = ""low""
    HIGH = ""high""
";

    private static (Module module, SemanticInfo info, IReadOnlyList<string> errors) Analyze(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var errors = typeChecker.Diagnostics.GetErrors().Select(e => $"{e.Code}: {e.Message}").ToList();
        return (module, semanticInfo, errors);
    }

    private static IEnumerable<T> Find<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildNodes())
        {
            if (child is T match)
                yield return match;
            foreach (var descendant in Find<T>(child))
                yield return descendant;
        }
    }

    [Theory]
    [InlineData("s", IterationLoweringKind.StringChars)]
    [InlineData("Color", IterationLoweringKind.EnumValues)]
    [InlineData("Tone", IterationLoweringKind.StringEnumValues)]
    public void ForStatement_RecordsTheIteratorLowering(string iterable, IterationLoweringKind expected)
    {
        var (module, info, errors) = Analyze(Enums + $@"
def main() -> None:
    s: str = ""ab""
    for x in {iterable}:
        pass
");
        errors.Should().BeEmpty();
        var forStmt = Find<ForStatement>(module).Single();
        info.GetIterationLowering(forStmt.Iterator)!.Kind.Should().Be(expected);
    }

    [Theory]
    [InlineData("s", IterationLoweringKind.StringChars)]
    [InlineData("Color", IterationLoweringKind.EnumValues)]
    [InlineData("Tone", IterationLoweringKind.StringEnumValues)]
    public void ComprehensionForClause_RecordsTheSameIteratorLowering(string iterable, IterationLoweringKind expected)
    {
        var (module, info, errors) = Analyze(Enums + $@"
def main() -> None:
    s: str = ""ab""
    xs = [x for x in {iterable}]
");
        errors.Should().BeEmpty();
        var comprehension = Find<ListComprehension>(module).Single();
        var iterator = comprehension.Clauses.OfType<ForClause>().Single().Iterator;
        info.GetIterationLowering(iterator)!.Kind.Should().Be(expected);
    }

    [Fact]
    public void ListIteration_RecordsNoLowering_InEitherPosition()
    {
        var (module, info, errors) = Analyze(@"
def main() -> None:
    items: list[int] = [1, 2]
    for x in items:
        pass
    ys = [y for y in items]
");
        errors.Should().BeEmpty();
        info.GetIterationLowering(Find<ForStatement>(module).Single().Iterator).Should().BeNull();
        info.GetIterationLowering(Find<ListComprehension>(module).Single().Clauses.OfType<ForClause>().Single().Iterator).Should().BeNull();
    }
}
