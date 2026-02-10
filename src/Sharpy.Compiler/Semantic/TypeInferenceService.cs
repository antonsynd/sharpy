using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Service for inferring result types from operations.
/// This service focuses purely on type inference - it does NOT validate or report errors.
///
/// The service extracts type inference logic that was previously embedded in validators.
/// Validators should use this service for type inference and handle error reporting separately.
/// </summary>
/// <remarks>
/// Design notes:
/// - All methods return nullable types (null means "cannot infer")
/// - Methods do NOT report errors (validation responsibility is separate)
/// - Results are cached for performance (operator results are highly repetitive)
/// - Thread-safe caching could be added in future if needed
/// </remarks>
internal class TypeInferenceService
{
    private readonly SymbolTable _symbolTable;
    private readonly ClrMemberCache _clrMemberCache;

    // Caches for performance (not thread-safe)
    private readonly Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache = new();
    private readonly Dictionary<(UnaryOperator, SemanticType), SemanticType?> _unaryOpCache = new();

    public TypeInferenceService(SymbolTable symbolTable, ClrMemberCache? clrMemberCache = null)
    {
        _symbolTable = symbolTable;
        _clrMemberCache = clrMemberCache ?? new ClrMemberCache();
    }

    #region Binary Operations

    /// <summary>
    /// Infers the result type of a binary operation.
    /// Returns null if the operation is not supported for the given types.
    /// </summary>
    public SemanticType? InferBinaryOpType(BinaryOperator op, SemanticType left, SemanticType right)
    {
        // Check cache first
        var cacheKey = (left, op, right);
        if (_binaryOpCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var result = InferBinaryOpTypeUncached(op, left, right);
        _binaryOpCache[cacheKey] = result;
        return result;
    }

    private SemanticType? InferBinaryOpTypeUncached(BinaryOperator op, SemanticType left, SemanticType right)
    {
        // Handle special operators that don't use dunder methods
        switch (op)
        {
            case BinaryOperator.And:
            case BinaryOperator.Or:
            case BinaryOperator.Is:
            case BinaryOperator.IsNot:
                return SemanticType.Bool;

            case BinaryOperator.In:
            case BinaryOperator.NotIn:
                // Membership test returns bool (validation is separate)
                return SemanticType.Bool;

            case BinaryOperator.NullCoalesce:
                return InferNullCoalesceType(left, right);
        }

        // Try builtin types first
        var builtinResult = TryInferBuiltinBinaryOp(op, left, right);
        if (builtinResult != null)
            return builtinResult;

        // Try type parameter constraints (e.g., T: IComparable allows comparison)
        var typeParamResult = TryInferTypeParameterBinaryOp(op, left, right);
        if (typeParamResult != null)
            return typeParamResult;

        // Try user-defined types
        var userResult = TryInferUserDefinedBinaryOp(op, left, right);
        if (userResult != null)
            return userResult;

        // Try CLR operators
        var clrResult = TryInferClrBinaryOp(op, left, right);
        if (clrResult != null)
            return clrResult;

        return null;
    }

    private SemanticType? InferNullCoalesceType(SemanticType left, SemanticType right)
    {
        if (left is NullableType nullableLeft)
        {
            var leftNonNullable = nullableLeft.UnderlyingType;
            if (right.IsAssignableTo(leftNonNullable))
            {
                // If right is nullable, result is nullable, otherwise non-nullable
                return right is NullableType ? left : leftNonNullable;
            }
            // Right may also be nullable/optional with compatible underlying type
            if (right is NullableType nullableRight && nullableRight.UnderlyingType.IsAssignableTo(leftNonNullable))
                return left;
            if (right is OptionalType optionalRight && optionalRight.UnderlyingType.IsAssignableTo(leftNonNullable))
                return left;
            return null; // Invalid - right not assignable
        }

        if (left is OptionalType optionalLeft)
        {
            var leftNonOptional = optionalLeft.UnderlyingType;
            if (right.IsAssignableTo(leftNonOptional))
            {
                return right is NullableType or OptionalType ? left : leftNonOptional;
            }
            // Right may also be nullable/optional with compatible underlying type
            if (right is NullableType nullableRight2 && nullableRight2.UnderlyingType.IsAssignableTo(leftNonOptional))
                return left;
            if (right is OptionalType optionalRight2 && optionalRight2.UnderlyingType.IsAssignableTo(leftNonOptional))
                return left;
            return null; // Invalid - right not assignable
        }

        return null; // Invalid - left must be nullable/optional
    }

    private SemanticType? TryInferBuiltinBinaryOp(BinaryOperator op, SemanticType left, SemanticType right)
    {
        // Integer types for bitwise operations
        if (IsIntegerType(left) && IsIntegerType(right))
        {
            var bitwiseResult = op switch
            {
                BinaryOperator.BitwiseAnd or
                BinaryOperator.BitwiseOr or
                BinaryOperator.BitwiseXor or
                BinaryOperator.LeftShift or
                BinaryOperator.RightShift => InferNumericResultType(left, right),
                _ => (SemanticType?)null
            };

            if (bitwiseResult != null)
                return bitwiseResult;
        }

        // Numeric types (includes integers for arithmetic operations)
        if (IsNumericType(left) && IsNumericType(right))
        {
            return op switch
            {
                BinaryOperator.Add or
                BinaryOperator.Subtract or
                BinaryOperator.Multiply or
                BinaryOperator.FloorDivide or
                BinaryOperator.Modulo => InferNumericResultType(left, right),

                // Division always returns float64 (Python semantics)
                BinaryOperator.Divide => SemanticType.Double,

                // Power: integer ** integer => Long, any float => Double
                BinaryOperator.Power => InferPowerResultType(left, right),

                BinaryOperator.Equal or
                BinaryOperator.NotEqual or
                BinaryOperator.LessThan or
                BinaryOperator.LessThanOrEqual or
                BinaryOperator.GreaterThan or
                BinaryOperator.GreaterThanOrEqual => SemanticType.Bool,

                _ => null
            };
        }

        // String operations
        if (left == SemanticType.Str && right == SemanticType.Str)
        {
            return op switch
            {
                BinaryOperator.Add => SemanticType.Str,
                BinaryOperator.Equal or BinaryOperator.NotEqual or
                BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual or
                BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual => SemanticType.Bool,
                _ => null
            };
        }

        // String repetition
        if (left == SemanticType.Str && right == SemanticType.Int)
        {
            if (op == BinaryOperator.Multiply)
                return SemanticType.Str;
        }

        // List concatenation
        if (left is GenericType { Name: "list" } leftList &&
            right is GenericType { Name: "list" } rightList)
        {
            if (op == BinaryOperator.Add)
            {
                return InferListConcatType(leftList, rightList);
            }
            if (op == BinaryOperator.Equal || op == BinaryOperator.NotEqual)
            {
                return SemanticType.Bool;
            }
        }

        // Equality for identical types
        if ((op == BinaryOperator.Equal || op == BinaryOperator.NotEqual) && left.Equals(right))
        {
            return SemanticType.Bool;
        }

        return null;
    }

    private SemanticType? InferListConcatType(GenericType leftList, GenericType rightList)
    {
        if (leftList.TypeArguments.Count > 0 && rightList.TypeArguments.Count > 0)
        {
            var leftElem = leftList.TypeArguments[0];
            var rightElem = rightList.TypeArguments[0];

            if (leftElem.Equals(rightElem))
                return leftList;
            return null; // Element types don't match
        }

        if (leftList.TypeArguments.Count == 0 && rightList.TypeArguments.Count == 0)
            return new GenericType { Name = "list" };

        if (leftList.TypeArguments.Count == 0)
            return rightList;

        return leftList;
    }

    private SemanticType? TryInferTypeParameterBinaryOp(BinaryOperator op, SemanticType left, SemanticType right)
    {
        // Check if either operand is a type parameter
        var typeParam = left as TypeParameterType ?? right as TypeParameterType;
        if (typeParam == null)
            return null;

        // Equality operators are always allowed (all .NET types support equality)
        if (op == BinaryOperator.Equal || op == BinaryOperator.NotEqual)
            return SemanticType.Bool;

        // Comparison operators require IComparable constraint
        if (op is BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
            or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual)
        {
            if (HasComparableConstraint(typeParam.Constraints))
                return SemanticType.Bool;
        }

        return null;
    }

    private static bool HasComparableConstraint(ImmutableArray<ConstraintClause> constraints)
    {
        foreach (var constraint in constraints)
        {
            if (constraint is TypeConstraint typeConstraint)
            {
                // Check if the constraint type name contains "Comparable"
                // This covers IComparable, IComparable[T], System.IComparable, etc.
                var typeName = typeConstraint.Type.Name;
                if (typeName.Contains("Comparable"))
                    return true;
            }
        }
        return false;
    }

    private SemanticType? TryInferUserDefinedBinaryOp(BinaryOperator op, SemanticType left, SemanticType right)
    {
        var dunderName = BinaryOperatorToDunder(op);
        if (dunderName == null)
            return null;

        if (left is UserDefinedType udt && udt.Symbol != null)
        {
            // Try direct operator
            if (udt.Symbol.OperatorMethods.TryGetValue(dunderName, out var methods))
            {
                var bestOverload = FindBestOverload(methods, right);
                if (bestOverload != null)
                    return bestOverload.ReturnType;
            }

            // Try equality complement synthesis
            var complementResult = TryInferEqualityComplement(op, udt, right);
            if (complementResult != null)
                return complementResult;
        }

        return null;
    }

    private SemanticType? TryInferEqualityComplement(BinaryOperator op, UserDefinedType udt, SemanticType right)
    {
        if (udt.Symbol == null)
            return null;

        bool hasEq = udt.Symbol.OperatorMethods.ContainsKey(DunderNames.Eq);
        bool hasNe = udt.Symbol.OperatorMethods.ContainsKey(DunderNames.Ne);

        if (op == BinaryOperator.Equal && hasNe && !hasEq)
        {
            var neMethods = udt.Symbol.OperatorMethods[DunderNames.Ne];
            var bestOverload = FindBestOverload(neMethods, right);
            if (bestOverload != null)
                return SemanticType.Bool;
        }
        else if (op == BinaryOperator.NotEqual && hasEq && !hasNe)
        {
            var eqMethods = udt.Symbol.OperatorMethods[DunderNames.Eq];
            var bestOverload = FindBestOverload(eqMethods, right);
            if (bestOverload != null)
                return SemanticType.Bool;
        }

        return null;
    }

    private FunctionSymbol? FindBestOverload(List<FunctionSymbol> candidates, SemanticType argumentType)
    {
        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        // Find exact match first
        var exactMatch = candidates.FirstOrDefault(c =>
            c.Parameters.Count == 2 &&
            c.Parameters[1].Type.Equals(argumentType));

        if (exactMatch != null)
            return exactMatch;

        // Find first assignable match (simplified - full resolution is in validator)
        var assignableMatch = candidates.FirstOrDefault(c =>
            c.Parameters.Count == 2 &&
            argumentType.IsAssignableTo(c.Parameters[1].Type));

        return assignableMatch;
    }

    private SemanticType? TryInferClrBinaryOp(BinaryOperator op, SemanticType left, SemanticType right)
    {
        var clrMethodName = BinaryOperatorToClrMethod(op);
        if (clrMethodName == null)
            return null;

        var leftClrType = GetClrType(left);
        var rightClrType = GetClrType(right);
        if (leftClrType == null || rightClrType == null)
            return null;

        var operators = _clrMemberCache.GetOperatorMethods(leftClrType);
        if (operators.TryGetValue(clrMethodName, out var operatorMethods))
        {
            foreach (var method in operatorMethods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == leftClrType &&
                    parameters[1].ParameterType == rightClrType)
                {
                    return MapClrTypeToSemanticType(method.ReturnType);
                }
            }
        }

        return null;
    }

