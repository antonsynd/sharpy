using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Expression checking (operators, member access, calls, collections)
/// </summary>
internal partial class TypeChecker
{
    public SemanticType CheckExpression(Expression expr)
    {
        // Periodic cancellation check (every N expressions)
        CheckCancellation();

        // Check cache
        var cached = _semanticInfo.GetExpressionType(expr);
        if (cached != null)
            return cached;

        // Track error count and error recovery marks before checking — if errors are
        // emitted or sub-expressions are marked as error recovery during this expression's
        // check and the result is UnknownType, it's error recovery (expected Unknown).
        int errorsBefore = _diagnostics.ErrorCount;
        int recoveryBefore = _errorRecoveryMarkCount;

        SemanticType type = expr switch
        {
            IntegerLiteral => SemanticType.Int,
            FloatLiteral => SemanticType.Double,
            StringLiteral => SemanticType.Str,
            BooleanLiteral => SemanticType.Bool,
            NoneLiteral => SemanticType.Void,
            Identifier id => CheckIdentifier(id),
            BinaryOp binOp => CheckBinaryOp(binOp),
            UnaryOp unOp => CheckUnaryOp(unOp),
            ComparisonChain chain => CheckComparisonChain(chain),
            SuperExpression superExpr => CheckSuperExpression(superExpr),
            MemberAccess memberAccess => CheckMemberAccess(memberAccess),
            IndexAccess indexAccess => CheckIndexAccess(indexAccess),
            FunctionCall call => CheckFunctionCall(call),
            ListLiteral list => CheckListLiteral(list),
            DictLiteral dict => CheckDictLiteral(dict),
            SetLiteral set => CheckSetLiteral(set),
            TupleLiteral tuple => CheckTupleLiteral(tuple),
            ListComprehension listComp => CheckListComprehension(listComp),
            SetComprehension setComp => CheckSetComprehension(setComp),
            DictComprehension dictComp => CheckDictComprehension(dictComp),
            ConditionalExpression cond => CheckConditionalExpression(cond),
            LambdaExpression lambda => CheckLambda(lambda),
            TypeCast cast => CheckTypeCast(cast),
            TypeCoercion coercion => CheckTypeCoercion(coercion),
            TypeCheck typeCheck => CheckTypeCheck(typeCheck),
            MaybeExpression maybeExpr => CheckMaybeExpression(maybeExpr),
            TryExpression tryExpr => CheckTryExpression(tryExpr),
            Parenthesized paren => CheckExpression(paren.Expression),
            FStringLiteral fstr => CheckFStringLiteral(fstr),
            EllipsisLiteral => SemanticType.Void,
            SliceAccess sliceAccess => CheckSliceAccess(sliceAccess),
            WalrusExpression walrus => CheckWalrusExpression(walrus),
            _ => HandleUnrecognizedExpression(expr)
        };

        // Track error recovery: if the result is UnknownType and either new errors were
        // emitted or sub-expressions were marked as error recovery, mark this expression
        // as error recovery too. This enables transitive propagation — if MathUtil.square
        // returns Unknown because MathUtil (a TypeSymbol ref) was Unknown, the MemberAccess
        // also gets marked as error recovery.
        if (type is UnknownType &&
            (_diagnostics.ErrorCount > errorsBefore || _errorRecoveryMarkCount > recoveryBefore))
        {
            MarkExpressionAsErrorRecovery(expr);
        }

        // Cache the result
        _semanticInfo.SetExpressionType(expr, type);
        return type;
    }

    private SemanticType HandleUnrecognizedExpression(Expression expr)
    {
        AddError(
            $"Internal: unrecognized expression type '{expr.GetType().Name}'. This is a compiler bug — please report it.",
            expr.LineStart,
            expr.ColumnStart,
            DiagnosticCodes.Semantic.UnrecognizedExpressionType,
            expr.Span);
        return SemanticType.Unknown;
    }

    private SemanticType CheckIdentifier(Identifier id)
    {
        // Special validation for 'self' - must be used inside an instance method
        if (id.Name == "self")
        {
            if (_currentClass == null)
            {
                AddError("'self' can only be used inside instance methods",
                    id.LineStart, id.ColumnStart, code: DiagnosticCodes.Semantic.InvalidSelfUsage,
                    span: id.Span);
                return SemanticType.Unknown;
            }
            // Normal identifier lookup will follow and find the self parameter
        }

        // 'Nothing' was removed — users should use None() for Optional construction
        if (id.Name == "Nothing")
        {
            AddError("'Nothing' is not a valid identifier. Use 'None()' to construct an empty Optional, or 'None' for the null literal",
                id.LineStart, id.ColumnStart, code: DiagnosticCodes.Semantic.InvalidNothingUsage,
                span: id.Span);
            return SemanticType.Unknown;
        }

        var symbol = _symbolTable.Lookup(id.Name);
        if (symbol == null)
        {
            // Don't error on tagged union constructors — they're handled by CheckFunctionCall
            if (_symbolTable.BuiltinRegistry.IsTaggedUnionConstructor(id.Name))
            {
                // These are function-call constructors, not bare identifiers.
                // If we get here, it means the user wrote e.g. 'x = Some' without calling it.
                AddError($"'{id.Name}' must be called as a function, e.g. '{id.Name}(value)'",
                    id.LineStart, id.ColumnStart, code: DiagnosticCodes.Semantic.NotCallable,
                    span: id.Span);
                return SemanticType.Unknown;
            }

            // Check if this identifier is a root cause (e.g., from a failed import).
            // If so, suppress the error - the root cause was already reported.
            // Mark as error recovery so the Unknown type doesn't trigger SPY0907.
            if (_diagnostics.IsRootCause(id.Name))
            {
                MarkExpressionAsErrorRecovery(id);
                return SemanticType.Unknown;
            }

            var message = $"Undefined identifier '{id.Name}'";
            var suggestion = FindSuggestion(id.Name);
            if (suggestion != null)
                message += $". Did you mean '{suggestion}'?";
            AddError(message,
                id.LineStart, id.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedVariable,
                span: id.Span);
            return SemanticType.Unknown;
        }

        // Check if this is an error recovery symbol (from a failed import).
        // If so, suppress cascading errors - the import error was already reported.
        if (symbol.IsErrorRecovery)
        {
            _semanticInfo.SetIdentifierSymbol(id, symbol);
            // Mark as error recovery — the import error was already reported upstream
            MarkExpressionAsErrorRecovery(id);
            return SemanticType.Unknown;
        }

        _semanticInfo.SetIdentifierSymbol(id, symbol);

        // Check if this identifier has a narrowed type in the current context
        var narrowedType = _narrowingContext.GetNarrowedType(id.Name);
        if (narrowedType != null)
        {
            // Persist the narrowed type for code generation
            // This allows RoslynEmitter to use the narrowed type when generating code
            _semanticInfo.SetNarrowedType(id, narrowedType);
            return narrowedType;
        }

        var identifierType = symbol switch
        {
            VariableSymbol varSymbol => GetVariableType(varSymbol),
            FunctionSymbol funcSymbol => new FunctionType
            {
                ParameterTypes = funcSymbol.Parameters.Select(p => p.Type).ToList(),
                ReturnType = funcSymbol.ReturnType
            },
            ModuleSymbol moduleSymbol => new ModuleType { Symbol = moduleSymbol },
            // Type names used as values (constructors) are resolved at the FunctionCall level,
            // not at the identifier level. Mark as error recovery since this is an expected gap.
            TypeSymbol => SemanticType.Unknown,
            _ => SemanticType.Unknown
        };

        // Mark intentional Unknown types: TypeSymbol references and unhandled symbol kinds
        // are not errors — they're expected gaps handled at higher levels (e.g., FunctionCall).
        if (identifierType is UnknownType && symbol is not null)
        {
            MarkExpressionAsErrorRecovery(id);
        }

        return identifierType;
    }

