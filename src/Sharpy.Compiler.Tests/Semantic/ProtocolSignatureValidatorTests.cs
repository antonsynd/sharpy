using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

public class ProtocolSignatureValidatorTests
{
    private FunctionDef CreateProtocolMethod(
        string name,
        int paramCount,
        string? returnTypeName = null,
        int lineStart = 1,
        int columnStart = 1)
    {
        var parameters = new List<Parameter>();
        for (int i = 0; i < paramCount; i++)
        {
            parameters.Add(new Parameter
            {
                Name = i == 0 ? "self" : $"param{i}",
                Type = new TypeAnnotation { Name = "object" },
                LineStart = lineStart,
                ColumnStart = columnStart
            });
        }

        var returnType = returnTypeName != null
            ? new TypeAnnotation { Name = returnTypeName }
            : null;

        return new FunctionDef
        {
            Name = name,
            Parameters = parameters,
            ReturnType = returnType,
            Body = new(),
            LineStart = lineStart,
            ColumnStart = columnStart
        };
    }

    private TypeSymbol CreateTypeSymbol(string name = "TestClass")
    {
        return new TypeSymbol
        {
            Name = name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };
    }

    // ==================== Test IsProtocolDunder ====================

    [Theory]
    [InlineData("__len__", true)]
    [InlineData("__str__", true)]
    [InlineData("__iter__", true)]
    [InlineData("__next__", true)]
    [InlineData("__init__", true)]
    [InlineData("__contains__", true)]
    [InlineData("__getitem__", true)]
    [InlineData("__setitem__", true)]
    [InlineData("__delitem__", true)]
    [InlineData("__repr__", true)]
    [InlineData("__hash__", true)]
    [InlineData("__bool__", true)]
    public void IsProtocolDunder_ReturnsTrueForProtocolDunders(string methodName, bool expected)
    {
        ProtocolSignatureValidator.IsProtocolDunder(methodName).Should().Be(expected);
    }

    [Theory]
    [InlineData("__add__")]
    [InlineData("__sub__")]
    [InlineData("__mul__")]
    [InlineData("__eq__")]
    [InlineData("__ne__")]
    [InlineData("__lt__")]
    public void IsProtocolDunder_ReturnsFalseForOperatorDunders(string methodName)
    {
        // Operator dunders are handled by OperatorSignatureValidator, not ProtocolSignatureValidator
        ProtocolSignatureValidator.IsProtocolDunder(methodName).Should().BeFalse();
    }

    [Theory]
    [InlineData("regular_method")]
    [InlineData("MyMethod")]
    [InlineData("get_value")]
    [InlineData("__custom_method__")]
    public void IsProtocolDunder_ReturnsFalseForNonDunders(string methodName)
    {
        ProtocolSignatureValidator.IsProtocolDunder(methodName).Should().BeFalse();
    }

    // ==================== Test Parameter Count Validation ====================

