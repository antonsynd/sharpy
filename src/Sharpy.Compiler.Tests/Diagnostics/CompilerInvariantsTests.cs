using System.Collections.Immutable;
using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;
using Xunit;

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
        var warnings = diagnostics.GetWarnings().ToList();
        Assert.True(warnings.Count >= 2, $"Expected at least 2 warnings, got {warnings.Count}");

        // Verify span invariant was checked
        Assert.Contains(warnings, w => w.Message.Contains("missing TextSpan"));

        // Verify symbol name invariant was checked
        Assert.Contains(warnings, w => w.Message.Contains("null/empty name"));
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

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Contains("unknown expression types remain", warnings[0].Message);
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

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Contains("unknown expression types remain", warnings[0].Message);
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

    #endregion

    #region Diagnostic Code Tests

    [Fact]
    public void AssertStatementsHaveSpans_EmitsSHP0904()
    {
        var diagnostics = CreateDiagnostics();
        var module = CreateModuleWithMissingSpan();

        CompilerInvariants.AssertStatementsHaveSpans(module, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.Infrastructure.InvariantViolation, warnings[0].Code);
    }

    [Fact]
    public void AssertAllSymbolsHaveNames_EmitsSHP0904()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTableWithEmptyName();

        CompilerInvariants.AssertAllSymbolsHaveNames(symbolTable, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.Infrastructure.InvariantViolation, warnings[0].Code);
    }

    [Fact]
    public void AssertNoDuplicateTypeNames_EmitsSHP0904()
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
    public void AssertNoUnresolvedInheritance_EmitsSHP0904()
    {
        var diagnostics = CreateDiagnostics();
        var symbolTable = CreateSymbolTableWithUnresolvedBase();

        CompilerInvariants.AssertNoUnresolvedInheritance(symbolTable, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.Infrastructure.InvariantViolation, warnings[0].Code);
    }

    [Fact]
    public void WarnIfUnknownTypes_EmitsSHP0904()
    {
        var diagnostics = CreateDiagnostics();
        var semanticInfo = CreateSemanticInfoWithUnknownType();

        CompilerInvariants.WarnIfUnknownTypes(semanticInfo, diagnostics);

        var warnings = diagnostics.GetWarnings().ToList();
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.Infrastructure.InvariantViolation, warnings[0].Code);
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
        diagnostics.AddError("Some type error", code: "SHP0220");

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

        Assert.Empty(diagnostics.GetWarnings());
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
