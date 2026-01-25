using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using System.Collections.Immutable;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Unit tests for GenericTypeInferenceService in isolation.
/// </summary>
public class GenericTypeInferenceServiceTests
{
    private readonly BuiltinRegistry _builtinRegistry = new();
    private readonly SymbolTable _symbolTable;
    private readonly GenericTypeInferenceService _service;

    public GenericTypeInferenceServiceTests()
    {
        _symbolTable = new SymbolTable(_builtinRegistry);
        _service = new GenericTypeInferenceService(_symbolTable);
    }

    [Fact]
    public void InferSingleTypeParameter_FromDirectArgument()
    {
        // Arrange: def identity[T](value: T) -> T
        var funcSymbol = new FunctionSymbol
        {
            Name = "identity",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "value",
                    Type = new TypeParameterType { Name = "T" }
                }
            },
            ReturnType = new TypeParameterType { Name = "T" }
        };

        // Act: call identity(42) with argument type int
        var argumentTypes = new List<SemanticType> { SemanticType.Int };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().NotBeNull();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferSingleTypeParameter_FromStringArgument()
    {
        // Arrange: def identity[T](value: T) -> T
        var funcSymbol = new FunctionSymbol
        {
            Name = "identity",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "value",
                    Type = new TypeParameterType { Name = "T" }
                }
            },
            ReturnType = new TypeParameterType { Name = "T" }
        };

        // Act: call identity("hello") with argument type str
        var argumentTypes = new List<SemanticType> { SemanticType.Str };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Str);
    }

    [Fact]
    public void InferMultipleTypeParameters_AllSame()
    {
        // Arrange: def pair[T](a: T, b: T) -> tuple[T, T]
        var funcSymbol = new FunctionSymbol
        {
            Name = "pair",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "a", Type = new TypeParameterType { Name = "T" } },
                new ParameterSymbol { Name = "b", Type = new TypeParameterType { Name = "T" } }
            },
            ReturnType = new Sharpy.Compiler.Semantic.TupleType { ElementTypes = new List<SemanticType>
            {
                new TypeParameterType { Name = "T" },
                new TypeParameterType { Name = "T" }
            }}
        };

        // Act: call pair(1, 2) with both args as int
        var argumentTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Int };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferFails_WhenNoArgumentsForTypeParameter()
    {
        // Arrange: def create_empty[T]() -> list[T]
        var funcSymbol = new FunctionSymbol
        {
            Name = "create_empty",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>(), // No parameters!
            ReturnType = new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "T" } }
            }
        };

        // Act: call create_empty() with no arguments
        var argumentTypes = new List<SemanticType>();
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorKind.Should().Be(InferenceErrorKind.NoArgumentsForTypeParameter);
    }

    [Fact]
    public void InferFails_WhenConflictingTypes()
    {
        // Arrange: def pair[T](a: T, b: T) -> tuple[T, T]
        var funcSymbol = new FunctionSymbol
        {
            Name = "pair",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "a", Type = new TypeParameterType { Name = "T" } },
                new ParameterSymbol { Name = "b", Type = new TypeParameterType { Name = "T" } }
            },
            ReturnType = new Sharpy.Compiler.Semantic.TupleType { ElementTypes = new List<SemanticType>
            {
                new TypeParameterType { Name = "T" },
                new TypeParameterType { Name = "T" }
            }}
        };

        // Act: call pair(1, "hello") with conflicting types
        var argumentTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Str };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorKind.Should().Be(InferenceErrorKind.ConflictingTypes);
    }

    [Fact]
    public void InferFromGenericContainer()
    {
        // Arrange: def first[T](items: list[T]) -> T
        var funcSymbol = new FunctionSymbol
        {
            Name = "first",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "items",
                    Type = new GenericType
                    {
                        Name = "list",
                        TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "T" } }
                    }
                }
            },
            ReturnType = new TypeParameterType { Name = "T" }
        };

        // Act: call first(list[int]) with argument type list[int]
        var argumentTypes = new List<SemanticType>
        {
            new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Int }
            }
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }
}
