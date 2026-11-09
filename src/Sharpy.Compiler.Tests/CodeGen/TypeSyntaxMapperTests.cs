using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using AstFunctionType = Sharpy.Compiler.Parser.Ast.FunctionType;

namespace Sharpy.Compiler.Tests.CodeGen;

public class TypeSyntaxMapperTests
{
    private readonly TypeSyntaxMapper _typeMapper;
    private readonly CodeGenContext _context;

    public TypeSyntaxMapperTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins) { Ir = IrCompilation.Empty };
        _typeMapper = new TypeSyntaxMapper(_context);
    }

    #region Builtin Type-Argument and Real-Suffix Hand-offs (#2443 item 1, plan-1faae3 D4)

    private static GenericType Builtin(string name, params SemanticType[] arguments)
        => new() { Name = name, TypeArguments = arguments.ToList() };

    private static string? Render(IReadOnlyList<TypeSyntax>? arguments)
        => arguments == null ? null : string.Join(", ", arguments.Select(a => a.ToString()));

    [Fact]
    public void MapBuiltinTypeArguments_MatchingNameAndArity_MapsTheLeadingArguments()
    {
        Render(_typeMapper.MapBuiltinTypeArguments(Builtin("list", SemanticType.Int), "list", 1, true))
            .Should().Be("int");
        Render(_typeMapper.MapBuiltinTypeArguments(Builtin("dict", SemanticType.Str, SemanticType.Int), "dict", 2, true))
            .Should().Be("string, int");
        Render(_typeMapper.MapBuiltinTypeArguments(Builtin("set", SemanticType.Str, SemanticType.Int), "set", 1, true))
            .Should().Be("string", "only the first arity arguments are mapped");
    }

    [Fact]
    public void MapBuiltinTypeArguments_TooFewArgumentsOrNotAGeneric_IsNull()
    {
        _typeMapper.MapBuiltinTypeArguments(Builtin("dict", SemanticType.Str), "dict", 2, false).Should().BeNull();
        _typeMapper.MapBuiltinTypeArguments(Builtin("list"), "list", 1, false).Should().BeNull();
        _typeMapper.MapBuiltinTypeArguments(SemanticType.Int, "list", 1, false).Should().BeNull();
        _typeMapper.MapBuiltinTypeArguments(null, "list", 1, false).Should().BeNull();
    }

    [Fact]
    public void MapBuiltinTypeArguments_OtherName_IsNull()
    {
        // A generic of another name (a user `Box[int]`, a `set[int]` asked as a list) is not the builtin.
        _typeMapper.MapBuiltinTypeArguments(Builtin("Box", SemanticType.Int), "list", 1, false).Should().BeNull();
        _typeMapper.MapBuiltinTypeArguments(Builtin("set", SemanticType.Int), "list", 1, true).Should().BeNull();
    }

    [Fact]
    public void MapBuiltinTypeArguments_UnknownArgument_IsNullOnlyWhenKnownArgumentsAreRequired()
    {
        var partial = Builtin("dict", SemanticType.Str, SemanticType.Unknown);
        // dict literal: either argument Unknown -> null, the caller emits <object, object>.
        _typeMapper.MapBuiltinTypeArguments(partial, "dict", 2, requireKnownArguments: true).Should().BeNull();
        // dict-spread comprehension: an Unknown argument maps to object on its own.
        Render(_typeMapper.MapBuiltinTypeArguments(partial, "dict", 2, requireKnownArguments: false))
            .Should().Be("string, object");
    }

    [Fact]
    public void RealLiteralSuffix_FollowsTheRecordedRealType()
    {
        TypeSyntaxMapper.RealLiteralSuffix(SemanticType.Float32).Should().Be("f");
        TypeSyntaxMapper.RealLiteralSuffix(SemanticType.Decimal).Should().Be("m");
        TypeSyntaxMapper.RealLiteralSuffix(SemanticType.Float).Should().BeNull("float is the default double form");
        TypeSyntaxMapper.RealLiteralSuffix(SemanticType.Int).Should().BeNull();
        TypeSyntaxMapper.RealLiteralSuffix(SemanticType.Unknown).Should().BeNull();
        TypeSyntaxMapper.RealLiteralSuffix(null).Should().BeNull();
    }

    #endregion

    #region Built-in Type Mapping Tests

    [Theory]
    [InlineData("int", "int")]
    [InlineData("long", "long")]
    [InlineData("float", "double")]      // Per spec: Sharpy float -> C# double
    [InlineData("float32", "float")]     // Per spec: Sharpy float32 -> C# float
    [InlineData("double", "double")]
    [InlineData("bool", "bool")]
    [InlineData("byte", "byte")]
    [InlineData("void", "void")]
    public void MapType_PrimitiveTypes_MapsCorrectly(string sharpyType, string expectedCSharpType)
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation { Name = sharpyType };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be(expectedCSharpType);
    }

    [Theory]
    [InlineData("str", "string")]
    [InlineData("string", "string")]
    public void MapType_StringTypes_MapsCorrectly(string sharpyType, string expectedCSharpType)
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation { Name = sharpyType };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be(expectedCSharpType);
    }

    [Fact]
    public void MapType_NullType_ReturnsObject()
    {
        // Act
        var result = _typeMapper.MapType(null);

        // Assert
        result.ToString().Should().Be("object");
    }

    #endregion

    #region Nullable Type Tests

    [Fact]
    public void MapType_OptionalInt_ReturnsOptionalInt()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "int",
            IsOptional = true
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("Optional<int>");
    }

    [Fact]
    public void MapType_OptionalString_ReturnsOptionalString()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "str",
            IsOptional = true
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("Optional<string>");
    }

    [Fact]
    public void MapType_CSharpNullableInt_ReturnsIntQuestion()
    {
        // T | None syntax (IsCSharpNullable) also maps to C# T?
        var typeAnnotation = new TypeAnnotation
        {
            Name = "int",
            IsCSharpNullable = true
        };

        var result = _typeMapper.MapType(typeAnnotation);

        result.ToString().Should().Be("int?");
    }

    [Fact]
    public void MapType_ResultType_ReturnsResultGeneric()
    {
        // T !E maps to Result<T, E>
        var typeAnnotation = new TypeAnnotation
        {
            Name = "int",
            ErrorType = new TypeAnnotation { Name = "str" }
        };

        var result = _typeMapper.MapType(typeAnnotation);

        result.ToString().Should().Be("Result<int,string>");
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void MapType_ListOfInt_ReturnsListInt()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "list",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray()
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("Sharpy.List<int>");
    }

    [Fact]
    public void MapType_DictOfStringInt_ReturnsDictionaryStringInt()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "dict",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "str" },
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray()
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("Sharpy.Dict<string,int>");
    }

    [Fact]
    public void MapType_SetOfString_ReturnsHashSetString()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "set",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "str" }
            }.ToImmutableArray()
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("Sharpy.Set<string>");
    }

    [Fact]
    public void MapType_OptionalListOfInt_ReturnsOptionalListInt()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "list",
            IsOptional = true,
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray()
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("Optional<Sharpy.List<int>>");
    }

    #endregion

    #region Function Type Tests

    [Fact]
    public void MapFunctionType_NoParamsReturnsInt_ReturnsFuncInt()
    {
        // Arrange
        var funcType = new AstFunctionType
        {
            ParameterTypes = ImmutableArray<TypeAnnotation>.Empty,
            ReturnType = new TypeAnnotation { Name = "int" }
        };

        // Act
        var result = _typeMapper.MapFunctionType(funcType);

        // Assert
        result.ToString().Should().Be("global::System.Func<int>");
    }

    [Fact]
    public void MapFunctionType_IntParamReturnsString_ReturnsFuncIntString()
    {
        // Arrange
        var funcType = new AstFunctionType
        {
            ParameterTypes = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray(),
            ReturnType = new TypeAnnotation { Name = "str" }
        };

        // Act
        var result = _typeMapper.MapFunctionType(funcType);

        // Assert
        result.ToString().Should().Be("global::System.Func<int,string>");
    }

    [Fact]
    public void MapFunctionType_TwoParamsReturnsVoid_ReturnsActionIntString()
    {
        // Arrange
        var funcType = new AstFunctionType
        {
            ParameterTypes = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" },
                new TypeAnnotation { Name = "str" }
            }.ToImmutableArray(),
            ReturnType = new TypeAnnotation { Name = "void" }
        };

        // Act
        var result = _typeMapper.MapFunctionType(funcType);

        // Assert
        result.ToString().Should().Be("global::System.Action<int,string>");
    }

    [Fact]
    public void MapFunctionType_NoParamsReturnsVoid_ReturnsAction()
    {
        // Arrange
        var funcType = new AstFunctionType
        {
            ParameterTypes = ImmutableArray<TypeAnnotation>.Empty,
            ReturnType = new TypeAnnotation { Name = "void" }
        };

        // Act
        var result = _typeMapper.MapFunctionType(funcType);

        // Assert
        result.ToString().Should().Be("global::System.Action");
    }

    [Fact]
    public void MapType_FunctionAnnotation_NoParamsReturnsInt_ReturnsFuncInt()
    {
        // (int) -> int with no params is () -> int, encoded as TypeArguments = [int]
        var typeAnnotation = new TypeAnnotation
        {
            Name = "function",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray()
        };

        var result = _typeMapper.MapType(typeAnnotation);

        result.ToString().Should().Be("global::System.Func<int>");
    }

    [Fact]
    public void MapType_FunctionAnnotation_IntParamReturnsInt_ReturnsFuncIntInt()
    {
        // (int) -> int encoded as TypeArguments = [int, int]
        var typeAnnotation = new TypeAnnotation
        {
            Name = "function",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" },
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray()
        };

        var result = _typeMapper.MapType(typeAnnotation);

        result.ToString().Should().Be("global::System.Func<int,int>");
    }

    [Fact]
    public void MapType_FunctionAnnotation_IntParamReturnsVoid_ReturnsActionInt()
    {
        // (int) -> None encoded as TypeArguments = [int, None]
        var typeAnnotation = new TypeAnnotation
        {
            Name = "function",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" },
                new TypeAnnotation { Name = "None" }
            }.ToImmutableArray()
        };

        var result = _typeMapper.MapType(typeAnnotation);

        result.ToString().Should().Be("global::System.Action<int>");
    }

    [Fact]
    public void MapType_FunctionAnnotation_NoParamsReturnsVoid_ReturnsAction()
    {
        // () -> None encoded as TypeArguments = [None]
        var typeAnnotation = new TypeAnnotation
        {
            Name = "function",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "None" }
            }.ToImmutableArray()
        };

        var result = _typeMapper.MapType(typeAnnotation);

        result.ToString().Should().Be("global::System.Action");
    }

    #endregion

    // Note: the emitter's syntactic element-type inference tier (InferElementType /
    // InferTypeFromExpression / InferExpressionType) was removed in #1039/#973. Element and
    // declaration types are now authoritative from SemanticInfo (TypeChecker) and are covered by
    // the file-based integration + snapshot suites rather than these unit tests.

    #region Helper Method Tests

    [Fact]
    public void CreateCollectionType_ListOfInt_ReturnsListInt()
    {
        // Arrange
        var elementType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));

        // Act
        var result = _typeMapper.CreateCollectionType("list", elementType);

        // Assert
        result.ToString().Should().Be("Sharpy.List<int>");
    }

    [Fact]
    public void CreateDictType_StringToInt_ReturnsDictionaryStringInt()
    {
        // Arrange
        var keyType = SyntaxFactory.ParseTypeName("string");
        var valueType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));

        // Act
        var result = _typeMapper.CreateDictType(keyType, valueType);

        // Assert
        result.ToString().Should().Be("Sharpy.Dict<string,int>");
    }

    [Fact]
    public void MakeNullable_Int_ReturnsNullableInt()
    {
        // Arrange
        var type = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));

        // Act
        var result = _typeMapper.MakeNullable(type);

        // Assert
        result.ToString().Should().Be("int?");
    }

    [Fact]
    public void MakeArrayType_IntArray_ReturnsIntArray()
    {
        // Arrange
        var elementType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));

        // Act
        var result = _typeMapper.MakeArrayType(elementType);

        // Assert
        result.ToString().Should().Be("int[]");
    }

    #endregion
}
