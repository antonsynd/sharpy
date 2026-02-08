using System.Collections.Immutable;
using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Negative tests for phase boundary assertions.
/// These verify that each assertion method DOES emit SPY0904
/// when the corresponding invariant is violated.
/// </summary>
public class PhaseBoundaryAssertionNegativeTests
{
    [Fact]
    public void AssertStatementsHaveSpans_MissingSpan_EmitsInvariantViolation()
    {
        // Construct a module with a statement that has no Span
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ReturnStatement { LineStart = 1, ColumnStart = 1, Span = null }
            )
        };
        var diagnostics = new DiagnosticBag();

        Compiler.AssertStatementsHaveSpans(module, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Single(violations);
        Assert.Contains("missing TextSpan", violations[0].Message);
        Assert.Contains("compiler bug", violations[0].Message);
    }

    [Fact]
    public void AssertStatementsHaveSpans_WithSpan_NoViolation()
    {
        var module = new Module
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
        var diagnostics = new DiagnosticBag();

        Compiler.AssertStatementsHaveSpans(module, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Empty(violations);
    }

    [Fact]
    public void AssertStatementsHaveSpans_ImportWithoutSpan_NoViolation()
    {
        // Import statements are exempt from span checks
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ImportStatement { LineStart = 1, ColumnStart = 1, Span = null }
            )
        };
        var diagnostics = new DiagnosticBag();

        Compiler.AssertStatementsHaveSpans(module, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Empty(violations);
    }

    [Fact]
    public void AssertAllSymbolsHaveNames_EmptyName_EmitsInvariantViolation()
    {
        var builtinRegistry = new BuiltinRegistry(NullLogger.Instance);
        var symbolTable = new SymbolTable(builtinRegistry);

        // Define a symbol with an empty name
        symbolTable.Define(new FunctionSymbol { Name = "", Kind = SymbolKind.Function });

        var diagnostics = new DiagnosticBag();

        Compiler.AssertAllSymbolsHaveNames(symbolTable, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Single(violations);
        Assert.Contains("null/empty name", violations[0].Message);
    }

    [Fact]
    public void AssertAllSymbolsHaveNames_ValidNames_NoViolation()
    {
        var builtinRegistry = new BuiltinRegistry(NullLogger.Instance);
        var symbolTable = new SymbolTable(builtinRegistry);

        symbolTable.Define(new FunctionSymbol { Name = "main", Kind = SymbolKind.Function });

        var diagnostics = new DiagnosticBag();

        Compiler.AssertAllSymbolsHaveNames(symbolTable, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Empty(violations);
    }

    [Fact]
    public void AssertNoDuplicateTypeNames_UniqueTypes_NoViolation()
    {
        // Scope uses Dictionary<string, Symbol> which enforces uniqueness by name,
        // so AssertNoDuplicateTypeNames is defense-in-depth. Verify it passes for
        // distinct user-defined types.
        var builtinRegistry = new BuiltinRegistry(NullLogger.Instance);
        var symbolTable = new SymbolTable(builtinRegistry);

        symbolTable.Define(new TypeSymbol
        {
            Name = "ClassA",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            DeclarationLine = 1
        });
        symbolTable.Define(new TypeSymbol
        {
            Name = "ClassB",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            DeclarationLine = 5
        });

        var diagnostics = new DiagnosticBag();
        Compiler.AssertNoDuplicateTypeNames(symbolTable, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Empty(violations);
    }

    [Fact]
    public void AssertNoDuplicateTypeNames_ClrTypesWithSameName_NoViolation()
    {
        // CLR types with the same name are allowed (re-exported from different modules)
        var builtinRegistry = new BuiltinRegistry(NullLogger.Instance);
        var symbolTable = new SymbolTable(builtinRegistry);

        symbolTable.Define(new TypeSymbol
        {
            Name = "Exception",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            ClrType = typeof(System.Exception)
        });
        symbolTable.Define(new TypeSymbol
        {
            Name = "Exception",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            ClrType = typeof(System.Exception)
        });

        var diagnostics = new DiagnosticBag();

        Compiler.AssertNoDuplicateTypeNames(symbolTable, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Empty(violations);
    }

    [Fact]
    public void AssertNoUnresolvedInheritance_UnresolvedBase_EmitsInvariantViolation()
    {
        var builtinRegistry = new BuiltinRegistry(NullLogger.Instance);
        var symbolTable = new SymbolTable(builtinRegistry);

        // Define a type with UnresolvedBaseName but no BaseType set
        symbolTable.Define(new TypeSymbol
        {
            Name = "Child",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedBaseName = "MissingParent"
        });

        var diagnostics = new DiagnosticBag();

        Compiler.AssertNoUnresolvedInheritance(symbolTable, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Single(violations);
        Assert.Contains("UnresolvedBaseName", violations[0].Message);
        Assert.Contains("MissingParent", violations[0].Message);
    }

    [Fact]
    public void AssertNoUnresolvedInheritance_UnresolvedInterfaces_EmitsInvariantViolation()
    {
        var builtinRegistry = new BuiltinRegistry(NullLogger.Instance);
        var symbolTable = new SymbolTable(builtinRegistry);

        // Define a type with unresolved interface names but no interfaces resolved
        symbolTable.Define(new TypeSymbol
        {
            Name = "MyClass",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            UnresolvedInterfaceNames = new System.Collections.Generic.List<string> { "IFoo", "IBar" }
        });

        var diagnostics = new DiagnosticBag();

        Compiler.AssertNoUnresolvedInheritance(symbolTable, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Single(violations);
        Assert.Contains("unresolved interface names", violations[0].Message);
    }

    [Fact]
    public void WarnIfUnknownTypes_UnknownWithNoErrors_EmitsInvariantViolation()
    {
        var semanticInfo = new SemanticInfo();
        var diagnostics = new DiagnosticBag();

        // Register an expression with UnknownType but no errors in the diagnostic bag
        // and NOT marked as error recovery — this simulates a type inference bug
        var expr = new IntegerLiteral { Value = "42", LineStart = 1, ColumnStart = 1 };
        semanticInfo.SetExpressionType(expr, SemanticType.Unknown);

        Compiler.WarnIfUnknownTypes(semanticInfo, diagnostics);

        var violations = diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Infrastructure.UnexpectedUnknownType)
            .ToList();
        Assert.Single(violations);
        Assert.Contains("type inference produced UnknownType", violations[0].Message);
    }

    [Fact]
    public void WarnIfUnknownTypes_UnknownWithErrors_NoViolation()
    {
        var semanticInfo = new SemanticInfo();
        var diagnostics = new DiagnosticBag();

        // Register an expression with UnknownType AND an error in the diagnostic bag
        var expr = new IntegerLiteral { Value = "42", LineStart = 1, ColumnStart = 1 };
        semanticInfo.SetExpressionType(expr, SemanticType.Unknown);
        diagnostics.AddError("Some type error", code: "SPY0220");

        Compiler.WarnIfUnknownTypes(semanticInfo, diagnostics);

        // With errors present, the unknown type is expected (error recovery) — no violation
        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Empty(violations);
    }

    [Fact]
    public void WarnIfUnknownTypes_NoUnknownTypes_NoViolation()
    {
        var semanticInfo = new SemanticInfo();
        var diagnostics = new DiagnosticBag();

        // Only known types — no violations
        var expr = new IntegerLiteral { Value = "42", LineStart = 1, ColumnStart = 1 };
        semanticInfo.SetExpressionType(expr, SemanticType.Int);

        Compiler.WarnIfUnknownTypes(semanticInfo, diagnostics);

        var violations = diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Infrastructure.InvariantViolation)
            .ToList();
        Assert.Empty(violations);
    }
}
