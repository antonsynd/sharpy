using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Function calls, overload resolution, argument validation
/// </summary>
internal partial class TypeChecker
{
    private SemanticType CheckFunctionCall(FunctionCall call)
    {
        // Deferred callable-reference selections (#1589) are scoped to this call: the entries its
        // arguments record sit at or past the watermark pushed here, a nested call pushes its own, and
        // whatever this call's dispatch did not resolve is refused before the scope closes — nothing
        // leaks into the next call's inference.
        SemanticType result;
        using (ScopedValue.Push(ref _pendingOverloadWatermark, _pendingOverloadSelections.Count))
        {
            result = CheckFunctionCallCore(call);
            RefusePendingOverloadSelections();
        }

        return result;
    }

    private SemanticType CheckFunctionCallCore(FunctionCall call)
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
        var isTypeTest = (callee is Identifier { Name: BuiltinNames.Isinstance }
            || (callee is MemberAccess { IsMemberBacktickEscaped: false, Member: BuiltinNames.Isinstance } qualifiedIsinstanceCallee
                && _semanticInfo.GetExpressionType(qualifiedIsinstanceCallee.Object) is ModuleType isinstanceCalleeModule
                && IsBuiltinsModule(isinstanceCalleeModule.Symbol)))
            && call.Arguments.Length > 0;
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

        // Resolve the deferred callable-reference selections this call's arguments recorded (#1589),
        // here — after every argument is checked and before any dispatch path — so a builtin whose
        // return type is read off its callable argument (map, sorted) sees the selected signature
        // rather than the still-open expected type. The substitutions come from the NON-deferred
        // arguments against the callee's formals: the early symbol's when there is one (only a
        // generic one has anything to bind), else the callee's own function type when it is open —
        // the shape a generic builtin like `map` has, which ResolveEarlyFunctionSymbol leaves null.
        // Entries this site cannot bind stay for the inference dispatch below; whatever that leaves
        // is refused when the call's scope closes.
        if (HasPendingOverloadSelections)
        {
            Func<int, SemanticType?>? formalAt = null;
            if (earlyFuncSymbol != null)
            {
                if (earlyFuncSymbol.IsGeneric)
                {
                    formalAt = i => i + earlyParamOffset < earlyFuncSymbol.Parameters.Count
                        ? earlyFuncSymbol.Parameters[i + earlyParamOffset].Type
                        : null;
                }
            }
            else if (calleeFunctionType != null && ContainsTypeParameter(calleeFunctionType))
            {
                formalAt = i => i < calleeFunctionType.ParameterTypes.Count
                    ? calleeFunctionType.ParameterTypes[i]
                    : null;
            }

            if (formalAt != null)
            {
                var substitutions = InferSubstitutionsFromArguments(formalAt, argTypes);
                if (substitutions.Count > 0)
                    ResolvePendingOverloadSelections(substitutions, argTypes);
            }
        }

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

            if (shadowsBuiltin && _semanticInfo != null)
                _semanticInfo.SetCalleeRouting(call, CalleeRouting.UserSymbol);

            // `from builtins import len as blen` binds the alias's SPELLING to the registry's own
            // symbol. The inference below is keyed by the builtin's NAME, which the alias is not, so
            // dispatch on what the symbol IS. Without this the call falls through to ordinary
            // overload ranking, where Len(ICollection)/Len(ISized)/Len(object) all match a
            // list[int] equally well and the program gets SPY0353 (#1383).
            var builtinName = symbol?.BuiltinAliasOf?.Name ?? id.Name;

            // Data-driven builtin function return type inference (len, hash, reversed, sorted, min, max)
            if (!id.IsNameBacktickEscaped && !shadowsBuiltin)
            {
                var builtinReturn = BuiltinReturnTypeInference.InferReturnType(
                    builtinName, EffectiveMinMaxArgumentTypes(call, argTypes), _typeInference);
                if (builtinReturn != null)
                {
                    if (builtinReturn is UnknownType
                        && builtinName is BuiltinNames.Min or BuiltinNames.Max
                        && argTypes.Count >= 2)
                    {
                        ReportMinMaxPromotionFailure(call, builtinName, argTypes);
                        return SemanticType.Unknown;
                    }
                    RecordMinMaxTypeArguments(call, builtinName, argTypes, builtinReturn);
                    ValidateMinMaxValueFormKey(builtinName, call, argTypes, kwargTypes);
                    return builtinReturn;
                }
            }

