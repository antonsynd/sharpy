using System.Collections.Immutable;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Service for inferring result types from operations.
/// This service focuses purely on type inference - it does NOT validate or report errors.
///
/// The service extracts type inference logic that was previously embedded in validators.
/// Validators should use this service for type inference and handle error reporting separately.
/// </summary>
/// <remarks>
/// <para>
/// Design notes:
/// - All methods return nullable types (null means "cannot infer")
/// - Methods do NOT report errors (validation responsibility is separate)
/// - Results are cached for performance (operator results are highly repetitive)
/// </para>
/// <para>
/// <b>Threading:</b> Not thread-safe. Caches use <see cref="Dictionary{TKey,TValue}"/>.
/// Each <see cref="TypeChecker"/> creates its own instance, so concurrent access does not
/// arise in practice.
/// </para>
/// <para>
/// <b>To make thread-safe:</b> Replace <c>_binaryOpCache</c> and <c>_unaryOpCache</c>
/// with <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>.
/// </para>
/// </remarks>
[NotThreadSafe(Reason = "Uses non-concurrent Dictionary caches; create per-compilation instance")]
internal class TypeInferenceService
{
    private readonly SymbolTable _symbolTable;
    private readonly ClrMemberCache _clrMemberCache;
    private readonly Lazy<ClrTypeMapper> _clrTypeMapper = new(() => new ClrTypeMapper());

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

