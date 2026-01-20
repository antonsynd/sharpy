using System.Collections.Immutable;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

public class OperatorSignatureValidatorTests
{
    private FunctionDef CreateOperatorMethod(
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
                Name = i == 0 ? "self" : $"other{i}",
                Type = new TypeAnnotation { Name = "int" },
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
            Parameters = parameters.ToImmutableArray(),
            ReturnType = returnType,
            Body = ImmutableArray<Statement>.Empty,
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

    #region IsOperatorDunder Tests

    [Fact]
    public void IsOperatorDunder_RecognizesArithmeticOperators()
    {
        OperatorSignatureValidator.IsOperatorDunder("__add__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__sub__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__mul__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__truediv__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__floordiv__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__mod__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__pow__").Should().BeTrue();
    }

    [Fact]
    public void IsOperatorDunder_RecognizesBitwiseOperators()
    {
        OperatorSignatureValidator.IsOperatorDunder("__and__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__or__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__xor__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__lshift__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__rshift__").Should().BeTrue();
    }

    [Fact]
    public void IsOperatorDunder_RecognizesInPlaceOperators()
    {
        OperatorSignatureValidator.IsOperatorDunder("__iadd__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__isub__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__imul__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__itruediv__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__ifloordiv__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__imod__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__ipow__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__iand__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__ior__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__ixor__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__ilshift__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__irshift__").Should().BeTrue();
    }

    [Fact]
    public void IsOperatorDunder_RecognizesComparisonOperators()
    {
        OperatorSignatureValidator.IsOperatorDunder("__eq__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__ne__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__lt__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__le__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__gt__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__ge__").Should().BeTrue();
    }

    [Fact]
    public void IsOperatorDunder_RecognizesUnaryOperators()
    {
        OperatorSignatureValidator.IsOperatorDunder("__pos__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__neg__").Should().BeTrue();
        OperatorSignatureValidator.IsOperatorDunder("__invert__").Should().BeTrue();
    }

    [Fact]
    public void IsOperatorDunder_RejectsNonOperatorDunders()
    {
        OperatorSignatureValidator.IsOperatorDunder("__init__").Should().BeFalse();
        OperatorSignatureValidator.IsOperatorDunder("__str__").Should().BeFalse();
        OperatorSignatureValidator.IsOperatorDunder("__repr__").Should().BeFalse();
        OperatorSignatureValidator.IsOperatorDunder("__hash__").Should().BeFalse();
        OperatorSignatureValidator.IsOperatorDunder("__len__").Should().BeFalse();
        OperatorSignatureValidator.IsOperatorDunder("__getitem__").Should().BeFalse();
    }

    [Fact]
    public void IsOperatorDunder_RejectsRegularMethods()
    {
        OperatorSignatureValidator.IsOperatorDunder("regular_method").Should().BeFalse();
        OperatorSignatureValidator.IsOperatorDunder("_private_method").Should().BeFalse();
    }

    #endregion

    #region Unary Operator Tests

    [Fact]
    public void ValidateDunderSignature_AcceptsValidUnaryOperator()
    {
        var funcDef = CreateOperatorMethod("__neg__", paramCount: 1, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_RejectsUnaryOperatorWithTooFewParameters()
    {
        var funcDef = CreateOperatorMethod("__neg__", paramCount: 0, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must have exactly 1 parameter");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsUnaryOperatorWithTooManyParameters()
    {
        var funcDef = CreateOperatorMethod("__pos__", paramCount: 2, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must have exactly 1 parameter");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsUnaryOperatorWithVoidReturn()
    {
        var funcDef = CreateOperatorMethod("__neg__", paramCount: 1, returnTypeName: "None");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must return a non-void type");
    }

    #endregion

    #region Binary Operator Tests

    [Fact]
    public void ValidateDunderSignature_AcceptsValidBinaryArithmeticOperator()
    {
        var funcDef = CreateOperatorMethod("__add__", paramCount: 2, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_AcceptsValidPowerOperator()
    {
        var funcDef = CreateOperatorMethod("__pow__", paramCount: 2, returnTypeName: "float");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_RejectsBinaryOperatorWithTooFewParameters()
    {
        var funcDef = CreateOperatorMethod("__mul__", paramCount: 1, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must have exactly 2 parameters");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsBinaryOperatorWithTooManyParameters()
    {
        var funcDef = CreateOperatorMethod("__sub__", paramCount: 3, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must have exactly 2 parameters");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsBinaryOperatorWithVoidReturn()
    {
        var funcDef = CreateOperatorMethod("__add__", paramCount: 2, returnTypeName: "None");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must return a non-void type");
    }

    #endregion

    #region Comparison Operator Tests

    [Fact]
    public void ValidateDunderSignature_AcceptsValidComparisonOperator()
    {
        var funcDef = CreateOperatorMethod("__eq__", paramCount: 2, returnTypeName: "bool");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_RejectsComparisonOperatorWithNonBoolReturn()
    {
        var funcDef = CreateOperatorMethod("__lt__", paramCount: 2, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must return 'bool'");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsComparisonOperatorWithVoidReturn()
    {
        var funcDef = CreateOperatorMethod("__ne__", paramCount: 2, returnTypeName: "None");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must return 'bool'");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsComparisonOperatorWithWrongParameterCount()
    {
        var funcDef = CreateOperatorMethod("__ge__", paramCount: 1, returnTypeName: "bool");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must have exactly 2 parameters");
    }

    #endregion

    #region In-Place Operator Tests

    [Fact]
    public void ValidateDunderSignature_AcceptsValidInPlaceOperator()
    {
        var funcDef = CreateOperatorMethod("__iadd__", paramCount: 2, returnTypeName: "TestClass");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_AcceptsValidInPlacePowerOperator()
    {
        var funcDef = CreateOperatorMethod("__ipow__", paramCount: 2, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_RejectsInPlaceOperatorWithWrongParameterCount()
    {
        var funcDef = CreateOperatorMethod("__isub__", paramCount: 3, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must have exactly 2 parameters");
    }

    [Fact]
    public void ValidateDunderSignature_RejectsInPlaceOperatorWithVoidReturn()
    {
        var funcDef = CreateOperatorMethod("__imul__", paramCount: 2, returnTypeName: "None");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must return a non-void type");
    }

    #endregion

    #region Bitwise Operator Tests

    [Fact]
    public void ValidateDunderSignature_AcceptsValidBitwiseOperator()
    {
        var funcDef = CreateOperatorMethod("__and__", paramCount: 2, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_AcceptsValidInvertOperator()
    {
        var funcDef = CreateOperatorMethod("__invert__", paramCount: 1, returnTypeName: "int");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_RejectsInvertOperatorWithVoidReturn()
    {
        var funcDef = CreateOperatorMethod("__invert__", paramCount: 1, returnTypeName: "None");
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Message.Should().Contain("must return a non-void type");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidateDunderSignature_AllowsOperatorWithoutReturnTypeAnnotation()
    {
        var funcDef = CreateOperatorMethod("__add__", paramCount: 2, returnTypeName: null);
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        // Should only validate parameter count, not return type if not specified
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_IgnoresNonOperatorDunders()
    {
        var funcDef = CreateOperatorMethod("__init__", paramCount: 0, returnTypeName: null);
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDunderSignature_ReportsCorrectLineAndColumn()
    {
        var funcDef = CreateOperatorMethod("__add__", paramCount: 3, returnTypeName: "int", lineStart: 42, columnStart: 10);
        var typeSymbol = CreateTypeSymbol();

        var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, typeSymbol);

        errors.Should().HaveCount(1);
        errors[0].Line.Should().Be(42);
        errors[0].Column.Should().Be(10);
    }

    #endregion
}
