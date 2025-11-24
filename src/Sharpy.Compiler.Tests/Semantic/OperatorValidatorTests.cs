using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Tests.Semantic;

public class OperatorValidatorTests
{
    private SymbolTable CreateSymbolTable()
    {
        var builtinRegistry = new BuiltinRegistry();
        return new SymbolTable(builtinRegistry);
    }

    private OperatorValidator CreateValidator(SymbolTable? symbolTable = null)
    {
        return new OperatorValidator(symbolTable ?? CreateSymbolTable());
    }

    #region Binary Operator Mapping Tests

    [Fact]
    public void ValidateBinaryOp_LogicalAnd_ReturnsBoolean()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.And,
            SemanticType.Bool,
            SemanticType.Bool,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_LogicalOr_ReturnsBoolean()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Or,
            SemanticType.Bool,
            SemanticType.Bool,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_Equality_ReturnsBoolean()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Equal,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_NotEqual_ReturnsBoolean()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.NotEqual,
            SemanticType.Str,
            SemanticType.Str,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_In_ReturnsBoolean()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.In,
            SemanticType.Int,
            new GenericType { Name = "list", TypeArguments = new() { SemanticType.Int } },
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_Is_ReturnsBoolean()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Is,
            SemanticType.Str,
            SemanticType.Void,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    #endregion

    #region Numeric Operator Tests

