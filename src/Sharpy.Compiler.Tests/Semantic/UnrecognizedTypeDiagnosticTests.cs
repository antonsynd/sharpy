using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests that unrecognized AST types in the TypeChecker emit proper diagnostics (SPY0255, SPY0256)
/// instead of being silently ignored or returning Unknown without reporting.
/// </summary>
public class UnrecognizedTypeDiagnosticTests
{
    /// <summary>
    /// A custom statement type that the TypeChecker does not recognize.
    /// </summary>
    private record FakeStatement : Statement;

    /// <summary>
    /// A custom expression type that the TypeChecker does not recognize.
    /// </summary>
    private record FakeExpression : Expression;

    private TypeChecker CreateTypeChecker()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };

        return typeChecker;
    }

    [Fact]
    public void CheckModule_UnrecognizedStatement_EmitsSPY0255()
    {
        var typeChecker = CreateTypeChecker();

        var fakeStmt = new FakeStatement { LineStart = 3, ColumnStart = 1 };
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(fakeStmt)
        };

        typeChecker.CheckModule(module);

        typeChecker.Diagnostics.ShouldHaveErrorWithCode(DiagnosticCodes.Semantic.UnrecognizedStatementType);

        var error = typeChecker.Diagnostics.GetErrors()
            .First(e => e.Code == DiagnosticCodes.Semantic.UnrecognizedStatementType);
        Assert.Contains("FakeStatement", error.Message);
        Assert.Contains("compiler bug", error.Message);
    }

    [Fact]
    public void CheckExpression_UnrecognizedExpression_EmitsSPY0256()
    {
        var typeChecker = CreateTypeChecker();

        var fakeExpr = new FakeExpression { LineStart = 7, ColumnStart = 5 };
        var exprStmt = new ExpressionStatement { Expression = fakeExpr };
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(exprStmt)
        };

        typeChecker.CheckModule(module);

        typeChecker.Diagnostics.ShouldHaveErrorWithCode(DiagnosticCodes.Semantic.UnrecognizedExpressionType);

        var error = typeChecker.Diagnostics.GetErrors()
            .First(e => e.Code == DiagnosticCodes.Semantic.UnrecognizedExpressionType);
        Assert.Contains("FakeExpression", error.Message);
        Assert.Contains("compiler bug", error.Message);
    }

    [Fact]
    public void CheckExpression_UnrecognizedExpression_ReturnsUnknownType()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };

        var fakeExpr = new FakeExpression { LineStart = 1, ColumnStart = 1 };

        // CheckExpression is public — call it directly
        var resultType = typeChecker.CheckExpression(fakeExpr);

        Assert.IsType<UnknownType>(resultType);
        typeChecker.Diagnostics.ShouldHaveErrorWithCode(DiagnosticCodes.Semantic.UnrecognizedExpressionType);
    }
}