    #endregion

    #region Unary Operations

    /// <summary>
    /// Infers the result type of a unary operation.
    /// Returns null if the operation is not supported for the given type.
    /// </summary>
    public SemanticType? InferUnaryOpType(UnaryOperator op, SemanticType operand)
    {
        // Check cache first
        var cacheKey = (op, operand);
        if (_unaryOpCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var result = InferUnaryOpTypeUncached(op, operand);
        _unaryOpCache[cacheKey] = result;
        return result;
    }

    private SemanticType? InferUnaryOpTypeUncached(UnaryOperator op, SemanticType operand)
    {
        // 'not' always returns bool
        if (op == UnaryOperator.Not)
            return SemanticType.Bool;

        // Try builtin types
        var builtinResult = TryInferBuiltinUnaryOp(op, operand);
        if (builtinResult != null)
            return builtinResult;

        // Try user-defined types
        var userResult = TryInferUserDefinedUnaryOp(op, operand);
        if (userResult != null)
            return userResult;

        // Try CLR operators
        var clrResult = TryInferClrUnaryOp(op, operand);
        if (clrResult != null)
            return clrResult;

        return null;
    }

    private SemanticType? TryInferBuiltinUnaryOp(UnaryOperator op, SemanticType operand)
    {
        // Bitwise not on integers
        if (IsIntegerType(operand) && op == UnaryOperator.BitwiseNot)
            return operand;

        // Numeric unary operators
        if (IsNumericType(operand))
        {
            return op switch
            {
                UnaryOperator.Plus or UnaryOperator.Minus => operand,
                _ => null
            };
        }

        return null;
    }

    private SemanticType? TryInferUserDefinedUnaryOp(UnaryOperator op, SemanticType operand)
    {
        var dunderName = UnaryOperatorToDunder(op);
        if (dunderName == null)
            return null;

        if (operand is UserDefinedType udt && udt.Symbol != null &&
            udt.Symbol.OperatorMethods.TryGetValue(dunderName, out var methods))
        {
            var method = methods.FirstOrDefault();
            if (method != null)
                return method.ReturnType;
        }

        return null;
    }

    private SemanticType? TryInferClrUnaryOp(UnaryOperator op, SemanticType operand)
    {
        var clrMethodName = UnaryOperatorToClrMethod(op);
        if (clrMethodName == null)
            return null;

        var clrType = GetClrType(operand);
        if (clrType == null)
            return null;

        var operators = _clrMemberCache.GetOperatorMethods(clrType);
        if (operators.TryGetValue(clrMethodName, out var operatorMethods))
        {
            foreach (var method in operatorMethods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == clrType)
                {
                    return MapClrTypeToSemanticType(method.ReturnType);
                }
            }
        }

        return null;
    }

