using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

public class ServiceAdapterTests
{
    [Fact]
    public void SymbolLookupAdapter_Lookup_FindsDefinedSymbol()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var adapter = new SymbolLookupAdapter(symbolTable);

        var symbol = new VariableSymbol { Name = "testVar", Kind = SymbolKind.Variable };
        symbolTable.Define(symbol);

        // Act
        var result = adapter.Lookup("testVar");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("testVar", result.Name);
    }

    [Fact]
    public void SymbolLookupAdapter_LookupType_FindsTypeSymbol()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var adapter = new SymbolLookupAdapter(symbolTable);

        var typeSymbol = new TypeSymbol
        {
            Name = "MyClass",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };
        symbolTable.Define(typeSymbol);

        // Act
        var result = adapter.LookupType("MyClass");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MyClass", result.Name);
        Assert.Equal(TypeKind.Class, result.TypeKind);
    }

    [Fact]
    public void SymbolLookupAdapter_LookupFunction_FindsFunctionSymbol()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var adapter = new SymbolLookupAdapter(symbolTable);

        var funcSymbol = new FunctionSymbol
        {
            Name = "myFunc",
            Kind = SymbolKind.Function,
            ReturnType = SemanticType.Int
        };
        symbolTable.Define(funcSymbol);

        // Act
        var result = adapter.LookupFunction("myFunc");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("myFunc", result.Name);
    }

    [Fact]
    public void SymbolLookupAdapter_ExistsInCurrentScope_ReturnsTrueForDefinedSymbol()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var adapter = new SymbolLookupAdapter(symbolTable);

        var symbol = new VariableSymbol { Name = "localVar", Kind = SymbolKind.Variable };
        symbolTable.Define(symbol);

        // Act & Assert
        Assert.True(adapter.ExistsInCurrentScope("localVar"));
        Assert.False(adapter.ExistsInCurrentScope("nonexistent"));
    }

    [Fact]
    public void TypeResolverAdapter_ResolveTypeAnnotation_ResolvesBuiltins()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        var adapter = new TypeResolverAdapter(typeResolver);

        var intAnnotation = new TypeAnnotation { Name = "int", IsOptional = false };

        // Act
        var result = adapter.ResolveTypeAnnotation(intAnnotation);

        // Assert
        Assert.Equal(SemanticType.Int, result);
    }

    [Fact]
    public void TypeResolverAdapter_ResolveTypeAnnotation_ResolvesString()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        var adapter = new TypeResolverAdapter(typeResolver);

        var strAnnotation = new TypeAnnotation { Name = "str", IsOptional = false };

        // Act
        var result = adapter.ResolveTypeAnnotation(strAnnotation);

        // Assert
        Assert.Equal(SemanticType.Str, result);
    }

    [Fact]
    public void DiagnosticReporter_ReportError_WithNode()
    {
        // Arrange
        var reporter = new DiagnosticReporter();
        var node = new PassStatement { LineStart = 5, ColumnStart = 10 };

        // Act
        reporter.ReportError("Test error at node", node);

        // Assert
        var errors = reporter.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Equal(5, errors[0].Line);
        Assert.Equal(10, errors[0].Column);
    }

    [Fact]
    public void DiagnosticReporter_ReportWarning_AddsWarning()
    {
        // Arrange
        var reporter = new DiagnosticReporter();

        // Act
        reporter.ReportWarning("Test warning", 1, 1);

        // Assert
        Assert.False(reporter.HasErrors);
        var warnings = reporter.Diagnostics.GetWarnings();
        Assert.Single(warnings);
        Assert.Equal("Test warning", warnings[0].Message);
    }

    [Fact]
    public void TypeResolverAdapter_UnderlyingResolver_ReturnsOriginal()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        var adapter = new TypeResolverAdapter(typeResolver);

        // Act & Assert
        Assert.Same(typeResolver, adapter.UnderlyingResolver);
    }

    [Fact]
    public void SymbolLookupAdapter_UnderlyingTable_ReturnsOriginal()
    {
        // Arrange
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var adapter = new SymbolLookupAdapter(symbolTable);

        // Act & Assert
        Assert.Same(symbolTable, adapter.UnderlyingTable);
    }
}
