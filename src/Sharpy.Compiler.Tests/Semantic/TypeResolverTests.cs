using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

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
            IsNullable = true
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
}