    #endregion

    #region Augmented Assignment Inference

    /// <summary>
    /// Infers the result type of an augmented assignment operation (+=, -=, *=, etc.).
    /// Returns null if the operation is not supported for the given types.
    /// </summary>
    /// <remarks>
    /// Augmented assignment desugars to the regular binary operator (e.g., += uses __add__).
    /// In-place operators do not exist in Sharpy.
    /// </remarks>
    public SemanticType? InferAugmentedAssignmentType(
        AssignmentOperator op,
        SemanticType targetType,
        SemanticType valueType)
    {
        // Simple assignment doesn't need type inference
        if (op == AssignmentOperator.Assign)
        {
            return valueType;
        }

        // Special case for ??=: result type is the target type (nullable)
        if (op == AssignmentOperator.NullCoalesceAssign)
        {
            return InferNullCoalesceType(targetType, valueType) != null ? targetType : null;
        }

        // Use regular binary operator (e.g., __add__ for +=)
        // In-place operators don't exist in Sharpy; augmented assignment desugars to x = x op y
        var binaryOp = AssignmentOperatorToBinaryOperator(op);
        if (binaryOp != null)
        {
            return InferBinaryOpType(binaryOp.Value, targetType, valueType);
        }

        return null;
    }

    private static BinaryOperator? AssignmentOperatorToBinaryOperator(AssignmentOperator op)
    {
        return op switch
        {
            AssignmentOperator.PlusAssign => BinaryOperator.Add,
            AssignmentOperator.MinusAssign => BinaryOperator.Subtract,
            AssignmentOperator.StarAssign => BinaryOperator.Multiply,
            AssignmentOperator.SlashAssign => BinaryOperator.Divide,
            AssignmentOperator.DoubleSlashAssign => BinaryOperator.FloorDivide,
            AssignmentOperator.PercentAssign => BinaryOperator.Modulo,
            AssignmentOperator.PowerAssign => BinaryOperator.Power,
            AssignmentOperator.AndAssign => BinaryOperator.BitwiseAnd,
            AssignmentOperator.OrAssign => BinaryOperator.BitwiseOr,
            AssignmentOperator.XorAssign => BinaryOperator.BitwiseXor,
            AssignmentOperator.LeftShiftAssign => BinaryOperator.LeftShift,
            AssignmentOperator.RightShiftAssign => BinaryOperator.RightShift,
            AssignmentOperator.NullCoalesceAssign => BinaryOperator.NullCoalesce,
            _ => null
        };
    }

