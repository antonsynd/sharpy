using System.Diagnostics;
using System.Globalization;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Function calls, overload resolution, argument validation
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckFunctionCall(FunctionCall call)
    {
        // The canonical callee (#1170). Redundant parentheses around a callee never change what a
        // call denotes, so every shape dispatch in this method — construction detection, special
        // forms, overload resolution, generic-reference resolution, argument binding — reads this
        // instead of `call.Function`. Computed once here and threaded into the helpers so a new arm
        // cannot reintroduce surface-syntax dispatch. The emitter unwraps the same way (#1147), so
        // node-keyed facts recorded against `callee` are the ones codegen looks up.
        //
        // `call.Function` is still what gets CheckExpression'd (the wrapper node needs its own
        // recorded type) and what diagnostics span (the parentheses are part of what the user wrote).
        var callee = UnwrapParenthesized(call.Function);

        // Handle functools.partial(f, ...) — compatibility shim that desugars to a placeholder lambda
        if (FunctoolsPartialHelper.IsFunctoolsPartialCall(call, _symbolTable))
        {
            return CheckFunctoolsPartialCall(call);
        }

        // Handle None() — empty Optional constructor
        var noneResult = CheckNoneConstruction(call, callee);
        if (noneResult != null)
            return noneResult;

        // Check for invalid tagged union constructor usage (wrong arity)
        if (callee is Identifier taggedId && call.KeywordArguments.Length == 0
            && _symbolTable.BuiltinRegistry.IsTaggedUnionConstructor(taggedId.Name)
            && _symbolTable.Lookup(taggedId.Name) == null)
        {
            if (call.Arguments.Length == 0)
            {
                var code = taggedId.Name == "Some"
                    ? DiagnosticCodes.Semantic.InvalidSomeConstructor
                    : DiagnosticCodes.Semantic.InvalidOkErrConstructor;
                AddError($"'{taggedId.Name}()' requires exactly one argument",
                    call.LineStart, call.ColumnStart, code: code, span: call.Span);
                return SemanticType.Unknown;
            }
            if (call.Arguments.Length > 1)
            {
                var code = taggedId.Name == "Some"
                    ? DiagnosticCodes.Semantic.InvalidSomeConstructor
                    : DiagnosticCodes.Semantic.InvalidOkErrConstructor;
                AddError($"'{taggedId.Name}()' takes exactly one argument, got {call.Arguments.Length}",
                    call.LineStart, call.ColumnStart, code: code, span: call.Span);
                foreach (var arg in call.Arguments)
                    CheckExpression(arg);
                return SemanticType.Unknown;
            }
        }

        // Check if this is a tagged union constructor shorthand (Some/Ok/Err)
        if (callee is Identifier constructorId && call.Arguments.Length == 1 && call.KeywordArguments.Length == 0)
        {
            var constructorResult = TryCheckTaggedUnionConstructor(constructorId, call);
            if (constructorResult != null)
                return constructorResult;
        }

        // Detect IIFE: (lambda x: ...)(args) — check arguments first to infer lambda param types
        if (callee is LambdaExpression iifeLambda && call.KeywordArguments.Length == 0)
        {
            return CheckIifeLambdaCall(call, iifeLambda);
        }

        // type(None) has no Sharpy equivalent — NoneType is not a real type
        if (callee is Identifier { Name: "type" } && call.Arguments.Length == 1
            && call.Arguments[0] is NoneLiteral)
        {
            AddError("type(None) is not supported; NoneType has no Sharpy equivalent",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UnsupportedTypeNone,
                span: call.Span);
            return SemanticType.Unknown;
        }

        // Check if this is a null conditional method call (obj?.method())
        bool isNullConditionalCall = callee is MemberAccess { IsNullConditional: true };
        bool isOptionalNullConditional = false;

        // Check the called expression type first. Mark the callee node so the CheckExpression choke
        // point accepts a GenericFunctionType here (`identity[int](x)` is legal) while still erroring
        // (SPY0335) if one surfaces on any other node (#1138). The scope restores on every exit path,
        // so nested calls (`outer(identity[int](5))`) recover the enclosing callee correctly.
        SemanticType calleeType;
        using (ScopedValue.Push(ref _currentCallCallee, call.Function))
            calleeType = CheckExpression(call.Function);

        // The call-only alias is retired (#1248), so nothing binds ConstructorReferenceType any more
        // and the callee-substitution arm that dispatched through it is gone. The carrier is now a
        // transient classification value inside CheckConstructorReference alone: a callee typed as
        // one means the checker bound it somewhere it should have refused, which is a bug in the
        // checker rather than a shape to tolerate.
        Debug.Assert(calleeType is not ConstructorReferenceType,
            "A callee typed as ConstructorReferenceType means a binding kept the carrier. "
            + "No tier produces one since #1248 retired the call-only alias.");

        // After checking the callee, determine if this is ?. on an Optional object
        if (isNullConditionalCall && callee is MemberAccess nullCondMa)
        {
            var objType = _semanticInfo.GetExpressionType(nullCondMa.Object);
            isOptionalNullConditional = objType is OptionalType;
        }

        // Validate event invoke restrictions and __init__() call tracking
        var initEventResult = ValidateInitAndEventCalls(call, callee);
        if (initEventResult != null)
            return initEventResult;

        // Resolve function symbol early for constructor inference on arguments
        var (earlyFuncSymbol, earlyParamOffset) = ResolveEarlyFunctionSymbol(call, callee);

        // #1206: `lst.select(f)` — an extension call with NO type arguments written, which nothing
        // resolves today. Bind what the receiver determines and feed the partially-closed signature
        // into the SAME early-symbol channel member calls already use, so the lambda gets its expected
        // type from the existing machinery rather than a second staging loop. `earlyFuncSymbol` is null
        // here by construction: ResolveEarlyFunctionSymbol found no such member on the receiver, which
        // is most of why this call reached the seam at all. Returns null — changing nothing — unless
        // all five gates hold; see TryBeginStagedExtensionCall.
        var stagedExtensionCall = TryBeginStagedExtensionCall(call, callee, calleeType);
        if (stagedExtensionCall != null)
            earlyFuncSymbol = stagedExtensionCall.Signature;

        // Check arguments and keyword arguments, collecting their types. isinstance's subject
        // reads the honest, un-narrowed value (see _typeTestOperand): a narrowing cast on the
        // operand would presuppose the very fact the test is checking.
        var calleeFunctionType = calleeType as FunctionType ?? ClosedExtensionSignature(callee);
        // Not a type test: each scope pushes the field's CURRENT value, so an enclosing type test's
        // operand and type argument survive rather than being cleared for this call's arguments. The
        // type-argument scope carries the conjunction — the second argument only names a type when
        // there IS a second argument.
        var isTypeTest = callee is Identifier { Name: BuiltinNames.Isinstance } && call.Arguments.Length > 0;
        List<SemanticType> argTypes;
        Dictionary<string, SemanticType> kwargTypes;
        // The direct-argument set covers every internal argument path CheckCallArguments takes, so the
        // constructor-reference rules see the same exemption regardless of which one runs (#1182).
        using (ScopedValue.Push(ref _typeTestOperand,
                   isTypeTest ? UnwrapParenthesized(call.Arguments[0]) : _typeTestOperand))
        using (ScopedValue.Push(ref _typeTestTypeArgument,
                   isTypeTest && call.Arguments.Length > 1
                       ? UnwrapParenthesized(call.Arguments[1])
                       : _typeTestTypeArgument))
        using (ScopedValue.Push(ref _currentCallArguments, DirectArgumentSetOf(call)))
        {
            (argTypes, kwargTypes) = CheckCallArguments(call, callee, earlyFuncSymbol, earlyParamOffset, calleeFunctionType);
        }

        // The arguments are checked, so the type parameters the receiver left open are now knowable:
        // close the vector and record the fact the call's type is read from (#1206). Records nothing on
        // any failure, leaving the call exactly as permissive as it is today. Runs here, before the
        // call's result type is computed, because that is what reads the fact.
        if (stagedExtensionCall != null)
            CompleteStagedExtensionCall(stagedExtensionCall, argTypes);

        var totalArgCount = argTypes.Count + kwargTypes.Count;

        // Decide what an `isinstance` TYPE OPERAND denotes, once, here. Runs after argument checking
        // because the accepted cases need the subject's static type (to fill a bare generic's vector)
        // and the module-qualified operand's recorded type. Rejects every shape that cannot lower to
        // one closed type, so none reaches codegen (#1207, #1213).
        ClassifyTypeTestOperand(call, callee, argTypes);

        MarkTypeReferenceArguments(call);
        MarkTypeFactoryArguments(call, callee);

        // Record how each argument in an iterable position binds there, so codegen projects it
        // (d.Keys() for a dict, a typed array for a tuple, unchanged otherwise) and the
        // argument-binding sites accept it as iterable[element] (#1154, #1159, #1198). One choke
        // point for the whole ring — the emitter is a pure applier (repo rule 2). Runs before every
        // dispatch path, because each of them binds arguments and must see the same marks.
        RecordIterableArgumentMarks(call, callee);

        // Try to get the function symbol directly for better validation
        FunctionSymbol? funcSymbol = null;

        // Handle generic type/function instantiation: Box[int](42) or identity[int](42)
        var genericResult = CheckGenericInstantiation(call, callee, calleeType, argTypes, kwargTypes, totalArgCount);
        if (genericResult != null)
            return genericResult;

        if (callee is Identifier id)
        {
            // The escape decides which namespace this callee names, and it decides it BOTH ways
            // (SPY0212's other half). A bare spelling is the builtin, always — so it must not be
            // answered by a user symbol that only exists because it was escaped. An escaped
            // spelling is the user's symbol — so the name-keyed builtin paths below must not claim
            // it. Mirrors the emitter's rule at RoslynEmitter.Expressions.Access.cs, and the two
            // have to agree: when they did not, the checker validated a bare `len()` against a
            // user `` class `len` `` while codegen emitted Sharpy.Builtins.Len() — a SPY0908 ICE
            // where a declaration elsewhere in the file changed the diagnosis of an unrelated call.
            var symbol = _symbolTable.Lookup(id.Name);
            if (symbol != null && id.IsNameBacktickEscaped != symbol.IsNameBacktickEscaped)
                symbol = null;

            // A user binding that spells a builtin name shadows it, exactly as any inner binding
            // shadows an outer one — that is what makes `def double`, `def isinstance` and a
            // parameter named `id` legal (SPY0483 warns; it does not refuse). The name-keyed
            // inference below must therefore yield to it, or the checker validates the call against
            // the BUILTIN while the emitter calls the USER's symbol: a bare `def len` was checked as
            // Sharpy.Builtins.Len and emitted as the user's Len, giving CS1503 -> SPY0908, and a
            // nested `def hash` silently ran the builtin (#1240, #1241). Identity decides, not the
            // name: when nothing shadows it, Lookup answers with the registry's own symbol.
            var shadowsBuiltin = symbol != null
                && !_symbolTable.BuiltinRegistry.IsBuiltinSymbol(symbol);

            // Data-driven builtin function return type inference (len, hash, reversed, sorted, min, max)
            if (!id.IsNameBacktickEscaped && !shadowsBuiltin)
            {
                var builtinReturn = BuiltinReturnTypeInference.InferReturnType(
                    id.Name, argTypes, _typeInference);
                if (builtinReturn != null)
                {
                    ValidateMinMaxValueFormKey(id, call, argTypes, kwargTypes);
                    return builtinReturn;
                }
            }

            // Special handling for constructor calls (calling a type)
            if (symbol is TypeSymbol typeSymbol)
            {
                // For primitive types (int, float, str, bool, long, etc.), route to builtin function overloads
                // instead of treating as constructor. This matches Python semantics where int(x) calls
                // the int conversion function, not constructs a new int object.
                // Registry IDENTITY, not name: `int(x)` routes to the conversion function only when
                // the type it resolved to IS the registry's own. A user type that merely spells a
                // primitive name (reachable now only backticked, since the bare spelling is
                // refused) owns its constructor — the same discipline ConstructorReferenceOf uses.
                var primitiveOverloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
                if (primitiveOverloads != null && primitiveOverloads.Count > 0
                    && PrimitiveCatalog.IsPrimitive(id.Name)
                    && ReferenceEquals(typeSymbol, _symbolTable.BuiltinRegistry.GetType(id.Name)))
                {
                    // Route to builtin function overload resolution below
                    // (fall through to overload handling)
                }
                else
                {
                    return CheckConstructorCall(call, typeSymbol, argTypes, kwargTypes, totalArgCount);
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

                // A binding whose initializer was already rejected has type Unknown; calling it is
                // not a second, separate error. Without this the SPY0336 ambiguous-reference report
                // for `g = xs.pop` is followed by a bogus "'g' is not callable" at every use (#1170).
                if (calleeType is UnknownType)
                {
                    MarkExpressionAsErrorRecovery(call);
                    return SemanticType.Unknown;
                }

                // Check if it's a variable with a FunctionType or delegate type - those are callable.
                // Use calleeType (the narrowed type) so an Optional function type narrowed via
                // `is not None` is recognized as callable.
                if (symbol is VariableSymbol varSym &&
                    (calleeType is FunctionType
                     || TryGetDelegateInvokeMethod(calleeType) != null))
                {
                    // Let the FunctionType / delegate handling below deal with this
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
            var overloadResult = ResolveBuiltinOverload(id, argTypes, totalArgCount, call);
            if (overloadResult != null)
                return overloadResult;

            // Resolve user-defined module-level function overloads (same compilation)
            {
                var userOverloadResult = ResolveUserDefinedFunctionOverload(
                    id, argTypes, totalArgCount, call);
                if (userOverloadResult != null)
                    return userOverloadResult;
            }

            // Resolve imported function overloads (e.g., from os.path import join)
            {
                var importedOverloadResult = ResolveImportedFunctionOverload(
                    id, argTypes, totalArgCount, call);
                if (importedOverloadResult != null)
                    return importedOverloadResult;
            }
        }
        // Handle union case construction: Shape.Circle(5.0) → new Shape.Circle(5.0)
        else if (callee is MemberAccess unionCaseAccess
            && calleeType is UserDefinedType caseUdt
            && caseUdt.Symbol?.BaseType is { TypeKind: TypeKind.Union } unionBaseSymbol)
        {
            return CheckUnionCaseConstruction(call, caseUdt, unionBaseSymbol, argTypes);
        }
        // Handle member access function calls (e.g., module.function() or obj.method())
        // Skip super() calls - they're already validated by ValidateSuperMemberAccess
        else if (callee is MemberAccess memberAccessCall && memberAccessCall.Object is not SuperExpression)
        {
            // Module-qualified constructor call (e.g., fractions.Fraction(1, 2)): resolve the
            // member to an exported TypeSymbol and route into the shared constructor-checking
            // path, which validates arguments, abstract-class usage, and deprecation.
            if (TryResolveTypeSymbolFromMemberAccess(memberAccessCall) is { } moduleTypeSymbol)
            {
                return CheckConstructorCall(call, moduleTypeSymbol, argTypes, kwargTypes, totalArgCount);
            }

            funcSymbol = ResolveFunctionSymbolFromMemberAccess(memberAccessCall);

            // Try module function overloads (e.g., os.path.join with different arities)
            {
                var moduleOverloadResult = ResolveModuleFunctionOverload(
                    memberAccessCall, argTypes, totalArgCount, call,
                    isNullConditionalCall, isOptionalNullConditional);
                if (moduleOverloadResult != null)
                    return moduleOverloadResult;
            }

            // Try user-defined method overloads: either when no symbol was found,
            // or when the found symbol's method has multiple overloads on the owning type
            {
                var overloadResult = ResolveUserMethodOverload(
                    memberAccessCall, argTypes, totalArgCount, call,
                    isNullConditionalCall, isOptionalNullConditional);
                if (overloadResult != null)
                    return overloadResult;
            }

            // Builtin method overloads (dict.get, list.pop) are now handled by
            // ResolveUserMethodOverload above via discovery-populated metadata.
        }

        // If we have a FunctionSymbol, use it for validation (supports default parameters)
        if (funcSymbol != null)
        {
            return ValidateFunctionSymbolCall(call, funcSymbol, argTypes, kwargTypes, totalArgCount,
                isNullConditionalCall, isOptionalNullConditional);
        }

        // Fallback to FunctionType validation (no default parameter support)
        // Use the already-computed calleeType to avoid re-evaluating call.Function
        // (which causes double validation, e.g., super().__init__() being flagged as duplicate)
        if (calleeType is FunctionType ft)
        {
            return CheckLambdaCall(call, ft, argTypes, totalArgCount,
                isNullConditionalCall, isOptionalNullConditional);
        }

        // Handle delegate-typed variable invocation: extract the Invoke method and validate
        {
            var delegateInvoke = TryGetDelegateInvokeMethod(calleeType);
            if (delegateInvoke != null)
            {
                return ValidateFunctionSymbolCall(call, delegateInvoke, argTypes, kwargTypes, totalArgCount,
                    isNullConditionalCall, isOptionalNullConditional);
            }
        }

        // If callee type is Unknown, this is error recovery from a sub-expression.
        // Explicitly mark the FunctionCall as error recovery as a safety net — transitive
        // tracking in CheckExpression usually handles this, but some paths (e.g., property
        // type resolution) can return Unknown without marking or emitting an error.
        // Otherwise, the callee evaluated to a non-callable type — emit an error.
        if (calleeType is UnknownType)
        {
            MarkExpressionAsErrorRecovery(call);
        }
        else
        {
            AddError($"Expression of type '{calleeType.GetDisplayName()}' is not callable",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.UndefinedFunction,
                span: call.Function.Span);
        }
        return SemanticType.Unknown;
    }

    // ============================================================
    // isinstance type-operand classification (#1207, #1213)
    // ============================================================

    /// <summary>
    /// Decides what the TYPE OPERAND of an <c>isinstance(x, T)</c> call denotes, and records the
    /// resulting type test on the operand node for codegen and for narrowing to read.
    /// <para>
    /// This is the single authority on the question. Before it existed, nothing classified the operand
    /// during semantic analysis: any shape the checker tolerated reached the emitter, which re-derived
    /// the C# type from the operand's syntax and produced raw Roslyn errors for the shapes it could not
    /// spell — an open generic (<c>Box</c> → <c>typeof(Box&lt;T&gt;)</c>, CS0305, #1207) and a tuple of
    /// types (CS1503/CS0119, #1213), both surfacing as SPY0908. Un-lowerable shapes must be
    /// semantic-time diagnostics (#1146), so those two are now errors here and never reach codegen.
    /// </para>
    /// <para>
    /// The governing rule for what to accept is narrowing, not expressiveness: <b>a type test that
    /// compiles but cannot narrow is worse than a clean refusal.</b> That is why the tuple form stays
    /// rejected even though <c>Sharpy.Builtins.Isinstance(object, params Type[])</c> exists and would
    /// return a correct boolean — no narrowing fact is produced for a tuple operand and Sharpy has no
    /// usable union type to narrow to, so the binding would silently fail on the next line. It is also
    /// why the open generic is rejected rather than lowered to a <c>GetGenericTypeDefinition()</c>
    /// runtime check: a successful test would narrow to <c>Box[T]</c> for an unknown T, which is not
    /// spellable.
    /// </para>
    /// <para>
    /// Shapes this method does not recognise are left unrecorded, and the ordinary call path lowers
    /// them exactly as before — classification adds decisions, it does not remove fallbacks.
    /// </para>
    /// </summary>
    private void ClassifyTypeTestOperand(FunctionCall call, Expression callee, List<SemanticType> argTypes)
    {
        if (callee is not Identifier { Name: BuiltinNames.Isinstance } isinstanceId)
            return;
        if (call.Arguments.Length != 2 || call.KeywordArguments.Length != 0)
            return;

        // Shadowing guard, carried over from the hint this classifier's tuple diagnostic replaces
        // (TransitionWarningValidator.CheckIsinstanceSingleType). A user-defined `isinstance` — their
        // own function or a variable — is an ordinary call whose second argument is an ordinary value.
        // Builtins are seeded into the global scope, so the name always resolves; identity against the
        // registry's own overloads is what separates the builtin from a shadow.
        var resolvedCallee = _symbolTable.Lookup(isinstanceId.Name);
        var builtinIsinstance = _symbolTable.BuiltinRegistry.GetFunctionOverloads(BuiltinNames.Isinstance);
        if (resolvedCallee is not FunctionSymbol calleeFunction
            || builtinIsinstance == null
            || !builtinIsinstance.Contains(calleeFunction))
        {
            return;
        }

        // A @test-decorated function's `assert` is not an ordinary expression: the emitter rewrites
        // the whole statement into an xUnit assertion (RoslynEmitter.GenerateTestAssert), pre-empting
        // the call lowering this classifier feeds, and that rewrite carries its own isinstance forms —
        // including the multi-type tuple spelling rejected below, which it lowers to
        // `a is T1 || a is T2` for a boolean nobody narrows through. Rejecting there would break a form
        // that path handles correctly. The rewrite is a special-purpose test lowering, not a precedent
        // for general expression lowering, and this batch deliberately does not generalize it; its own
        // un-lowerable shapes belong to the sibling type-operand sweep.
        if (IsTestAssertTypeTest(call))
            return;

        var operandNode = call.Arguments[1];
        var typeOperand = UnwrapParenthesized(operandNode);
        var subjectType = argTypes.Count > 0 ? argTypes[0] : null;

        // The tuple spelling — Python's OR-of-types. Rejected by design; see the class-level rationale.
        if (typeOperand is TupleLiteral tuple)
        {
            var typeNames = string.Join(", ", tuple.Elements.Select(DescribeTypeOperand));
            AddError(
                $"isinstance() in Sharpy accepts only a single type argument, but a tuple of "
                    + $"{tuple.Elements.Length} types ({typeNames}) was passed. "
                    + "Unlike Python's `isinstance(x, (A, B))`, Sharpy keeps the form single-typed "
                    + "so that successful checks narrow to one concrete type. "
                    + "Combine multiple checks with `or` (e.g., "
                    + "`isinstance(x, A) or isinstance(x, B)`), or use a tagged union with `match`.",
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.Semantic.MultiTypeTypeTest,
                span: call.Span);
            return;
        }

        // A bare name is the only shape that can be an OPEN generic, so it is the only one that needs
        // the vector-filling rule; every other shape either names its type arguments or has none.
        if (typeOperand is Identifier typeId)
        {
            ClassifyBareTypeNameOperand(call, operandNode, typeId, subjectType);
            return;
        }

        if (ResolveTypeTestOperandType(typeOperand) is { } resolved)
            _semanticInfo.SetTypeTestLowering(operandNode, new TypeTestLowering(TypeTestLoweringKind.ClosedType, resolved));
    }

    /// <summary>
    /// Classifies a bare type NAME operand (<c>isinstance(x, Dog)</c>, <c>isinstance(x, list)</c>,
    /// <c>isinstance(x, Box)</c>). Three outcomes: a primitive or non-generic type is a closed test;
    /// an unparameterized builtin collection is a type-erased test against its protocol interface
    /// (#912); a generic type name gets its vector filled from the subject's own static type, and is
    /// rejected (SPY0345) when nothing determines it.
    /// </summary>
    private void ClassifyBareTypeNameOperand(
        FunctionCall call, Expression operandNode, Identifier typeId, SemanticType? subjectType)
    {
        if (ResolveBuiltinPrimitiveTypeName(typeId.Name) is { } primitive)
        {
            _semanticInfo.SetTypeTestLowering(operandNode, new TypeTestLowering(TypeTestLoweringKind.ClosedType, primitive));
            return;
        }

        if (_symbolTable.Lookup(typeId.Name) is not TypeSymbol typeSymbol)
            return;

        // list/set/dict written without type arguments: the test cannot know the element types, so it
        // erases to the non-generic protocol interface. BuildIsInstanceNarrowedType is what narrowing
        // resolves the same operand to, and it fills default `object` arguments so member access on the
        // narrowed value still resolves — the two answers stay the same object here by construction.
        if (typeSymbol.IsGeneric && BuiltinNames.IsErasableCollection(typeSymbol.Name))
        {
            _semanticInfo.SetTypeTestLowering(operandNode,
                new TypeTestLowering(TypeTestLoweringKind.ErasedBuiltinCollection, BuildIsInstanceNarrowedType(typeSymbol)));
            return;
        }

        if (!typeSymbol.IsGeneric)
        {
            _semanticInfo.SetTypeTestLowering(operandNode,
                new TypeTestLowering(TypeTestLoweringKind.ClosedType, BuildIsInstanceNarrowedType(typeSymbol)));
            return;
        }

        // A generic user type named without its arguments. .NET reifies generics, so `Box` alone names
        // no runtime type; fill the vector from the subject's own static type when it determines one.
        if (FillTypeArgumentsFromSubject(typeSymbol, subjectType) is { } closedGeneric)
        {
            _semanticInfo.SetTypeTestLowering(operandNode,
                new TypeTestLowering(TypeTestLoweringKind.ClosedType, closedGeneric));
            return;
        }

        var suggestion = string.Join(", ", typeSymbol.TypeParameters.Select(_ => "..."));
        AddError(
            $"'{typeId.Name}' is a generic type, so it does not name a single type to test against, "
                + $"and nothing at this call determines its type arguments. "
                + $"Write the closed spelling — for example `{BuiltinNames.Isinstance}(..., {typeId.Name}[{suggestion}])` — "
                + "or test against a non-generic base type. Unlike Python, Sharpy's generics are real "
                + "runtime types, and a successful open test could not narrow to a type you can write.",
            typeId.LineStart, typeId.ColumnStart,
            code: DiagnosticCodes.Semantic.OpenGenericTypeTest,
            span: typeId.Span ?? call.Span);
    }

    /// <summary>
    /// Fills a generic type's argument vector from the type test's SUBJECT — <c>isinstance(b, Box)</c>
    /// where <c>b: Box[int]</c> tests <c>Box[int]</c>. Only the subject's own instantiation is
    /// consulted (through Optional/nullable wrappers): walking a base chain would need argument
    /// substitution per level, and the rejection message names a spelling that always works, so a
    /// subject typed by a generic BASE class gets an actionable error rather than a guess.
    /// </summary>
    private static GenericType? FillTypeArgumentsFromSubject(TypeSymbol typeSymbol, SemanticType? subjectType)
    {
        var subject = subjectType switch
        {
            OptionalType optional => optional.UnderlyingType,
            NullableType nullable => nullable.UnderlyingType,
            _ => subjectType
        };

        return subject is GenericType generic
            && generic.TypeArguments.Count == typeSymbol.TypeParameters.Count
            && (ReferenceEquals(generic.GenericDefinition, typeSymbol) || generic.Name == typeSymbol.Name)
                ? generic
                : null;
    }

    /// <summary>
    /// Resolves a type-operand expression that already names its type arguments (or has none):
    /// a module-qualified name (<c>isinstance(x, mod.Type)</c>, #903) or a closed generic spelling
    /// (<c>isinstance(x, Box[int])</c>). Returns null for anything that does not denote a type, which
    /// leaves the call on the ordinary path it took before classification existed.
    /// </summary>
    private SemanticType? ResolveTypeTestOperandType(Expression typeOperand)
    {
        switch (UnwrapParenthesized(typeOperand))
        {
            case Identifier typeId:
                return ResolveBuiltinPrimitiveTypeName(typeId.Name)
                    ?? (_symbolTable.Lookup(typeId.Name) is TypeSymbol { IsGeneric: false } typeSymbol
                        ? new UserDefinedType { Symbol = typeSymbol, Name = typeSymbol.Name }
                        : null);

            // A module-qualified type reads the type the checker already recorded for the expression.
            case MemberAccess memberAccess:
                return _semanticInfo.GetExpressionType(memberAccess) as UserDefinedType;

            // `Box[int]` / `dict[str, int]` — the base name plus an argument list, which is a single
            // element or a TupleLiteral of them.
            case IndexAccess { Object: Identifier baseName } indexAccess:
                {
                    if (_symbolTable.Lookup(baseName.Name) is not TypeSymbol { IsGeneric: true } genericDefinition)
                        return null;

                    var argumentNodes = UnwrapParenthesized(indexAccess.Index) is TupleLiteral argumentTuple
                        ? (IReadOnlyList<Expression>)argumentTuple.Elements
                        : new[] { indexAccess.Index };
                    if (argumentNodes.Count != genericDefinition.TypeParameters.Count)
                        return null;

                    var typeArguments = new List<SemanticType>(argumentNodes.Count);
                    foreach (var argumentNode in argumentNodes)
                    {
                        if (ResolveTypeTestOperandType(argumentNode) is not { } argument)
                            return null;
                        typeArguments.Add(argument);
                    }

                    return new GenericType
                    {
                        Name = genericDefinition.Name,
                        TypeArguments = typeArguments,
                        GenericDefinition = genericDefinition
                    };
                }

            default:
                return null;
        }
    }

    /// <summary>
    /// Maps a builtin primitive type NAME to its singleton, or null when the name is not one.
    /// Shared by every arm of the classifier so <c>int</c> means the same thing bare, module-qualified,
    /// and as a type argument.
    /// </summary>
    private static SemanticType? ResolveBuiltinPrimitiveTypeName(string name) => name switch
    {
        BuiltinNames.Int => SemanticType.Int,
        BuiltinNames.Long => SemanticType.Long,
        BuiltinNames.Float => SemanticType.Float,
        BuiltinNames.Float32 => SemanticType.Float32,
        BuiltinNames.Decimal => SemanticType.Decimal,
        BuiltinNames.Double => SemanticType.Double,
        BuiltinNames.Bool => SemanticType.Bool,
        BuiltinNames.Str => SemanticType.Str,
        _ => null
    };

    /// <summary>
    /// True when <paramref name="call"/> is the test expression of an <c>assert</c> inside a
    /// <c>@test</c>-decorated function (directly, parenthesized, or under <c>not</c>) — the statement
    /// the emitter rewrites into an xUnit assertion instead of lowering as an expression.
    /// </summary>
    private bool IsTestAssertTypeTest(FunctionCall call)
    {
        if (_testAssertTest == null)
            return false;

        var test = UnwrapParenthesized(_testAssertTest);
        if (test is UnaryOp { Operator: UnaryOperator.Not } negated)
            test = UnwrapParenthesized(negated.Operand);

        return ReferenceEquals(test, call);
    }

    /// <summary>
    /// Best-effort textual rendering of a type-position expression for the multi-type diagnostic.
    /// Falls back to a placeholder when the expression is not a simple name.
    /// </summary>
    private static string DescribeTypeOperand(Expression expr) => expr switch
    {
        Identifier id => id.Name,
        MemberAccess ma => $"{DescribeTypeOperand(ma.Object)}.{ma.Member}",
        IndexAccess ia => $"{DescribeTypeOperand(ia.Object)}[...]",
        _ => "<type>"
    };

    /// <summary>
    /// Checks <c>tuple[int, str](t)</c> — an explicitly spelled tuple type applied to a tuple (#1200).
    /// This is a CONVERSION, not a construction: a tuple's arity is part of its type, so there is
    /// nothing to build from separate arguments and the result is simply the written
    /// <see cref="TupleType"/>. Codegen emits the argument itself (identity), which is why the
    /// argument must already be assignable to that type.
    /// </summary>
    /// <remarks>
    /// The bare <c>tuple(iterable)</c> form is a different question — the arity is missing, not the
    /// element types — and keeps its SPY0338
    /// (<see cref="ReportUnsupportedTupleFromIterable"/>).
    /// </remarks>
    private SemanticType CheckParameterizedTupleConversion(
        FunctionCall call, TupleType targetType, List<SemanticType> argTypes)
    {
        if (call.Arguments.Length != 1 || call.KeywordArguments.Length != 0)
        {
            AddError(
                $"'{targetType.GetDisplayName()}(...)' expects exactly 1 argument (the tuple to "
                    + $"convert) but got {call.Arguments.Length + call.KeywordArguments.Length}",
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: call.Span);
            return targetType;
        }

        var argType = argTypes.Count > 0 ? argTypes[0] : SemanticType.Unknown;
        if (argType is not UnknownType && !IsAssignable(argType, targetType))
        {
            AddError(
                $"Cannot convert argument of type '{argType.GetDisplayName()}' to "
                    + $"'{targetType.GetDisplayName()}'",
                call.Arguments[0].LineStart, call.Arguments[0].ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: call.Arguments[0].Span);
        }

        return targetType;
    }

    /// <summary>
    /// Handles generic type instantiation (Box[int](42)) and generic function calls (identity[int](42)).
    /// Returns null if the call is not a generic instantiation.
    /// </summary>
    /// <param name="callee">The canonical (paren-stripped) callee — see the #1170 contract in
    /// <see cref="AstHelper.UnwrapParenthesized"/>. <c>(Box[int])(42)</c> instantiates like
    /// <c>Box[int](42)</c>.</param>
    private SemanticType? CheckGenericInstantiation(FunctionCall call, Expression callee, SemanticType calleeType,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes, int totalArgCount)
    {
        // Special handling for array construction: array[T](size) -> new T[size]
        if (callee is IndexAccess arrayAccess &&
            arrayAccess.Object is Identifier arrayId &&
            arrayId.Name == BuiltinNames.Array)
        {
            var arrayTypeArgs = TryResolveTypeArguments(arrayAccess.Index);
            if (arrayTypeArgs != null && arrayTypeArgs.Count == 1)
            {
                if (call.Arguments.Length != 1)
                {
                    AddError("Array constructor requires exactly 1 argument (the size)",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                    return SemanticType.Unknown;
                }

                if (argTypes.Count > 0 && argTypes[0] != SemanticType.Unknown &&
                    argTypes[0] != SemanticType.Int && argTypes[0] != SemanticType.Long)
                {
                    AddError("Array size must be an integer",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Span);
                }

                return new GenericType
                {
                    Name = BuiltinNames.Array,
                    TypeArguments = arrayTypeArgs
                };
            }
        }

        // tuple[int, str]((1, "a")): converting a tuple to an explicitly spelled tuple type. Handled
        // before the generic arm because a tuple is not constructed from arguments — its arity is
        // part of its type, so the call is an identity conversion whose result is the written
        // TupleType (#1200). The resolver already typed the callee as that TupleType.
        if (callee is IndexAccess tupleAccess
            && tupleAccess.Object is Identifier { Name: BuiltinNames.Tuple }
            && _semanticInfo.GetExpressionType(tupleAccess) is TupleType targetTupleType)
        {
            return CheckParameterizedTupleConversion(call, targetTupleType, argTypes);
        }

        // Special handling for generic type instantiation: Box[int](42) or Pair[int, str](1, "a")
        // This is parsed as FunctionCall(Function: IndexAccess(Object: Box, Index: int or TupleLiteral), Arguments: [...])
        // The object may be a bare identifier (Box) or a module-qualified member access
        // (difflib.SequenceMatcher), both of which can resolve to a generic TypeSymbol.
        if (callee is IndexAccess indexAccess &&
            TryResolveGenericTypeSymbolFromIndexObject(indexAccess.Object) is { IsGeneric: true } genericTypeSymbol)
        {
            // The "index" is actually type argument(s). The generic-reference resolver already
            // normalized this callee (CheckCall ran CheckExpression on it above) and owns the
            // vector: it is the written one with any PEP-696 type-parameter defaults filled in, so
            // `Pair[int]` where `Pair[K, V = str]` constructs a Pair[int, str] exactly as the
            // annotation position resolves it (#1192).
            var typeArgs = _semanticInfo.GetGenericReference(indexAccess) is
            {
                Kind: GenericReferenceKind.GenericTypeRef
                        or GenericReferenceKind.ModuleType
                        or GenericReferenceKind.NestedTypeRef
            } typeReference
                ? typeReference.TypeArgs.ToList()
                : TryResolveTypeArguments(indexAccess.Index);

            // No fact and a vector that does not fit the declaration means the resolver REJECTED
            // this reference and already reported the arity (#1192). Constructing from a vector it
            // refused would stack a derived diagnostic on the same line, so fall through to the
            // caller's Unknown/error-recovery tail.
            if (typeArgs != null && typeArgs.Count != genericTypeSymbol.TypeParameters.Count)
                return SemanticType.Unknown;

            if (typeArgs != null)
            {
                // Validate constructor arguments against __init__ parameters (skip 'self').
                // Only validate when there's a single __init__ (no overloads).
                var initMethods = genericTypeSymbol.Methods.Where(m => m.Name == DunderNames.Init).ToList();
                if (initMethods.Count == 1)
                {
                    var initParams = initMethods[0].Parameters.Skip(1).ToList();

                    // SPY0357: Check for iterable spread into non-variadic generic constructor
                    if (CheckSpreadIntoNonVariadic(call, genericTypeSymbol.Name, initParams))
                        return new GenericType
                        {
                            Name = genericTypeSymbol.Name,
                            TypeArguments = typeArgs,
                            GenericDefinition = genericTypeSymbol
                        };

                    // The written type arguments have already decided the substitution, so the
                    // arguments are checked against the SUBSTITUTED __init__ — `key: K` bound to
                    // `int` by `Slot[int, str]`. Nothing compared them before: the explicit
                    // spelling arrived with the substitution settled and went straight to emission,
                    // where the mismatch surfaced as CS1503 behind SPY0908 naming C# types and
                    // argument positions rather than the user's own binding (#1243).
                    ValidateCallArguments(call, initParams, argTypes, kwargTypes, totalArgCount,
                        WrittenTypeParameterBinding(genericTypeSymbol, typeArgs));
                }
                else if (initMethods.Count > 1)
                {
                    var initParams = initMethods[0].Parameters.Skip(1).ToList();
                    if (CheckSpreadIntoNonVariadic(call, genericTypeSymbol.Name, initParams))
                        return new GenericType
                        {
                            Name = genericTypeSymbol.Name,
                            TypeArguments = typeArgs,
                            GenericDefinition = genericTypeSymbol
                        };

                    ValidateSoleArityMatchingOverload(call, initMethods, argTypes, kwargTypes,
                        totalArgCount, WrittenTypeParameterBinding(genericTypeSymbol, typeArgs));
                }

                // A type with no construction cannot be constructed — same authority as the
                // non-generic path in CheckConstructorCall (#1271).
                if (CannotInstantiateMessageOf(genericTypeSymbol) is { } cannotInstantiate)
                {
                    AddError(cannotInstantiate,
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

        // lst.select[str](f), and since #1206 lst.select(f) too — an extension method whose closed
        // signature semantic analysis materialized (#1195). No FunctionSymbol exists for this kind, so
        // nothing below can bind it: the closed CLR signature IS the contract. The call's type is the
        // closed return type (which is what lets `list(lst.select(f))` know what it wraps), and its
        // value arguments are checked against the closed parameter types (the #1148 contract for this
        // kind). Both callee shapes are accepted here because both spellings record the same fact,
        // differing only in the node it is keyed on: the written `[...]` for the explicit spelling, the
        // member access itself for the staged one.
        if (IsClosedExtensionCallee(callee)
            && _semanticInfo.GetGenericReference(callee) is
            {
                Kind: GenericReferenceKind.BclExtensionMethod,
                ClosedReturnType: { } closedReturnType
            } extensionReference)
        {
            ValidateClosedExtensionArguments(call, callee, extensionReference, argTypes);
            return closedReturnType;
        }

        // Handle generic function call: identity[int](42)
        // The calleeType will be GenericFunctionType from CheckIndexAccess
        if (calleeType is GenericFunctionType genericFuncType)
        {
            // Record the resolved call target for codegen
            _semanticInfo.SetCallTarget(call, genericFuncType.FunctionSymbol);

            // #1148: the explicit type arguments have already narrowed the overload set —
            // map[int, int, int] leaves only Map<T1, T2, TOut>, which takes a two-argument function
            // and TWO iterables. Nothing validated the value arguments against that narrowing, so a
            // call whose type-argument count matched but whose value arguments did not was accepted
            // here and emitted verbatim, surfacing as CS7036 out of Roslyn instead of a diagnostic.
            ValidateSelectedGenericOverloadArguments(call, callee, genericFuncType, argTypes, totalArgCount);

            // Substitute type parameters with type arguments in the return type
            var substitutedReturnType = SubstituteTypeParameters(
                genericFuncType.FunctionSymbol.ReturnType,
                genericFuncType.FunctionSymbol.TypeParameters,
                genericFuncType.TypeArguments);
            return substitutedReturnType;
        }

        return null;
    }

    /// <summary>
    /// The closed signature of an extension-method callee, shaped as a <see cref="FunctionType"/> so
    /// its value arguments receive expected types through the ordinary argument-checking seam: the
    /// lambda in <c>lst.select[str](f)</c> is checked against <c>Func[int, str]</c> instead of being
    /// inferred blind and only failing at the C# layer (#1195). Null for every other callee.
    /// </summary>
    private FunctionType? ClosedExtensionSignature(Expression callee)
        => IsClosedExtensionCallee(callee)
           && _semanticInfo.GetGenericReference(callee) is
           {
               Kind: GenericReferenceKind.BclExtensionMethod,
               ClosedReturnType: { } returnType,
               ClosedParameterTypes: { } parameterTypes,
           }
            ? new FunctionType { ParameterTypes = parameterTypes.ToList(), ReturnType = returnType }
            : null;

    /// <summary>
    /// The two callee shapes a <see cref="GenericReferenceKind.BclExtensionMethod"/> fact can be keyed
    /// on: the <c>IndexAccess</c> of the explicit spelling <c>lst.select[str](f)</c> (#1195) and the
    /// <c>MemberAccess</c> of the staged no-type-args spelling <c>lst.select(f)</c> (#1206).
    ///
    /// <para>
    /// A shape test rather than a bare lookup because <see cref="SemanticInfo.GetGenericReference"/> is
    /// keyed on <c>Expression</c> and every other reference kind is recorded on an <c>IndexAccess</c>;
    /// naming the two admissible shapes keeps a future kind from silently acquiring an extension
    /// call's contract by being passed to one of these readers.
    /// </para>
    /// </summary>
    private static bool IsClosedExtensionCallee(Expression callee)
        => callee is IndexAccess or MemberAccess;

    /// <summary>
    /// Validates the value arguments of an extension call against its CLOSED signature (#1195) — the
    /// <see cref="GenericReferenceKind.BclExtensionMethod"/> counterpart of
    /// <see cref="ValidateSelectedGenericOverloadArguments"/>, which cannot serve this kind because
    /// no <see cref="FunctionSymbol"/> exists for it.
    ///
    /// <para>Scope is deliberately narrow. ARITY is left to C#: the acceptance surface ships
    /// same-name overloads whose value parameters differ (<c>Select</c>'s plain and index-taking
    /// selectors close to the same type-argument vector, which the resolver treats as one
    /// resolution), so a count that does not match THIS closed candidate need not be wrong. Only an
    /// argument that is definitely of the wrong type is reported, and an argument whose own type is
    /// unknown — a bare lambda with no expected type — is skipped rather than guessed at.</para>
    /// </summary>
    private void ValidateClosedExtensionArguments(
        FunctionCall call, Expression callee, GenericReference reference, List<SemanticType> argTypes)
    {
        if (reference.ClosedParameterTypes is not { } expectedTypes
            || call.KeywordArguments.Length > 0
            || argTypes.Count != expectedTypes.Count)
        {
            return;
        }

        // The member name sits one level in for the explicit spelling (`lst.select[str]` — the callee is
        // the IndexAccess) and IS the callee for the staged one (`lst.select`).
        var writtenName = callee switch
        {
            IndexAccess explicitCallee => (explicitCallee.Object as MemberAccess)?.Member,
            MemberAccess stagedCallee => stagedCallee.Member,
            _ => null
        };
        var memberName = writtenName ?? reference.ClrMemberName ?? "extension method";

        for (int i = 0; i < argTypes.Count; i++)
        {
            if (argTypes[i] is UnknownType || expectedTypes[i] is UnknownType
                || argTypes[i] is FunctionType { } argFn && argFn.HasUnresolvedTypes()
                || expectedTypes[i] is FunctionType { } expectedFn && expectedFn.HasUnresolvedTypes())
            {
                continue;
            }

            if (IsAssignable(argTypes[i], expectedTypes[i]))
                continue;

            // Quote what the user actually wrote: `select[...]` only when they wrote type arguments.
            var written = callee is IndexAccess ? $"{memberName}[...]" : memberName;
            AddError(
                $"Argument {i + 1} of '{written}' expects '{expectedTypes[i].GetDisplayName()}' "
                + $"but got '{argTypes[i].GetDisplayName()}'",
                call.Arguments[i].LineStart,
                call.Arguments[i].ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: call.Arguments[i].Span);
        }
    }

    /// <summary>
    /// Validates a generic call's VALUE arguments against the overloads its explicit type arguments
    /// selected, with those type arguments substituted into the parameter types (#1148). Driven by
    /// the <see cref="GenericReference"/> fact, so it covers every callee kind rather than
    /// special-casing any one builtin.
    ///
    /// <para>The type-argument count narrows the overload set but does NOT always pin a single
    /// overload: <c>itertools.islice[int]</c> and <c>itertools.repeat[int]</c> each have several
    /// same-generic-arity overloads differing only in their value parameters. (<c>frozen_set[int]</c>
    /// was a third example until #1210 retired that spelling — <c>frozenset</c> is a registered
    /// collection type now, so <c>frozenset[int]()</c> is a construction, not a generic call.) The
    /// call is therefore checked against all of them through the shared
    /// <see cref="ResolveOverloadCore"/>, and only a call that NO candidate accepts is reported —
    /// via <see cref="ReportOverloadError"/>, the same no-match reporting the non-generic overload
    /// paths use (#1013).</para>
    /// </summary>
    private void ValidateSelectedGenericOverloadArguments(
        FunctionCall call, Expression canonicalCallee, GenericFunctionType genericFuncType,
        List<SemanticType> argTypes, int totalArgCount)
    {
        var callee = canonicalCallee as IndexAccess;
        var reference = callee != null ? _semanticInfo.GetGenericReference(callee) : null;
        var selected = reference?.SelectedOverload ?? genericFuncType.FunctionSymbol;

        // Look overloads up by the SOURCE name — the registries are keyed by what was written
        // (`islice`, `map`), which a discovered symbol's own Name need not match.
        var calleeName = callee?.Object switch
        {
            MemberAccess memberAccess => memberAccess.Member,
            Identifier identifier => identifier.Name,
            _ => selected.Name
        };

        var candidates = CollectGenericOverloadCandidates(
            reference, selected, calleeName, genericFuncType.TypeArguments.Count);

        var (match, arityCandidates, isAmbiguous) = ResolveOverloadCore(new OverloadResolutionContext(
            Candidates: candidates,
            TotalArgCount: totalArgCount,
            ArgTypes: argTypes,
            SkipSelfParam: true,
            TypeSubstitution: t => SubstituteTypeParameters(
                t, selected.TypeParameters, genericFuncType.TypeArguments),
            SkipUnknownTypes: true,
            KeywordArgNames: ExtractKeywordArgNames(call),
            Call: call));

        // Ambiguity among same-generic-arity candidates is not this check's business: the explicit
        // type arguments already pinned the overload codegen emits. Only "nothing accepts these
        // value arguments" is the #1148 defect.
        if (match != null || isAmbiguous)
            return;

        ReportOverloadError(calleeName, call, isAmbiguous: false, arityCandidates, totalArgCount);
    }

    /// <summary>
    /// Collects the overloads a generic reference could be calling: every overload of the same
    /// callee whose type-parameter count matches the supplied type arguments, plus the selected
    /// overload itself. Kinds whose overload set is not enumerable here (BCL methods, which the
    /// #1136 path resolves by reflection) fall back to the selected overload alone.
    /// </summary>
    private List<FunctionSymbol> CollectGenericOverloadCandidates(
        GenericReference? reference, FunctionSymbol selected, string calleeName, int typeArgCount)
    {
        var overloads = reference?.Kind switch
        {
            GenericReferenceKind.Builtin => _symbolTable.BuiltinRegistry.GetFunctionOverloads(calleeName),
            GenericReferenceKind.UserFunction => _symbolTable.LookupFunctionOverloads(calleeName),
            GenericReferenceKind.ModuleFunction => LookupModuleFunctionOverloads(reference.ReceiverType, calleeName),
            GenericReferenceKind.InstanceMethod => LookupInstanceMethodOverloads(reference.ReceiverType, calleeName),
            _ => null,
        };

        var candidates = new List<FunctionSymbol> { selected };
        if (overloads != null)
        {
            foreach (var overload in overloads)
            {
                if (overload.TypeParameters.Count == typeArgCount && !ReferenceEquals(overload, selected))
                    candidates.Add(overload);
            }
        }

        return candidates;
    }

    /// <summary>
    /// The overload set an imported module exports under <paramref name="memberName"/>, trying the
    /// PascalCase spelling as well for .NET modules (mirroring the member lookup in the resolver).
    /// </summary>
    private static List<FunctionSymbol>? LookupModuleFunctionOverloads(SemanticType? receiverType, string memberName)
    {
        if (receiverType is not ModuleType moduleType)
            return null;
        if (moduleType.Symbol.FunctionOverloads.TryGetValue(memberName, out var overloads))
            return overloads;
        return moduleType.Symbol.FunctionOverloads.TryGetValue(NameMangler.ToPascalCase(memberName), out var pascal)
            ? pascal
            : null;
    }

    /// <summary>
    /// The overload set a receiver's type declares under <paramref name="memberName"/>.
    /// </summary>
    private List<FunctionSymbol>? LookupInstanceMethodOverloads(SemanticType? receiverType, string memberName)
    {
        if (receiverType == null)
            return null;

        var ownerSymbol = receiverType switch
        {
            UserDefinedType { Symbol: { } udtSymbol } => udtSymbol,
            GenericType { GenericDefinition: { } genericDefinition } => genericDefinition,
            _ => ResolveBuiltinTypeInfo(receiverType).TypeSymbol
        };

        return ownerSymbol != null && ownerSymbol.MethodOverloads.TryGetValue(memberName, out var overloads)
            ? overloads
            : null;
    }

    /// <summary>
    /// Recursively checks whether <paramref name="arg"/> can satisfy an expected parameter
    /// <paramref name="expected"/> that may contain open generic type parameters. A bare type
    /// parameter is a wildcard at its own position; a same-name/same-arity generic is matched
    /// recursively (so a flat <c>list[int]</c> does NOT satisfy a nested <c>list[list[T]]</c>);
    /// any other case compares against the expected shape with every remaining type parameter
    /// treated as <c>object</c> (rejecting structurally incompatible arguments such as
    /// <c>float</c> vs <c>list[T]</c>, while still accepting non-generic arguments genuinely
    /// assignable to the open shape). Mirrors the structural half of C#'s overload
    /// applicability for open generic parameters (#954, #957).
    /// </summary>
    private bool ArgMatchesGenericShape(SemanticType arg, SemanticType expected)
    {
        if (expected is TypeParameterType)
            return true;

        if (!ContainsTypeParameter(expected))
            return IsAssignable(arg, expected);

        if (expected is GenericType eg)
        {
            // Same outer generic: recurse so a flat list[int] does NOT satisfy list[list[T]] (#957).
            if (arg is GenericType ag && string.Equals(eg.Name, ag.Name, StringComparison.Ordinal))
            {
                if (ag.TypeArguments.Count != eg.TypeArguments.Count)
                    return false;
                for (int i = 0; i < eg.TypeArguments.Count; i++)
                {
                    if (!ArgMatchesGenericShape(ag.TypeArguments[i], eg.TypeArguments[i]))
                        return false;
                }

                return true;
            }

            // Different outer generic name: reject (preserves list[int] ↛ array[T], #954).
            if (arg is GenericType)
                return false;

            // Non-generic argument against an open generic shape: accept only if genuinely
            // assignable with type parameters treated as object — rejects float vs list[T]
            // while still allowing a subtype (e.g. MyList vs list[T]).
            return IsAssignable(arg, SubstituteTypeParametersWithObject(expected));
        }

        // NullableType<T>, OptionalType<T>, TupleType<T,...>: substitute type parameters
        // with object and check assignability — rejects structurally incompatible args
        // (e.g., list[int] ↛ T?) while still accepting compatible ones (#966).
        if (expected is NullableType or OptionalType or TupleType)
            return IsAssignable(arg, SubstituteTypeParametersWithObject(expected));

        // FunctionType, GenericFunctionType, and other opaque shapes: preserve permissive
        // behavior — real checking happens during generic type inference.
        return true;
    }

    /// <summary>
    /// Returns a copy of <paramref name="type"/> with every <see cref="TypeParameterType"/>
    /// replaced by <see cref="SemanticType.Object"/> — the most permissive binding — so an
    /// open generic shape can be compared via ordinary assignability.
    /// </summary>
    private static SemanticType SubstituteTypeParametersWithObject(SemanticType type)
    {
        switch (type)
        {
            case TypeParameterType:
                return SemanticType.Object;
            case GenericType g:
                return new GenericType
                {
                    Name = g.Name,
                    GenericDefinition = g.GenericDefinition,
                    TypeArguments = g.TypeArguments.Select(SubstituteTypeParametersWithObject).ToList()
                };
            case NullableType n:
                return new NullableType { UnderlyingType = SubstituteTypeParametersWithObject(n.UnderlyingType) };
            case OptionalType o:
                return new OptionalType { UnderlyingType = SubstituteTypeParametersWithObject(o.UnderlyingType) };
            case TupleType t:
                return new TupleType
                {
                    ElementTypes = t.ElementTypes.Select(SubstituteTypeParametersWithObject).ToList(),
                    ElementNames = t.ElementNames
                };
            default:
                return type;
        }
    }

    /// <summary>
    /// Structural "more specific" comparison per C# §12.6.4.4: a type parameter is less
    /// specific than any concrete or structured type at the same position; same-name/same-arity
    /// generics recurse position-wise (more specific if strictly better in some position and
    /// not worse in any). Used only as a tiebreaker when assignability gives no preference.
    /// </summary>
    private bool IsMoreSpecificType(SemanticType a, SemanticType b)
    {
        if (a.Equals(b))
            return false;

        var aIsParam = a is TypeParameterType;
        var bIsParam = b is TypeParameterType;
        if (bIsParam && !aIsParam)
            return true;
        if (aIsParam && !bIsParam)
            return false;

        if (a is GenericType ga && b is GenericType gb
            && string.Equals(ga.Name, gb.Name, StringComparison.Ordinal)
            && ga.TypeArguments.Count == gb.TypeArguments.Count)
        {
            var anyStrictlyBetter = false;
            for (int i = 0; i < ga.TypeArguments.Count; i++)
            {
                if (IsMoreSpecificType(gb.TypeArguments[i], ga.TypeArguments[i]))
                    return false;
                if (IsMoreSpecificType(ga.TypeArguments[i], gb.TypeArguments[i]))
                    anyStrictlyBetter = true;
            }

            return anyStrictlyBetter;
        }

        return false;
    }

    /// <summary>
    /// Returns the effective CLR type for a parameter, with generic type parameters
    /// replaced by <c>typeof(object)</c> so <see cref="Type.IsAssignableFrom"/> works.
    /// Prefers the original CLR metadata from <see cref="FunctionSymbol.ClrMethod"/>
    /// (preserving IEnumerable vs List distinction that <see cref="Discovery.ClrTypeBridge"/>
    /// erases), falling back to <see cref="TryGetClrType"/> for source-defined overloads.
    /// </summary>
    internal Type? ResolveClrParameterType(FunctionSymbol func, int paramIdx, SemanticType semanticType)
    {
        if (func.ClrMethod != null)
        {
            var clrParams = func.ClrMethod.GetParameters();
            if (paramIdx < clrParams.Length)
                return SubstituteGenericParameters(clrParams[paramIdx].ParameterType);
        }

        if (paramIdx < func.Parameters.Count)
        {
            var clrTypeName = func.Parameters[paramIdx].ClrTypeName;
            if (!string.IsNullOrEmpty(clrTypeName))
            {
                var clrType = Type.GetType(clrTypeName);
                if (clrType == null)
                {
                    // Assembly.GetType expects just the namespace-qualified name,
                    // not the full AQN — strip the assembly qualifier.
                    var commaIdx = clrTypeName!.IndexOf(',', StringComparison.Ordinal);
                    var typeNameOnly = commaIdx >= 0 ? clrTypeName[..commaIdx] : clrTypeName;
                    clrType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType(typeNameOnly))
                        .FirstOrDefault(t => t != null);
                }
                if (clrType != null)
                {
                    if (clrType.IsGenericTypeDefinition)
                        return clrType.MakeGenericType(
                            Enumerable.Repeat(typeof(object), clrType.GetGenericArguments().Length).ToArray());
                    return clrType;
                }
            }
        }

        return TryGetClrType(semanticType);
    }

    internal static Type SubstituteGenericParameters(Type type)
    {
        if (type.IsGenericParameter)
        {
            if ((type.GenericParameterAttributes &
                 System.Reflection.GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
                return typeof(int);
            return typeof(object);
        }
        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            var resolved = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                resolved[i] = SubstituteGenericParameters(args[i]);
            return type.GetGenericTypeDefinition().MakeGenericType(resolved);
        }
        if (type.IsArray)
            return SubstituteGenericParameters(type.GetElementType()!).MakeArrayType();
        return type;
    }

    /// <summary>
    /// Finds the single candidate whose parameter types are more specific than all other
    /// candidates (the "best" function member).  Returns <see langword="null"/> if no single
    /// candidate dominates or if the list has fewer than two entries.
    /// </summary>
    private FunctionSymbol? FindMostSpecificOverload(List<FunctionSymbol> candidates, OverloadResolutionContext context)
    {
        if (candidates.Count < 2)
            return null;

        FunctionSymbol? best = null;
        foreach (var candidate in candidates)
        {
            bool beatsAll = true;
            foreach (var other in candidates)
            {
                if (ReferenceEquals(candidate, other))
                    continue;
                if (!IsMoreSpecificOverload(candidate, other, context))
                {
                    beatsAll = false;
                    break;
                }
            }
            if (beatsAll)
            {
                if (best != null)
                    return null; // Two candidates both beat all others — still ambiguous.
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Extracts keyword argument names from a function call for use in overload filtering.
    /// Returns null when there are no keyword arguments (avoids allocating an empty collection).
    /// </summary>
    private static IReadOnlyCollection<string>? ExtractKeywordArgNames(FunctionCall call)
    {
        if (call.KeywordArguments.Length == 0)
            return null;
        return call.KeywordArguments.Select(kw => kw.Name).ToList();
    }

    /// <summary>
    /// Validates the keyword arguments of the variadic value form of <c>min()</c>/<c>max()</c>
    /// (<c>min(a, b, …)</c> with ≥2 positional args).
    /// <see cref="BuiltinReturnTypeInference"/> returns the value type before overload
    /// resolution runs, so these kwargs are otherwise never type-checked. Codegen lowers the
    /// <c>key=</c> form to <c>Min(new[]{…}, key)</c> (#1012). Two things are checked so an
    /// invalid call yields a Sharpy diagnostic instead of a leaked C# error: the value form
    /// accepts only <c>key=</c> (any other kwarg — e.g. <c>default=</c>, which Python permits
    /// only for the iterable form — would otherwise emit CS1744), and the <c>key</c> must be
    /// callable (a non-callable key would silently bind to the
    /// <c>(IEnumerable&lt;T&gt;, T default)</c> overload). The extra-kwarg case is rejected with
    /// SPY0234 and the non-callable key with SPY0230.
    /// </summary>
    private void ValidateMinMaxValueFormKey(
        Identifier id, FunctionCall call,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes)
    {
        if (id.Name is not (BuiltinNames.Min or BuiltinNames.Max))
            return;
        // Only the value form (>= 2 positional args) is handled here; the single-positional
        // iterable form (incl. its default=/key= kwargs) is handled by ordinary overload
        // resolution against the iterable overloads.
        if (argTypes.Count < 2)
            return;

        // The value form accepts only key=. Any other kwarg bypasses overload resolution
        // (InferReturnType returned early) and would leak a raw C# error from codegen
        // (e.g. default= -> CS1744). Reject it with a Sharpy diagnostic — matching Python,
        // which raises TypeError for default= alongside multiple positional args. (#1012)
        foreach (var kw in call.KeywordArguments)
        {
            if (kw.Name != "key")
                AddError(
                    $"'{id.Name}' value form does not accept keyword argument '{kw.Name}'",
                    kw.LineStart, kw.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                    span: kw.Span);
        }

        if (!kwargTypes.TryGetValue("key", out var keyType))
            return;

        // Callable: a function/delegate type, or (UnknownType) an already-reported error.
        if (keyType is FunctionType or UnknownType
            || TryGetDelegateInvokeMethod(keyType) != null)
            return;

        var keyArg = call.KeywordArguments.FirstOrDefault(k => k.Name == "key");
        // A bare function reference (user function or builtin) is callable even when its
        // value-position expression type is not surfaced as a FunctionType.
        if (keyArg?.Value is Identifier keyId
            && (_symbolTable.Lookup(keyId.Name) is FunctionSymbol
                || _symbolTable.BuiltinRegistry.GetFunctionOverloads(keyId.Name) != null))
            return;

        AddError(
            $"'{id.Name}' key must be callable; got '{keyType.GetDisplayName()}'",
            keyArg?.LineStart, keyArg?.ColumnStart,
            code: DiagnosticCodes.Semantic.NotCallable,
            span: keyArg?.Span);
    }

    /// <summary>
    /// Resolves builtin function overloads for a call. Returns the resolved return type,
    /// or null if no overload resolution is needed.
    /// </summary>
    private SemanticType? ResolveBuiltinOverload(
        Identifier id, List<SemanticType> argTypes, int totalArgCount, FunctionCall call)
    {
        // When there are multiple overloads, we need to perform overload resolution to find the right one.
        // The funcSymbol from Lookup is just the first overload, which may not match the call.
        // Only use builtin overloads if there's no user-defined function shadowing the builtin.
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
        var isBuiltinWithOverloads = overloads != null && overloads.Count > 1;
        var funcSymbol = _symbolTable.Lookup(id.Name) as FunctionSymbol;
        // If funcSymbol was found in symbol table AND it's one of the builtin overloads, use overload resolution
        var needsOverloadResolution = isBuiltinWithOverloads &&
            (funcSymbol == null || overloads!.Contains(funcSymbol));
        if (!needsOverloadResolution)
            return null;

        var kwNames = ExtractKeywordArgNames(call);
        // Builtin overloads resolve through the deterministic betterness chain (exact arity → fewer
        // type parameters → most specific), not registration-order first-match, so resolution is
        // order-independent (#1043). BuiltinRegistry registration order is no longer load-bearing.
        var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads!, totalArgCount, argTypes,
                KeywordArgNames: kwNames, Call: call));

        // Order-independence recovery (#1043): the deterministic chain reports ambiguity where the
        // old first-match silently picked one. When the arity-matching overloads all yield the SAME
        // return type (e.g. int's 13 overloads all return int), the *observable* result — the return
        // type — is unambiguous even if no single overload is "best"; pick a representative so
        // resolution stays order-independent instead of failing. This also recovers gracefully when
        // an argument is still UnknownType (a cascade from an un-inferred lambda parameter), where
        // the old path returned the first overload's (common) return type.
        if (matchingOverload == null && arityCandidates.Count > 0
            && (isAmbiguous || argTypes.Any(a => a is UnknownType)))
        {
            var typeMatches = arityCandidates
                .Where(o => o.ReturnType is not null)
                .ToList();
            if (typeMatches.Count > 0
                && typeMatches.All(o => o.ReturnType.Equals(typeMatches[0].ReturnType)))
            {
                matchingOverload = typeMatches[0];
            }
        }

        if (matchingOverload != null)
        {
            // Update the identifier symbol to point to the matching overload
            _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
            // Record the resolved call target for codegen
            _semanticInfo.SetCallTarget(call, matchingOverload);
            return matchingOverload.ReturnType;
        }

        // isinstance must always type to bool even when no overload matched. No overload accepts a
        // tuple or a bare generic name, and the diagnostic for those shapes belongs to the type-operand
        // classifier (SPY0344/SPY0345, #1207/#1213) — a premature SPY0224 here would report the
        // symptom instead.
        if (id.Name == BuiltinNames.Isinstance)
            return SemanticType.Bool;

        // No matching overload found. Distinguish the two failure modes (#1010):
        //   - the argument COUNT matches some overload but its parameter TYPES don't
        //     (arityCandidates non-empty) -> report a type/overload mismatch. Reporting the
        //     arity here would be self-contradictory (e.g. "expects 1 or 2 or 3 arguments but
        //     got 2" when 2 IS in range), which is the misleading message #1010 called out.
        //   - the argument count matches no overload at all -> report the arity mismatch with
        //     the helpful expected-count list.
        if (arityCandidates.Count > 0)
        {
            var typeList = string.Join(", ", argTypes.Select(t => t.GetDisplayName()));
            AddError($"No overload of '{id.Name}' matches the argument types ({typeList})",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                span: call.Span);
            return SemanticType.Unknown;
        }

        var expectedCounts = string.Join(" or ", overloads!.Select(o =>
        {
            var required = o.Parameters.Count(p => !p.HasDefault && !p.IsVariadic);
            var total = o.Parameters.Count;
            var hasVariadic = o.Parameters.Any(p => p.IsVariadic);
            if (hasVariadic)
                return $"{required}+";
            return required == total ? total.ToString(CultureInfo.InvariantCulture) : $"{required}-{total}";
        }).Distinct());
        AddError($"Function '{id.Name}' expects {expectedCounts} arguments but got {totalArgCount}",
            call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
            span: call.Span);
        return SemanticType.Unknown;
    }

    // TODO(#205): Add language spec for method overloading (docs/language_specification/method_overloading.md)
    // TODO(#207): Add test fixtures for ambiguous overloads and overloads with default parameters
    /// <summary>
    /// Resolves a method overload from a member access call (e.g., obj.method(args)).
    /// Handles both user-defined types and built-in generic types (dict, list, set).
    /// Returns the resolved return type when the method has multiple overloads, null if not applicable.
    /// </summary>
    private SemanticType? ResolveUserMethodOverload(
        MemberAccess memberAccess, List<SemanticType> argTypes, int totalArgCount, FunctionCall call,
        bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        var rawObjectType = _semanticInfo.GetExpressionType(memberAccess.Object);
        if (rawObjectType == null)
            return null;
        var objectType = UnwrapCallTarget(rawObjectType);

        TypeSymbol? typeSymbol = null;
        List<SemanticType>? typeArgs = null;

        if (objectType is UserDefinedType { Symbol: { } udt })
        {
            typeSymbol = udt;
        }
        else
        {
            var (resolved, resolvedTypeArgs) = ResolveBuiltinTypeInfo(objectType);
            typeSymbol = resolved;
            typeArgs = resolvedTypeArgs;
        }

        if (typeSymbol == null)
            return null;

        // Walk the hierarchy looking for overloads
        var overloads = FindMethodOverloadsInHierarchy(typeSymbol, memberAccess.Member);
        if (overloads == null || overloads.Count <= 1)
            return null;

        // SPY0357: Check for iterable spread into non-variadic overloaded method.
        // Must run before argument count filtering, since spread collapses N args into 1.
        var anyOverloadVariadic = overloads.Any(o => o.Parameters.Any(p => p.IsVariadic));
        if (!anyOverloadVariadic)
        {
            for (int i = 0; i < call.Arguments.Length; i++)
            {
                if (call.Arguments[i] is SpreadElement spreadElem)
                {
                    var spreadType = _semanticInfo.GetExpressionType(spreadElem.Value);
                    if (spreadType is not null and not UnknownType and not TupleType)
                    {
                        AddError(
                            $"Cannot spread '{spreadType.GetDisplayName()}' into non-variadic function '{memberAccess.Member}'; " +
                            "use a function with *args parameter or pass arguments individually",
                            spreadElem.LineStart, spreadElem.ColumnStart,
                            code: DiagnosticCodes.Semantic.SpreadIntoNonVariadic,
                            span: spreadElem.Span);
                        return SemanticType.Unknown;
                    }
                }
            }
        }

        Func<SemanticType, SemanticType>? typeSubstitution = null;
        if (typeArgs != null && typeSymbol.TypeParameters.Count > 0)
        {
            var capturedTypeSymbol = typeSymbol;
            var capturedTypeArgs = typeArgs;
            typeSubstitution = t => SubstituteTypeParameters(t, capturedTypeSymbol.TypeParameters, capturedTypeArgs);
        }

        var kwNames = ExtractKeywordArgNames(call);
        var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads, totalArgCount, argTypes,
                SkipSelfParam: true, TypeSubstitution: typeSubstitution,
                SkipUnknownTypes: true, KeywordArgNames: kwNames, Call: call));

        if (isAmbiguous || matchingOverload == null)
        {
            ReportOverloadError(memberAccess.Member, call, isAmbiguous, arityCandidates, totalArgCount);
            return SemanticType.Unknown;
        }

        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        var returnType = matchingOverload.ReturnType;

        // Substitute type parameters for builtin generic types (e.g., T0 -> int for dict[str, int])
        if (typeSubstitution != null)
        {
            returnType = typeSubstitution(returnType);
        }

        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Finds all overloads for a method name walking the type hierarchy.
    /// Returns null if no overloads are found.
    /// </summary>
    private List<FunctionSymbol>? FindMethodOverloadsInHierarchy(TypeSymbol type, string methodName)
    {
        // Check the type itself
        if (type.MethodOverloads.TryGetValue(methodName, out var overloads) && overloads.Count > 0)
            return overloads;

        // Check base class chain using TypeHierarchyService
        foreach (var baseType in TypeHierarchyService.GetAllBaseTypes(type, SemanticBinding))
        {
            if (baseType.MethodOverloads.TryGetValue(methodName, out overloads) && overloads.Count > 0)
                return overloads;
        }

        // Check interfaces — handles interface-typed variables and interface
        // methods not found via base class chain (#364)
        foreach (var iface in TypeHierarchyService.GetAllInterfaces(type, SemanticBinding))
        {
            if (iface.MethodOverloads.TryGetValue(methodName, out overloads) && overloads.Count > 0)
                return overloads;
        }

        return null;
    }

    /// <summary>
    /// Reports an overload resolution error (ambiguous or no matching overload).
    /// Shared by all overload resolution methods to avoid duplicating diagnostic logic.
    /// </summary>
    private void ReportOverloadError(
        string calleeName, FunctionCall call, bool isAmbiguous,
        List<FunctionSymbol> arityCandidates, int totalArgCount)
    {
        if (isAmbiguous)
        {
            AddError($"Ambiguous call to overloaded method '{calleeName}' — multiple overloads match the argument types",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.AmbiguousOverload,
                span: call.Span);
        }
        else if (arityCandidates.Count == 0)
        {
            AddError($"No matching overload for '{calleeName}' with {totalArgCount} argument(s)",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                span: call.Span);
        }
        else
        {
            AddError($"No matching overload for '{calleeName}' with the given argument types",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NoMatchingOverload,
                span: call.Span);
        }
    }

    /// <summary>
    /// Unwraps nullable/optional types for null-conditional method calls.
    /// Returns the unwrapped type.
    /// </summary>
    private static SemanticType UnwrapCallTarget(SemanticType type)
    {
        if (type is NullableType nt)
            return nt.UnderlyingType;
        if (type is OptionalType ot)
            return ot.UnderlyingType;
        return type;
    }

    /// <summary>
    /// Resolves a FunctionSymbol from a member access expression (e.g., module.function()).
    /// Returns null if the member does not resolve to a FunctionSymbol.
    /// </summary>
    private FunctionSymbol? ResolveFunctionSymbolFromMemberAccess(MemberAccess memberAccess)
    {
        // Re-evaluate the object to get the module, then lookup the member.
        // This is duplicate work but necessary until we refactor to store symbols in SemanticInfo.
        var objectType = CheckExpression(memberAccess.Object);
        if (objectType is ModuleType moduleType)
        {
            var moduleSymbol = moduleType.Symbol;
            if (moduleSymbol.Exports.TryGetValue(memberAccess.Member, out var exportedSymbol))
            {
                return exportedSymbol as FunctionSymbol;
            }
        }
        return null;
    }

    /// <summary>
    /// Resolves a member access that NAMES a type, from either of the two qualifier kinds that can
    /// name one: an (possibly nested) imported module whose member is an exported
    /// <see cref="TypeSymbol"/> — <c>fractions.Fraction</c>, <c>email.message.Message</c>, with a
    /// PascalCase fallback for .NET modules — or a TYPE whose member is a nested type,
    /// <c>Outer.Inner</c> / <c>A.B.C</c>. Returns null when the qualifier is neither, or names no
    /// such member.
    /// <para>The nested arm is what gives <c>Outer.Inner(5)</c> the constructor type inference bare
    /// <c>Box(5)</c> has always had: both callers route a resolved type symbol into the shared
    /// constructor-checking path, and before this the member-access arm answered only for modules, so
    /// a nested generic construction without explicit type arguments never reached inference at all
    /// and emitted <c>new Outer.Inner(5)</c> — CS0305 (#1193).</para>
    /// </summary>
    private TypeSymbol? TryResolveTypeSymbolFromMemberAccess(MemberAccess memberAccess)
    {
        // The object is usually already checked (CheckCall runs CheckExpression on the callee
        // before routing here); fall back to checking it for paths that reach this helper
        // first (e.g., generic index-access resolution). Nested module access (email.message)
        // returns a ModuleType, so this handles both direct and nested module qualifiers.
        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object)
            ?? CheckExpression(memberAccess.Object);
        if (objectType is not ModuleType moduleType)
        {
            // A TYPE qualifier: the same symbol-table walk the explicit-type-argument spelling
            // (Outer.Inner[int]) already uses, so both spellings agree on what the member names.
            // Accessibility is not filtered here for the same reason it is not there — the
            // AccessValidator owns that report, and suppressing the symbol would degrade a precise
            // "not accessible" into a bare "not callable".
            return LookupNestedTypeSymbol(memberAccess);
        }

        var moduleSymbol = moduleType.Symbol;
        var memberName = memberAccess.Member;

        // For .NET modules, try PascalCase conversion if the exact name isn't found
        // (mirrors the module branch in CheckMemberAccess / CheckIndexAccess).
        if (!moduleSymbol.Exports.ContainsKey(memberName) && moduleSymbol.IsNetModule)
        {
            var pascalName = NameMangler.ToPascalCase(memberName);
            if (moduleSymbol.Exports.ContainsKey(pascalName))
                memberName = pascalName;
        }

        if (moduleSymbol.Exports.TryGetValue(memberName, out var exportedSymbol)
            && exportedSymbol is TypeSymbol typeSymbol)
        {
            return typeSymbol;
        }

        return null;
    }

    /// <summary>
    /// Resolves the object of a generic-instantiation index access (e.g., the <c>Box</c> in
    /// <c>Box[int](42)</c> or the <c>difflib.SequenceMatcher</c> in
    /// <c>difflib.SequenceMatcher[str](...)</c>) to its underlying <see cref="TypeSymbol"/>.
    /// Returns null when the object is neither a known type identifier nor a module-exported type.
    /// </summary>
    private TypeSymbol? TryResolveGenericTypeSymbolFromIndexObject(Expression indexObject) => indexObject switch
    {
        Identifier id => _symbolTable.Lookup(id.Name) as TypeSymbol,
        MemberAccess ma => TryResolveTypeSymbolFromMemberAccess(ma),
        _ => null
    };

    /// <summary>
    /// Resolves overloaded module-level functions (e.g., os.path.join with different arities).
    /// Returns null if the object is not a module or has no overloads for the member.
    /// </summary>
    private SemanticType? ResolveModuleFunctionOverload(
        MemberAccess memberAccess, List<SemanticType> argTypes, int totalArgCount, FunctionCall call,
        bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object);
        if (objectType is not ModuleType moduleType)
            return null;

        var moduleSymbol = moduleType.Symbol;
        if (!moduleSymbol.FunctionOverloads.TryGetValue(memberAccess.Member, out var overloads) || overloads.Count <= 1)
            return null;

        var kwNames = ExtractKeywordArgNames(call);
        var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads, totalArgCount, argTypes,
                SkipUnknownTypes: true, KeywordArgNames: kwNames, Call: call));

        if (isAmbiguous || matchingOverload == null)
        {
            ReportOverloadError(memberAccess.Member, call, isAmbiguous, arityCandidates, totalArgCount);
            return SemanticType.Unknown;
        }

        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        var returnType = InferGenericReturnType(matchingOverload, argTypes, call);

        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Resolves calls to overloaded module-level functions defined in the current
    /// compilation (i.e., not imported). Reads overloads from
    /// SymbolTable.LookupFunctionOverloads and dispatches to the matching overload
    /// by arity/signature. Imported overloads are handled separately by
    /// ResolveImportedFunctionOverload.
    /// </summary>
    private SemanticType? ResolveUserDefinedFunctionOverload(
        Identifier id, List<SemanticType> argTypes, int totalArgCount, FunctionCall call)
    {
        var overloads = _symbolTable.LookupFunctionOverloads(id.Name);
        if (overloads == null || overloads.Count <= 1)
            return null;

        // Only handle overloads declared in the current file. Imported overloads
        // (different DeclaringFilePath) are resolved by ResolveImportedFunctionOverload.
        if (_currentFilePath == null || overloads[0].DeclaringFilePath != _currentFilePath)
            return null;

        var kwNames = ExtractKeywordArgNames(call);
        var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads!, totalArgCount, argTypes,
                SkipUnknownTypes: true, KeywordArgNames: kwNames, Call: call));

        if (isAmbiguous || matchingOverload == null)
        {
            ReportOverloadError(id.Name, call, isAmbiguous, arityCandidates, totalArgCount);
            return SemanticType.Unknown;
        }

        // Update the identifier symbol to point to the matching overload
        _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        return InferGenericReturnType(matchingOverload, argTypes, call);
    }

    /// <summary>
    /// Resolves overloaded functions that were imported via from-import (e.g., from os.path import join).
    /// Uses the same overload resolution logic as ResolveModuleFunctionOverload but reads from
    /// SymbolTable.LookupFunctionOverloads instead of ModuleSymbol.FunctionOverloads.
    /// </summary>
    private SemanticType? ResolveImportedFunctionOverload(
        Identifier id, List<SemanticType> argTypes, int totalArgCount, FunctionCall call)
    {
        var overloads = _symbolTable.LookupFunctionOverloads(id.Name);
        if (overloads == null || overloads.Count <= 1)
            return null;

        // Shadow check: if a user-defined function with the same name exists and it
        // was NOT imported from the same source as the overloads, it shadows them.
        // Skip overload resolution so the normal call path uses the user's function.
        var funcSymbol = _symbolTable.Lookup(id.Name) as FunctionSymbol;
        if (funcSymbol != null)
        {
            // Check if funcSymbol is from a different source than the overloads.
            // Imported overloads share a DeclaringFilePath; a local shadow won't.
            // Guard against null paths (e.g. CLR-discovered symbols) to avoid
            // null == null being treated as "same source".
            var overloadPath = overloads[0].DeclaringFilePath;
            if (overloadPath != null && funcSymbol.DeclaringFilePath != overloadPath)
                return null;
        }

        var kwNames = ExtractKeywordArgNames(call);
        var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
            new OverloadResolutionContext(overloads!, totalArgCount, argTypes,
                SkipUnknownTypes: true, KeywordArgNames: kwNames, Call: call));

        if (isAmbiguous || matchingOverload == null)
        {
            ReportOverloadError(id.Name, call, isAmbiguous, arityCandidates, totalArgCount);
            return SemanticType.Unknown;
        }

        // Update the identifier symbol to point to the matching overload
        _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, matchingOverload);

        return InferGenericReturnType(matchingOverload, argTypes, call);
    }

    /// <summary>
    /// Checks for iterable spread arguments in a call to a non-variadic target.
    /// Returns true if a violation was found and a diagnostic was emitted.
    /// TupleType spreads are excluded because their size is statically known.
    /// </summary>
    private bool CheckSpreadIntoNonVariadic(
        FunctionCall call, string targetName, IReadOnlyList<ParameterSymbol>? parameters)
    {
        if (parameters == null)
            return false;

        var hasVariadicParam = parameters.Any(p => p.IsVariadic);
        if (hasVariadicParam)
            return false;

        for (int i = 0; i < call.Arguments.Length; i++)
        {
            if (call.Arguments[i] is SpreadElement spreadElem)
            {
                var spreadType = _semanticInfo.GetExpressionType(spreadElem.Value);
                if (spreadType is not null and not UnknownType and not TupleType)
                {
                    AddError(
                        $"Cannot spread '{spreadType.GetDisplayName()}' into non-variadic function '{targetName}'; " +
                        "use a function with *args parameter or pass arguments individually",
                        spreadElem.LineStart, spreadElem.ColumnStart,
                        code: DiagnosticCodes.Semantic.SpreadIntoNonVariadic,
                        span: spreadElem.Span);
                    return true;
                }
            }
        }
        return false;
    }

    private SemanticType InferGenericReturnType(
        FunctionSymbol overload, List<SemanticType> argTypes, FunctionCall call)
    {
        if (!overload.IsGeneric)
            return overload.ReturnType;

        var inferenceResult = _genericInference.InferTypeArguments(overload, argTypes);
        if (inferenceResult.Success && inferenceResult.InferredTypes != null)
        {
            _semanticInfo.SetInferredTypeArguments(call, inferenceResult.InferredTypes);
            return SubstituteTypeParameters(
                overload.ReturnType,
                overload.TypeParameters,
                inferenceResult.InferredTypes);
        }

        return overload.ReturnType;
    }

    /// <summary>
    /// Validates a function call against a resolved FunctionSymbol, including generic inference,
    /// argument count, positional/keyword argument type checking.
    /// </summary>
    private SemanticType ValidateFunctionSymbolCall(
        FunctionCall call, FunctionSymbol funcSymbol,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes,
        int totalArgCount, bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        // Record the resolved call target for codegen
        _semanticInfo.SetCallTarget(call, funcSymbol);

        CheckDeprecatedUsage(funcSymbol, call);

        // Check for iterable spread into non-variadic function (SPY0357)
        // Must run before generic inference — generic functions without *args must also reject
        // iterable spread. Tuple spread is excluded because tuple size is statically known.
        if (CheckSpreadIntoNonVariadic(call, funcSymbol.Name, funcSymbol.Parameters))
        {
            var earlyReturn = funcSymbol.ReturnType;
            if (isNullConditionalCall && earlyReturn is not NullableType and not OptionalType)
            {
                if (isOptionalNullConditional)
                    return new OptionalType { UnderlyingType = earlyReturn };
                return new NullableType { UnderlyingType = earlyReturn };
            }
            return earlyReturn;
        }

        // Handle generic function inference: identity(42) -> infer T=int
        // This is triggered when calling a generic function without explicit type arguments
        if (funcSymbol.IsGeneric)
        {
            // Diagnose a type parameter that can only be inferred from a lambda argument whose
            // parameters are unannotated (#904). Without this, inference binds the parameter to
            // Unknown and the leak surfaces as a C# CS0411 in generated code. Annotated lambdas
            // provide concrete types and are unaffected.
            if (TryReportUninferrableLambdaTypeArg(call, funcSymbol))
                return SemanticType.Unknown;

            // Keyword arguments also carry type information for inference (e.g.
            // cmp_to_key(cmp=lambda a: int, b: int: ...)). Map them into their formal
            // parameter slots so the index-aligned inference can see them (#909).
            var inferenceArgTypes = BuildInferenceArgumentTypes(
                funcSymbol, ApplyProjectionsToArgumentTypes(call, argTypes), kwargTypes);
            var inferenceResult = _genericInference.InferTypeArguments(funcSymbol, inferenceArgTypes);
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

        ValidateCallArguments(call, funcSymbol.Parameters, argTypes, kwargTypes, totalArgCount);

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

    /// <summary>
    /// Builds the positionally-aligned argument-type list used for generic inference, filling
    /// keyword arguments into their matching formal parameter slots. Generic inference is
    /// index-aligned to <see cref="FunctionSymbol.Parameters"/>, so a lambda passed by keyword
    /// (e.g. <c>cmp_to_key(cmp=...)</c>) must be placed in its parameter slot to contribute its
    /// type. When there are no keyword arguments the positional list is returned unchanged — the
    /// same <paramref name="argTypes"/> instance, not a copy, so callers must not mutate the result.
    /// Stops at the first parameter slot with neither a positional nor a keyword argument, since
    /// the index alignment cannot represent a gap.
    /// </summary>
    /// <summary>
    /// The positional argument types generic inference should unify against: an argument carrying an
    /// iterable projection contributes its PROJECTED type — the type codegen will actually pass
    /// (#1159). Unification walks types with no argument nodes to consult, so the substitution happens
    /// here, at the same argument-binding boundary <see cref="IsArgumentAssignable"/> applies the rule
    /// for non-generic signatures. Without it <c>any(d)</c>/<c>all(d)</c> — whose only parameter is
    /// <c>IEnumerable[T]</c> — bind nothing for <c>T</c> and fail SPY0237 even though the emitted
    /// argument is <c>d.Keys()</c>.
    /// Returns <paramref name="argTypes"/> itself when nothing is projected (the common case).
    /// </summary>
    private List<SemanticType> ApplyProjectionsToArgumentTypes(FunctionCall call, List<SemanticType> argTypes)
    {
        List<SemanticType>? substituted = null;
        for (int i = 0; i < argTypes.Count; i++)
        {
            if (ProjectedArgumentType(ArgumentNodeAt(call, i)) is not { } projected)
                continue;
            substituted ??= new List<SemanticType>(argTypes);
            substituted[i] = projected;
        }

        return substituted ?? argTypes;
    }

    private static List<SemanticType> BuildInferenceArgumentTypes(
        FunctionSymbol funcSymbol,
        List<SemanticType> argTypes,
        Dictionary<string, SemanticType> kwargTypes)
    {
        if (kwargTypes.Count == 0)
            return argTypes;

        var ordered = new List<SemanticType>();
        for (int i = 0; i < funcSymbol.Parameters.Count; i++)
        {
            if (i < argTypes.Count)
                ordered.Add(argTypes[i]);
            else if (kwargTypes.TryGetValue(funcSymbol.Parameters[i].Name, out var kwargType))
                ordered.Add(kwargType);
            else
                break;
        }

        return ordered;
    }

    /// <summary>
    /// Emits SPY0237 when a generic function has a type parameter that appears only in
    /// <see cref="FunctionType"/>-shaped parameter positions and the argument bound to that
    /// formal slot — positionally or by keyword name — is a lambda with one or more unannotated
    /// parameters — i.e. the type parameter is genuinely uninferrable. Returns true (and reports)
    /// when such a case is found, so the caller can stop instead of binding the parameter to
    /// Unknown and leaking CS0411 (#904).
    /// Annotated lambdas supply concrete parameter types and never trigger this.
    /// </summary>
    private bool TryReportUninferrableLambdaTypeArg(FunctionCall call, FunctionSymbol funcSymbol)
    {
        if (!funcSymbol.IsGeneric || funcSymbol.TypeParameters.Count == 0)
            return false;

        foreach (var typeParam in funcSymbol.TypeParameters)
        {
            if (!IsTypeParameterUninferrableWithoutLambdaAnnotation(funcSymbol, typeParam.Name))
                continue;

            for (int i = 0; i < funcSymbol.Parameters.Count; i++)
            {
                var paramType = funcSymbol.Parameters[i].Type;
                if (paramType is not FunctionType functionParam
                    || !functionParam.ParameterTypes.Any(p => ReferencesTypeParameterNamed(p, typeParam.Name)))
                    continue;

                // Bind the i-th formal parameter to its argument: positionally to the i-th
                // positional argument, or by name to a matching keyword argument.
                var boundArg = i < call.Arguments.Length
                    ? call.Arguments[i]
                    : call.KeywordArguments.FirstOrDefault(k => k.Name == funcSymbol.Parameters[i].Name)?.Value;
                if (boundArg is LambdaExpression lambda
                    && lambda.Parameters.Any(p => p.Type == null))
                {
                    AddError(
                        $"Cannot infer type argument '{typeParam.Name}' for generic function " +
                        $"'{funcSymbol.Name}' from an unannotated lambda. Annotate the lambda " +
                        "parameters (e.g. `lambda a: int, b: int: ...`).",
                        call.LineStart, call.ColumnStart,
                        code: DiagnosticCodes.Semantic.CannotInferGenericType,
                        span: call.Span);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when the named type parameter can only be inferred from the <em>parameter</em>
    /// types of a lambda/delegate argument — i.e. it appears in the parameter-type positions of some
    /// <see cref="FunctionType"/> parameter and in no position that an argument could bind on its own:
    /// a non-function parameter (bound directly from its argument) or a <see cref="FunctionType"/>
    /// parameter's <em>return</em> position (inferred from the lambda body, no annotation needed).
    /// This distinguishes <c>cmp_to_key(Func&lt;T,T,int&gt;)</c> (T only in lambda param positions →
    /// needs annotation) from <c>list.sort(key=Func&lt;T,TKey&gt;)</c> (TKey is in the return position,
    /// inferred from the body). The function's own outer return type is irrelevant — it can never
    /// bind a type parameter from arguments.
    /// </summary>
    private static bool IsTypeParameterUninferrableWithoutLambdaAnnotation(FunctionSymbol funcSymbol, string name)
    {
        var inLambdaParamPosition = false;
        var inInferablePosition = false;

        foreach (var param in funcSymbol.Parameters)
        {
            var type = param.Type;
            if (type is null || !ReferencesTypeParameterNamed(type, name))
                continue;

            if (type is FunctionType functionParam)
            {
                if (functionParam.ParameterTypes.Any(p => ReferencesTypeParameterNamed(p, name)))
                    inLambdaParamPosition = true;
                if (ReferencesTypeParameterNamed(functionParam.ReturnType, name))
                    inInferablePosition = true;
            }
            else
            {
                // A non-function parameter binds the type parameter directly from its argument.
                inInferablePosition = true;
            }
        }

        return inLambdaParamPosition && !inInferablePosition;
    }

    /// <summary>
    /// Validates a function call against a FunctionType (lambda/delegate calls without a FunctionSymbol).
    /// </summary>
    private SemanticType CheckLambdaCall(
        FunctionCall call, FunctionType ft, List<SemanticType> argTypes,
        int totalArgCount, bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        var paramTypes = ft.ParameterTypes;
        var returnType = ft.ReturnType;

        // Infer method-level generic type parameters from arguments BEFORE validation.
        // For methods like Map<U>(Func<T, U> f) -> Result<U, E>, the method-level
        // TypeParameterType("U") appears in both parameter types and return type.
        // We infer U from the actual argument types and substitute everywhere.
        if (ContainsTypeParameterType(returnType) || paramTypes.Any(ContainsTypeParameterType))
        {
            var typeParamMap = _genericInference.UnifyTypes(paramTypes, argTypes);
            if (typeParamMap != null && typeParamMap.Count > 0)
            {
                returnType = GenericTypeInferenceService.SubstituteTypeParameters(returnType, typeParamMap);
                paramTypes = paramTypes
                    .Select(p => GenericTypeInferenceService.SubstituteTypeParameters(p, typeParamMap))
                    .ToList();
            }
        }

        // Skip validation for .NET types with multiple constructor overloads
        // (C# compiler will handle overload resolution)
        if (!ft.SkipArgumentValidation)
        {
            var variadicIndex = ft.VariadicParameterIndex;
            var hasVariadic = variadicIndex.HasValue;

            // Validate argument count (accounting for optional parameters with defaults
            // and variadic params). The variadic parameter itself is not counted toward
            // the required minimum (it accepts zero or more), and variadic calls have
            // no upper bound on positional arguments.
            var requiredCount = paramTypes.Count - ft.OptionalParameterCount - (hasVariadic ? 1 : 0);
            var tooFew = totalArgCount < requiredCount;
            var tooMany = !hasVariadic && totalArgCount > paramTypes.Count;

            if (tooFew || tooMany)
            {
                if (hasVariadic)
                {
                    AddError($"Function expects at least {requiredCount} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
                else if (ft.OptionalParameterCount > 0 && requiredCount != paramTypes.Count)
                {
                    AddError($"Function expects {requiredCount} to {paramTypes.Count} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
                else
                {
                    AddError($"Function expects {paramTypes.Count} arguments but got {totalArgCount}",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                        span: call.Span);
                }
            }
            else
            {
                // Validate positional argument types. Arguments at or after the variadic
                // parameter index all bind to the variadic element type (paramTypes holds
                // the element type at that slot, not an array).
                for (int i = 0; i < argTypes.Count; i++)
                {
                    SemanticType expected;
                    if (hasVariadic && i >= variadicIndex!.Value)
                    {
                        expected = paramTypes[variadicIndex.Value];
                    }
                    else if (i < paramTypes.Count)
                    {
                        expected = paramTypes[i];
                    }
                    else
                    {
                        break;
                    }

                    if (!IsArgumentAssignable(argTypes[i], expected, ArgumentNodeAt(call, i)))
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{expected.GetDisplayName()}'",
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: call.Arguments[i].Span);
                    }
                }
            }
        }

        // Wrap result in optional/nullable for null conditional calls
        if (isNullConditionalCall && returnType is not NullableType and not OptionalType)
        {
            if (isOptionalNullConditional)
                return new OptionalType { UnderlyingType = returnType };
            return new NullableType { UnderlyingType = returnType };
        }
        return returnType;
    }

    /// <summary>
    /// Returns true when the parameter type is backed by CLR <see cref="System.Type"/> — i.e., it
    /// expects a type reference (e.g. assert_raises's exceptionType parameter). Discovery rehydrates
    /// System.Type as a <see cref="BuiltinType"/>; ClrTypeBridge produces a <see cref="UserDefinedType"/>.
    /// </summary>
    private static bool IsSystemTypeParameter(SemanticType paramType)
    {
        var clrType = paramType switch
        {
            BuiltinType bt => bt.ClrType,
            UserDefinedType udt => udt.Symbol?.ClrType,
            _ => null
        };
        return clrType == typeof(System.Type);
    }

    /// <summary>
    /// Validates call arguments against a parameter list: argument count, types,
    /// and positional-only/keyword-only constraints. Used by both regular function
    /// calls and constructor calls (with __init__ params minus self).
    ///
    /// <para><paramref name="typeBinding"/> carries a constructor's type-parameter binding, so
    /// <c>Slot[int, str](...)</c> validates against the SUBSTITUTED <c>__init__</c> signature
    /// (<c>key: int</c>) rather than the declared one (<c>key: K</c>) — #1243. Parameters whose
    /// substituted type still contains a type parameter are left to inference and not
    /// type-checked, which is what makes one mechanism serve the explicitly-instantiated, the
    /// inferred and the non-generic constructor alike: for the last two the binding is simply
    /// absent or empty.</para>
    /// </summary>
    private void ValidateCallArguments(
        FunctionCall call, IReadOnlyList<ParameterSymbol> parameters,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes,
        int totalArgCount, TypeParameterBinding? typeBinding = null)
    {
        var hasVariadicParam = parameters.Any(p => p.IsVariadic);
        var requiredParamCount = parameters.Count(p => !p.HasDefault && !p.IsVariadic);
        var totalParamCount = parameters.Count;

        // Count parameters eligible for positional arguments (not keyword-only)
        var positionalParamCount = parameters.Count(p => !p.IsKeywordOnly);

        // Validate argument count considering defaults and variadic params
        var tooFew = totalArgCount < requiredParamCount;
        var tooManyPositional = !hasVariadicParam && argTypes.Count > positionalParamCount;
        var tooMany = !hasVariadicParam && totalArgCount > totalParamCount;
        if (tooFew || tooMany || tooManyPositional)
        {
            if (hasVariadicParam)
            {
                AddError($"Function expects at least {requiredParamCount} arguments but got {totalArgCount}",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
            }
            else if (requiredParamCount == totalParamCount)
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
            var variadicParamIndex = parameters.ToList().FindIndex(p => p.IsVariadic);
            for (int i = 0; i < argTypes.Count; i++)
            {
                ParameterSymbol param;
                if (variadicParamIndex >= 0 && i >= variadicParamIndex)
                {
                    param = parameters[variadicParamIndex];
                }
                else if (i < parameters.Count)
                {
                    param = parameters[i];
                }
                else
                {
                    break;
                }

                if (param.IsKeywordOnly)
                {
                    AddError($"'{param.Name}' is keyword-only and must be passed as a keyword argument",
                        call.Arguments[i].LineStart, call.Arguments[i].ColumnStart,
                        code: DiagnosticCodes.Semantic.KeywordOnlyPassedPositionally,
                        span: call.Arguments[i].Span);
                    continue;
                }

                var paramType = SubstitutedParameterType(param.Type, typeBinding);
                if (paramType == null)
                    continue; // still open after substitution — inference decides, not this check

                // A Sharpy-native collection parameter is a Sharpy slot, so a CLR sequence bound to it
                // materializes (#1251). A CLR-mapped parameter is NOT — there the emitted formal is the
                // CLR type and the value goes in unconverted (#1260); RecordSequenceMaterialization
                // enforces that distinction, so both rules read the same predicate.
                RecordSequenceMaterialization(ArgumentNodeAt(call, i), argTypes[i], paramType);

                if (!IsArgumentAssignable(argTypes[i], paramType, ArgumentNodeAt(call, i)))
                {
                    // PEP 675: string literals (and concatenations thereof) satisfy LiteralString
                    if (paramType is LiteralStringType && i < call.Arguments.Length
                        && IsLiteralStringExpression(call.Arguments[i]))
                    {
                        // Allow — literal string expression satisfies LiteralString
                    }
                    // A type-reference expression (e.g. module.SomeError) satisfies a
                    // parameter backed by CLR System.Type (e.g. assert_raises's exceptionType).
                    else if (IsSystemTypeParameter(paramType) && i < call.Arguments.Length
                        && _semanticInfo.IsTypeReference(call.Arguments[i]))
                    {
                        // Allow — type reference satisfies a System.Type parameter
                    }
                    else
                    {
                        AddError($"Cannot pass argument of type '{argTypes[i].GetDisplayName()}' to parameter of type '{paramType.GetDisplayName()}'"
                            + DescribeTypeParameterBinding(param.Type, typeBinding),
                            call.Arguments[i].LineStart, call.Arguments[i].ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: call.Arguments[i].Span);
                    }
                }
            }

            // Validate keyword arguments
            foreach (var kwarg in call.KeywordArguments)
            {
                var param = parameters.FirstOrDefault(p => p.Name == kwarg.Name);
                if (param == null)
                {
                    AddError($"Unknown keyword argument '{kwarg.Name}'",
                        kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                        span: kwarg.Span ?? kwarg.Value.Span);
                }
                else if (param.IsPositionalOnly)
                {
                    AddError($"'{kwarg.Name}' is positional-only and cannot be passed as a keyword argument",
                        kwarg.LineStart, kwarg.ColumnStart,
                        code: DiagnosticCodes.Semantic.PositionalOnlyPassedByKeyword,
                        span: kwarg.Span ?? kwarg.Value.Span);
                }
                else
                {
                    var paramIndex = parameters.ToList().IndexOf(param);
                    var paramType = SubstitutedParameterType(param.Type, typeBinding);
                    if (!param.IsKeywordOnly && paramIndex < argTypes.Count)
                    {
                        AddError($"Argument '{kwarg.Name}' was already provided positionally",
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                            span: kwarg.Span ?? kwarg.Value.Span);
                    }
                    else if (paramType != null
                        && !IsArgumentAssignable(kwargTypes[kwarg.Name], paramType)
                        && !(IsSystemTypeParameter(paramType) && _semanticInfo.IsTypeReference(kwarg.Value)))
                    {
                        AddError($"Cannot pass argument of type '{kwargTypes[kwarg.Name].GetDisplayName()}' to parameter '{kwarg.Name}' of type '{paramType.GetDisplayName()}'"
                            + DescribeTypeParameterBinding(param.Type, typeBinding),
                            kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.TypeMismatch,
                            span: kwarg.Span ?? kwarg.Value.Span);
                    }
                }
            }
        }
    }

    /// <summary>
    /// How a construction binds the constructed declaration's type parameters (#1243).
    /// <paramref name="Substitution"/> is empty when the construction wrote no type arguments —
    /// <c>Slot(1, "b")</c> — because inference has not run yet at this point;
    /// <paramref name="TypeParameterNames"/> still names them all, which is what lets
    /// <see cref="SubstitutedParameterType"/> recognise a still-open parameter and stay out of
    /// inference's way.
    /// </summary>
    /// <param name="Substitution">Type-parameter name to written type argument.</param>
    /// <param name="TypeParameterNames">Every type parameter of the constructed declaration.</param>
    /// <param name="Origin">
    /// The written instantiation (<c>Slot[int, str]</c>), used only to explain a mismatch;
    /// null when nothing was written.
    /// </param>
    private sealed record TypeParameterBinding(
        IReadOnlyDictionary<string, SemanticType> Substitution,
        IReadOnlySet<string> TypeParameterNames,
        string? Origin);

    /// <summary>
    /// Validates an overloaded constructor's arguments when exactly one overload can accept this
    /// many of them, and stays silent otherwise (#1243).
    ///
    /// <para>The count is the whole selection rule here on purpose. Selecting on argument TYPES
    /// would mean re-deciding overload resolution at a seam that does not own it, and would report
    /// against a guess when nothing matches; when one arity fits, the user's intent is not in
    /// doubt, so a wrong argument there is exactly as diagnosable as it is for a single
    /// <c>__init__</c>. Two overloads of the same arity keep the pre-#1243 silence — a real
    /// remaining gap, but a narrower one than "no overloaded constructor is ever checked".</para>
    /// </summary>
    private void ValidateSoleArityMatchingOverload(
        FunctionCall call, IReadOnlyList<FunctionSymbol> initMethods,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes,
        int totalArgCount, TypeParameterBinding? typeBinding)
    {
        List<ParameterSymbol>? soleMatch = null;
        foreach (var init in initMethods)
        {
            var parameters = init.Parameters.Skip(1).ToList();
            if (parameters.Any(p => p.IsVariadic))
                return; // a variadic overload can absorb any count; arity decides nothing

            var required = parameters.Count(p => !p.HasDefault);
            if (totalArgCount < required || totalArgCount > parameters.Count)
                continue;

            if (soleMatch != null)
                return; // more than one overload accepts this count — leave resolution alone

            soleMatch = parameters;
        }

        if (soleMatch == null)
            return; // none fits: the count diagnostic belongs to overload resolution, not here

        ValidateCallArguments(call, soleMatch, argTypes, kwargTypes, totalArgCount, typeBinding);
    }

    /// <summary>
    /// The binding for a construction that WROTE its type arguments — <c>Slot[int, str](...)</c>.
    /// The vector is the resolver's, already default-filled, and is positionally paired with the
    /// declaration's type parameters.
    /// </summary>
    private static TypeParameterBinding? WrittenTypeParameterBinding(
        TypeSymbol typeSymbol, IReadOnlyList<SemanticType> typeArgs)
    {
        var typeParams = typeSymbol.TypeParameters;
        if (typeParams.Count == 0 || typeParams.Count != typeArgs.Count)
            return UnwrittenTypeParameterBinding(typeSymbol);

        var substitution = new Dictionary<string, SemanticType>(StringComparer.Ordinal);
        for (int i = 0; i < typeParams.Count; i++)
            substitution[typeParams[i].Name] = typeArgs[i];

        var origin = $"{typeSymbol.Name}[{string.Join(", ", typeArgs.Select(a => a.GetDisplayName()))}]";
        return new TypeParameterBinding(
            substitution,
            new HashSet<string>(typeParams.Select(tp => tp.Name), StringComparer.Ordinal),
            origin);
    }

    /// <summary>
    /// The binding for a construction that wrote NO type arguments — <c>Slot(1, "b")</c>, or any
    /// non-generic type. It carries the parameter names but no substitution, which is precisely
    /// "these are still open": <see cref="SubstitutedParameterType"/> declines to check them and
    /// leaves them to the inference that runs after this validation.
    /// </summary>
    private static TypeParameterBinding? UnwrittenTypeParameterBinding(TypeSymbol typeSymbol)
        => typeSymbol.TypeParameters.Count == 0
            ? null
            : new TypeParameterBinding(
                new Dictionary<string, SemanticType>(StringComparer.Ordinal),
                new HashSet<string>(typeSymbol.TypeParameters.Select(tp => tp.Name), StringComparer.Ordinal),
                Origin: null);

    /// <summary>
    /// The parameter type this call actually binds, or <c>null</c> when it is still open and only
    /// inference can decide it. Constructors are the one call seam that had no argument type check
    /// at all — a mismatch escaped to Roslyn as CS1503 behind SPY0908, generic or not (#1243) — and
    /// this is the substitution that lets one check serve every spelling.
    /// <para>Substitution uses <c>substituteNamedUserTypes</c> because an imported generic
    /// materialises a type-parameter reference in a member signature as a bare
    /// <see cref="UserDefinedType"/> named after the parameter rather than a
    /// <see cref="TypeParameterType"/>. Without it the three import spellings of the same
    /// mismatch would not agree, which is the acceptance axis for #1243. The openness test reads
    /// <see cref="TypeParameterBinding.TypeParameterNames"/> for the same reason: a leftover
    /// <c>UserDefinedType</c> named <c>K</c> is an unbound type parameter, not a user type.</para>
    /// </summary>
    private static SemanticType? SubstitutedParameterType(
        SemanticType declared, TypeParameterBinding? binding)
    {
        if (binding == null)
            return ContainsTypeParameter(declared) ? null : declared;

        var substituted = binding.Substitution.Count > 0
            ? TypeSubstitution.Apply(declared, binding.Substitution, substituteNamedUserTypes: true)
            : declared;

        return ContainsTypeParameter(substituted)
               || MentionsTypeParameter(substituted, binding.TypeParameterNames)
            ? null
            : substituted;
    }

    /// <summary>
    /// Whether the type still names one of <paramref name="typeParameterNames"/>, in either of the
    /// two shapes a type-parameter reference can take (see <see cref="SubstitutedParameterType"/>).
    /// </summary>
    private static bool MentionsTypeParameter(SemanticType type, IReadOnlySet<string> typeParameterNames)
    {
        if (typeParameterNames.Count == 0)
            return false;

        return type switch
        {
            TypeParameterType tpt => typeParameterNames.Contains(tpt.Name),
            UserDefinedType udt => typeParameterNames.Contains(udt.Name),
            GenericType gt => gt.TypeArguments.Any(t => MentionsTypeParameter(t, typeParameterNames)),
            NullableType nt => MentionsTypeParameter(nt.UnderlyingType, typeParameterNames),
            OptionalType ot => MentionsTypeParameter(ot.UnderlyingType, typeParameterNames),
            TupleType tt => tt.ElementTypes.Any(t => MentionsTypeParameter(t, typeParameterNames)),
            ResultType rt => MentionsTypeParameter(rt.OkType, typeParameterNames)
                             || MentionsTypeParameter(rt.ErrorType, typeParameterNames),
            FunctionType ft => ft.ParameterTypes.Any(t => MentionsTypeParameter(t, typeParameterNames))
                               || MentionsTypeParameter(ft.ReturnType, typeParameterNames),
            _ => false
        };
    }

    /// <summary>
    /// The clause that tells a user why a parameter written <c>key: K</c> has to be an <c>int</c>:
    /// their own instantiation bound it. Empty for a non-generic parameter, where the declaration
    /// already reads as the checked type and the leading clause is the whole story — which keeps
    /// the message byte-identical to the method and free-function seams for every ordinary call.
    /// </summary>
    private static string DescribeTypeParameterBinding(
        SemanticType declared, TypeParameterBinding? binding)
    {
        if (binding?.Origin is not { } origin || binding.Substitution.Count == 0)
            return string.Empty;

        var bound = binding.Substitution
            .Where(entry => MentionsTypeParameter(declared, new HashSet<string>(StringComparer.Ordinal) { entry.Key }))
            .Select(entry => $"'{entry.Key}' to '{entry.Value.GetDisplayName()}'")
            .ToList();

        return bound.Count == 0 ? string.Empty : $"; '{origin}' binds {string.Join(", ", bound)}";
    }

    private SemanticType CheckIifeLambdaCall(FunctionCall call, LambdaExpression lambda)
    {
        // 1. Check arguments first to get concrete types
        var argTypes = new List<SemanticType>();
        foreach (var arg in call.Arguments)
            argTypes.Add(CheckExpression(arg));

        // 2. Validate argument count (accounting for default parameters)
        var totalParamCount = lambda.Parameters.Length;
        var optionalCount = lambda.Parameters.Count(p => p.DefaultValue != null);
        var requiredParamCount = totalParamCount - optionalCount;

        if (argTypes.Count < requiredParamCount || argTypes.Count > totalParamCount)
        {
            var countDesc = requiredParamCount == totalParamCount
                ? $"{totalParamCount}"
                : $"{requiredParamCount} to {totalParamCount}";
            AddError($"Lambda expects {countDesc} argument(s) but {argTypes.Count} were given",
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: call.Span);
            // Check lambda for error recovery (records its type in SemanticInfo)
            CheckExpression(call.Function);
            return SemanticType.Unknown;
        }

        // 3. Build expected FunctionType from argument types
        var expectedFuncType = new FunctionType
        {
            ParameterTypes = argTypes,
            ReturnType = SemanticType.Unknown
        };

        // 4. Check lambda with expected type context
        var saved = _expectedType;
        _expectedType = expectedFuncType;
        var lambdaType = CheckExpression(call.Function);
        _expectedType = saved;

        // 5. Return the inferred return type
        return lambdaType is FunctionType ft ? ft.ReturnType : SemanticType.Unknown;
    }

    /// <summary>
    /// Attempts to infer generic type arguments for a constructor call by creating a synthetic
    /// FunctionSymbol from the class's __init__ method and using GenericTypeInferenceService.
    /// Returns null if inference fails (caller should fall back to error or UnknownType).
    /// </summary>
    /// <summary>
    /// Handles union case construction: validates arguments against case fields
    /// and performs type parameter substitution for generic unions.
    /// </summary>
    private SemanticType CheckUnionCaseConstruction(
        FunctionCall call, UserDefinedType caseUdt, TypeSymbol unionBaseSymbol,
        List<SemanticType> argTypes)
    {
        var caseFields = caseUdt.Symbol!.Fields;

        // For generic unions, substitute type parameters using the expected type
        var typeParams = unionBaseSymbol.TypeParameters;
        List<SemanticType>? typeArgs = null;
        if (typeParams.Count > 0 && _expectedType is GenericType expectedGenericType
            && expectedGenericType.Name == unionBaseSymbol.Name
            && expectedGenericType.TypeArguments.Count == typeParams.Count)
        {
            typeArgs = expectedGenericType.TypeArguments;
        }

        // Validate argument count
        if (argTypes.Count != caseFields.Count)
        {
            AddError($"Union case '{unionBaseSymbol.Name}.{caseUdt.Name}' expects {caseFields.Count} argument(s) but got {argTypes.Count}",
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: call.Span);
        }
        else
        {
            // Validate argument types (with type parameter substitution for generics)
            for (int i = 0; i < caseFields.Count; i++)
            {
                var expectedFieldType = caseFields[i].Type;
                if (typeArgs != null)
                {
                    expectedFieldType = SubstituteTypeParameters(expectedFieldType, typeParams, typeArgs);
                }

                if (!IsAssignable(argTypes[i], expectedFieldType))
                {
                    AddError($"Argument {i + 1} has type '{argTypes[i].GetDisplayName()}' but field '{caseFields[i].Name}' expects '{expectedFieldType.GetDisplayName()}'",
                        call.Arguments[i].LineStart, call.Arguments[i].ColumnStart,
                        code: DiagnosticCodes.Semantic.TypeMismatch,
                        span: call.Arguments[i].Span);
                }
            }
        }

        // For generic unions, return a GenericType matching the expected type
        if (typeArgs != null)
        {
            return new GenericType
            {
                Name = unionBaseSymbol.Name,
                TypeArguments = typeArgs,
                GenericDefinition = unionBaseSymbol
            };
        }

        // For non-generic unions, return the union base type
        return new UserDefinedType { Name = unionBaseSymbol.Name, Symbol = unionBaseSymbol };
    }

    /// <summary>
    /// Checks call arguments and keyword arguments, collecting their types.
    /// Sets _expectedType per-parameter when an early function symbol or callee FunctionType
    /// is available, enabling constructor inference (Some/None()/Ok/Err) in function arguments.
    /// </summary>
    /// <param name="callee">The canonical (paren-stripped) callee — see the #1170 contract in
    /// <see cref="AstHelper.UnwrapParenthesized"/>.</param>
    private (List<SemanticType> ArgTypes, Dictionary<string, SemanticType> KwargTypes) CheckCallArguments(
        FunctionCall call, Expression callee, FunctionSymbol? earlyFuncSymbol, int earlyParamOffset,
        FunctionType? calleeFunctionType)
    {
        var argTypes = new List<SemanticType>();
        // #1009: map(lambda, iter1, iter2, ...) needs the lambda's parameter types inferred
        // from the iterables' element types so an unannotated multi-iterable map closes its
        // return type. Handle the whole positional list specially in that one case. It runs before
        // the general deferral below, which would otherwise take over map and lose this path's
        // element-type projection details.
        if (TryCheckMapLambdaArguments(call, callee, argTypes))
        {
            // Positional list populated in source order; keyword arguments are checked below.
        }
        // #1161: a lambda argument whose parameter types arrive inward from generic-call unification
        // must have its body checked AFTER the other arguments bind those type parameters — otherwise
        // every fact the body records (read-node types, operator lowerings) is computed from a
        // placeholder. Handles the whole argument list (positional and keyword) when it applies.
        else if (TryCheckDeferredLambdaArguments(call, callee, earlyFuncSymbol, earlyParamOffset,
                     out var deferredArgTypes, out var deferredKwargTypes))
        {
            return (deferredArgTypes, deferredKwargTypes);
        }
        else
        {
            for (int argIdx = 0; argIdx < call.Arguments.Length; argIdx++)
            {
                var previousExpectedType = _expectedType;

                // Handle spread arguments: *expr
                if (call.Arguments[argIdx] is SpreadElement spreadArg)
                {
                    var spreadValueType = CheckExpression(spreadArg.Value);

                    if (spreadValueType is TupleType tupleSpread)
                    {
                        // Tuple spread: expand element types as individual arguments
                        argTypes.AddRange(tupleSpread.ElementTypes);
                    }
                    else
                    {
                        // Iterable spread: extract element type for variadic param matching
                        var elemType = _typeInference.InferIterableElementType(spreadValueType);
                        if (elemType != null)
                            argTypes.Add(elemType);
                        else
                            argTypes.Add(SemanticType.Unknown);
                    }
                    _expectedType = previousExpectedType;
                    continue;
                }

                if (earlyFuncSymbol != null && argIdx + earlyParamOffset < earlyFuncSymbol.Parameters.Count)
                {
                    var paramType = earlyFuncSymbol.Parameters[argIdx + earlyParamOffset].Type;
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

        return (argTypes, kwargTypes);
    }

    /// <summary>
    /// #1009: For the builtin <c>map(lambda, iter1, iter2, ...)</c>, infer the lambda's parameter
    /// types from the iterables' element types (proper bidirectional inference) so an unannotated
    /// multi-iterable map closes its return type and the wrapping <c>list(...)</c> emits a typed
    /// <c>Sharpy.List&lt;T&gt;</c>. The single-iterable case already works via the body heuristic
    /// (<see cref="TryInferLambdaParamTypesFromBody"/>); this also strengthens it and is the only
    /// path that handles lambdas the heuristic cannot (e.g. both binary operands placeholders, or a
    /// bare-identifier body). Gated narrowly on the builtin <c>map</c> with a lambda first argument,
    /// mirroring how <see cref="ResolveEarlyMethodSymbol"/> gates on <see cref="CallHasLambdaArgument"/>.
    /// </summary>
    /// <returns>
    /// True if this call was handled here (positional <paramref name="argTypes"/> populated in
    /// source order); false to let the caller's normal argument loop run.
    /// </returns>
    private bool TryCheckMapLambdaArguments(FunctionCall call, Expression callee, List<SemanticType> argTypes)
    {
        // Bare `map(...)` call (an Identifier callee) with a lambda first argument and at least
        // one iterable. Method-form or aliased map() falls through to the normal loop.
        if (callee is not Identifier mapId || mapId.Name != BuiltinNames.Map)
            return false;
        if (call.Arguments.Length < 2 || call.Arguments[0] is not LambdaExpression)
            return false;

        // Don't special-case a user-defined `map` that shadows the builtin.
        var sym = _symbolTable.Lookup(mapId.Name) as FunctionSymbol;
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(mapId.Name);
        bool isBuiltinMap = overloads != null && (sym == null || overloads.Contains(sym));
        if (!isBuiltinMap)
            return false;

        // Spreads change positional arity; keep the fast path simple and defer to the normal loop.
        foreach (var arg in call.Arguments)
        {
            if (arg is SpreadElement)
                return false;
        }

        // Check the iterable arguments first (args[1..]) to learn their element types. One entry
        // is recorded per positional argument so the list stays POSITIONALLY ALIGNED with the
        // lambda's parameters: when an argument's element type can't be inferred (an opaque or
        // non-iterable arg, e.g. a trailing positional `strict` bool), Unknown is recorded in its
        // place rather than dropped. Dropping would shift a later iterable's element type onto an
        // earlier lambda parameter (mis-inference). A keyword `strict=` is handled separately and
        // never reaches the positional list; a trailing positional `strict` adds a surplus Unknown
        // that CheckLambda ignores (it maps only up to the lambda's own arity).
        var elementTypes = new List<SemanticType>();
        var iterableTypes = new List<SemanticType>();
        var previousExpectedType = _expectedType;
        _expectedType = null;
        for (int i = 1; i < call.Arguments.Length; i++)
        {
            var argType = CheckExpression(call.Arguments[i]);
            iterableTypes.Add(argType);
            var elem = _typeInference.InferIterableElementType(argType);
            elementTypes.Add(elem ?? SemanticType.Unknown);
        }

        // Feed the synthesized expected function type into the lambda so CheckLambda types its
        // parameters from the element types (lambdas/CheckLambda already consume a FunctionType
        // _expectedType). The return type is left Unknown; the body determines it.
        _expectedType = new FunctionType
        {
            ParameterTypes = elementTypes,
            ReturnType = SemanticType.Unknown
        };
        var lambdaType = CheckExpression(call.Arguments[0]);
        _expectedType = previousExpectedType;

        // Reassemble positional argTypes in source order: [lambda, iter1, iter2, ...].
        argTypes.Add(lambdaType);
        argTypes.AddRange(iterableTypes);
        return true;
    }

    private static readonly int[] IterablePositionZero = { 0 };
    private static readonly int[] IterablePositionOne = { 1 };

    /// <summary>
    /// Records how each argument sitting in a call's ITERABLE position binds there — its element type
    /// and the projection codegen must apply — for every iterable source, not just dicts (#1154,
    /// #1159, #1198, #1199). This is the single recording choke point for the whole iteration ring.
    /// The emitter reads the mark in its argument-generation funnel and applies the projection
    /// verbatim; it never re-derives which positions are iterable or what a source iterates as
    /// (repo rule 2).
    ///
    /// <para>The mark is also the gate on ACCEPTING a non-<c>list</c> iterable in these positions
    /// (<see cref="ProjectedArgumentType"/>), so recording runs before any dispatch — every consumer
    /// that binds arguments reads it. Recording only sources that
    /// <see cref="ClassifyIterableArgument"/> can also lower is what keeps acceptance and lowering
    /// one decision.</para>
    ///
    /// <para>The position tables are the only thing that grants this acceptance, which keeps it
    /// scoped: a user-declared <c>def f(xs: list[int])</c> parameter and ordinary assignment stay
    /// strict (Axiom 3). <c>for k in d</c> (duck-typed foreach) never reaches here at all.</para>
    ///
    /// <para>Two callee shapes carry iterable positions: an identifier naming a builtin
    /// (<c>sum(s)</c>) and a method on a receiver (<c>", ".join(s)</c>). Anything else records
    /// nothing.</para>
    /// </summary>
    private void RecordIterableArgumentMarks(FunctionCall call, Expression callee)
    {
        IReadOnlyList<int>? positions;
        switch (callee)
        {
            case Identifier id:
                positions = GetBuiltinIterableKeyPositions(id.Name, call.Arguments.Length);
                // A user-defined function shadowing the builtin name takes over (Python scoping); its
                // dict parameter is not projected. Builtin collection type names (list/set/tuple) are
                // reserved, so a user shadow there is not a concern.
                if (_symbolTable.Lookup(id.Name) is FunctionSymbol { CodeGenInfo: not null })
                    return;
                break;

            case MemberAccess memberAccess:
                positions = GetMemberIterableKeyPositions(memberAccess, call.Arguments.Length);
                break;

            default:
                return;
        }

        if (positions == null)
            return;

        foreach (var position in positions)
        {
            if (position >= call.Arguments.Length)
                continue;
            var argNode = call.Arguments[position];
            var argType = _semanticInfo.GetExpressionType(argNode);
            if (argType == null || ClassifyIterableArgument(argType) is not { } projection)
                continue;

            // reversed(s) has a dedicated lowering — StringHelpers.Reversed(string) — that consumes
            // the RAW string lazily, with no list materialization. Recording StrToList here would
            // make the positional-argument funnel wrap the operand in ListFromStr before that arm
            // reads it, emitting StringHelpers.Reversed(List<string>) — CS1503 behind SPY0908. A
            // recorded fact the emitter does not apply is worse than no fact, so the mark is
            // deliberately withheld for exactly this callee (#1209).
            if (projection.Kind == IterableProjectionKind.StrToList
                && callee is Identifier { Name: BuiltinNames.Reversed })
            {
                continue;
            }

            _semanticInfo.SetIterableProjection(argNode, projection);
        }
    }

    /// <summary>
    /// Decides how an argument type binds in an iterable position: the element type it iterates as
    /// and the projection that makes its C# form an <c>IEnumerable&lt;element&gt;</c> — or
    /// <c>null</c> when the ring does not accept this source there (#1198).
    ///
    /// <para>The two halves are one decision on purpose. Every arm below either needs no lowering
    /// (the source really is an <c>IEnumerable&lt;element&gt;</c>, proven by CLR inspection rather
    /// than assumed) or names the lowering that makes it one. A source with no arm is left unmarked
    /// and stays rejected, which is a deliberate semantic diagnostic rather than the CS1503/CS0411
    /// internal errors that acceptance-without-lowering produced (#1198, #1199).</para>
    /// </summary>
    private IterableArgumentProjection? ClassifyIterableArgument(SemanticType argType)
    {
        // A dict (bare, or `| None` — the C#-interop nullable, whose null throws at .Keys() like
        // Python raises on iterating None) iterates its KEYS: project to d.Keys(). Sharpy's strict
        // `dict[K, V]?` is deliberately not unwrapped by that authority and gets no mark.
        if (_typeInference.GetProjectedDictKeysType(argType)
            is GenericType { TypeArguments.Count: 1 } projectedKeys)
        {
            return new IterableArgumentProjection(
                IterableProjectionKind.DictKeys, projectedKeys.TypeArguments[0]);
        }

        // System.ValueTuple implements no IEnumerable<T>, so a tuple needs the typed-array bridge.
        // Arity is static, so the spread is always well-formed; requiring one element type keeps the
        // array well-typed (a heterogeneous tuple gets no mark and is rejected, not mis-lowered).
        if (argType is TupleType tuple && tuple.ElementTypes.Count > 0)
        {
            var tupleElement = tuple.ElementTypes[0];
            foreach (var elementType in tuple.ElementTypes)
            {
                if (!elementType.Equals(tupleElement))
                    return null;
            }

            return new IterableArgumentProjection(
                IterableProjectionKind.TupleToArray, tupleElement, tuple.ElementTypes.Count);
        }

        // `str` cannot prove itself below — System.String is IEnumerable<char>, not
        // IEnumerable<string> — but Python iterates a string as one-character STRINGS, and
        // Builtins.ListFromStr is exactly that bridge (list(s) has always used it). Kept here, next
        // to the proof it fails, because that is where the question is asked (#1209).
        if (argType == SemanticType.Str)
        {
            return new IterableArgumentProjection(IterableProjectionKind.StrToList, SemanticType.Str);
        }

        // Everything else must prove it already presents as IEnumerable<element> in C# — list, set,
        // frozenset, the dict views, range/iterators, CLR-backed collections.
        if (_typeInference.InferIterableElementType(argType) is { } inferredElement
            && EnumeratesAsInClr(argType, inferredElement))
        {
            return new IterableArgumentProjection(IterableProjectionKind.Direct, inferredElement);
        }

        return null;
    }

    /// <summary>
    /// Whether the C# form of <paramref name="source"/> is assignable to
    /// <c>IEnumerable&lt;element&gt;</c> — the question "will the emitted argument bind?", answered
    /// where CLR inspection belongs (semantic analysis, never the emitter). Returns false whenever
    /// either side has no resolvable CLR type, so acceptance is never granted on a guess.
    /// Reflection here is the established checker-side pattern (<see cref="TryGetClrType"/> and
    /// its 10+ existing uses across TypeChecker/TypeInferenceService); CLAUDE.md's
    /// "CLR inspection belongs to Discovery" rule constrains the EMITTER, which only ever reads
    /// the materialized <see cref="IterableArgumentProjection"/> fact this method helps produce.
    /// </summary>
    private bool EnumeratesAsInClr(SemanticType source, SemanticType element)
    {
        var sourceClr = TryGetClrType(source);
        var elementClr = TryGetClrType(element);
        if (sourceClr == null || elementClr == null)
            return false;

        try
        {
            return typeof(IEnumerable<>).MakeGenericType(elementClr).IsAssignableFrom(sourceClr);
        }
        catch (ArgumentException)
        {
            // MakeGenericType rejects pointer/byref/open element types; treat as "cannot prove".
            return false;
        }
    }

    /// <summary>
    /// Returns the positional argument indices a builtin treats as an iterable-of-keys (so a bare dict
    /// there projects to its keys), or <c>null</c> if the builtin does not consume an iterable this way.
    /// The one place that encodes the ring's position knowledge; <c>dict(d)</c> is deliberately absent
    /// (it copies key/value pairs, not keys).
    /// </summary>
    private static IReadOnlyList<int>? GetBuiltinIterableKeyPositions(string name, int argCount)
    {
        switch (name)
        {
            // Single leading iterable (a trailing start=/default=/key= is a keyword arg or position 1).
            case BuiltinNames.Sorted:
            case BuiltinNames.List:
            case BuiltinNames.Set:
            case BuiltinNames.Tuple:
            case BuiltinNames.Reversed:
            case BuiltinNames.Enumerate:
            case BuiltinNames.Sum:
            case BuiltinNames.Any:
            case BuiltinNames.All:
                return IterablePositionZero;

            // min/max also have a value form (min(a, b, …)); only the single-positional iterable form
            // iterates — two-or-more positional args compare values, not dict keys.
            case BuiltinNames.Min:
            case BuiltinNames.Max:
                return argCount == 1 ? IterablePositionZero : null;

            // zip(a, b, …): every positional argument is an iterable.
            case BuiltinNames.Zip:
                return argCount > 0 ? Enumerable.Range(0, argCount).ToArray() : null;

            // map(f, a, b, …): the mapper is position 0; positions 1.. are iterables.
            case BuiltinNames.Map:
                return argCount >= 2 ? Enumerable.Range(1, argCount - 1).ToArray() : null;

            // filter(pred, it): the iterable is position 1.
            case BuiltinNames.Filter:
                return argCount >= 2 ? IterablePositionOne : null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Returns the positional argument indices a METHOD treats as an iterable-of-keys, or <c>null</c>
    /// when the method does not consume an iterable this way — the member-call twin of
    /// <see cref="GetBuiltinIterableKeyPositions"/> (#1159).
    ///
    /// <para>The one member of the ring today is <c>str.join(iterable)</c>: Python's
    /// <c>", ".join(d)</c> joins the KEYS. The receiver type is checked, so a same-named
    /// <c>join</c> on any other type (a user class, a stdlib module's path join) is untouched.</para>
    /// </summary>
    private IReadOnlyList<int>? GetMemberIterableKeyPositions(MemberAccess memberAccess, int argCount)
    {
        if (memberAccess.Member != BuiltinNames.Join || argCount != 1)
            return null;

        var receiverType = _semanticInfo.GetExpressionType(memberAccess.Object);
        return receiverType == SemanticType.Str ? IterablePositionZero : null;
    }

    /// <summary>
    /// Resolves the function symbol early for constructor inference on arguments.
    /// For simple identifier calls (foo(Some(42))), looks up the function before
    /// checking arguments, allowing _expectedType to be set per-parameter.
    /// </summary>
    /// <param name="callee">The canonical (paren-stripped) callee — see the #1170 contract in
    /// <see cref="AstHelper.UnwrapParenthesized"/>.</param>
    /// <returns>The early-resolved function symbol and parameter offset (0 for functions, 1 for constructors skipping 'self').</returns>
    private (FunctionSymbol? Symbol, int ParamOffset) ResolveEarlyFunctionSymbol(FunctionCall call, Expression callee)
    {
        // Method calls (obj.method(...)): resolve the method on the receiver type so lambda
        // arguments get their parameter types inferred from the receiver-substituted signature
        // (e.g. list[str].sort(key=lambda s: ...) → s: str). See #889.
        if (callee is MemberAccess earlyMa && earlyMa.Object is not SuperExpression)
            return ResolveEarlyMethodSymbol(call, earlyMa);

        if (callee is not Identifier earlyId)
            return (null, 0);

        var earlySymbol = _symbolTable.Lookup(earlyId.Name);
        if (earlySymbol is FunctionSymbol fs && !fs.IsGeneric)
        {
            // Only use early resolution for non-generic, non-overloaded functions.
            // Generic functions need argument types first for inference.
            // Overloaded builtins need argument types for resolution.
            var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(earlyId.Name);
            if (overloads == null || overloads.Count <= 1 || !overloads.Contains(fs))
            {
                return (fs, 0);
            }
        }
        else if (earlySymbol is TypeSymbol ts && !ts.IsGeneric)
        {
            // Constructor call: Person(Some(42)) — look up __init__ for parameter types.
            // __init__ includes 'self' at index 0, but call arguments don't, so offset by 1.
            var initMethod = ts.Methods.FirstOrDefault(m => m.Name == DunderNames.Init);
            if (initMethod != null && !initMethod.IsGeneric)
            {
                return (initMethod, 1); // skip 'self' parameter
            }
        }

        return (null, 0);
    }

    /// <summary>
    /// Resolves a method symbol early for a member-access call (obj.method(...)) so that lambda
    /// arguments can have their parameter types inferred from the method signature. Parameter
    /// types are rewritten with the receiver's generic substitution applied (e.g. T → str for a
    /// <c>list[str]</c> receiver), while method-level type parameters (e.g. TKey in
    /// <c>Sort&lt;TKey&gt;(Func&lt;T, TKey&gt;, bool)</c>) are left unsubstituted — CheckLambda skips
    /// those per-position. Returns a synthesized FunctionSymbol used only for expected-type hints;
    /// the real overload is resolved later. Bails (null) when the method is missing or overloaded
    /// candidates disagree on a parameter type, so we never guess (#889).
    /// </summary>
    private (FunctionSymbol? Symbol, int ParamOffset) ResolveEarlyMethodSymbol(FunctionCall call, MemberAccess memberAccess)
    {
        // Scope this to calls that actually pass a lambda — the only case that benefits from
        // pre-resolved parameter types. This keeps every other method call on its existing
        // expected-type path, avoiding over-constraining context-sensitive args like None() (#889).
        if (!CallHasLambdaArgument(call))
            return (null, 0);

        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object);
        if (objectType is null or UnknownType)
            return (null, 0);

        // Resolve the receiver's TypeSymbol and (for builtin generics) its type arguments.
        TypeSymbol? typeSymbol;
        List<SemanticType>? typeArgs = null;
        if (objectType is UserDefinedType { Symbol: { } udt })
        {
            typeSymbol = udt;
        }
        else
        {
            var (resolved, resolvedArgs) = ResolveBuiltinTypeInfo(objectType);
            typeSymbol = resolved;
            typeArgs = resolvedArgs;
        }

        if (typeSymbol == null)
            return (null, 0);

        // Gather candidate overloads for the method name. Prefer the explicit overload set;
        // otherwise fall back to same-named entries in Methods (how builtin collection methods
        // like list.sort are stored) and finally a single hierarchy lookup.
        var candidates = new List<FunctionSymbol>();
        var overloads = FindMethodOverloadsInHierarchy(typeSymbol, memberAccess.Member);
        if (overloads != null && overloads.Count > 0)
        {
            candidates.AddRange(overloads);
        }
        else
        {
            candidates.AddRange(typeSymbol.Methods.Where(m => m.Name == memberAccess.Member));
            if (candidates.Count == 0)
            {
                var (single, _) = FindMethodInHierarchy(typeSymbol, memberAccess.Member);
                if (single != null)
                    candidates.Add(single);
            }
        }

        if (candidates.Count == 0)
            return (null, 0);

        // Build the receiver substitution (T → str for list[str]). Method-level type
        // parameters (TKey) are not in typeSymbol.TypeParameters and stay unsubstituted.
        Func<SemanticType, SemanticType> substitution = static t => t;
        if (typeArgs != null && typeSymbol.TypeParameters.Count > 0
            && typeSymbol.TypeParameters.Count == typeArgs.Count)
        {
            var capturedSymbol = typeSymbol;
            var capturedArgs = typeArgs;
            substitution = t => SubstituteTypeParameters(t, capturedSymbol.TypeParameters, capturedArgs);
        }

        // Keep only the overloads compatible with this call's keyword-argument names, so a
        // `key=` call doesn't pick an overload that lacks `key` (e.g. list has both
        // Sort(bool reverse) and Sort<TKey>(Func<T,TKey> key, bool reverse)).
        var kwNames = ExtractKeywordArgNames(call);
        if (kwNames != null && kwNames.Count > 0)
        {
            var compatible = candidates
                .Where(c => kwNames.All(n => c.Parameters.Any(p => p.Name == n)))
                .ToList();
            if (compatible.Count > 0)
                candidates = compatible;
        }

        // Only provide expected types when unambiguous: all candidates must agree on the
        // substituted type of each parameter (by name). Otherwise bail — never guess.
        if (candidates.Count > 1 && !OverloadsAgreeOnParameterTypes(candidates, substitution))
            return (null, 0);

        // Use the parameter-richest remaining overload as the expected-type source. Since the
        // candidates agree per name, the superset gives the widest set of expected parameter
        // types (e.g. both `key` and `reverse`) without conflicting with the others.
        var chosen = candidates.OrderByDescending(c => c.Parameters.Count).First();
        var substitutedParameters = chosen.Parameters
            .Select(p => p with { Type = NormalizeExpectedParamType(substitution(p.Type)) })
            .ToList();
        var synthesized = chosen with { Parameters = substitutedParameters };
        return (synthesized, 0);
    }

    /// <summary>
    /// Maps an uninformative early-resolved parameter type to <see cref="SemanticType.Unknown"/>
    /// so CheckCallArguments leaves <c>_expectedType</c> unset. The discovery layer collapses some
    /// generic parameters (e.g. <c>list.append(T)</c>) to <c>object</c>; forcing that as an expected
    /// type would wrongly constrain context-sensitive arguments such as <c>None()</c> (#889).
    /// </summary>
    private static SemanticType NormalizeExpectedParamType(SemanticType type)
    {
        return type is BuiltinType { Name: BuiltinNames.Object } or UserDefinedType { Name: BuiltinNames.Object }
            ? SemanticType.Unknown
            : type;
    }

    /// <summary>
    /// Returns true when any positional or keyword argument of the call is a lambda
    /// (possibly wrapped in parentheses).
    /// </summary>
    private static bool CallHasLambdaArgument(FunctionCall call)
    {
        static bool IsLambda(Expression e) =>
            e is LambdaExpression || (e is Parenthesized p && p.Expression is LambdaExpression);

        foreach (var arg in call.Arguments)
            if (IsLambda(arg))
                return true;
        foreach (var kwarg in call.KeywordArguments)
            if (IsLambda(kwarg.Value))
                return true;
        return false;
    }

    /// <summary>
    /// Returns true when every overload exposes the same substituted parameter type for each
    /// parameter name shared across the candidates. Used to decide whether it is safe to pre-set
    /// expected parameter types for lambda inference without guessing between conflicting overloads.
    /// </summary>
    private static bool OverloadsAgreeOnParameterTypes(
        List<FunctionSymbol> candidates, Func<SemanticType, SemanticType> substitution)
    {
        var seen = new Dictionary<string, SemanticType>();
        foreach (var candidate in candidates)
        {
            foreach (var parameter in candidate.Parameters)
            {
                var substituted = substitution(parameter.Type);
                if (seen.TryGetValue(parameter.Name, out var existing))
                {
                    if (!existing.Equals(substituted))
                        return false;
                }
                else
                {
                    seen[parameter.Name] = substituted;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Validates event invoke restrictions and __init__() call tracking.
    /// Checks that events are only raised from within the declaring class,
    /// tracks super().__init__() calls, and validates self.__init__() usage.
    /// Returns a type if an early return is needed (e.g., event invoke violation),
    /// or null if the dispatcher should continue.
    /// </summary>
    /// <param name="callee">The canonical (paren-stripped) callee — see the #1170 contract in
    /// <see cref="AstHelper.UnwrapParenthesized"/>. <c>(self.__init__)(v)</c> is the same
    /// initializer call as <c>self.__init__(v)</c>.</param>
    private SemanticType? ValidateInitAndEventCalls(FunctionCall call, Expression callee)
    {
        // Check event raise restriction: events can only be invoked from within the declaring class
        if (callee is MemberAccess invokeMA && invokeMA.Member == "invoke")
        {
            // The object of .invoke() might be an event access (e.g., self.on_click?.invoke(...))
            if (_semanticInfo.IsEventAccess(invokeMA.Object))
            {
                // Determine the event's declaring type
                if (invokeMA.Object is MemberAccess eventMA)
                {
                    var eventOwner = ResolveEventOwner(eventMA);
                    if (eventOwner != null && (_currentClass == null || !ReferenceEquals(_currentClass, eventOwner)))
                    {
                        AddError(
                            $"Cannot raise event '{eventMA.Member}' from outside the declaring class",
                            call.LineStart, call.ColumnStart,
                            DiagnosticCodes.Semantic.RaiseEventOutsideClass,
                            call.Span);
                        return SemanticType.Void;
                    }
                }
            }
        }

        // Track super().__init__() calls AFTER validation completes
        // (do this after CheckExpression so the validation doesn't see it as already called)
        if (callee is MemberAccess ma && ma.Object is SuperExpression && ma.Member == DunderNames.Init)
        {
            _superInitCalled = true;

            // Validate keyword-argument names against the base constructor overloads so an
            // unknown kwarg surfaces as SPY0234 here instead of leaking a C# CS1739 (#907).
            // super()/no-parent context errors are already reported by ValidateSuperMemberAccess;
            // a null base yields no candidates, so this stays silent in those cases.
            if (_currentClass != null)
                ValidateInitializerKeywordArguments(call, GetBaseType(_currentClass));
        }

        // Validate self.__init__() is only called inside a constructor
        if (callee is MemberAccess selfInitMa &&
            selfInitMa.Object is Identifier { Name: "self" } &&
            selfInitMa.Member == DunderNames.Init)
        {
            if (_currentMethodName != DunderNames.Init)
            {
                AddError("self.__init__() can only be called inside a constructor (__init__)",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.SelfInitOutsideConstructor,
                    span: call.Span);
            }
            else if (_superInitCalled)
            {
                AddError("Cannot use both super().__init__() and self.__init__() in the same constructor",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.ConflictingConstructorInitializers,
                    span: call.Span);
            }
            else if (_currentClass != null)
            {
                // Context is valid — validate keyword-argument names against this class's own
                // constructor overloads (#907). Skipped above to avoid double-reporting on calls
                // already flagged as out-of-context or conflicting.
                ValidateInitializerKeywordArguments(call, _currentClass);
            }
        }

        return null;
    }

    /// <summary>
    /// Reports SPY0234 for any keyword argument of a <c>super().__init__</c>/<c>self.__init__</c>
    /// call whose name matches no non-self parameter of any candidate constructor overload (#907).
    /// An empty candidate set (CLR base with no enumerated metadata, or no constructors) defers to
    /// the C# compiler and reports nothing.
    /// </summary>
    private void ValidateInitializerKeywordArguments(FunctionCall initCall, TypeSymbol? candidateRoot)
    {
        if (initCall.KeywordArguments.Length == 0 || candidateRoot == null)
            return;

        var candidates = ResolveInitializerConstructorCandidates(candidateRoot);
        if (candidates.Count == 0)
            return;

        foreach (var kwarg in initCall.KeywordArguments)
        {
            var matchesSomeOverload = candidates.Any(
                ctor => ctor.Parameters.Skip(1).Any(p => p.Name == kwarg.Name));
            if (!matchesSomeOverload)
            {
                AddError($"Unknown keyword argument '{kwarg.Name}'",
                    kwarg.LineStart, kwarg.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                    span: kwarg.Span ?? kwarg.Value.Span);
            }
        }
    }

    /// <summary>
    /// Handles None() — empty Optional constructor.
    /// Returns the result type if this is a None() call, or null if the dispatcher should continue.
    /// </summary>
    private SemanticType? CheckNoneConstruction(FunctionCall call, Expression callee)
    {
        if (callee is not NoneLiteral || call.Arguments.Length != 0 || call.KeywordArguments.Length != 0)
            return null;

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

    private List<SemanticType>? TryInferConstructorTypeArgs(
        TypeSymbol typeSymbol, FunctionCall call, List<SemanticType> argTypes)
    {
        var initMethods = typeSymbol.Methods.Where(m => m.Name == DunderNames.Init).ToList();
        if (initMethods.Count != 1)
            return null;

        var initMethod = initMethods[0];
        // Skip 'self' parameter
        var initParams = initMethod.Parameters.Skip(1).ToList();

        // Create a synthetic FunctionSymbol with the class's type parameters
        // and the __init__'s parameters (minus self) so GenericTypeInferenceService
        // can unify the argument types against the parameter types.
        var syntheticFunc = new FunctionSymbol
        {
            Name = typeSymbol.Name,
            Parameters = initParams,
            TypeParameters = typeSymbol.TypeParameters,
        };

        var inferenceResult = _genericInference.InferTypeArguments(syntheticFunc, argTypes);
        if (inferenceResult.Success && inferenceResult.InferredTypes != null)
        {
            _semanticInfo.SetInferredTypeArguments(call, inferenceResult.InferredTypes);
            return inferenceResult.InferredTypes;
        }

        return null;
    }

    private void CheckDeprecatedUsage(Symbol symbol, Expression callSite)
    {
        if (symbol.DeprecationMessage != null)
        {
            _diagnostics.AddWarning(
                $"'{symbol.Name}' is deprecated: {symbol.DeprecationMessage}",
                callSite.LineStart, callSite.ColumnStart, _currentFilePath,
                code: DiagnosticCodes.Validation.DeprecatedUsage,
                phase: CompilerPhase.TypeChecking);
        }
    }

    private static bool IsLiteralStringExpression(Expression expr)
    {
        return expr switch
        {
            StringLiteral => true,
            BinaryOp { Operator: BinaryOperator.Add, Left: var left, Right: var right }
                => IsLiteralStringExpression(left) && IsLiteralStringExpression(right),
            _ => false
        };
    }

    /// <summary>
    /// Type-checks a <c>functools.partial(f, fixed_args..., kw=val, ...)</c> call.
    /// Validates the target is callable, type-checks the fixed arguments against the target's
    /// parameters, and returns a <see cref="FunctionType"/> describing the remaining (unfixed)
    /// parameters. Emits SPY1010 to encourage migration to the idiomatic <c>_</c> placeholder form.
    /// </summary>
    private SemanticType CheckFunctoolsPartialCall(FunctionCall call)
    {
        // Resolve the 'functools' module identifier so SemanticInfo records the binding;
        // LSP find-references and go-to-definition for 'functools' continues to work even
        // though we bypass the normal member-access resolution.
        if (UnwrapParenthesized(call.Function) is MemberAccess memberAccess
            && memberAccess.Object is Identifier moduleId)
        {
            _ = CheckExpression(moduleId);
        }

        // Emit the placeholder-form suggestion.
        AddInfo(
            "Prefer the '_' placeholder syntax over functools.partial for new code; e.g., 'add(5, _)' instead of 'functools.partial(add, 5)'.",
            call.LineStart, call.ColumnStart,
            code: DiagnosticCodes.Info.FunctoolsPartialPlaceholderHint);

        if (call.Arguments.IsDefaultOrEmpty || call.Arguments.Length == 0)
        {
            AddError(
                "functools.partial() requires at least one argument (the target callable)",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: call.Span);
            return SemanticType.Unknown;
        }

        // Validate the target is callable
        var targetExpr = call.Arguments[0];
        var targetType = CheckExpression(targetExpr);

        FunctionType? targetFunctionType = null;
        FunctionSymbol? targetFunctionSymbol = null;

        // Prefer FunctionSymbol when available (preserves parameter names for keyword fixing)
        if (targetExpr is Identifier targetId)
        {
            targetFunctionSymbol = _symbolTable.Lookup(targetId.Name) as FunctionSymbol;
        }

        if (targetType is FunctionType ft)
        {
            targetFunctionType = ft;
        }
        else if (targetFunctionSymbol != null)
        {
            targetFunctionType = BuildFunctionTypeFromSymbol(targetFunctionSymbol);
        }
        else if (targetType is UnknownType)
        {
            // Error recovery — already emitted
            MarkExpressionAsErrorRecovery(call);
            return SemanticType.Unknown;
        }

        if (targetFunctionType == null)
        {
            AddError(
                $"First argument to functools.partial() must be callable; got '{targetType.GetDisplayName()}'",
                targetExpr.LineStart, targetExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedFunction,
                span: targetExpr.Span);
            return SemanticType.Unknown;
        }

        // Type-check fixed positional and keyword args so SemanticInfo records their types
        var fixedPositionalCount = call.Arguments.Length - 1;
        for (var i = 1; i < call.Arguments.Length; i++)
        {
            _ = CheckExpression(call.Arguments[i]);
        }

        var fixedKwargNames = new HashSet<string>(call.KeywordArguments.Length, System.StringComparer.Ordinal);
        foreach (var kwarg in call.KeywordArguments)
        {
            _ = CheckExpression(kwarg.Value);
            fixedKwargNames.Add(kwarg.Name);
        }

        // Compute remaining parameters:
        //   Positional fix consumes leading parameters in declaration order.
        //   Keyword fix removes parameters by name (requires FunctionSymbol for names).
        FunctionType resultType;
        if (targetFunctionSymbol != null)
        {
            resultType = ComputeResultTypeFromSymbol(targetFunctionSymbol, fixedPositionalCount,
                fixedKwargNames, targetExpr);
        }
        else
        {
            // FunctionType has no parameter names — keyword fixing is unsupported in this path
            if (fixedKwargNames.Count > 0)
            {
                AddError(
                    "Keyword arguments to functools.partial() require the target to be a named function; consider using '_' placeholder syntax with explicit keyword arguments instead.",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypeMismatch,
                    span: call.Span);
                return SemanticType.Unknown;
            }

            var computed = FunctoolsPartialHelper.ComputeResultTypeFromFunctionType(
                targetFunctionType, fixedPositionalCount);
            if (computed == null)
            {
                AddError(
                    $"Too many positional arguments to functools.partial(); target accepts at most {targetFunctionType.ParameterTypes.Count} positional parameter(s).",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.WrongArgumentCount,
                    span: call.Span);
                return SemanticType.Unknown;
            }
            resultType = computed;
        }

        _semanticInfo.SetExpressionType(call, resultType);
        return resultType;
    }

    /// <summary>
    /// Builds a <see cref="FunctionType"/> from a <see cref="FunctionSymbol"/>, projecting
    /// each parameter's resolved <see cref="SemanticType"/> into the parameter list.
    /// </summary>
    private static FunctionType BuildFunctionTypeFromSymbol(FunctionSymbol funcSymbol)
    {
        return FunctionType.FromParameters(funcSymbol.Parameters, funcSymbol.ReturnType);
    }

    /// <summary>
    /// Computes the result <see cref="FunctionType"/> for a <c>functools.partial</c> call when
    /// the target is a named <see cref="FunctionSymbol"/>. Positional fixing removes leading
    /// parameters; keyword fixing removes parameters by name.
    /// </summary>
    private FunctionType ComputeResultTypeFromSymbol(FunctionSymbol targetSymbol,
        int fixedPositionalCount, HashSet<string> fixedKwargNames, Expression targetExpr)
    {
        var parameters = targetSymbol.Parameters;

        if (fixedPositionalCount > parameters.Count)
        {
            AddError(
                $"Too many positional arguments to functools.partial(); '{targetSymbol.Name}' accepts at most {parameters.Count} positional parameter(s).",
                targetExpr.LineStart, targetExpr.ColumnStart,
                code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: targetExpr.Span);
            fixedPositionalCount = parameters.Count;
        }

        var knownNames = new HashSet<string>(parameters.Count, System.StringComparer.Ordinal);
        for (var i = 0; i < parameters.Count; i++)
        {
            knownNames.Add(parameters[i].Name);
        }
        foreach (var kwName in fixedKwargNames)
        {
            if (!knownNames.Contains(kwName))
            {
                AddError(
                    $"functools.partial(): '{targetSymbol.Name}' has no parameter named '{kwName}'",
                    targetExpr.LineStart, targetExpr.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                    span: targetExpr.Span);
            }
        }

        var remaining = new List<SemanticType>();
        var optionalCount = 0;
        for (var i = fixedPositionalCount; i < parameters.Count; i++)
        {
            var p = parameters[i];
            if (fixedKwargNames.Contains(p.Name))
            {
                continue;
            }
            remaining.Add(p.Type);
            if (p.HasDefault)
            {
                optionalCount++;
            }
        }

        return new FunctionType
        {
            ParameterTypes = remaining,
            ReturnType = targetSymbol.ReturnType,
            OptionalParameterCount = optionalCount,
        };
    }

    /// <summary>
    /// The call's direct argument expressions, unwrapped through parentheses, for
    /// <c>_currentCallArguments</c>. Spread values count: <c>f(*args)</c> passes the spread operand
    /// itself into the argument position (#1182).
    /// </summary>
    private static HashSet<Expression> DirectArgumentSetOf(FunctionCall call)
    {
        var arguments = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        foreach (var argument in call.Arguments)
        {
            var unwrapped = UnwrapParenthesized(argument);
            arguments.Add(unwrapped);
            if (unwrapped is SpreadElement spread)
                arguments.Add(UnwrapParenthesized(spread.Value));
        }

        foreach (var keywordArgument in call.KeywordArguments)
            arguments.Add(UnwrapParenthesized(keywordArgument.Value));

        return arguments;
    }

    private void MarkTypeReferenceArguments(FunctionCall call)
    {
        foreach (var arg in call.Arguments)
        {
            if (IsTypeNameExpression(arg))
                _semanticInfo.MarkTypeReference(arg);
        }

        foreach (var kwarg in call.KeywordArguments)
        {
            if (IsTypeNameExpression(kwarg.Value))
                _semanticInfo.MarkTypeReference(kwarg.Value);
        }
    }

    /// <summary>
    /// Marks a <c>defaultdict(list)</c> / <c>defaultdict[str, list[int]](list)</c> first argument that
    /// names a type used as a zero-argument factory callable. The DefaultDict constructor takes
    /// <c>Func&lt;TValue&gt;</c>, so codegen wraps such an argument in <c>() =&gt; new TValue()</c> —
    /// but WHETHER the argument is a factory name is a semantic question (the name may resolve as a
    /// TypeSymbol, as a builtin collection function, or only through the wrapper-collection special
    /// cases), so it is decided here and read as a fact by the emitter (#1175, Critical Rule 2).
    /// </summary>
    private void MarkTypeFactoryArguments(FunctionCall call, Expression callee)
    {
        if (call.Arguments.Length == 0 || call.Arguments[0] is not Identifier factoryName)
            return;

        // The callee names defaultdict either bare or with explicit type arguments.
        var calleeName = callee switch
        {
            Identifier id => id.Name,
            IndexAccess { Object: Identifier typeId } => typeId.Name,
            _ => null,
        };
        if (!string.Equals(calleeName, BuiltinNames.DefaultDict, StringComparison.OrdinalIgnoreCase))
            return;

        if (_symbolTable.Lookup(factoryName.Name) is TypeSymbol
            || _semanticInfo.GetIdentifierSymbol(factoryName) is TypeSymbol
            || Discovery.ClrTypeBridge.SpecialCases.TryGetWrapperCollectionName(factoryName.Name) != null
            || _symbolTable.BuiltinRegistry.GetFunction(factoryName.Name) != null)
        {
            _semanticInfo.MarkTypeFactoryArgument(factoryName);
        }
    }

    private bool IsTypeNameExpression(Expression expr)
    {
        if (expr is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);
            return symbol is TypeSymbol;
        }

        return false;
    }

}
