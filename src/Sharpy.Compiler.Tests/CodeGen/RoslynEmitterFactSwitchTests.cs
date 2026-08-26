using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Emitter-side mutation tests for two fact switches (plan c6ae1b D0c): the emitted syntax
/// must follow the recorded <see cref="SemanticInfo"/> fact on the <b>same</b> AST — flipping
/// the tag flips the output — and an absent fact must throw rather than fall back to an AST
/// shape predicate.
///
/// <list type="bullet">
///   <item><see cref="MultiAxisAccessLowering"/> read by <c>GenerateMultiAxisAccess</c> (#1621):
///   two Index dimensions emit <c>a[1, 2]</c> under <c>IndexSpread</c> and a <c>.Slice(…)</c>
///   call under <c>SliceCall [Slice, Index]</c>, proving the emitter reads the recorded
///   per-dimension kinds and never <c>SubscriptDimension.IsSlice</c>.</item>
///   <item><see cref="StatementLowering"/> read by <c>GenerateExpressionStatement</c> (#1622):
///   the same <c>f()</c> statement emits a bare invocation under <c>PlainStatement</c> and
///   <c>_ = f();</c> under <c>Discard</c>, proving the emitter reads the tag and never
///   <c>expr is FunctionCall</c>.</item>
/// </list>
///
/// Every AST here is hand-built with no semantic analysis, so a fact is present only when the
/// test records it — the absent-fact arms are therefore real absences, not analysis failures.
/// </summary>
public class RoslynEmitterFactSwitchTests
{
    private readonly CodeGenContext _context;
    private readonly RoslynEmitter _emitter;
    private readonly MethodInfo _generateExpression;
    private readonly MethodInfo _generateBodyStatement;

    public RoslynEmitterFactSwitchTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _emitter = new RoslynEmitter(_context);
        _generateExpression = typeof(RoslynEmitter).GetMethod(
            "GenerateExpression", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GenerateExpression not found");
        _generateBodyStatement = typeof(RoslynEmitter).GetMethod(
            "GenerateBodyStatement", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GenerateBodyStatement not found");
    }

    private ExpressionSyntax GenerateExpression(Expression expr)
        => (ExpressionSyntax)_generateExpression.Invoke(_emitter, new object[] { expr })!;

    private StatementSyntax GenerateStatement(Statement stmt)
        => (StatementSyntax)_generateBodyStatement.Invoke(_emitter, new object[] { stmt })!;

    #region MultiAxisAccessLowering (#1621)

    /// <summary>
    /// <c>a[1, 2]</c> — two Index dimensions. Deliberately contains NO slice dimension so the
    /// SliceCall arm below can only be reached through the recorded fact.
    /// </summary>
    private static MultiAxisAccess TwoIndexDimensions() => new()
    {
        Object = new Identifier { Name = "a" },
        Dimensions = ImmutableArray.Create(
            new SubscriptDimension { IsSlice = false, Index = new IntegerLiteral { Value = "1" } },
            new SubscriptDimension { IsSlice = false, Index = new IntegerLiteral { Value = "2" } }),
    };

    [Fact]
    public void MultiAxis_IndexSpreadFact_EmitsElementAccess()
    {
        var access = TwoIndexDimensions();
        var info = new SemanticInfo();
        info.SetMultiAxisAccessLowering(access, new MultiAxisAccessLowering(
            MultiAxisAccessKind.IndexSpread,
            ImmutableArray.Create(MultiAxisDimensionKind.Index, MultiAxisDimensionKind.Index)));
        _context.SemanticInfo = info;

        var result = GenerateExpression(access);

        result.Should().BeOfType<ElementAccessExpressionSyntax>();
        result.NormalizeWhitespace().ToFullString().Should().Be("a[1, 2]");
    }

    [Fact]
    public void MultiAxis_SliceCallFact_OnSameAst_EmitsSliceCall()
    {
        // Same AST as the IndexSpread case; only the recorded fact differs. If the emitter read
        // dim.IsSlice (both false) it would emit a[1, 2] here.
        var access = TwoIndexDimensions();
        var info = new SemanticInfo();
        info.SetMultiAxisAccessLowering(access, new MultiAxisAccessLowering(
            MultiAxisAccessKind.SliceCall,
            ImmutableArray.Create(MultiAxisDimensionKind.Slice, MultiAxisDimensionKind.Index)));
        _context.SemanticInfo = info;

        var result = GenerateExpression(access);

        result.Should().BeOfType<InvocationExpressionSyntax>();
        var code = result.NormalizeWhitespace().ToFullString();
        code.Should().StartWith("a.Slice(");
        code.Should().Contain("SliceSpec.All");
        code.Should().Contain("SliceSpec.Range(2, 2 + 1)");
        code.Should().NotContain("a[");
    }

    [Fact]
    public void MultiAxis_AbsentFact_Throws()
    {
        var access = TwoIndexDimensions();
        _context.SemanticInfo = new SemanticInfo();

        var act = () => GenerateExpression(access);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*No MultiAxisAccessLowering recorded*");
    }

    [Fact]
    public void MultiAxis_FactWithWrongDimensionCount_Throws()
    {
        // A fact that does not cover every dimension is as unusable as no fact: the emitter must
        // not pad the missing kinds from the AST.
        var access = TwoIndexDimensions();
        var info = new SemanticInfo();
        info.SetMultiAxisAccessLowering(access, new MultiAxisAccessLowering(
            MultiAxisAccessKind.SliceCall,
            ImmutableArray.Create(MultiAxisDimensionKind.Slice)));
        _context.SemanticInfo = info;

        var act = () => GenerateExpression(access);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*dimension kind(s)*");
    }

    #endregion

    #region StatementLowering (#1622)

    /// <summary>A bare <c>f()</c> expression statement — the shape the deleted predicate keyed on.</summary>
    private static ExpressionStatement CallStatement() => new()
    {
        Expression = new FunctionCall
        {
            Function = new Identifier { Name = "f" },
            Arguments = ImmutableArray<Expression>.Empty,
        },
    };

    [Fact]
    public void ExpressionStatement_PlainStatementFact_EmitsBareInvocation()
    {
        var stmt = CallStatement();
        var info = new SemanticInfo();
        info.SetStatementLowering(stmt, new StatementLowering(StatementLoweringKind.PlainStatement));
        _context.SemanticInfo = info;

        var result = GenerateStatement(stmt);

        var exprStmt = result.Should().BeOfType<ExpressionStatementSyntax>().Subject;
        exprStmt.Expression.Should().BeOfType<InvocationExpressionSyntax>();
    }

    [Fact]
    public void ExpressionStatement_DiscardFact_OnSameAst_EmitsDiscardAssignment()
    {
        // Same f() AST; only the fact differs. Under the deleted `expr is FunctionCall` predicate
        // this would emit a bare invocation.
        var stmt = CallStatement();
        var info = new SemanticInfo();
        info.SetStatementLowering(stmt, new StatementLowering(StatementLoweringKind.Discard));
        _context.SemanticInfo = info;

        var result = GenerateStatement(stmt);

        var exprStmt = result.Should().BeOfType<ExpressionStatementSyntax>().Subject;
        var assignment = exprStmt.Expression.Should().BeOfType<AssignmentExpressionSyntax>().Subject;
        assignment.Left.ToString().Should().Be("_");
        assignment.Right.Should().BeOfType<InvocationExpressionSyntax>();
    }

    [Theory]
    [InlineData(StatementLoweringKind.ElideNoneLiteral)]
    [InlineData(StatementLoweringKind.ElideMethodGroupStatement)]
    public void ExpressionStatement_ElideFact_OnSameAst_EmitsEmptyStatement(StatementLoweringKind kind)
    {
        var stmt = CallStatement();
        var info = new SemanticInfo();
        info.SetStatementLowering(stmt, new StatementLowering(kind));
        _context.SemanticInfo = info;

        var result = GenerateStatement(stmt);

        result.Should().BeOfType<EmptyStatementSyntax>();
    }

    [Fact]
    public void ExpressionStatement_AbsentFact_Throws()
    {
        var stmt = CallStatement();
        _context.SemanticInfo = new SemanticInfo();

        var act = () => GenerateStatement(stmt);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*No StatementLowering recorded*");
    }

    #endregion
}