    #endregion

    #region Protocol Type Inference

    /// <summary>
    /// Infers the element type when iterating over a container.
    /// Returns null if the type is not iterable.
    /// </summary>
    public SemanticType? InferIterableElementType(SemanticType iterableType)
    {
        // Generic containers
        if (iterableType is GenericType generic && generic.TypeArguments.Count > 0)
        {
            // For dict, iteration yields keys (first type argument)
            return generic.TypeArguments[0];
        }

        // Tuples
        if (iterableType is TupleType tuple && tuple.ElementTypes.Count > 0)
        {
            return tuple.ElementTypes[0];
        }

        // Strings
        if (iterableType == SemanticType.Str)
        {
            return SemanticType.Str;
        }

        // CLR Iterator<T> types
        if (iterableType is BuiltinType builtin && builtin.ClrType != null)
        {
            var elementType = GetIteratorElementType(builtin.ClrType);
            if (elementType != null)
                return MapClrTypeToSemanticType(elementType);
        }

        return null;
    }

    /// <summary>
    /// Infers the result type of an index access operation.
    /// Returns null if the type is not indexable.
    /// </summary>
    public SemanticType? InferIndexAccessType(SemanticType container, SemanticType index)
    {
        // Generic containers
        if (container is GenericType generic)
        {
            // For dict, indexing returns value type (second argument)
            if (generic.Name == "dict" && generic.TypeArguments.Count > 1)
                return generic.TypeArguments[1];

            // For list/tuple, return element type (first argument)
            if (generic.TypeArguments.Count > 0)
                return generic.TypeArguments[0];
        }

        // Tuples
        if (container is TupleType tuple && tuple.ElementTypes.Count > 0)
        {
            return tuple.ElementTypes[0];
        }

        // Strings
        if (container == SemanticType.Str)
        {
            return SemanticType.Str;
        }

        return null;
    }

