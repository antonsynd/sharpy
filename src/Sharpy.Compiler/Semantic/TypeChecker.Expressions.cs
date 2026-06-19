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
            BytesLiteralExpression bytesLit => CheckBytesLiteral(bytesLit),
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
            DictSpreadComprehension dictSpreadComp => CheckDictSpreadComprehension(dictSpreadComp),
            ConditionalExpression cond => CheckConditionalExpression(cond),
            LambdaExpression lambda => CheckLambda(lambda),
            TypeCoercion coercion => CheckTypeCoercion(coercion),
            TypeCheck typeCheck => CheckTypeCheck(typeCheck),
            MaybeExpression maybeExpr => CheckMaybeExpression(maybeExpr),
            TryExpression tryExpr => CheckTryExpression(tryExpr),
            Parenthesized paren => CheckExpression(paren.Expression),
            FStringLiteral fstr => CheckFStringLiteral(fstr),
            TStringLiteral tstr => CheckTStringLiteral(tstr),
            EllipsisLiteral => SemanticType.Void,
            SliceAccess sliceAccess => CheckSliceAccess(sliceAccess),
            MultiAxisAccess multiAxis => CheckMultiAxisAccess(multiAxis),
            WalrusExpression walrus => CheckWalrusExpression(walrus),
            AwaitExpression awaitExpr => CheckAwaitExpression(awaitExpr),
            SpreadElement spread => CheckExpression(spread.Value),
            StarExpression star => CheckExpression(star.Operand),
            ModifiedArgument modArg => CheckModifiedArgument(modArg),
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

    private SemanticType CheckModifiedArgument(ModifiedArgument modArg)
    {
        // Handle inline out declarations: out value: int, out value: auto
        if (modArg.InlineName != null)
        {
            // Resolve the inline type annotation via TypeResolver
            // (TypeResolver returns UnknownType for "auto", which maps to C# var)
            var resolvedType = _typeResolver.ResolveTypeAnnotation(modArg.InlineType);

            // Register the variable in the current scope (follows walrus operator pattern)
            var existingSymbol = _symbolTable.Lookup(modArg.InlineName, searchParents: false);
            if (existingSymbol is VariableSymbol existingVar)
            {
                // Variable already exists in this scope — update its type
                SemanticBinding.SetVariableType(existingVar, resolvedType);
            }
            else
            {
                // New variable — create and register it
                var newSymbol = new VariableSymbol
                {
                    Name = modArg.InlineName,
                    Kind = SymbolKind.Variable,
                    Type = resolvedType,
                    IsConstant = false,
                    DeclarationLine = modArg.Argument.LineStart,
                    DeclarationColumn = modArg.Argument.ColumnStart,
                    NameDeclarationLine = modArg.Argument.LineStart,
                    NameDeclarationColumn = modArg.Argument.ColumnStart
                };
                _symbolTable.Define(newSymbol);
                SemanticBinding.SetVariableType(newSymbol, resolvedType);
            }

            // Record type for the Argument (Identifier) sub-expression so codegen can find it
            _semanticInfo.SetExpressionType(modArg.Argument, resolvedType);

            // For 'auto', TypeResolver returns UnknownType — mark as error recovery
            // so SPY0907 doesn't fire (C# var handles the inference at compile time)
            if (resolvedType is UnknownType)
            {
                MarkExpressionAsErrorRecovery(modArg.Argument);
            }

            // Return the resolved type; CheckExpression caches it on the ModifiedArgument node
            return resolvedType;
        }

        if (modArg.Modifier is Parser.Ast.ParameterModifier.Ref or Parser.Ast.ParameterModifier.Out)
        {
            if (modArg.Argument is not (Identifier or MemberAccess or IndexAccess))
            {
                AddError($"'{modArg.Modifier.ToString().ToLowerInvariant()}' argument must be a variable",
                    modArg.Argument.LineStart, modArg.Argument.ColumnStart,
                    code: DiagnosticCodes.Semantic.ModifierRequiresVariable,
                    span: modArg.Argument.Span);
            }
        }
        return CheckExpression(modArg.Argument);
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

            // Inline CLR namespace resolution: a backtick-escaped identifier (e.g., `System`)
            // that hasn't been imported can resolve directly to a .NET namespace.
            if (id.IsNameBacktickEscaped && ModuleRegistry != null)
            {
                var resolved = TryResolveInlineClrNamespace(id);
                if (resolved != null)
                    return resolved;
            }

            if (id.Name == "_")
            {
                AddError("'_' placeholder can only be used inside function call arguments for partial application (e.g., f(_, 2)). "
                    + "If you intended a throwaway variable, assign it first: _ = ...",
                    id.LineStart, id.ColumnStart, code: DiagnosticCodes.Parser.PlaceholderOutsideCallOrOperator,
                    span: id.Span);
                return SemanticType.Unknown;
            }

            var message = $"Undefined identifier '{id.Name}'";

            // Enhanced diagnostic for Python developers: if the identifier was
            // declared inside a now-exited block scope (for/if/while/try/with/
            // except/match/comprehension), explain why it isn't visible here.
            // Sharpy uses block scoping (unlike Python, where for-loop and
            // except variables leak into the enclosing function).
            if (_symbolTable.TryGetExitedVariable(id.Name, out var blockType, out var declLine))
            {
                if (blockType == "comprehension")
                {
                    message += $". Note: '{id.Name}' was declared inside a comprehension"
                        + " — in Sharpy, comprehension variables (including walrus assignments)"
                        + " don't leak to outer scope (unlike Python 3.8+)";
                }
                else
                {
                    message += $". Note: '{id.Name}' was declared inside a {blockType} block"
                        + $" at line {declLine} — in Sharpy, block-scoped variables are"
                        + " not accessible outside their block (unlike Python)";
                }
            }
            else
            {
                var suggestion = FindSuggestion(id.Name);
                if (suggestion != null)
                    message += $". Did you mean '{suggestion}'?";
            }

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
            FunctionSymbol funcSymbol => FunctionType.FromParameters(funcSymbol.Parameters, funcSymbol.ReturnType),
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
                DeclarationColumn = walrus.ColumnStart,
                NameDeclarationLine = walrus.LineStart,
                NameDeclarationColumn = walrus.ColumnStart
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

    /// <summary>
    /// Resolves a backtick-escaped identifier (e.g., `System`) to a synthetic ModuleSymbol
    /// representing a .NET namespace, without requiring an explicit import statement.
    /// Returns null if the identifier doesn't map to a known .NET namespace.
    /// </summary>
    private SemanticType? TryResolveInlineClrNamespace(Identifier id)
    {
        if (!ModuleRegistry!.IsNetNamespace(id.Name))
            return null;

        var netNamespace = ModuleRegistry.GetNetNamespace(id.Name);
        if (netNamespace == null)
            return null;

        var moduleSymbol = new ModuleSymbol
        {
            Name = id.Name,
            Kind = SymbolKind.Module,
            FilePath = $".net:{id.Name}",
            IsNetModule = true,
            NetNamespaceName = netNamespace,
            IsNameBacktickEscaped = true
        };

        foreach (var typeSymbol in ModuleRegistry.GetNamespaceTypes(id.Name))
            moduleSymbol.Exports[typeSymbol.Name] = typeSymbol;

        _symbolTable.TryDefine(moduleSymbol);
        _semanticInfo.SetIdentifierSymbol(id, moduleSymbol);

        return new ModuleType { Symbol = moduleSymbol };
    }
}