    private SemanticType CheckBinaryOp(BinaryOp binOp)
    {
        // Handle pipe forward operator specially - it's a syntactic transformation, not a regular operator
        if (binOp.Operator == BinaryOperator.PipeForward)
        {
            return CheckPipeForward(binOp);
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
                AddError($"Piping to constructors is not yet supported",
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
                // Validate argument count (include both positional and keyword args)
                var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);
                var totalParamCount = funcSymbol.Parameters.Count;

                if (totalArgCount < requiredParamCount || totalArgCount > totalParamCount)
                {
                    if (requiredParamCount == totalParamCount)
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
                for (int i = 0; i < allArgTypes.Count; i++)
                {
                    var argType = allArgTypes[i];
                    var paramType = funcSymbol.Parameters[i].Type;

                    if (!argType.IsAssignableTo(paramType))
                    {
                        var argDesc = i == 0 ? "piped value" : $"argument {i}";
                        var argNode = i == 0 ? binOp.Left : call.Arguments[i - 1];
                        AddError($"Cannot pass {argDesc} of type '{argType.GetDisplayName()}' to parameter '{funcSymbol.Parameters[i].Name}' of type '{paramType.GetDisplayName()}'",
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
                AddError($"Piping to constructors is not yet supported",
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

    private SemanticType CheckMemberAccess(MemberAccess memberAccess)
    {
        // Check for super() usage - the parser directly produces SuperExpression for super()
        if (memberAccess.Object is SuperExpression superExpr)
        {
            return ValidateSuperMemberAccess(memberAccess, superExpr);
        }

        var objectType = CheckExpression(memberAccess.Object);

        // If object type is Unknown (e.g., from error recovery symbols), return Unknown
        // to prevent cascading errors. The original error (e.g., import failure) was already reported.
        if (objectType is UnknownType)
        {
            return SemanticType.Unknown;
        }

        // Handle null conditional access (?.)
        SemanticType memberLookupType = objectType;
        if (memberAccess.IsNullConditional)
        {
            // Null conditional can only be used on nullable/optional types
            if (objectType is NullableType nullableObjectType)
            {
                memberLookupType = nullableObjectType.UnderlyingType;
            }
            else if (objectType is OptionalType optionalObjectType)
            {
                memberLookupType = optionalObjectType.UnderlyingType;
            }
            else
            {
                AddError(
                    $"Null conditional operator '?.' can only be used on nullable types, but got '{objectType.GetDisplayName()}'",
                    memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.InvalidNullConditional,
                    span: memberAccess.Span);
                return SemanticType.Unknown;
            }
        }

        // Handle module member access (e.g., config.MAX_SIZE, utils.helper())
        if (memberLookupType is ModuleType moduleType)
        {
            var moduleSymbol = moduleType.Symbol;
            if (moduleSymbol.Exports.TryGetValue(memberAccess.Member, out var exportedSymbol))
            {
                var exportedType = exportedSymbol switch
                {
                    VariableSymbol varSymbol => GetVariableType(varSymbol),
                    FunctionSymbol funcSymbol => new FunctionType
                    {
                        ParameterTypes = funcSymbol.Parameters.Select(p => p.Type).ToList(),
                        ReturnType = funcSymbol.ReturnType
                    },
                    TypeSymbol typeSymbol => new UserDefinedType { Name = typeSymbol.Name, Symbol = typeSymbol },
                    ModuleSymbol nestedModule => new ModuleType { Symbol = nestedModule },
                    _ => SemanticType.Unknown
                };
                // Mark error recovery for unhandled symbol types in module exports
                // (e.g., TypeAliasSymbol) — these are resolved elsewhere, not a compiler bug.
                if (exportedType is UnknownType)
                    MarkExpressionAsErrorRecovery(memberAccess);
                return exportedType;
            }

            var moduleMemberMessage = $"Module '{moduleSymbol.Name}' has no member '{memberAccess.Member}'";
            var moduleMemberSuggestion = FindModuleMemberSuggestion(memberAccess.Member, moduleSymbol);
            if (moduleMemberSuggestion != null)
                moduleMemberMessage += $". Did you mean '{moduleMemberSuggestion}'?";
            AddError(moduleMemberMessage,
                memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span);
            return SemanticType.Unknown;
        }

        if (memberLookupType is UserDefinedType udt && udt.Symbol != null)
        {
            // Look for field or property (including inherited fields)
            var (field, fieldOwner) = FindFieldInHierarchy(udt.Symbol, memberAccess.Member);
            if (field != null && fieldOwner != null)
            {
                // Access level validation is handled by AccessValidator in the validation pipeline

                var fieldType = GetVariableType(field);

                // Wrap result in optional/nullable for null conditional access
                if (memberAccess.IsNullConditional && fieldType is not NullableType and not OptionalType)
                {
                    // Use OptionalType when object is Optional, NullableType for C# nullable
                    if (objectType is OptionalType)
                        return new OptionalType { UnderlyingType = fieldType };
                    return new NullableType { UnderlyingType = fieldType };
                }
                return fieldType;
            }

            // Look for method (including inherited methods)
            var (method, methodOwner) = FindMethodInHierarchy(udt.Symbol, memberAccess.Member);
            if (method != null && methodOwner != null)
            {
                // Access level validation is handled by AccessValidator in the validation pipeline

                // When accessing a method via member access (obj.method), the object is implicitly
                // bound as the first parameter (self), so we skip it when creating the FunctionType
                var paramTypes = method.Parameters.Skip(1).Select(p => p.Type).ToList();

                var methodFunctionType = new FunctionType
                {
                    ParameterTypes = paramTypes,
                    ReturnType = method.ReturnType
                };

                // For null conditional method access, we don't wrap the FunctionType itself,
                // but the eventual call result should be nullable (handled in CheckFunctionCall)
                return methodFunctionType;
            }

            var typeMemberMessage = $"Type '{memberLookupType.GetDisplayName()}' has no member '{memberAccess.Member}'";
            if (udt.Symbol != null)
            {
                var typeMemberSuggestion = FindMemberSuggestion(memberAccess.Member, udt.Symbol);
                if (typeMemberSuggestion != null)
                    typeMemberMessage += $". Did you mean '{typeMemberSuggestion}'?";
            }
            AddError(typeMemberMessage,
                memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span);
        }

        // Handle named tuple element access: pos.x, pos.y
        if (memberLookupType is TupleType tupleType && tupleType.IsNamed)
        {
            var names = tupleType.ElementNames!.Value;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == memberAccess.Member)
                {
                    return tupleType.ElementTypes[i];
                }
            }

            AddError(
                $"Named tuple type '{tupleType.GetDisplayName()}' has no element '{memberAccess.Member}'",
                memberAccess.LineStart, memberAccess.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedMember,
                span: memberAccess.Span);
            return SemanticType.Unknown;
        }

        // Intentional Unknown without error for non-UserDefinedType member access:
        // GenericType (list[T].append), BuiltinType (str.upper), TupleType, etc.
        // are resolved by the codegen layer through CLR member discovery, not the
        // type checker. Mark as error recovery to suppress SPY0907 false positives.
        MarkExpressionAsErrorRecovery(memberAccess);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Finds a field by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (VariableSymbol? Field, TypeSymbol? Owner) FindFieldInHierarchy(TypeSymbol type, string fieldName)
    {
        // First check the type itself
        var field = type.Fields.FirstOrDefault(f => f.Name == fieldName);
        if (field != null)
            return (field, type);

        // Check base class chain
        var current = GetBaseType(type);
        while (current != null)
        {
            field = current.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field != null)
                return (field, current);
            current = GetBaseType(current);
        }

        return (null, null);
    }

    /// <summary>
    /// Finds a method by name in the type's hierarchy (including parent classes and interfaces).
    /// </summary>
    private (FunctionSymbol? Method, TypeSymbol? Owner) FindMethodInHierarchy(TypeSymbol type, string methodName)
    {
        // First check the type itself
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (method != null)
            return (method, type);

        // Check base class chain
        var current = GetBaseType(type);
        while (current != null)
        {
            method = current.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method != null)
                return (method, current);
            current = GetBaseType(current);
        }

        // Check interfaces (for method contracts)
        foreach (var iface in GetInterfaces(type))
        {
            method = iface.Methods.FirstOrDefault(m => m.Name == methodName);
            if (method != null)
                return (method, iface);
        }

        return (null, null);
    }

    private SemanticType CheckIndexAccess(IndexAccess indexAccess)
    {
        // Check if this subscript expression has a narrowed type
        var narrowingKey = ExtractNarrowingKey(indexAccess);
        if (narrowingKey != null)
        {
            var narrowedType = _narrowingContext.GetNarrowedType(narrowingKey);
            if (narrowedType != null)
            {
                return narrowedType;
            }
        }

        // Special handling for generic type reference: Box[int] or Pair[int, str]
        // This is parsed as IndexAccess(Object: Box, Index: int or TupleLiteral)
        // When the object is a generic type and the index can be resolved as type(s),
        // this represents a generic type with type arguments, not an index operation
        if (indexAccess.Object is Identifier typeId)
        {
            var symbol = _symbolTable.Lookup(typeId.Name);

            // Handle generic type reference (e.g., Box[int])
            if (symbol is TypeSymbol genericTypeSymbol && genericTypeSymbol.IsGeneric)
            {
                var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (typeArgs != null)
                {
                    // Return a GenericType representing the instantiated type
                    return new GenericType
                    {
                        Name = genericTypeSymbol.Name,
                        TypeArguments = typeArgs,
                        GenericDefinition = genericTypeSymbol
                    };
                }
            }

            // Handle generic function reference (e.g., identity[int])
            // This creates a special "instantiated generic function" type for use in function calls
            if (symbol is FunctionSymbol genericFuncSymbol && genericFuncSymbol.IsGeneric)
            {
                var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (typeArgs != null)
                {
                    // Store the type arguments in SemanticInfo for use in CheckFunctionCall
                    _semanticInfo.SetExpressionType(indexAccess, new GenericFunctionType
                    {
                        FunctionSymbol = genericFuncSymbol,
                        TypeArguments = typeArgs
                    });
                    return _semanticInfo.GetExpressionType(indexAccess)!;
                }
            }
        }

        var objectType = CheckExpression(indexAccess.Object);
        var indexType = CheckExpression(indexAccess.Index);

        // Use TypeInferenceService for type inference (errors reported by validator in pipeline)
        var resultType = _typeInference.InferIndexAccessType(objectType, indexType);

        // TypeInferenceService covers all supported operations - return Unknown for unsupported
        return resultType ?? SemanticType.Unknown;
    }

    private SemanticType CheckFunctionCall(FunctionCall call)
    {
        // Handle None() — empty Optional constructor
        if (call.Function is NoneLiteral && call.Arguments.Length == 0 && call.KeywordArguments.Length == 0)
        {
            if (_expectedType is OptionalType)
            {
                return _expectedType;
            }
            else if (_expectedType != null)
            {
                AddError($"'None()' can only construct Optional types, not '{_expectedType.GetDisplayName()}'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.InvalidNoneConstructor,
                    span: call.Span);
                return SemanticType.Unknown;
            }
            else
            {
                AddError("Cannot infer type for 'None()' without a type annotation. Add a type annotation like 'x: int? = None()'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                return SemanticType.Unknown;
            }
        }

        // Check if this is a tagged union constructor shorthand (Some/Ok/Err)
        if (call.Function is Identifier constructorId && call.Arguments.Length == 1 && call.KeywordArguments.Length == 0)
        {
            var constructorResult = TryCheckTaggedUnionConstructor(constructorId, call);
            if (constructorResult != null)
                return constructorResult;
        }

        // Check if this is a null conditional method call (obj?.method())
        bool isNullConditionalCall = call.Function is MemberAccess { IsNullConditional: true };
        bool isOptionalNullConditional = false;

        // Check the called expression type first
        var calleeType = CheckExpression(call.Function);

        // After checking the callee, determine if this is ?. on an Optional object
        if (isNullConditionalCall && call.Function is MemberAccess nullCondMa)
        {
            var objType = _semanticInfo.GetExpressionType(nullCondMa.Object);
            isOptionalNullConditional = objType is OptionalType;
        }

        // Track super().__init__() calls AFTER validation completes
        // (do this after CheckExpression so the validation doesn't see it as already called)
        if (call.Function is MemberAccess ma && ma.Object is SuperExpression && ma.Member == DunderNames.Init)
        {
            _superInitCalled = true;
        }

        // Try to resolve the function symbol early for constructor inference on arguments.
        // For simple identifier calls (foo(Some(42))), we can look up the function before
        // checking arguments, allowing _expectedType to be set per-parameter.
        FunctionSymbol? earlyFuncSymbol = null;
        if (call.Function is Identifier earlyId)
        {
            var earlySymbol = _symbolTable.Lookup(earlyId.Name);
            if (earlySymbol is FunctionSymbol fs && !fs.IsGeneric)
            {
                // Only use early resolution for non-generic, non-overloaded functions.
                // Generic functions need argument types first for inference.
                // Overloaded builtins need argument types for resolution.
                var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(earlyId.Name);
                if (overloads == null || overloads.Count <= 1 || !overloads.Contains(fs))
                {
                    earlyFuncSymbol = fs;
                }
            }
        }

        // Check arguments and collect their types
        // When we have an early function symbol or callee FunctionType, set _expectedType per-parameter
        // to enable constructor inference (Some/None()/Ok/Err) in function arguments.
        var calleeFunctionType = calleeType as FunctionType;
        var argTypes = new List<SemanticType>();
        for (int argIdx = 0; argIdx < call.Arguments.Length; argIdx++)
        {
            var previousExpectedType = _expectedType;
            if (earlyFuncSymbol != null && argIdx < earlyFuncSymbol.Parameters.Count)
            {
                var paramType = earlyFuncSymbol.Parameters[argIdx].Type;
                _expectedType = paramType is UnknownType ? null : paramType;
            }
            else if (calleeFunctionType != null && argIdx < calleeFunctionType.ParameterTypes.Count)
            {
                var paramType = calleeFunctionType.ParameterTypes[argIdx];
                _expectedType = paramType is UnknownType ? null : paramType;
            }
            argTypes.Add(CheckExpression(call.Arguments[argIdx]));
            _expectedType = previousExpectedType;
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            var previousExpectedType = _expectedType;
            if (earlyFuncSymbol != null)
            {
                var param = earlyFuncSymbol.Parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                if (param != null)
                {
                    _expectedType = param.Type is UnknownType ? null : param.Type;
                }
            }
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
            _expectedType = previousExpectedType;
        }

        // Total argument count includes both positional and keyword arguments
        var totalArgCount = argTypes.Count + kwargTypes.Count;

        // Try to get the function symbol directly for better validation
        FunctionSymbol? funcSymbol = null;

        // Special handling for generic type instantiation: Box[int](42) or Pair[int, str](1, "a")
        // This is parsed as FunctionCall(Function: IndexAccess(Object: Box, Index: int or TupleLiteral), Arguments: [...])
        if (call.Function is IndexAccess indexAccess &&
            indexAccess.Object is Identifier genericTypeId &&
            _symbolTable.Lookup(genericTypeId.Name) is TypeSymbol genericTypeSymbol &&
            genericTypeSymbol.IsGeneric)
        {
            // The "index" is actually type argument(s) - try to resolve them as types
            var typeArgs = TryResolveTypeArguments(indexAccess.Index);
            if (typeArgs != null)
            {
                // Cannot instantiate abstract classes
                if (genericTypeSymbol.IsAbstract)
                {
                    AddError($"Cannot instantiate abstract class '{genericTypeSymbol.Name}'",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AbstractInstantiation,
                        span: call.Span);
                    return SemanticType.Unknown;
                }

                // Return a GenericType with the type arguments
                return new GenericType
                {
                    Name = genericTypeSymbol.Name,
                    TypeArguments = typeArgs,
                    GenericDefinition = genericTypeSymbol
                };
            }
        }

        // Handle generic function call: identity[int](42)
        // The calleeType will be GenericFunctionType from CheckIndexAccess
        if (calleeType is GenericFunctionType genericFuncType)
        {
            // For now, just return the substituted return type
            // We substitute type parameters with type arguments in the return type
            var substitutedReturnType = SubstituteTypeParameters(
                genericFuncType.FunctionSymbol.ReturnType,
                genericFuncType.FunctionSymbol.TypeParameters,
                genericFuncType.TypeArguments);
            return substitutedReturnType;
        }

        if (call.Function is Identifier id)
        {
            // Special handling for builtin len() - validate that argument supports __len__ protocol
            // See: #103 (use BuiltinNames constant instead of hardcoded string)
            if (id.Name == "len" && argTypes.Count == 1)
            {
                // Use TypeInferenceService for type inference (errors reported by validator in pipeline)
                var lenType = _typeInference.InferLenType(argTypes[0]);

                // TypeInferenceService always returns Int for len() - return Unknown only if completely unsupported
                return lenType ?? SemanticType.Unknown;
            }

            // Special handling for builtin hash() - every object supports GetHashCode()
            if (id.Name == "hash" && argTypes.Count == 1)
            {
                var hashType = _typeInference.InferHashType(argTypes[0]);
                return hashType ?? SemanticType.Unknown;
            }

            var symbol = _symbolTable.Lookup(id.Name);

            // Special handling for constructor calls (calling a type)
            if (symbol is TypeSymbol typeSymbol)
            {
                // For primitive types (int, float, str, bool, long, etc.), route to builtin function overloads
                // instead of treating as constructor. This matches Python semantics where int(x) calls
                // the int conversion function, not constructs a new int object.
                var primitiveOverloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
                if (primitiveOverloads != null && primitiveOverloads.Count > 0 && PrimitiveCatalog.IsPrimitive(id.Name))
                {
                    // Route to builtin function overload resolution below
                    // (fall through to overload handling)
                }
                else
                {
                    // Cannot instantiate abstract classes
                    if (typeSymbol.IsAbstract)
                    {
                        AddError($"Cannot instantiate abstract class '{typeSymbol.Name}'",
                            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AbstractInstantiation,
                            span: call.Span);
                        return SemanticType.Unknown;
                    }

                    // Constructor call returns an instance of the type
                    return new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name };
                }
            }

            funcSymbol = symbol as FunctionSymbol;

            // If we found a symbol but it's not a function or type, it's not callable
            // UNLESS it's a variable with a FunctionType (e.g., a parameter with type (T) -> U)
            if (symbol != null && funcSymbol == null && symbol is not TypeSymbol)
            {
                // Check if it's an error recovery symbol - suppress cascading errors
                if (symbol.IsErrorRecovery)
                {
                    return SemanticType.Unknown;
                }

                // Check if it's a variable with a FunctionType - those are callable
                if (symbol is VariableSymbol varSym && GetVariableType(varSym) is FunctionType)
                {
                    // Let the FunctionType handling below deal with this
                }
                else
                {
                    AddError($"'{id.Name}' is not callable (type: {calleeType.GetDisplayName()})",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                        span: call.Function.Span);
                    return SemanticType.Unknown;
                }
            }

            // Special handling for builtin functions with overloads
            // When there are multiple overloads, we need to perform overload resolution to find the right one.
            // The funcSymbol from Lookup is just the first overload, which may not match the call.
            // Only use builtin overloads if there's no user-defined function shadowing the builtin.
            var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
            var isBuiltinWithOverloads = overloads != null && overloads.Count > 1;
            // If funcSymbol was found in symbol table AND it's one of the builtin overloads, use overload resolution
            var needsOverloadResolution = isBuiltinWithOverloads &&
                (funcSymbol == null || (funcSymbol != null && overloads!.Contains(funcSymbol)));
            if (needsOverloadResolution)
            {
                // First pass: filter by argument count (considering default parameters and variadic parameters)
                var candidateOverloads = overloads!.Where(o =>
                {
                    var requiredParams = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
                    var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
                    var totalParams = o.Parameters.Count;
                    // Variadic functions can accept any number of arguments >= required
                    if (hasVariadic)
                        return totalArgCount >= requiredParams;
                    return totalArgCount >= requiredParams && totalArgCount <= totalParams;
                }).ToList();

                // Second pass: check type compatibility
                FunctionSymbol? matchingOverload = null;
                foreach (var overload in candidateOverloads)
                {
                    bool typesMatch = true;
                    var hasVariadic = overload.Parameters.Any(p => p.IsVariadic);
                    var variadicParam = overload.Parameters.FirstOrDefault(p => p.IsVariadic);

                    for (int i = 0; i < argTypes.Count; i++)
                    {
                        SemanticType expectedType;
                        if (i < overload.Parameters.Count && !overload.Parameters[i].IsVariadic)
                        {
                            // Regular parameter
                            expectedType = overload.Parameters[i].Type;
                        }
                        else if (variadicParam != null)
                        {
                            // Variadic parameter - all remaining args must match the element type
                            expectedType = variadicParam.Type;
                        }
                        else
                        {
                            // Index out of bounds - shouldn't happen with valid candidates
                            typesMatch = false;
                            break;
                        }

                        if (!IsAssignable(argTypes[i], expectedType))
                        {
                            typesMatch = false;
                            break;
                        }
                    }
                    if (typesMatch)
                    {
                        matchingOverload = overload;
                        break;
                    }
                }

                if (matchingOverload != null)
                {
                    // Update the identifier symbol to point to the matching overload
                    _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
                    return matchingOverload.ReturnType;
                }
                else
                {
                    // No matching overload found
                    var expectedCounts = string.Join(" or ", overloads!.Select(o =>
                    {
                        var required = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
                        var total = o.Parameters.Count;
                        var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
                        if (hasVariadic)
                            return $"{required}+";
                        return required == total ? total.ToString() : $"{required}-{total}";
                    }).Distinct());
                    AddError($"Function '{id.Name}' expects {expectedCounts} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                    return SemanticType.Unknown;
                }
            }
        }
        // Handle member access function calls (e.g., module.function() or obj.method())
        // Skip super() calls - they're already validated by ValidateSuperMemberAccess
        else if (call.Function is MemberAccess memberAccessCall && memberAccessCall.Object is not SuperExpression)
        {
            // For module member access (lib.math.add), we already checked the expression at line 1697
            // which gave us a FunctionType. But we also need the FunctionSymbol for better validation.
            // The calleeType already validated that memberAccessCall resolves to a function.
            // We just need to extract the FunctionSymbol.

            // Note: We can't just call CheckExpression again on memberAccessCall.Object because
            // it was already called as part of checking call.Function at line 1697.
            // Instead, we need to use the already-computed calleeType information or
            // re-traverse the member access to find the function symbol.

            // Since the semantic info stores resolved symbols, let's try to get the function symbol
            // from the semantic info for the member access.
            // But actually, the issue is that CheckMemberAccess returns a FunctionType but doesn't
            // store the underlying FunctionSymbol anywhere we can retrieve it.

            // The best approach is to re-evaluate the object to get the module, then lookup the member.
            // This is duplicate work but necessary until we refactor to store symbols in SemanticInfo.
            var objectType = CheckExpression(memberAccessCall.Object);
            if (objectType is ModuleType moduleType)
            {
                var moduleSymbol = moduleType.Symbol;
                if (moduleSymbol.Exports.TryGetValue(memberAccessCall.Member, out var exportedSymbol))
                {
                    funcSymbol = exportedSymbol as FunctionSymbol;
                }
            }
        }

        // If we have a FunctionSymbol, use it for validation (supports default parameters)
        if (funcSymbol != null)
        {
            // Handle generic function inference: identity(42) -> infer T=int
            // This is triggered when calling a generic function without explicit type arguments
            if (funcSymbol.IsGeneric)
            {
                var inferenceResult = _genericInference.InferTypeArguments(funcSymbol, argTypes);
                if (inferenceResult.Success && inferenceResult.InferredTypes != null)
                {
                    // Inference succeeded - substitute type parameters and return the result
                    var substitutedReturnType = SubstituteTypeParameters(
                        funcSymbol.ReturnType,
                        funcSymbol.TypeParameters,
                        inferenceResult.InferredTypes);

                    // Store the inferred type arguments for codegen
                    _semanticInfo.SetInferredTypeArguments(call, inferenceResult.InferredTypes);

                    // Wrap result in optional/nullable for null conditional calls
                    if (isNullConditionalCall && substitutedReturnType is not NullableType and not OptionalType)
                    {
                        if (isOptionalNullConditional)
                            return new OptionalType { UnderlyingType = substitutedReturnType };
                        return new NullableType { UnderlyingType = substitutedReturnType };
                    }
                    return substitutedReturnType;
                }
                else
                {
                    // Inference failed - report error
                    AddError(inferenceResult.ErrorMessage ?? "Type arguments cannot be inferred",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferGenericType,
                        span: call.Span);
                    return SemanticType.Unknown;
                }
            }

            // Count required parameters (those without defaults)
            var requiredParamCount = funcSymbol.Parameters.Count(p => !p.HasDefault);
            var totalParamCount = funcSymbol.Parameters.Count;

            // Validate argument count considering defaults (include both positional and keyword args)
            if (totalArgCount < requiredParamCount || totalArgCount > totalParamCount)
            {
                if (requiredParamCount == totalParamCount)
                {
                    AddError($"Function expects {totalParamCount} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
                else
                {
                    AddError($"Function expects {requiredParamCount} to {totalParamCount} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
            }
            else
            {
                // Validate positional argument types
                for (int i = 0; i < argTypes.Count; i++)
                {
                    if (!IsAssignable(argTypes[i], funcSymbol.Parameters[i].Type))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{funcSymbol.Parameters[i].Type.GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: call.Arguments[i].Span);
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
                        // Check if this parameter was already provided positionally
                        var paramIndex = funcSymbol.Parameters.ToList().IndexOf(param);
                        if (paramIndex < argTypes.Count)
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
            }

            var returnType = funcSymbol.ReturnType;

            // Wrap result in optional/nullable for null conditional calls
            if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
            {
                if (isOptionalNullConditional)
                    return new OptionalType { UnderlyingType = returnType };
                return new NullableType { UnderlyingType = returnType };
            }
            return returnType;
        }

        // Fallback to FunctionType validation (no default parameter support)
        // Use the already-computed calleeType to avoid re-evaluating call.Function
        // (which causes double validation, e.g., super().__init__() being flagged as duplicate)
        var funcType = calleeType;

        if (funcType is FunctionType ft)
        {
            // Skip validation for .NET types with multiple constructor overloads
            // (C# compiler will handle overload resolution)
            if (!ft.SkipArgumentValidation)
            {
                // Validate argument count (include both positional and keyword arguments)
                if (totalArgCount != ft.ParameterTypes.Count)
                {
                    AddError($"Function expects {ft.ParameterTypes.Count} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
                else
                {
                    // Validate positional argument types
                    for (int i = 0; i < argTypes.Count; i++)
                    {
                        if (!IsAssignable(argTypes[i], ft.ParameterTypes[i]))
                        {
                            AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{ft.ParameterTypes[i].GetDisplayName()}'",
                                call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                                span: call.Arguments[i].Span);
                        }
                    }
                }
            }

            var returnType = ft.ReturnType;

            // Wrap result in optional/nullable for null conditional calls
            if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
            {
                if (isOptionalNullConditional)
                    return new OptionalType { UnderlyingType = returnType };
                return new NullableType { UnderlyingType = returnType };
            }
            return returnType;
        }

        // If callee type is Unknown, this is error recovery from a sub-expression
        // (covered by transitive error recovery tracking in CheckExpression).
        // Otherwise, the callee evaluated to a non-callable type — emit an error.
        if (funcType is not UnknownType)
        {
            AddError($"Expression of type '{funcType.GetDisplayName()}' is not callable",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                span: call.Function.Span);
        }
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Tries to resolve an expression as a type. This is used for generic type instantiation
    /// where Box[int](42) parses the type argument as an expression.
    /// Returns null if the expression cannot be interpreted as a type.
    /// </summary>
    private SemanticType? TryResolveExpressionAsType(Expression expr)
    {
        // Handle simple identifier as type name (e.g., "int", "str", "MyClass")
        if (expr is Identifier typeId)
        {
            // Create a synthetic type annotation and resolve it
            var typeAnnotation = new Parser.Ast.TypeAnnotation
            {
                Name = typeId.Name,
                LineStart = expr.LineStart,
                ColumnStart = expr.ColumnStart
            };
            var resolved = _typeResolver.ResolveTypeAnnotation(typeAnnotation);
            return resolved != SemanticType.Unknown ? resolved : null;
        }

        // Handle nested generic types (e.g., Box[int] in Container[Box[int]])
        if (expr is IndexAccess indexAccess &&
            indexAccess.Object is Identifier nestedTypeId &&
            _symbolTable.Lookup(nestedTypeId.Name) is TypeSymbol nestedGenericType &&
            nestedGenericType.IsGeneric)
        {
            var nestedTypeArgs = TryResolveTypeArguments(indexAccess.Index);
            if (nestedTypeArgs != null)
            {
                return new GenericType
                {
                    Name = nestedGenericType.Name,
                    TypeArguments = nestedTypeArgs,
                    GenericDefinition = nestedGenericType
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Tries to resolve one or more type arguments from an index expression.
    /// Handles both single type arguments (int) and multiple type arguments (int, str as TupleLiteral).
    /// Returns null if the expressions cannot be interpreted as types.
    /// </summary>
    private List<SemanticType>? TryResolveTypeArguments(Expression indexExpr)
    {
        var typeArgs = new List<SemanticType>();

        // Handle multiple type arguments: Pair[int, str] parses as TupleLiteral
        if (indexExpr is TupleLiteral tuple)
        {
            foreach (var element in tuple.Elements)
            {
                var typeArg = TryResolveExpressionAsType(element);
                if (typeArg == null)
                    return null;
                typeArgs.Add(typeArg);
            }
            return typeArgs;
        }

        // Handle single type argument
        var singleTypeArg = TryResolveExpressionAsType(indexExpr);
        if (singleTypeArg == null)
            return null;
        typeArgs.Add(singleTypeArg);
        return typeArgs;
    }

    /// <summary>
    /// Tries to check a function call as a tagged union constructor (Some/Ok/Err).
    /// Returns the resolved type if successful, or null if this is not a constructor call.
    /// </summary>
    private SemanticType? TryCheckTaggedUnionConstructor(Identifier constructorId, FunctionCall call)
    {
        var name = constructorId.Name;

        if (name == "Some")
        {
            if (_expectedType is OptionalType opt)
            {
                var argType = CheckExpression(call.Arguments[0]);
                if (!IsAssignable(argType, opt.UnderlyingType))
                {
                    AddError($"Argument type '{argType.GetDisplayName()}' is not compatible with Optional underlying type '{opt.UnderlyingType.GetDisplayName()}'",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Arguments[0].Span);
                }
                return _expectedType;
            }
            else if (_expectedType == null && _symbolTable.Lookup("Some") == null)
            {
                // No expected type and no user-defined 'Some' — error
                AddError("Cannot infer type for 'Some()' without a type annotation. Add a type annotation like 'x: int? = Some(value)'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                // Still check the argument to avoid cascading errors
                CheckExpression(call.Arguments[0]);
                return SemanticType.Unknown;
            }
            // Fall through to normal function call if there's a user-defined 'Some' or expectedType is not OptionalType
        }

        if (name == "Ok")
        {
            if (_expectedType is ResultType result)
            {
                var argType = CheckExpression(call.Arguments[0]);
                if (!IsAssignable(argType, result.OkType))
                {
                    AddError($"Argument type '{argType.GetDisplayName()}' is not compatible with Result Ok type '{result.OkType.GetDisplayName()}'",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Arguments[0].Span);
                }
                return _expectedType;
            }
            else if (_expectedType == null && _symbolTable.Lookup("Ok") == null)
            {
                AddError("Cannot infer type for 'Ok()' without a type annotation. Add a type annotation like 'x: int !str = Ok(value)'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                CheckExpression(call.Arguments[0]);
                return SemanticType.Unknown;
            }
        }

        if (name == "Err")
        {
            if (_expectedType is ResultType result)
            {
                var argType = CheckExpression(call.Arguments[0]);
                if (!IsAssignable(argType, result.ErrorType))
                {
                    AddError($"Argument type '{argType.GetDisplayName()}' is not compatible with Result Error type '{result.ErrorType.GetDisplayName()}'",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Arguments[0].Span);
                }
                return _expectedType;
            }
            else if (_expectedType == null && _symbolTable.Lookup("Err") == null)
            {
                AddError("Cannot infer type for 'Err()' without a type annotation. Add a type annotation like 'x: int !str = Err(error)'",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                CheckExpression(call.Arguments[0]);
                return SemanticType.Unknown;
            }
        }

        return null; // Not a tagged union constructor — fall through to normal handling
    }

    private SemanticType CheckListLiteral(ListLiteral list)
    {
        if (list.Elements.Length == 0)
        {
            return new GenericType
            {
                Name = "list",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown }
            };
        }

        var elementTypes = list.Elements.Select(CheckExpression).ToList();

        // Find least common ancestor of all element types
        // This handles cases like [Bug(), Feature()] -> list[WorkItem]
        var commonType = FindLeastCommonAncestor(elementTypes);

        return new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckDictLiteral(DictLiteral dict)
    {
        if (dict.Entries.Length == 0)
        {
            return new GenericType
            {
                Name = "dict",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown, SemanticType.Unknown }
            };
        }

        var keyTypes = dict.Entries.Select(e => CheckExpression(e.Key)).ToList();
        var valueTypes = dict.Entries.Select(e => CheckExpression(e.Value)).ToList();

        // Find least common ancestor for both keys and values
        var commonKeyType = FindLeastCommonAncestor(keyTypes);
        var commonValueType = FindLeastCommonAncestor(valueTypes);

        return new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { commonKeyType, commonValueType }
        };
    }

    private SemanticType CheckSetLiteral(SetLiteral set)
    {
        if (set.Elements.Length == 0)
        {
            return new GenericType
            {
                Name = "set",
                TypeArguments = new List<SemanticType> { SemanticType.Unknown }
            };
        }

        var elementTypes = set.Elements.Select(CheckExpression).ToList();

        // Find least common ancestor of all element types
        var commonType = FindLeastCommonAncestor(elementTypes);

        return new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { commonType }
        };
    }

    private SemanticType CheckTupleLiteral(TupleLiteral tuple)
    {
        var elementTypes = tuple.Elements.Select(CheckExpression).ToList();
        var tupleType = new TupleType { ElementTypes = elementTypes };

        // Propagate element names for named tuple literals
        if (!tuple.ElementNames.IsEmpty)
        {
            tupleType = tupleType with { ElementNames = tuple.ElementNames };
        }

        return tupleType;
    }

    private SemanticType CheckListComprehension(ListComprehension listComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("list-comprehension");

        // Process clauses (for and if)
        CheckComprehensionClauses(listComp.Clauses);

        // Check element expression
        var elementType = CheckExpression(listComp.Element);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = "list",
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckSetComprehension(SetComprehension setComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("set-comprehension");

        // Process clauses (for and if)
        CheckComprehensionClauses(setComp.Clauses);

        // Check element expression
        var elementType = CheckExpression(setComp.Element);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = "set",
            TypeArguments = new List<SemanticType> { elementType }
        };
    }

    private SemanticType CheckDictComprehension(DictComprehension dictComp)
    {
        // Enter comprehension scope (variables don't leak)
        _symbolTable.EnterScope("dict-comprehension");

        // Process clauses (for and if)
        CheckComprehensionClauses(dictComp.Clauses);

        // Check key and value expressions
        var keyType = CheckExpression(dictComp.Key);
        var valueType = CheckExpression(dictComp.Value);

        _symbolTable.ExitScope();

        return new GenericType
        {
            Name = "dict",
            TypeArguments = new List<SemanticType> { keyType, valueType }
        };
    }

    /// <summary>
    /// Processes comprehension clauses (ForClause and IfClause), defining loop variables
    /// and validating filter conditions. This is shared logic used by list, set, and dict
    /// comprehensions.
    /// </summary>
    /// <param name="clauses">The comprehension clauses to process</param>
    private void CheckComprehensionClauses(IReadOnlyList<ComprehensionClause> clauses)
    {
        foreach (var clause in clauses)
        {
            switch (clause)
            {
                case ForClause forClause:
                    CheckComprehensionForClause(forClause);
                    break;

                case IfClause ifClause:
                    CheckComprehensionIfClause(ifClause);
                    break;
            }
        }
    }

    /// <summary>
    /// Processes a for clause in a comprehension, checking the iterator type and
    /// defining the loop variable in the current scope.
    /// </summary>
    private void CheckComprehensionForClause(ForClause forClause)
    {
        // Check iterator type and infer element type (errors reported by validator in pipeline)
        var iterType = CheckExpression(forClause.Iterator);
        var elemType = _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;

        // Define loop variable (single identifier only for now)
        if (forClause.Target is Identifier id)
        {
            var loopVarSymbol = new VariableSymbol
            {
                Name = id.Name,
                Kind = SymbolKind.Variable,
                Type = elemType,
                AccessLevel = AccessLevel.Public,
                DeclarationLine = id.LineStart,
                DeclarationColumn = id.ColumnStart
            };
            _symbolTable.Define(loopVarSymbol);
            _semanticInfo.SetIdentifierSymbol(id, loopVarSymbol);
            _semanticInfo.SetExpressionType(forClause.Target, elemType);
            if (elemType is UnknownType)
                MarkExpressionAsErrorRecovery(forClause.Target);
        }
        else
        {
            // For tuple unpacking or other complex targets
            // See: #104 (tuple unpacking in comprehensions)
            AddError($"Tuple unpacking in comprehensions not yet supported",
                forClause.LineStart, forClause.ColumnStart, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking,
                span: forClause.Target.Span);
        }
    }

    /// <summary>
    /// Processes an if clause in a comprehension, validating that the condition
    /// is a boolean expression.
    /// </summary>
    private void CheckComprehensionIfClause(IfClause ifClause)
    {
        // Check condition is boolean
        var condType = CheckExpression(ifClause.Condition);
        if (!condType.IsAssignableTo(SemanticType.Bool))
        {
            AddError($"Comprehension filter must be bool, got '{condType.GetDisplayName()}'",
                ifClause.LineStart, ifClause.ColumnStart, code: DiagnosticCodes.Semantic.ConditionNotBoolean,
                span: ifClause.Condition.Span);
        }
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

    private SemanticType CheckLambda(LambdaExpression lambda)
    {
        // Use _expectedType for bidirectional type inference: if the context expects
        // a FunctionType, extract parameter types from it to infer lambda parameter types.
        FunctionType? expectedFunc = _expectedType as FunctionType;

        var paramTypes = new List<SemanticType>();
        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var param = lambda.Parameters[i];
            if (param.Type != null)
            {
                // Explicit type annotation — use it
                paramTypes.Add(_typeResolver.ResolveTypeAnnotation(param.Type));
            }
            else if (expectedFunc != null && i < expectedFunc.ParameterTypes.Count)
            {
                // Infer from expected function type context
                paramTypes.Add(expectedFunc.ParameterTypes[i]);
            }
            else
            {
                paramTypes.Add(SemanticType.Unknown);
            }
        }

        // Enter lambda scope
        _symbolTable.EnterScope("lambda");

        // Enter an isolated narrowing scope for this lambda.
        // Type narrowings from the enclosing scope should NOT be visible inside the lambda,
        // because lambdas can be stored and called later when the narrowing condition no longer holds.
        // This is the same logic as for nested function definitions (task 1.7).
        using var _ = _narrowingContext.EnterIsolatedScope();

        for (int i = 0; i < lambda.Parameters.Length; i++)
        {
            var paramSymbol = new VariableSymbol
            {
                Name = lambda.Parameters[i].Name,
                Kind = SymbolKind.Parameter,
                Type = paramTypes[i],
                IsParameter = true
            };
            _symbolTable.Define(paramSymbol);
        }

        var bodyType = CheckExpression(lambda.Body);

        _symbolTable.ExitScope();

        return new FunctionType
        {
            ParameterTypes = paramTypes,
            ReturnType = bodyType
        };
    }

    private SemanticType CheckTypeCast(TypeCast cast)
    {
        CheckExpression(cast.Value);
        return _typeResolver.ResolveTypeAnnotation(cast.TargetType);
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
        return type is BuiltinType builtin && (builtin.Name == "str" || builtin.Name == "string");
    }

    /// <summary>
    /// Returns true if the type is the object type.
    /// </summary>
    private static bool IsObjectType(SemanticType type)
    {
        return type is BuiltinType builtin && builtin.Name == "object";
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

        // Builtin to user-defined: only valid if unboxing from object
        if (source is BuiltinType sourceBuiltin && sourceBuiltin.Name == "object")
            return true;

        // User-defined to builtin: only valid for boxing to object
        if (target is BuiltinType targetBuiltin && targetBuiltin.Name == "object")
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

    /// <summary>
    /// Checks if a type symbol inherits from another type symbol (directly or indirectly).
    /// </summary>
    private bool InheritsFrom(TypeSymbol? derived, TypeSymbol? baseType)
    {
        if (derived == null || baseType == null)
            return false;

        var current = GetBaseType(derived);
        while (current != null)
        {
            if (current == baseType || current.Name == baseType.Name)
                return true;
            current = GetBaseType(current);
        }

        // Also check interfaces
        foreach (var iface in GetInterfaces(derived))
        {
            if (iface == baseType || iface.Name == baseType.Name)
                return true;
        }

        return false;
    }

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
        if (tryExpr.ExceptionType != null)
        {
            // Explicit exception type: try[ValueError] expr
            errorType = _typeResolver.ResolveTypeAnnotation(tryExpr.ExceptionType);
        }
        else if (tryExpr.Operand is TypeCoercion)
        {
            // Special case: try x to Cat → Result[Cat, InvalidCastException]
            var clrSymbol = _symbolTable.BuiltinRegistry.TryResolveClrType("InvalidCastException");
            errorType = new UserDefinedType { Name = "InvalidCastException", Symbol = clrSymbol };
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
    /// Check if a type is numeric (int, long, float, double, etc.).
    /// Delegates to PrimitiveCatalog for exhaustive primitive type checking.
    /// Note: Also allows Unknown types to avoid cascading errors.
    /// </summary>
    private static bool IsNumericType(SemanticType type)
        => type is UnknownType || PrimitiveCatalog.IsNumeric(type);

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

    private SemanticType CheckFStringLiteral(FStringLiteral fstr)
    {
        // Type-check all interpolated expressions within the f-string
        foreach (var part in fstr.Parts)
        {
            if (part.Expression != null)
            {
                CheckExpression(part.Expression);
            }
        }
        return SemanticType.Str;
    }

    private SemanticType CheckSliceAccess(SliceAccess sliceAccess)
    {
        var objType = CheckExpression(sliceAccess.Object);
        if (sliceAccess.Start != null)
            CheckExpression(sliceAccess.Start);
        if (sliceAccess.Stop != null)
            CheckExpression(sliceAccess.Stop);
        if (sliceAccess.Step != null)
            CheckExpression(sliceAccess.Step);

        // Slicing a list returns a list, slicing a str returns a str
        if (objType is GenericType gt && gt.Name == "list")
            return objType;
        if (objType == SemanticType.Str)
            return SemanticType.Str;

        // For other types, return the same type (best effort)
        return objType;
    }

    private SemanticType CheckWalrusExpression(WalrusExpression walrus)
    {
        var valueType = CheckExpression(walrus.Value);

        // Register the walrus target variable in the current scope
        var existingSymbol = _symbolTable.Lookup(walrus.Target, searchParents: false);
        if (existingSymbol is VariableSymbol existingVar)
        {
            // Variable already exists — update its type (redeclaration)
            SemanticBinding.SetVariableType(existingVar, valueType);
        }
        else
        {
            // New variable — create and register it
            var newSymbol = new VariableSymbol
            {
                Name = walrus.Target,
                Kind = SymbolKind.Variable,
                Type = valueType,
                IsConstant = false,
                DeclarationLine = walrus.LineStart,
                DeclarationColumn = walrus.ColumnStart
            };
            _symbolTable.Define(newSymbol);
            SemanticBinding.SetVariableType(newSymbol, valueType);
        }

        // The walrus expression both assigns and returns the value
        return valueType;
    }
}