    [Fact]
    public void ValidateBinaryOp_IntAddInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntAddFloat_ReturnsFloat()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            SemanticType.Int,
            SemanticType.Float,
            1, 1);

        result.Should().Be(SemanticType.Float);
    }

    [Fact]
    public void ValidateBinaryOp_FloatAddDouble_ReturnsDouble()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            SemanticType.Float,
            SemanticType.Double,
            1, 1);

        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void ValidateBinaryOp_IntSubtractInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Subtract,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntMultiplyInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Multiply,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntDivideInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Divide,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntFloorDivideInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.FloorDivide,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntModuloInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Modulo,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntPowerInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Power,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_FloatPowerFloat_ReturnsFloat()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Power,
            SemanticType.Float,
            SemanticType.Float,
            1, 1);

        result.Should().Be(SemanticType.Float);
    }

    #endregion

    #region Comparison Operator Tests

    [Fact]
    public void ValidateBinaryOp_IntLessThanInt_ReturnsBoolean()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.LessThan,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_FloatGreaterThanDouble_ReturnsBoolean()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.GreaterThan,
            SemanticType.Float,
            SemanticType.Double,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    #endregion

    #region Bitwise Operator Tests

    [Fact]
    public void ValidateBinaryOp_IntBitwiseAndInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.BitwiseAnd,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntBitwiseOrInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.BitwiseOr,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntBitwiseXorInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.BitwiseXor,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntLeftShiftInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.LeftShift,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateBinaryOp_IntRightShiftInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.RightShift,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    #endregion

    #region String Operator Tests

    [Fact]
    public void ValidateBinaryOp_StringAddString_ReturnsString()
    {
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            SemanticType.Str,
            SemanticType.Str,
            1, 1);

        result.Should().Be(SemanticType.Str);
    }

    #endregion

    #region Unary Operator Tests

    [Fact]
    public void ValidateUnaryOp_Not_ReturnsBoolean()
    {
        var validator = CreateValidator();
        var result = validator.ValidateUnaryOp(
            UnaryOperator.Not,
            SemanticType.Bool,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateUnaryOp_MinusInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateUnaryOp(
            UnaryOperator.Minus,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateUnaryOp_PlusFloat_ReturnsFloat()
    {
        var validator = CreateValidator();
        var result = validator.ValidateUnaryOp(
            UnaryOperator.Plus,
            SemanticType.Float,
            1, 1);

        result.Should().Be(SemanticType.Float);
    }

    [Fact]
    public void ValidateUnaryOp_BitwiseNotInt_ReturnsInt()
    {
        var validator = CreateValidator();
        var result = validator.ValidateUnaryOp(
            UnaryOperator.BitwiseNot,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    #endregion

    #region User-Defined Operator Tests

    [Fact]
    public void ValidateBinaryOp_UserDefinedAdd_ReturnsReturnType()
    {
        var symbolTable = CreateSymbolTable();

        // Create a Vector type with __add__ method
        var vectorType = new TypeSymbol
        {
            Name = "Vector",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var addMethod = new FunctionSymbol
        {
            Name = "__add__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } }
            },
            ReturnType = new UserDefinedType { Name = "Vector", Symbol = vectorType }
        };

        vectorType.OperatorMethods["__add__"] = new() { addMethod };
        vectorType.Methods.Add(addMethod);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "Vector", Symbol = vectorType };
        var rightType = new UserDefinedType { Name = "Vector", Symbol = vectorType };

        var result = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            leftType,
            rightType,
            1, 1);

        result.Should().BeEquivalentTo(new UserDefinedType { Name = "Vector", Symbol = vectorType });
    }

    [Fact]
    public void ValidateBinaryOp_UserDefinedComparison_ReturnsBoolean()
    {
        var symbolTable = CreateSymbolTable();

        // Create a Point type with __lt__ method
        var pointType = new TypeSymbol
        {
            Name = "Point",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var ltMethod = new FunctionSymbol
        {
            Name = "__lt__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Point", Symbol = pointType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "Point", Symbol = pointType } }
            },
            ReturnType = SemanticType.Bool
        };

        pointType.OperatorMethods["__lt__"] = new() { ltMethod };
        pointType.Methods.Add(ltMethod);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "Point", Symbol = pointType };
        var rightType = new UserDefinedType { Name = "Point", Symbol = pointType };

        var result = validator.ValidateBinaryOp(
            BinaryOperator.LessThan,
            leftType,
            rightType,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateUnaryOp_UserDefinedNegate_ReturnsReturnType()
    {
        var symbolTable = CreateSymbolTable();

        // Create a Number type with __neg__ method
        var numberType = new TypeSymbol
        {
            Name = "Number",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var negMethod = new FunctionSymbol
        {
            Name = "__neg__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Number", Symbol = numberType } }
            },
            ReturnType = new UserDefinedType { Name = "Number", Symbol = numberType }
        };

        numberType.OperatorMethods["__neg__"] = new() { negMethod };
        numberType.Methods.Add(negMethod);

        var validator = CreateValidator(symbolTable);

        var operandType = new UserDefinedType { Name = "Number", Symbol = numberType };

        var result = validator.ValidateUnaryOp(
            UnaryOperator.Minus,
            operandType,
            1, 1);

        result.Should().BeEquivalentTo(new UserDefinedType { Name = "Number", Symbol = numberType });
    }

    [Fact]
    public void ValidateBinaryOp_UserDefinedPower_ReturnsReturnType()
    {
        var symbolTable = CreateSymbolTable();

        // Create a Matrix type with __pow__ method
        var matrixType = new TypeSymbol
        {
            Name = "Matrix",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var powMethod = new FunctionSymbol
        {
            Name = "__pow__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Matrix", Symbol = matrixType } },
                new ParameterSymbol { Name = "exponent", Type = SemanticType.Int }
            },
            ReturnType = new UserDefinedType { Name = "Matrix", Symbol = matrixType }
        };

        matrixType.OperatorMethods["__pow__"] = new() { powMethod };
        matrixType.Methods.Add(powMethod);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "Matrix", Symbol = matrixType };

        var result = validator.ValidateBinaryOp(
            BinaryOperator.Power,
            leftType,
            SemanticType.Int,
            1, 1);

        result.Should().BeEquivalentTo(new UserDefinedType { Name = "Matrix", Symbol = matrixType });
    }

    #endregion

    #region Overload Resolution Tests

    [Fact]
    public void ValidateBinaryOp_MultipleOverloads_ChoosesExactMatch()
    {
        var symbolTable = CreateSymbolTable();

        // Create a Calculator type with multiple __add__ overloads
        var calcType = new TypeSymbol
        {
            Name = "Calculator",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var addIntMethod = new FunctionSymbol
        {
            Name = "__add__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Calculator", Symbol = calcType } },
                new ParameterSymbol { Name = "value", Type = SemanticType.Int }
            },
            ReturnType = new UserDefinedType { Name = "Calculator", Symbol = calcType }
        };

        var addFloatMethod = new FunctionSymbol
        {
            Name = "__add__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Calculator", Symbol = calcType } },
                new ParameterSymbol { Name = "value", Type = SemanticType.Float }
            },
            ReturnType = new UserDefinedType { Name = "Calculator", Symbol = calcType }
        };

        calcType.OperatorMethods["__add__"] = new() { addIntMethod, addFloatMethod };
        calcType.Methods.Add(addIntMethod);
        calcType.Methods.Add(addFloatMethod);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "Calculator", Symbol = calcType };

        // Should choose the int overload
        var resultInt = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            leftType,
            SemanticType.Int,
            1, 1);

        resultInt.Should().BeEquivalentTo(new UserDefinedType { Name = "Calculator", Symbol = calcType });

        // Should choose the float overload
        var resultFloat = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            leftType,
            SemanticType.Float,
            1, 1);

        resultFloat.Should().BeEquivalentTo(new UserDefinedType { Name = "Calculator", Symbol = calcType });
    }

    #endregion

    #region Caching Tests

    [Fact]
    public void ValidateBinaryOp_CachesResults()
    {
        var validator = CreateValidator();

        // Call twice with the same arguments
        var result1 = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        var result2 = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result1.Should().Be(result2);
        result1.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateUnaryOp_CachesResults()
    {
        var validator = CreateValidator();

        // Call twice with the same arguments
        var result1 = validator.ValidateUnaryOp(
            UnaryOperator.Minus,
            SemanticType.Int,
            1, 1);

        var result2 = validator.ValidateUnaryOp(
            UnaryOperator.Minus,
            SemanticType.Int,
            1, 1);

        result1.Should().Be(result2);
        result1.Should().Be(SemanticType.Int);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidateBinaryOp_UnsupportedOperator_ReturnsUnknown()
    {
        var validator = CreateValidator();

        // String doesn't support subtraction
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Subtract,
            SemanticType.Str,
            SemanticType.Str,
            1, 1);

        result.Should().Be(SemanticType.Unknown);
    }

    [Fact]
    public void ValidateUnaryOp_UnsupportedOperator_ReturnsUnknown()
    {
        var validator = CreateValidator();

        // String doesn't support unary minus
        var result = validator.ValidateUnaryOp(
            UnaryOperator.Minus,
            SemanticType.Str,
            1, 1);

        result.Should().Be(SemanticType.Unknown);
    }

    [Fact]
    public void ValidateBinaryOp_BitwiseOnFloat_ReturnsUnknown()
    {
        var validator = CreateValidator();

        // Float doesn't support bitwise operations
        var result = validator.ValidateBinaryOp(
            BinaryOperator.BitwiseAnd,
            SemanticType.Float,
            SemanticType.Float,
            1, 1);

        result.Should().Be(SemanticType.Unknown);
    }

    #endregion

    #region List Operation Tests

    [Fact]
    public void ValidateBinaryOp_ListAddList_ReturnsList()
    {
        var validator = CreateValidator();

        var intList = new GenericType
        {
            Name = "list",
            TypeArguments = new() { SemanticType.Int }
        };

        var result = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            intList,
            intList,
            1, 1);

        result.Should().BeEquivalentTo(intList);
    }

    #endregion

    #region Equality Complement Synthesis Tests

    [Fact]
    public void ValidateBinaryOp_OnlyEqDefined_NotEqualWorks()
    {
        // Test that when only __eq__ is defined, != operator works via complement synthesis
        var symbolTable = CreateSymbolTable();

        var pointType = new TypeSymbol
        {
            Name = "Point",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var eqMethod = new FunctionSymbol
        {
            Name = "__eq__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Point", Symbol = pointType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "Point", Symbol = pointType } }
            },
            ReturnType = SemanticType.Bool
        };

        pointType.OperatorMethods["__eq__"] = new() { eqMethod };
        pointType.Methods.Add(eqMethod);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "Point", Symbol = pointType };
        var rightType = new UserDefinedType { Name = "Point", Symbol = pointType };

        // Test != operator when only __eq__ is defined
        var result = validator.ValidateBinaryOp(
            BinaryOperator.NotEqual,
            leftType,
            rightType,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_OnlyNeDefined_EqualWorks()
    {
        // Test that when only __ne__ is defined, == operator works via complement synthesis
        var symbolTable = CreateSymbolTable();

        var vectorType = new TypeSymbol
        {
            Name = "Vector",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var neMethod = new FunctionSymbol
        {
            Name = "__ne__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } }
            },
            ReturnType = SemanticType.Bool
        };

        vectorType.OperatorMethods["__ne__"] = new() { neMethod };
        vectorType.Methods.Add(neMethod);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "Vector", Symbol = vectorType };
        var rightType = new UserDefinedType { Name = "Vector", Symbol = vectorType };

        // Test == operator when only __ne__ is defined
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Equal,
            leftType,
            rightType,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_BothEqAndNeDefined_UsesDirectImplementation()
    {
        // Test that when both __eq__ and __ne__ are defined, they are used directly (no synthesis)
        var symbolTable = CreateSymbolTable();

        var customType = new TypeSymbol
        {
            Name = "CustomType",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var eqMethod = new FunctionSymbol
        {
            Name = "__eq__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "CustomType", Symbol = customType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "CustomType", Symbol = customType } }
            },
            ReturnType = SemanticType.Bool
        };

        var neMethod = new FunctionSymbol
        {
            Name = "__ne__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "CustomType", Symbol = customType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "CustomType", Symbol = customType } }
            },
            ReturnType = SemanticType.Bool
        };

        customType.OperatorMethods["__eq__"] = new() { eqMethod };
        customType.OperatorMethods["__ne__"] = new() { neMethod };
        customType.Methods.Add(eqMethod);
        customType.Methods.Add(neMethod);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "CustomType", Symbol = customType };
        var rightType = new UserDefinedType { Name = "CustomType", Symbol = customType };

        // Test == operator
        var eqResult = validator.ValidateBinaryOp(
            BinaryOperator.Equal,
            leftType,
            rightType,
            1, 1);

        eqResult.Should().Be(SemanticType.Bool);

        // Test != operator
        var neResult = validator.ValidateBinaryOp(
            BinaryOperator.NotEqual,
            leftType,
            rightType,
            1, 1);

        neResult.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_NeitherEqNorNeDefined_UsesDefaultEquality()
    {
        // Test that when neither __eq__ nor __ne__ is defined, default equality is used
        var symbolTable = CreateSymbolTable();

        var noEqualityType = new TypeSymbol
        {
            Name = "NoEquality",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "NoEquality", Symbol = noEqualityType };
        var rightType = new UserDefinedType { Name = "NoEquality", Symbol = noEqualityType };

        // Test == operator - should use default equality and return bool
        var eqResult = validator.ValidateBinaryOp(
            BinaryOperator.Equal,
            leftType,
            rightType,
            1, 1);

        eqResult.Should().Be(SemanticType.Bool);

        // Test != operator - should also use default equality and return bool
        var neResult = validator.ValidateBinaryOp(
            BinaryOperator.NotEqual,
            leftType,
            rightType,
            1, 1);

        neResult.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void ValidateBinaryOp_ComplementSynthesis_OnlyForEqualityOperators()
    {
        // Test that complement synthesis only applies to == and !=, not other operators
        var symbolTable = CreateSymbolTable();

        var typeWithOnlyEq = new TypeSymbol
        {
            Name = "TypeWithOnlyEq",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var eqMethod = new FunctionSymbol
        {
            Name = "__eq__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "TypeWithOnlyEq", Symbol = typeWithOnlyEq } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "TypeWithOnlyEq", Symbol = typeWithOnlyEq } }
            },
            ReturnType = SemanticType.Bool
        };

        typeWithOnlyEq.OperatorMethods["__eq__"] = new() { eqMethod };
        typeWithOnlyEq.Methods.Add(eqMethod);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "TypeWithOnlyEq", Symbol = typeWithOnlyEq };
        var rightType = new UserDefinedType { Name = "TypeWithOnlyEq", Symbol = typeWithOnlyEq };

        // Test that < operator doesn't benefit from complement synthesis
        var ltResult = validator.ValidateBinaryOp(
            BinaryOperator.LessThan,
            leftType,
            rightType,
            1, 1);

        ltResult.Should().Be(SemanticType.Unknown);
    }

    [Fact]
    public void ValidateBinaryOp_OnlyEqDefined_WithDifferentArgumentType()
    {
        // Test complement synthesis with different argument types (inheritance/assignability)
        var symbolTable = CreateSymbolTable();

        var baseType = new TypeSymbol
        {
            Name = "Base",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var derivedType = new TypeSymbol
        {
            Name = "Derived",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            BaseType = baseType
        };

        var eqMethod = new FunctionSymbol
        {
            Name = "__eq__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Base", Symbol = baseType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "Base", Symbol = baseType } }
            },
            ReturnType = SemanticType.Bool
        };

        baseType.OperatorMethods["__eq__"] = new() { eqMethod };
        baseType.Methods.Add(eqMethod);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "Base", Symbol = baseType };
        var rightType = new UserDefinedType { Name = "Derived", Symbol = derivedType };

        // Test != with derived type when only __eq__ is defined
        var result = validator.ValidateBinaryOp(
            BinaryOperator.NotEqual,
            leftType,
            rightType,
            1, 1);

        result.Should().Be(SemanticType.Bool);
    }

    #endregion
}
