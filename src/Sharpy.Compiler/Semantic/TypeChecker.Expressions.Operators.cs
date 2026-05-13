using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Binary/unary operators, comparisons, type casts, type checks, try/maybe expressions
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckBinaryOp(BinaryOp binOp)
    {
        // Handle pipe forward operator specially - it's a syntactic transformation, not a regular operator
        if (binOp.Operator == BinaryOperator.PipeForward)
        {
            return CheckPipeForward(binOp);
        }

        if (binOp.Operator == BinaryOperator.And)
        {
            return CheckBooleanAndOp(binOp);
        }

        var leftType = CheckExpression(binOp.Left);
        var rightType = CheckExpression(binOp.Right);

        // If either operand is Unknown, return Unknown to avoid cascading errors
        if (leftType is UnknownType || rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Use TypeInferenceService for type inference
        var resultType = _typeInference.InferBinaryOpType(binOp.Operator, leftType, rightType);

        // If type inference fails, report the error directly
        // (validators may not catch all type incompatibilities)
        if (resultType == null)
        {
            AddError(
                $"Type '{leftType.GetDisplayName()}' does not support operator '{GetOperatorSymbol(binOp.Operator)}' with operand of type '{rightType.GetDisplayName()}'",
                binOp.LineStart,
                binOp.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                span: binOp.Span);
            return SemanticType.Unknown;
        }

        // Warn when is/is not is used with value types — identity comparison is
        // meaningless because value types are boxed, so the result is always False.
        if (binOp.Operator is BinaryOperator.Is or BinaryOperator.IsNot)
        {
            var effectiveLeft = leftType is NullableType nl ? nl.UnderlyingType : leftType;
            var effectiveRight = rightType is NullableType nr ? nr.UnderlyingType : rightType;

            if (effectiveLeft.IsValueType && effectiveRight.IsValueType)
            {
                var opSymbol = binOp.Operator == BinaryOperator.Is ? "is" : "is not";
                _diagnostics.AddWarning(
                    $"'{opSymbol}' used with value types '{leftType.GetDisplayName()}' and " +
                    $"'{rightType.GetDisplayName()}' — identity comparison is always False " +
                    "due to boxing; use '==' or '!=' instead",
                    binOp.LineStart,
                    binOp.ColumnStart,
                    _currentFilePath,
                    code: DiagnosticCodes.Validation.IsWithValueTypes,
                    phase: CompilerPhase.TypeChecking);
            }
        }

        return resultType;
    }

    private SemanticType CheckBooleanAndOp(BinaryOp andOp)
    {
        var leftType = CheckExpression(andOp.Left);

        if (leftType is UnknownType)
        {
            CheckExpression(andOp.Right);
            return SemanticType.Unknown;
        }

        var (leftNarrowed, _) = ExtractNarrowedTypes(andOp.Left, true);

        SemanticType rightType;
        using (_narrowingContext.EnterScope())
        {
            _narrowingContext.ApplyNarrowings(leftNarrowed);
            rightType = CheckExpression(andOp.Right);
        }

        if (rightType is UnknownType)
            return SemanticType.Unknown;

        var resultType = _typeInference.InferBinaryOpType(BinaryOperator.And, leftType, rightType);
        if (resultType == null)
        {
            AddError(
                $"Type '{leftType.GetDisplayName()}' does not support operator 'and' with operand of type '{rightType.GetDisplayName()}'",
                andOp.LineStart,
                andOp.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                span: andOp.Span);
            return SemanticType.Unknown;
        }

        return resultType;
    }

    /// <summary>
    /// Type-checks the pipe forward operator (|>).
    /// x |> f → f(x)
    /// x |> f(y) → f(x, y) (prepend x to argument list)
    /// x |> f |> g → g(f(x)) (chains via left-associativity)
    /// </summary>
    private SemanticType CheckPipeForward(BinaryOp binOp)
    {
        var leftType = CheckExpression(binOp.Left);

        if (leftType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Case 1: x |> f(y, z) - right side is already a function call
        // We need to re-validate with x prepended to arguments
        if (binOp.Right is FunctionCall funcCall)
        {
            return CheckPipeForwardWithFunctionCall(leftType, funcCall, binOp);
        }

        // Case 2: x |> f - right side is an identifier or expression that should be callable
        // Pre-check: if right side is an identifier that resolves to a TypeSymbol, emit the
        // constructor-pipe error immediately. CheckExpression returns UnknownType for non-primitive
        // TypeSymbols, which would hide this case behind a silent early return.
        if (binOp.Right is Identifier preId)
        {
            var preSymbol = _symbolTable.Lookup(preId.Name);
            if (preSymbol is TypeSymbol)
            {
                AddError("Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }
        }

        var rightType = CheckExpression(binOp.Right);

        if (rightType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Check if right side is a function type
        if (rightType is FunctionType ft)
        {
            // Validate that the function accepts leftType as first argument
            if (ft.ParameterTypes.Count < 1)
            {
                AddError($"Pipe target function takes no arguments, cannot pipe a value to it",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }

            if (!leftType.IsAssignableTo(ft.ParameterTypes[0]))
            {
                AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function expecting '{ft.ParameterTypes[0].GetDisplayName()}'",
                    binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: binOp.Span);
                return SemanticType.Unknown;
            }

            return ft.ReturnType;
        }

        // If right side is an identifier, look up the function symbol
        if (binOp.Right is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Validate argument count
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);

                if (requiredParamCount < 1)
                {
                    AddError($"Pipe target function '{id.Name}' takes no required arguments, cannot pipe a value to it",
                        binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                        span: binOp.Right.Span);
                    return SemanticType.Unknown;
                }

                // Validate the piped value type matches first parameter
                var firstParam = funcSymbol.Parameters[0];
                if (!leftType.IsAssignableTo(firstParam.Type))
                {
                    AddError($"Cannot pipe value of type '{leftType.GetDisplayName()}' to function '{id.Name}' expecting '{firstParam.Type.GetDisplayName()}'",
                        binOp.LineStart, binOp.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: binOp.Span);
                    return SemanticType.Unknown;
                }

                // Check if remaining required args are satisfied (they must all have defaults)
                if (requiredParamCount > 1)
                {
                    AddError($"Function '{id.Name}' requires {requiredParamCount} arguments but only 1 is provided via pipe",
                        binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: binOp.Right.Span);
                    return SemanticType.Unknown;
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass → SomeClass(x)
                // This is allowed, handled similarly to function call
                AddError("Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }

            AddError($"'{id.Name}' is not callable",
                binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                span: binOp.Right.Span);
            return SemanticType.Unknown;
        }

        // Right side is some other expression that's not callable
        AddError($"Pipe target must be callable, got '{rightType.GetDisplayName()}'",
            binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
            span: binOp.Right.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Type-checks a pipe forward where the right side is a function call.
    /// x |> f(y, z) → f(x, y, z) - prepend piped value to argument list.
    /// </summary>
    private SemanticType CheckPipeForwardWithFunctionCall(SemanticType pipedType, FunctionCall call, BinaryOp binOp)
    {
        // Get the function being called
        var calleeType = CheckExpression(call.Function);

        // Collect existing argument types
        var existingArgTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
        {
            existingArgTypes.Add(CheckExpression(arg));
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
        }

        // Build the full argument list: piped value + existing args
        var allArgTypes = new List<SemanticType> { pipedType };
        allArgTypes.AddRange(existingArgTypes);

        // Total argument count includes piped value, positional args, and keyword args
        var totalArgCount = allArgTypes.Count + kwargTypes.Count;

        // Try to resolve the function symbol for better validation
        if (call.Function is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);

            if (symbol is FunctionSymbol funcSymbol)
            {
                // Record the resolved call target for codegen
                _semanticInfo.SetCallTarget(call, funcSymbol);

                // Validate argument count considering variadic and keyword-only params
                var hasVariadicParam = funcSymbol.Parameters.Any(p => p.IsVariadic);
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
                var totalParamCount = funcSymbol.Parameters.Count;
                var positionalParamCount = funcSymbol.Parameters.Count(p => !p.IsKeywordOnly);

                var tooFew = totalArgCount < requiredParamCount;
                var tooManyPositional = !hasVariadicParam && allArgTypes.Count > positionalParamCount;
                var tooMany = !hasVariadicParam && totalArgCount > totalParamCount;

                if (tooFew || tooMany || tooManyPositional)
                {
                    if (hasVariadicParam)
                    {
                        AddError($"Function '{id.Name}' expects at least {requiredParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                            span: call.Span);
                    }
                    else if (requiredParamCount == totalParamCount)
                    {
                        AddError($"Function '{id.Name}' expects {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                            span: call.Span);
                    }
                    else
                    {
                        AddError($"Function '{id.Name}' expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount} (including piped value)",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                            span: call.Span);
                    }
                    return SemanticType.Unknown;
                }

                // Validate positional argument types (piped value + call.Arguments)
                var variadicParamIndex = funcSymbol.Parameters.ToList().FindIndex(p => p.IsVariadic);
                for (int i = 0; i < allArgTypes.Count; i++)
                {
                    var argType = allArgTypes[i];
                    ParameterSymbol param;
                    if (variadicParamIndex >= 0 && i >= variadicParamIndex)
                    {
                        param = funcSymbol.Parameters[variadicParamIndex];
                    }
                    else if (i < funcSymbol.Parameters.Count)
                    {
                        param = funcSymbol.Parameters[i];
                    }
                    else
                    {
                        break; // Shouldn't happen due to tooMany check
                    }

                    if (!argType.IsAssignableTo(param.Type))
                    {
                        var argDesc = i == 0 ? "piped value" : $"argument {i}";
                        var argNode = i == 0 ? binOp.Left : call.Arguments[i - 1];
                        AddError($"Cannot pass {argDesc} of type '{argType.GetDisplayName()}' to parameter '{param.Name}' of type '{param.Type.GetDisplayName()}'",
                            argNode.LineStart,
                            argNode.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: argNode.Span);
                    }
                }

                // Validate keyword arguments
                foreach (var kwarg in call.KeywordArguments)
                {
                    var param = funcSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                    if (param == null)
                    {
                        AddError($"Unknown keyword argument '{kwarg.Name}'",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                            span: kwarg.Value.Span);
                    }
                    else
                    {
                        // Check if this parameter was already provided positionally (including piped value)
                        var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
                        if (paramIndex < allArgTypes.Count)
                        {
                            AddError($"Argument '{kwarg.Name}' was already provided positionally",
                                kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                                span: kwarg.Value.Span);
                        }
                        else if (!IsAssignable(kwargTypes[kwarg.Name], param.Type))
                        {
                            AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{param.Type.GetDisplayName()}'",
                                kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                                span: kwarg.Value.Span);
                        }
                    }
                }

                return funcSymbol.ReturnType;
            }

            if (symbol is TypeSymbol typeSymbol)
            {
                // Constructor call via pipe - x |> SomeClass(y) → SomeClass(x, y)
                AddError("Piping to constructors is not yet supported",
                    binOp.Right.LineStart, binOp.Right.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
                    span: binOp.Right.Span);
                return SemanticType.Unknown;
            }

            if (symbol != null)
            {
                AddError($"'{id.Name}' is not callable",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                    span: call.Function.Span);
                return SemanticType.Unknown;
            }
        }

        // Fallback: check if callee is a FunctionType
        if (calleeType is FunctionType ft)
        {
            if (totalArgCount != ft.ParameterTypes.Count)
            {
                AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount} (including piped value)",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
                return SemanticType.Unknown;
            }

            // Validate positional argument types
            for (int i = 0; i < allArgTypes.Count; i++)
            {
                if (!allArgTypes[i].IsAssignableTo(ft.ParameterTypes[i]))
                {
                    var argDesc = i == 0 ? "piped value" : $"argument {i}";
                    var argNode = i == 0 ? binOp.Left : call.Arguments[i - 1];
                    AddError($"Cannot pass {argDesc} of type '{allArgTypes[i].GetDisplayName()}' where '{ft.ParameterTypes[i].GetDisplayName()}' is expected",
                        argNode.LineStart,
                        argNode.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: argNode.Span);
                }
            }

            return ft.ReturnType;
        }

        AddError($"Pipe target must be callable, got '{calleeType.GetDisplayName()}'",
            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.InvalidPipeTarget,
            span: binOp.Right.Span);
        return SemanticType.Unknown;
    }

    private SemanticType CheckUnaryOp(UnaryOp unOp)
    {
        var operandType = CheckExpression(unOp.Operand);

        // If operand is Unknown, return Unknown to avoid cascading errors
        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Use TypeInferenceService for type inference
        var resultType = _typeInference.InferUnaryOpType(unOp.Operator, operandType);

        // If type inference fails, report the error directly
        if (resultType == null)
        {
            AddError(
                $"Type '{operandType.GetDisplayName()}' does not support unary operator '{GetOperatorSymbol(unOp.Operator)}'",
                unOp.LineStart,
                unOp.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidUnaryOperation,
                span: unOp.Span);
            return SemanticType.Unknown;
        }

        return resultType;
    }

    private SemanticType CheckComparisonChain(ComparisonChain chain)
    {
        // A comparison chain like "a < b < c" has:
        // - Operands: [a, b, c]
        // - Operators: [LessThan, LessThan]
        // We need to validate each adjacent pair: (a < b) and (b < c)

        // Validate chain structure: operators count should equal operands count minus 1
        // (e.g., 3 operands need 2 operators: a < b < c)
        if (chain.Operands.Length < 2 || chain.Operators.Length != chain.Operands.Length - 1)
        {
            // Malformed chain, just return bool and let parser handle errors
            return SemanticType.Bool;
        }

        // Check all operands and build their types
        var operandTypes = new List<SemanticType>();
        for (int i = 0; i < chain.Operands.Length; i++)
        {
            operandTypes.Add(CheckExpression(chain.Operands[i]));
        }

        // Validate each comparison pair
        for (int i = 0; i < chain.Operators.Length; i++)
        {
            var leftType = operandTypes[i];
            var rightType = operandTypes[i + 1];

            // Skip validation if either operand is Unknown to avoid cascading errors
            if (leftType is UnknownType || rightType is UnknownType)
            {
                continue;
            }

            // Map ComparisonOperator to BinaryOperator and validate
            var binaryOp = TypeUtils.ComparisonOperatorToBinaryOperator(chain.Operators[i]);
            var resultType = _typeInference.InferBinaryOpType(binaryOp, leftType, rightType);

            // If type inference fails, report the error directly
            if (resultType == null)
            {
                AddError(
                    $"Type '{leftType.GetDisplayName()}' does not support operator '{GetOperatorSymbol(binaryOp)}' with operand of type '{rightType.GetDisplayName()}'",
                    chain.Operands[i].LineStart,
                    chain.Operands[i].ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidBinaryOperation,
                    span: chain.Span);
            }
        }

        // All comparison chains return bool
        return SemanticType.Bool;
    }

    private SemanticType CheckConditionalExpression(ConditionalExpression cond)
    {
        CheckExpression(cond.Test);
        var thenType = CheckExpression(cond.ThenValue);
        var elseType = CheckExpression(cond.ElseValue);

        // Return common type
        if (thenType.IsAssignableTo(elseType))
            return elseType;
        if (elseType.IsAssignableTo(thenType))
            return thenType;

        // Intentional Unknown without error: when then/else branch types are incompatible
        // (e.g., `1 if cond else "str"`), we return Unknown rather than emitting an error
        // because the LCA (least common ancestor) logic is limited. Mark as error recovery
        // to suppress SPY0907 — a proper fix would compute LCA or emit a type mismatch error.
        MarkExpressionAsErrorRecovery(cond);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Type-checks a type coercion expression (value to Type).
    /// Validates that the coercion is valid per the language specification.
    /// </summary>
    private SemanticType CheckTypeCoercion(TypeCoercion coercion)
    {
        var sourceType = CheckExpression(coercion.Value);
        var targetType = _typeResolver.ResolveTypeAnnotation(coercion.TargetType);

        // If either type is unknown, skip validation to avoid cascading errors
        if (sourceType is UnknownType || targetType is UnknownType)
        {
            return targetType;
        }

        // Get the underlying target type (strip nullable/optional wrapper if present)
        var underlyingTargetType = targetType switch
        {
            NullableType nullable => nullable.UnderlyingType,
            OptionalType optional => optional.UnderlyingType,
            _ => targetType
        };

        // Validate the coercion
        ValidateTypeCoercion(coercion, sourceType, underlyingTargetType);

        return targetType;
    }

    /// <summary>
    /// Validates that a type coercion is valid per the language specification.
    /// Reports errors for invalid casts.
    /// </summary>
    private void ValidateTypeCoercion(TypeCoercion coercion, SemanticType sourceType, SemanticType targetType)
    {
        // Unboxing: object to any type is valid (runtime check) - check this first
        if (IsObjectType(sourceType))
        {
            return; // Valid
        }

        // Numeric to numeric conversions are always valid (may throw at runtime for narrowing)
        if (PrimitiveCatalog.IsNumeric(sourceType) && PrimitiveCatalog.IsNumeric(targetType))
        {
            return; // Valid
        }

        // Check for invalid numeric/bool to string conversion
        // This is a common mistake - users should use str(x) instead
        if (IsStringType(targetType))
        {
            var sourceInfo = PrimitiveCatalog.GetPrimitiveInfo(sourceType);
            if (sourceInfo != null && sourceInfo.ClrType != typeof(string))
            {
                // Source is a primitive but not string - reject
                AddError(
                    $"Cannot cast '{sourceType.GetDisplayName()}' to 'str'. Use str(...) instead.",
                    coercion.LineStart, coercion.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidCast,
                    span: coercion.Span);
                return;
            }
        }

        // Check for user-defined __explicit__ conversion operators
        if (HasUserDefinedConversion(sourceType, targetType, DunderNames.Explicit)
            || HasUserDefinedConversion(targetType, sourceType, DunderNames.Explicit))
        {
            return; // Valid — C# will invoke the user-defined explicit operator
        }

        // Check for valid reference type casts (inheritance relationship or interface implementation)
        if (!CanPotentiallyCast(sourceType, targetType))
        {
            AddError(
                $"Cannot cast '{sourceType.GetDisplayName()}' to '{targetType.GetDisplayName()}' (no inheritance relationship).",
                coercion.LineStart, coercion.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidCast,
                span: coercion.Span);
        }
    }

    /// <summary>
    /// Returns true if the type is the str/string type.
    /// </summary>
    private static bool IsStringType(SemanticType type)
    {
        return type is BuiltinType builtin && (builtin.Name == BuiltinNames.Str || builtin.Name == "string");
    }

    /// <summary>
    /// Returns true if the type is the object type.
    /// </summary>
    private static bool IsObjectType(SemanticType type)
    {
        return type is BuiltinType { Name: "object" } or UserDefinedType { Name: "object" };
    }

    /// <summary>
    /// Determines if a cast between two types COULD potentially succeed at runtime.
    /// Returns true if there's an inheritance relationship, interface implementation, or unboxing potential.
    /// Returns false if the cast is statically impossible.
    /// </summary>
    private bool CanPotentiallyCast(SemanticType source, SemanticType target)
    {
        // Same type is always castable
        if (source.Equals(target))
            return true;

        // Both must be user-defined types for inheritance checks
        if (source is UserDefinedType sourceUdt && target is UserDefinedType targetUdt)
        {
            // Check if source inherits from target (downcast - always safe)
            if (InheritsFrom(sourceUdt.Symbol, targetUdt.Symbol))
                return true;

            // Check if target inherits from source (upcast - runtime check)
            if (InheritsFrom(targetUdt.Symbol, sourceUdt.Symbol))
                return true;

            // Check if target is an interface that could be implemented
            if (targetUdt.Symbol?.TypeKind == TypeKind.Interface)
                return true;

            // Check if source is an interface that the target could implement
            if (sourceUdt.Symbol?.TypeKind == TypeKind.Interface)
                return true;

            // No relationship found
            return false;
        }

        // Interface casting is always potentially valid at runtime
        if (source is UserDefinedType && target is UserDefinedType targetType && targetType.Symbol?.TypeKind == TypeKind.Interface)
            return true;

        // Unboxing from object is always valid
        if (IsObjectType(source))
            return true;

        // Boxing to object is always valid
        if (IsObjectType(target))
            return true;

        // For generic types, check the base definition
        if (source is GenericType sourceGeneric && target is GenericType targetGeneric)
        {
            // Same generic definition with potentially different type args
            if (sourceGeneric.GenericDefinition?.Name == targetGeneric.GenericDefinition?.Name)
                return true;
        }

        // Default: allow if types don't fit the checked categories (to be conservative)
        // This handles edge cases and allows the C# compiler to do final validation
        return true;
    }

    private bool HasUserDefinedConversion(SemanticType sourceType, SemanticType targetType, string dunderName)
    {
        var typeSymbol = sourceType switch
        {
            UserDefinedType udt => udt.Symbol,
            _ => null
        };

        if (typeSymbol == null)
            return false;

        foreach (var method in typeSymbol.Methods)
        {
            if (method.Name != dunderName || !method.IsStatic)
                continue;

            if (method.Parameters.Count == 1 && method.ReturnType != null)
            {
                if (method.ReturnType.Equals(targetType))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a type symbol inherits from another type symbol (directly or indirectly).
    /// </summary>
    private bool InheritsFrom(TypeSymbol? derived, TypeSymbol? baseType)
        => TypeHierarchyService.InheritsFrom(derived, baseType, SemanticBinding);

    private SemanticType CheckTypeCheck(TypeCheck typeCheck)
    {
        CheckExpression(typeCheck.Value);
        _typeResolver.ResolveTypeAnnotation(typeCheck.CheckType);
        return SemanticType.Bool;
    }

    /// <summary>
    /// Type-checks a maybe expression: maybe expr.
    /// The operand must be a NullableType (T | None). The result is OptionalType wrapping the underlying type.
    /// </summary>
    private SemanticType CheckMaybeExpression(MaybeExpression maybeExpr)
    {
        var operandType = CheckExpression(maybeExpr.Operand);

        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        if (operandType is not NullableType nullable)
        {
            AddError(
                $"'maybe' expression requires a nullable type (T | None), but got '{operandType.GetDisplayName()}'",
                maybeExpr.LineStart, maybeExpr.ColumnStart, code: DiagnosticCodes.Semantic.InvalidMaybeExpression,
                span: maybeExpr.Span);
            return SemanticType.Unknown;
        }

        if (nullable.UnderlyingType is TypeParameterType typeParam
            && !typeParam.Constraints.Any(c => c is ClassConstraint or StructConstraint))
        {
            AddError(
                $"'maybe' cannot be used with unconstrained generic type parameter '{typeParam.Name}'. The type parameter must be constrained to either a reference type or value type.",
                maybeExpr.LineStart, maybeExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.MaybeOnUnconstrainedTypeParameter,
                span: maybeExpr.Span);
            return SemanticType.Unknown;
        }

        return new OptionalType { UnderlyingType = nullable.UnderlyingType };
    }

    /// <summary>
    /// Type-checks a try expression: try expr or try[ExceptionType] expr.
    /// Wraps the operand in Result[T, E] where T is the operand type and E is the exception type.
    /// Default E is Exception, except for 'to' expressions where it's InvalidCastException.
    /// </summary>
    private SemanticType CheckTryExpression(TryExpression tryExpr)
    {
        var operandType = CheckExpression(tryExpr.Operand);

        if (operandType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Determine the error type
        SemanticType errorType;
        if (tryExpr.ExceptionTypes.Length == 1)
        {
            // Explicit single exception type: try[ValueError] expr
            errorType = _typeResolver.ResolveTypeAnnotation(tryExpr.ExceptionTypes[0]);
        }
        else if (tryExpr.Operand is TypeCoercion)
        {
            // Special case: try x to Cat → Result[Cat, InvalidCastException]
            var clrSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("InvalidCastException");
            errorType = new UserDefinedType { Name = "InvalidCastException", Symbol = clrSymbol };
        }
        else if (_expectedType is ResultType expectedResult)
        {
            // RFC 3721: infer error type from expected type context (return statement
            // or variable annotation with Result type)
            errorType = expectedResult.ErrorType;
        }
        else if (_currentFunctionReturnType is ResultType enclosingResult
            && _expectedType == null)
        {
            // RFC 3721: infer error type from enclosing function's Result return type
            // Only when there's no explicit type context (e.g., variable annotation)
            errorType = enclosingResult.ErrorType;
        }
        else
        {
            // Default: try expr → Result[T, Exception]
            var clrSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("Exception");
            errorType = new UserDefinedType { Name = "Exception", Symbol = clrSymbol };
        }

        return new ResultType { OkType = operandType, ErrorType = errorType };
    }

    /// <summary>
    /// Extract narrowed types from a conditional expression
    /// </summary>

    /// <summary>
    /// Gets the human-readable symbol for a binary operator.
    /// </summary>
    private static string GetOperatorSymbol(BinaryOperator op) => op switch
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
        BinaryOperator.LessThan => "<",
        BinaryOperator.LessThanOrEqual => "<=",
        BinaryOperator.GreaterThan => ">",
        BinaryOperator.GreaterThanOrEqual => ">=",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.And => "and",
        BinaryOperator.Or => "or",
        BinaryOperator.Is => "is",
        BinaryOperator.IsNot => "is not",
        BinaryOperator.In => "in",
        BinaryOperator.NotIn => "not in",
        BinaryOperator.NullCoalesce => "??",
        BinaryOperator.PipeForward => "|>",
        _ => op.ToString()
    };

    /// <summary>
    /// Gets the human-readable symbol for a unary operator.
    /// </summary>
    private static string GetOperatorSymbol(UnaryOperator op) => op switch
    {
        UnaryOperator.Minus => "-",
        UnaryOperator.Plus => "+",
        UnaryOperator.Not => "not",
        UnaryOperator.BitwiseNot => "~",
        _ => op.ToString()
    };
}
