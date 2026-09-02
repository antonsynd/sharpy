using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Text;
using Xunit;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Tests for the consolidated <see cref="CompilerInvariants"/> class.
/// </summary>
public class CompilerInvariantsTests
{
    private static DiagnosticBag CreateDiagnostics() => new();
    private static BuiltinRegistry CreateBuiltinRegistry() => new(NullLogger.Instance);
    private static SymbolTable CreateSymbolTable() => new(CreateBuiltinRegistry());
    private static SemanticInfo CreateSemanticInfo() => new();

    #region InvariantSet Flag Tests

    [Fact]
    public void Assert_WithNoneFlag_DoesNothing()
    {
        var diagnostics = CreateDiagnostics();

        CompilerInvariants.Assert(
            diagnostics,
            CompilerInvariants.InvariantSet.None,
            module: CreateModuleWithMissingSpan(),
            symbolTable: CreateSymbolTableWithEmptyName());

        // No invariants checked, no diagnostics
        Assert.Empty(diagnostics.GetAll());
    }

    [Fact]
    public void Assert_WithSpansFlag_OnlyChecksSpans()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTableWithEmptyName();

        CompilerInvariants.Assert(
            diagnostics,
            CompilerInvariants.InvariantSet.Spans,
            module: CreateModuleWithValidSpan(),
            symbolTable: symbolTable);

