using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Verifies that the TypeChecker records a <see cref="StatementLowering"/> for every
/// <see cref="ExpressionStatement"/> it accepts (#1622, plan c6ae1b D5) — the full kind matrix:
/// call, await, <c>None</c>, method group (identifier AND member), literal, member value, index,
/// binary op, comprehension, walrus — and that the shapes a C# discard cannot type (a bare
/// lambda, a non-call expression of type <c>None</c>) are refused with SPY0603 and record
/// <b>no</b> lowering (the emitter throws on an absent fact). The third refused shape, a bare
/// module reference, needs import resolution this bare harness does not run; it is pinned by
/// the <c>statements/expression_statement_refuse_module_1622</c> fixture.
///
/// <para><c>ElideMethodGroupStatement</c> is the kind <c>MustUseValidator</c> reads to emit
/// SPY0480; the two method-group rows pin it for both spellings.</para>
/// </summary>
public class StatementLoweringRecordingTests
{
    private static (Module module, SemanticInfo info, DiagnosticBag diagnostics) Analyze(string source)
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

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module, isEntryPoint: true);

        return (module, semanticInfo, typeChecker.Diagnostics);
    }

    private static IEnumerable<ExpressionStatement> FindExpressionStatements(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            if (child is ExpressionStatement es)
                yield return es;
            foreach (var desc in FindExpressionStatements(child))
                yield return desc;
        }
    }

    /// <summary>
    /// A program with one <c>main</c> whose body is <paramref name="statement"/>, plus the
    /// declarations every row may need (a None-returning function, an int-returning one, an
    /// async one, and a class with a field and a method).
    /// </summary>
    private static string Program(string statement) => $@"
class Box:
    v: int

    def __init__(self) -> None:
        self.v = 1

    def m(self) -> int:
        return 2


def sink() -> None:
    pass


def value() -> int:
    return 42


async def avoid() -> None:
    pass


async def main() -> None:
    b: Box = Box()
    xs: list[int] = [1, 2, 3]
    {statement}
";

    /// <summary>The lowering of the LAST expression statement in <c>main</c> (the one under test).</summary>
    private static (StatementLowering? lowering, DiagnosticBag diagnostics) LoweringOfLast(string statement)
    {
        var (module, info, diagnostics) = Analyze(Program(statement));
        var last = FindExpressionStatements(module).Last();
        return (info.GetStatementLowering(last), diagnostics);
    }

    [Theory]
    [InlineData("sink()", StatementLoweringKind.PlainStatement)]
    [InlineData("value()", StatementLoweringKind.PlainStatement)]
    [InlineData("b.m()", StatementLoweringKind.PlainStatement)]
    [InlineData("(sink())", StatementLoweringKind.PlainStatement)]
    [InlineData("await avoid()", StatementLoweringKind.PlainStatement)]
    [InlineData("None", StatementLoweringKind.ElideNoneLiteral)]
    [InlineData("(None)", StatementLoweringKind.ElideNoneLiteral)]
    [InlineData("sink", StatementLoweringKind.ElideMethodGroupStatement)]
    [InlineData("b.m", StatementLoweringKind.ElideMethodGroupStatement)]
    [InlineData("42", StatementLoweringKind.Discard)]
    [InlineData("\"docstringish\"", StatementLoweringKind.Discard)]
    [InlineData("True", StatementLoweringKind.Discard)]
    [InlineData("b.v", StatementLoweringKind.Discard)]
    [InlineData("xs[value() % 3]", StatementLoweringKind.Discard)]
    [InlineData("1 + 2", StatementLoweringKind.Discard)]
    [InlineData("1 < 2", StatementLoweringKind.Discard)]
    [InlineData("[x for x in range(2)]", StatementLoweringKind.Discard)]
    [InlineData("(w := 5)", StatementLoweringKind.Discard)]
    [InlineData("value() if True else 0", StatementLoweringKind.Discard)]
    public void EveryAcceptedKind_RecordsItsLowering(string statement, StatementLoweringKind expected)
    {
        var (lowering, diagnostics) = LoweringOfLast(statement);

        Assert.False(diagnostics.HasErrors,
            string.Join("\n", diagnostics.GetAll().Select(d => $"{d.Code}: {d.Message}")));
        Assert.NotNull(lowering);
        Assert.Equal(expected, lowering!.Kind);
    }

    [Theory]
    [InlineData("lambda: 1", "a lambda cannot be an expression statement")]
    [InlineData("(lambda: 1)", "a lambda cannot be an expression statement")]
    [InlineData("sink() if True else sink()", "expression statement of type 'None' must be a call")]
    [InlineData("(sink() if True else sink())", "expression statement of type 'None' must be a call")]
    public void UndiscardableShape_IsRefusedWithSpy0603_AndRecordsNoLowering(string statement, string message)
    {
        var (lowering, diagnostics) = LoweringOfLast(statement);

        var refusal = diagnostics.GetAll().SingleOrDefault(
            d => d.Code == DiagnosticCodes.SemanticOverflow.ExpressionStatementNotDiscardable);
        Assert.True(refusal != null,
            "expected SPY0603, got: " + string.Join("\n", diagnostics.GetAll().Select(d => $"{d.Code}: {d.Message}")));
        Assert.Contains(message, refusal!.Message);
        Assert.Null(lowering);
    }

    [Fact]
    public void EveryExpressionStatement_HasLowering()
    {
        var (module, info, diagnostics) = Analyze(@"
def greet() -> None:
    pass

def main():
    greet()
    42
    None
    1 + 2
    True
    greet
");
        Assert.False(diagnostics.HasErrors);
        var stmts = FindExpressionStatements(module).ToList();
        Assert.Equal(6, stmts.Count);
        foreach (var stmt in stmts)
        {
            var lowering = info.GetStatementLowering(stmt);
            Assert.NotNull(lowering);
        }
    }
}
