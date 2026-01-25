// Testing deprecated OperatorValidator API - these tests ensure backward compatibility
#pragma warning disable CS0618

using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using static Sharpy.Compiler.Tests.TestHelpers;

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
    public void ValidateBinaryOp_IntAddFloat_ReturnsDouble()
    {
        // Per spec: Sharpy 'float' maps to C# double. int + double = double.
        // The result semantic type is 'double' (the canonical name for typeof(double)).
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            SemanticType.Int,
            SemanticType.Float,
            1, 1);

        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void ValidateBinaryOp_FloatAddDouble_ReturnsDouble()
    {
        // Per spec: Both 'float' and 'double' map to C# double.
        // The canonical name for typeof(double) is 'double' in PrimitiveCatalog.
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
    public void ValidateBinaryOp_IntDivideInt_ReturnsDouble()
    {
        // Python semantics: division always returns float (double)
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Divide,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Double);
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
    public void ValidateBinaryOp_IntPowerInt_ReturnsDouble()
    {
        // Python semantics: power always returns float (double) due to Math.Pow
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Power,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void ValidateBinaryOp_FloatPowerFloat_ReturnsDouble()
    {
        // Per spec: Sharpy 'float' maps to C# double. double ** double = double.
        // The canonical name for typeof(double) is 'double' in PrimitiveCatalog.
        var validator = CreateValidator();
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Power,
            SemanticType.Float,
            SemanticType.Float,
            1, 1);

        result.Should().Be(SemanticType.Double);
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

    [Fact]
    public void ValidateBinaryOp_AmbiguousOverloads_ReportsError()
    {
        var symbolTable = CreateSymbolTable();

        // Create a base type that both parameter types will "derive" from
        var baseType = new TypeSymbol
        {
            Name = "BaseValue",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        // Create two unrelated types that don't have a clear specificity relationship
        var typeA = new TypeSymbol
        {
            Name = "ValueA",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var typeB = new TypeSymbol
        {
            Name = "ValueB",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        // Create a Calculator type with ambiguous __add__ overloads
        var calcType = new TypeSymbol
        {
            Name = "Calculator",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        // Create overloads that accept BaseValue - since both ValueA and ValueB would be
        // "assignable" to BaseValue conceptually, and neither is more specific than the other
        var addBaseMethod1 = new FunctionSymbol
        {
            Name = "__add__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Calculator", Symbol = calcType } },
                new ParameterSymbol { Name = "value", Type = new UserDefinedType { Name = "ValueA", Symbol = typeA } }
            },
            ReturnType = new UserDefinedType { Name = "Calculator", Symbol = calcType }
        };

        var addBaseMethod2 = new FunctionSymbol
        {
            Name = "__add__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Calculator", Symbol = calcType } },
                new ParameterSymbol { Name = "value", Type = new UserDefinedType { Name = "ValueB", Symbol = typeB } }
            },
            ReturnType = new UserDefinedType { Name = "Calculator", Symbol = calcType }
        };

        calcType.OperatorMethods["__add__"] = new() { addBaseMethod1, addBaseMethod2 };
        calcType.Methods.Add(addBaseMethod1);
        calcType.Methods.Add(addBaseMethod2);

        var validator = CreateValidator(symbolTable);

        var leftType = new UserDefinedType { Name = "Calculator", Symbol = calcType };

        // Create an argument type that is assignable to both ValueA and ValueB (simulated)
        // We'll use a type with custom IsAssignableTo that returns true for both
        var ambiguousArgType = new TestAssignableType("AmbiguousArg", new[] { "ValueA", "ValueB" });

        // When called with ambiguous type, should report an error
        var result = validator.ValidateBinaryOp(
            BinaryOperator.Add,
            leftType,
            ambiguousArgType,
            5, 10);

        // Result should still work (returns first match) but error should be recorded
        result.Should().BeEquivalentTo(new UserDefinedType { Name = "Calculator", Symbol = calcType });

        // Verify error was reported
        validator.Errors.Should().ContainSingle();
        validator.Errors[0].Message.Should().Contain("Ambiguous");
        validator.Errors[0].Message.Should().Contain("+");
        validator.Errors[0].Message.Should().Contain("Calculator");
        validator.Errors[0].Message.Should().Contain("AmbiguousArg");
        validator.Errors[0].Line.Should().Be(5);
        validator.Errors[0].Column.Should().Be(10);
    }

    /// <summary>
    /// A test semantic type that claims to be assignable to specific named types.
    /// Used to simulate ambiguous overload scenarios.
    /// </summary>
    private record TestAssignableType : SemanticType
    {
        private readonly string _name;
        private readonly HashSet<string> _assignableToTypes;

        public TestAssignableType(string name, IEnumerable<string> assignableToTypes)
        {
            _name = name;
            _assignableToTypes = new HashSet<string>(assignableToTypes);
        }

        public override string GetDisplayName() => _name;

        public override bool IsAssignableTo(SemanticType target)
        {
            if (target is UserDefinedType udt)
            {
                return _assignableToTypes.Contains(udt.Name);
            }
            return base.IsAssignableTo(target);
        }
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

    #region Augmented Assignment Tests

    [Fact]
    public void ValidateAugmentedAssignment_OnlyInPlaceDefined_UsesInPlace()
    {
        // Test that when only __iadd__ is defined, += uses it
        var symbolTable = CreateSymbolTable();

        var vectorType = new TypeSymbol
        {
            Name = "Vector",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var iaddMethod = new FunctionSymbol
        {
            Name = "__iadd__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } }
            },
            ReturnType = new UserDefinedType { Name = "Vector", Symbol = vectorType }
        };

        vectorType.OperatorMethods["__iadd__"] = new() { iaddMethod };
        vectorType.Methods.Add(iaddMethod);

        var validator = CreateValidator(symbolTable);

        var targetType = new UserDefinedType { Name = "Vector", Symbol = vectorType };
        var valueType = new UserDefinedType { Name = "Vector", Symbol = vectorType };

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PlusAssign,
            targetType,
            valueType,
            1, 1);

        result.Should().Be(targetType);
    }

    [Fact]
    public void ValidateAugmentedAssignment_OnlyBinaryDefined_FallsBackToBinary()
    {
        // Test that when only __add__ is defined, += falls back to it
        var symbolTable = CreateSymbolTable();

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

        var targetType = new UserDefinedType { Name = "Vector", Symbol = vectorType };
        var valueType = new UserDefinedType { Name = "Vector", Symbol = vectorType };

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PlusAssign,
            targetType,
            valueType,
            1, 1);

        result.Should().Be(targetType);
    }

    [Fact]
    public void ValidateAugmentedAssignment_BothDefined_PrefersInPlace()
    {
        // Test that when both __iadd__ and __add__ are defined, __iadd__ takes precedence
        var symbolTable = CreateSymbolTable();

        var vectorType = new TypeSymbol
        {
            Name = "Vector",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var iaddMethod = new FunctionSymbol
        {
            Name = "__iadd__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } }
            },
            ReturnType = new UserDefinedType { Name = "Vector", Symbol = vectorType }
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

        vectorType.OperatorMethods["__iadd__"] = new() { iaddMethod };
        vectorType.OperatorMethods["__add__"] = new() { addMethod };
        vectorType.Methods.Add(iaddMethod);
        vectorType.Methods.Add(addMethod);

        var validator = CreateValidator(symbolTable);

        var targetType = new UserDefinedType { Name = "Vector", Symbol = vectorType };
        var valueType = new UserDefinedType { Name = "Vector", Symbol = vectorType };

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PlusAssign,
            targetType,
            valueType,
            1, 1);

        // Should succeed and return Vector type (from __iadd__)
        result.Should().Be(targetType);
    }

    [Fact]
    public void ValidateAugmentedAssignment_NeitherDefined_ReportsError()
    {
        // Test that when neither __iadd__ nor __add__ is defined, it reports an error
        var symbolTable = CreateSymbolTable();
        var logger = new CollectingTestLogger();

        var vectorType = new TypeSymbol
        {
            Name = "Vector",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var validator = new OperatorValidator(symbolTable, logger);

        var targetType = new UserDefinedType { Name = "Vector", Symbol = vectorType };
        var valueType = new UserDefinedType { Name = "Vector", Symbol = vectorType };

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PlusAssign,
            targetType,
            valueType,
            1, 1);

        result.Should().Be(SemanticType.Unknown);
        logger.Errors.Should().ContainSingle();
        logger.Errors[0].Message.Should().Contain("does not support augmented assignment operator '+='");
    }

    [Fact]
    public void ValidateAugmentedAssignment_ResultNotAssignableToTarget_ReportsError()
    {
        // Test that when the result type is not assignable to target, it reports an error
        var symbolTable = CreateSymbolTable();
        var logger = new CollectingTestLogger();

        var vectorType = new TypeSymbol
        {
            Name = "Vector",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var matrixType = new TypeSymbol
        {
            Name = "Matrix",
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
            // Returns Matrix instead of Vector
            ReturnType = new UserDefinedType { Name = "Matrix", Symbol = matrixType }
        };

        vectorType.OperatorMethods["__add__"] = new() { addMethod };
        vectorType.Methods.Add(addMethod);

        var validator = new OperatorValidator(symbolTable, logger);

        var targetType = new UserDefinedType { Name = "Vector", Symbol = vectorType };
        var valueType = new UserDefinedType { Name = "Vector", Symbol = vectorType };

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PlusAssign,
            targetType,
            valueType,
            1, 1);

        result.Should().Be(SemanticType.Unknown);
        logger.Errors.Should().ContainSingle();
        logger.Errors[0].Message.Should().Contain("is not assignable to target type");
    }

    [Fact]
    public void ValidateAugmentedAssignment_BuiltinInt_WorksCorrectly()
    {
        // Test augmented assignment with builtin int type
        var validator = CreateValidator();

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PlusAssign,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateAugmentedAssignment_BuiltinString_WorksCorrectly()
    {
        // Test augmented assignment with builtin string type (concatenation)
        var validator = CreateValidator();

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PlusAssign,
            SemanticType.Str,
            SemanticType.Str,
            1, 1);

        result.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void ValidateAugmentedAssignment_BuiltinList_WorksCorrectly()
    {
        // Test augmented assignment with builtin list type
        var validator = CreateValidator();

        var listType = new GenericType { Name = "list", TypeArguments = new() { SemanticType.Int } };

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PlusAssign,
            listType,
            listType,
            1, 1);

        result.Should().Be(listType);
    }

    [Fact]
    public void ValidateAugmentedAssignment_MinusAssign_WorksCorrectly()
    {
        // Test -= operator
        var symbolTable = CreateSymbolTable();

        var vectorType = new TypeSymbol
        {
            Name = "Vector",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class
        };

        var isubMethod = new FunctionSymbol
        {
            Name = "__isub__",
            Kind = SymbolKind.Function,
            Parameters = new()
            {
                new ParameterSymbol { Name = "self", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } },
                new ParameterSymbol { Name = "other", Type = new UserDefinedType { Name = "Vector", Symbol = vectorType } }
            },
            ReturnType = new UserDefinedType { Name = "Vector", Symbol = vectorType }
        };

        vectorType.OperatorMethods["__isub__"] = new() { isubMethod };
        vectorType.Methods.Add(isubMethod);

        var validator = CreateValidator(symbolTable);

        var targetType = new UserDefinedType { Name = "Vector", Symbol = vectorType };
        var valueType = new UserDefinedType { Name = "Vector", Symbol = vectorType };

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.MinusAssign,
            targetType,
            valueType,
            1, 1);

        result.Should().Be(targetType);
    }

    [Fact]
    public void ValidateAugmentedAssignment_PowerAssign_WorksCorrectly()
    {
        // Test **= operator with double target type
        // Python semantics: power always returns float (double) due to Math.Pow
        // Note: Using int target would fail because double is not assignable to int
        var validator = CreateValidator();

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PowerAssign,
            SemanticType.Double,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void ValidateAugmentedAssignment_BitwiseAndAssign_WorksCorrectly()
    {
        // Test &= operator with builtin integer types
        var validator = CreateValidator();

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.AndAssign,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateAugmentedAssignment_FloorDivAssign_WorksCorrectly()
    {
        // Test //= operator with builtin numeric types
        var validator = CreateValidator();

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.DoubleSlashAssign,
            SemanticType.Int,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void ValidateAugmentedAssignment_SimpleAssign_ReturnsValueType()
    {
        // Test that simple assignment (=) just returns the value type
        var validator = CreateValidator();

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.Assign,
            SemanticType.Int,
            SemanticType.Float,
            1, 1);

        // Simple assignment should just return the value type
        result.Should().Be(SemanticType.Float);
    }

    [Fact]
    public void ValidateAugmentedAssignment_NumericPromotion_WorksCorrectly()
    {
        // Test that numeric promotion works correctly (float += int -> double)
        // Per spec: Sharpy 'float' maps to C# double. float + int = double.
        var validator = CreateValidator();

        var result = validator.ValidateAugmentedAssignment(
            AssignmentOperator.PlusAssign,
            SemanticType.Float,
            SemanticType.Int,
            1, 1);

        result.Should().Be(SemanticType.Double);
    }

    #endregion
}
