using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Parser.Ast;
using System.Collections.Immutable;
using FunctionType = Sharpy.Compiler.Semantic.FunctionType;

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
            ReturnType = new Sharpy.Compiler.Semantic.TupleType
            {
                ElementTypes = new List<SemanticType>
            {
                new TypeParameterType { Name = "T" },
                new TypeParameterType { Name = "T" }
            }
            }
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
            ReturnType = new Sharpy.Compiler.Semantic.TupleType
            {
                ElementTypes = new List<SemanticType>
            {
                new TypeParameterType { Name = "T" },
                new TypeParameterType { Name = "T" }
            }
            }
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

    #region Optional and Result Type Unification

    [Fact]
    public void InferFromOptionalParameter()
    {
        // Arrange: def unwrap_or[T](opt: T?, default: T) -> T
        var funcSymbol = new FunctionSymbol
        {
            Name = "unwrap_or",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "opt",
                    Type = new OptionalType { UnderlyingType = new TypeParameterType { Name = "T" } }
                },
                new ParameterSymbol
                {
                    Name = "default_val",
                    Type = new TypeParameterType { Name = "T" }
                }
            },
            ReturnType = new TypeParameterType { Name = "T" }
        };

        // Act: call unwrap_or(Optional[int], 0)
        var argumentTypes = new List<SemanticType>
        {
            new OptionalType { UnderlyingType = SemanticType.Int },
            SemanticType.Int
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferFromResultParameter()
    {
        // Arrange: def map_ok[T, U, E](result: Result[T, E], f: (T) -> U) -> Result[U, E]
        var funcSymbol = new FunctionSymbol
        {
            Name = "map_ok",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" },
                new TypeParameterDef { Name = "E" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "result",
                    Type = new ResultType
                    {
                        OkType = new TypeParameterType { Name = "T" },
                        ErrorType = new TypeParameterType { Name = "E" }
                    }
                },
                new ParameterSymbol
                {
                    Name = "default_val",
                    Type = new TypeParameterType { Name = "T" }
                }
            },
            ReturnType = new TypeParameterType { Name = "T" }
        };

        // Act: call map_ok(Result[int, str], 0)
        var argumentTypes = new List<SemanticType>
        {
            new ResultType { OkType = SemanticType.Int, ErrorType = SemanticType.Str },
            SemanticType.Int
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(2);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
        result.InferredTypes![1].Should().Be(SemanticType.Str);
    }

    #endregion

    #region FunctionType Unification

    [Fact]
    public void InferFromFunctionType_SingleParameter()
    {
        // Arrange: def apply[T](f: (T) -> bool, value: T) -> bool
        var funcSymbol = new FunctionSymbol
        {
            Name = "apply",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "f",
                    Type = new FunctionType
                    {
                        ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                        ReturnType = SemanticType.Bool
                    }
                },
                new ParameterSymbol
                {
                    Name = "value",
                    Type = new TypeParameterType { Name = "T" }
                }
            },
            ReturnType = SemanticType.Bool
        };

        // Act: call apply((int) -> bool, 42)
        var argumentTypes = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Int },
                ReturnType = SemanticType.Bool
            },
            SemanticType.Int
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert: T should be inferred as int
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferFromFunctionType_ParameterOnly()
    {
        // Arrange: matching (T) -> bool against (int) -> bool should infer T=int
        var funcSymbol = new FunctionSymbol
        {
            Name = "predicate_test",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "pred",
                    Type = new FunctionType
                    {
                        ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                        ReturnType = SemanticType.Bool
                    }
                }
            },
            ReturnType = SemanticType.Bool
        };

        // Act: call predicate_test((int) -> bool)
        var argumentTypes = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Int },
                ReturnType = SemanticType.Bool
            }
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert: T should be inferred as int from the function parameter type
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferFromFunctionType_ReturnType()
    {
        // Arrange: def transform[T, U](f: (T) -> U, value: T) -> U
        var funcSymbol = new FunctionSymbol
        {
            Name = "transform",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" },
                new TypeParameterDef { Name = "U" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "f",
                    Type = new FunctionType
                    {
                        ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                        ReturnType = new TypeParameterType { Name = "U" }
                    }
                },
                new ParameterSymbol
                {
                    Name = "value",
                    Type = new TypeParameterType { Name = "T" }
                }
            },
            ReturnType = new TypeParameterType { Name = "U" }
        };

        // Act: call transform((str) -> int, "hello")
        var argumentTypes = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Str },
                ReturnType = SemanticType.Int
            },
            SemanticType.Str
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert: T=str (from function param + value), U=int (from function return type)
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(2);
        result.InferredTypes![0].Should().Be(SemanticType.Str);
        result.InferredTypes![1].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferFromFunctionType_FilterPattern()
    {
        // Arrange: def filter[T](pred: (T) -> bool, items: list[T]) -> list[T]
        // This mimics the builtin filter() with Func<T, bool>
        var funcSymbol = new FunctionSymbol
        {
            Name = "filter",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "pred",
                    Type = new FunctionType
                    {
                        ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                        ReturnType = SemanticType.Bool
                    }
                },
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
            ReturnType = new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "T" } }
            }
        };

        // Act: call filter((int) -> bool, list[int])
        var argumentTypes = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Int },
                ReturnType = SemanticType.Bool
            },
            new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Int }
            }
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert: T=int inferred consistently from both the lambda and the list
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferFromFunctionType_SortedKeyPattern()
    {
        // Arrange: def sorted[T](items: list[T], key: (T) -> int) -> list[T]
        // This mimics sorted() with a key function (discovery layer emits FunctionType for Func<T, TResult>)
        var funcSymbol = new FunctionSymbol
        {
            Name = "sorted",
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
                },
                new ParameterSymbol
                {
                    Name = "key",
                    Type = new FunctionType
                    {
                        ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                        ReturnType = SemanticType.Int
                    }
                }
            },
            ReturnType = new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "T" } }
            }
        };

        // Act: call sorted(list[str], (str) -> int)
        var argumentTypes = new List<SemanticType>
        {
            new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Str }
            },
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Str },
                ReturnType = SemanticType.Int
            }
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert: T=str inferred from list and confirmed by key function parameter
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Str);
    }

    [Fact]
    public void InferFromFunctionType_MultipleParameters()
    {
        // Arrange: def combine[T, U](f: (T, U) -> str) with a two-parameter function
        var funcSymbol = new FunctionSymbol
        {
            Name = "combine",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" },
                new TypeParameterDef { Name = "U" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "f",
                    Type = new FunctionType
                    {
                        ParameterTypes = new List<SemanticType>
                        {
                            new TypeParameterType { Name = "T" },
                            new TypeParameterType { Name = "U" }
                        },
                        ReturnType = SemanticType.Str
                    }
                }
            },
            ReturnType = SemanticType.Str
        };

        // Act: call combine((int, float) -> str)
        var argumentTypes = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Float },
                ReturnType = SemanticType.Str
            }
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert: T=int, U=float
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(2);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
        result.InferredTypes![1].Should().Be(SemanticType.Float);
    }

    [Fact]
    public void InferFromFunctionType_MismatchedArity_NoError()
    {
        // When function types have different parameter counts, unification should
        // gracefully skip binding (not fail), since type validation is the caller's job
        var funcSymbol = new FunctionSymbol
        {
            Name = "apply_unary",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "f",
                    Type = new FunctionType
                    {
                        ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                        ReturnType = SemanticType.Bool
                    }
                },
                new ParameterSymbol
                {
                    Name = "value",
                    Type = new TypeParameterType { Name = "T" }
                }
            },
            ReturnType = SemanticType.Bool
        };

        // Act: pass a binary function where unary is expected, but also pass int value
        var argumentTypes = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Str },
                ReturnType = SemanticType.Bool
            },
            SemanticType.Int
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert: T=int inferred from the second argument (the mismatched function type is skipped)
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferFromFunctionType_VoidReturn()
    {
        // Arrange: def for_each[T](action: (T) -> None, items: list[T]) -> None
        var funcSymbol = new FunctionSymbol
        {
            Name = "for_each",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "T" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "action",
                    Type = new FunctionType
                    {
                        ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                        ReturnType = SemanticType.Void
                    }
                },
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
            ReturnType = SemanticType.Void
        };

        // Act: call for_each((str) -> None, list[str])
        // This mimics Action<T> discovery (void-returning function type)
        var argumentTypes = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Str },
                ReturnType = SemanticType.Void
            },
            new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Str }
            }
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        // Assert: T=str from both the action parameter and the list
        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Str);
    }

    #endregion

    #region Constructor-like Parameter Patterns

    // These tests verify that InferTypeArguments works for patterns typical of
    // generic class constructors, where type parameters come from the class
    // (e.g., class Box[T] → __init__(self, value: T)).

    [Fact]
    public void ConstructorPattern_SingleTypeParam_SingleArg()
    {
        // Simulates: class Box[T] with __init__(self, value: T), called as Box(42)
        var funcSymbol = new FunctionSymbol
        {
            Name = "__init__",
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
            ReturnType = SemanticType.Void
        };

        var argumentTypes = new List<SemanticType> { SemanticType.Int };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ConstructorPattern_MultiTypeParams()
    {
        // Simulates: class Pair[A, B] with __init__(self, a: A, b: B), called as Pair(42, "hello")
        var funcSymbol = new FunctionSymbol
        {
            Name = "__init__",
            Kind = SymbolKind.Function,
            TypeParameters = new List<TypeParameterDef>
            {
                new TypeParameterDef { Name = "A" },
                new TypeParameterDef { Name = "B" }
            },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "a", Type = new TypeParameterType { Name = "A" } },
                new ParameterSymbol { Name = "b", Type = new TypeParameterType { Name = "B" } }
            },
            ReturnType = SemanticType.Void
        };

        var argumentTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Str };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(2);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
        result.InferredTypes![1].Should().Be(SemanticType.Str);
    }

    [Fact]
    public void ConstructorPattern_NestedGeneric()
    {
        // Simulates: class Wrapper[T] with __init__(self, items: list[T]), called as Wrapper([1, 2, 3])
        var funcSymbol = new FunctionSymbol
        {
            Name = "__init__",
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
            ReturnType = SemanticType.Void
        };

        var argumentTypes = new List<SemanticType>
        {
            new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Int }
            }
        };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        result.Success.Should().BeTrue();
        result.InferredTypes.Should().HaveCount(1);
        result.InferredTypes![0].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ConstructorPattern_ConflictingArgs()
    {
        // Simulates: class Container[T] with __init__(self, a: T, b: T), called as Container(42, "hello")
        // Should fail because T can't be both int and str
        var funcSymbol = new FunctionSymbol
        {
            Name = "__init__",
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
            ReturnType = SemanticType.Void
        };

        var argumentTypes = new List<SemanticType> { SemanticType.Int, SemanticType.Str };
        var result = _service.InferTypeArguments(funcSymbol, argumentTypes);

        result.Success.Should().BeFalse();
        result.ErrorKind.Should().Be(InferenceErrorKind.ConflictingTypes);
    }

    #endregion

    #region UnifyTypes

    [Fact]
    public void UnifyTypes_SingleParam_FunctionType_InfersTFromParameter()
    {
        // Formal: (T) -> bool, Actual: (int) -> bool → T=int
        var formals = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                ReturnType = SemanticType.Bool
            }
        };
        var actuals = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Int },
                ReturnType = SemanticType.Bool
            }
        };

        var result = _service.UnifyTypes(formals, actuals);

        result.Should().NotBeNull();
        result.Should().ContainKey("T");
        result!["T"].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void UnifyTypes_TwoParams_FunctionType_InfersTAndU()
    {
        // Formal: (T) -> U, Actual: (str) -> int → T=str, U=int
        var formals = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                ReturnType = new TypeParameterType { Name = "U" }
            }
        };
        var actuals = new List<SemanticType>
        {
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Str },
                ReturnType = SemanticType.Int
            }
        };

        var result = _service.UnifyTypes(formals, actuals);

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result!["T"].Should().Be(SemanticType.Str);
        result["U"].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void UnifyTypes_EmptyLists_ReturnsEmptyDict()
    {
        // No params to unify → empty dict, not null
        var formals = new List<SemanticType>();
        var actuals = new List<SemanticType>();

        var result = _service.UnifyTypes(formals, actuals);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void UnifyTypes_NestedGenericType_InfersT()
    {
        // Formal: list[T], Actual: list[int] → T=int
        var formals = new List<SemanticType>
        {
            new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "T" } }
            }
        };
        var actuals = new List<SemanticType>
        {
            new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Int }
            }
        };

        var result = _service.UnifyTypes(formals, actuals);

        result.Should().NotBeNull();
        result.Should().ContainKey("T");
        result!["T"].Should().Be(SemanticType.Int);
    }

    [Fact]
    public void UnifyTypes_MismatchedArity_UnifiesWhatItCan()
    {
        // 2 formals vs 1 actual → should still unify the first pair
        var formals = new List<SemanticType>
        {
            new TypeParameterType { Name = "T" },
            new TypeParameterType { Name = "U" }
        };
        var actuals = new List<SemanticType>
        {
            SemanticType.Int
        };

        var result = _service.UnifyTypes(formals, actuals);

        result.Should().NotBeNull();
        result.Should().ContainKey("T");
        result!["T"].Should().Be(SemanticType.Int);
        result.Should().NotContainKey("U");
    }

    [Fact]
    public void UnifyTypes_ResultTypeWithFunctionParams_InfersTAndE()
    {
        // Formal: Result[T, E] + (T) -> U
        // Actual: Result[int, str] + (int) -> bool
        // → T=int, E=str, U=bool
        var formals = new List<SemanticType>
        {
            new ResultType
            {
                OkType = new TypeParameterType { Name = "T" },
                ErrorType = new TypeParameterType { Name = "E" }
            },
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
                ReturnType = new TypeParameterType { Name = "U" }
            }
        };
        var actuals = new List<SemanticType>
        {
            new ResultType
            {
                OkType = SemanticType.Int,
                ErrorType = SemanticType.Str
            },
            new FunctionType
            {
                ParameterTypes = new List<SemanticType> { SemanticType.Int },
                ReturnType = SemanticType.Bool
            }
        };

        var result = _service.UnifyTypes(formals, actuals);

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result!["T"].Should().Be(SemanticType.Int);
        result["E"].Should().Be(SemanticType.Str);
        result["U"].Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void UnifyTypes_ConflictingBindings_ReturnsNull()
    {
        // Formal: T, T
        // Actual: int, str
        // → T=int conflicts with T=str → null
        var formals = new List<SemanticType>
        {
            new TypeParameterType { Name = "T" },
            new TypeParameterType { Name = "T" }
        };
        var actuals = new List<SemanticType>
        {
            SemanticType.Int,
            SemanticType.Str
        };

        var result = _service.UnifyTypes(formals, actuals);

        result.Should().BeNull();
    }

    [Fact]
    public void UnifyTypes_ConcreteTypesOnly_ReturnsEmptyDict()
    {
        // Formal: int, str (no type params) → empty dict
        var formals = new List<SemanticType>
        {
            SemanticType.Int,
            SemanticType.Str
        };
        var actuals = new List<SemanticType>
        {
            SemanticType.Int,
            SemanticType.Str
        };

        var result = _service.UnifyTypes(formals, actuals);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void UnifyTypes_DictWithTypeParams_InfersKAndV()
    {
        // Formal: dict[K, V], Actual: dict[str, int] → K=str, V=int
        var formals = new List<SemanticType>
        {
            new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType>
                {
                    new TypeParameterType { Name = "K" },
                    new TypeParameterType { Name = "V" }
                }
            }
        };
        var actuals = new List<SemanticType>
        {
            new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType>
                {
                    SemanticType.Str,
                    SemanticType.Int
                }
            }
        };

        var result = _service.UnifyTypes(formals, actuals);

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result!["K"].Should().Be(SemanticType.Str);
        result["V"].Should().Be(SemanticType.Int);
    }

    #endregion

    #region SubstituteTypeParameters

    [Fact]
    public void SubstituteTypeParameters_FunctionType_ReplacesTypeParam()
    {
        // (T) -> bool with T=int → (int) -> bool
        var funcType = new FunctionType
        {
            ParameterTypes = new List<SemanticType> { new TypeParameterType { Name = "T" } },
            ReturnType = SemanticType.Bool
        };
        var substitutions = new Dictionary<string, SemanticType>
        {
            ["T"] = SemanticType.Int
        };

        var result = GenericTypeInferenceService.SubstituteTypeParameters(funcType, substitutions);

        var ft = result.Should().BeOfType<FunctionType>().Subject;
        ft.ParameterTypes.Should().HaveCount(1);
        ft.ParameterTypes[0].Should().Be(SemanticType.Int);
        ft.ReturnType.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void SubstituteTypeParameters_FunctionType_ReplacesMultipleParams()
    {
        // (T, U) -> T with T=int, U=str → (int, str) -> int
        var funcType = new FunctionType
        {
            ParameterTypes = new List<SemanticType>
            {
                new TypeParameterType { Name = "T" },
                new TypeParameterType { Name = "U" }
            },
            ReturnType = new TypeParameterType { Name = "T" }
        };
        var substitutions = new Dictionary<string, SemanticType>
        {
            ["T"] = SemanticType.Int,
            ["U"] = SemanticType.Str
        };

        var result = GenericTypeInferenceService.SubstituteTypeParameters(funcType, substitutions);

        var ft = result.Should().BeOfType<FunctionType>().Subject;
        ft.ParameterTypes.Should().HaveCount(2);
        ft.ParameterTypes[0].Should().Be(SemanticType.Int);
        ft.ParameterTypes[1].Should().Be(SemanticType.Str);
        ft.ReturnType.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void SubstituteTypeParameters_NestedGenericInFunction_ReplacesRecursively()
    {
        // (list[T]) -> dict[T, U] with T=int, U=str → (list[int]) -> dict[int, str]
        var funcType = new FunctionType
        {
            ParameterTypes = new List<SemanticType>
            {
                new GenericType
                {
                    Name = "list",
                    TypeArguments = new List<SemanticType> { new TypeParameterType { Name = "T" } }
                }
            },
            ReturnType = new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType>
                {
                    new TypeParameterType { Name = "T" },
                    new TypeParameterType { Name = "U" }
                }
            }
        };
        var substitutions = new Dictionary<string, SemanticType>
        {
            ["T"] = SemanticType.Int,
            ["U"] = SemanticType.Str
        };

        var result = GenericTypeInferenceService.SubstituteTypeParameters(funcType, substitutions);

        var ft = result.Should().BeOfType<FunctionType>().Subject;

        // Check parameter: list[int]
        var paramType = ft.ParameterTypes[0].Should().BeOfType<GenericType>().Subject;
        paramType.Name.Should().Be("list");
        paramType.TypeArguments[0].Should().Be(SemanticType.Int);

        // Check return: dict[int, str]
        var retType = ft.ReturnType.Should().BeOfType<GenericType>().Subject;
        retType.Name.Should().Be("dict");
        retType.TypeArguments[0].Should().Be(SemanticType.Int);
        retType.TypeArguments[1].Should().Be(SemanticType.Str);
    }

    [Fact]
    public void SubstituteTypeParameters_NoMatchingParams_ReturnsUnchanged()
    {
        // (int) -> bool with T=str → no change
        var funcType = new FunctionType
        {
            ParameterTypes = new List<SemanticType> { SemanticType.Int },
            ReturnType = SemanticType.Bool
        };
        var substitutions = new Dictionary<string, SemanticType>
        {
            ["T"] = SemanticType.Str
        };

        var result = GenericTypeInferenceService.SubstituteTypeParameters(funcType, substitutions);

        var ft = result.Should().BeOfType<FunctionType>().Subject;
        ft.ParameterTypes[0].Should().Be(SemanticType.Int);
        ft.ReturnType.Should().Be(SemanticType.Bool);
    }

    #endregion
}
