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
            IntegerLiteral il => ClassifyIntegerLiteral(il),
            FloatLiteral fl => fl.Suffix?.ToUpperInvariant() switch
            {
                "F" => SemanticType.Float32,
                "M" => SemanticType.Decimal,
                _ => SemanticType.Double,
            },
            StringLiteral => SemanticType.Str,
            BytesLiteralExpression bytesLit => CheckBytesLiteral(bytesLit),
            BooleanLiteral => SemanticType.Bool,
            NoneLiteral noneLiteral => CheckNoneLiteral(noneLiteral),
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
            QuestionMarkExpression qm => CheckQuestionMarkExpression(qm),
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

        // #1138: A GenericFunctionType (identity[int]) is an internal carrier used to pass explicit
        // type arguments from an IndexAccess to its enclosing FunctionCall — never a first-class value.
        // It is legal only as the immediate callee of a call (`identity[int](x)`); CheckFunctionCall
        // marks that node in _currentCallCallee. If one surfaces on any other node, the user tried to
        // use it as a value (assign / pass / store / return); error here so it never escapes to codegen,
        // where GenerateIndexAccess would emit `identity[int]` as C# element access (CS0021 → SPY0908).
        // Substitute UnknownType and fall through to the error-recovery block below so the new error +
        // Unknown result marks this node as error recovery, preserving downstream cascade suppression.
        if (type is GenericFunctionType genericFnRef && !IsCurrentCallCallee(expr))
        {
            AddError(
                $"a generic function reference must be called; '{genericFnRef.FunctionSymbol.Name}[...]' cannot be used as a value",
                expr.LineStart, expr.ColumnStart,
                code: DiagnosticCodes.Semantic.GenericFunctionReferenceNotCalled,
                span: expr.Span);
            type = SemanticType.Unknown;
        }

        // #1192: the type-side counterpart. A generic TYPE reference (Box[int], difflib.Matcher[str],
        // Outer.Inner[int], tuple[int, str]) names a type: legal as the thing being constructed, as a
        // type argument, or in a type test — never as a value. Uncalled it reached codegen as C#
        // element access on a type name (CS0021/CS0119 behind SPY0908). The test is the RECORDED
        // GenericReference fact, never `type is GenericType`: every list and dict value has a
        // GenericType expression type and must stay untouched. NestedTypeRef is caught here too, even
        // though its expression typing stays on the value-indexing path, because the fact is recorded
        // either way; TupleTypeRef joins them because it is a type reference in every sense but its
        // spelling (#1200).
        if (expr is IndexAccess typeReferenceAccess
            && _semanticInfo.GetGenericReference(typeReferenceAccess) is
            {
                Kind: GenericReferenceKind.GenericTypeRef
                    or GenericReferenceKind.ModuleType
                    or GenericReferenceKind.NestedTypeRef
                    or GenericReferenceKind.TupleTypeRef
            } typeReference
            && !IsCurrentCallCallee(expr)
            && !IsCurrentMemberAccessQualifier(expr)
            && !_semanticInfo.IsTypeReference(expr)
            && !ReferenceEquals(UnwrapParenthesized(expr), _typeTestTypeArgument))
        {
            AddError(
                $"a generic type reference must be constructed; '{DescribeTypeReference(typeReference)}[...]' "
                + "cannot be used as a value",
                expr.LineStart, expr.ColumnStart,
                code: DiagnosticCodes.Semantic.GenericTypeReferenceNotConstructed,
                span: expr.Span);
            type = SemanticType.Unknown;
        }

        // #1168, #1170: the value-position rules for callable references. A reference in callee
        // position is exempt throughout — for the same reason as the #1138 arm above, the call path
        // resolves the target against the arguments — and parentheses do not make a callee a value
        // (IsCurrentCallCallee compares through them).
        if (expr is Identifier or MemberAccess && !IsCurrentCallCallee(expr))
        {
            // #1593: a function reference in value position (f = deprecated_add) is the deprecation
            // surface — the call site (f(1, 2)) invokes a delegate with no Symbol. Fire SPY0466 here.
            if (expr is Identifier refId
                && _semanticInfo.GetIdentifierSymbol(refId) is FunctionSymbol refFunc)
            {
                CheckDeprecatedUsage(refFunc, expr);
            }

            type = CheckValuePositionReference(expr, type);
        }

        // Track error recovery: if the result is UnknownType and either new errors were
        // emitted or sub-expressions were marked as error recovery, mark this expression
        // as error recovery too. This enables transitive propagation — if MathUtil.square
        // returns Unknown because MathUtil (a TypeSymbol ref) was Unknown, the MemberAccess
        // also gets marked as error recovery.
        if (type is UnknownType &&
            (_diagnostics.ErrorCount > errorsBefore || _errorRecoveryMarkCount > recoveryBefore))
        {
            MarkExpressionAsErrorRecovery(expr,
                ErrorRecoveryReason.Propagated(
                    "a sub-expression reported an error or claimed recovery"));
        }

        // Cache the result
        _semanticInfo.SetExpressionType(expr, type);
        return type;
    }

    /// <summary>
    /// True when <paramref name="expr"/> is the immediate callee of the FunctionCall currently being
    /// checked (see <c>_currentCallCallee</c>). Both nodes are compared through <see cref="UnwrapParenthesized"/>
    /// because a parenthesized callee like <c>(identity[int])(5)</c> is legal and CheckExpression recurses
    /// through <see cref="Parenthesized"/> wrappers — the guard must accept both the wrapper and the inner
    /// node. Reference equality: AST records use identity comparison via SemanticInfo (#1138).
    /// </summary>
    private bool IsCurrentCallCallee(Expression expr)
    {
        if (_currentCallCallee == null)
            return false;
        return ReferenceEquals(UnwrapParenthesized(_currentCallCallee), UnwrapParenthesized(expr));
    }

    /// <summary>
    /// True when <paramref name="expr"/> is the qualifier of the MemberAccess currently being checked
    /// (see <c>_currentMemberAccessQualifier</c>). A generic type reference is legal there —
    /// <c>Box[int].of(42)</c> names the type a static member is reached through, rather than using it
    /// as a value (#1192). Compared through parentheses for the same reason
    /// <see cref="IsCurrentCallCallee"/> is.
    /// </summary>
    private bool IsCurrentMemberAccessQualifier(Expression expr)
    {
        if (_currentMemberAccessQualifier == null)
            return false;
        return ReferenceEquals(UnwrapParenthesized(_currentMemberAccessQualifier), UnwrapParenthesized(expr));
    }

    /// <summary>The type's name as written, for the #1192 uncalled-type-reference diagnostic.</summary>
    private static string DescribeTypeReference(GenericReference reference)
        => reference.TargetSymbol?.Name ?? "type";

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

    private SemanticType ClassifyIntegerLiteral(IntegerLiteral il)
    {
        var result = Shared.IntegerLiteralClassifier.Classify(il.Value, il.Suffix);
        if (result.IsError)
        {
            AddError(result.ErrorMessage!,
                il.LineStart, il.ColumnStart,
                code: DiagnosticCodes.Semantic.IntegerLiteralOutOfRange,
                span: il.Span);
            return result.Type;
        }
        return result.Type;
    }

    private SemanticType CheckModifiedArgument(ModifiedArgument modArg)
    {
        // Handle inline out declarations: out value: int, out value: auto
        if (modArg.InlineName != null)
        {
            // Resolve the inline type annotation via TypeResolver
            // (TypeResolver returns UnknownType for "auto", which maps to C# var)
            var resolvedType = _typeResolver.ResolveTypeAnnotation(modArg.InlineType);

            // Register the variable in the current scope (follows walrus operator pattern,
            // including the escape's part in the binding's identity — #1326)
            var existingSymbol = _symbolTable.Lookup(modArg.InlineName, searchParents: false);
            if (existingSymbol is VariableSymbol existingVar
                && existingVar.IsNameBacktickEscaped == modArg.IsNameBacktickEscaped)
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
                    IsNameBacktickEscaped = modArg.IsNameBacktickEscaped,
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
                MarkExpressionAsErrorRecovery(modArg.Argument,
                    ErrorRecoveryReason.DeliberatelyPermissive(
                        "'auto' defers the inference to C#'s var at compile time"));
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

    /// <summary>
    /// Types a bare <c>None</c> and, when its destination is an <see cref="OptionalType"/>, records
    /// the materialization the emitter applies (#1478).
    ///
    /// <para>The literal's TYPE is unchanged — <see cref="VoidType"/>, the value that means "no
    /// value" and is assignable to both nullable and optional destinations. What is recorded is a
    /// LOWERING: the same <c>None</c> token emits C# <c>null</c> for a <c>T | None</c> destination
    /// and <c>Optional&lt;T&gt;.None</c> for a <c>T?</c> one, and only the checker knows which
    /// destination it landed in. The emitter used to answer this from its own ambient target-type
    /// context, which it could apply only at the direct value sites; an argument got no conversion
    /// and emitted a bare <c>null</c> into a <c>Sharpy.Optional&lt;int&gt;</c> slot — CS1503 behind
    /// SPY0908, because that Optional is a struct. Per Critical Rule 2 the decision is semantic-side
    /// and the emitter reads it.</para>
    ///
    /// <para>Gated on <c>_parameterTypedArgument</c>, not on <c>_expectedType</c> alone.
    /// <c>_expectedType</c> is a general "what does this position want" channel and is NOT
    /// necessarily this node's destination — in <c>x: int? = f(None)</c> the enclosing declaration's
    /// type is still visible while the argument is checked, and reading it here would record
    /// <c>Optional&lt;int&gt;.None</c> for an argument whose parameter is a nullable reference. The
    /// binding is what makes the read sound; see the field's own comment.</para>
    /// </summary>
    private SemanticType CheckNoneLiteral(NoneLiteral noneLiteral)
    {
        if (_expectedType is OptionalType optionalTarget
            && ReferenceEquals(_parameterTypedArgument, noneLiteral))
        {
            _semanticInfo.SetOptionalNoneMaterialization(noneLiteral, optionalTarget);
        }

        return SemanticType.Void;
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

        // A name a `from M import *` bound over a builtin is ambiguous WHERE IT IS USED, not at the
        // import — C#'s CS0104 rule (#1324). Reporting at the import would refuse
        // `from numpy import *` in a file that never touches `sum`, which is both louder and less
        // precise than the language Sharpy is bound to by Axiom 1. A backtick-escaped reference is
        // the user naming their own symbol and is never ambiguous.
        if (!id.IsNameBacktickEscaped
            && _symbolTable.AmbiguousGlobImports.TryGetValue(id.Name, out var globModule))
        {
            AddError(
                $"'{id.Name}' is an ambiguous reference between '{globModule}.{id.Name}' (bound by "
                + $"'from {globModule} import *') and the builtin '{id.Name}'. Qualify it as "
                + $"'{globModule}.{id.Name}', or reach the builtin as 'builtins.{id.Name}' (which "
                + "needs 'import builtins' — the module is not implicitly in scope), or import it "
                + $"explicitly with 'from {globModule} import {id.Name}' to state which one you mean",
                id.LineStart, id.ColumnStart,
                code: DiagnosticCodes.Validation.AmbiguousGlobImportOfBuiltin,
                span: id.Span);
            return SemanticType.Unknown;
        }

        var (symbol, escapeDeclaredShadow) = LookupBySpelling(id);

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
                MarkExpressionAsErrorRecovery(id,
                    ErrorRecoveryReason.AlreadyReported(
                        "the import that would define this name failed (SPY0300)"));
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

            // The name exists — escape-declared — and the bare spelling found no builtin to fall
            // back to (#1328). Say that, instead of an edit-distance guess: the declaration is
            // usually a few lines up, and the only thing wrong is the missing escape.
            if (escapeDeclaredShadow)
            {
                AddError(
                    $"'{id.Name}' is declared with a backtick escape (`{id.Name}`), which makes the "
                    + $"escaped spelling its name. Write `{id.Name}` to refer to it — a bare "
                    + $"'{id.Name}' denotes the builtin namespace the escape steps out of, and no "
                    + "builtin has that name.",
                    id.LineStart, id.ColumnStart,
                    code: DiagnosticCodes.Semantic.UndefinedVariable,
                    span: id.Span);
                return SemanticType.Unknown;
            }

            var message = $"Undefined identifier '{id.Name}'";
            string? suggestedName = null;

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
                suggestedName = FindSuggestion(id.Name);
                if (suggestedName != null)
                    message += $". Did you mean '{suggestedName}'?";
            }

            AddError(message,
                id.LineStart, id.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedVariable,
                span: id.Span,
                data: SuggestionData(suggestedName));
            return SemanticType.Unknown;
        }

        // Check if this is an error recovery symbol — a name whose DECLARATION was already refused,
        // so a reference to it must not cascade. Two shapes reach here: a failed import (SPY0300)
        // and a refused variadic property/event accessor parameter (SPY0496, #1462), both of which
        // bind the symbol as Unknown with IsErrorRecovery set. The diagnostic that refused it was
        // already reported, so the reference is legibly error recovery rather than an unexplained
        // Unknown.
        if (symbol.IsErrorRecovery)
        {
            _semanticInfo.SetIdentifierSymbol(id, symbol);
            MarkExpressionAsErrorRecovery(id,
                ErrorRecoveryReason.AlreadyReported(
                    "the symbol's declaration was already refused (a failed import, or a variadic "
                    + "accessor parameter refused by SPY0496)"));
            return SemanticType.Unknown;
        }

        _semanticInfo.SetIdentifierSymbol(id, symbol);

        // Check if this identifier has a narrowed type. Expression-level narrowings (the `and`
        // right-hand side, match arms) live in _narrowingContext and take precedence; statement-level
        // control-flow narrowings come from the CFG dataflow facts resolved against the variable's
        // live type (#1042). Each narrowing that implies an accessor also records a
        // NarrowedReadLowering so codegen applies it without re-deriving flow (#1081). Type-test
        // operands (`x is (not) None`, isinstance subjects) are exempt — they read the raw value.
        if (!ReferenceEquals(id, _typeTestOperand))
        {
            if (_narrowingContext.TryGetNarrowing(id.Name, out var contextNarrowed, out var contextLowering))
            {
                // Persist the narrowed type for code generation and tooling (ISemanticQuery).
                _semanticInfo.SetNarrowedType(id, contextNarrowed!);
                if (contextLowering != null)
                    RecordNarrowedReadLowering(id, contextLowering);
                return contextNarrowed!;
            }

            if (symbol is VariableSymbol narrowableVar
                && ResolveNarrowedTypeFromFacts(id.Name, GetVariableType(narrowableVar)) is { } factRead)
            {
                _semanticInfo.SetNarrowedType(id, factRead.Type);
                RecordNarrowedReadLowering(id, factRead.Lowering);
                return factRead.Type;
            }
        }

        var identifierType = symbol switch
        {
            VariableSymbol varSymbol => GetVariableType(varSymbol),
            FunctionSymbol funcSymbol => FunctionType.FromParameters(funcSymbol.Parameters, funcSymbol.ReturnType),
            ModuleSymbol moduleSymbol => new ModuleType { Symbol = moduleSymbol },
            // Type aliases are transparent in every position (#1527): resolve the alias
            // to its target and type the identifier as if it were the target's own spelling.
            TypeAliasSymbol aliasSymbol => ResolveTypeAliasInExpressionPosition(aliasSymbol),
            // Primitive type names (int, str, bool, float, etc.) used as function references
            // (e.g., map(int, items)) get a synthesized FunctionType so downstream consumers
            // like BuiltinReturnTypeInference can extract the return type.
            // bytes joins this gate only outside member-access qualifier position, because
            // bytes.fromhex needs the type-reference path (#1583, #1347).
            // Non-primitive TypeSymbols remain Unknown — resolved at FunctionCall level.
            TypeSymbol ts when PrimitiveCatalog.IsPrimitive(ts.Name) =>
                SynthesizePrimitiveFunctionType(ts),
            TypeSymbol ts when ts.Name == Shared.BuiltinNames.Bytes
                && !IsCurrentMemberAccessQualifier(id) =>
                SynthesizePrimitiveFunctionType(ts),
            TypeSymbol => SemanticType.Unknown,
            _ => SemanticType.Unknown
        };

        // Mark intentional Unknown types: non-primitive TypeSymbol references and unhandled
        // symbol kinds are not errors — they're expected gaps handled at higher levels (e.g., FunctionCall).
        // Primitive TypeSymbols get a FunctionType above and should NOT be marked as error recovery.
        if (identifierType is UnknownType && symbol is not null)
        {
            MarkExpressionAsErrorRecovery(id,
                ErrorRecoveryReason.DeliberatelyPermissive(
                    "a non-primitive type reference is typed at the call site, not here"));
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
    private SemanticType ResolveTypeAliasInExpressionPosition(TypeAliasSymbol aliasSymbol)
    {
        if (aliasSymbol.TypeAnnotation == null)
            return SemanticType.Unknown;

        var expanded = _typeResolver.ResolveTypeAnnotation(aliasSymbol.TypeAnnotation);
        if (expanded is BuiltinType bt)
        {
            var registryType = _symbolTable.BuiltinRegistry.GetType(bt.Name);
            if (registryType != null && PrimitiveCatalog.IsPrimitive(registryType.Name))
                return SynthesizePrimitiveFunctionType(registryType);
            return new UserDefinedType { Name = bt.Name, Symbol = registryType };
        }

        if (expanded is UserDefinedType { Symbol: TypeSymbol ts })
        {
            if (PrimitiveCatalog.IsPrimitive(ts.Name)
                && ReferenceEquals(ts, _symbolTable.BuiltinRegistry.GetType(ts.Name)))
                return SynthesizePrimitiveFunctionType(ts);
            return SemanticType.Unknown;
        }

        return SemanticType.Unknown;
    }

    private SemanticType ResolveModuleExportedAlias(TypeAliasSymbol aliasSymbol)
    {
        if (aliasSymbol.TypeAnnotation == null)
            return SemanticType.Unknown;

        var expanded = _typeResolver.ResolveTypeAnnotation(aliasSymbol.TypeAnnotation);
        if (expanded is BuiltinType bt)
        {
            var registryType = _symbolTable.BuiltinRegistry.GetType(bt.Name);
            if (registryType != null && PrimitiveCatalog.IsPrimitive(registryType.Name))
                return SynthesizePrimitiveFunctionType(registryType);
            return new UserDefinedType { Name = bt.Name, Symbol = registryType };
        }

        if (expanded is UserDefinedType udt)
        {
            if (udt.Symbol is TypeSymbol ts && PrimitiveCatalog.IsPrimitive(ts.Name)
                && ReferenceEquals(ts, _symbolTable.BuiltinRegistry.GetType(ts.Name)))
                return SynthesizePrimitiveFunctionType(ts);
            return udt;
        }

        return SemanticType.Unknown;
    }

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

        // Register the walrus target variable in the current scope. The escape is part of the
        // binding's identity, so a `` `len` := 5 `` never rebinds a bare `len` already in scope and
        // vice versa: they are two names that happen to share a spelling (#1326).
        var existingSymbol = _symbolTable.Lookup(walrus.Target, searchParents: false);
        if (existingSymbol is VariableSymbol existingVar
            && existingVar.IsNameBacktickEscaped == walrus.IsNameBacktickEscaped)
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
                IsNameBacktickEscaped = walrus.IsNameBacktickEscaped,
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
        // The subject gets the same #1370 treatment as the statement form's: a Cast lowering here
        // makes the emitted switch EXPRESSION statically total, and the trailing `case _` arm draws
        // CS8510 instead of CS0162 — same defect, different diagnostic.
        //
        // Resolve the subject against the facts at its evaluation point, exactly as CheckMatch does
        // for a statement's subject (#1299). The CFG now records match-EXPRESSION subjects too
        // (#1502, ControlFlowGraphBuilder.CollectMatchExpressionSubjects), so FactsBeforeBranch
        // returns the recorded out-set — the `??` fallback is safe rather than clearing the facts the
        // subject inherits (the mutation-proven trap this comment used to forbid). The `??` remains
        // load-bearing where there is no flow analysis (module body). CheckStatement's finally
        // restores _currentFacts.
        _currentFacts = _narrowingFlow?.FactsBeforeBranch(matchExpr.Scrutinee) ?? _currentFacts;

        SemanticType scrutineeType;
        using (ScopedValue.Push(ref _matchSubjectOperand, UnwrapParenthesized(matchExpr.Scrutinee)))
            scrutineeType = CheckExpression(matchExpr.Scrutinee);

        scrutineeType = ApplyVoidScrutineePolicy(matchExpr.Scrutinee, scrutineeType);

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
                    var (guardTruthTestable, guardTruthLowering) = ClassifyTruthiness(guardType);
                    if (!guardTruthTestable)
                    {
                        AddError(
                            "Guard condition must be a boolean expression",
                            arm.Guard.LineStart, arm.Guard.ColumnStart,
                            code: DiagnosticCodes.Semantic.ConditionNotBoolean,
                            span: arm.Guard.Span);
                    }
                    else
                    {
                        _semanticInfo.SetTruthinessLowering(arm.Guard, guardTruthLowering);
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
            moduleSymbol.Exports.Add(typeSymbol.Name, typeSymbol);

        _symbolTable.TryDefine(moduleSymbol);
        _semanticInfo.SetIdentifierSymbol(id, moduleSymbol);

        return new ModuleType { Symbol = moduleSymbol };
    }
}
