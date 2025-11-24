using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates operator usage in Sharpy code, supporting both Sharpy dunder methods
/// and CLR operator overloads for .NET interop.
/// </summary>
public class OperatorValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    
    // Caches for performance
    private readonly Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache = new();
    private readonly Dictionary<(UnaryOperator, SemanticType), SemanticType?> _unaryOpCache = new();
    private readonly Dictionary<Type, Dictionary<string, List<MethodInfo>>> _clrOperatorCache = new();

    public OperatorValidator(SymbolTable symbolTable, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Validates a binary operation and returns the result type.
    /// </summary>
    public SemanticType ValidateBinaryOp(
        BinaryOperator op,
        SemanticType left,
        SemanticType right,
        int line,
        int column)
    {
        // Check cache first
        var cacheKey = (left, op, right);
        if (_binaryOpCache.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult ?? SemanticType.Unknown;
        }

        SemanticType result;

        // Handle special cases that don't involve operator overloading
        switch (op)
        {
            case BinaryOperator.And:
            case BinaryOperator.Or:
                // Logical operators always return bool in Sharpy
                result = SemanticType.Bool;
                break;

            case BinaryOperator.In:
            case BinaryOperator.NotIn:
            case BinaryOperator.Is:
            case BinaryOperator.IsNot:
                // Membership and identity operators always return bool
                result = SemanticType.Bool;
                break;

            default:
                // For all other operators, resolve via dunder methods or CLR operators
                result = ResolveOperatorOverload(op, left, right, line, column);
                break;
        }

        // Cache the result
        _binaryOpCache[cacheKey] = result;
        return result;
    }

    /// <summary>
    /// Validates a unary operation and returns the result type.
    /// </summary>
    public SemanticType ValidateUnaryOp(
        UnaryOperator op,
        SemanticType operand,
        int line,
        int column)
    {
        // Check cache first
        var cacheKey = (op, operand);
        if (_unaryOpCache.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult ?? SemanticType.Unknown;
        }

        SemanticType result;

        // Handle special case: 'not' always returns bool
        result = (op == UnaryOperator.Not)
            ? SemanticType.Bool
            : ResolveUnaryOperatorOverload(op, operand, line, column);

        // Cache the result
        _unaryOpCache[cacheKey] = result;
        return result;
    }

    /// <summary>
    /// Maps a BinaryOperator to its corresponding dunder method name.
    /// </summary>
    private string? BinaryOperatorToDunder(BinaryOperator op)
    {
        return op switch
        {
            // Arithmetic
            BinaryOperator.Add => "__add__",
            BinaryOperator.Subtract => "__sub__",
            BinaryOperator.Multiply => "__mul__",
            BinaryOperator.Divide => "__truediv__",
            BinaryOperator.FloorDivide => "__floordiv__",
            BinaryOperator.Modulo => "__mod__",
            BinaryOperator.Power => "__pow__",

            // Bitwise
            BinaryOperator.BitwiseAnd => "__and__",
            BinaryOperator.BitwiseOr => "__or__",
            BinaryOperator.BitwiseXor => "__xor__",
            BinaryOperator.LeftShift => "__lshift__",
            BinaryOperator.RightShift => "__rshift__",

            // Comparison
            BinaryOperator.Equal => "__eq__",
            BinaryOperator.NotEqual => "__ne__",
            BinaryOperator.LessThan => "__lt__",
            BinaryOperator.LessThanOrEqual => "__le__",
            BinaryOperator.GreaterThan => "__gt__",
            BinaryOperator.GreaterThanOrEqual => "__ge__",

            // These don't map to dunders
            BinaryOperator.And => null,
            BinaryOperator.Or => null,
            BinaryOperator.In => null,
            BinaryOperator.NotIn => null,
            BinaryOperator.Is => null,
            BinaryOperator.IsNot => null,
            BinaryOperator.NullCoalesce => null,

            _ => null
        };
    }

    /// <summary>
    /// Maps a UnaryOperator to its corresponding dunder method name.
    /// </summary>
    private string? UnaryOperatorToDunder(UnaryOperator op)
    {
        return op switch
        {
            UnaryOperator.Plus => "__pos__",
            UnaryOperator.Minus => "__neg__",
            UnaryOperator.BitwiseNot => "__invert__",
            UnaryOperator.Not => null, // 'not' doesn't have a dunder method
            _ => null
        };
    }

    /// <summary>
    /// Maps a BinaryOperator to its corresponding CLR operator method name.
    /// </summary>
    private string? BinaryOperatorToClrMethod(BinaryOperator op)
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

    /// <summary>
    /// Maps a UnaryOperator to its corresponding CLR operator method name.
    /// </summary>
    private string? UnaryOperatorToClrMethod(UnaryOperator op)
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

    /// <summary>
    /// Resolves a binary operator overload for the given operand types.
    /// </summary>
    private SemanticType ResolveOperatorOverload(
        BinaryOperator op,
        SemanticType left,
        SemanticType right,
        int line,
        int column)
    {
        var dunderName = BinaryOperatorToDunder(op);
        
        // Try user-defined type first
        if (left is UserDefinedType udt && udt.Symbol != null && dunderName != null &&
            udt.Symbol.OperatorMethods.TryGetValue(dunderName, out var methods))
        {
            var bestOverload = ResolveBestOverload(methods, right, line, column);
            if (bestOverload != null)
            {
                return bestOverload.ReturnType;
            }
        }

        // Try Sharpy builtin types
        var builtinResult = TryResolveBuiltinOperator(op, left, right);
        if (builtinResult != null)
        {
            return builtinResult;
        }

        // Try CLR operator
        var clrResult = TryResolveClrOperator(op, left, right);
        if (clrResult != null)
        {
            return clrResult;
        }

        // No operator found
        _logger.LogError(
            $"Type '{left.GetDisplayName()}' does not support operator '{GetOperatorSymbol(op)}' with right operand of type '{right.GetDisplayName()}'",
            line,
            column);
        
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Resolves a unary operator overload for the given operand type.
    /// </summary>
    private SemanticType ResolveUnaryOperatorOverload(
        UnaryOperator op,
        SemanticType operand,
        int line,
        int column)
    {
        var dunderName = UnaryOperatorToDunder(op);
        
        // Try user-defined type first
        if (operand is UserDefinedType udt && udt.Symbol != null && dunderName != null &&
            udt.Symbol.OperatorMethods.TryGetValue(dunderName, out var methods))
        {
            // For unary operators, we expect exactly one overload with just 'self'
            var method = methods.FirstOrDefault();
            if (method != null)
            {
                return method.ReturnType;
            }
        }

        // Try Sharpy builtin types
        var builtinResult = TryResolveBuiltinUnaryOperator(op, operand);
        if (builtinResult != null)
        {
            return builtinResult;
        }

        // Try CLR operator
        var clrResult = TryResolveClrUnaryOperator(op, operand);
        if (clrResult != null)
        {
            return clrResult;
        }

        // No operator found
        _logger.LogError(
            $"Type '{operand.GetDisplayName()}' does not support unary operator '{GetUnaryOperatorSymbol(op)}'",
            line,
            column);
        
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Resolves the best overload from a list of candidate methods.
    /// Uses most-specific match semantics.
    /// </summary>
    private FunctionSymbol? ResolveBestOverload(List<FunctionSymbol> candidates, SemanticType argumentType, int line, int column)
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

        // Find assignable match
        var assignableMatches = candidates.Where(c =>
            c.Parameters.Count == 2 &&
            argumentType.IsAssignableTo(c.Parameters[1].Type)).ToList();

        if (assignableMatches.Count == 0)
            return null;

        if (assignableMatches.Count == 1)
            return assignableMatches[0];

        // Multiple matches - this is ambiguous
        _logger.LogWarning(
            $"Ambiguous operator overload: multiple candidates found for argument type '{argumentType.GetDisplayName()}'",
            line, column);
        
        return assignableMatches[0];
    }

    /// <summary>
    /// Try to resolve operator for Sharpy builtin types (int, float, str, list, dict, etc.)
    /// </summary>
    private SemanticType? TryResolveBuiltinOperator(BinaryOperator op, SemanticType left, SemanticType right)
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
                BinaryOperator.Divide or
                BinaryOperator.FloorDivide or
                BinaryOperator.Modulo or
                BinaryOperator.Power => InferNumericResultType(left, right),
                
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
                BinaryOperator.Equal or BinaryOperator.NotEqual => SemanticType.Bool,
                _ => null
            };
        }

        // List concatenation and comparison
        if (left is GenericType { Name: "list" } leftList && 
            right is GenericType { Name: "list" } rightList)
        {
            if (op == BinaryOperator.Add)
            {
                // Result is a list with the common type of elements
                if (leftList.TypeArguments.Count > 0 && rightList.TypeArguments.Count > 0)
                {
                    var leftElem = leftList.TypeArguments[0];
                    var rightElem = rightList.TypeArguments[0];

                    if (leftElem.Equals(rightElem))
                    {
                        return leftList;
                    }
                    // Explicitly return null if element types do not match
                    return null;
                }
                else if (leftList.TypeArguments.Count == 0 || rightList.TypeArguments.Count == 0)
                {
                    // If either list is untyped, return a generic list type (list with no type arguments)
                    return new GenericType("list");
                }
            }
            else if (op == BinaryOperator.Equal || op == BinaryOperator.NotEqual)
            {
                return SemanticType.Bool;
            }
        }

        // Default equality for all types: only allow if types are identical
        if ((op == BinaryOperator.Equal || op == BinaryOperator.NotEqual) && left.Equals(right))
        {
            return SemanticType.Bool;
        }

        return null;
    }

    /// <summary>
    /// Try to resolve unary operator for Sharpy builtin types.
    /// </summary>
    private SemanticType? TryResolveBuiltinUnaryOperator(UnaryOperator op, SemanticType operand)
    {
        // Bitwise not on integers (check before general numeric)
        if (IsIntegerType(operand) && op == UnaryOperator.BitwiseNot)
        {
            return operand;
        }

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

    /// <summary>
    /// Try to resolve operator using CLR reflection.
    /// </summary>
    private SemanticType? TryResolveClrOperator(BinaryOperator op, SemanticType left, SemanticType right)
    {
        var clrMethodName = BinaryOperatorToClrMethod(op);
        if (clrMethodName == null)
            return null;

        Type? leftClrType = GetClrType(left);
        if (leftClrType == null)
            return null;

        Type? rightClrType = GetClrType(right);
        if (rightClrType == null)
            return null;

        // Get or cache CLR operators for this type
        if (!_clrOperatorCache.TryGetValue(leftClrType, out var operators))
        {
            operators = new Dictionary<string, List<MethodInfo>>();
            foreach (var method in leftClrType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.StartsWith("op_")))
            {
                if (!operators.TryGetValue(method.Name, out var methodList))
                {
                    methodList = new List<MethodInfo>();
                    operators[method.Name] = methodList;
                }
                methodList.Add(method);
            }
            _clrOperatorCache[leftClrType] = operators;
        }

        if (operators.TryGetValue(clrMethodName, out var operatorMethods))
        {
            // Find the overload whose parameter types match left and right
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
            // No matching overload found
            return null;
        }

        return null;
    }

    /// <summary>
    /// Try to resolve unary operator using CLR reflection.
    /// </summary>
    private SemanticType? TryResolveClrUnaryOperator(UnaryOperator op, SemanticType operand)
    {
        var clrMethodName = UnaryOperatorToClrMethod(op);
        if (clrMethodName == null)
            return null;

        Type? clrType = GetClrType(operand);
        if (clrType == null)
            return null;

        // Get or cache CLR operators for this type
        if (!_clrOperatorCache.TryGetValue(clrType, out var operators))
        {
            operators = new Dictionary<string, List<MethodInfo>>();
            foreach (var method in clrType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.StartsWith("op_")))
            {
                if (!operators.TryGetValue(method.Name, out var methodList))
                {
                    methodList = new List<MethodInfo>();
                    operators[method.Name] = methodList;
                }
                methodList.Add(method);
            }
            _clrOperatorCache[clrType] = operators;
        }

        if (operators.TryGetValue(clrMethodName, out var operatorMethods))
        {
            // Find the overload with matching parameter type
            foreach (var method in operatorMethods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == clrType)
                {
                    return MapClrTypeToSemanticType(method.ReturnType);
                }
            }
            // No matching overload found
            return null;
        }

        return null;
    }

    /// <summary>
    /// Gets the CLR type for a SemanticType, if available.
    /// </summary>
    private Type? GetClrType(SemanticType type)
    {
        return type switch
        {
            BuiltinType builtin => builtin.ClrType,
            UserDefinedType udt => udt.Symbol?.ClrType,
            _ => null
        };
    }

    /// <summary>
    /// Maps a CLR Type to a SemanticType.
    /// </summary>
    private SemanticType MapClrTypeToSemanticType(Type clrType)
    {
        // Map common CLR types to Sharpy types
        if (clrType == typeof(int)) return SemanticType.Int;
        if (clrType == typeof(long)) return SemanticType.Long;
        if (clrType == typeof(float)) return SemanticType.Float;
        if (clrType == typeof(double)) return SemanticType.Double;
        if (clrType == typeof(bool)) return SemanticType.Bool;
        if (clrType == typeof(string)) return SemanticType.Str;
        if (clrType == typeof(void)) return SemanticType.Void;

        // For other types, try to look up the symbol in the symbol table
        var symbol = _symbolTable.LookupByClrType(clrType);
        if (symbol != null)
        {
            return new UserDefinedType { Name = clrType.Name, Symbol = symbol };
        }
        else
        {
            _logger.Warn($"UserDefinedType for CLR type '{clrType.FullName}' created without symbol. This may require further resolution.");
            return new UserDefinedType { Name = clrType.Name };
        }
    }

    /// <summary>
    /// Checks if a type is numeric (int, long, float, double).
    /// </summary>
    private bool IsNumericType(SemanticType type)
    {
        return type == SemanticType.Int ||
               type == SemanticType.Long ||
               type == SemanticType.Float ||
               type == SemanticType.Double;
    }

    /// <summary>
    /// Checks if a type is an integer type (int, long).
    /// </summary>
    private bool IsIntegerType(SemanticType type)
    {
        return type == SemanticType.Int || type == SemanticType.Long;
    }

    /// <summary>
    /// Infers the result type of a numeric operation.
    /// Uses standard numeric promotion rules.
    /// </summary>
    private SemanticType InferNumericResultType(SemanticType left, SemanticType right)
    {
        // Double beats everything
        if (left == SemanticType.Double || right == SemanticType.Double)
            return SemanticType.Double;

        // Float beats int and long
        if (left == SemanticType.Float || right == SemanticType.Float)
            return SemanticType.Float;

        // Long beats int
        if (left == SemanticType.Long || right == SemanticType.Long)
            return SemanticType.Long;

        // Both must be int
        return SemanticType.Int;
    }

    /// <summary>
    /// Gets a human-readable symbol for a binary operator.
    /// </summary>
    private string GetOperatorSymbol(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.FloorDivide => "//",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Power => "**",
            BinaryOperator.BitwiseAnd => "&",
            BinaryOperator.BitwiseOr => "|",
            BinaryOperator.BitwiseXor => "^",
            BinaryOperator.LeftShift => "<<",
            BinaryOperator.RightShift => ">>",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.And => "and",
            BinaryOperator.Or => "or",
            BinaryOperator.In => "in",
            BinaryOperator.NotIn => "not in",
            BinaryOperator.Is => "is",
            BinaryOperator.IsNot => "is not",
            _ => op.ToString()
        };
    }

    /// <summary>
    /// Gets a human-readable symbol for a unary operator.
    /// </summary>
    private string GetUnaryOperatorSymbol(UnaryOperator op)
    {
        return op switch
        {
            UnaryOperator.Plus => "+",
            UnaryOperator.Minus => "-",
            UnaryOperator.BitwiseNot => "~",
            UnaryOperator.Not => "not",
            _ => op.ToString()
        };
    }
}
