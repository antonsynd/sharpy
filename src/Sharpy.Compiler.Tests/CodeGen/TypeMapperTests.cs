using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;
using AstFunctionType = Sharpy.Compiler.Parser.Ast.FunctionType;
using AstTupleType = Sharpy.Compiler.Parser.Ast.TupleType;

namespace Sharpy.Compiler.Tests.CodeGen;

public class TypeMapperTests
{
    private readonly TypeMapper _typeMapper;
    private readonly CodeGenContext _context;

    public TypeMapperTests()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        _context = new CodeGenContext(symbolTable, builtins);
        _typeMapper = new TypeMapper(_context);
    }

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
        result.ToString().Should().Be("System.Collections.Generic.List<int>");
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
        result.ToString().Should().Be("Dict<string,int>");
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
        result.ToString().Should().Be("System.Collections.Generic.HashSet<string>");
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
        result.ToString().Should().Be("Optional<System.Collections.Generic.List<int>>");
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
        result.ToString().Should().Be("System.Func<int>");
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
        result.ToString().Should().Be("System.Func<int,string>");
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
        result.ToString().Should().Be("System.Action<int,string>");
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
        result.ToString().Should().Be("System.Action");
    }

    #endregion

    #region Tuple Type Tests

    [Fact]
    public void MapTupleType_EmptyTuple_ReturnsValueTuple()
    {
        // Arrange
        var tupleType = new AstTupleType
        {
            ElementTypes = ImmutableArray<TypeAnnotation>.Empty
        };

        // Act
        var result = _typeMapper.MapTupleType(tupleType);

        // Assert
        result.ToString().Should().Be("System.ValueTuple");
    }

    [Fact]
    public void MapTupleType_SingleElement_ReturnsElementType()
    {
        // Arrange
        var tupleType = new AstTupleType
        {
            ElementTypes = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }.ToImmutableArray()
        };

        // Act
        var result = _typeMapper.MapTupleType(tupleType);

        // Assert
        result.ToString().Should().Be("int");
    }

    [Fact]
    public void MapTupleType_TwoElements_ReturnsValueTupleIntString()
    {
        // Arrange
        var tupleType = new AstTupleType
        {
            ElementTypes = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" },
                new TypeAnnotation { Name = "str" }
            }.ToImmutableArray()
        };

        // Act
        var result = _typeMapper.MapTupleType(tupleType);

        // Assert
        result.ToString().Should().Be("System.ValueTuple<int,string>");
    }

    #endregion

    #region Type Inference Tests

    [Fact]
    public void InferElementType_AllIntegers_ReturnsInt()
    {
        // Arrange
        var expressions = new List<Expression>
        {
            new IntegerLiteral { Value = "1" },
            new IntegerLiteral { Value = "2" },
            new IntegerLiteral { Value = "3" }
        }.ToImmutableArray();

        // Act
        var result = _typeMapper.InferElementType(expressions);

        // Assert
        result.ToString().Should().Be("int");
    }

    [Fact]
    public void InferElementType_AllFloats_ReturnsDouble()
    {
        // Arrange
        var expressions = new List<Expression>
        {
            new FloatLiteral { Value = "1.0" },
            new FloatLiteral { Value = "2.0" },
            new FloatLiteral { Value = "3.0" }
        }.ToImmutableArray();

        // Act
        var result = _typeMapper.InferElementType(expressions);

        // Assert
        result.ToString().Should().Be("double");
    }

    [Fact]
    public void InferElementType_MixedTypes_ReturnsObject()
    {
        // Arrange
        var expressions = new List<Expression>
        {
            new IntegerLiteral { Value = "1" },
            new StringLiteral { Value = "hello" },
            new BooleanLiteral { Value = true }
        }.ToImmutableArray();

        // Act
        var result = _typeMapper.InferElementType(expressions);

        // Assert
        result.ToString().Should().Be("object");
    }

    [Fact]
    public void InferElementType_EmptyList_ReturnsObject()
    {
        // Arrange
        var expressions = ImmutableArray<Expression>.Empty;

        // Act
        var result = _typeMapper.InferElementType(expressions);

        // Assert
        result.ToString().Should().Be("object");
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void CreateCollectionType_ListOfInt_ReturnsListInt()
    {
        // Arrange
        var elementType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));

        // Act
        var result = _typeMapper.CreateCollectionType("list", elementType);

        // Assert
        result.ToString().Should().Be("System.Collections.Generic.List<int>");
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
        result.ToString().Should().Be("Dict<string,int>");
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
