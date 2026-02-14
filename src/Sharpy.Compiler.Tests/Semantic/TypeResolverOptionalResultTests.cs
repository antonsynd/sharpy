using System.Collections.Immutable;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeResolverOptionalResultTests
{
    private (TypeResolver, SymbolTable, SemanticInfo) CreateResolver()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var resolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        return (resolver, symbolTable, semanticInfo);
    }

    #region Optional (T?) Resolution

    [Fact]
    public void Resolve_OptionalInt_ReturnsOptionalType()
    {
        var (resolver, _, _) = CreateResolver();

        var annotation = new TypeAnnotation { Name = "int", IsOptional = true };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<OptionalType>();
        var opt = (OptionalType)type;
        opt.UnderlyingType.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void Resolve_OptionalString_ReturnsOptionalType()
    {
        var (resolver, _, _) = CreateResolver();

        var annotation = new TypeAnnotation { Name = "str", IsOptional = true };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<OptionalType>();
        var opt = (OptionalType)type;
        opt.UnderlyingType.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void Resolve_OptionalGeneric_ReturnsOptionalType()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        var listTypeSymbol = new TypeSymbol
        {
            Name = "list",
            Kind = SymbolKind.Type,
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" } }
        };
        symbolTable.Define(listTypeSymbol);

        var annotation = new TypeAnnotation
        {
            Name = "list",
            IsOptional = true,
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray()
        };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<OptionalType>();
        var opt = (OptionalType)type;
        opt.UnderlyingType.Should().BeOfType<GenericType>();
    }

    [Fact]
    public void Resolve_ExplicitOptional_ReturnsOptionalType()
    {
        var (resolver, _, _) = CreateResolver();

        var annotation = new TypeAnnotation
        {
            Name = "Optional",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray()
        };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<OptionalType>();
        var opt = (OptionalType)type;
        opt.UnderlyingType.Should().Be(SemanticType.Int);
    }

    #endregion

    #region C# Nullable (T | None) Resolution

    [Fact]
    public void Resolve_CSharpNullable_ReturnsNullableType()
    {
        var (resolver, _, _) = CreateResolver();

        var annotation = new TypeAnnotation { Name = "str", IsCSharpNullable = true };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<NullableType>();
        var nullable = (NullableType)type;
        nullable.UnderlyingType.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void Resolve_CSharpNullableGeneric_ReturnsNullableType()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        var listTypeSymbol = new TypeSymbol
        {
            Name = "list",
            Kind = SymbolKind.Type,
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" } }
        };
        symbolTable.Define(listTypeSymbol);

        var annotation = new TypeAnnotation
        {
            Name = "list",
            IsCSharpNullable = true,
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray()
        };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<NullableType>();
    }

    #endregion

    #region Result (T !E) Resolution

    [Fact]
    public void Resolve_ResultType_ReturnsResultType()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        // Register ValueError as a user-defined type
        var valueErrorSymbol = new TypeSymbol
        {
            Name = "ValueError",
            Kind = SymbolKind.Type
        };
        symbolTable.Define(valueErrorSymbol);

        var annotation = new TypeAnnotation
        {
            Name = "int",
            ErrorType = new TypeAnnotation { Name = "ValueError" }
        };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<ResultType>();
        var result = (ResultType)type;
        result.OkType.Should().Be(SemanticType.Int);
        result.ErrorType.GetDisplayName().Should().Be("ValueError");
    }

    [Fact]
    public void Resolve_ResultWithGenericOk_ReturnsResultType()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        var listTypeSymbol = new TypeSymbol
        {
            Name = "list",
            Kind = SymbolKind.Type,
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" } }
        };
        symbolTable.Define(listTypeSymbol);

        var parseErrorSymbol = new TypeSymbol
        {
            Name = "ParseError",
            Kind = SymbolKind.Type
        };
        symbolTable.Define(parseErrorSymbol);

        var annotation = new TypeAnnotation
        {
            Name = "list",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray(),
            ErrorType = new TypeAnnotation { Name = "ParseError" }
        };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<ResultType>();
        var result = (ResultType)type;
        result.OkType.Should().BeOfType<GenericType>();
    }

    [Fact]
    public void Resolve_ExplicitResult_ReturnsResultType()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        var valueErrorSymbol = new TypeSymbol
        {
            Name = "ValueError",
            Kind = SymbolKind.Type
        };
        symbolTable.Define(valueErrorSymbol);

        var annotation = new TypeAnnotation
        {
            Name = "Result",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" },
                new TypeAnnotation { Name = "ValueError" }
            }.ToImmutableArray()
        };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<ResultType>();
        var result = (ResultType)type;
        result.OkType.Should().Be(SemanticType.Int);
        result.ErrorType.GetDisplayName().Should().Be("ValueError");
    }

    #endregion

    #region Combined Modifiers

    [Fact]
    public void Resolve_ResultWithCSharpNullable_ReturnsNullableResult()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        var valueErrorSymbol = new TypeSymbol
        {
            Name = "ValueError",
            Kind = SymbolKind.Type
        };
        symbolTable.Define(valueErrorSymbol);

        // int !ValueError | None
        var annotation = new TypeAnnotation
        {
            Name = "int",
            ErrorType = new TypeAnnotation { Name = "ValueError" },
            IsCSharpNullable = true
        };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<NullableType>();
        var nullable = (NullableType)type;
        nullable.UnderlyingType.Should().BeOfType<ResultType>();
    }

    [Fact]
    public void Resolve_OptionalVsNullable_AreDifferentTypes()
    {
        var (resolver, _, _) = CreateResolver();

        var optionalAnnotation = new TypeAnnotation { Name = "int", IsOptional = true };
        var nullableAnnotation = new TypeAnnotation { Name = "int", IsCSharpNullable = true };

        var optional = resolver.ResolveTypeAnnotation(optionalAnnotation);
        var nullable = resolver.ResolveTypeAnnotation(nullableAnnotation);

        optional.Should().BeOfType<OptionalType>();
        nullable.Should().BeOfType<NullableType>();
        optional.Should().NotBe(nullable);
    }

    #endregion

    #region Display Names

    [Fact]
    public void Resolve_OptionalType_DisplayNameHasQuestion()
    {
        var (resolver, _, _) = CreateResolver();

        var annotation = new TypeAnnotation { Name = "int", IsOptional = true };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.GetDisplayName().Should().Be("int?");
    }

    [Fact]
    public void Resolve_ResultType_DisplayNameHasBang()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        var valueErrorSymbol = new TypeSymbol
        {
            Name = "ValueError",
            Kind = SymbolKind.Type
        };
        symbolTable.Define(valueErrorSymbol);

        var annotation = new TypeAnnotation
        {
            Name = "int",
            ErrorType = new TypeAnnotation { Name = "ValueError" }
        };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.GetDisplayName().Should().Be("int !ValueError");
    }

    [Fact]
    public void Resolve_NullableType_DisplayNameHasQuestion()
    {
        var (resolver, _, _) = CreateResolver();

        var annotation = new TypeAnnotation { Name = "int", IsCSharpNullable = true };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.GetDisplayName().Should().Contain("?");
    }

    #endregion

    #region Type Alias with Optional

    [Fact]
    public void Resolve_OptionalTypeAlias_ReturnsOptionalType()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        var aliasSymbol = new TypeAliasSymbol
        {
            Name = "UserId",
            Kind = SymbolKind.TypeAlias,
            TypeAnnotation = new TypeAnnotation { Name = "int" }
        };
        symbolTable.Define(aliasSymbol);

        var annotation = new TypeAnnotation { Name = "UserId", IsOptional = true };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().BeOfType<OptionalType>();
        var opt = (OptionalType)type;
        opt.UnderlyingType.Should().Be(SemanticType.Int);
    }

    #endregion
}