    /// <summary>
    /// Infers the result type of a membership test (in/not in).
    /// Always returns Bool if valid, null if not.
    /// </summary>
    public SemanticType? InferMembershipType(SemanticType container, SemanticType element)
    {
        // Membership test always returns bool (validation is separate)
        return SemanticType.Bool;
    }

    /// <summary>
    /// Infers the result type of len() call.
    /// Always returns Int if the type supports len.
    /// </summary>
    public SemanticType? InferLenType(SemanticType target)
    {
        // len() always returns int
        return SemanticType.Int;
    }

    #endregion

    #region Helper Methods

    private static bool IsNumericType(SemanticType type)
    {
        return type == SemanticType.Int ||
               type == SemanticType.Long ||
               type == SemanticType.Float ||
               type == SemanticType.Float32 ||
               type == SemanticType.Double;
    }

    private static bool IsIntegerType(SemanticType type)
    {
        return type == SemanticType.Int || type == SemanticType.Long;
    }

    private static SemanticType InferPowerResultType(SemanticType left, SemanticType right)
    {
        // Power type promotion:
        // - Both integer types → use numeric promotion (int**int→int, int**long→long, etc.)
        //   Math.Pow returns double, but we cast back to the promoted integer type
        // - Any float involvement → Double
        if (IsIntegerType(left) && IsIntegerType(right))
            return InferNumericResultType(left, right);
        return SemanticType.Double;
    }

    private static SemanticType InferNumericResultType(SemanticType left, SemanticType right)
    {
        // Type promotion rules:
        // - double > float32 > long > int
        // - Mixed integer/float produces float
        if (left == SemanticType.Double || right == SemanticType.Double)
            return SemanticType.Double;
        if (left == SemanticType.Float || right == SemanticType.Float)
            return SemanticType.Double; // Sharpy float maps to double
        if (left == SemanticType.Float32 || right == SemanticType.Float32)
            return SemanticType.Float32;
        if (left == SemanticType.Long || right == SemanticType.Long)
            return SemanticType.Long;
        return SemanticType.Int;
    }

