using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Utility methods for working with types.
/// Centralizes common operations to avoid duplication.
/// </summary>
internal static class TypeUtils
{
    /// <summary>
    /// Check if a type is numeric or Unknown (to avoid cascading errors).
    /// </summary>
    public static bool IsNumericOrUnknown(SemanticType type)
        => type is UnknownType || IsNumeric(type);

    /// <summary>
    /// Check if a type is numeric (int, long, float, double, decimal).
    /// </summary>
    public static bool IsNumeric(SemanticType type)
    {
        if (type is BuiltinType builtin && builtin.ClrType != null)
        {
            return builtin.ClrType == typeof(int) ||
                   builtin.ClrType == typeof(long) ||
                   builtin.ClrType == typeof(float) ||
                   builtin.ClrType == typeof(double) ||
                   builtin.ClrType == typeof(decimal) ||
                   builtin.ClrType == typeof(short) ||
                   builtin.ClrType == typeof(byte) ||
                   builtin.ClrType == typeof(sbyte) ||
                   builtin.ClrType == typeof(ushort) ||
                   builtin.ClrType == typeof(uint) ||
                   builtin.ClrType == typeof(ulong);
        }
        return false;
    }

    /// <summary>
    /// Check if a type is an integer type (non-floating point).
    /// </summary>
    public static bool IsInteger(SemanticType type)
    {
        if (type is BuiltinType builtin && builtin.ClrType != null)
        {
            return builtin.ClrType == typeof(int) ||
                   builtin.ClrType == typeof(long) ||
                   builtin.ClrType == typeof(short) ||
                   builtin.ClrType == typeof(byte) ||
                   builtin.ClrType == typeof(sbyte) ||
                   builtin.ClrType == typeof(ushort) ||
                   builtin.ClrType == typeof(uint) ||
                   builtin.ClrType == typeof(ulong);
        }
        return false;
    }

    /// <summary>
    /// Check if a type is a floating point type.
    /// </summary>
    public static bool IsFloatingPoint(SemanticType type)
    {
        if (type is BuiltinType builtin && builtin.ClrType != null)
        {
            return builtin.ClrType == typeof(float) ||
                   builtin.ClrType == typeof(double) ||
                   builtin.ClrType == typeof(decimal);
        }
        return false;
    }

    /// <summary>
    /// Check if a type is a string type.
    /// </summary>
    public static bool IsString(SemanticType type)
    {
        return type is BuiltinType { Name: "str" or "string" };
    }

    /// <summary>
    /// Check if a type is a boolean type.
    /// </summary>
    public static bool IsBool(SemanticType type)
    {
        return type is BuiltinType { Name: "bool" };
    }

    /// <summary>
    /// Check if a type is a collection (list, dict, set).
    /// </summary>
    public static bool IsCollection(SemanticType type)
    {
        return type is GenericType generic &&
            (generic.Name == BuiltinNames.List || generic.Name == BuiltinNames.Dict || generic.Name == BuiltinNames.Set);
    }

    /// <summary>
    /// Check if a type is a list.
    /// </summary>
    public static bool IsList(SemanticType type)
    {
        return type is GenericType { Name: BuiltinNames.List };
    }

    /// <summary>
    /// Check if a type is a dict.
    /// </summary>
    public static bool IsDict(SemanticType type)
    {
        return type is GenericType { Name: BuiltinNames.Dict };
    }

    /// <summary>
    /// Check if a type is a set.
    /// </summary>
    public static bool IsSet(SemanticType type)
    {
        return type is GenericType { Name: BuiltinNames.Set };
    }

    /// <summary>
    /// Check if a type is a tuple.
    /// </summary>
    public static bool IsTuple(SemanticType type)
    {
        return type is TupleType;
    }

    /// <summary>
    /// Get the element type of a collection, if applicable.
    /// For list/set: returns the element type.
    /// For dict: returns the value type.
    /// </summary>
    public static SemanticType? GetElementType(SemanticType type)
    {
        if (type is GenericType generic)
        {
            if (generic.Name == BuiltinNames.List || generic.Name == BuiltinNames.Set)
                return generic.TypeArguments.FirstOrDefault();
            if (generic.Name == BuiltinNames.Dict)
                return generic.TypeArguments.Skip(1).FirstOrDefault(); // Value type
        }
        return null;
    }

