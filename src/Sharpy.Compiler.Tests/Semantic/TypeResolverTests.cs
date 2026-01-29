using System.Collections.Immutable;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using AstFunctionType = Sharpy.Compiler.Parser.Ast.FunctionType;
using SemanticFunctionType = Sharpy.Compiler.Semantic.FunctionType;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeResolverTests
{
    private (TypeResolver, SymbolTable, SemanticInfo) CreateResolver()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var resolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        return (resolver, symbolTable, semanticInfo);
    }

    [Fact]
    public void ResolvesBuiltinTypes()
    {
        var (resolver, _, _) = CreateResolver();

        var intAnnotation = new TypeAnnotation
        {
            Name = "int"
        };

        var type = resolver.ResolveTypeAnnotation(intAnnotation);

        type.Should().Be(SemanticType.Int);
        resolver.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ResolvesStringType()
    {
        var (resolver, _, _) = CreateResolver();

        var strAnnotation = new TypeAnnotation
        {
            Name = "str"
        };

        var type = resolver.ResolveTypeAnnotation(strAnnotation);

        type.Should().Be(SemanticType.Str);
        resolver.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ResolvesAutoAsUnknown()
    {
        var (resolver, _, _) = CreateResolver();

        var autoAnnotation = new TypeAnnotation
        {
            Name = "auto"
        };

        var type = resolver.ResolveTypeAnnotation(autoAnnotation);

        type.Should().BeOfType<UnknownType>();
        resolver.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ResolvesNullableTypes()
    {
        var (resolver, _, _) = CreateResolver();

        var nullableIntAnnotation = new TypeAnnotation
        {
            Name = "int",
            IsOptional = true
        };

        var type = resolver.ResolveTypeAnnotation(nullableIntAnnotation);

        type.Should().BeOfType<NullableType>();
        var nullableType = (NullableType)type;
        nullableType.UnderlyingType.Should().Be(SemanticType.Int);
        resolver.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ReportsErrorForUnknownType()
    {
        var (resolver, _, _) = CreateResolver();

        var unknownAnnotation = new TypeAnnotation
        {
            Name = "UnknownType"
        };

        var type = resolver.ResolveTypeAnnotation(unknownAnnotation);

        type.Should().Be(SemanticType.Unknown);
        resolver.Errors.Should().HaveCount(1);
        resolver.Errors[0].Message.Should().Contain("not found");
    }

    [Fact]
    public void CachesResolvedTypes()
    {
        var (resolver, _, semanticInfo) = CreateResolver();

        var annotation = new TypeAnnotation
        {
            Name = "int"
        };

        // First resolution
        var type1 = resolver.ResolveTypeAnnotation(annotation);

        // Second resolution should use cache
        var type2 = resolver.ResolveTypeAnnotation(annotation);

        type1.Should().BeSameAs(type2);
        semanticInfo.GetTypeAnnotation(annotation).Should().BeSameAs(type1);
    }

    [Fact]
    public void ExpandsSimpleTypeAlias()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        // Register type alias: type UserId = int
        var aliasSymbol = new TypeAliasSymbol
        {
            Name = "UserId",
            Kind = SymbolKind.TypeAlias,
            TypeAnnotation = new TypeAnnotation { Name = "int" }
        };
        symbolTable.Define(aliasSymbol);

        // Use the alias
        var annotation = new TypeAnnotation { Name = "UserId" };
        var type = resolver.ResolveTypeAnnotation(annotation);

        // Should expand to int
        type.Should().Be(SemanticType.Int);
        resolver.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ExpandsNullableTypeAlias()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        // Register type alias: type UserId = int
        var aliasSymbol = new TypeAliasSymbol
        {
            Name = "UserId",
            Kind = SymbolKind.TypeAlias,
            TypeAnnotation = new TypeAnnotation { Name = "int" }
        };
        symbolTable.Define(aliasSymbol);

        // Use the alias with nullable modifier: UserId?
        var annotation = new TypeAnnotation { Name = "UserId", IsOptional = true };
        var type = resolver.ResolveTypeAnnotation(annotation);

        // Should expand to int?
        type.Should().BeOfType<NullableType>();
        var nullableType = (NullableType)type;
        nullableType.UnderlyingType.Should().Be(SemanticType.Int);
        resolver.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ExpandsGenericTypeAlias()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        // First register list[T] as a generic type
        var listTypeSymbol = new TypeSymbol
        {
            Name = "list",
            Kind = SymbolKind.Type,
            TypeParameters = new List<TypeParameterDef> { new TypeParameterDef { Name = "T" } }
        };
        symbolTable.Define(listTypeSymbol);

        // Register type alias: type StringList = list[str]
        var aliasSymbol = new TypeAliasSymbol
        {
            Name = "StringList",
            Kind = SymbolKind.TypeAlias,
            TypeAnnotation = new TypeAnnotation
            {
                Name = "list",
                TypeArguments = new List<TypeAnnotation>
                {
                    new TypeAnnotation { Name = "str" }
                }.ToImmutableArray()
            }
        };
        symbolTable.Define(aliasSymbol);

        // Use the alias
        var annotation = new TypeAnnotation { Name = "StringList" };
        var type = resolver.ResolveTypeAnnotation(annotation);

        // Should expand to list[str]
        type.Should().BeOfType<GenericType>();
        var genericType = (GenericType)type;
        genericType.Name.Should().Be("list");
        genericType.TypeArguments.Should().HaveCount(1);
        genericType.TypeArguments[0].Should().Be(SemanticType.Str);
        resolver.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ExpandsFunctionTypeAlias()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        // Register type alias: type Callback = (int, str) -> bool
        var aliasSymbol = new TypeAliasSymbol
        {
            Name = "Callback",
            Kind = SymbolKind.TypeAlias,
            FunctionType = new AstFunctionType
            {
                ParameterTypes = new List<TypeAnnotation>
                {
                    new TypeAnnotation { Name = "int" },
                    new TypeAnnotation { Name = "str" }
                }.ToImmutableArray(),
                ReturnType = new TypeAnnotation { Name = "bool" }
            }
        };
        symbolTable.Define(aliasSymbol);

        // Use the alias
        var annotation = new TypeAnnotation { Name = "Callback" };
        var type = resolver.ResolveTypeAnnotation(annotation);

        // Should expand to (int, str) -> bool
        type.Should().BeOfType<SemanticFunctionType>();
        var funcType = (SemanticFunctionType)type;
        funcType.ParameterTypes.Should().HaveCount(2);
        funcType.ParameterTypes[0].Should().Be(SemanticType.Int);
        funcType.ParameterTypes[1].Should().Be(SemanticType.Str);
        funcType.ReturnType.Should().Be(SemanticType.Bool);
        resolver.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ExpandsNestedTypeAlias()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        // Register type alias: type UserId = int
        var userIdAlias = new TypeAliasSymbol
        {
            Name = "UserId",
            Kind = SymbolKind.TypeAlias,
            TypeAnnotation = new TypeAnnotation { Name = "int" }
        };
        symbolTable.Define(userIdAlias);

        // Register type alias: type MaybeUserId = UserId?
        var maybeUserIdAlias = new TypeAliasSymbol
        {
            Name = "MaybeUserId",
            Kind = SymbolKind.TypeAlias,
            TypeAnnotation = new TypeAnnotation { Name = "UserId", IsOptional = true }
        };
        symbolTable.Define(maybeUserIdAlias);

        // Use the nested alias
        var annotation = new TypeAnnotation { Name = "MaybeUserId" };
        var type = resolver.ResolveTypeAnnotation(annotation);

        // Should expand to int?
        type.Should().BeOfType<NullableType>();
        var nullableType = (NullableType)type;
        nullableType.UnderlyingType.Should().Be(SemanticType.Int);
        resolver.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ReportsErrorForTypeAliasWithNoDefinition()
    {
        var (resolver, symbolTable, _) = CreateResolver();

        // Register invalid type alias with no TypeAnnotation or FunctionType
        var aliasSymbol = new TypeAliasSymbol
        {
            Name = "InvalidAlias",
            Kind = SymbolKind.TypeAlias,
            TypeAnnotation = null,
            FunctionType = null
        };
        symbolTable.Define(aliasSymbol);

        // Try to use the invalid alias
        var annotation = new TypeAnnotation { Name = "InvalidAlias" };
        var type = resolver.ResolveTypeAnnotation(annotation);

        type.Should().Be(SemanticType.Unknown);
        resolver.Errors.Should().HaveCount(1);
        resolver.Errors[0].Message.Should().Contain("has no type definition");
    }
}
