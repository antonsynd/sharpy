using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Expression checking dispatch and small utilities.
/// Sub-partials: Operators, Literals, Access
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
            FloatLiteral fl => fl.Suffix?.ToUpperInvariant() switch
            {
                "F" => SemanticType.Float32,
                "M" => SemanticType.Decimal,
                _ => SemanticType.Double,
            },
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
            TypeCoercion coercion => CheckTypeCoercion(coercion),
            TypeCheck typeCheck => CheckTypeCheck(typeCheck),
            MaybeExpression maybeExpr => CheckMaybeExpression(maybeExpr),
            TryExpression tryExpr => CheckTryExpression(tryExpr),
            Parenthesized paren => CheckExpression(paren.Expression),
            FStringLiteral fstr => CheckFStringLiteral(fstr),
            EllipsisLiteral => SemanticType.Void,
            SliceAccess sliceAccess => CheckSliceAccess(sliceAccess),
            WalrusExpression walrus => CheckWalrusExpression(walrus),
            AwaitExpression awaitExpr => CheckAwaitExpression(awaitExpr),
            SpreadElement spread => CheckExpression(spread.Value),
            StarExpression star => CheckExpression(star.Operand),
            MatchExpression matchExpr => CheckMatchExpression(matchExpr),
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

    private SemanticType CheckAwaitExpression(AwaitExpression awaitExpr)
    {
        if (!_currentFunctionIsAsync)
        {
            AddError("'await' can only be used inside 'async def' functions",
                awaitExpr.LineStart, awaitExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.AwaitOutsideAsync, span: awaitExpr.Span);
            return SemanticType.Unknown;
        }

        var operandType = CheckExpression(awaitExpr.Operand);

        if (operandType is TaskType taskType)
            return taskType.ResultType ?? SemanticType.Void;

        if (operandType is UnknownType)
            return SemanticType.Unknown;

        AddError($"Cannot await non-Task type '{operandType.GetDisplayName()}'",
            awaitExpr.LineStart, awaitExpr.ColumnStart,
            code: DiagnosticCodes.Semantic.InvalidAwaitOperand, span: awaitExpr.Span);
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
                ReturnType = funcSymbol.ReturnType,
                VariadicParameterIndex = GetVariadicIndex(funcSymbol.Parameters)
            },
            ModuleSymbol moduleSymbol => new ModuleType { Symbol = moduleSymbol },
            // Primitive type names (int, str, bool, float, etc.) used as function references
            // (e.g., map(int, items)) get a synthesized FunctionType so downstream consumers
            // like BuiltinReturnTypeInference can extract the return type.
            // Non-primitive TypeSymbols remain Unknown — resolved at FunctionCall level.
            TypeSymbol ts when PrimitiveCatalog.IsPrimitive(ts.Name) =>
                SynthesizePrimitiveFunctionType(ts),
            TypeSymbol => SemanticType.Unknown,
            _ => SemanticType.Unknown
        };

        // Mark intentional Unknown types: non-primitive TypeSymbol references and unhandled
        // symbol kinds are not errors — they're expected gaps handled at higher levels (e.g., FunctionCall).
        // Primitive TypeSymbols get a FunctionType above and should NOT be marked as error recovery.
        if (identifierType is UnknownType && symbol is not null)
        {
            MarkExpressionAsErrorRecovery(id);
        }

        return identifierType;
    }

    /// <summary>
    /// Synthesizes a FunctionType for a primitive type name (int, str, bool, float, etc.)
    /// used as a function reference (e.g., map(int, items), filter(bool, items)).
    /// The return type is the primitive type itself. The input parameter uses a synthetic
    /// TypeParameterType so that generic type inference (e.g., filter[T]) does not
    /// over-constrain T from the predicate's parameter — only the iterable argument
    /// should bind T.
    /// </summary>
    private SemanticType SynthesizePrimitiveFunctionType(TypeSymbol ts)
    {
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(ts.Name);
        if (overloads == null || overloads.Count == 0)
            return SemanticType.Unknown;

        // Pick the first single-parameter overload for arity reference
        var overload = overloads.FirstOrDefault(o => o.Parameters.Count == 1) ?? overloads[0];

        // Use a synthetic TypeParameterType for each input parameter so generic inference
        // treats the parameter as unconstrained (see GenericTypeInferenceService.IsSyntheticTypeParameter).
        var paramTypes = overload.Parameters
            .Select((_, i) => (SemanticType)new TypeParameterType
            {
                Name = GenericTypeInferenceService.SyntheticTypeParameterPrefix + i,
                DeclaringType = null
            })
            .ToList();

        return new FunctionType
        {
            ParameterTypes = paramTypes,
            ReturnType = overload.ReturnType
        };
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

    private SemanticType CheckMatchExpression(MatchExpression matchExpr)
    {
        var scrutineeType = CheckExpression(matchExpr.Scrutinee);
        SemanticType? resultType = null;

        foreach (var arm in matchExpr.Arms)
        {
            using (_narrowingContext.EnterScope())
            {
                _symbolTable.EnterScope("match-arm");
                _controlFlowDepth++;
                CheckPattern(arm.Pattern, scrutineeType);

                if (arm.Guard != null)
                {
                    var guardType = CheckExpression(arm.Guard);
                    if (!IsTruthTestable(guardType))
                    {
                        AddError(
                            "Guard condition must be a boolean expression",
                            arm.Guard.LineStart, arm.Guard.ColumnStart,
                            code: DiagnosticCodes.Semantic.ConditionNotBoolean,
                            span: arm.Guard.Span);
                    }
                }

                var armType = CheckExpression(arm.Result);

                if (resultType == null)
                {
                    resultType = armType;
                }
                else if (!IsAssignable(armType, resultType) && armType is not UnknownType)
                {
                    // Try the reverse direction
                    if (IsAssignable(resultType, armType))
                    {
                        resultType = armType; // widen
                    }
                    else
                    {
                        AddError(
                            $"Match expression arm type '{armType.GetDisplayName()}' is incompatible with previous arm type '{resultType.GetDisplayName()}'",
                            arm.Result.LineStart, arm.Result.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: arm.Result.Span);
                    }
                }

                _controlFlowDepth--;
                _symbolTable.ExitScope();
            }
        }

        return resultType ?? SemanticType.Void;
    }
}