    private Type? GetClrType(SemanticType type)
    {
        return type switch
        {
            BuiltinType builtin => builtin.ClrType,
            UserDefinedType udt => udt.Symbol?.ClrType,
            GenericType generic => generic.GenericDefinition?.ClrType,
            _ => null
        };
    }

    private Type? GetIteratorElementType(Type clrType)
    {
        var currentType = clrType;
        while (currentType != null)
        {
            if (currentType.IsGenericType &&
                currentType.GetGenericTypeDefinition().FullName == "Sharpy.Iterator`1")
            {
                return currentType.GetGenericArguments()[0];
            }
            currentType = currentType.BaseType;
        }
        return null;
    }

    private SemanticType MapClrTypeToSemanticType(Type clrType)
    {
        if (clrType == typeof(int))
            return SemanticType.Int;
        if (clrType == typeof(long))
            return SemanticType.Long;
        if (clrType == typeof(float))
            return SemanticType.Float32;
        if (clrType == typeof(double))
            return SemanticType.Double;
        if (clrType == typeof(bool))
            return SemanticType.Bool;
        if (clrType == typeof(string))
            return SemanticType.Str;
        return SemanticType.Object;
    }

    private static string? BinaryOperatorToDunder(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => DunderNames.Add,
            BinaryOperator.Subtract => DunderNames.Sub,
            BinaryOperator.Multiply => DunderNames.Mul,
            BinaryOperator.Divide => DunderNames.Div,
            BinaryOperator.Modulo => DunderNames.Mod,
            BinaryOperator.BitwiseAnd => DunderNames.And,
            BinaryOperator.BitwiseOr => DunderNames.Or,
            BinaryOperator.BitwiseXor => DunderNames.Xor,
            BinaryOperator.LeftShift => DunderNames.LShift,
            BinaryOperator.RightShift => DunderNames.RShift,
            BinaryOperator.Equal => DunderNames.Eq,
            BinaryOperator.NotEqual => DunderNames.Ne,
            BinaryOperator.LessThan => DunderNames.Lt,
            BinaryOperator.LessThanOrEqual => DunderNames.Le,
            BinaryOperator.GreaterThan => DunderNames.Gt,
            BinaryOperator.GreaterThanOrEqual => DunderNames.Ge,
            _ => null
        };
    }

    private static string? UnaryOperatorToDunder(UnaryOperator op)
    {
        return op switch
        {
            UnaryOperator.Plus => DunderNames.Pos,
            UnaryOperator.Minus => DunderNames.Neg,
            UnaryOperator.BitwiseNot => DunderNames.Invert,
            _ => null
        };
    }

    private static string? BinaryOperatorToClrMethod(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => "op_Addition",
            BinaryOperator.Subtract => "op_Subtraction",
            BinaryOperator.Multiply => "op_Multiply",
            BinaryOperator.Divide => "op_Division",
            BinaryOperator.Modulo => "op_Modulus",
            BinaryOperator.BitwiseAnd => "op_BitwiseAnd",
            BinaryOperator.BitwiseOr => "op_BitwiseOr",
            BinaryOperator.BitwiseXor => "op_ExclusiveOr",
            BinaryOperator.LeftShift => "op_LeftShift",
            BinaryOperator.RightShift => "op_RightShift",
            BinaryOperator.Equal => "op_Equality",
            BinaryOperator.NotEqual => "op_Inequality",
            BinaryOperator.LessThan => "op_LessThan",
            BinaryOperator.LessThanOrEqual => "op_LessThanOrEqual",
            BinaryOperator.GreaterThan => "op_GreaterThan",
            BinaryOperator.GreaterThanOrEqual => "op_GreaterThanOrEqual",
            _ => null
        };
    }

    private static string? UnaryOperatorToClrMethod(UnaryOperator op)
    {
        return op switch
        {
            UnaryOperator.Plus => "op_UnaryPlus",
            UnaryOperator.Minus => "op_UnaryNegation",
            UnaryOperator.BitwiseNot => "op_OnesComplement",
            UnaryOperator.Not => "op_LogicalNot",
            _ => null
        };
    }

    #endregion
}
