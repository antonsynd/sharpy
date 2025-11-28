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
    [InlineData("float", "float")]
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
    public void MapType_NullableInt_ReturnsNullableInt()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "int",
            IsNullable = true
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("int?");
    }

    [Fact]
    public void MapType_NullableString_ReturnsNullableString()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "str",
            IsNullable = true
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("string?");
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void MapType_ListOfInt_ReturnsSharpyListInt()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "list",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("global::Sharpy.Core.List<int>");
    }

    [Fact]
    public void MapType_DictOfStringInt_ReturnsSharpyDictStringInt()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "dict",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "str" },
                new TypeAnnotation { Name = "int" }
            }
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("global::Sharpy.Core.Dict<string,int>");
    }

    [Fact]
    public void MapType_SetOfString_ReturnsSharpySetString()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "set",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "str" }
            }
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("global::Sharpy.Core.Set<string>");
    }

    [Fact]
    public void MapType_NullableListOfInt_ReturnsNullableSharpyListInt()
    {
        // Arrange
        var typeAnnotation = new TypeAnnotation
        {
            Name = "list",
            IsNullable = true,
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }
        };

        // Act
        var result = _typeMapper.MapType(typeAnnotation);

        // Assert
        result.ToString().Should().Be("global::Sharpy.Core.List<int>?");
    }

    #endregion

    #region Function Type Tests

    [Fact]
    public void MapFunctionType_NoParamsReturnsInt_ReturnsFuncInt()
    {
        // Arrange
        var funcType = new AstFunctionType
        {
            ParameterTypes = new List<TypeAnnotation>(),
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
            },
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
            },
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
            ParameterTypes = new List<TypeAnnotation>(),
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
            ElementTypes = new List<TypeAnnotation>()
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
            }
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
            }
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
        };

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
        };

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
        };

        // Act
        var result = _typeMapper.InferElementType(expressions);

        // Assert
        result.ToString().Should().Be("object");
    }

    [Fact]
    public void InferElementType_EmptyList_ReturnsObject()
    {
        // Arrange
        var expressions = new List<Expression>();

        // Act
        var result = _typeMapper.InferElementType(expressions);

        // Assert
        result.ToString().Should().Be("object");
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void CreateCollectionType_ListOfInt_ReturnsSharpyListInt()
    {
        // Arrange
        var elementType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));

        // Act
        var result = _typeMapper.CreateCollectionType("list", elementType);

        // Assert
        result.ToString().Should().Be("global::Sharpy.Core.List<int>");
    }

    [Fact]
    public void CreateDictType_StringToInt_ReturnsSharpyDictStringInt()
    {
        // Arrange
        var keyType = SyntaxFactory.ParseTypeName("string");
        var valueType = SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword));

        // Act
        var result = _typeMapper.CreateDictType(keyType, valueType);

        // Assert
        result.ToString().Should().Be("global::Sharpy.Core.Dict<string,int>");
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
