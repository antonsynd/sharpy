using Sharpy.Compiler.Parser.Ast;

using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Verifies that the TypeChecker records a <see cref="StatementLowering"/> for every
/// <see cref="ExpressionStatement"/> (#1622).
/// </summary>
public class StatementLoweringRecordingTests
{
    private static (Module module, SemanticInfo info) Analyze(string source)
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

        return (module, semanticInfo);
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

    [Fact]
    public void FunctionCall_RecordsPlainStatement()
    {
        var (module, info) = Analyze(@"
def foo() -> None:
    pass

def main():
    foo()
");
        var stmts = FindExpressionStatements(module).ToList();
        var callStmt = stmts.First(s => s.Expression is FunctionCall);
        Assert.Equal(StatementLoweringKind.PlainStatement,
            info.GetStatementLowering(callStmt)!.Kind);
    }

    [Fact]
    public void NoneLiteral_RecordsElideNoneLiteral()
    {
        var (module, info) = Analyze(@"
def main():
    None
");
        var stmts = FindExpressionStatements(module).ToList();
        var noneStmt = stmts.First(s => s.Expression is NoneLiteral);
        Assert.Equal(StatementLoweringKind.ElideNoneLiteral,
            info.GetStatementLowering(noneStmt)!.Kind);
    }

    [Fact]
    public void IntegerLiteral_RecordsDiscard()
    {
        var (module, info) = Analyze(@"
def main():
    42
");
        var stmts = FindExpressionStatements(module).ToList();
        var litStmt = stmts.First(s => s.Expression is IntegerLiteral);
        Assert.Equal(StatementLoweringKind.Discard,
            info.GetStatementLowering(litStmt)!.Kind);
    }

    [Fact]
    public void BooleanLiteral_RecordsDiscard()
    {
        var (module, info) = Analyze(@"
def main():
    True
");
        var stmts = FindExpressionStatements(module).ToList();
        var litStmt = stmts.First(s => s.Expression is BooleanLiteral);
        Assert.Equal(StatementLoweringKind.Discard,
            info.GetStatementLowering(litStmt)!.Kind);
    }

    [Fact]
    public void BinaryOp_RecordsDiscard()
    {
        var (module, info) = Analyze(@"
def main():
    1 + 2
");
        var stmts = FindExpressionStatements(module).ToList();
        var binStmt = stmts.First(s => s.Expression is BinaryOp);
        Assert.Equal(StatementLoweringKind.Discard,
            info.GetStatementLowering(binStmt)!.Kind);
    }

    [Fact]
    public void EveryExpressionStatement_HasLowering()
    {
        var (module, info) = Analyze(@"
def greet() -> None:
    pass

def main():
    greet()
    42
    None
    1 + 2
    True
");
        var stmts = FindExpressionStatements(module).ToList();
        foreach (var stmt in stmts)
        {
            var lowering = info.GetStatementLowering(stmt);
            Assert.NotNull(lowering);
        }
    }
}
