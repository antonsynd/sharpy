using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Validates operator usage in Sharpy code, supporting both Sharpy dunder methods
/// and CLR operator overloads for .NET interop.
///
/// NOTE: This class is NOT thread-safe. Instances should not be shared across threads.
/// The internal caches are not protected by locks for performance reasons.
/// </summary>
public class OperatorValidator
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    // Caches for performance (not thread-safe)
    private readonly Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache = new();
    private readonly Dictionary<(UnaryOperator, SemanticType), SemanticType?> _unaryOpCache = new();
    private readonly Dictionary<Type, Dictionary<string, List<MethodInfo>>> _clrOperatorCache = new();

    public OperatorValidator(SymbolTable symbolTable, ICompilerLogger? logger = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Gets the errors collected during operator validation.
    /// </summary>
    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Adds an error to the errors collection and logs it.
    /// </summary>
    private void AddError(string message, int line, int column)
    {
        _errors.Add(new SemanticError(message, line, column));
        _logger.LogError(message, line, column);
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

            case BinaryOperator.NullCoalesce:
                // TODO: Implement null coalescing operator support
                // For now, return Unknown and log an error
                AddError(
                    $"Null coalescing operator ('??') is not yet implemented",
                    line, column);
                result = SemanticType.Unknown;
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
        var result = TryResolveOperatorOverloadWithoutLogging(op, left, right, line, column);

        if (result != null)
        {
            return result;
        }

        // No operator found
        AddError(
            $"Type '{left.GetDisplayName()}' does not support operator '{GetOperatorSymbol(op)}' with right operand of type '{right.GetDisplayName()}'",
            line,
            column);

        return SemanticType.Unknown;
    }

    /// <summary>
    /// Tries to resolve a binary operator overload without logging errors.
    /// Returns null if no operator is found.
    /// </summary>
    private SemanticType? TryResolveOperatorOverloadWithoutLogging(
        BinaryOperator op,
        SemanticType left,
        SemanticType right,
        int line,
        int column)
    {
        var dunderName = BinaryOperatorToDunder(op);

        // Try user-defined type first (direct operator or complement synthesis)
        if (left is UserDefinedType udt && udt.Symbol != null)
        {
            // Try direct operator lookup first
            if (dunderName != null && udt.Symbol.OperatorMethods.TryGetValue(dunderName, out var methods))
            {
                var bestOverload = ResolveBestOverload(
                    methods,
                    right,
                    GetOperatorSymbol(op),
                    udt.Name,
                    line,
                    column);
                if (bestOverload != null)
                {
                    return bestOverload.ReturnType;
                }
            }

            // If direct lookup failed, try equality complement synthesis
            // (only for == and != when the complement operator exists)
            var complementResult = TryResolveEqualityComplement(op, udt, right, line, column);
            if (complementResult != null)
            {
                return complementResult;
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

        return null;
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
        AddError(
            $"Type '{operand.GetDisplayName()}' does not support unary operator '{GetUnaryOperatorSymbol(op)}'",
            line,
            column);

        return SemanticType.Unknown;
    }

    /// <summary>
    /// Resolves the best overload from a list of candidate methods.
    /// Uses most-specific match semantics: finds exact match first, then the most derived/specific assignable match.
    /// </summary>
    /// <param name="candidates">List of candidate function symbols</param>
    /// <param name="argumentType">The type of the right-hand operand</param>
    /// <param name="operatorSymbol">The operator symbol for error messages (e.g., "+", "__add__")</param>
    /// <param name="leftTypeName">The name of the left operand type for error messages</param>
    /// <param name="line">Line number for error reporting</param>
    /// <param name="column">Column number for error reporting</param>
    private FunctionSymbol? ResolveBestOverload(
        List<FunctionSymbol> candidates,
        SemanticType argumentType,
        string operatorSymbol,
        string leftTypeName,
        int line,
        int column)
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

        // Find assignable matches
        var assignableMatches = candidates.Where(c =>
            c.Parameters.Count == 2 &&
            argumentType.IsAssignableTo(c.Parameters[1].Type)).ToList();

        if (assignableMatches.Count == 0)
            return null;

        if (assignableMatches.Count == 1)
            return assignableMatches[0];

        // Multiple assignable matches - find the most specific one
        // A parameter type is more specific if other types are assignable to it
        FunctionSymbol? mostSpecific = null;
        bool isAmbiguous = false;

        foreach (var candidate in assignableMatches)
        {
            var candidateParamType = candidate.Parameters[1].Type;
            bool isMostSpecific = true;
            bool hasSomeMoreGeneral = false;

            foreach (var other in assignableMatches)
            {
                if (candidate == other)
                    continue;

                var otherParamType = other.Parameters[1].Type;

                // If candidateParamType is assignable to otherParamType,
                // then candidateParamType is more specific (more derived)
                if (candidateParamType.IsAssignableTo(otherParamType))
                {
                    hasSomeMoreGeneral = true;
                }
                // If otherParamType is assignable to candidateParamType,
                // then otherParamType is more specific
                else if (otherParamType.IsAssignableTo(candidateParamType))
                {
                    isMostSpecific = false;
                    break;
                }
            }

            if (isMostSpecific && hasSomeMoreGeneral)
            {
                if (mostSpecific != null)
                {
                    // Found multiple "most specific" candidates - ambiguous
                    isAmbiguous = true;
                }
                else
                {
                    mostSpecific = candidate;
                }
            }
        }

        if (mostSpecific != null && !isAmbiguous)
        {
            return mostSpecific;
        }

        // Either no clear most-specific match or ambiguous - report error
        // Build list of candidate parameter types for the error message
        var candidateTypes = string.Join(", ", assignableMatches
            .Where(c => c.Parameters.Count >= 2)
            .Select(c => $"'{c.Parameters[1].Type.GetDisplayName()}'"));

        AddError(
            $"Ambiguous overload for operator '{operatorSymbol}' on type '{leftTypeName}': " +
            $"multiple overloads are applicable for argument type '{argumentType.GetDisplayName()}' " +
            $"(candidates accept: {candidateTypes})",
            line, column);

        // Return first match to allow continued type checking
        return assignableMatches[0];
    }

    /// <summary>
    /// Tries to resolve equality/inequality operators using complement synthesis.
    /// If only __eq__ is defined, synthesize __ne__ (and vice versa) to match RoslynEmitter behavior.
    /// </summary>
    private SemanticType? TryResolveEqualityComplement(
        BinaryOperator op,
        UserDefinedType udt,
        SemanticType right,
        int line,
        int column)
    {
        // Only applies to equality and inequality operators
        if (op != BinaryOperator.Equal && op != BinaryOperator.NotEqual)
        {
            return null;
        }

        var hasEq = udt.Symbol!.OperatorMethods.ContainsKey("__eq__");
        var hasNe = udt.Symbol!.OperatorMethods.ContainsKey("__ne__");

        // If both are defined or neither is defined, no complement synthesis needed
        if ((hasEq && hasNe) || (!hasEq && !hasNe))
        {
            return null;
        }

        // Try to synthesize the complement
        if (op == BinaryOperator.Equal && hasNe)
        {
            // == requested but only __ne__ exists
            // We can synthesize == by using __ne__
            var neMethods = udt.Symbol.OperatorMethods["__ne__"];
            var bestOverload = ResolveBestOverload(
                neMethods,
                right,
                "==",
                udt.Name,
                line,
                column);
            if (bestOverload != null)
            {
                // __ne__ should return bool, so == will also return bool
                return SemanticType.Bool;
            }
        }
        else if (op == BinaryOperator.NotEqual && hasEq)
        {
            // != requested but only __eq__ exists
            // We can synthesize != by using __eq__
            var eqMethods = udt.Symbol.OperatorMethods["__eq__"];
            var bestOverload = ResolveBestOverload(
                eqMethods,
                right,
                "!=",
                udt.Name,
                line,
                column);
            if (bestOverload != null)
            {
                // __eq__ should return bool, so != will also return bool
                return SemanticType.Bool;
            }
        }

        return null;
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
                BinaryOperator.Equal or BinaryOperator.NotEqual or
                BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual or
                BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual => SemanticType.Bool,
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
                else if (leftList.TypeArguments.Count == 0 && rightList.TypeArguments.Count == 0)
                {
                    // Both untyped - return untyped list
                    return new GenericType { Name = "list" };
                }
                else if (leftList.TypeArguments.Count == 0)
                {
                    // Left is untyped, right is typed - return right's type
                    return rightList;
                }
                else if (rightList.TypeArguments.Count == 0)
                {
                    // Right is untyped, left is typed - return left's type
                    return leftList;
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
    /// Gets or caches CLR operators for a given type.
    /// </summary>
    private Dictionary<string, List<MethodInfo>> GetOrCacheClrOperators(Type clrType)
    {
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
        return operators;
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
        var operators = GetOrCacheClrOperators(leftClrType);

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
        var operators = GetOrCacheClrOperators(clrType);

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

        // For other types, create a UserDefinedType
        // TODO: Look up the symbol in the symbol table when SymbolTable.LookupByClrType() is implemented
        // This would allow proper symbol resolution for CLR types
        return new UserDefinedType { Name = clrType.Name };
    }

    /// <summary>
    /// Checks if a type is numeric (int, long, float, double, etc.).
    /// Delegates to PrimitiveCatalog for exhaustive primitive type checking.
    /// </summary>
    private static bool IsNumericType(SemanticType type)
        => PrimitiveCatalog.IsNumeric(type);

    /// <summary>
    /// Checks if a type is an integer type (signed or unsigned).
    /// Delegates to PrimitiveCatalog for exhaustive primitive type checking.
    /// </summary>
    private static bool IsIntegerType(SemanticType type)
        => PrimitiveCatalog.IsInteger(type);

    /// <summary>
    /// Infers the result type of a numeric operation.
    /// Delegates to PrimitiveCatalog for standard numeric promotion rules.
    /// </summary>
    private static SemanticType InferNumericResultType(SemanticType left, SemanticType right)
    {
        var promoted = PrimitiveCatalog.GetPromotedType(left, right);
        // Fall back to Unknown if types cannot be promoted (shouldn't happen for valid numeric types)
        return promoted ?? SemanticType.Unknown;
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
            BinaryOperator.NullCoalesce => "??",
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

    /// <summary>
    /// Maps a ComparisonOperator to its corresponding BinaryOperator.
    /// </summary>
    public static BinaryOperator ComparisonOperatorToBinaryOperator(ComparisonOperator op)
    {
        return op switch
        {
            ComparisonOperator.Equal => BinaryOperator.Equal,
            ComparisonOperator.NotEqual => BinaryOperator.NotEqual,
            ComparisonOperator.LessThan => BinaryOperator.LessThan,
            ComparisonOperator.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
            ComparisonOperator.GreaterThan => BinaryOperator.GreaterThan,
            ComparisonOperator.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
            ComparisonOperator.In => BinaryOperator.In,
            ComparisonOperator.NotIn => BinaryOperator.NotIn,
            ComparisonOperator.Is => BinaryOperator.Is,
            ComparisonOperator.IsNot => BinaryOperator.IsNot,
            _ => throw new ArgumentException($"Unknown comparison operator: {op}", nameof(op))
        };
    }

    /// <summary>
    /// Maps an AssignmentOperator to its corresponding in-place dunder method name.
    /// Returns null for simple assignment (=).
    /// </summary>
    private string? AssignmentOperatorToInPlaceDunder(AssignmentOperator op)
    {
        return op switch
        {
            AssignmentOperator.PlusAssign => "__iadd__",
            AssignmentOperator.MinusAssign => "__isub__",
            AssignmentOperator.StarAssign => "__imul__",
            AssignmentOperator.SlashAssign => "__itruediv__",
            AssignmentOperator.DoubleSlashAssign => "__ifloordiv__",
            AssignmentOperator.PercentAssign => "__imod__",
            AssignmentOperator.PowerAssign => "__ipow__",
            AssignmentOperator.AndAssign => "__iand__",
            AssignmentOperator.OrAssign => "__ior__",
            AssignmentOperator.XorAssign => "__ixor__",
            AssignmentOperator.LeftShiftAssign => "__ilshift__",
            AssignmentOperator.RightShiftAssign => "__irshift__",
            AssignmentOperator.Assign => null,
            _ => null
        };
    }

    /// <summary>
    /// Maps an AssignmentOperator to its corresponding BinaryOperator.
    /// Returns null for simple assignment (=).
    /// </summary>
    private BinaryOperator? AssignmentOperatorToBinaryOperator(AssignmentOperator op)
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
            AssignmentOperator.Assign => null,
            _ => null
        };
    }

    /// <summary>
    /// Gets a human-readable symbol for an assignment operator.
    /// </summary>
    private string GetAssignmentOperatorSymbol(AssignmentOperator op)
    {
        return op switch
        {
            AssignmentOperator.Assign => "=",
            AssignmentOperator.PlusAssign => "+=",
            AssignmentOperator.MinusAssign => "-=",
            AssignmentOperator.StarAssign => "*=",
            AssignmentOperator.SlashAssign => "/=",
            AssignmentOperator.DoubleSlashAssign => "//=",
            AssignmentOperator.PercentAssign => "%=",
            AssignmentOperator.PowerAssign => "**=",
            AssignmentOperator.AndAssign => "&=",
            AssignmentOperator.OrAssign => "|=",
            AssignmentOperator.XorAssign => "^=",
            AssignmentOperator.LeftShiftAssign => "<<=",
            AssignmentOperator.RightShiftAssign => ">>=",
            _ => op.ToString()
        };
    }

    /// <summary>
    /// Validates an augmented assignment operation (+=, -=, *=, etc.).
    /// Returns the result type of the operation, which must be assignable to the target type.
    /// </summary>
    /// <param name="op">The assignment operator (e.g., PlusAssign for +=)</param>
    /// <param name="targetType">The type of the assignment target (left-hand side)</param>
    /// <param name="valueType">The type of the value being assigned (right-hand side)</param>
    /// <param name="line">Line number for error reporting</param>
    /// <param name="column">Column number for error reporting</param>
    /// <returns>The result type of the operation, or Unknown if invalid</returns>
    public SemanticType ValidateAugmentedAssignment(
        AssignmentOperator op,
        SemanticType targetType,
        SemanticType valueType,
        int line,
        int column)
    {
        // Simple assignment doesn't need special handling
        if (op == AssignmentOperator.Assign)
        {
            return valueType;
        }

        var inPlaceDunder = AssignmentOperatorToInPlaceDunder(op);
        var binaryOp = AssignmentOperatorToBinaryOperator(op);

        // Try in-place operator first (e.g., __iadd__)
        SemanticType? resultType = null;

        if (inPlaceDunder != null
            && targetType is UserDefinedType udt
            && udt.Symbol != null
            && udt.Symbol.OperatorMethods.TryGetValue(inPlaceDunder, out var methods))
        {
            var bestOverload = ResolveBestOverload(
                methods,
                valueType,
                GetAssignmentOperatorSymbol(op),
                udt.Name,
                line,
                column);
            if (bestOverload != null)
            {
                resultType = bestOverload.ReturnType;
            }
        }

        // Fall back to binary operator if in-place operator not found (e.g., __add__ for +=)
        if (resultType == null && binaryOp != null)
        {
            // Use TryResolveOperatorOverloadWithoutLogging to avoid duplicate error messages
            resultType = TryResolveOperatorOverloadWithoutLogging(binaryOp.Value, targetType, valueType, line, column);
        }

        // If no operator found, report error
        if (resultType == null)
        {
            AddError(
                $"Type '{targetType.GetDisplayName()}' does not support augmented assignment operator '{GetAssignmentOperatorSymbol(op)}' with right operand of type '{valueType.GetDisplayName()}'",
                line,
                column);
            return SemanticType.Unknown;
        }

        // Verify result type is assignable to target type
        if (!resultType.IsAssignableTo(targetType))
        {
            AddError(
                $"Result type '{resultType.GetDisplayName()}' of augmented assignment '{GetAssignmentOperatorSymbol(op)}' is not assignable to target type '{targetType.GetDisplayName()}'",
                line,
                column);
            return SemanticType.Unknown;
        }

        return resultType;
    }
}