    /// <summary>
    /// Get the key type of a dict, if applicable.
    /// </summary>
    public static SemanticType? GetKeyType(SemanticType type)
    {
        if (type is GenericType { Name: BuiltinNames.Dict } generic)
        {
            return generic.TypeArguments.FirstOrDefault();
        }
        return null;
    }

    /// <summary>
    /// Unwrap nullable types recursively.
    /// </summary>
    public static SemanticType UnwrapAllNullable(SemanticType type)
    {
        while (type is NullableType nullable)
            type = nullable.UnderlyingType;
        return type;
    }

    /// <summary>
    /// Check if two types are structurally equivalent.
    /// </summary>
    public static bool AreEquivalent(SemanticType a, SemanticType b)
    {
        // Unwrap nullables for comparison
        var unwrappedA = UnwrapAllNullable(a);
        var unwrappedB = UnwrapAllNullable(b);

        // Check nullable mismatch
        bool aNullable = a is NullableType;
        bool bNullable = b is NullableType;
        if (aNullable != bNullable)
            return false;

        return unwrappedA.Equals(unwrappedB);
    }

    /// <summary>
    /// Get the common type of two types for binary operations.
    /// Returns null if types are not compatible.
    /// </summary>
    public static SemanticType? GetCommonType(SemanticType a, SemanticType b)
    {
        // Same type
        if (a.Equals(b))
            return a;

        // Numeric widening: int -> long -> float -> double
        if (IsNumeric(a) && IsNumeric(b))
        {
            // Double is the widest
            if (a is BuiltinType { ClrType: var aClr } && b is BuiltinType { ClrType: var bClr })
            {
                if (aClr == typeof(double) || bClr == typeof(double))
                    return SemanticType.Double;
                if (aClr == typeof(float) || bClr == typeof(float))
                    return SemanticType.Float;
                if (aClr == typeof(long) || bClr == typeof(long))
                    return SemanticType.Long;
                return SemanticType.Int;
            }
        }

        // Nullable handling
        if (a is NullableType nullableA)
        {
            var commonInner = GetCommonType(nullableA.UnderlyingType, b);
            if (commonInner != null)
                return new NullableType { UnderlyingType = commonInner };
        }
        if (b is NullableType nullableB)
        {
            var commonInner = GetCommonType(a, nullableB.UnderlyingType);
            if (commonInner != null)
                return new NullableType { UnderlyingType = commonInner };
        }

        return null;
    }

    /// <summary>
    /// Maps a ComparisonOperator to its corresponding BinaryOperator.
    /// </summary>
    public static Parser.Ast.BinaryOperator ComparisonOperatorToBinaryOperator(Parser.Ast.ComparisonOperator op)
    {
        return op switch
        {
            Parser.Ast.ComparisonOperator.Equal => Parser.Ast.BinaryOperator.Equal,
            Parser.Ast.ComparisonOperator.NotEqual => Parser.Ast.BinaryOperator.NotEqual,
            Parser.Ast.ComparisonOperator.LessThan => Parser.Ast.BinaryOperator.LessThan,
            Parser.Ast.ComparisonOperator.LessThanOrEqual => Parser.Ast.BinaryOperator.LessThanOrEqual,
            Parser.Ast.ComparisonOperator.GreaterThan => Parser.Ast.BinaryOperator.GreaterThan,
            Parser.Ast.ComparisonOperator.GreaterThanOrEqual => Parser.Ast.BinaryOperator.GreaterThanOrEqual,
            Parser.Ast.ComparisonOperator.In => Parser.Ast.BinaryOperator.In,
            Parser.Ast.ComparisonOperator.NotIn => Parser.Ast.BinaryOperator.NotIn,
            Parser.Ast.ComparisonOperator.Is => Parser.Ast.BinaryOperator.Is,
            Parser.Ast.ComparisonOperator.IsNot => Parser.Ast.BinaryOperator.IsNot,
            _ => throw new ArgumentException($"Unknown comparison operator: {op}", nameof(op))
        };
    }
}