    /// <summary>
    /// Determines how codegen should emit an equality operation (<c>==</c>/<c>!=</c>) on the
    /// given operand types. Returns <see cref="BinaryOpLowering.EqualsCall"/> for tuples and for
    /// CLR types resolved via the Equals fallback (see <see cref="IsClrEqualsFallback"/>);
    /// otherwise <see cref="BinaryOpLowering.NativeOperator"/>. The decision is a pure function of
    /// the operand types, mirroring the inference rules so the emitter never has to re-derive it.
    /// </summary>
    internal BinaryOpLowering GetBinaryOpLowering(BinaryOperator op, SemanticType left, SemanticType right)
    {
        // `x == None` / `x != None` on a reference-semantics type lowers to a C# null pattern
        // check (`x is null` / `x is not null`), bypassing any overloaded operator (#901).
        if (IsNoneReferenceEquality(op, left, right))
            return BinaryOpLowering.NoneCheck;

        if (IsTupleEquality(op, left, right) || IsClrEqualsFallback(op, left, right))
            return BinaryOpLowering.EqualsCall;

        return BinaryOpLowering.NativeOperator;
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

        // `x == None` / `x != None` against a reference-semantics type (#901): Python's
        // `obj == None` falls back to identity (→ False for a live object), so we treat it as
        // a null check that yields Bool. Codegen lowers it to a C# null pattern (NoneCheck).
        // Non-nullable value types keep SPY0222 (statically always-False — almost surely a bug).
        // NullableType/OptionalType have their own paths and are intentionally excluded here.
        if (IsNoneReferenceEquality(op, left, right))
            return SemanticType.Bool;

        // C# NullableType (T?, the loose interop form) supports comparison/equality operators
        // via C#'s lifted operators — `int? == int` is valid and yields bool (#947). Resolve
        // the operator against the underlying types; only comparisons are relaxed (arithmetic on
        // possibly-null operands is intentionally not enabled here). OptionalType (Sharpy T?, the
        // strict tagged union) is deliberately excluded — it must be narrowed/unwrapped first.
        if (IsComparisonOperator(op) && (left is NullableType || right is NullableType))
        {
            var nullableResult = InferBinaryOpType(op, UnwrapNullable(left), UnwrapNullable(right));
            if (nullableResult != null)
                return nullableResult;
        }

        // Try builtin types first
        var builtinResult = TryInferBuiltinBinaryOp(op, left, right);
        if (builtinResult != null)
            return builtinResult;

        // Try type parameter constraints (e.g., T: IComparable allows comparison)
        var typeParamResult = TryInferTypeParameterBinaryOp(op, left, right);
        if (typeParamResult != null)
            return typeParamResult;

        // Enum types support comparison operators natively (backed by integers in C#)
        if (IsEnumType(left) && IsEnumType(right))
        {
            if (op is BinaryOperator.Equal or BinaryOperator.NotEqual
                or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
                or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual)
                return SemanticType.Bool;
        }

        // Tuple equality: (a, b) == (c, d) when both are tuples of equal arity whose
        // element pairs each support equality. Lowers to a structural Equals() call.
        var tupleResult = TryInferTupleBinaryOp(op, left, right);
        if (tupleResult != null)
            return tupleResult;

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
        if (TypeUtils.IsInteger(left) && TypeUtils.IsInteger(right))
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
        if (TypeUtils.IsNumeric(left) && TypeUtils.IsNumeric(right))
        {
            return op switch
            {
                BinaryOperator.Add or
                BinaryOperator.Subtract or
                BinaryOperator.Multiply or
                BinaryOperator.FloorDivide or
                BinaryOperator.Modulo => InferNumericResultType(left, right),

                // Division: decimal/decimal => decimal, otherwise float64 (Python semantics)
                BinaryOperator.Divide => PrimitiveCatalog.IsDecimal(left) && PrimitiveCatalog.IsDecimal(right)
                    ? InferNumericResultType(left, right) ?? SemanticType.Decimal
                    : SemanticType.Double,

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

        // String repetition: str * int or int * str
        if (op == BinaryOperator.Multiply)
        {
            if ((left == SemanticType.Str && TypeUtils.IsInteger(right)) ||
                (TypeUtils.IsInteger(left) && right == SemanticType.Str))
                return SemanticType.Str;
        }

        // Bytes operations: concatenation, repetition, equality, comparison
        if (left is UserDefinedType { Name: BuiltinNames.Bytes } leftBytes)
        {
            if (right is UserDefinedType { Name: BuiltinNames.Bytes })
            {
                var result = op switch
                {
                    BinaryOperator.Add => leftBytes,
                    BinaryOperator.Equal or BinaryOperator.NotEqual
                        or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
                        or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual => SemanticType.Bool,
                    _ => (SemanticType?)null
                };
                if (result != null)
                    return result;
            }
            if (op == BinaryOperator.Multiply && TypeUtils.IsInteger(right))
                return leftBytes;
        }

        // Bytes repetition: int * bytes
        if (op == BinaryOperator.Multiply && TypeUtils.IsInteger(left) &&
            right is UserDefinedType { Name: BuiltinNames.Bytes })
        {
            return right;
        }

        // Template concatenation: Template + Template -> Template
        if (left is TemplateType && right is TemplateType && op == BinaryOperator.Add)
        {
            return TemplateType.Instance;
        }

        // List concatenation
        if (left is GenericType { Name: BuiltinNames.List } leftList &&
            right is GenericType { Name: BuiltinNames.List } rightList)
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

    /// <summary>
    /// Infers the result of an equality operation on two tuple types. Returns
    /// <see cref="SemanticType.Bool"/> when <paramref name="op"/> is <c>==</c>/<c>!=</c>,
    /// both operands are tuples of equal arity, and every element pair itself supports
    /// equality (checked recursively). Returns null otherwise so SPY0222 still fires for
    /// mismatched-arity or incomparable tuples.
    /// </summary>
    private SemanticType? TryInferTupleBinaryOp(BinaryOperator op, SemanticType left, SemanticType right)
    {
        return IsTupleEquality(op, left, right) ? SemanticType.Bool : null;
    }

    /// <summary>
    /// Returns true when <paramref name="op"/> is an equality operator and both operands are
    /// tuples of equal arity whose element pairs each support equality. Used both for type
    /// inference and to decide the <see cref="BinaryOpLowering.EqualsCall"/> codegen strategy.
    /// </summary>
    private bool IsTupleEquality(BinaryOperator op, SemanticType left, SemanticType right)
    {
        if (op is not (BinaryOperator.Equal or BinaryOperator.NotEqual))
            return false;

        if (left is not TupleType leftTuple || right is not TupleType rightTuple)
            return false;

        if (leftTuple.ElementTypes.Count != rightTuple.ElementTypes.Count)
            return false;

        for (int i = 0; i < leftTuple.ElementTypes.Count; i++)
        {
            // Each element pair must itself support equality (recursively).
            if (InferBinaryOpType(BinaryOperator.Equal, leftTuple.ElementTypes[i], rightTuple.ElementTypes[i]) == null)
                return false;
        }

        return true;
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
            return new GenericType { Name = BuiltinNames.List };

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
                if (typeName.Contains("Comparable", StringComparison.Ordinal))
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

        TypeSymbol? typeSymbol = left switch
        {
            UserDefinedType udt => udt.Symbol,
            GenericType gt => gt.GenericDefinition,
            _ => null
        };

        if (typeSymbol != null)
        {
            // Try direct operator
            if (typeSymbol.OperatorMethods.TryGetValue(dunderName, out var methods))
            {
                var bestOverload = FindBestOverload(methods, right);
                if (bestOverload != null)
                {
                    // Discovery-loaded generic types (e.g., Counter<T>) may have
                    // operator return types that resolved to 'object' because the
                    // CLR type mapper couldn't represent the self-referential generic
                    // type (Counter<T> returns Counter<T>). In that case, use the
                    // left operand type which is the correctly-instantiated generic.
                    // Only apply for self-returning operators (arithmetic/bitwise),
                    // not comparison operators which should return bool.
                    if (bestOverload.ReturnType == SemanticType.Object && left is GenericType
                        && !IsComparisonOperator(op))
                        return left;
                    return bestOverload.ReturnType;
                }
            }

            // Try equality complement synthesis
            var complementResult = TryInferEqualityComplement(op, typeSymbol, right);
            if (complementResult != null)
                return complementResult;
        }

        return null;
    }

    private static bool IsComparisonOperator(BinaryOperator op) =>
        op is BinaryOperator.Equal or BinaryOperator.NotEqual
            or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
            or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual;

    private SemanticType? TryInferEqualityComplement(BinaryOperator op, TypeSymbol typeSymbol, SemanticType right)
    {
        bool hasEq = typeSymbol.OperatorMethods.ContainsKey(DunderNames.Eq);
        bool hasNe = typeSymbol.OperatorMethods.ContainsKey(DunderNames.Ne);

        if (op == BinaryOperator.Equal && hasNe && !hasEq)
        {
            var neMethods = typeSymbol.OperatorMethods[DunderNames.Ne];
            var bestOverload = FindBestOverload(neMethods, right);
            if (bestOverload != null)
                return SemanticType.Bool;
        }
        else if (op == BinaryOperator.NotEqual && hasEq && !hasNe)
        {
            var eqMethods = typeSymbol.OperatorMethods[DunderNames.Eq];
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

        if (assignableMatch != null)
            return assignableMatch;

        // Find generic parameter match: candidate has TypeParameterType args (e.g., Box[T])
        // and argument is a concrete instantiation (e.g., Box[int])
        if (argumentType is GenericType argGeneric)
        {
            var genericMatch = candidates.FirstOrDefault(c =>
                c.Parameters.Count == 2 &&
                c.Parameters[1].Type is GenericType paramGeneric &&
                paramGeneric.Name == argGeneric.Name &&
                paramGeneric.TypeArguments.Count == argGeneric.TypeArguments.Count &&
                paramGeneric.TypeArguments.Any(a => a is TypeParameterType));

            if (genericMatch != null)
                return genericMatch;
        }

        return null;
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

        // Collect operator candidates from BOTH operand types — C# overload resolution unions
        // the user-defined operators declared on each operand's type before picking the best
        // match. This lets `int + Fraction` see Fraction.op_Addition(long, Fraction) (#887).
        var candidates = new List<System.Reflection.MethodInfo>();
        CollectClrOperators(leftClrType, clrMethodName, candidates);
        if (rightClrType != leftClrType)
            CollectClrOperators(rightClrType, clrMethodName, candidates);

        // Resolve using C# conversion rules: prefer exact parameter matches, then builtin
        // numeric widening (int → long, …), then user-defined op_Implicit (int → Fraction, …).
        var resolved = ResolveClrBinaryOperator(candidates, leftClrType, rightClrType);
        if (resolved != null)
            return MapClrTypeToSemanticType(resolved.ReturnType);

        // Equality fallback: CLR types that implement IEquatable<self>, override Equals(object),
        // or are value types/enums but define no op_Equality still support ==/!=. The result is
        // bool; codegen lowers it to an Equals call (BinaryOpLowering.EqualsCall) because a native
        // C# == would be reference equality (wrong) or fail to compile for elementless types.
        if (IsClrEqualsFallback(op, left, right))
            return SemanticType.Bool;

        return null;
    }

    /// <summary>
    /// Adds the operator overloads named <paramref name="clrMethodName"/> declared on
    /// <paramref name="clrType"/> to <paramref name="into"/>, skipping duplicates.
    /// </summary>
    private void CollectClrOperators(Type clrType, string clrMethodName, List<System.Reflection.MethodInfo> into)
    {
        var operators = _clrMemberCache.GetOperatorMethods(clrType);
        if (operators.TryGetValue(clrMethodName, out var methods))
        {
            foreach (var method in methods)
            {
                if (!into.Contains(method))
                    into.Add(method);
            }
        }
    }

    /// <summary>
    /// Resolves a binary operator overload following C# rules: each candidate's two parameters
    /// must accept the operand types by exact match, builtin numeric widening, or a user-defined
    /// implicit conversion (in that order of preference, lower score = better). Returns the single
    /// best candidate, or null when nothing matches or the best score is shared by candidates with
    /// differing return types (ambiguous → SPY0222).
    /// </summary>
    private System.Reflection.MethodInfo? ResolveClrBinaryOperator(
        List<System.Reflection.MethodInfo> candidates, Type leftClrType, Type rightClrType)
    {
        System.Reflection.MethodInfo? best = null;
        int bestScore = int.MaxValue;
        bool ambiguous = false;

        foreach (var method in candidates)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 2)
                continue;

            var leftRank = ClrConversionRank(leftClrType, parameters[0].ParameterType);
            var rightRank = ClrConversionRank(rightClrType, parameters[1].ParameterType);
            if (leftRank < 0 || rightRank < 0)
                continue;

            var score = leftRank + rightRank;
            if (score < bestScore)
            {
                bestScore = score;
                best = method;
                ambiguous = false;
            }
            else if (score == bestScore && best != null && best.ReturnType != method.ReturnType)
            {
                ambiguous = true;
            }
        }

        return ambiguous ? null : best;
    }

    /// <summary>
    /// Conversion rank from an argument CLR type to a parameter CLR type: 0 = exact,
    /// 1 = reference assignability or builtin numeric widening, 2 = user-defined implicit
    /// conversion (op_Implicit). Returns -1 when no implicit conversion exists. Lower is preferred.
    /// </summary>
    private int ClrConversionRank(Type argClrType, Type paramClrType)
    {
        if (argClrType == paramClrType)
            return 0;

        // Reference / inheritance conversions (derived → base, type → interface).
        if (paramClrType.IsAssignableFrom(argClrType))
            return 1;

        // Builtin numeric widening (int → long, int → double, float → double, …).
        var fromInfo = PrimitiveCatalog.GetByClrType(argClrType);
        var toInfo = PrimitiveCatalog.GetByClrType(paramClrType);
        if (fromInfo != null && toInfo != null && PrimitiveCatalog.CanImplicitlyConvert(fromInfo, toInfo))
            return 1;

        // User-defined implicit conversions: op_Implicit may be declared on either the target
        // (parameter) type or the source (argument) type.
        if (HasImplicitConversion(paramClrType, argClrType, paramClrType)
            || HasImplicitConversion(argClrType, argClrType, paramClrType))
            return 2;

        return -1;
    }

    /// <summary>
    /// Returns true when <paramref name="definingType"/> declares an <c>op_Implicit</c> that
    /// converts from <paramref name="fromType"/> to <paramref name="toType"/>.
    /// </summary>
    private bool HasImplicitConversion(Type definingType, Type fromType, Type toType)
    {
        var operators = _clrMemberCache.GetOperatorMethods(definingType);
        if (!operators.TryGetValue("op_Implicit", out var methods))
            return false;

        foreach (var method in methods)
        {
            var ps = method.GetParameters();
            if (ps.Length == 1 && method.ReturnType == toType
                && ps[0].ParameterType.IsAssignableFrom(fromType))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true when an equality operation (<c>==</c>/<c>!=</c>) compares the None literal
    /// (<see cref="VoidType"/>) against a reference-semantics type (a non-value-type
    /// <see cref="UserDefinedType"/>, <c>str</c>, or a collection <see cref="GenericType"/>).
    /// Such comparisons are a null check and yield <see cref="SemanticType.Bool"/>, lowered by
    /// codegen to a C# null pattern (<see cref="BinaryOpLowering.NoneCheck"/>). Exactly one operand
    /// must be the None literal. NullableType/OptionalType and non-nullable value types are excluded:
    /// the former have dedicated handling, the latter must keep emitting SPY0222 (#901).
    /// <para>
    /// Invariant (#911): a <see cref="VoidType"/> operand reaching equality inference/lowering is
    /// guaranteed to be the <c>None</c> literal — void-returning call operands are rejected earlier
    /// in <c>TypeChecker.CheckBinaryOp</c> with SPY0329 before this classifier or
    /// <see cref="GetBinaryOpLowering"/> runs. (A third consumer, <c>OperatorValidator</c>, reads
    /// the lowering type-wise only to suppress SPY0402; it never selects operands, so it is benign.)
    /// </para>
    /// </summary>
    private static bool IsNoneReferenceEquality(BinaryOperator op, SemanticType left, SemanticType right)
    {
        if (op is not (BinaryOperator.Equal or BinaryOperator.NotEqual))
            return false;

        var leftIsNone = left is VoidType;
        var rightIsNone = right is VoidType;

        // Exactly one operand must be the None literal.
        if (leftIsNone == rightIsNone)
            return false;

        var other = leftIsNone ? right : left;
        return IsNoneCheckReferenceType(other);
    }

    /// <summary>
    /// Reference-semantics classification for the non-None operand of an <c>== None</c>/<c>!= None</c>
    /// comparison: <c>str</c>, collection generics, and class/interface/delegate user-defined types
    /// are reference types; numerics, bools, structs, enums, and unions are value types.
    /// </summary>
    private static bool IsNoneCheckReferenceType(SemanticType type) => type switch
    {
        // Among builtin primitives only str (System.String) is a reference type.
        BuiltinType => type == SemanticType.Str,
        // Collection generics (list/dict/set/...) are reference types.
        GenericType => true,
        UserDefinedType udt => IsReferenceUserType(udt),
        _ => false,
    };

    private static bool IsReferenceUserType(UserDefinedType udt)
    {
        // Prefer CLR type info when available (CLR-discovered types): structs/enums are value types.
        if (udt.ClrType is Type clr)
            return !clr.IsValueType;

        // Sharpy-defined types not yet mapped to a CLR type: classify by declared kind.
        // A null Symbol (error recovery / undiscovered CLR type) conservatively returns
        // false: the comparison is excluded from the NoneCheck lowering and falls through
        // to the pre-existing diagnostics path, never to a silent null check.
        return udt.Symbol?.TypeKind is TypeKind.Class or TypeKind.Interface or TypeKind.Delegate;
    }

    /// <summary>
    /// Returns true when an equality operation (<c>==</c>/<c>!=</c>) on two CLR-backed types
    /// must be resolved via <c>Equals</c> rather than a native operator: the operands denote
    /// the same (or mutually assignable) CLR type, that type defines no <c>op_Equality</c>, and
    /// it nonetheless has meaningful value/structural equality (implements <c>IEquatable&lt;self&gt;</c>,
    /// overrides <c>Equals(object)</c>, or is a value type / enum). Used for both type inference
    /// and the <see cref="BinaryOpLowering.EqualsCall"/> codegen decision.
    /// </summary>
    private bool IsClrEqualsFallback(BinaryOperator op, SemanticType left, SemanticType right)
    {
        if (op is not (BinaryOperator.Equal or BinaryOperator.NotEqual))
            return false;

        // Only CLR-discovered user-defined types use this fallback. Builtin primitives
        // (int, bool, str, …) already resolve via TryInferBuiltinBinaryOp with a native
        // operator and must not be rerouted to Equals just because they are value types.
        if (left is not UserDefinedType || right is not UserDefinedType)
            return false;

        var leftClrType = GetClrType(left);
        var rightClrType = GetClrType(right);
        if (leftClrType == null || rightClrType == null)
            return false;

        // Operands must denote the same CLR type, or be mutually assignable user-defined types.
        if (!left.IsAssignableTo(right) && !right.IsAssignableTo(left))
            return false;

        // Enums support a native C# == (backed by their integer value); they are handled by
        // the enum path during inference and must not be rerouted through a boxing Equals call.
        if (leftClrType.IsEnum || rightClrType.IsEnum)
            return false;

        // If either side defines an op_Equality, the native-operator path owns this case.
        if (DefinesEqualityOperator(leftClrType) || DefinesEqualityOperator(rightClrType))
            return false;

        // Supported when the type gives structural/value equality via Equals: it implements
        // IEquatable<self>, overrides Equals(object), or is a (non-enum) value type — for which
        // a native C# == would not even compile without an op_Equality.
        return ImplementsSelfEquatable(leftClrType) || OverridesObjectEquals(leftClrType)
            || leftClrType.IsValueType;
    }

    private bool DefinesEqualityOperator(Type clrType)
        => _clrMemberCache.GetOperatorMethods(clrType).ContainsKey("op_Equality");

    private static bool ImplementsSelfEquatable(Type clrType)
    {
        var equatable = typeof(IEquatable<>).MakeGenericType(clrType);
        return equatable.IsAssignableFrom(clrType);
    }

    private static bool OverridesObjectEquals(Type clrType)
    {
        var equals = clrType.GetMethod(nameof(object.Equals), new[] { typeof(object) });
        return equals != null && equals.DeclaringType != typeof(object);
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
        if (TypeUtils.IsInteger(operand) && op == UnaryOperator.BitwiseNot)
            return operand;

        // Numeric unary operators
        if (TypeUtils.IsNumeric(operand))
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
        // T | None (C# nullable interop) is iterable as its underlying type (null fails at
        // runtime — Python-parity). OptionalType (T?) is strict and is NOT unwrapped here.
        iterableType = UnwrapNullable(iterableType);

        // Dict view types: items() yields (K, V) tuples, keys() yields K, values() yields V
        if (iterableType is GenericType { Name: BuiltinNames.DictItemsView } itemsView && itemsView.TypeArguments.Count == 2)
            return new TupleType { ElementTypes = new List<SemanticType>(itemsView.TypeArguments) };
        if (iterableType is GenericType { Name: BuiltinNames.DictKeyView } keyView && keyView.TypeArguments.Count == 2)
            return keyView.TypeArguments[0];
        if (iterableType is GenericType { Name: BuiltinNames.DictValuesView } valuesView && valuesView.TypeArguments.Count == 2)
            return valuesView.TypeArguments[1];

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

        // Enum types: iteration yields the enum values
        if (iterableType is UserDefinedType { Symbol.TypeKind: TypeKind.Enum })
        {
            return iterableType;
        }

        // User-defined types with __iter__() or __getitem__()
        if (iterableType is UserDefinedType udt)
        {
            // If Symbol is null (e.g., return type from CLR module discovery),
            // resolve it from the SymbolTable by name
            var symbol = udt.Symbol ?? _symbolTable.Lookup(udt.Name) as TypeSymbol;
            if (symbol != null)
            {
                var iterMethod = symbol.Methods.FirstOrDefault(m => m.Name == DunderNames.Iter);
                if (iterMethod?.ReturnType is GenericType iterReturn
                    && iterReturn.Name == BuiltinNames.Iterator
                    && iterReturn.TypeArguments.Count > 0)
                {
                    return iterReturn.TypeArguments[0];
                }

                var getitemMethod = symbol.Methods.FirstOrDefault(m => m.Name == DunderNames.GetItem);
                if (getitemMethod?.ReturnType is { } itemType && itemType != SemanticType.Unknown)
                {
                    return itemType;
                }

                // Fallback: CLR-backed UserDefinedType implementing IEnumerable<T>
                if (symbol.ClrType != null)
                {
                    var clrElementType = GetIEnumerableElementType(symbol.ClrType);
                    if (clrElementType != null)
                    {
                        return _clrTypeMapper.Value.MapClrTypeToSemanticType(clrElementType);
                    }
                }
            }
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
    /// Infers the element type produced by reversed() on a given type.
    /// Handles standard iterables (list, str, etc.) and user-defined types
    /// with __reversed__() protocol methods.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="InferIterableElementType"/> because reversed
    /// semantics differ from forward iteration — a type may support __reversed__
    /// without supporting __iter__ (or vice versa).
    /// </remarks>
    public SemanticType? InferReversedElementType(SemanticType type)
    {
        // First try standard iterable inference (works for list, str, etc.)
        var iterableElement = InferIterableElementType(type);
        if (iterableElement != null)
            return iterableElement;

        // Check for __reversed__ protocol method on user-defined types
        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            var reversedMethod = udt.Symbol.Methods.FirstOrDefault(
                m => m.Name == DunderNames.Reversed);
            if (reversedMethod?.ReturnType is { } returnType
                && returnType != SemanticType.Unknown
                && returnType != SemanticType.Void)
            {
                // __reversed__ returns the element type directly (it's a generator yielding T)
                return returnType;
            }
        }

        return null;
    }

    /// <summary>
    /// Infers the result type of an index access operation.
    /// Returns null if the type is not indexable.
    /// </summary>
    public SemanticType? InferIndexAccessType(SemanticType container, SemanticType index)
    {
        // T | None (C# nullable interop) exposes the underlying type's protocols, so indexing a
        // nullable yields the element type of T (a null receiver fails at runtime — Python-parity).
        // OptionalType (T?) is intentionally NOT unwrapped: it is strict and the ProtocolValidator
        // reports an actionable narrow/unwrap error instead.
        container = UnwrapNullable(container);

        // Generic containers
        if (container is GenericType generic)
        {
            // For dict, indexing returns value type (second argument)
            if (generic.Name == BuiltinNames.Dict && generic.TypeArguments.Count > 1)
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

        // Bytes - integer indexing returns int (b[0] → 104)
        if (container is UserDefinedType { Name: BuiltinNames.Bytes })
        {
            return SemanticType.Int;
        }

        // User-defined types with __getitem__
        TypeSymbol? typeSymbol = container switch
        {
            UserDefinedType udt => udt.Symbol,
            GenericType gt => gt.GenericDefinition,
            _ => null
        };

        if (typeSymbol != null &&
            (typeSymbol.OperatorMethods.TryGetValue(DunderNames.GetItem, out var getItemMethods) ||
             typeSymbol.ProtocolMethods.TryGetValue(DunderNames.GetItem, out getItemMethods)))
        {
            var bestOverload = FindBestOverload(getItemMethods, index);
            if (bestOverload != null)
                return bestOverload.ReturnType;
            // Fall back to first overload's return type
            if (getItemMethods.Count > 0)
                return getItemMethods[0].ReturnType;
        }

        return null;
    }

    /// <summary>
    /// Infers the element/value type produced by a CLR type's parameterized indexer
    /// (<c>this[...]</c> getter) on a <em>closed</em> CLR type. Used for discovery-backed
    /// types whose indexer return type differs from the standard list/dict shape — e.g.
    /// <c>collections.Counter&lt;T&gt;</c> whose indexer returns <c>int</c> (a count) rather
    /// than <c>T</c>, or <c>ChainMap&lt;K,V&gt;</c> whose indexer returns the closed <c>V</c>.
    /// Because the supplied type is closed, the property type is already substituted.
    /// Returns null when the type exposes no readable indexer.
    /// </summary>
    public SemanticType? InferClrIndexerReturnType(System.Type closedClrType)
    {
        var indexer = closedClrType.GetProperties()
            .FirstOrDefault(p => p.GetIndexParameters().Length > 0 && p.GetGetMethod() != null);
        if (indexer == null)
            return null;

        var mapped = MapClrTypeToSemanticType(indexer.PropertyType);
        return mapped is UnknownType ? null : mapped;
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

    /// <summary>
    /// Infers the result type of hash() call.
    /// Always returns Int — every object supports GetHashCode().
    /// </summary>
    public SemanticType? InferHashType(SemanticType target)
    {
        return SemanticType.Int;
    }

    #endregion

    #region Helper Methods


    private static SemanticType InferPowerResultType(SemanticType left, SemanticType right)
    {
        // Power type promotion:
        // - Both integer types → use numeric promotion (int**int→int, int**long→long, etc.)
        //   Math.Pow returns double, but we cast back to the promoted integer type
        // - Any float involvement → Double
        if (TypeUtils.IsInteger(left) && TypeUtils.IsInteger(right))
            return InferNumericResultType(left, right) ?? left;
        return SemanticType.Double;
    }

    private static SemanticType? InferNumericResultType(SemanticType left, SemanticType right)
    {
        return PrimitiveCatalog.GetPromotedType(left, right);
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

    /// <summary>
    /// Recursively unwraps <see cref="NullableType"/> (C# nullable interop) wrappers for
    /// container/protocol-position inference (indexing, iteration). <see cref="OptionalType"/>
    /// is deliberately NOT unwrapped — T? is strict and must be narrowed/unwrapped explicitly.
    /// Not applied to general assignability.
    /// </summary>
    private static SemanticType UnwrapNullable(SemanticType type) => type switch
    {
        NullableType nt => UnwrapNullable(nt.UnderlyingType),
        _ => type
    };

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

    /// <summary>
    /// Extracts the element type T from a CLR type implementing IEnumerable&lt;T&gt;.
    /// Returns null if the type does not implement IEnumerable&lt;T&gt;.
    /// </summary>
    private static Type? GetIEnumerableElementType(Type clrType)
    {
        // Check interfaces implemented by the type for IEnumerable<T>
        foreach (var iface in clrType.GetInterfaces())
        {
            if (iface.IsGenericType &&
                iface.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IEnumerable<>))
            {
                return iface.GetGenericArguments()[0];
            }
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
        if (clrType == typeof(decimal))
            return SemanticType.Decimal;
        if (clrType == typeof(bool))
            return SemanticType.Bool;
        if (clrType == typeof(string))
            return SemanticType.Str;
        if (clrType == typeof(void))
            return SemanticType.Void;
        // Delegate to ClrTypeMapper for Sharpy-namespace and other CLR types
        // so that operator return types like Sharpy.DateTime map back to `datetime`
        // instead of collapsing to `object`.
        return _clrTypeMapper.Value.MapClrTypeToSemanticType(clrType);
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

    /// <summary>
    /// Checks whether a SemanticType represents an enum type.
    /// Handles both fully-resolved types (Symbol.TypeKind == Enum) and partially-resolved
    /// types from cross-module imports where Symbol may be null. In the latter case,
    /// falls back to looking up the type name in the SymbolTable.
    /// </summary>
    private bool IsEnumType(SemanticType type)
    {
        if (type is not UserDefinedType udt)
            return false;

        // Fast path: Symbol is already resolved with TypeKind
        if (udt.Symbol is { TypeKind: TypeKind.Enum })
            return true;

        // Cross-module fallback: ModuleLoader creates UserDefinedType without Symbol
        // for field types in imported classes. Look up the type name in the SymbolTable
        // to resolve the actual TypeKind.
        if (udt.Symbol == null && udt.Name != null)
        {
            var symbol = _symbolTable.Lookup(udt.Name);
            if (symbol is TypeSymbol { TypeKind: TypeKind.Enum })
                return true;
        }

        return false;
    }
}