            // Type alias transparency (#1527): expand the alias and route the call through
            // the target type, so `type bint = int; bint("42")` takes int("42")'s path.
            if (symbol is TypeAliasSymbol calleeAlias && calleeAlias.TypeAnnotation != null)
            {
                var expanded = _typeResolver.ResolveTypeAnnotation(calleeAlias.TypeAnnotation);
                TypeSymbol? aliasTarget = expanded switch
                {
                    BuiltinType bt => _symbolTable.BuiltinRegistry.GetType(bt.Name),
                    UserDefinedType { Symbol: TypeSymbol ts } => ts,
                    _ => null
                };

                if (aliasTarget != null)
                {
                    symbol = aliasTarget;
                    builtinName = aliasTarget.BuiltinAliasOf?.Name ?? aliasTarget.Name;
                    if (_symbolTable.BuiltinRegistry.IsBuiltinSymbol(aliasTarget) && _semanticInfo != null)
                        _semanticInfo.SetCalleeRouting(call, CalleeRouting.Builtin);
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
                var primitiveOverloads = PrimitiveConversionResolver.ResolveOverloads(typeSymbol, _symbolTable.BuiltinRegistry);
                if (primitiveOverloads != null && primitiveOverloads.Count > 0)
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
                    MarkExpressionAsErrorRecovery(call,
                        ErrorRecoveryReason.Propagated("the callee binding's type"));
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
                    var callableResult = TryResolveCallableObject(calleeType, call, argTypes, kwargTypes, totalArgCount);
                    if (callableResult != null)
                        return callableResult;

                    // SPY0230 (not callable), not SPY0201 (undefined function): the name IS bound —
                    // its type simply has no __call__ and is not a function or delegate (#1672).
                    AddError($"'{id.Name}' is not callable (type: {calleeType.GetDisplayName()})",
                        call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NotCallable,
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
            // `builtins.X(...)` is decided FIRST, and decided by the registry: the qualified
            // spelling means exactly what the bare spelling means, so it goes to the bare arm's
            // authorities rather than to the module machinery below (#1322).
            var builtinsQualified = CheckBuiltinsQualifiedCall(
                memberAccessCall, call, argTypes, kwargTypes, totalArgCount,
                isNullConditionalCall, isOptionalNullConditional);
            if (builtinsQualified != null)
                return builtinsQualified;

            if (TryResolveTypeSymbolFromMemberAccess(memberAccessCall, out var resolvedAlias) is { } moduleTypeSymbol)
            {
                if (resolvedAlias != null
                    && _symbolTable.BuiltinRegistry.IsBuiltinSymbol(moduleTypeSymbol))
                {
                    _semanticInfo.SetCalleeRouting(call, CalleeRouting.Builtin);
                    _semanticInfo.SetCalleeAliasTargetName(call,
                        resolvedAlias.TypeAnnotation?.Name ?? moduleTypeSymbol.Name);
                }
                return CheckConstructorCall(call, moduleTypeSymbol, argTypes, kwargTypes, totalArgCount);
            }

            funcSymbol = ResolveFunctionSymbolFromMemberAccess(memberAccessCall);

            // Try module function overloads (e.g., os.path.join with different arities)
            {
                var moduleOverloadResult = ResolveModuleFunctionOverload(
                    memberAccessCall, argTypes, kwargTypes, totalArgCount, call,
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

            // One shared receiver resolution for the two recording blocks below — the resolution
            // chain (GetExpressionType → UnwrapCallTarget → UserDefinedType-or-builtin) must stay
            // identical for both, so it is derived once.
            {
                var rawObjType = _semanticInfo.GetExpressionType(memberAccessCall.Object);
                if (rawObjType != null)
                {
                    var objType = UnwrapCallTarget(rawObjType);
                    TypeSymbol? receiverSymbol = objType is UserDefinedType { Symbol: { } uds }
                        ? uds
                        : ResolveBuiltinTypeInfo(objType).TypeSymbol;

                    if (receiverSymbol != null)
                    {
                        // Record the call target for single (non-overloaded) instance-method calls.
                        // ResolveUserMethodOverload declines these (overloads.Count <= 1); the call
                        // still validates through CheckLambdaCall below, but the seam never saw it —
                        // so @deprecated, @must_use, LSP go-to-definition, and codegen's
                        // GetCallTarget were all blind (#1537). Dunders are excluded: __init__
                        // belongs to the constructor-delegation route (#1536), and recording it here
                        // would silently change that route's semantics.
                        if (!DunderDetector.IsDunderMethod(memberAccessCall.Member))
                        {
                            var (method, _) = TypeHierarchyService.FindMethod(
                                receiverSymbol, memberAccessCall.Member, SemanticBinding);
                            if (method != null)
                            {
                                RecordResolvedCallTarget(call, method);

                                // The resolved symbol's NAMED parameters are in hand here, and the
                                // validation route below (CheckLambdaCall) sees only a FunctionType
                                // — positional types with no names — so kwarg names/types validate
                                // at this seam (#1591). Self is skipped by the same rule
                                // FunctionType.FromParameters' skipLeading applied when the member
                                // seam typed the callee. Guarded on funcSymbol: a call that resolves
                                // a symbol below validates kwargs in ValidateFunctionSymbolCall, and
                                // reporting here too would double it.
                                if (funcSymbol == null && call.KeywordArguments.Length > 0)
                                {
                                    var selfOffset = method.Parameters.Count > 0
                                        && method.Parameters[0].Name == PythonNames.Self ? 1 : 0;
                                    ValidateKeywordArguments(call,
                                        method.Parameters.Skip(selfOffset).ToList(),
                                        argTypes.Count, kwargTypes,
                                        clrParameterNames: method.ClrMethodName != null);
                                }
                            }
                        }

                        // Record default-interface dispatch and CLR-property-call lowerings for the
                        // emitter (#1519). Both run after resolution to avoid re-deriving hierarchy
                        // facts at emit time.
                        var defaultIface = TryGetDefaultMethodInterfaceName(
                            receiverSymbol, memberAccessCall.Member);
                        if (defaultIface != null)
                            _semanticInfo.SetDefaultInterfaceDispatch(call, defaultIface);

                        if (call.Arguments.Length == 0 && call.KeywordArguments.Length == 0
                            && IsClrPropertyOnType(receiverSymbol, memberAccessCall.Member))
                            _semanticInfo.SetClrPropertyCallLowering(call);
                    }
                }
            }

            // Nothing above typed this call, so it is on the name-only interop channel: the emitter
            // writes it verbatim and Roslyn performs the only binding check it ever gets — which is
            // how `xs.add("not an int")` came back as CS1503 behind SPY0908, the compiler reporting
            // its own bug for a user's type error (#1290). Runs last, and only for what the
            // resolutions above declined, so every call one of them owns keeps that owner's check.
            if (funcSymbol == null && calleeType is UnknownType)
            {
                // A zero-arg call onto a CLR PROPERTY (`s.count()`, `DateTime.now()`) is legal Sharpy
                // and lowers to the property access itself. The member seam resolved the property but
                // declined to type the callee (a property is not callable), so the type belongs here,
                // on the call node — with the collapse recorded for the emitter. One decision for
                // every receiver kind, instance and static alike (#1640).
                if (ClrPropertyCallType(call, memberAccessCall) is { } propertyCallType)
                    return propertyCallType;

                CheckClrInstanceMethodCall(call, memberAccessCall, argTypes, kwargTypes);

                // Same channel, the other half of the same silence: nothing typed this call either, so
                // its value was Unknown and assignable to anything. Reflection types it when the call's
                // arity selects exactly one overload, which is also where a CLR char becomes the str
                // Sharpy means by it (#1291).
                if (BclCallTypeOnBuiltinReceiver(call, memberAccessCall, argTypes) is { } bclCallType)
                    return bclCallType;

                // The same silence on a STATIC CLR receiver, for the argument direction the char
                // family had not covered: a `str` bound to a reflected `char` parameter (#1402).
                if (ClrStaticCallType(call, memberAccessCall, argTypes) is { } staticCallType)
                    return staticCallType;

            }
        }

        // If we have a FunctionSymbol, use it for validation (supports default parameters)
        if (funcSymbol != null)
        {
            return ValidateFunctionSymbolCall(call, funcSymbol, argTypes, kwargTypes, totalArgCount,
                isNullConditionalCall, isOptionalNullConditional);
        }

        // Fallback to FunctionType validation (handles defaults via FunctionType.OptionalParameterCount).
        // Use the already-computed calleeType to avoid re-evaluating call.Function
        // (which causes double validation, e.g., super().__init__() being flagged as duplicate).
        if (calleeType is FunctionType ft)
        {
            return CheckLambdaCall(call, callee, ft, argTypes, totalArgCount,
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

        // Try __call__ dispatch before giving up
        {
            var callableResult = TryResolveCallableObject(calleeType, call, argTypes, kwargTypes, totalArgCount);
            if (callableResult != null)
                return callableResult;
        }

        // If callee type is Unknown, this is error recovery from a sub-expression.
        // Explicitly mark the FunctionCall as error recovery as a safety net — transitive
        // tracking in CheckExpression usually handles this, but some paths (e.g., property
        // type resolution) can return Unknown without marking or emitting an error.
        // Otherwise, the callee evaluated to a non-callable type — emit an error.
        if (calleeType is UnknownType)
        {
            MarkExpressionAsErrorRecovery(call,
                ErrorRecoveryReason.Propagated("the callee's type"));
        }
        else
        {
            // SPY0230, the twin of the identifier-callee arm above: the expression evaluated to a
            // type that is not callable, which is not the same thing as an undefined function.
            AddError($"Expression of type '{calleeType.GetDisplayName()}' is not callable",
                call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.NotCallable,
                span: call.Function.Span);
        }
        return SemanticType.Unknown;
    }

    // ============================================================
    // isinstance type-operand classification (#1207, #1213, #1532)
    // ============================================================

    /// <summary>
    /// Decides what the TYPE OPERAND of an <c>isinstance(x, T)</c> call denotes, and records the
    /// resulting type test on the operand node for codegen and for narrowing to read.
    /// <para>
    /// The second argument is a <b>type position</b>: <c>(A, B)</c> denotes <c>tuple[A, B]</c>, not
    /// Python's any-of check (#1532). Classification is total — every second argument either resolves
    /// as a type or is refused with a diagnostic; nothing reaches codegen unbound.
    /// </para>
    /// <para>
    /// Open generics are rejected because a successful test would narrow to <c>Box[T]</c> for an
    /// unknown T, which is not spellable (SPY0345, #1207).
    /// </para>
    /// </summary>
    private void ClassifyTypeTestOperand(FunctionCall call, Expression callee, List<SemanticType> argTypes)
    {
        var isQualifiedIsinstance = callee is MemberAccess { IsMemberBacktickEscaped: false, Member: BuiltinNames.Isinstance } qualifiedIsinstance
            && _semanticInfo.GetExpressionType(qualifiedIsinstance.Object) is ModuleType isinstanceModule
            && IsBuiltinsModule(isinstanceModule.Symbol);
        var isinstanceId = callee as Identifier;
        if (!isQualifiedIsinstance && isinstanceId is not { Name: BuiltinNames.Isinstance })
            return;
        if (call.Arguments.Length != 2 || call.KeywordArguments.Length != 0)
            return;

        if (!isQualifiedIsinstance)
        {
            var resolvedCallee = _symbolTable.Lookup(isinstanceId!.Name);
            var builtinIsinstance = _symbolTable.BuiltinRegistry.GetFunctionOverloads(BuiltinNames.Isinstance);
            if (resolvedCallee is not FunctionSymbol calleeFunction
                || builtinIsinstance == null
                || !builtinIsinstance.Contains(calleeFunction))
            {
                return;
            }
        }

        var operandNode = call.Arguments[1];
        var subjectType = argTypes.Count > 0 ? argTypes[0] : null;

        // The operand is a TYPE position: whatever value-selection its expression check deferred
        // (a primitive spelling with arity-divergent conversion overloads) is never a value here.
        DiscardPendingOverloadSelectionFor(UnwrapParenthesized(operandNode));
        if (UnwrapParenthesized(operandNode) is TupleLiteral operandTuple)
        {
            foreach (var element in operandTuple.Elements)
                DiscardPendingOverloadSelectionFor(UnwrapParenthesized(element));
        }

        // (A, B) denotes tuple[A, B] — the second argument is a type position (#1532).
        // A parenthesized single (T) denotes tuple[T] per type_annotation_shorthand.md.
        var rawOperand = operandNode is Parenthesized paren ? paren.Expression : operandNode;
        if (rawOperand is TupleLiteral tuple)
        {
            ClassifyTupleTypeTestOperand(call, operandNode, tuple.Elements, subjectType);
            return;
        }

        // A parenthesized single expression that is NOT a TupleLiteral: (T) means tuple[T].
        if (operandNode is Parenthesized singleParen)
        {
            ClassifyTupleTypeTestOperand(
                call, operandNode, ImmutableArray.Create(singleParen.Expression), subjectType);
            return;
        }

        ClassifyTypeTestExpressionOperand(call, operandNode, subjectType);
    }

    /// <summary>
    /// Classifies a tuple-shaped type-test operand as a structural tuple type: the elements of a
    /// <c>TupleLiteral</c>, or a parenthesized single expression as the 1-tuple <c>tuple[T]</c>
    /// (per type_annotation_shorthand). Each element must resolve as a type; the result is
    /// <c>tuple[A, B, ...]</c>.
    /// </summary>
    private void ClassifyTupleTypeTestOperand(
        FunctionCall call, Expression operandNode, IReadOnlyList<Expression> elements,
        SemanticType? subjectType)
    {
        var elementTypes = new List<SemanticType>();
        foreach (var element in elements)
        {
            var resolved = TryResolveExpressionAsType(element, TypeOperandShapes.TypeTestOperand);
            if (resolved == null)
            {
                AddError(
                    "isinstance()'s second argument is a type position, but this expression "
                    + "is not a type. For Python's any-of check, write "
                    + "`isinstance(x, A) or isinstance(x, B)`; "
                    + "`(A, B)` denotes the tuple type `tuple[A, B]`.",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.MultiTypeTypeTest,
                    span: call.Span);
                return;
            }

            if (resolved is UserDefinedType { Symbol: { IsGeneric: true } openDef })
            {
                ClassifyResolvedTypeOperand(
                    call, element, element, openDef, DescribeTypeOperand(element), subjectType);
                return;
            }

            elementTypes.Add(resolved);
        }

        var tupleType = new TupleType { ElementTypes = elementTypes };

        if (subjectType != null
            && subjectType is not UnknownType
            && !IsObjectType(subjectType)
            && subjectType is not TupleType)
        {
            AddError(
                $"isinstance() tuple type test is statically impossible: the scrutinee's type "
                + $"'{subjectType.GetDisplayName()}' is never a tuple. "
                + "For Python's any-of check, write "
                + "`isinstance(x, A) or isinstance(x, B)`; "
                + "`(A, B)` denotes the tuple type `tuple[A, B]`.",
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.Validation.ImpossibleTupleTypeTest,
                span: call.Span);
            return;
        }

        _semanticInfo.SetTypeTestLowering(operandNode, new TypeTestLowering(TypeTestLoweringKind.ClosedType, tupleType));
    }

    /// <summary>
    /// Classifies one expression-shaped type operand — a non-tuple <c>isinstance</c> second argument.
    /// Records the decision on <paramref name="operandNode"/> so the emitter applies it verbatim.
    /// </summary>
    private void ClassifyTypeTestExpressionOperand(
        FunctionCall call, Expression operandNode, SemanticType? subjectType)
    {
        var typeOperand = UnwrapParenthesized(operandNode);

        // A bare name is the only shape that can be an OPEN generic, so it is the only one that needs
        // the vector-filling rule; every other shape either names its type arguments or has none.
        if (typeOperand is Identifier typeId)
        {
            ClassifyBareTypeNameOperand(call, operandNode, typeId, subjectType);
            return;
        }

        // Every other shape already names its type arguments (or has none): a qualified name
        // (`mod.Type`, #903) or a closed generic spelling (`Box[int]`). Read through the single
        // expression-as-type resolver the generic-construction path uses, so the two cannot drift
        // (#1257).
        var resolved = TryResolveExpressionAsType(typeOperand, TypeOperandShapes.TypeTestOperand);
        if (resolved == null)
        {
            AddError(
                "isinstance()'s second argument is a type position, but this expression "
                + "is not a type. For Python's any-of check, write "
                + "`isinstance(x, A) or isinstance(x, B)`; "
                + "`(A, B)` denotes the tuple type `tuple[A, B]`.",
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.Semantic.MultiTypeTypeTest,
                span: call.Span);
            return;
        }

        // ...with one exception the "already names its arguments" reading gets wrong: a QUALIFIED
        // name whose declaration is generic (`mod.Box`) names no more than a bare `Box` does. The
        // qualified arm above reads the recorded UserDefinedType and has no counterpart to the bare
        // arm's "a bare generic name denotes nothing" rule, so the open name was recorded as a
        // CLOSED test and reached Roslyn as `ModA.Bag<T>` — CS0305 behind SPY0908 — while the bare
        // spelling drew SPY0345. Same declaration, same rule, whatever the qualifier (#1411).
        if (resolved is UserDefinedType { Symbol: { IsGeneric: true } openDefinition })
        {
            ClassifyResolvedTypeOperand(
                call, operandNode, typeOperand, openDefinition, DescribeTypeOperand(typeOperand),
                subjectType);
            return;
        }

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
        // The escape decides the namespace both ways (#1325): the primitive claim below belongs
        // to the bare spelling only — `isinstance(x, `int`)` tests the user's escaped class,
        // never the builtin, while a bare `int` stays the builtin even when an escaped class
        // shadows it. Symbol acceptance is by identity: escaped never binds the registry's own
        // symbol, bare never binds an escape-declared one, quoting a bare-declared import stands.
        if (!typeId.IsNameBacktickEscaped && ResolveBuiltinPrimitiveTypeName(typeId.Name) is { } primitive)
        {
            _semanticInfo.SetTypeTestLowering(operandNode, new TypeTestLowering(TypeTestLoweringKind.ClosedType, primitive));
            return;
        }

        var operandSymbol = _symbolTable.Lookup(typeId.Name);
        if (operandSymbol != null)
        {
            if (typeId.IsNameBacktickEscaped && _symbolTable.BuiltinRegistry.IsBuiltinSymbol(operandSymbol))
                operandSymbol = null;
            else if (!typeId.IsNameBacktickEscaped && operandSymbol.IsNameBacktickEscaped)
                operandSymbol = _symbolTable.BuiltinRegistry.GetType(typeId.Name);
        }

        if (operandSymbol is not TypeSymbol typeSymbol)
            return;

        ClassifyResolvedTypeOperand(call, operandNode, typeId, typeSymbol, typeId.Name, subjectType);
    }

    /// <summary>
    /// The three-outcome rule for an <c>isinstance</c> operand once its declaration is in hand,
    /// shared by the bare spelling (<c>Box</c>) and the module-qualified one (<c>mod.Box</c>) so the
    /// qualifier cannot buy an exemption from a rule that is about the TYPE (#1411).
    /// </summary>
    /// <param name="operandNode">The node the lowering is keyed on (grouping parentheses included,
    /// since that is the node the emitter looks the decision up by).</param>
    /// <param name="reportOn">The unwrapped operand a refusal points at.</param>
    /// <param name="writtenName">
    /// The operand exactly as the user typed it — what the refusal message must echo, since it is
    /// the text they have to retype. This is why the shared body takes a name rather than reading
    /// <c>typeSymbol.Name</c>, which for a qualified spelling would print a name that does not
    /// resolve at this site.
    /// </param>
    private void ClassifyResolvedTypeOperand(
        FunctionCall call, Expression operandNode, Node reportOn, TypeSymbol typeSymbol,
        string writtenName, SemanticType? subjectType)
    {
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

        // Shares SPY0345's body with the annotation-shaped sites (#1235); only the site noun and the
        // example spelling differ, and the example here is the whole call because that is what the
        // reader has to retype.
        ReportOpenGenericTypeOperand(
            reportOn, writtenName, siteNoun: "call",
            remedy: ClosedSpellingRemedy(
                $"{BuiltinNames.Isinstance}(..., {writtenName}[{OpenGenericPlaceholders(typeSymbol)}])"),
            fallbackSpan: call.Span);
    }

    /// <summary>
    /// Fills a generic type's argument vector from the type test's SUBJECT — <c>isinstance(b, Box)</c>
    /// where <c>b: Box[int]</c> tests <c>Box[int]</c>. Consults the subject's own instantiation
    /// first; on miss, walks the inheritance chain via <see cref="GenericInstantiationWalker"/>
    /// so that a subject typed by a generic DERIVED class fills through substitution (#1308).
    /// </summary>
    private GenericType? FillTypeArgumentsFromSubject(TypeSymbol typeSymbol, SemanticType? subjectType)
    {
        var subject = subjectType switch
        {
            OptionalType optional => optional.UnderlyingType,
            NullableType nullable => nullable.UnderlyingType,
            _ => subjectType
        };

        if (subject is not GenericType generic)
            return null;

        // Identity match — the subject is already the target type.
        //
        // TODO(#1448): identity only decides here when the subject carries a resolvable definition.
        // A subject without one makes ResolveDefinition fall through to a name lookup that finds a
        // same-named LOCAL declaration and compares it against itself, so `isinstance(x, Bag)` emits
        // a test against the wrong Bag. The fix is upstream, where the annotation is resolved; this
        // comparison is already strict.
        //
        // The IN-SOURCE-SET half of this was #1410, closed by the Exports-copy elimination
        // (a804d4f5e): a module-qualified annotation now resolves to the compilation's own symbol,
        // which IS the definition. What remains is the extraction path — ModuleLoader's
        // ConvertTypeAnnotationToSemanticType (ModuleLoader.cs:838) builds a definition-less
        // GenericType for every variable, field, parameter and return annotation of a module that is
        // imported but not compiled, and keeps the annotation's name verbatim (#1448).
        if (generic.TypeArguments.Count == typeSymbol.TypeParameters.Count
            && NamesSameDeclaration(generic, typeSymbol))
        {
            return generic;
        }

        // Walk the inheritance chain to find the target as a supertype (#1308)
        //
        // TODO(#1412): this walk has no erasable-collection check, and #912's erasure decision is
        // only kept intact by the fact that nothing can reach it. The hazard would be a scrutinee
        // whose SUPERTYPE is an erasable collection: it would fill through inheritance where the
        // pre-#1308 code erased. The cell that would show it — `class MyList[T](list[T])` with a
        // `MyList[int]` scrutinee against `case list():` — cannot be written, because SPY0325 makes
        // a Sharpy class hand-implement the entire IList/IDeepCopyable surface to declare that base,
        // and the CLR-backed spellings are refused earlier by the pattern-compatibility check. For
        // the pattern to be compatible at all the scrutinee must already BE a list, which the
        // identity match above claims before the walk is reached.
        //
        // The trigger that makes this live: anything letting a Sharpy class declare an erasable
        // collection as its base — a synthesized or inherited IList implementation, or relaxing
        // SPY0325 for builtin-collection bases. If that lands, add a BuiltinNames.IsErasableCollection
        // check here (the shape the boolean type-test sites use in ClassifyBareTypeNameOperand and
        // in the annotation operand path, both of which erase BEFORE the fill and are unaffected —
        // only this pattern site reaches the fill with an erasable target) and pin the
        // `MyList[int]` vs `case list():` cell at the same time.
        //
        // No guard is written now on purpose: it would be unreachable code that no test can
        // exercise and no mutation can prove, which is worse than the coupling it documents
        // (Batch E's measured rationale). #1412 is the tripwire — parked and closed by the
        // 2026-08-13 ruling; any change that lets a Sharpy class declare an erasable-collection
        // base re-opens it and adds the guard with the MyList[int] vs case list(): cell pinned.
        foreach (var supertype in GenericInstantiationWalker.EnumerateSupertypes(
            generic, _symbolTable, SemanticBinding, _typeResolver))
        {
            if (TypeHierarchyService.IsSameType(supertype.Definition, typeSymbol))
            {
                return new GenericType
                {
                    Name = typeSymbol.Name,
                    TypeArguments = supertype.TypeArguments.ToList(),
                    GenericDefinition = typeSymbol
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Maps a builtin primitive type NAME to its singleton, or null when the name is not one.
    /// Shared by every arm of the classifier so <c>int</c> means the same thing bare, module-qualified,
    /// and as a type argument.
    /// </summary>
    private static SemanticType? ResolveBuiltinPrimitiveTypeName(string name)
    {
        var direct = name switch
        {
            BuiltinNames.Int => SemanticType.Int,
            BuiltinNames.Long => SemanticType.Long,
            BuiltinNames.Float => SemanticType.Float,
            BuiltinNames.Float32 => SemanticType.Float32,
            BuiltinNames.Decimal => SemanticType.Decimal,
            BuiltinNames.Double => SemanticType.Double,
            BuiltinNames.Bool => SemanticType.Bool,
            BuiltinNames.Str => SemanticType.Str,
            _ => (SemanticType?)null
        };
        if (direct != null)
            return direct;

        var info = PrimitiveCatalog.GetByName(name);
        if (info != null)
            return TypeResolver.ClrTypeToSemanticType(info.ClrType);
        return null;
    }

    /// <summary>
    /// Message rendering only: best-effort textual rendering of a type-position expression for
    /// the multi-type diagnostic. The default is a generic placeholder; no semantic decision
    /// keys on this switch.
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
                        WrittenTypeParameterBinding(genericTypeSymbol, typeArgs),
                        clrParameterNames: initMethods[0].ClrMethodName != null);

                    CheckDeprecatedUsage(initMethods[0], call);
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

                    var resolvedInit = ValidateSoleArityMatchingOverload(call, initMethods, argTypes, kwargTypes,
                        totalArgCount, WrittenTypeParameterBinding(genericTypeSymbol, typeArgs));
                    if (resolvedInit != null)
                        CheckDeprecatedUsage(resolvedInit, call);
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

            // A closed signature over a string receiver is a signature over CHARS — `s.first()` is
            // Enumerable.First<char>. Sharpy has no char, so the same projection the builtin-receiver
            // seams apply runs here: without it `x: str = s.first()` was refused as char-vs-str, a
            // refusal for something that is a one-character str in every sense the user means (#1291).
            // The sequence shape projects too, through the rule that owns it: converting a CLR
            // SEQUENCE'S element is the "collection whose element the bridge RE-REPRESENTS" case of
            // #1251's materialization, so ProjectClrCharSequence states the per-element conversion
            // that makes such a collection materializable rather than lowering it from here (#1401).
            // The two projections are exclusive by shape, so the composition is just "scalar/array,
            // else sequence".
            return ProjectClrCharSequence(call, ProjectClrChar(call, closedReturnType));
        }

        // Handle generic function call: identity[int](42)
        // The calleeType will be GenericFunctionType from CheckIndexAccess
        if (calleeType is GenericFunctionType genericFuncType)
        {
            // Record the resolved call target for codegen (and check deprecation) — #1438
            RecordResolvedCallTarget(call, genericFuncType.FunctionSymbol);

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
            return FinalizeCallReturnType(substitutedReturnType);
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
    /// True for the synthetic <c>builtins</c> module — the one whose members are the same symbols
    /// the bare spelling of a builtin resolves to.
    /// </summary>
    /// <remarks>
    /// The <c>IsNetModule</c> half is what keeps a user's own <c>builtins.spy</c> out: a source
    /// module is not a .NET module, so it keeps ordinary module resolution and does not inherit
    /// builtin inference. Name alone would hand a user's module the builtins' semantics.
    /// </remarks>
    private static bool IsBuiltinsModule(ModuleSymbol moduleSymbol) =>
        moduleSymbol.IsNetModule
        && string.Equals(moduleSymbol.CanonicalModuleName, "builtins", StringComparison.Ordinal);

    /// <summary>
    /// The builtin TYPE a <c>builtins.</c>-qualified member names (<c>builtins.dict</c> → the
    /// registry's <c>dict</c>), or null when the member names no builtin type.
    /// </summary>
    /// <remarks>
    /// <para>The registry — not the discovered CLR surface of <c>Sharpy.Builtins</c> — is what a
    /// bare spelling resolves against, so it is what the qualified spelling has to resolve against
    /// too, or the escape hatch means something different from the name it escapes to. The two
    /// disagreed in BOTH directions, because the discovered surface is a static helper class whose
    /// method inventory is an implementation detail: <c>dict</c> and <c>frozenset</c> have no
    /// helper method behind them, so the qualified spelling reported "module has no member" where
    /// bare gives SPY0227; and <c>tuple</c> DOES have one (<c>Builtins.Tuple&lt;T1,T2&gt;</c>), so
    /// the qualified spelling bound a method where bare refuses the shape outright (SPY0338) — the
    /// call reached codegen and came back as CS0411 behind SPY0908 (#1322).</para>
    /// <para>The primitives are the one carve-out, and it is the bare path's own carve-out rather
    /// than a second rule: <c>int(x)</c> is the conversion FUNCTION, not a construction, so a
    /// primitive name that has registered overloads is left to the function path exactly as
    /// <see cref="CheckFunctionCall"/>'s identifier arm leaves it.</para>
    /// <para>An escaped member (<c>builtins.`dict`</c>) is excluded by the same identity rule that
    /// governs every other name position: an escaped spelling never binds a bare-declared symbol,
    /// and the registry's names are bare.</para>
    /// </remarks>
    private TypeSymbol? TryResolveBuiltinsQualifiedType(
        ModuleSymbol moduleSymbol, string memberName, bool isMemberBacktickEscaped,
        bool isCalleePosition = true)
    {
        if (isMemberBacktickEscaped || !IsBuiltinsModule(moduleSymbol))
            return null;

        var registryType = _symbolTable.BuiltinRegistry.GetType(memberName);
        if (registryType == null)
            return null;

        // Primitives with overloads (int, str, float, …) are carved out in CALLEE position
        // so CheckBuiltinsQualifiedCall routes them through overload resolution rather than
        // CheckConstructorCall. In VALUE position the carve-out lifts: the constructor-
        // reference tiers need the resolved type (#1463).
        if (isCalleePosition)
        {
            var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(memberName);
            if (PrimitiveCatalog.IsPrimitive(memberName) && overloads is { Count: > 0 })
                return null;
        }

        return registryType;
    }

    /// <summary>
    /// Checks <c>builtins.X(...)</c> — the qualified spelling of a builtin call — through the same
    /// authorities, in the same order, that <see cref="CheckFunctionCall"/>'s identifier arm applies
    /// to the bare spelling. Returns null when the receiver is not the builtins module or the member
    /// names nothing the registry knows, leaving every other module call exactly as it was.
    /// </summary>
    /// <remarks>
    /// <para>The qualified spelling is the sanctioned escape from a shadowed builtin, which is why
    /// SPY0483 warns instead of refusing. That trade is only honest if the escape lands on the same
    /// meaning, and it did not: the qualified path resolved against the CLR-discovered surface of
    /// the <c>Sharpy.Builtins</c> static class, so its answers tracked that class's method inventory
    /// instead of the registry. The failures were not one bug but one CAUSE with several faces —
    /// <c>builtins.dict()</c> "module has no member" vs bare's SPY0227; <c>builtins.tuple(xs)</c>
    /// binding a helper method where bare refuses the shape (SPY0338), reaching codegen and coming
    /// back CS0411 behind SPY0908; single-signature builtins reporting arity as SPY0354 where bare
    /// says SPY0224; and <c>builtins.isinstance(x, T)</c> ranked against unrankable CLR overloads
    /// into SPY0353 where bare answers <c>bool</c> (#1322).</para>
    /// <para>So this dispatches rather than re-checks: <see cref="BuiltinReturnTypeInference"/>
    /// first (bare's first authority), then construction through
    /// <see cref="CheckConstructorCall"/> for a registry TYPE, then the registry's own overload
    /// ranking, then <see cref="ValidateFunctionSymbolCall"/> for a single signature. No refusal is
    /// restated here — every one of them is reached by going where bare goes.</para>
    /// <para>It also records <see cref="CalleeRouting.Builtin"/>, which codegen applies by emitting
    /// the BARE spelling's emission: the qualified syntax has no C# form of its own
    /// (<c>Sharpy.Builtins.Dict()</c> names no method), and the receiver's identity is a semantic
    /// fact the emitter must not re-derive.</para>
    /// </remarks>
    private SemanticType? CheckBuiltinsQualifiedCall(
        MemberAccess memberAccess, FunctionCall call, List<SemanticType> argTypes,
        Dictionary<string, SemanticType> kwargTypes, int totalArgCount,
        bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        if (memberAccess.IsMemberBacktickEscaped
            || _semanticInfo.GetExpressionType(memberAccess.Object) is not ModuleType moduleType
            || !IsBuiltinsModule(moduleType.Symbol))
        {
            return null;
        }

        var name = memberAccess.Member;

        // `isinstance` was held back here until the recogniser could be told whether a member-access
        // receiver is the builtins module (#1381). It can now: NarrowingConditionInterpreter takes a
        // REQUIRED predicate and its leaf arm recognises the qualified spelling, so routing this
        // through no longer produces a type test that compiles without narrowing — the failure mode
        // that made the refusal correct. The condition that comment named is the one this satisfies.

        var registryType = TryResolveBuiltinsQualifiedType(moduleType.Symbol, name, isMemberBacktickEscaped: false);
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(name);
        if (registryType == null && overloads is not { Count: > 0 })
            return null;

        _semanticInfo.SetCalleeRouting(call, CalleeRouting.Builtin);

        // Data-driven inference (len, hash, reversed, sorted, min, max) — bare checks this before
        // anything else, so a name it answers must not be answered by construction or by overload
        // ranking here either.
        var builtinReturn = BuiltinReturnTypeInference.InferReturnType(
            name, EffectiveMinMaxArgumentTypes(call, argTypes), _typeInference);
        if (builtinReturn != null)
        {
            if (builtinReturn is UnknownType
                && name is BuiltinNames.Min or BuiltinNames.Max
                && argTypes.Count >= 2)
            {
                ReportMinMaxPromotionFailure(call, name, argTypes);
                return SemanticType.Unknown;
            }
            RecordMinMaxTypeArguments(call, name, argTypes, builtinReturn);
            ValidateMinMaxValueFormKey(name, call, argTypes, kwargTypes);
            return builtinReturn;
        }

        if (registryType != null)
            return CheckConstructorCall(call, registryType, argTypes, kwargTypes, totalArgCount);

        return overloads!.Count > 1
            ? ResolveBuiltinOverloadCore(name, id: null, overloads, argTypes, totalArgCount, call)
            : ValidateFunctionSymbolCall(call, overloads[0], argTypes, kwargTypes, totalArgCount,
                isNullConditionalCall, isOptionalNullConditional);
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
            GenericType generic when GenericDefinitionOf(generic) is { } genericDefinition => genericDefinition,
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

            // Different outer name — if expected carries CLR provenance and the arg's name
            // matches the origin's simple name, recurse on type arguments (#1518). The closed
            // spelling reaches the provenance arm (ClrOriginIsSatisfiedBy) in IsAssignable;
            // the open spelling could not, because TryGetClrType returns null for generator-
            // wrapped IEnumerable[T] (no backing symbol). Matching the origin name structurally
            // with recursive ArgMatchesGenericShape avoids the CLR round-trip entirely.
            // Gated on ClrOriginTypeName so user-defined formals (no provenance) stay strict:
            // a Sharpy-written list[T] has no origin, so take() stays SPY0354; array[T] has
            // no origin, so #954 stays refused; same-name shapes never reach this arm.
            if (arg is GenericType argGeneric)
            {
                if (expected is GenericType { ClrOriginTypeName: not null } expectedGeneric
                    && argGeneric.TypeArguments.Count == expectedGeneric.TypeArguments.Count
                    && OriginSimpleNameMatches(argGeneric.Name, expectedGeneric.ClrOriginTypeName))
                {
                    for (int i = 0; i < expectedGeneric.TypeArguments.Count; i++)
                    {
                        if (!ArgMatchesGenericShape(argGeneric.TypeArguments[i], expectedGeneric.TypeArguments[i]))
                            return false;
                    }

                    return true;
                }

                return false;
            }

            // Non-generic argument against an open generic shape: accept only if genuinely
            // assignable with type parameters treated as object — rejects float vs list[T]
            // while still allowing a subtype (e.g. MyList vs list[T]).
            return IsAssignable(arg, SubstituteTypeParametersWithObject(expected));
        }

        // Same-arity tuple against a tuple shape: recurse element-wise, exactly as the same-name
        // generic arm does, so an open generic INSIDE a tuple element keeps its wildcard positions.
        // Substituting object here made `tuple[K, list[T]]` compare `list[T]` against `list[object]`,
        // which invariance rejects — the shape `iter[tuple[K, list[T]]]` was refused SPY0354 whether
        // or not K carried a constraint, while `tuple[K, T]` (no nested generic) passed (#1600).
        if (expected is TupleType expectedTuple && arg is TupleType argTuple)
        {
            if (argTuple.ElementTypes.Count != expectedTuple.ElementTypes.Count)
                return false;
            for (int i = 0; i < expectedTuple.ElementTypes.Count; i++)
            {
                if (!ArgMatchesGenericShape(argTuple.ElementTypes[i], expectedTuple.ElementTypes[i]))
                    return false;
            }

            return true;
        }

        // NullableType<T>, OptionalType<T>, TupleType<T,...> (against a non-tuple arg): substitute
        // type parameters with object and check assignability — rejects structurally incompatible
        // args (e.g., list[int] ↛ T?) while still accepting compatible ones (#966).
        if (expected is NullableType or OptionalType or TupleType)
            return IsAssignable(arg, SubstituteTypeParametersWithObject(expected));

        // FunctionType, GenericFunctionType, and other opaque shapes: preserve permissive
        // behavior — real checking happens during generic type inference.
        return true;
    }

    /// <summary>
    /// True when <paramref name="argName"/> matches the simple (unqualified, arity-stripped)
    /// name from a CLR origin type name. For example, <c>"IEnumerable"</c> matches
    /// <c>"System.Collections.Generic.IEnumerable`1"</c>.
    /// </summary>
    private static bool OriginSimpleNameMatches(string argName, string clrOriginTypeName)
    {
        var simpleName = Shared.ClrNameHelper.StripArity(clrOriginTypeName);
        var lastDot = simpleName.LastIndexOf('.');
        if (lastDot >= 0)
            simpleName = simpleName[(lastDot + 1)..];
        return string.Equals(argName, simpleName, StringComparison.Ordinal);
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
                    TypeArguments = g.TypeArguments.Select(SubstituteTypeParametersWithObject).ToList(),
                    // Provenance survives the substitution, as it does in TypeSubstitution.Apply.
                    // A formal the bridge mapped from CLR metadata MEANS the CLR type it came from,
                    // and the assignability arm that knows this keys on ClrOriginTypeName (#1260);
                    // rebuilding without it silently asked a different question. No verdict in the
                    // current suite depends on this, so it is a correctness alignment between two
                    // copies of one operation rather than a fix — but a reconstruction that drops
                    // provenance is a trap for the next caller either way.
                    ClrOriginTypeName = g.ClrOriginTypeName
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
                    // Close the definition CONSTRAINT-AWARE, matching the ClrMethod path above
                    // (SubstituteGenericParameters): a blanket typeof(object) violates a value-type
                    // constraint — `NdArray<T where T : unmanaged>` threw TypeLoadException building
                    // `NdArray<object>` (#1395). SubstituteGenericParameters substitutes a
                    // value-constrained parameter with int, object otherwise.
                    if (clrType.IsGenericTypeDefinition)
                        return SubstituteGenericParameters(clrType);
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
        string calleeName, FunctionCall call,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes)
    {
        if (calleeName is not (BuiltinNames.Min or BuiltinNames.Max))
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
                    $"'{calleeName}' value form does not accept keyword argument '{kw.Name}'",
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
            $"'{calleeName}' key must be callable; got '{keyType.GetDisplayName()}'",
            keyArg?.LineStart, keyArg?.ColumnStart,
            code: DiagnosticCodes.Semantic.NotCallable,
            span: keyArg?.Span);
    }

    private void ReportMinMaxPromotionFailure(FunctionCall call, string calleeName, List<SemanticType> argTypes)
    {
        var typeNames = string.Join(", ", argTypes.Select(t => $"'{t.GetDisplayName()}'"));
        AddError(
            $"Cannot determine common numeric type for '{calleeName}' with argument types {typeNames}"
            + " — no numeric promotion covers this pair (C# §12.4.7: uint64 has no operator with a signed operand);"
            + " cast one argument to a shared type, e.g. int64(x) or uint64(x)",
            call.LineStart, call.ColumnStart,
            code: DiagnosticCodes.Semantic.TypeMismatch,
            span: call.Span);
    }

    private void RecordMinMaxTypeArguments(FunctionCall call, string calleeName, List<SemanticType> argTypes, SemanticType returnType)
    {
        if (calleeName is not (BuiltinNames.Min or BuiltinNames.Max))
            return;
        if (argTypes.Count < 2 || _semanticInfo == null)
            return;
        if (argTypes.Any(t => !Equals(t, returnType)))
            _semanticInfo.SetInferredTypeArguments(call, new List<SemanticType> { returnType });
    }

    /// <summary>
    /// Resolves builtin function overloads for a call. Returns the resolved return type,
    /// or null if no overload resolution is needed.
    /// </summary>
    private SemanticType? ResolveBuiltinOverload(
        Identifier id, List<SemanticType> argTypes, int totalArgCount, FunctionCall call)
    {
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
        var isBuiltinWithOverloads = overloads != null && overloads.Count > 1;
        if (!isBuiltinWithOverloads)
            return null;

        // Identity decides: ANY non-builtin symbol answering Lookup shadows the builtin,
        // regardless of symbol kind. The old `as FunctionSymbol` let a VariableSymbol shadow
        // fall through (funcSymbol == null → builtin wins silently, #1326). The escape filter
        // is critical: a lookup for bare `str` may answer the user's escaped `class \`str\`` —
        // that is not a shadow, it's a different namespace (#1241).
        var lookupSymbol = _symbolTable.Lookup(id.Name);
        if (lookupSymbol != null && id.IsNameBacktickEscaped != lookupSymbol.IsNameBacktickEscaped)
            lookupSymbol = null;
        if (lookupSymbol != null && !_symbolTable.BuiltinRegistry.IsBuiltinSymbol(lookupSymbol))
            return null;

        return ResolveBuiltinOverloadCore(id.Name, id, overloads!, argTypes, totalArgCount, call);
    }

    /// <summary>
    /// Ranks a call against a builtin's registered overloads, keyed by NAME so the qualified
    /// spelling (<c>builtins.min(…)</c>) reaches the identical ranking the bare one does (#1322).
    /// <paramref name="id"/> is the callee identifier when there is one — the qualified spelling has
    /// no identifier node to annotate, and the call-target fact is what codegen reads either way.
    /// </summary>
    /// <remarks>
    /// The shadow gate lives in the bare caller, not here: a qualified spelling is by definition
    /// unshadowable, so applying the gate to it would defeat the escape.
    /// </remarks>
    private SemanticType? ResolveBuiltinOverloadCore(
        string name, Identifier? id, List<FunctionSymbol> overloads,
        List<SemanticType> argTypes, int totalArgCount, FunctionCall call)
    {
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
            // Update the identifier symbol to point to the matching overload (the qualified
            // spelling has no identifier node; the call target below is what codegen reads)
            if (id != null)
                _semanticInfo.SetIdentifierSymbol(id, matchingOverload);
            // Record the resolved call target for codegen (and check deprecation) — #1438
            RecordResolvedCallTarget(call, matchingOverload);
            return FinalizeCallReturnType(matchingOverload.ReturnType);
        }

        // isinstance must always type to bool even when no overload matched. No overload accepts a
        // tuple or a bare generic name, and the diagnostic for those shapes belongs to the type-operand
        // classifier (SPY0344/SPY0345, #1207/#1213) — a premature SPY0224 here would report the
        // symptom instead.
        if (name == BuiltinNames.Isinstance)
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
            AddError($"No overload of '{name}' matches the argument types ({typeList})",
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
        AddError($"Function '{name}' expects {expectedCounts} arguments but got {totalArgCount}",
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

        // Record the resolved call target for codegen (and check deprecation) — #1438
        RecordResolvedCallTarget(call, matchingOverload);

        var returnType = matchingOverload.ReturnType;

        // Substitute type parameters for builtin generic types (e.g., T0 -> int for dict[str, int])
        if (typeSubstitution != null)
        {
            returnType = typeSubstitution(returnType);
        }
        returnType = FinalizeCallReturnType(returnType);

        if (isNullConditionalCall)
            return WrapNullConditionalResult(call, returnType, isOptionalNullConditional);
        return returnType;
    }

    /// <summary>
    /// Finds all overloads for a method name walking the type hierarchy.
    /// Returns null if no overloads are found.
    /// </summary>
    private List<FunctionSymbol>? FindMethodOverloadsInHierarchy(TypeSymbol type, string methodName)
        => FindOverloadsInHierarchy(type, methodName,
            static (t, n) => t.MethodOverloads.TryGetValue(n, out var overloads) && overloads.Count > 0
                ? overloads
                : null);

    /// <summary>
    /// Finds the overload set for a DUNDER name walking the type hierarchy, or <c>null</c>.
    ///
    /// <para>Dunder overloads are deliberately kept out of <see cref="TypeSymbol.MethodOverloads"/>
    /// — <c>NameResolver</c> files an operator dunder under <see cref="TypeSymbol.OperatorMethods"/>
    /// and a protocol dunder (including <c>__call__</c>) under
    /// <see cref="TypeSymbol.ProtocolMethods"/>. A lookup that reads only <c>MethodOverloads</c> is
    /// therefore blind to every dunder overload set, which is how <c>obj(args)</c> on a class with
    /// two <c>__call__</c> declarations resolved to whichever was written first (#1672).</para>
    /// </summary>
    private List<FunctionSymbol>? FindDunderOverloadsInHierarchy(TypeSymbol type, string dunderName)
        => FindOverloadsInHierarchy(type, dunderName, static (t, n) =>
            t.ProtocolMethods.TryGetValue(n, out var protocolOverloads) && protocolOverloads.Count > 0
                ? protocolOverloads
                : t.OperatorMethods.TryGetValue(n, out var operatorOverloads) && operatorOverloads.Count > 0
                    ? operatorOverloads
                    : null);

    /// <summary>
    /// The one hierarchy walk both overload lookups share: the type itself, then its base-class
    /// chain, then its interfaces (the last handles interface-typed variables and interface methods
    /// not reachable through the base chain, #364). Only the per-type dictionary differs.
    /// </summary>
    private List<FunctionSymbol>? FindOverloadsInHierarchy(
        TypeSymbol type, string name, Func<TypeSymbol, string, List<FunctionSymbol>?> lookup)
    {
        if (lookup(type, name) is { } own)
            return own;

        foreach (var baseType in TypeHierarchyService.GetAllBaseTypes(type, SemanticBinding))
        {
            if (lookup(baseType, name) is { } inherited)
                return inherited;
        }

        foreach (var iface in TypeHierarchyService.GetAllInterfaces(type, SemanticBinding))
        {
            if (lookup(iface, name) is { } fromInterface)
                return fromInterface;
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
        => TryResolveTypeSymbolFromMemberAccess(memberAccess, out _);

    private TypeSymbol? TryResolveTypeSymbolFromMemberAccess(
        MemberAccess memberAccess, out TypeAliasSymbol? resolvedAlias)
    {
        resolvedAlias = null;
        // The object is usually already checked (CheckCall runs CheckExpression on the callee
        // before routing here); fall back to checking it for paths that reach this helper
        // first (e.g., generic index-access resolution). Nested module access (email.message)
        // returns a ModuleType, so this handles both direct and nested module qualifiers.
        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object)
            ?? CheckExpression(memberAccess.Object);
        if (objectType is not ModuleType moduleType)
        {
            // A TYPE qualifier: use the already-resolved expression type to find nested types
            // rather than re-walking from scratch. This handles module→type→nested chains
            // like `lib.Registry.Entry` where the root is a module (#1523).
            if (objectType is UserDefinedType qualUdt)
            {
                var qualSym = qualUdt.Symbol ?? _symbolTable.LookupType(qualUdt.Name);
                return qualSym?.NestedTypes.FirstOrDefault(n => n.Name == memberAccess.Member);
            }
            return LookupNestedTypeSymbol(memberAccess);
        }

        var moduleSymbol = moduleType.Symbol;
        var memberName = memberAccess.Member;

        // The builtins module's TYPE surface is the registry, consulted BEFORE the discovered
        // exports — `dict` is not among them and `tuple` is among them as the wrong thing. See
        // TryResolveBuiltinsQualifiedType (#1322).
        if (TryResolveBuiltinsQualifiedType(moduleSymbol, memberName, memberAccess.IsMemberBacktickEscaped)
            is { } builtinsQualifiedType)
        {
            return builtinsQualifiedType;
        }

        // For .NET modules, try PascalCase conversion if the exact name isn't found
        // (mirrors the module branch in CheckMemberAccess / CheckIndexAccess).
        if (!moduleSymbol.Exports.ContainsKey(memberName) && moduleSymbol.IsNetModule)
        {
            var pascalName = NameMangler.ToPascalCase(memberName);
            if (moduleSymbol.Exports.ContainsKey(pascalName))
                memberName = pascalName;
        }

        if (moduleSymbol.Exports.TryGetValue(memberName, out var exportedSymbol))
        {
            if (exportedSymbol is TypeSymbol typeSymbol)
                return typeSymbol;

            if (exportedSymbol is TypeAliasSymbol aliasSymbol
                && aliasSymbol.TypeAnnotation != null)
            {
                var expanded = _typeResolver.ResolveTypeAnnotation(aliasSymbol.TypeAnnotation);
                if (expanded is UserDefinedType { Symbol: TypeSymbol targetType })
                {
                    resolvedAlias = aliasSymbol;
                    return targetType;
                }
                if (expanded is BuiltinType)
                {
                    var registryType = _symbolTable.BuiltinRegistry.GetType(aliasSymbol.TypeAnnotation.Name);
                    if (registryType != null)
                    {
                        resolvedAlias = aliasSymbol;
                        return registryType;
                    }
                }
            }
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
        MemberAccess memberAccess, List<SemanticType> argTypes,
        Dictionary<string, SemanticType> kwargTypes, int totalArgCount, FunctionCall call,
        bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        var objectType = _semanticInfo.GetExpressionType(memberAccess.Object);
        if (objectType is not ModuleType moduleType)
            return null;

        var moduleSymbol = moduleType.Symbol;

        // `builtins.len(xs)` must mean exactly what `len(xs)` means — the qualified spelling is the
        // sanctioned way to reach a builtin that a local declaration has shadowed, so if the two
        // spellings disagree the escape is worthless precisely when it is needed. The bare spelling
        // short-circuits into BuiltinReturnTypeInference (see CheckCall) before any CLR overload
        // resolution runs. The qualified one used to skip that and resolve against the raw
        // discovered overload set, where Len(ICollection), Len(ISized) and Len(object) all match a
        // list[int] with nothing to rank them — SPY0353, with no shadowing involved (#1322).
        // The bare spelling is the definition; this makes the qualified one agree.
        if (IsBuiltinsModule(moduleSymbol))
        {
            var builtinReturn = BuiltinReturnTypeInference.InferReturnType(
                memberAccess.Member, EffectiveMinMaxArgumentTypes(call, argTypes), _typeInference);
            if (builtinReturn != null)
            {
                if (builtinReturn is UnknownType
                    && memberAccess.Member is BuiltinNames.Min or BuiltinNames.Max
                    && argTypes.Count >= 2)
                {
                    ReportMinMaxPromotionFailure(call, memberAccess.Member, argTypes);
                    return SemanticType.Unknown;
                }
                RecordMinMaxTypeArguments(call, memberAccess.Member, argTypes, builtinReturn);
                return builtinReturn;
            }
        }

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

        // Record the resolved call target for codegen (and check deprecation) — #1438
        RecordResolvedCallTarget(call, matchingOverload);

        // Overload selection filters by keyword NAMES leniently — a fully unmatched set falls
        // through to arity-only ranking — and nothing after it checked them, so
        // `json.dumps(x, allowNan=False)` bound silently under a spelling CPython refuses
        // (#1591). The selected overload's parameter list is the binding surface; validate the
        // kwargs against it. Module functions carry no receiver parameter, so no self skip.
        ValidateKeywordArguments(call, matchingOverload.Parameters, argTypes.Count, kwargTypes,
            clrParameterNames: matchingOverload.ClrMethodName != null);

        var returnType = InferGenericReturnType(matchingOverload, argTypes, call);

        if (isNullConditionalCall)
            return WrapNullConditionalResult(call, returnType, isOptionalNullConditional);
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
        if (_currentFilePath != overloads[0].DeclaringFilePath)
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
        // Record the resolved call target for codegen (and check deprecation) — #1438
        RecordResolvedCallTarget(call, matchingOverload);

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

        // Shadow check: if the bound symbol IS one of the overloads — or both are clones of
        // the same declaration, compared by following the OriginSymbol chain to its root —
        // it is not shadowed and overload resolution proceeds. Otherwise a local definition
        // shadows the imported overloads (#1525). Pure reference identity through the chain;
        // no path agreement anywhere (the DeclaringFilePath comparison this replaced disabled
        // itself silently when the extraction left the path null).
        var funcSymbol = _symbolTable.Lookup(id.Name) as FunctionSymbol;
        if (funcSymbol != null
            && !overloads.Any(o => ReferenceEquals(RootOrigin(o), RootOrigin(funcSymbol))))
        {
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
        // Record the resolved call target for codegen (and check deprecation) — #1438
        RecordResolvedCallTarget(call, matchingOverload);

        return InferGenericReturnType(matchingOverload, argTypes, call);
    }

    private string? TryGetDefaultMethodInterfaceName(TypeSymbol typeSymbol, string methodName)
    {
        if (typeSymbol.TypeKind != TypeKind.Class)
            return null;

        var (existingMethod, _) = TypeHierarchyService.FindMember<FunctionSymbol>(
            typeSymbol, methodName, t => t.Methods, searchInterfaces: false);
        if (existingMethod != null)
            return null;

        foreach (var ifaceRef in typeSymbol.Interfaces)
        {
            if (ifaceRef.Definition.Methods.Any(m => m.Name == methodName && !m.IsAbstract))
                return NameMangler.Transform(ifaceRef.Definition.Name, NameContext.Interface);
        }

        return null;
    }

    /// <summary>
    /// The type of a zero-argument call whose callee resolved to a CLR property or field
    /// (<c>s.count()</c>, <c>sb.length()</c>, <c>DateTime.now()</c>), or null when the callee is
    /// anything else. Records the zero-arg-call-onto-property collapse the emitter reads, so the
    /// generated C# is the property access and the call's type is the property's type (#1640).
    /// </summary>
    /// <remarks>
    /// Before this, the member seam declined a callee-position property and nothing typed the call:
    /// it stayed Unknown, assignable to anything, so <c>x: str = s.count()</c> reached Roslyn as
    /// CS0029 behind SPY0908 — the compiler reporting its own bug for an ordinary type error. The
    /// STATIC spelling did not even emit: <c>DateTime.now()</c> wrote <c>DateTime.Now()</c> and came
    /// back CS1955, because the collapse was recorded only inside the discovered-user-type route.
    /// </remarks>
    private SemanticType? ClrPropertyCallType(FunctionCall call, MemberAccess memberAccess)
    {
        if (call.Arguments.Length != 0 || call.KeywordArguments.Length != 0)
            return null;

        if (ClrCalleeValueMember(memberAccess) is not { } member)
            return null;

        _semanticInfo.SetClrPropertyCallLowering(call);
        _semanticInfo.SetResolvedClrMemberName(memberAccess, member.ClrName);
        return ProjectClrChar(call, member.Type);
    }

    private static bool IsClrPropertyOnType(TypeSymbol typeSymbol, string memberName)
    {
        var current = typeSymbol;
        while (current != null)
        {
            if (current.Properties.Any(p =>
                string.Equals(p.Name, memberName, StringComparison.OrdinalIgnoreCase)))
            {
                var (method, _) = TypeHierarchyService.FindMethod(typeSymbol, memberName);
                return method == null;
            }

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Follows the <see cref="Symbol.OriginSymbol"/> chain to the declaration a clone was
    /// ultimately made from. Identity of two symbols' roots IS the shadow decision (#1525):
    /// clones of the same declaration are the same function, whatever their spelling.
    /// </summary>
    private static FunctionSymbol RootOrigin(FunctionSymbol s)
    {
        while (s.OriginSymbol is FunctionSymbol origin)
            s = origin;
        return s;
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
        // A bridged generic builtin (Builtins.Max<T>) carries no own TypeParameters but names T in
        // its return type — the same seam decides whether that T is in scope or an unbound leak.
        if (!overload.IsGeneric)
            return FinalizeCallReturnType(overload.ReturnType);

        var inferenceResult = _genericInference.InferTypeArguments(overload, argTypes);
        if (inferenceResult.Success && inferenceResult.InferredTypes != null)
        {
            _semanticInfo.SetInferredTypeArguments(call, inferenceResult.InferredTypes);
            var result = SubstituteTypeParameters(
                overload.ReturnType,
                overload.TypeParameters,
                inferenceResult.InferredTypes);
            return FinalizeCallReturnType(result);
        }

        return FinalizeCallReturnType(overload.ReturnType);
    }

    /// <summary>
    /// The type a call records: <paramref name="returnType"/>, or
    /// <c>Unknown</c> when the type still names a type parameter that is NOT in scope — the
    /// callee's own parameter that inference failed to bind (an argument was already
    /// <c>Unknown</c>, or an early-return route never ran inference), so nothing may render the
    /// unsubstituted <c>'T'</c> in a later diagnostic (#1728, plan-14853b Decision 9 ii). A type
    /// parameter of the enclosing class or of an enclosing generic def IS in scope and is kept:
    /// <c>x: int = first(self.items)</c> inside <c>class Box[U]</c> stays the SPY0220 that names
    /// <c>'U'</c>, never an Unknown that ICEs later (SPY0220 @ f7c7d3d97, CS0029 after a1b22ed94).
    /// Every route that turns a resolved <see cref="FunctionSymbol"/> into a call's type goes
    /// through this one seam, so the rule cannot differ by callee kind.
    /// </summary>
    private SemanticType FinalizeCallReturnType(SemanticType returnType)
        => ContainsUnboundTypeParameter(returnType) ? SemanticType.Unknown : returnType;

    private bool IsTypeParameterInScope(TypeParameterType parameter)
        => _functionTypeParametersInScope.Contains(parameter.Name)
            || (_currentClass?.TypeParameters.Any(tp => tp.Name == parameter.Name) ?? false);

    private bool ContainsUnboundTypeParameter(SemanticType type) => type switch
    {
        TypeParameterType p => !IsTypeParameterInScope(p),
        ResultType rt => ContainsUnboundTypeParameter(rt.OkType) || ContainsUnboundTypeParameter(rt.ErrorType),
        OptionalType ot => ContainsUnboundTypeParameter(ot.UnderlyingType),
        NullableType nt => ContainsUnboundTypeParameter(nt.UnderlyingType),
        GenericType gt => gt.TypeArguments.Any(ContainsUnboundTypeParameter),
        FunctionType ft => ft.ParameterTypes.Any(ContainsUnboundTypeParameter) || ContainsUnboundTypeParameter(ft.ReturnType),
        TupleType tt => tt.ElementTypes.Any(ContainsUnboundTypeParameter),
        _ => false
    };

    /// <summary>
    /// Validates a function call against a resolved FunctionSymbol, including generic inference,
    /// argument count, positional/keyword argument type checking.
    /// </summary>
    private SemanticType ValidateFunctionSymbolCall(
        FunctionCall call, FunctionSymbol funcSymbol,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes,
        int totalArgCount, bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        // Record the resolved call target for codegen (and check deprecation) — #1438
        RecordResolvedCallTarget(call, funcSymbol);

        // Check for iterable spread into non-variadic function (SPY0357)
        // Must run before generic inference — generic functions without *args must also reject
        // iterable spread. Tuple spread is excluded because tuple size is statically known.
        if (CheckSpreadIntoNonVariadic(call, funcSymbol.Name, funcSymbol.Parameters))
        {
            var earlyReturn = FinalizeCallReturnType(funcSymbol.ReturnType);
            if (isNullConditionalCall)
                return WrapNullConditionalResult(call, earlyReturn, isOptionalNullConditional);
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
                // Resolve any pending overload selections that were deferred because the expected
                // type contained unsolved type parameters (#1589). The inference result now has
                // (at least partial) bindings, so re-enter overload selection with the concrete
                // expected type. Any additional bindings (e.g. U = bytes from a selected
                // bytes(str) -> bytes overload) are folded back into the inferred types.
                var inferredBindings = new Dictionary<string, SemanticType>(StringComparer.Ordinal);
                for (int tpi = 0; tpi < funcSymbol.TypeParameters.Count && tpi < inferenceResult.InferredTypes.Count; tpi++)
                    inferredBindings[funcSymbol.TypeParameters[tpi].Name] = inferenceResult.InferredTypes[tpi];
                var additionalBindings = ResolvePendingOverloadSelections(inferredBindings, argTypes: null);
                if (additionalBindings != null)
                {
                    for (int tpi = 0; tpi < funcSymbol.TypeParameters.Count && tpi < inferenceResult.InferredTypes.Count; tpi++)
                    {
                        if (inferenceResult.InferredTypes[tpi] is TypeParameterType unbound
                            && additionalBindings.TryGetValue(unbound.Name, out var bound))
                        {
                            inferenceResult.InferredTypes[tpi] = bound;
                        }
                    }
                }

                // Inference succeeded - substitute type parameters and return the result
                var substitutedReturnType = SubstituteTypeParameters(
                    funcSymbol.ReturnType,
                    funcSymbol.TypeParameters,
                    inferenceResult.InferredTypes);

                // Store the inferred type arguments for codegen
                _semanticInfo.SetInferredTypeArguments(call, inferenceResult.InferredTypes);

                // This branch returns without reaching ValidateCallArguments below, so kwargs on a
                // generic call went entirely unchecked (#1591). Validate them against the INFERRED
                // binding, so a kwarg bound to a now-closed type parameter gets the same SPY0220 a
                // non-generic parameter gets. The slot mapping kwargs use for inference
                // (BuildInferenceArgumentTypes) matches names through the same
                // FindKeywordParameter arms, so what steered inference is what validates here.
                ValidateKeywordArguments(call, funcSymbol.Parameters, argTypes.Count, kwargTypes,
                    InferredTypeParameterBinding(funcSymbol, inferenceResult.InferredTypes),
                    clrParameterNames: funcSymbol.ClrMethodName != null);

                // Wrap result in optional/nullable for null conditional calls
                substitutedReturnType = FinalizeCallReturnType(substitutedReturnType);
                if (isNullConditionalCall)
                    return WrapNullConditionalResult(call, substitutedReturnType, isOptionalNullConditional);
                return substitutedReturnType;
            }
            else
            {
                // Inference failed — drop the deferred selections this call's arguments recorded;
                // without inference there is no target for them, and the call already has its error (#1589).
                DiscardPendingOverloadSelections();

                // Inference failed - report error
                AddError(inferenceResult.ErrorMessage ?? "Type arguments cannot be inferred",
                    call.LineStart, call.ColumnStart, code: DiagnosticCodes.Semantic.CannotInferGenericType,
                    span: call.Span);

                // The kwarg NAME rules need no binding at all, and a mistyped name is often the
                // very reason inference failed (the slot mapping stops at the first unmatched
                // parameter) — report it alongside, so the actionable diagnosis isn't hidden
                // behind the inference failure (#1591). Open parameter types are skipped by
                // SubstitutedParameterType with a null binding, so no type check runs against a
                // still-open type parameter.
                ValidateKeywordArguments(call, funcSymbol.Parameters, argTypes.Count, kwargTypes,
                    clrParameterNames: funcSymbol.ClrMethodName != null);
                return SemanticType.Unknown;
            }
        }

        ValidateCallArguments(call, funcSymbol.Parameters, argTypes, kwargTypes, totalArgCount,
            clrParameterNames: funcSymbol.ClrMethodName != null);

        var returnType = FinalizeCallReturnType(funcSymbol.ReturnType);

        // Wrap result in optional/nullable for null conditional calls
        if (isNullConditionalCall)
            return WrapNullConditionalResult(call, returnType, isOptionalNullConditional);
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
            else if (TryGetKeywordArgumentType(kwargTypes, funcSymbol.Parameters[i].Name, out var kwargType))
                ordered.Add(kwargType);
            else
                break;
        }

        return ordered;
    }

    /// <summary>
    /// Looks a parameter's keyword argument up by name using the same two spelling arms as
    /// <see cref="FindKeywordParameter"/>, in the same order — verbatim first, then the Python
    /// snake_case spelling of a verbatim-stored CLR name — so the slot mapping that steers generic
    /// inference and the validation that reports on it match the same kwargs (#909, #1591).
    /// </summary>
    private static bool TryGetKeywordArgumentType(
        Dictionary<string, SemanticType> kwargTypes, string parameterName, out SemanticType kwargType)
    {
        if (kwargTypes.TryGetValue(parameterName, out kwargType!))
            return true;

        foreach (var (keywordName, type) in kwargTypes)
        {
            if (parameterName == NameMangler.ToCamelCase(keywordName))
            {
                kwargType = type;
                return true;
            }
        }

        return false;
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
        FunctionCall call, Expression callee, FunctionType ft, List<SemanticType> argTypes,
        int totalArgCount, bool isNullConditionalCall, bool isOptionalNullConditional)
    {
        // #1650: FunctionType has no parameter names, so keyword arguments cannot bind.
        // Refuse them with a steer to pass positionally — but only for true function-typed
        // values (identifiers/lambdas), not member-access method calls that fell through
        // without a FunctionSymbol (e.g. super().__init__(), which has named parameters).
        //
        // The shape test reads the CANONICAL callee (#1170), never `call.Function`: redundant
        // parentheses do not change what a call denotes, so `(obj.method)(k=1)` is the same
        // method call as `obj.method(k=1)`. Testing the surface node refused every
        // parenthesized method call with a keyword argument — the MetamorphicCorpus
        // ParensWrapCallee transform turned eight compile-clean fixtures red with SPY0279.
        if (call.KeywordArguments.Length > 0 && callee is not MemberAccess)
        {
            foreach (var kwarg in call.KeywordArguments)
            {
                AddError(
                    $"Keyword arguments are not supported when calling a function-typed value; " +
                    $"pass '{kwarg.Name}' positionally",
                    kwarg.LineStart, kwarg.ColumnStart,
                    code: DiagnosticCodes.Semantic.KeywordArgOnFunctionType,
                    span: kwarg.Span);
            }
        }

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
                if (tooFew && IsDelegateCallee(call.Function))
                {
                    AddError(
                        "Defaults are not available through a function-typed value; " +
                        "call the function directly or pass the argument",
                        call.LineStart, call.ColumnStart,
                        code: DiagnosticCodes.Semantic.DelegateErasedDefaults,
                        span: call.Span);
                }
                else if (hasVariadic)
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

                    var functionValueArgNode = ArgumentNodeAt(call, i);
                    if (IsArgumentAssignable(argTypes[i], expected, functionValueArgNode))
                    {
                        ApplyArgumentConversion(
                            StorePosition.ArgumentPositional, functionValueArgNode, argTypes[i], expected);
                    }
                    else
                    {
                        CheckStore(StorePosition.ArgumentPositional, functionValueArgNode, argTypes[i],
                            expected, call.Arguments[i], call.Arguments[i].Span);
                    }
                }
            }
        }

        // Wrap result in optional/nullable for null conditional calls. A function-typed value's
        // declared return type can still name a type parameter no scope declares (a builtin bound
        // as a function value, called with an Unknown argument) — same seam as the symbol routes.
        returnType = FinalizeCallReturnType(returnType);
        if (isNullConditionalCall)
            return WrapNullConditionalResult(call, returnType, isOptionalNullConditional);
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
        int totalArgCount, TypeParameterBinding? typeBinding = null,
        bool clrParameterNames = false)
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

                var argNode = ArgumentNodeAt(call, i);
                if (!IsArgumentAssignable(argTypes[i], paramType, argNode))
                {
                    // A type-reference expression (e.g. module.SomeError) satisfies a
                    // parameter backed by CLR System.Type (e.g. assert_raises's exceptionType).
                    if (IsSystemTypeParameter(paramType) && i < call.Arguments.Length
                        && _semanticInfo.IsTypeReference(call.Arguments[i]))
                    {
                        // Allow — type reference satisfies a System.Type parameter
                    }
                    else
                    {
                        // The refusal is the seam's: same classification, same steers, and the
                        // strict-Optional cells report SPY0604 here exactly as they do at a
                        // declaration (#1720). Only the type-parameter-binding clause is this
                        // site's own.
                        CheckStore(StorePosition.ArgumentPositional, argNode, argTypes[i], paramType,
                            call.Arguments[i], call.Arguments[i].Span,
                            extraSteer: DescribeTypeParameterBinding(param.Type, typeBinding));
                    }
                }
                else
                {
                    ApplyArgumentConversion(StorePosition.ArgumentPositional, argNode, argTypes[i], paramType);
                }
            }

            ValidateKeywordArguments(call, parameters, argTypes.Count, kwargTypes, typeBinding,
                clrParameterNames);
        }
    }

    /// <summary>
    /// Validates a call's KEYWORD arguments against a (self-free) parameter list: unknown names
    /// (SPY0234, with a did-you-mean), positional-only violations (SPY0370), arguments already
    /// supplied positionally (SPY0235), and value types (SPY0220). Extracted from
    /// <see cref="ValidateCallArguments"/> so every route that holds a resolved parameter list —
    /// the #1537 single-method recording seam, the generic-inference branch of
    /// <see cref="ValidateFunctionSymbolCall"/>, and the module-overload route — applies the ONE
    /// implementation instead of growing a second (#1591).
    ///
    /// <para><paramref name="clrParameterNames"/> mirrors the emitter's
    /// <c>GetCSharpParameterName</c> gate (<c>ClrMethodName != null</c>): CLR-discovered parameter
    /// names are stored VERBATIM (camelCase — see <c>OverloadIndexBuilder.CreateParameterSignature</c>,
    /// the #942 regression guard), so the Python snake_case spelling is matched by camel-casing the
    /// written kwarg, and a hit whose written spelling is not the canonical Python form refuses with
    /// a steer to it — <c>allowNan=</c> steers to <c>allow_nan=</c> exactly as <c>math.Pi</c> steers
    /// to <c>pi</c> (#1540 one-spelling ruling, applied to kwargs by #1591). A Sharpy-declared
    /// parameter's canonical spelling is its declared source name, so a respelling of it refuses
    /// toward the declaration — which is CPython's own rule for a declared parameter name.</para>
    /// </summary>
    private void ValidateKeywordArguments(
        FunctionCall call, IReadOnlyList<ParameterSymbol> parameters,
        int positionalArgCount, Dictionary<string, SemanticType> kwargTypes,
        TypeParameterBinding? typeBinding = null, bool clrParameterNames = false)
    {
        foreach (var kwarg in call.KeywordArguments)
        {
            var param = FindKeywordParameter(parameters, kwarg.Name);
            if (param == null)
            {
                var suggestion = EditDistance.FindClosestMatch(kwarg.Name,
                    parameters.Select(p => CanonicalKeywordSpellingOf(p, clrParameterNames)));
                var unknownMessage = $"Unknown keyword argument '{kwarg.Name}'";
                if (suggestion != null)
                    unknownMessage += $". Did you mean '{suggestion}'?";
                AddError(unknownMessage,
                    kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                    span: kwarg.Span ?? kwarg.Value.Span,
                    data: SuggestionData(suggestion));
            }
            else if (CanonicalKeywordSpellingOf(param, clrParameterNames) is { } canonicalSpelling
                && canonicalSpelling != kwarg.Name)
            {
                // The raw CLR spelling names the right parameter, but the canonical kwarg spelling
                // in Sharpy source is the Python one (#1591): refuse with the exact steer. The
                // snake→camel mapping to the CLR parameter is the compiler's job under the hood.
                AddError($"Unknown keyword argument '{kwarg.Name}'. Did you mean '{canonicalSpelling}'?",
                    kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                    span: kwarg.Span ?? kwarg.Value.Span,
                    data: SuggestionData(canonicalSpelling));
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
                if (!param.IsKeywordOnly && paramIndex < positionalArgCount)
                {
                    AddError($"Argument '{kwarg.Name}' was already provided positionally",
                        kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                        span: kwarg.Span ?? kwarg.Value.Span);
                }
                else if (paramType != null)
                {
                    // A keyword argument is a store into the named parameter's slot, node and all:
                    // the route used to drop the node in the acceptance question and applied no
                    // side effects at all, so `f(x=0.5)` into a float32 formal emitted an
                    // unsuffixed double (CS1503 behind SPY0908) and `g(s="a")` into LiteralString
                    // was SPY0220 (#1688, #1731).
                    var kwargType = kwargTypes[kwarg.Name];
                    if (IsArgumentAssignable(kwargType, paramType, kwarg.Value))
                    {
                        ApplyArgumentConversion(
                            StorePosition.ArgumentKeyword, kwarg.Value, kwargType, paramType);
                    }
                    else if (!(IsSystemTypeParameter(paramType) && _semanticInfo.IsTypeReference(kwarg.Value)))
                    {
                        CheckStoreAt(StorePosition.ArgumentKeyword, kwarg.Value, kwargType, paramType,
                            kwarg.LineStart, kwarg.ColumnStart, kwarg.Span ?? kwarg.Value.Span,
                            slotName: kwarg.Name,
                            extraSteer: DescribeTypeParameterBinding(param.Type, typeBinding));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Binds a written keyword name to a parameter: the verbatim spelling first (a Sharpy-declared
    /// name, or a CLR name already in its Python form), then the camel-cased spelling (the Python
    /// snake_case form of a verbatim-stored CLR parameter name) — the same two arms, in the same
    /// order, as the emitter's <c>GetCSharpParameterName</c>, so the checker accepts exactly what
    /// emission can bind.
    /// </summary>
    private static ParameterSymbol? FindKeywordParameter(
        IReadOnlyList<ParameterSymbol> parameters, string keywordName)
    {
        return parameters.FirstOrDefault(p => p.Name == keywordName)
            ?? parameters.FirstOrDefault(p => p.Name == NameMangler.ToCamelCase(keywordName));
    }

    /// <summary>
    /// The ONE kwarg spelling that names <paramref name="param"/> in Sharpy source (#1591): the
    /// declared name for a Sharpy-declared parameter; <see cref="CanonicalClrParameterSpelling"/>
    /// for a verbatim-stored CLR name.
    /// </summary>
    private static string CanonicalKeywordSpellingOf(ParameterSymbol param, bool clrParameterNames)
        => clrParameterNames ? CanonicalClrParameterSpelling(param.Name) : param.Name;

    /// <summary>
    /// The Python spelling of a verbatim-stored CLR parameter name — its snake_case form, but only
    /// when that form round-trips back to the stored name (so the steer itself binds through
    /// <see cref="FindKeywordParameter"/>'s camel arm and the emitter's matching arm). A CLR name
    /// the round-trip cannot reach (e.g. a single capital <c>N</c>) keeps its verbatim spelling
    /// rather than steering callers into a name no arm can bind.
    /// </summary>
    private static string CanonicalClrParameterSpelling(string parameterName)
    {
        var snake = NameMangler.ToSnakeCase(parameterName);
        return parameterName == NameMangler.ToCamelCase(snake) ? snake : parameterName;
    }

    /// <summary>
    /// Validates keyword-argument NAMES against a reflected CLR parameter-name surface, for the
    /// seams that hold raw <see cref="System.Reflection.ParameterInfo"/> names rather than
    /// <see cref="ParameterSymbol"/>s: the CLR instance-call seam and the staged-extension decline
    /// (#1591). Names only — a name any candidate binds under the one-spelling rule passes, an
    /// unknown name refuses with a did-you-mean, and a raw CLR spelling refuses with the exact
    /// steer to its Python form. An empty surface checks nothing (stays permissive).
    /// </summary>
    private void ValidateClrKeywordArgumentNames(
        FunctionCall call, IReadOnlyCollection<string> parameterNames)
    {
        if (parameterNames.Count == 0)
            return;

        foreach (var kwarg in call.KeywordArguments)
        {
            var match = parameterNames.FirstOrDefault(name => name == kwarg.Name)
                ?? parameterNames.FirstOrDefault(name => name == NameMangler.ToCamelCase(kwarg.Name));
            if (match == null)
            {
                var suggestion = EditDistance.FindClosestMatch(kwarg.Name,
                    parameterNames.Select(CanonicalClrParameterSpelling));
                var unknownMessage = $"Unknown keyword argument '{kwarg.Name}'";
                if (suggestion != null)
                    unknownMessage += $". Did you mean '{suggestion}'?";
                AddError(unknownMessage,
                    kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                    span: kwarg.Span ?? kwarg.Value.Span,
                    data: SuggestionData(suggestion));
            }
            else if (CanonicalClrParameterSpelling(match) is { } canonicalSpelling
                && canonicalSpelling != kwarg.Name)
            {
                AddError($"Unknown keyword argument '{kwarg.Name}'. Did you mean '{canonicalSpelling}'?",
                    kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
                    span: kwarg.Span ?? kwarg.Value.Span,
                    data: SuggestionData(canonicalSpelling));
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
    ///
    /// <para>This path keeps <c>allowConstantConversion: true</c> (the single-candidate default)
    /// and is exempt from the #1464 ranking exclusion on purpose: arity already did the selecting,
    /// so a constant conversion here can only widen what the ONE chosen signature accepts — it
    /// never chooses between candidates. Two overloads of the same arity bail out above before any
    /// type check runs, so no ambiguity can be created.</para>
    /// </summary>
    /// <summary>
    /// Applies the store seam's accepted verdict to every positional argument of a call whose
    /// parameter list is known, WITHOUT reporting anything — the apply-only half of
    /// <see cref="ValidateCallArguments"/>, for routes that resolve a binding but do not (yet)
    /// type-check it.
    ///
    /// <para>The CLR-constructor route is the one that needs it: a bridged type's constructors live
    /// in <see cref="TypeSymbol.Constructors"/>, not in <c>Methods</c>, so
    /// <see cref="CheckConstructorCall"/> found no <c>__init__</c> and checked nothing at all —
    /// arity, types and conversions alike. <c>Vector2(1.0, 2.0)</c> was accepted and emitted as two
    /// unsuffixed doubles (CS1503 behind SPY0908, #1688). Applying the verdict cannot introduce a
    /// refusal: an argument the seam does not admit simply records no fact and the route behaves
    /// exactly as it does today. (Type-CHECKING CLR constructor arguments is a separate gap — the
    /// bridge's parameter types would have to be trusted to refuse with.)</para>
    /// </summary>
    private void ApplyResolvedArgumentConversions(
        FunctionCall call, IReadOnlyList<ParameterSymbol> parameters, List<SemanticType> argTypes,
        TypeParameterBinding? typeBinding = null)
    {
        var variadicIndex = -1;
        for (int p = 0; p < parameters.Count; p++)
        {
            if (parameters[p].IsVariadic)
            {
                variadicIndex = p;
                break;
            }
        }

        for (int i = 0; i < argTypes.Count; i++)
        {
            ParameterSymbol param;
            if (variadicIndex >= 0 && i >= variadicIndex)
                param = parameters[variadicIndex];
            else if (i < parameters.Count)
                param = parameters[i];
            else
                break;

            if (SubstitutedParameterType(param.Type, typeBinding) is not { } paramType)
                continue;

            ApplyArgumentConversion(
                StorePosition.ArgumentPositional, ArgumentNodeAt(call, i), argTypes[i], paramType);
        }
    }

    /// <summary>
    /// The sole constructor of <paramref name="typeSymbol"/> that accepts this many positional
    /// arguments, or null when zero or several do — the same arity-decides rule
    /// <see cref="ValidateSoleArityMatchingOverload"/> applies to <c>__init__</c> overloads,
    /// asked of the CLR-discovered constructor surface.
    /// </summary>
    private static IReadOnlyList<ParameterSymbol>? SoleArityMatchingConstructor(
        TypeSymbol typeSymbol, int totalArgCount)
    {
        IReadOnlyList<ParameterSymbol>? soleMatch = null;
        foreach (var ctor in typeSymbol.Constructors)
        {
            var parameters = ctor.Parameters.Skip(1).ToList();
            if (parameters.Any(p => p.IsVariadic))
                return null;

            var required = parameters.Count(p => !p.HasDefault);
            if (totalArgCount < required || totalArgCount > parameters.Count)
                continue;

            if (soleMatch != null)
                return null;

            soleMatch = parameters;
        }

        return soleMatch;
    }

    private FunctionSymbol? ValidateSoleArityMatchingOverload(
        FunctionCall call, IReadOnlyList<FunctionSymbol> initMethods,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes,
        int totalArgCount, TypeParameterBinding? typeBinding)
    {
        FunctionSymbol? soleMatchSymbol = null;
        List<ParameterSymbol>? soleMatch = null;
        foreach (var init in initMethods)
        {
            var parameters = init.Parameters.Skip(1).ToList();
            if (parameters.Any(p => p.IsVariadic))
                return null; // a variadic overload can absorb any count; arity decides nothing

            var required = parameters.Count(p => !p.HasDefault);
            if (totalArgCount < required || totalArgCount > parameters.Count)
                continue;

            if (soleMatch != null)
                return null; // more than one overload accepts this count — leave resolution alone

            soleMatchSymbol = init;
            soleMatch = parameters;
        }

        if (soleMatch == null)
            return null; // none fits: the count diagnostic belongs to overload resolution, not here

        ValidateCallArguments(call, soleMatch, argTypes, kwargTypes, totalArgCount, typeBinding,
            clrParameterNames: soleMatchSymbol!.ClrMethodName != null);
        return soleMatchSymbol;
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
    /// The binding a generic FUNCTION call's successful inference produced, positionally paired
    /// with the function's type parameters — the same vector
    /// <see cref="SubstituteTypeParameters"/> closes the return type with. Lets the kwarg
    /// validation in <see cref="ValidateFunctionSymbolCall"/>'s inference branch check a keyword
    /// argument against the CLOSED parameter type (#1591). A parameter inference left unclosed
    /// stays out of the substitution, so <see cref="SubstitutedParameterType"/> declines to check
    /// it — the same stay-out-of-inference's-way rule the constructor bindings follow.
    /// </summary>
    private static TypeParameterBinding InferredTypeParameterBinding(
        FunctionSymbol funcSymbol, IReadOnlyList<SemanticType> inferredTypes)
    {
        var substitution = new Dictionary<string, SemanticType>(StringComparer.Ordinal);
        for (int i = 0; i < funcSymbol.TypeParameters.Count && i < inferredTypes.Count; i++)
            substitution[funcSymbol.TypeParameters[i].Name] = inferredTypes[i];

        return new TypeParameterBinding(
            substitution,
            new HashSet<string>(funcSymbol.TypeParameters.Select(tp => tp.Name), StringComparer.Ordinal),
            Origin: null);
    }

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

        var typeParams = unionBaseSymbol.TypeParameters;
        List<SemanticType>? typeArgs = null;
        if (typeParams.Count > 0)
        {
            // Try 1: annotation slot (e.g. b: Box[int] = Box.Full(1))
            if (_expectedType is GenericType expectedGenericType
                && NamesSameDeclaration(expectedGenericType, unionBaseSymbol)
                && expectedGenericType.TypeArguments.Count == typeParams.Count)
            {
                typeArgs = expectedGenericType.TypeArguments;
            }

            // Try 2: infer from the arguments (e.g. Box.Full(1) → Box[int])
            if (typeArgs == null && caseFields.Count > 0 && argTypes.Count == caseFields.Count)
            {
                var syntheticParams = caseFields.Select(f => new ParameterSymbol
                {
                    Name = f.Name,
                    Type = f.Type,
                }).ToList();
                var syntheticFunc = new FunctionSymbol
                {
                    Name = $"{unionBaseSymbol.Name}.{caseUdt.Name}",
                    Parameters = syntheticParams,
                    TypeParameters = typeParams,
                };
                var inferenceResult = _genericInference.InferTypeArguments(syntheticFunc, argTypes);
                if (inferenceResult.Success && inferenceResult.InferredTypes != null)
                {
                    typeArgs = inferenceResult.InferredTypes;
                }
            }

            // Inference failed — report SPY0227 with an annotation steer
            if (typeArgs == null)
            {
                var typeParamNames = string.Join(", ", typeParams.Select(tp => tp.Name));
                AddError(
                    $"Cannot infer type arguments for '{unionBaseSymbol.Name}.{caseUdt.Name}'; " +
                    $"add a type annotation (e.g., x: {unionBaseSymbol.Name}[{typeParamNames}] = {unionBaseSymbol.Name}.{caseUdt.Name}(...))",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.CannotInferType,
                    span: call.Span);
                return SemanticType.Unknown;
            }
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

        // For generic unions, return the closed GenericType
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
        // #1671: a COLLECTION LITERAL (or comprehension) written as an argument may take its
        // contextual type only from a RESOLVED callee. `earlyFuncSymbol` and `calleeFunctionType`
        // are one arbitrary candidate's signature when the callee names an overload set — the
        // first `mean` of `statistics.mean(list[float] | list[int] | list[long])`, the first `m` of
        // two same-named methods — so recording the literal with that candidate's element type
        // lets the candidate type the argument and then lets that type select the overload,
        // making the answer depend on declaration order. Such an argument is left to type from its
        // own elements, and `overload_resolution.md`'s applicability + betterness decide.
        //
        // The gate is on the literal, not on the expectation: a lambda or a method-group argument
        // has no type WITHOUT its expectation and is re-resolved after inference by the deferred
        // machinery (#1161, #1589), so those keep the expectation they have always had. Evaluated
        // only when such an argument is present, so the predicate's CLR-reflection arm stays off
        // the path of every ordinary call.
        var hasContextTypedCollectionArgument =
            call.Arguments.Any(TakesContextualCollectionType)
            || call.KeywordArguments.Any(k => TakesContextualCollectionType(k.Value));
        var calleeDenotesOverloadSet =
            hasContextTypedCollectionArgument && CalleeDenotesOverloadSet(callee, call);

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
                var previousParameterTypedArgument = _parameterTypedArgument;

                // Cleared up front, so the arms below can only ever set it TOGETHER with the
                // parameter type they push. The `else` of those arms leaves `_expectedType` holding
                // the ENCLOSING context's expectation, which is not this argument's parameter type
                // — see the field's own comment.
                _parameterTypedArgument = null;

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
                    _parameterTypedArgument = previousParameterTypedArgument;
                    continue;
                }

                var noCandidateExpectation = calleeDenotesOverloadSet
                    && TakesContextualCollectionType(call.Arguments[argIdx]);

                if (!noCandidateExpectation
                    && earlyFuncSymbol != null && argIdx + earlyParamOffset < earlyFuncSymbol.Parameters.Count)
                {
                    var paramType = earlyFuncSymbol.Parameters[argIdx + earlyParamOffset].Type;
                    _expectedType = paramType is UnknownType ? null : paramType;
                    _parameterTypedArgument = ParameterTypedArgumentOf(paramType, call.Arguments[argIdx]);
                }
                else if (!noCandidateExpectation
                    && calleeFunctionType != null && argIdx < calleeFunctionType.ParameterTypes.Count)
                {
                    var paramType = calleeFunctionType.ParameterTypes[argIdx];
                    _expectedType = paramType is UnknownType ? null : paramType;
                    _parameterTypedArgument = ParameterTypedArgumentOf(paramType, call.Arguments[argIdx]);
                }
                else if (noCandidateExpectation)
                {
                    // The ENCLOSING context's expectation is not this argument's parameter type
                    // either, and leaving it in place would type the literal from it.
                    _expectedType = null;
                }
                argTypes.Add(CheckExpression(call.Arguments[argIdx]));
                _expectedType = previousExpectedType;
                _parameterTypedArgument = previousParameterTypedArgument;
            }
        }

        // Check keyword arguments and collect their types
        var kwargTypes = new Dictionary<string, SemanticType>();
        foreach (var kwarg in call.KeywordArguments)
        {
            // Python refuses a repeated keyword outright (`f(x=1, x=2)` is "SyntaxError: keyword
            // argument repeated"); the dictionary below would otherwise keep the LAST value and
            // silently drop the rest (#1591). Runs here, on the collection pass every call takes,
            // so a duplicate refuses regardless of which resolution route later validates the
            // names. Identity is the written spelling — two spellings of one parameter are a
            // spelling error at the binding seam, not a duplicate here.
            if (kwargTypes.ContainsKey(kwarg.Name))
            {
                AddError($"Duplicate keyword argument '{kwarg.Name}'",
                    kwarg.LineStart, kwarg.ColumnStart, code: DiagnosticCodes.Semantic.DuplicateArgument,
                    span: kwarg.Span ?? kwarg.Value.Span);
            }

            var previousExpectedType = _expectedType;
            var previousParameterTypedArgument = _parameterTypedArgument;
            _parameterTypedArgument = null;
            if (calleeDenotesOverloadSet && TakesContextualCollectionType(kwarg.Value))
            {
                _expectedType = null;
            }
            else if (earlyFuncSymbol != null)
            {
                var param = FindKeywordParameter(earlyFuncSymbol.Parameters, kwarg.Name);
                if (param != null)
                {
                    _expectedType = param.Type is UnknownType ? null : param.Type;
                    _parameterTypedArgument = ParameterTypedArgumentOf(param.Type, kwarg.Value);
                }
            }
            kwargTypes[kwarg.Name] = CheckExpression(kwarg.Value);
            _expectedType = previousExpectedType;
            _parameterTypedArgument = previousParameterTypedArgument;
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
                if (_symbolTable.Lookup(id.Name) is FunctionSymbol lookupFs
                    && SemanticBinding.HasCodeGenInfo(lookupFs))
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
        if (OperandView(argType) == SemanticType.Str)
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
        return receiverType != null && OperandView(receiverType) == SemanticType.Str ? IterablePositionZero : null;
    }

    /// <summary>
    /// Whether <paramref name="argument"/> is an expression whose recorded type an enclosing
    /// expected type can OVERRIDE: a non-empty collection literal or comprehension. Such an
    /// expression has a type of its own (its elements'), and #1671 lets a contextual type replace
    /// it — which is exactly the override an unresolved candidate must not make.
    ///
    /// <para>An EMPTY literal is excluded: it has no type of its own, so its contextual type is
    /// not an override but its only source (<see cref="TryInferEmptyCollectionType"/>), and denying
    /// it would turn working calls into SPY0227.</para>
    /// </summary>
    private static bool TakesContextualCollectionType(Expression argument) =>
        UnwrapParenthesized(argument) switch
        {
            ListLiteral list => list.Elements.Length > 0,
            SetLiteral set => set.Elements.Length > 0,
            DictLiteral dict => dict.Entries.Length > 0,
            TupleLiteral tuple => tuple.Elements.Length > 0,
            ListComprehension or SetComprehension or DictComprehension or DictSpreadComprehension => true,
            _ => false
        };

    /// <summary>
    /// Whether <paramref name="callee"/> denotes an overload <b>set</b> rather than one resolved
    /// target. This is the gate on contextual (expected-type) information flowing into a call's
    /// arguments: a literal's contextual type may come only from a RESOLVED target — a single
    /// candidate, or an overload already chosen — never from a candidate set (#1671).
    ///
    /// <para>Without the gate the checker records an argument's type from whichever candidate the
    /// name happened to bind to, and overload resolution then selects on that recorded type: with
    /// <c>def h(xs: list[float])</c> declared before <c>def h(xs: list[int])</c>, <c>h([1, 2])</c>
    /// bound the <c>float</c> overload purely because it was written first, while the same two
    /// declarations in the other order bound the <c>int</c> one. Argument types are computed from
    /// the arguments alone; applicability and betterness (<c>overload_resolution.md</c>) then run
    /// on those types.</para>
    ///
    /// <para>The candidate sets consulted are the ones the resolution routes themselves read:
    /// user and imported function overloads (<see cref="SymbolTable.LookupFunctionOverloads"/>),
    /// builtin overloads (<see cref="Registry.BuiltinRegistry.GetFunctionOverloads"/>), module
    /// exports (<see cref="LookupModuleFunctionOverloads"/>), instance methods
    /// (<see cref="LookupInstanceMethodOverloads"/>) and reflected CLR method groups.</para>
    /// </summary>
    /// <summary>
    /// Whether <paramref name="callee"/> denotes an overload <b>set</b> rather than one resolved
    /// target. This is the gate on contextual (expected-type) information flowing into a call's
    /// arguments: a literal's contextual type may come only from a RESOLVED target — a single
    /// candidate, or an overload already chosen — never from a candidate set (#1671).
    ///
    /// <para>Without the gate the checker records an argument's type from whichever candidate the
    /// name happened to bind to, and overload resolution then selects on that recorded type: with
    /// <c>def h(xs: list[float])</c> declared before <c>def h(xs: list[int])</c>, <c>h([1, 2])</c>
    /// bound the <c>float</c> overload purely because it was written first, while the same two
    /// declarations in the other order bound the <c>int</c> one. Argument types are computed from
    /// the arguments alone; applicability and betterness (<c>overload_resolution.md</c>) then run
    /// on those types.</para>
    ///
    /// <para>The candidate sets consulted are the ones the resolution routes themselves read:
    /// user and imported function overloads (<see cref="SymbolTable.LookupFunctionOverloads"/>),
    /// builtin overloads (<see cref="Registry.BuiltinRegistry.GetFunctionOverloads"/>), module
    /// exports (<see cref="LookupModuleFunctionOverloads"/>), instance methods
    /// (<see cref="LookupInstanceMethodOverloads"/>) and reflected CLR method groups.</para>
    /// </summary>
    /// <summary>
    /// Whether the bare name <paramref name="name"/> is answered by more than one declaration —
    /// a user/imported overload list, or a builtin overload set the name actually denotes (a user
    /// symbol shadowing a builtin name is its own, single target: SPY0212's rule). Unlike
    /// <see cref="CalleeDenotesOverloadSet"/> this asks nothing about the call site, because its
    /// caller holds an ARBITRARILY bound member of the set rather than the applicable one.
    /// </summary>
    private bool NameDenotesMultipleDeclarations(string name, FunctionSymbol bound)
    {
        if (_symbolTable.LookupFunctionOverloads(name) is { Count: > 1 })
            return true;

        var builtinOverloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(name);
        return builtinOverloads is { Count: > 1 } && builtinOverloads.Contains(bound);
    }

    private bool CalleeDenotesOverloadSet(Expression callee, FunctionCall call)
    {
        switch (callee)
        {
            case Identifier id:
                {
                    if (IsUnresolvedSet(_symbolTable.LookupFunctionOverloads(id.Name), call))
                        return true;

                    // A builtin overload set counts only when the bare spelling actually denotes it:
                    // a user symbol that shadows the name is its own, single target (SPY0212's rule).
                    var builtinOverloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
                    if (builtinOverloads is { Count: > 1 })
                    {
                        var bound = _symbolTable.Lookup(id.Name) as FunctionSymbol;
                        return (bound == null || builtinOverloads.Contains(bound))
                            && IsUnresolvedSet(builtinOverloads, call);
                    }

                    return false;
                }

            case MemberAccess memberAccess:
                {
                    var rawReceiverType = _semanticInfo.GetExpressionType(memberAccess.Object);
                    if (rawReceiverType == null)
                        return false;
                    // Same receiver chain ResolveUserMethodOverload walks, so the set consulted here is
                    // the set that will actually resolve the call.
                    var receiverType = UnwrapCallTarget(rawReceiverType);

                    if (IsUnresolvedSet(LookupModuleFunctionOverloads(receiverType, memberAccess.Member), call))
                        return true;
                    if (IsUnresolvedSet(LookupInstanceMethodOverloads(receiverType, memberAccess.Member), call))
                        return true;

                    // Reflected CLR overloads on a CLR-backed receiver — the same method group
                    // BclMemberTypeOnBuiltinReceiver declines to type (ClrMemberResolution.MethodGroup),
                    // reached through the one receiver→CLR-type resolution both share.
                    if ((ClrReceiverTypeOf(receiverType) ?? InheritedClrReceiverTypeOf(receiverType)) is { } clrReceiver)
                    {
                        var resolver = new Discovery.ClrMemberTypeResolver(_bclGenericMethodBridge);
                        return resolver.Resolve(clrReceiver, memberAccess.Member, ClrReceiverKindOf(memberAccess))
                                is Discovery.ClrMemberResolution.MethodGroup group
                            && ArityApplicableCount(group.Candidates, call) > 1;
                    }

                    return false;
                }

            default:
                return false;
        }
    }

    /// <summary>
    /// Whether <paramref name="candidates"/> still holds more than one candidate after the ONE
    /// applicability test that needs no argument types: arity.
    ///
    /// <para>Arity is syntactic — the call site's argument count is known before any argument is
    /// checked. When exactly one candidate can accept that count, the callee is resolved by arity
    /// alone and no argument type can change the selection, so its parameter types are a resolved
    /// target's and may be pushed as expectations (this is what keeps the deferred callable-argument
    /// selection of #1589 working for <c>map(bytes, sizes)</c>: only the two-argument <c>map</c>
    /// accepts two arguments). When two or more survive, the selection depends on the argument
    /// types, and those types must not be derived from any candidate (#1671).</para>
    ///
    /// <para>A spread argument (<c>f(*xs)</c>) expands to an unknown number of arguments, so arity
    /// narrows nothing and the whole set counts.</para>
    /// </summary>
    private static bool IsUnresolvedSet(IReadOnlyList<FunctionSymbol>? candidates, FunctionCall call)
        => candidates is { Count: > 1 } && ArityApplicableCount(candidates, call) > 1;

    private static int CallSiteArgumentCount(FunctionCall call)
        => call.Arguments.Length + call.KeywordArguments.Length;

    private static int ArityApplicableCount(IReadOnlyList<FunctionSymbol> candidates, FunctionCall call)
    {
        if (call.Arguments.Any(a => a is SpreadElement))
            return candidates.Count;

        var argCount = CallSiteArgumentCount(call);
        var applicable = 0;
        foreach (var candidate in candidates)
        {
            // `self` is never one of the call's arguments; the parameter lists of instance methods
            // carry it, module functions and builtins do not.
            var parameters = candidate.Parameters.Count > 0
                && candidate.Parameters[0].Name == PythonNames.Self
                    ? candidate.Parameters.Skip(1).ToList()
                    : (IReadOnlyList<ParameterSymbol>)candidate.Parameters;

            var required = parameters.Count(p => !p.HasDefault && !p.IsVariadic);
            var isVariadic = parameters.Any(p => p.IsVariadic);
            if (isVariadic ? argCount >= required : argCount >= required && argCount <= parameters.Count)
                applicable++;
        }

        return applicable;
    }

    private static int ArityApplicableCount(
        IReadOnlyList<System.Reflection.MethodInfo> candidates, FunctionCall call)
    {
        if (call.Arguments.Any(a => a is SpreadElement))
            return candidates.Count;

        var argCount = CallSiteArgumentCount(call);
        var applicable = 0;
        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            var isVariadic = parameters.Length > 0
                && parameters[^1].IsDefined(typeof(System.ParamArrayAttribute), inherit: false);
            var required = parameters.Count(p => !p.IsOptional)
                - (isVariadic ? 1 : 0);
            if (isVariadic ? argCount >= required : argCount >= required && argCount <= parameters.Length)
                applicable++;
        }

        return applicable;
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
            // (What this symbol may and may not contribute to a context-sensitive argument is
            // decided at the one seam that pushes expectations — see CheckCallArguments' #1671
            // gate — so this arm keeps its original, unrelated job.)
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
            {
                ValidateInitializerKeywordArguments(call, GetBaseType(_currentClass));

                // #1594: fire SPY0466 on super().__init__() when the base __init__ is @deprecated.
                var baseType = GetBaseType(_currentClass);
                if (baseType != null)
                {
                    var baseInit = baseType.Methods.FirstOrDefault(m => m.Name == DunderNames.Init);
                    if (baseInit != null)
                        CheckDeprecatedUsage(baseInit, call);
                }
            }
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

                // #1594: fire SPY0466 on self.__init__() when the class's own __init__ is @deprecated.
                var selfInit = _currentClass.Methods.FirstOrDefault(m => m.Name == DunderNames.Init);
                if (selfInit != null)
                    CheckDeprecatedUsage(selfInit, call);
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

    /// <summary>
    /// Tries to resolve <c>obj(args)</c> through a <c>__call__</c> dunder method on the callee's
    /// type. Returns the call's return type if <c>__call__</c> is found and validated; null otherwise.
    /// Records <see cref="CallableObjectDispatch"/> so the emitter emits <c>obj.Invoke(args)</c>.
    ///
    /// <para><c>obj(args)</c> IS the member call <c>obj.__call__(args)</c>, so it resolves through
    /// the same machinery <c>obj.m(args)</c> does rather than a private re-implementation of it
    /// (#1672): <see cref="FindMethodOverloadsInHierarchy"/> and
    /// <see cref="TypeHierarchyService.FindMethod"/> for the base/interface walk,
    /// <see cref="ResolveOverloadCore"/> for the overload set, and
    /// <see cref="ValidateCallArguments"/> for arity, defaults, <c>*args</c>, keyword names and
    /// argument types. The hand-rolled loop this replaced saw only the type's OWN methods, took the
    /// first same-named candidate, and never read <paramref name="kwargTypes"/> at all — so an
    /// inherited <c>__call__</c> was "not callable", a keyword argument reached codegen unbound
    /// (CS7036 behind SPY0908), <c>*args</c> was an arity error, and an overload pair resolved to
    /// whichever member was declared first.</para>
    ///
    /// <para>Writing <c>obj.__call__(args)</c> explicitly stays refused (SPY0427,
    /// <c>dunder_invocation_rules.md</c>) — that refusal is on the member-access seam and is
    /// unaffected by this route.</para>
    /// </summary>
    private SemanticType? TryResolveCallableObject(
        SemanticType calleeType, FunctionCall call,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes, int totalArgCount)
    {
        TypeSymbol? typeSymbol;
        List<SemanticType>? typeArgs = null;
        if (calleeType is UserDefinedType { Symbol: { } udt })
        {
            typeSymbol = udt;
        }
        else if (calleeType is GenericType)
        {
            var (resolved, resolvedTypeArgs) = ResolveBuiltinTypeInfo(calleeType);
            typeSymbol = resolved;
            typeArgs = resolvedTypeArgs;
        }
        else
        {
            return null;
        }

        if (typeSymbol == null)
            return null;

        Func<SemanticType, SemanticType>? typeSubstitution = null;
        if (typeArgs != null && typeSymbol.TypeParameters.Count > 0)
        {
            var capturedTypeSymbol = typeSymbol;
            var capturedTypeArgs = typeArgs;
            typeSubstitution = t => SubstituteTypeParameters(t, capturedTypeSymbol.TypeParameters, capturedTypeArgs);
        }

        FunctionSymbol? callMethod;
        var overloads = FindDunderOverloadsInHierarchy(typeSymbol, DunderNames.Call);
        if (overloads is { Count: > 1 })
        {
            var kwNames = ExtractKeywordArgNames(call);
            var (matchingOverload, arityCandidates, isAmbiguous) = ResolveOverloadCore(
                new OverloadResolutionContext(overloads, totalArgCount, argTypes,
                    SkipSelfParam: true, TypeSubstitution: typeSubstitution,
                    SkipUnknownTypes: true, KeywordArgNames: kwNames, Call: call));

            if (isAmbiguous || matchingOverload == null)
            {
                ReportOverloadError(DunderNames.Call, call, isAmbiguous, arityCandidates, totalArgCount);
                return SemanticType.Unknown;
            }

            callMethod = matchingOverload;
        }
        else
        {
            (callMethod, _) = TypeHierarchyService.FindMethod(typeSymbol, DunderNames.Call, SemanticBinding);
        }

        if (callMethod == null)
            return null;

        // The call site writes no receiver, so `self` is not one of its arguments — the same
        // skipLeading rule the member seam applies when it types `obj.m` as a FunctionType.
        var selfOffset = callMethod.Parameters.Count > 0
                         && callMethod.Parameters[0].Name == PythonNames.Self ? 1 : 0;
        var parameters = selfOffset == 0
            ? (IReadOnlyList<ParameterSymbol>)callMethod.Parameters
            : callMethod.Parameters.Skip(selfOffset).ToList();

        ValidateCallArguments(call, parameters, argTypes, kwargTypes, totalArgCount,
            clrParameterNames: callMethod.ClrMethodName != null);

        var returnType = callMethod.ReturnType ?? SemanticType.Void;
        if (typeSubstitution != null)
            returnType = typeSubstitution(returnType);

        RecordResolvedCallTarget(call, callMethod);
        _semanticInfo.SetCallableObjectDispatch(call,
            new CallableObjectDispatch("Invoke", returnType));
        _semanticInfo.SetExpressionType(call, returnType);

        return returnType;
    }

    /// <summary>
    /// Single seam for recording a resolved call-node target: records the target for codegen AND
    /// runs the deprecation check. Every call-node resolution route (single-candidate, overload,
    /// generic-function-type, pipe-forward) MUST go through this helper rather than calling
    /// <see cref="SemanticInfo.SetCallTarget"/> directly, so a future route inherits the deprecation
    /// check by construction (#1438). The construction route checks type-symbol and function-symbol
    /// deprecation directly via <see cref="CheckDeprecatedUsage"/> (#1536) without recording a call
    /// target — recording __init__ as a call target would make emitter consumers see targets on
    /// constructor-call nodes, an unmeasured blast radius for zero benefit.
    /// </summary>
    private void RecordResolvedCallTarget(FunctionCall call, FunctionSymbol symbol)
    {
        _semanticInfo.SetCallTarget(call, symbol);
        CheckDeprecatedUsage(symbol, call);
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
            MarkExpressionAsErrorRecovery(call,
                ErrorRecoveryReason.Propagated("the call target's type"));
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

        // The full spec (#1520): remaining parameters (name + resolved C# name + type, matching
        // the result FunctionType's vector positionally) and the keyword fixes with their resolved
        // C# parameter names. The emitter reads this verbatim — no Parameters walk, no name
        // re-resolution at emit time.
        var remainingParameters =
            new List<(string Name, string CSharpName, SemanticType Type)>(resultType.ParameterTypes.Count);
        if (targetFunctionSymbol != null)
        {
            var parameters = targetFunctionSymbol.Parameters;
            for (var i = Math.Min(fixedPositionalCount, parameters.Count); i < parameters.Count; i++)
            {
                var p = parameters[i];
                if (fixedKwargNames.Contains(p.Name))
                    continue;
                if (remainingParameters.Count >= resultType.ParameterTypes.Count)
                    break;
                remainingParameters.Add((p.Name,
                    ResolveCSharpParameterName(p.Name, targetFunctionSymbol),
                    resultType.ParameterTypes[remainingParameters.Count]));
            }
        }
        while (remainingParameters.Count < resultType.ParameterTypes.Count)
        {
            var syntheticName = $"__partial_arg{remainingParameters.Count}";
            remainingParameters.Add((syntheticName,
                NameMangler.ToCamelCase(syntheticName),
                resultType.ParameterTypes[remainingParameters.Count]));
        }

        var fixedKeywords = new List<(string CSharpName, int ArgumentIndex)>(call.KeywordArguments.Length);
        for (var i = 0; i < call.KeywordArguments.Length; i++)
        {
            fixedKeywords.Add((
                ResolveCSharpParameterName(call.KeywordArguments[i].Name, targetFunctionSymbol),
                i));
        }

        _semanticInfo.SetFunctoolsPartialSpec(call, new FunctoolsPartialSpec(
            targetFunctionSymbol, fixedPositionalCount, remainingParameters, fixedKeywords));
        return resultType;
    }

    /// <summary>
    /// Resolves the C# parameter name a Sharpy-spelled keyword binds to on <paramref name="funcSymbol"/>.
    /// For CLR-backed targets (<c>ClrMethodName != null</c>) reflection parameter names are the
    /// actual C# identifiers stored UNMANGLED (#942), so the declared name is matched and used
    /// verbatim (keyword-escaped); Sharpy-defined parameters camelCase-mangle. The decision half of
    /// the emitter's <c>GetCSharpParameterName</c>, made at check time for the partial spec (#1520).
    /// </summary>
    private static string ResolveCSharpParameterName(string sharpyName, FunctionSymbol? funcSymbol)
    {
        if (funcSymbol?.ClrMethodName != null)
        {
            var camelName = NameMangler.ToCamelCase(sharpyName);
            var match = funcSymbol.Parameters.FirstOrDefault(p => p.Name == sharpyName)
                ?? funcSymbol.Parameters.FirstOrDefault(p => p.Name == camelName);
            if (match != null)
                return CSharpKeywords.EscapeIfNeeded(match.Name);
        }
        return NameMangler.ToCamelCase(sharpyName);
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
    /// itself into the argument position (#1182). The spread-operand entry IS reachable and
    /// load-bearing (#1336): <c>f(*SomeClass)</c> on a multi-constructor class reaches
    /// <see cref="RefuseUnpinnedCallArgument"/>'s distinct message through the spread-operand
    /// membership query, pinned by the <c>spread_constructor_ref_1336</c> fixture.
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

    /// <summary>
    /// The node to record in <c>_parameterTypedArgument</c> when <paramref name="parameterType"/> is
    /// pushed as <c>_expectedType</c> for <paramref name="argument"/>: the unwrapped argument, or
    /// null for an <see cref="UnknownType"/> parameter — that arm pushes a null
    /// <c>_expectedType</c>, so there is no parameter type to bind to.
    /// </summary>
    private static Expression? ParameterTypedArgumentOf(SemanticType parameterType, Expression argument)
        => parameterType is UnknownType ? null : UnwrapParenthesized(argument);

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

    // ============================================================
    // CLR instance-method calls: arity and argument types (#1290)
    // ============================================================

    /// <summary>
    /// What a member call on a CLR receiver could bind to: the public instance methods answering to
    /// the written name, and whether an extension method of that name is reachable from the emitted
    /// compilation. Memoized together because they are computed from the same reflection and consumed
    /// by the same decision.
    /// </summary>
    private sealed record ClrInstanceCallSurface(
        System.Reflection.MethodInfo[] Candidates, bool ExtensionNameReachable);

    // The PRESENT-member companion of the #1141 absence memo (_bclMemberAbsenceMemo in
    // TypeChecker.cs): same memo pattern, asked at the same seam, of the same reflection. Keyed on
    // the CONSTRUCTED receiver type rather than its TypeSymbol, because that is what distinguishes
    // the signatures being checked — List[int].Add and List[str].Add share one TypeSymbol and take
    // different arguments.
    private readonly Dictionary<(Type, string), ClrInstanceCallSurface> _clrInstanceCallMemo = new();

    /// <summary>
    /// Checks the ARITY and ARGUMENT TYPES of a call whose member sits on a CLR-backed receiver and
    /// which nothing else typed — the last call seam that had no check of its own. On the name-only
    /// interop channel the emitter writes the call verbatim and Roslyn performs the only binding
    /// check it ever gets, so <c>xs.add("not an int")</c> on an imported <c>List[int]</c> came back
    /// as CS1503 behind SPY0908: the compiler reporting its own bug for a user's type error (#1290).
    ///
    /// <para>The absence half of that issue (#1141's proof, wired into the member seam) refuses a
    /// member reflection can prove is not there. This is the PRESENT case — the member exists, and
    /// the question is whether THIS call binds to it — so the candidate set comes from the same
    /// constructed receiver that proof reflects on, memoized the same way.</para>
    ///
    /// <para>The selection rule is #1243's, verbatim: when exactly one arity fits, the user's intent
    /// is not in doubt and a wrong argument is as diagnosable as it is for a single declared method;
    /// two candidates of the same arity keep silence, because choosing between them is CLR overload
    /// resolution and this seam does not own it.</para>
    ///
    /// <para>Every other step defaults to silence too — an unreflectable or open-generic receiver, a
    /// spread argument, a name an extension method could also answer, a <c>ref</c> or
    /// delegate or <c>params</c> parameter, a bridge mapping that collapsed to <c>object</c>. A false
    /// refusal here rejects interop .NET binds happily, which is strictly worse than the ICE it would
    /// replace (#1260), so an undecidable step is left to Roslyn rather than guessed at.</para>
    ///
    /// <para>Keyword arguments used to ride the same silence as deliberate permissiveness. The #1591
    /// ruling overturned that for their NAMES — one spelling, the Python one, validated below against
    /// the reflected candidate parameter names — while their presence still keeps arity and types
    /// unchecked, because a keyword argument binds by name and a positional count check would read
    /// the call wrong. Binding is unchanged wherever the names are valid.</para>
    /// </summary>
    private void CheckClrInstanceMethodCall(
        FunctionCall call, MemberAccess memberAccess,
        List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes)
    {
        // A keyword argument binds by CLR parameter name, and a spread occupies one argument slot
        // while standing for however many the sequence holds — so neither the count nor the
        // positions mean here what the arity/type checks below would read them as. A spread (or a
        // count the argument walk disagreed on) leaves the call exactly as permissive as it is
        // today. Keyword arguments no longer share that bail wholesale: their NAMES are
        // position-independent and validate against the reflected surface below (#1591); only the
        // arity/type checks stay off in their presence.
        var hasKeywordArguments = kwargTypes.Count > 0 || call.KeywordArguments.Length > 0;
        if (!hasKeywordArguments
            && (call.Arguments.Length != argTypes.Count
                || call.Arguments.Any(argument => argument is SpreadElement)))
        {
            return;
        }

        // A static call through a type name (`Console.write_line(...)`) reaches a different resolver
        // with a different receiver; this seam is about instance members.
        if (memberAccess.Object is Identifier staticId
            && _semanticInfo.GetIdentifierSymbol(staticId) is TypeSymbol)
        {
            return;
        }

        // `obj?.method()` binds the member on the underlying type, which is what the member seam
        // itself looked the call up on.
        var receiverType = _semanticInfo.GetExpressionType(memberAccess.Object) switch
        {
            NullableType nullableReceiver => nullableReceiver.UnderlyingType,
            OptionalType optionalReceiver => optionalReceiver.UnderlyingType,
            var other => other
        };
        if (receiverType is null or UnknownType)
            return;

        var reflectionType = ClrReceiverReflectionType(receiverType);
        if (reflectionType == null)
            return;

        var surface = ClrInstanceCallSurfaceOf(reflectionType, memberAccess.Member);

        // No candidate at all: a property, a field, a member only codegen can resolve, or one that is
        // genuinely absent — and absence is the member seam's question, answered there (#1141).
        if (surface.Candidates.Length == 0)
            return;

        // The permissive extension clause, for the same reason the absence proof has it: an extension
        // method binds when no instance overload is applicable, so `xs.contains(v, comparer)` is a
        // legal call to Enumerable.Contains that no instance candidate accounts for. Refusing on the
        // instance surface alone would reject it — and its parameter names could answer a keyword
        // name the instance candidates cannot, so the name validation below stays behind this bail
        // too.
        if (surface.ExtensionNameReachable)
            return;

        // Keyword NAMES validate against the union of every candidate's parameters — permissive
        // across overloads on purpose: which overload the call means is CLR overload resolution's
        // answer, so only a name NO candidate can bind refuses (#1591). Arity and types stay
        // unchecked in the kwargs' presence, exactly as before.
        if (hasKeywordArguments)
        {
            ValidateClrKeywordArgumentNames(call,
                surface.Candidates
                    .SelectMany(candidate => candidate.GetParameters())
                    .Select(parameter => parameter.Name)
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToList());
            return;
        }

        var fitting = surface.Candidates
            .Where(candidate => ClrArityFits(candidate, argTypes.Count))
            .ToList();

        var memberDisplay = $"{Shared.ClrNameHelper.StripArity(reflectionType.Name)}.{memberAccess.Member}";

        if (fitting.Count == 0)
        {
            AddError(
                $"'{memberDisplay}' expects {DescribeClrArities(surface.Candidates)} but got {argTypes.Count}",
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: call.Span);
            return;
        }

        // Two overloads of this arity: which one the call means is CLR overload resolution's answer,
        // not this seam's (#1243). Reporting against a guess would be worse than the silence.
        if (fitting.Count > 1)
            return;

        CheckClrCallArgumentTypes(call, fitting[0], argTypes, memberDisplay);
    }

    /// <summary>
    /// Checks the ARITY and ARGUMENT TYPES of a STATIC call on a CLR type name, and types its
    /// result — the static twin of <see cref="CheckClrInstanceMethodCall"/> (#1451). Returns
    /// <c>null</c>, leaving the call exactly as permissive as it was, for every shape it cannot
    /// decide.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The instance seam deliberately skips a static call ("reaches a different resolver with a
    /// different receiver"), and nothing checked that other resolver: three arguments to a
    /// two-argument overload set was SILENT at semantic time and came back as CS1501 behind
    /// SPY0908. Worse than the ICE, the call was typed <c>Unknown</c>, so its value was assignable
    /// to anything and every downstream slot went unchecked too.
    /// </para>
    /// <para>
    /// <b>The instance seam's shape, verbatim, including its conservatism.</b> #1243's rule decides:
    /// exactly one candidate of the call's arity means the user's intent is not in doubt, and TWO
    /// keep silence, because choosing between them is CLR overload resolution and this seam does not
    /// own it. A member with no candidate at all is not this seam's question either — absence is
    /// answered by #1141's proof at the member seam. The conservatism is the point: a false refusal
    /// at an interop seam rejects a call .NET binds happily, which is strictly worse than the ICE it
    /// replaces (#1260, #1243).
    /// </para>
    /// <para>
    /// <b>The char row (#1402) is now a special case of this arm rather than its whole scope.</b>
    /// That row is the REVERSE direction of the #1291 char family: a <c>str</c> going into a CLR
    /// <c>char</c> has no char-producing expression to key on, so the fact lives on the PARAMETER
    /// and is decided here and recorded on the argument node. Only a ONE-character string literal
    /// converts — <c>Char.to_upper("abc")</c> has no correct char to pass, and taking the first
    /// character would be Sharpy inventing a truncation .NET never asked for. "Single character"
    /// means a single UTF-16 code unit per Axiom 1's string model, so a non-BMP scalar
    /// ("&#128512;", Length 2) is refused alongside longer strings: a surrogate pair cannot fit a
    /// CLR <c>char</c> at all. Those arguments are handled here and EXCLUDED from the general
    /// argument check, which would otherwise report the same <c>str</c>/<c>char</c> mismatch a
    /// second time in its own words.
    /// </para>
    /// </remarks>
    private SemanticType? ClrStaticCallType(
        FunctionCall call, MemberAccess memberAccess, List<SemanticType> argTypes)
    {
        // A keyword argument binds by CLR parameter name and a spread stands for however many
        // arguments the sequence holds, so neither the count nor the positions mean here what this
        // seam would read them as.
        if (call.KeywordArguments.Length > 0
            || call.Arguments.Length != argTypes.Count
            || call.Arguments.Any(argument => argument is SpreadElement))
        {
            return null;
        }

        if (memberAccess.Object is not Identifier typeName
            || _semanticInfo.GetIdentifierSymbol(typeName) is not TypeSymbol { ClrType: { } clrType })
        {
            return null;
        }

        var methodName = Discovery.ClrTypeHelper.ResolveClrMethodName(clrType, memberAccess.Member);
        if (methodName == null)
            return null;

        var surface = ClrStaticCallSurfaceOf(clrType, methodName);

        // No candidate under this name at all: a property, a field, a member only codegen resolves,
        // or one that is genuinely absent — and absence is the member seam's question, answered
        // there (#1141). Not this seam's to refuse.
        if (surface.Length == 0)
            return null;

        var memberDisplay = $"{Shared.ClrNameHelper.StripArity(clrType.Name)}.{memberAccess.Member}";

        var candidates = surface
            .Where(m => ClrArityFits(m, argTypes.Count))
            .ToList();

        // The arity check the static receiver never had (#1451). `Char.is_digit("a", 0, 5)` against
        // a one- and two-argument overload set reached Roslyn as CS1501 behind SPY0908.
        if (candidates.Count == 0)
        {
            AddError(
                $"'{memberDisplay}' expects {DescribeClrArities(surface)} but got {argTypes.Count}",
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.Semantic.WrongArgumentCount,
                span: call.Span);
            return SemanticType.Unknown;
        }

        if (candidates.Count > 1)
        {
            // #1530: argument-driven unique-candidate selection. For each candidate, check the
            // mapped parameter types against the argument types. Only a parameter that
            // MapClrParameterType cannot express (null) counts as accepting; every MAPPED
            // parameter adjudicates. This is deliberately narrower than the plan's
            // ClrParameterIsUndecidable rule — that predicate calls `decimal` undecidable (it
            // declares op_Implicit), which would leave Math.floor(1.5) ambiguous and contradict
            // the plan's own acceptance example. A mapped parameter is judged the same way the
            // unique-candidate seam (CheckClrCallArgumentTypes) judges it: the Sharpy-vocabulary
            // acceptance, refuted when the mapping is lossy and .NET rejects the argument's own
            // CLR type (enum→int manufactured false uniqueness, #1573), and rescued by .NET when
            // the mapping lost a relation .NET has (a real enum value against the enum's `int`
            // spelling, which the mapped check alone refused).
            var argumentCompatible = candidates.Where(c =>
            {
                var ps = c.GetParameters();
                for (int i = 0; i < argTypes.Count && i < ps.Length; i++)
                {
                    if (IsClrParamsArray(ps[i]))
                        break;
                    if (!ClrParameterAccepts(ps[i], argTypes[i], ArgumentNodeAt(call, i)))
                        return false;
                }
                return true;
            }).ToList();

            // #1573: round-trip verification via CLR reflection. The mapped-type
            // filter can produce false positives from lossy bridge arms (enum→int,
            // MemberInfo→object). Verify each candidate's CLR parameters actually
            // accept the emitted argument types. When at least one arg has a resolvable
            // CLR type, the round-trip is authoritative and the result replaces the
            // mapped-type set; otherwise fall through with the mapped set intact.
            if (argumentCompatible.Count >= 1)
            {
                var anyArgHasClrType = argTypes.Any(a => TryGetClrType(a) != null);
                if (anyArgHasClrType)
                {
                    var roundTripped = argumentCompatible.Where(c =>
                    {
                        var ps = c.GetParameters();
                        for (int i = 0; i < argTypes.Count && i < ps.Length; i++)
                        {
                            if (IsClrParamsArray(ps[i]))
                                break;
                            var argClrType = TryGetClrType(argTypes[i]);
                            if (argClrType == null)
                                continue;
                            if (!ps[i].ParameterType.IsAssignableFrom(argClrType))
                                return false;
                        }
                        return true;
                    }).ToList();
                    if (roundTripped.Count > 0)
                        argumentCompatible = roundTripped;
                }
            }

            // Prefer non-params candidates over params candidates — matches C#'s
            // preference for the more specific overload (e.g. CreateInstance(Type) over
            // CreateInstance(Type, params Object[])).
            if (argumentCompatible.Count > 1)
            {
                var nonParams = argumentCompatible
                    .Where(c => !c.GetParameters().Any(p => IsClrParamsArray(p)))
                    .ToList();
                if (nonParams.Count > 0)
                    argumentCompatible = nonParams;
            }

            // Prefer candidates whose total parameter count matches the arg count
            // exactly — Dump(Object, TextFile) wins over Dump(Object, TextFile,
            // Boolean=default) when called with 2 args (standard C# preference for
            // the overload that doesn't skip optional parameters).
            if (argumentCompatible.Count > 1)
            {
                var exactArity = argumentCompatible
                    .Where(c => c.GetParameters().Length == argTypes.Count)
                    .ToList();
                if (exactArity.Count > 0)
                    argumentCompatible = exactArity;
            }

            // Best conversion target: when one candidate's parameters are all at
            // least as specific as another's, eliminate the less specific one. This
            // handles Console.WriteLine(String) vs WriteLine(Object) — String is
            // more specific, so it wins (standard C# better-conversion-target rule).
            if (argumentCompatible.Count > 1)
            {
                argumentCompatible = argumentCompatible.Where(candidate =>
                {
                    var ps = candidate.GetParameters();
                    return !argumentCompatible.Any(other =>
                    {
                        if (ReferenceEquals(other, candidate))
                            return false;
                        var ops = other.GetParameters();
                        if (ops.Length != ps.Length)
                            return false;
                        var otherIsStricter = false;
                        for (int i = 0; i < ps.Length; i++)
                        {
                            if (ps[i].ParameterType == ops[i].ParameterType)
                                continue;
                            if (ps[i].ParameterType.IsAssignableFrom(ops[i].ParameterType))
                            {
                                otherIsStricter = true;
                                continue;
                            }
                            return false;
                        }
                        return otherIsStricter;
                    });
                }).ToList();
            }

            if (argumentCompatible.Count != 1)
            {
                // #1569: refuse only when the checker could actually adjudicate. An argument whose
                // type is still being inferred (an unresolved lambda) or is an error recovery
                // (Unknown) carries no fact to adjudicate on, and Roslyn still has the delegate
                // information this seam lacks, so such a call falls through as before. Every other
                // argument — a `None`, a Sharpy value, a CLR value — was judged above.
                var cannotAdjudicate = argTypes.Any(a =>
                    a is UnknownType || (a is FunctionType argFn && argFn.HasUnresolvedTypes()));
                if (cannotAdjudicate)
                    return null;

                var survivingCandidates = argumentCompatible.Count > 0 ? argumentCompatible : candidates;
                var candidateDescriptions = string.Join(", ",
                    survivingCandidates.Select(c =>
                    {
                        var ps = c.GetParameters();
                        var paramDisplay = string.Join(", ", ps.Select(p => Shared.ClrNameHelper.StripArity(p.ParameterType.Name)));
                        return $"{c.Name}({paramDisplay})";
                    }));

                if (argumentCompatible.Count == 0)
                {
                    // Nothing survived: the arguments fit no overload, which is a no-match, not an
                    // ambiguity — the two are different user actions (change the argument vs pick
                    // between overloads), so they get the existing SPY0354 and its wording.
                    AddError(
                        $"No matching overload for '{memberDisplay}' with the given argument types. "
                        + $"Candidates: {candidateDescriptions}",
                        call.LineStart, call.ColumnStart,
                        code: DiagnosticCodes.Semantic.NoMatchingOverload,
                        span: call.Span);
                    return SemanticType.Unknown;
                }

                AddError(
                    $"Call to '{memberDisplay}' is ambiguous between {survivingCandidates.Count} overloads: " +
                    $"{candidateDescriptions}. {DescribeDisambiguatingCast(survivingCandidates, argTypes.Count)}",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.SemanticOverflow.AmbiguousClrOverload,
                    span: call.Span);
                return SemanticType.Unknown;
            }

            candidates = argumentCompatible;
        }

        var parameters = candidates[0].GetParameters();

        // The char row (#1402): a `str` argument bound to a reflected `char` parameter. Decided
        // here, on the parameter, and recorded on the argument node.
        var charArgumentIndices = new HashSet<int>();
        for (int i = 0; i < parameters.Length && i < argTypes.Count; i++)
        {
            if (parameters[i].ParameterType != typeof(char))
                continue;

            charArgumentIndices.Add(i);
            var argument = ArgumentNodeAt(call, i);
            if (argument is StringLiteral { Value.Length: 1 })
            {
                _semanticInfo.SetCharMaterialization(argument, CharMaterializationKind.Literal);
                continue;
            }

            AddError(
                $"Argument {i + 1} of '{memberDisplay}' takes a CLR 'char', which only a "
                + "single-character str literal converts to — Sharpy will not truncate a longer or "
                + "computed str",
                call.Arguments[i].LineStart, call.Arguments[i].ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: call.Arguments[i].Span);
        }

        // The argument check the instance seam already performs, now reaching the static receiver
        // too. Char parameters are skipped because the row above already answered them, in the
        // vocabulary that names the real constraint.
        CheckClrCallArgumentTypes(call, candidates[0], argTypes, memberDisplay, charArgumentIndices);

        // The call's own value. Typing it is half the fix: an untyped static call was `Unknown` and
        // therefore assignable to anything, so every downstream slot went unchecked as well. A
        // char-returning static is the same one-character str every other seam in the family
        // projects it to, which is what keeps `x: str = Char.to_upper("a")` from handing Roslyn a
        // `char` for a `string` slot.
        var returnType = Discovery.ClrDeclaredNullability.Apply(
            _bclGenericMethodBridge.MapClrTypeToSemanticType(candidates[0].ReturnType),
            Discovery.ClrDeclaredNullability.DeclaresNullableReturn(candidates[0]));
        return StaticCallResultTypeOrNull(call, returnType);
    }

    /// <summary>
    /// The type to bind to a static CLR call's result, or null to leave it <c>Unknown</c> as before.
    /// Two shapes are NOT typed: unresolved type parameters and
    /// <see cref="UnmappedClrType"/> — the bridge's "I could not express this" sentinel (#1534).
    /// </summary>
    private SemanticType? StaticCallResultTypeOrNull(FunctionCall call, SemanticType returnType)
    {
        if (returnType is UnknownType || ContainsTypeParameter(returnType))
            return null;

        if (returnType is UnmappedClrType)
            return null;

        return ProjectClrChar(call, returnType);
    }

    // The static companion of `_clrInstanceCallMemo`: same memo pattern, same reflection, asked of a
    // TYPE NAME rather than of a constructed receiver. Keyed on the CLR type and the resolved method
    // name, which together decide the candidate set.
    private readonly Dictionary<(Type, string), System.Reflection.MethodInfo[]> _clrStaticCallMemo = new();

    /// <summary>The public static overloads of <paramref name="methodName"/>, memoized.</summary>
    private System.Reflection.MethodInfo[] ClrStaticCallSurfaceOf(Type clrType, string methodName)
    {
        var key = (clrType, methodName);
        if (_clrStaticCallMemo.TryGetValue(key, out var cached))
            return cached;

        // Generic method definitions are excluded for the reason the char row excluded them: their
        // parameter types carry unsubstituted type parameters, so neither the arity rule nor the
        // argument check can read them without doing inference this seam does not own.
        var candidates = clrType
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name == methodName && !m.IsGenericMethodDefinition)
            .ToArray();

        _clrStaticCallMemo[key] = candidates;
        return candidates;
    }

    /// <summary>
    /// Checks each argument of a member call against the sole arity-matching CLR candidate, using the
    /// RAW <see cref="System.Reflection.ParameterInfo"/> types. The bridge's mapping is used to ASK
    /// the question in Sharpy vocabulary (so the provenance-aware <see cref="IsAssignable"/> answers
    /// it, and so the message names a type the user wrote), with the raw CLR type as a second chance
    /// for anything the mapping does not describe — never the reconstructed signature a
    /// <see cref="BuildBclGenericMethodSymbol"/> would build, whose <c>object</c> fallbacks accept
    /// everything.
    /// </summary>
    /// <param name="skipArgumentIndices">Argument positions a caller has already decided on its own
    /// terms — the static seam's CLR-<c>char</c> parameters (#1402), whose <c>str</c> argument is
    /// governed by the single-character-literal rule and would otherwise be reported a second time
    /// here as a plain <c>str</c>/<c>char</c> mismatch.</param>
    private void CheckClrCallArgumentTypes(
        FunctionCall call, System.Reflection.MethodInfo method,
        List<SemanticType> argTypes, string memberDisplay,
        HashSet<int>? skipArgumentIndices = null)
    {
        var parameters = method.GetParameters();

        for (int i = 0; i < argTypes.Count && i < parameters.Length; i++)
        {
            if (skipArgumentIndices?.Contains(i) == true)
                continue;

            var parameter = parameters[i];

            // A params array absorbs the whole tail, and C# lets the caller pass either the elements
            // or the array itself — two shapes this seam would have to re-decide to check either.
            if (IsClrParamsArray(parameter))
                return;

            if (ClrParameterIsUndecidable(parameter))
                continue;

            // An argument whose own type is not settled (an error recovery, a still-open type
            // parameter, a bare `None` whose target decides its meaning) is skipped rather than
            // guessed at.
            //
            // A FUNCTION-typed argument is skipped ONLY while it is still being inferred — an
            // UNRESOLVED lambda (`ft.HasUnresolvedTypes()`, the same predicate as :1116). The former
            // blanket FunctionType skip shielded a #1393 mis-resolution (a parameter named like its
            // own function resolving to the function); that landed in 1fbf87e21, so a genuine closed
            // FunctionType argument is now checked like any other — the last shape of #1290's gap
            // (#1501). `calendar_module.spy` stays the regression pin for the unresolved-lambda case.
            if (argTypes[i] is UnknownType or TypeParameterType
                || (argTypes[i] is FunctionType argFn && argFn.HasUnresolvedTypes())
                || call.Arguments[i] is NoneLiteral)
            {
                continue;
            }

            var argumentNode = ArgumentNodeAt(call, i);
            var argumentClrType = TryGetClrType(argTypes[i]);
            string expectedDisplay;

            if (MapClrParameterType(parameter) is { } expected)
            {
                // Materialization is recorded before the acceptance question, in the same order the
                // argument-binding seam uses (ValidateCallArguments), so the checker and the emitter
                // agree about copies. A CLR formal is a .NET position — the emitted parameter IS the
                // CLR type and the value goes in unconverted — so this records nothing here today; it
                // is the rule (#1251, #1260) that must be stated at every binding site, not an effect.
                RecordSequenceMaterialization(argumentNode, argTypes[i], expected);

                if (ClrParameterAccepts(parameter, argTypes[i], argumentNode))
                {
                    // A CLR formal is a typed slot like any other: an argument admitted by a value
                    // shape must carry that shape's fact to the emitter. Without this,
                    // `Vector2(1.0, 2.0)` and `xs.append(0.0)` into a `float` formal were accepted
                    // and then emitted as unsuffixed doubles — CS1503 behind SPY0908 (#1688).
                    ApplyArgumentConversion(
                        StorePosition.ArgumentPositional, argumentNode, argTypes[i], expected);
                    continue;
                }

                // A lossy mapping refused by .NET is reported in .NET's words: the user wrote an
                // `int` where the formal is an enum, and "expects 'int'" would send them in circles.
                expectedDisplay = IsLossyClrMapping(parameter.ParameterType, expected)
                    ? Shared.ClrNameHelper.StripArity(parameter.ParameterType.Name)
                    : expected.GetDisplayName();
            }
            else
            {
                // The bridge collapsed the formal to `object`, which is a degradation and not a fact —
                // checking against it would accept everything, which is how `sb.append_line("ok", 42)`
                // stayed silent (its arity-2 overload's first parameter is IFormatProvider, an
                // interface the bridge has no Sharpy word for). The RAW parameter type is then the
                // only honest description of the formal, so the question is asked of .NET directly.
                if (argumentClrType == null
                    || parameter.ParameterType.IsAssignableFrom(argumentClrType))
                {
                    continue;
                }

                expectedDisplay = Shared.ClrNameHelper.StripArity(parameter.ParameterType.Name);
            }

            AddError(
                $"Argument {i + 1} of '{memberDisplay}' expects '{expectedDisplay}' "
                + $"but got '{argTypes[i].GetDisplayName()}'",
                call.Arguments[i].LineStart, call.Arguments[i].ColumnStart,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: call.Arguments[i].Span);
        }
    }

    /// <summary>
    /// Whether nothing this seam knows can decide an argument against <paramref name="parameter"/>,
    /// whichever description of the formal is used. A <c>ref</c>/<c>out</c> or pointer parameter is
    /// unwritable from Sharpy and a different diagnosis; one still naming a type parameter has no
    /// concrete formal at all; a delegate is bound from a lambda by a C# conversion rather than an
    /// assignability rule; <see cref="System.Type"/> is satisfied by a type reference, as
    /// <see cref="IsSystemTypeParameter"/> already allows; <c>object</c> accepts everything; and a
    /// ref-struct (<c>Span</c>, <c>ReadOnlySpan</c>) or a type carrying <c>op_Implicit</c> is reached
    /// by conversions reflection cannot enumerate.
    ///
    /// <para>An enum is NOT undecidable: it reaches the call as the bridge's <c>int</c>, which is a
    /// lossy spelling, and .NET decides it exactly through the argument's own CLR type — see
    /// <see cref="ClrParameterAccepts"/> (#1573).</para>
    /// </summary>
    private static bool ClrParameterIsUndecidable(System.Reflection.ParameterInfo parameter)
    {
        var parameterClrType = parameter.ParameterType;

        return parameterClrType.IsByRef || parameterClrType.IsPointer
            || parameterClrType.ContainsGenericParameters
            || parameterClrType.IsByRefLike
            || parameterClrType == typeof(Type) || parameterClrType == typeof(object)
            || typeof(Delegate).IsAssignableFrom(parameterClrType)
            || DeclaresImplicitConversion(parameterClrType);
    }

    /// <summary>
    /// The parameter's formal in Sharpy vocabulary, or <c>null</c> when the bridge collapsed it to
    /// <c>object</c> — a degradation, not a fact, and the caller asks .NET about the raw type instead.
    /// </summary>
    private SemanticType? MapClrParameterType(System.Reflection.ParameterInfo parameter)
    {
        var mapped = _bclGenericMethodBridge.MapClrTypeToSemanticType(parameter.ParameterType);
        if (mapped is UnknownType || IsObjectType(mapped))
            return null;

        // `string? value` accepts None by declaration; the reflected Type cannot say so (#1705).
        mapped = Discovery.ClrDeclaredNullability.Apply(mapped, Discovery.ClrDeclaredNullability.DeclaresNullableArgument(parameter));

        // A delegate parameter (Converter<T,U>, Func<>, Action<>, Predicate<>) maps to GenericType
        // after #1640 (was UnmappedClrType → IsObjectType → null). The bridge cannot match a Sharpy
        // FunctionType/lambda against a GenericType delegate spelling, so keep it unspellable.
        if (mapped is GenericType && typeof(Delegate).IsAssignableFrom(parameter.ParameterType))
            return null;

        return mapped;
    }

    /// <summary>
    /// The SPY0601 steer: the first argument position at which the surviving candidates disagree,
    /// and the Sharpy spellings of each candidate's parameter there — the types the user can cast the
    /// argument to (<c>Math.floor(float(x))</c>). A parameter the bridge cannot spell is named by its
    /// CLR type. Falls back to the generic steer when the candidates differ only in arity.
    /// </summary>
    private string DescribeDisambiguatingCast(
        IReadOnlyList<System.Reflection.MethodInfo> candidates, int argCount)
    {
        for (int position = 0; position < argCount; position++)
        {
            var spellings = candidates
                .Select(c => c.GetParameters())
                .Where(ps => position < ps.Length)
                // The cast TARGET is the parameter's underlying type: a `string?` parameter is
                // disambiguated by casting to `str` (`str?` would read as Optional) (#1705).
                .Select(ps => MapClrParameterType(ps[position]) is { } formal
                    ? (formal is NullableType nullable ? nullable.UnderlyingType : formal).GetDisplayName()
                    : Shared.ClrNameHelper.StripArity(ps[position].ParameterType.Name))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (spellings.Count > 1)
                return $"Disambiguate by casting argument {position + 1} to one of: {string.Join(", ", spellings)}";
        }

        return "Disambiguate by casting the argument to the intended type";
    }

    /// <summary>
    /// Whether the bridge's Sharpy spelling of <paramref name="parameterType"/> names a CLR type .NET
    /// would NOT accept for the parameter. The enum arm is the standing case: every enum maps to
    /// <c>int</c>, so a Sharpy <c>int</c> satisfies the spelling while the emitted C# needs the enum
    /// itself (CS1503 behind SPY0908, #1573). Faithful arms (<c>double</c>→<c>float</c>,
    /// <c>String</c>→<c>str</c>, a generic collection through its CLR origin) round-trip to a type the
    /// parameter accepts. A spelling whose CLR type is unknown is not called lossy — there is nothing
    /// to refute it with, and the mapped acceptance stands.
    /// </summary>
    private bool IsLossyClrMapping(Type parameterType, SemanticType mapped)
        => TryGetClrType(mapped) is { } mappedClrType && !parameterType.IsAssignableFrom(mappedClrType);

    /// <summary>
    /// Whether a MAPPED CLR parameter accepts an argument — the one answer both the candidate filter
    /// and the unique-candidate seam (<c>CheckClrCallArgumentTypes</c>) give. A parameter the bridge
    /// cannot express (<see cref="MapClrParameterType"/> returns null) counts as accepting here; the
    /// unique-candidate seam asks .NET about that one directly.
    /// <list type="number">
    /// <item>The Sharpy-vocabulary acceptance, exactly as an annotation would decide it — unless the
    /// mapping is <see cref="IsLossyClrMapping">lossy</see> and the argument has a CLR type .NET
    /// rejects for the parameter, in which case the acceptance proved nothing (#1573).</item>
    /// <item>Otherwise .NET's own answer on the argument's CLR type: the mapping is a description and
    /// can lose a relation .NET has (a derived CLR class against a base-class parameter, or a real
    /// enum value against the enum's <c>int</c> spelling).</item>
    /// </list>
    /// </summary>
    private bool ClrParameterAccepts(
        System.Reflection.ParameterInfo parameter, SemanticType argType, Expression? argumentNode)
    {
        // A bare `None` is C#'s null literal: applicable to every reference-type and Nullable<T>
        // parameter and to nothing else. Mirroring that keeps the candidate set exactly as ambiguous
        // as Roslyn will find it — `Console.write_line(None)` is ambiguous between the char[], string
        // and object overloads in C# too — so the refusal is SPY0601 rather than CS0121 behind
        // SPY0908 (#1569).
        if (argumentNode != null && UnwrapParenthesized(argumentNode) is NoneLiteral)
        {
            var parameterType = parameter.ParameterType;
            return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null;
        }

        var mapped = MapClrParameterType(parameter);
        if (mapped == null)
            return true;

        var argumentClrType = TryGetClrType(argType);
        var clrAccepts = argumentClrType != null && parameter.ParameterType.IsAssignableFrom(argumentClrType);

        if (IsArgumentAssignable(argType, mapped, argumentNode))
            return clrAccepts || argumentClrType == null || !IsLossyClrMapping(parameter.ParameterType, mapped);

        return clrAccepts;
    }

    /// <summary>
    /// Whether a type declares any user-defined implicit conversion. Such a type can be reached from
    /// values <see cref="Type.IsAssignableFrom"/> says nothing about, so the raw check stays out of it.
    /// </summary>
    private static bool DeclaresImplicitConversion(Type type)
        => type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Any(m => m.Name == "op_Implicit");

    /// <summary>
    /// Whether <paramref name="method"/> can take <paramref name="argCount"/> positional arguments:
    /// optional parameters lower the floor and a <c>params</c> array removes the ceiling, exactly as
    /// C# counts them.
    /// </summary>
    private static bool ClrArityFits(System.Reflection.MethodInfo method, int argCount)
    {
        var parameters = method.GetParameters();
        var required = parameters.Count(p => !p.IsOptional && !IsClrParamsArray(p));
        if (argCount < required)
            return false;

        return (parameters.Length > 0 && IsClrParamsArray(parameters[^1]))
               || argCount <= parameters.Length;
    }

    private static bool IsClrParamsArray(System.Reflection.ParameterInfo parameter)
        => parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false);

    /// <summary>
    /// The argument counts a member's CLR overloads accept, as a phrase — "1 argument",
    /// "0 to 2 arguments", "at least 1 argument" — for the count diagnostic. Phrased as a span
    /// rather than a list because optional parameters make each candidate a span of its own; the
    /// message says what would have been acceptable, and the refused count is outside it either way.
    /// </summary>
    private static string DescribeClrArities(IReadOnlyList<System.Reflection.MethodInfo> candidates)
    {
        var fewest = int.MaxValue;
        var most = 0;
        var unbounded = false;

        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            if (parameters.Length > 0 && IsClrParamsArray(parameters[^1]))
                unbounded = true;

            fewest = Math.Min(fewest, parameters.Count(p => !p.IsOptional && !IsClrParamsArray(p)));
            most = Math.Max(most, parameters.Length);
        }

        if (unbounded)
            return $"at least {fewest} argument{(fewest == 1 ? "" : "s")}";

        return fewest == most
            ? $"{fewest} argument{(fewest == 1 ? "" : "s")}"
            : $"{fewest} to {most} arguments";
    }

    /// <summary>
    /// The CLR type a member call on <paramref name="receiverType"/> binds against — the CONSTRUCTED
    /// receiver where one is available (<c>List&lt;int&gt;</c>, not the open <c>List&lt;&gt;</c>),
    /// mirroring the #1136 fallback and the #1141 absence proof so all three see the same surface.
    /// Null when the receiver has no CLR type, or when the type is still open: an open generic's
    /// parameters name <c>T</c>, so there is nothing to check an argument against.
    /// </summary>
    private Type? ClrReceiverReflectionType(SemanticType receiverType)
    {
        var ownerSymbol = ResolveInstanceMemberOwnerSymbol(receiverType);
        if (ownerSymbol?.ClrType == null)
            return null;

        var reflectionType = TryGetClrType(receiverType) ?? ownerSymbol.ClrType;
        return reflectionType.ContainsGenericParameters ? null : reflectionType;
    }

    private ClrInstanceCallSurface ClrInstanceCallSurfaceOf(Type reflectionType, string memberName)
    {
        var memoKey = (reflectionType, memberName);
        if (_clrInstanceCallMemo.TryGetValue(memoKey, out var memoized))
            return memoized;

        var surface = BuildClrInstanceCallSurface(reflectionType, memberName);
        _clrInstanceCallMemo[memoKey] = surface;
        return surface;
    }

    private ClrInstanceCallSurface BuildClrInstanceCallSurface(Type reflectionType, string memberName)
    {
        var empty = new ClrInstanceCallSurface(Array.Empty<System.Reflection.MethodInfo>(), false);

        System.Reflection.MethodInfo[] candidates;
        try
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

            var declared = reflectionType.GetMethods(flags).AsEnumerable();
            // A receiver typed as an interface does not see its inherited interface methods through
            // GetMethods; for a class receiver this adds nothing (same rule as GetMemberNameSurface).
            if (reflectionType.IsInterface)
                declared = declared.Concat(reflectionType.GetInterfaces().SelectMany(i => i.GetMethods(flags)));

            candidates = declared
                .Where(method => !method.IsGenericMethodDefinition
                    && ClrMethodAnswersToName(method, memberName))
                .ToArray();

            // A property or field of the same name means the call may be invoking a delegate stored
            // there, which the method surface does not describe.
            if (candidates.Length > 0
                && Discovery.ClrTypeHelper.ResolveClrPropertyName(reflectionType, memberName) != null)
            {
                return empty;
            }
        }
        catch (Exception ex) when (ex is System.Reflection.ReflectionTypeLoadException or TypeLoadException
                                       or System.IO.FileNotFoundException or NotSupportedException)
        {
            return empty; // reflection could not answer — nothing is proven, so nothing is refused
        }

        if (candidates.Length == 0)
            return empty;

        return new ClrInstanceCallSurface(
            candidates, ClrExtensionMethodNameIsReachable(reflectionType, memberName));
    }

    /// <summary>
    /// Whether a CLR method answers to the written Sharpy member name — verbatim, or by the same
    /// reverse mangling every other CLR member lookup uses (<c>add_range</c> → <c>AddRange</c>).
    /// </summary>
    private static bool ClrMethodAnswersToName(System.Reflection.MethodInfo method, string memberName)
        => method.Name == memberName
           || NameMangler.ToSharpyName(method.Name, ReverseNameContext.Method) == memberName;

    /// <summary>
    /// Whether an extension method of this name is reachable from the emitted compilation — the same
    /// clause, over the same assemblies, that keeps the #1141 absence proof permissive.
    /// </summary>
    private bool ClrExtensionMethodNameIsReachable(Type receiverClrType, string memberName)
    {
        var pascalName = NameMangler.ToPascalCase(memberName);
        foreach (var assembly in EnumerateExtensionMethodAssemblies(receiverClrType))
        {
            var extensionNames = Discovery.ClrTypeHelper.GetExtensionMethodNames(assembly);
            if (extensionNames.Contains(memberName) || extensionNames.Contains(pascalName))
                return true;
        }

        return false;
    }

    private bool IsDelegateCallee(Expression callee)
    {
        if (callee is Identifier id)
        {
            var symbol = _symbolTable.Lookup(id.Name);
            return symbol is VariableSymbol { Type: FunctionType };
        }
        return false;
    }
}