        // Spans check passes, SymbolNames not checked (even though symbol table has issue)
        Assert.Empty(diagnostics.GetWarnings());
    }

    [Fact]
    public void Assert_WithSymbolNamesFlag_OnlyChecksSymbolNames()
    {
        var diagnostics = CreateDiagnostics();
        var module = CreateModuleWithMissingSpan();

        CompilerInvariants.Assert(
            diagnostics,
            CompilerInvariants.InvariantSet.SymbolNames,
            module: module,
            symbolTable: CreateSymbolTable());

        // SymbolNames check passes, Spans not checked (even though module has missing span)
        Assert.Empty(diagnostics.GetWarnings());
    }

    [Fact]
    public void Assert_WithAllFlag_ChecksAllInvariants()
    {
        var diagnostics = CreateDiagnostics();
        var module = CreateModuleWithMissingSpan();
        var symbolTable = CreateSymbolTableWithEmptyName();
        var semanticInfo = CreateSemanticInfoWithUnknownType();

        CompilerInvariants.Assert(
            diagnostics,
            CompilerInvariants.InvariantSet.All,
            module: module,
            symbolTable: symbolTable,
            semanticInfo: semanticInfo);

        // All applicable invariants should be checked
        var allDiagnostics = diagnostics.GetAll().ToList();
        Assert.True(allDiagnostics.Count >= 3, $"Expected at least 3 diagnostics, got {allDiagnostics.Count}");

        // Verify span invariant was checked (warning)
        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Contains(warnings, w => w.Message.Contains("missing TextSpan"));

        // Verify symbol name invariant was checked (warning)
        Assert.Contains(warnings, w => w.Message.Contains("null/empty name"));

        // Verify unknown type invariant was checked (error)
        var errors = diagnostics.GetErrors().ToList();
        Assert.Contains(errors, e => e.Message.Contains("type inference produced UnknownType"));
    }

    [Fact]
    public void Assert_WithPostParseFlag_EqualsSpansFlag()
    {
        var diagnostics = CreateDiagnostics();
        var module = CreateModuleWithMissingSpan();

        CompilerInvariants.Assert(
            diagnostics,
            CompilerInvariants.InvariantSet.PostParse,
            module: module);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Contains("missing TextSpan", warnings[0].Message);
    }

    [Fact]
    public void Assert_WithPostNameResolutionFlag_ChecksSymbolNamesAndTypeUniqueness()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTableWithEmptyName();

        CompilerInvariants.Assert(
            diagnostics,
            CompilerInvariants.InvariantSet.PostNameResolution,
            symbolTable: symbolTable);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Contains("null/empty name", warnings[0].Message);
    }

    [Fact]
    public void Assert_WithPostInheritanceFlag_ChecksInheritance()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTableWithUnresolvedBase();

        CompilerInvariants.Assert(
            diagnostics,
            CompilerInvariants.InvariantSet.PostInheritance,
            symbolTable: symbolTable);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Contains("UnresolvedBaseName", warnings[0].Message);
    }

    [Fact]
    public void Assert_WithPostTypeCheckingFlag_ChecksUnknownTypes()
    {
        var diagnostics = CreateDiagnostics();
        var semanticInfo = CreateSemanticInfoWithUnknownType();

        CompilerInvariants.Assert(
            diagnostics,
            CompilerInvariants.InvariantSet.PostTypeChecking,
            semanticInfo: semanticInfo);

        var errors = diagnostics.GetErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("type inference produced UnknownType", errors[0].Message);
    }

    [Fact]
    public void Assert_WithCombinedFlags_ChecksMultipleInvariants()
    {
        var diagnostics = CreateDiagnostics();
        var module = CreateModuleWithMissingSpan();
        var symbolTable = CreateSymbolTableWithEmptyName();

        var flags = CompilerInvariants.InvariantSet.Spans | CompilerInvariants.InvariantSet.SymbolNames;
        CompilerInvariants.Assert(
            diagnostics,
            flags,
            module: module,
            symbolTable: symbolTable);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Message.Contains("missing TextSpan"));
        Assert.Contains(warnings, w => w.Message.Contains("null/empty name"));
    }

    #endregion

    #region Convenience Method Tests

    [Fact]
    public void AssertPostParse_ChecksSpans()
    {
        var diagnostics = CreateDiagnostics();
        var module = CreateModuleWithMissingSpan();

        CompilerInvariants.AssertPostParse(module, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Contains("missing TextSpan", warnings[0].Message);
    }

    [Fact]
    public void AssertPostNameResolution_ChecksSymbolNamesAndTypeUniqueness()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTableWithEmptyName();

        CompilerInvariants.AssertPostNameResolution(symbolTable, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Contains(warnings, w => w.Message.Contains("null/empty name"));
    }

    [Fact]
    public void AssertPostInheritance_ChecksInheritance()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTableWithUnresolvedBase();

        CompilerInvariants.AssertPostInheritance(symbolTable, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Contains("UnresolvedBaseName", warnings[0].Message);
    }

    [Fact]
    public void AssertPostTypeChecking_ChecksUnknownTypes()
    {
        var diagnostics = CreateDiagnostics();
        var semanticInfo = CreateSemanticInfoWithUnknownType();

        CompilerInvariants.AssertPostTypeChecking(semanticInfo, diagnostics);

        var errors = diagnostics.GetErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("type inference produced UnknownType", errors[0].Message);
    }

    [Fact]
    public void AssertPostCodeGen_ValidCSharp_NoErrors()
    {
        var diagnostics = CreateDiagnostics();
        var validCSharp = "namespace Test { public class Foo { } }";

        CompilerInvariants.AssertPostCodeGen(validCSharp, diagnostics);

        Assert.Empty(diagnostics.GetErrors());
    }

    [Fact]
    public void AssertPostCodeGen_InvalidCSharp_EmitsError()
    {
        var diagnostics = CreateDiagnostics();
        var invalidCSharp = "namespace Test { public class { } }"; // Missing class name

        CompilerInvariants.AssertPostCodeGen(invalidCSharp, diagnostics);

        var errors = diagnostics.GetErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("generated C# contains", errors[0].Message);
        Assert.Contains("syntax error", errors[0].Message);
    }

    [Fact]
    public void AssertPostCodeGen_ValidTree_NoErrors()
    {
        // D3 (#1050): the hot-path overload reads GetDiagnostics off the emitter's tree
        // instead of reparsing. A well-formed tree must not report a spurious error.
        var diagnostics = CreateDiagnostics();
        var tree = CSharpSyntaxTree.ParseText("namespace Test { public class Foo { } }");

        CompilerInvariants.AssertPostCodeGen(tree, diagnostics);

        Assert.Empty(diagnostics.GetErrors());
    }

    [Fact]
    public void AssertPostCodeGen_InvalidTree_EmitsError()
    {
        // A structurally broken tree still surfaces the internal-error diagnostic via
        // GetDiagnostics, without a reparse.
        var diagnostics = CreateDiagnostics();
        var tree = CSharpSyntaxTree.ParseText("namespace Test { public class { } }"); // Missing class name

        CompilerInvariants.AssertPostCodeGen(tree, diagnostics);

        var errors = diagnostics.GetErrors().ToList();
        Assert.Single(errors);
        Assert.Contains("generated C# contains", errors[0].Message);
        Assert.Contains("syntax error", errors[0].Message);
    }

    // ----- EmittedTreePrecedence (#1727, #1712) -----

    [Fact]
    public void AssertEmittedTreePrecedence_CleanUnit_NoErrors()
    {
        // (flag ? a : b).Length > 0 — the #1727 shape built correctly: the conditional receiver is
        // parenthesized, so the tree and its printed text mean the same program.
        var diagnostics = CreateDiagnostics();
        var unit = UnitWithExpression(
            BinaryExpression(SyntaxKind.GreaterThanExpression,
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    ParenthesizedExpression(ConditionalExpression(
                        IdentifierName("flag"), IdentifierName("a"), IdentifierName("b"))),
                    IdentifierName("Length")),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))));

        CompilerInvariants.AssertEmittedTreePrecedence(unit, diagnostics);

        Assert.Empty(diagnostics.GetErrors());
    }

    [Fact]
    public void AssertEmittedTreePrecedence_InvertedUnit_EmitsSPY0524()
    {
        // The same tree with the receiver left bare: the tree means (flag ? a : b).Length > 0, its
        // printed text `flag ? a : b.Length > 0` means flag ? a : (b.Length > 0) — CS0173 behind
        // SPY0908 at fe652987f. The net names the class instead.
        var diagnostics = CreateDiagnostics();
        var unit = UnitWithExpression(
            BinaryExpression(SyntaxKind.GreaterThanExpression,
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    ConditionalExpression(IdentifierName("flag"), IdentifierName("a"), IdentifierName("b")),
                    IdentifierName("Length")),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))));

        CompilerInvariants.AssertEmittedTreePrecedence(unit, diagnostics);

        var error = Assert.Single(diagnostics.GetErrors());
        Assert.Equal(DiagnosticCodes.CodeGen.EmittedTreePrecedenceInversion, error.Code);
        Assert.Contains("ConditionalExpression", error.Message);
        Assert.Contains("SimpleMemberAccessExpression", error.Message);
        Assert.Contains("Receiver", error.Message);
    }

    [Fact]
    public void Assert_WithEmittedTreePrecedenceFlag_ChecksEmitterUnit()
    {
        // cond ? a : b.IsSome — the #1712 shape. The flag routes the unit to the check; without the
        // flag the same unit is not examined.
        var inverted = UnitWithExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                ConditionalExpression(IdentifierName("flag"), IdentifierName("a"), IdentifierName("b")),
                IdentifierName("IsSome")));

        var withFlag = CreateDiagnostics();
        CompilerInvariants.Assert(withFlag, CompilerInvariants.InvariantSet.EmittedTreePrecedence, emitterUnit: inverted);
        Assert.Single(withFlag.GetErrors());

        var withoutFlag = CreateDiagnostics();
        CompilerInvariants.Assert(withoutFlag, CompilerInvariants.InvariantSet.GeneratedCSharp, emitterUnit: inverted);
        Assert.Empty(withoutFlag.GetErrors());
    }

    [Fact]
    public void PostCodeGenAndAllFlags_IncludeEmittedTreePrecedence()
    {
        Assert.True(CompilerInvariants.InvariantSet.PostCodeGen.HasFlag(CompilerInvariants.InvariantSet.EmittedTreePrecedence));
        Assert.True(CompilerInvariants.InvariantSet.All.HasFlag(CompilerInvariants.InvariantSet.EmittedTreePrecedence));
    }

    private static CompilationUnitSyntax UnitWithExpression(ExpressionSyntax expression)
    {
        var method = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier("M"))
            .WithBody(Block(ExpressionStatement(expression)));
        var cls = ClassDeclaration("C").WithMembers(SingletonList<MemberDeclarationSyntax>(method));
        return CompilationUnit().WithMembers(SingletonList<MemberDeclarationSyntax>(cls));
    }

    #endregion

    #region Diagnostic Code Tests

    [Fact]
    public void AssertStatementsHaveSpans_EmitsSPY0904()
    {
        var diagnostics = CreateDiagnostics();
        var module = CreateModuleWithMissingSpan();

        CompilerInvariants.AssertStatementsHaveSpans(module, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.Infrastructure.InvariantViolation, warnings[0].Code);
    }

    [Fact]
    public void AssertAllSymbolsHaveNames_EmitsSPY0904()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTableWithEmptyName();

        CompilerInvariants.AssertAllSymbolsHaveNames(symbolTable, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.Infrastructure.InvariantViolation, warnings[0].Code);
    }

    [Fact]
    public void AssertNoDuplicateTypeNames_EmitsSPY0904()
    {
        // Note: SymbolTable's Scope uses Dictionary which prevents true duplicates by name.
        // This assertion is defense-in-depth. We test by manipulating the underlying data
        // or accepting this assertion can't currently trigger a warning through normal flow.
        // For now, verify it doesn't throw on valid data.
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTable();

        CompilerInvariants.AssertNoDuplicateTypeNames(symbolTable, diagnostics);

        // No duplicates = no warnings
        Assert.Empty(diagnostics.GetWarnings());
    }

    [Fact]
    public void AssertNoUnresolvedInheritance_EmitsSPY0904()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTableWithUnresolvedBase();

        CompilerInvariants.AssertNoUnresolvedInheritance(symbolTable, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.Infrastructure.InvariantViolation, warnings[0].Code);
    }

    [Fact]
    public void WarnIfUnknownTypes_EmitsSPY0907()
    {
        var diagnostics = CreateDiagnostics();
        var semanticInfo = CreateSemanticInfoWithUnknownType();

        CompilerInvariants.WarnIfUnknownTypes(semanticInfo, diagnostics);

        var errors = diagnostics.GetErrors().ToList();
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.Infrastructure.UnexpectedUnknownType, errors[0].Code);
    }

    [Fact]
    public void AssertGeneratedCSharpParses_EmitsCodeGenError()
    {
        var diagnostics = CreateDiagnostics();
        var invalidCSharp = "invalid {{ code";

        CompilerInvariants.AssertGeneratedCSharpParses(invalidCSharp, diagnostics);

        var errors = diagnostics.GetErrors().ToList();
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError, errors[0].Code);
    }

    #endregion

    #region No Violation Tests

    [Fact]
    public void AssertStatementsHaveSpans_WithValidSpans_NoViolation()
    {
        var diagnostics = CreateDiagnostics();
        var module = CreateModuleWithValidSpan();

        CompilerInvariants.AssertStatementsHaveSpans(module, diagnostics);

        Assert.Empty(diagnostics.GetWarnings());
    }

    [Fact]
    public void AssertStatementsHaveSpans_ImportWithoutSpan_NoViolation()
    {
        var diagnostics = CreateDiagnostics();
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ImportStatement { LineStart = 1, ColumnStart = 1, Span = null }
            )
        };

        CompilerInvariants.AssertStatementsHaveSpans(module, diagnostics);

        // Import statements are exempt from span checks
        Assert.Empty(diagnostics.GetWarnings());
    }

    [Fact]
    public void AssertAllSymbolsHaveNames_WithValidNames_NoViolation()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTable();
        symbolTable.Define(new FunctionSymbol { Name = "main", Kind = SymbolKind.Function });

        CompilerInvariants.AssertAllSymbolsHaveNames(symbolTable, diagnostics);

        Assert.Empty(diagnostics.GetWarnings());
    }

    [Fact]
    public void AssertNoUnresolvedInheritance_WithResolvedBase_NoViolation()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTable();

        var baseType = new TypeSymbol
        {
            Name = "Parent",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };
        symbolTable.Define(baseType);

        var derivedType = new TypeSymbol
        {
            Name = "Child",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedBaseName = "Parent",
            BaseType = baseType
        };
        symbolTable.Define(derivedType);

        CompilerInvariants.AssertNoUnresolvedInheritance(symbolTable, diagnostics);

        Assert.Empty(diagnostics.GetWarnings());
    }

    [Fact]
    public void WarnIfUnknownTypes_WithErrors_NoViolation()
    {
        var diagnostics = CreateDiagnostics();
        var semanticInfo = CreateSemanticInfoWithUnknownType();

        // Add an error to the diagnostic bag
        diagnostics.AddError("Some type error", code: "SPY0220");

        CompilerInvariants.WarnIfUnknownTypes(semanticInfo, diagnostics);

        // With errors present, the unknown type is expected (error recovery) - no additional warning
        var invariantWarnings = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Empty(invariantWarnings);
    }

    [Fact]
    public void WarnIfUnknownTypes_NoUnknownTypes_NoViolation()
    {
        var diagnostics = CreateDiagnostics();
        var semanticInfo = CreateSemanticInfo();

        var expr = new IntegerLiteral { Value = "42", LineStart = 1, ColumnStart = 1 };
        semanticInfo.SetExpressionType(expr, SemanticType.Int);

        CompilerInvariants.WarnIfUnknownTypes(semanticInfo, diagnostics);

        Assert.Empty(diagnostics.GetErrors());
    }

    [Fact]
    public void WarnIfUnknownTypes_ErrorRecoveryMarkedUnknown_NoViolation()
    {
        var diagnostics = CreateDiagnostics();
        var semanticInfo = CreateSemanticInfo();

        // Create an expression with UnknownType but marked as error recovery
        var expr = new IntegerLiteral { Value = "42", LineStart = 1, ColumnStart = 1 };
        semanticInfo.SetExpressionType(expr, SemanticType.Unknown);
        semanticInfo.MarkErrorRecovery(expr);

        CompilerInvariants.WarnIfUnknownTypes(semanticInfo, diagnostics);

        // Error-recovery-marked Unknown types should not be flagged
        Assert.Empty(diagnostics.GetErrors());
    }

    #endregion

    #region Helper Methods

    private static Module CreateModuleWithMissingSpan()
    {
        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ReturnStatement { LineStart = 1, ColumnStart = 1, Span = null }
            )
        };
    }

    private static Module CreateModuleWithValidSpan()
    {
        return new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ReturnStatement
                {
                    LineStart = 1,
                    ColumnStart = 1,
                    Span = new TextSpan(0, 6)
                }
            )
        };
    }

    private static SymbolTable CreateSymbolTableWithEmptyName()
    {
        var symbolTable = CreateSymbolTable();
        symbolTable.Define(new FunctionSymbol { Name = "", Kind = SymbolKind.Function });
        return symbolTable;
    }

    private static SymbolTable CreateSymbolTableWithUnresolvedBase()
    {
        var symbolTable = CreateSymbolTable();
        symbolTable.Define(new TypeSymbol
        {
            Name = "Child",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedBaseName = "MissingParent"
            // BaseType is null - unresolved
        });
        return symbolTable;
    }

    private static SemanticInfo CreateSemanticInfoWithUnknownType()
    {
        var semanticInfo = CreateSemanticInfo();
        var expr = new IntegerLiteral { Value = "42", LineStart = 1, ColumnStart = 1 };
        semanticInfo.SetExpressionType(expr, SemanticType.Unknown);
        return semanticInfo;
    }

    #endregion
}