    [Theory]
    [InlineData("__len__", 1)]
    [InlineData("__str__", 1)]
    [InlineData("__repr__", 1)]
    [InlineData("__hash__", 1)]
    [InlineData("__bool__", 1)]
    [InlineData("__iter__", 1)]
    [InlineData("__next__", 1)]
    public void ValidateDunderSignature_AcceptsCorrectParamCountForSingleSelfMethods(string dunderName, int paramCount)
    {
        var funcDef = CreateProtocolMethod(dunderName, paramCount);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("__contains__", 2)]
    [InlineData("__getitem__", 2)]
    [InlineData("__delitem__", 2)]
    public void ValidateDunderSignature_AcceptsCorrectParamCountForTwoParamMethods(string dunderName, int paramCount)
    {
        var funcDef = CreateProtocolMethod(dunderName, paramCount);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_AcceptsCorrectParamCountForSetItem()
    {
        // __setitem__ takes self, key, value = 3 params
        var funcDef = CreateProtocolMethod("__setitem__", 3);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_AcceptsAnyParamCountForInit()
    {
        // __init__ can have any number of parameters (1+ for self)
        var typeSymbol = CreateTypeSymbol();

        for (int i = 1; i <= 5; i++)
        {
            var funcDef = CreateProtocolMethod("__init__", i);
            var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);
            errors.Should().BeEmpty($"__init__ should accept {i} parameters");
        }
    }

    [Fact]
    public void ValidateDunderSignature_RejectsInitWithZeroParams()
    {
        // __init__ with 0 parameters should produce a 'must have self' error
        var funcDef = CreateProtocolMethod("__init__", 0);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("self");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsWrongParamCountForLen()
    {
        // __len__ must have exactly 1 parameter (self)
        var funcDef = CreateProtocolMethod("__len__", 2);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("__len__");
        errors[0].Message.Should().Contain("1 parameter");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsWrongParamCountForContains()
    {
        // __contains__ must have exactly 2 parameters (self, item)
        var funcDef = CreateProtocolMethod("__contains__", 1);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("__contains__");
        errors[0].Message.Should().Contain("2 parameters");
    }

    // ==================== Test Return Type Validation ====================

    [Theory]
    [InlineData("__len__", "int")]
    [InlineData("__str__", "str")]
    [InlineData("__repr__", "str")]
    [InlineData("__hash__", "int")]
    [InlineData("__bool__", "bool")]
    [InlineData("__contains__", "bool")]
    public void ValidateDunderSignature_AcceptsCorrectReturnType(string dunderName, string returnType)
    {
        var paramCount = ProtocolRegistry.GetProtocol(dunderName)!.ExpectedParamCount;
        var funcDef = CreateProtocolMethod(dunderName, paramCount, returnType);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_AcceptsVoidReturnForSetItem()
    {
        var funcDef = CreateProtocolMethod("__setitem__", 3, "None");
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_AcceptsVoidReturnForDelItem()
    {
        var funcDef = CreateProtocolMethod("__delitem__", 2, "None");
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_AcceptsNoneReturnForInit()
    {
        var funcDef = CreateProtocolMethod("__init__", 1, "None");
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_RejectsWrongReturnTypeForInit()
    {
        var funcDef = CreateProtocolMethod("__init__", 1, "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("__init__");
        errors[0].Message.Should().Contain("must return");
        errors[0].Message.Should().Contain("None");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsWrongReturnTypeForLen()
    {
        var funcDef = CreateProtocolMethod("__len__", 1, "str");
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("__len__");
        errors[0].Message.Should().Contain("must return");
        errors[0].Message.Should().Contain("int");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsWrongReturnTypeForStr()
    {
        var funcDef = CreateProtocolMethod("__str__", 1, "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("__str__");
        errors[0].Message.Should().Contain("must return");
        errors[0].Message.Should().Contain("str");
    }

    // ==================== Test Self Parameter Validation ====================

    [Fact]
    public void ValidateDunderSignature_RejectsFirstParamNotSelf()
    {
        var funcDef = new FunctionDef
        {
            Name = "__len__",
            Parameters = new List<Parameter>
            {
                new Parameter { Name = "this", Type = new TypeAnnotation { Name = "object" } }
            },
            Body = new(),
            LineStart = 1,
            ColumnStart = 1
        };
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("self");
        errors[0].Message.Should().Contain("this");
    }

    // ==================== Test Non-Protocol Dunders Return Empty ====================

    [Fact]
    public void ValidateDunderSignature_ReturnsEmptyForNonProtocolDunder()
    {
        var funcDef = CreateProtocolMethod("regular_method", 3);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    // ==================== Test Error Message Quality ====================

    [Fact]
    public void ValidateDunderSignature_IncludesTypeNameInError()
    {
        var funcDef = CreateProtocolMethod("__len__", 2);
        var typeSymbol = CreateTypeSymbol("MyCustomClass");

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("MyCustomClass");
    }

    [Fact]
    public void ValidateDunderSignature_IncludesInterfaceReferenceForDocumentedProtocols()
    {
        var funcDef = CreateProtocolMethod("__len__", 2);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().ContainSingle();
        errors[0].Message.Should().Contain("ISized");
    }

    // ==================== Test No Return Type Annotation ====================

    [Fact]
    public void ValidateDunderSignature_AcceptsNoReturnTypeAnnotation()
    {
        // If no return type annotation, validation is skipped (inferred later)
        var funcDef = CreateProtocolMethod("__str__", 1, null);
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    // ==================== Test Generic Return Types ====================

    [Fact]
    public void ValidateDunderSignature_AcceptsAnyReturnTypeForGetItem()
    {
        // __getitem__ can return any type (element type of the collection)
        var funcDef = CreateProtocolMethod("__getitem__", 2, "SomeType");
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_AcceptsAnyReturnTypeForIter()
    {
        // __iter__ can return any iterator type
        var funcDef = CreateProtocolMethod("__iter__", 1, "Iterator[int]");
        var typeSymbol = CreateTypeSymbol();

        var errors = ProtocolSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }
}
