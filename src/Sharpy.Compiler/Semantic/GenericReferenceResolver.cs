using System.Collections.Generic;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The kind of callee a generic reference (<c>callee[T, ...]</c>) resolves to. One enum value per arm
/// the resolver replaces, so the emitter can dispatch its lowering on <see cref="GenericReference.Kind"/>
/// alone rather than re-deriving the callee shape (#1143).
/// </summary>
public enum GenericReferenceKind
{
    /// <summary>A builtin generic function, e.g. <c>map[int, int]</c>.</summary>
    Builtin,

    /// <summary>A user-declared top-level generic function, e.g. <c>identity[int]</c>.</summary>
    UserFunction,

    /// <summary>A generic function exported by an imported module, e.g. <c>mathlib.identity[int]</c> or <c>json.loads[int]</c>.</summary>
    ModuleFunction,

    /// <summary>A generic type exported by an imported module, e.g. <c>difflib.SequenceMatcher[str]</c>.</summary>
    ModuleType,

    /// <summary>A generic instance method on a user-declared receiver, e.g. <c>recv.convert[int]</c>.</summary>
    InstanceMethod,

    /// <summary>A generic instance method on a raw BCL receiver, e.g. <c>lst.convert_all[str]</c> (#1136).</summary>
    BclInstanceMethod,

    /// <summary>
    /// A generic extension method reached on a BCL/sequence receiver with explicit type arguments,
    /// e.g. <c>lst.select[str]</c> → <c>lst.Select&lt;int, string&gt;</c> (#1163). Distinct from
    /// <see cref="BclInstanceMethod"/> because the lowering differs: there is no discovered
    /// <see cref="FunctionSymbol"/>, and the emitted type-argument vector is
    /// <see cref="GenericReference.LoweredTypeArgs"/> (receiver-inferred arguments included), not the
    /// written ones.
    /// </summary>
    BclExtensionMethod,

    /// <summary>A generic type reference, e.g. <c>Box[int]</c>.</summary>
    GenericTypeRef,

    /// <summary>A generic nested type reference, e.g. <c>Outer.Inner[int]</c> (#1164).</summary>
    NestedTypeRef,

    /// <summary>An array type reference, e.g. <c>array[int]</c>.</summary>
    ArrayTypeRef,

    /// <summary>
    /// A tuple type reference, e.g. <c>tuple[int, str]</c> (#1200). Distinct from
    /// <see cref="GenericTypeRef"/> because a tuple's arity is part of its type: the written vector is
    /// the ELEMENT list, not arguments to a fixed-arity declaration, so the reference types as a
    /// <see cref="TupleType"/> — the one spelling the annotation position also produces — and calling
    /// it converts a tuple rather than invoking a constructor.
    /// </summary>
    TupleTypeRef,
}

/// <summary>
/// The normalized fact the <see cref="TypeChecker"/>'s generic-reference resolver produces for a
/// <c>callee[T, ...]</c> index access, regardless of what <c>callee</c> is. It is the single lowering
/// face for generic references (Critical Rule 2 pattern (b); #1143): semantic analysis decides the
/// kind, target, receiver, type arguments, and (for arity-selected builtins) the selected overload;
/// the emitter reads this and never re-derives the shape. The parallel <see cref="GenericFunctionType"/>
/// / <see cref="GenericType"/> expression-type recording remains the type-system face.
/// </summary>
public sealed record GenericReference
{
    /// <summary>Which callee kind the reference resolved to.</summary>
    public required GenericReferenceKind Kind { get; init; }

    /// <summary>
    /// The resolved target: a <see cref="FunctionSymbol"/> for the function/method kinds, a
    /// <see cref="TypeSymbol"/> for the type-reference kinds, or <c>null</c> for <see cref="GenericReferenceKind.ArrayTypeRef"/>
    /// (the builtin <c>array</c> has no owning symbol).
    /// </summary>
    public Symbol? TargetSymbol { get; init; }

    /// <summary>
    /// The receiver's semantic type for the member-qualified kinds (module function/type, instance and
    /// BCL methods); <c>null</c> for bare-identifier references.
    /// </summary>
    public SemanticType? ReceiverType { get; init; }

    /// <summary>The resolved type arguments in source order.</summary>
    public required IReadOnlyList<SemanticType> TypeArgs { get; init; }

    /// <summary>
    /// The complete type-argument vector to EMIT, when it differs from what was written. Only
    /// <see cref="GenericReferenceKind.BclExtensionMethod"/> sets it: <c>lst.select[str]</c> writes one
    /// argument but lowers to <c>Select&lt;int, string&gt;</c>, the element type having been inferred
    /// from the receiver during resolution (#1163). <c>null</c> for every other kind, whose emitted
    /// vector is exactly <see cref="TypeArgs"/>.
    /// </summary>
    public IReadOnlyList<SemanticType>? LoweredTypeArgs { get; init; }

    /// <summary>
    /// The CLR member name to emit, when the Sharpy spelling does not determine it by mangling alone.
    /// Set for <see cref="GenericReferenceKind.BclExtensionMethod"/> (the reflected method's own name),
    /// <c>null</c> otherwise.
    /// </summary>
    public string? ClrMemberName { get; init; }

    /// <summary>
    /// For <see cref="GenericReferenceKind.Builtin"/>, the arity-selected overload (which may differ
    /// from the first-by-name symbol — #999); otherwise the resolved target itself. Consumers validate
    /// value arguments against this signature (#1148).
    /// </summary>
    public FunctionSymbol? SelectedOverload { get; init; }

    /// <summary>
    /// The reference's result type once the type-argument vector closes it. Only
    /// <see cref="GenericReferenceKind.BclExtensionMethod"/> sets it — no <see cref="FunctionSymbol"/>
    /// exists for that kind, so the closed <c>MethodInfo</c>'s return type mapped back to a Sharpy
    /// type IS the signature (#1195). It is the reference's expression type, which is what lets
    /// <c>list(lst.select[str](f))</c> know what it is wrapping.
    /// </summary>
    public SemanticType? ClosedReturnType { get; init; }

    /// <summary>
    /// The closed method's VALUE parameter types, receiver (<c>this</c>) parameter dropped, in
    /// declaration order — the expected types the call's arguments are checked against, so the lambda
    /// in <c>lst.select[str](f)</c> is compared with <c>Func&lt;int, str&gt;</c> instead of reaching
    /// Roslyn as CS0029 (#1195, the #1148 contract for this kind). Set alongside
    /// <see cref="ClosedReturnType"/>.
    /// </summary>
    public IReadOnlyList<SemanticType>? ClosedParameterTypes { get; init; }
}

internal partial class TypeChecker
{
    /// <summary>
    /// Maps the CLR type arguments that extension-method resolution infers back to Sharpy types for the
    /// <see cref="GenericReference.LoweredTypeArgs"/> vector (#1163). Lazy: most compilations never
    /// reach the extension-method arm.
    /// </summary>
    private readonly Lazy<Discovery.ClrTypeBridge> _clrTypeBridge = new(() => new Discovery.ClrTypeBridge());

    /// <summary>
    /// Single resolution step for a generic reference <c>callee[T, ...]</c>. Normalizes every callee
    /// kind (array/type/function, bare or module- or instance-qualified) into one
    /// <see cref="GenericReference"/> fact and records it node-keyed in <see cref="SemanticInfo"/>,
    /// alongside the same <see cref="GenericFunctionType"/>/<see cref="GenericType"/> expression type the
    /// per-arm code recorded before. Returns <c>true</c> when the index access IS a generic reference —
    /// either resolved cleanly (<paramref name="resultType"/> is the reference type) or rejected with a
    /// deliberate arity diagnostic (<paramref name="resultType"/> is <see cref="SemanticType.Unknown"/>).
    /// Returns <c>false</c> for ordinary value indexing, having emitted no diagnostic, so
    /// <see cref="CheckIndexAccessCore"/> proceeds to the value-indexing path. One shape records a
    /// fact and still returns <c>false</c>: a nested generic type reference
    /// (<see cref="GenericReferenceKind.NestedTypeRef"/>, #1164) is materialized for codegen while
    /// its type-checking stays on the pre-existing path — see
    /// <see cref="RecordNestedGenericTypeRef"/>.
    /// </summary>
    /// <remarks>
    /// Because every cell here references a generic callable, <c>[...]</c> is always explicit type
    /// arguments — never a subscript. The resolver must therefore DECLINE gracefully on non-generic
    /// callees (never resolving the index as types on a value receiver, which would emit a spurious
    /// "type not found"): each arm gates on the callee being generic before calling
    /// <see cref="TryResolveTypeArguments"/> (the #1136 lesson).
    /// </remarks>
    private bool TryResolveGenericReference(IndexAccess indexAccess, out SemanticType resultType)
    {
        resultType = SemanticType.Unknown;

        // --- bare-identifier callees: array[int], Box[int], identity[int], map[int, int] ---
        if (indexAccess.Object is Identifier typeId)
        {
            // array[int] -> GenericType("array", [T]). Only a single type arg is an array reference;
            // any other shape falls through to the symbol lookup below (preserving prior behavior).
            if (typeId.Name == BuiltinNames.Array)
            {
                var arrayTypeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (arrayTypeArgs != null && arrayTypeArgs.Count == 1)
                {
                    var arrayType = new GenericType
                    {
                        Name = BuiltinNames.Array,
                        TypeArguments = arrayTypeArgs
                    };
                    _semanticInfo.SetExpressionType(indexAccess, arrayType);
                    _semanticInfo.SetGenericReference(indexAccess, new GenericReference
                    {
                        Kind = GenericReferenceKind.ArrayTypeRef,
                        TypeArgs = arrayTypeArgs,
                    });
                    resultType = arrayType;
                    return true;
                }
            }

            // tuple[int, str] -> TupleType. A tuple's arity is part of its type, so the written vector
            // is the element list rather than arguments to a fixed-arity declaration — checking it
            // against the `tuple` symbol's single declared parameter rejected every multi-element
            // spelling (SPY0224), and typing it as GenericType("tuple", …) made it unequal to the
            // identical annotation, which is the self-contradictory SPY0220 of #1200. One TupleType
            // spelling everywhere, exactly as TypeResolver produces for the annotation.
            if (typeId.Name == BuiltinNames.Tuple
                && _symbolTable.Lookup(typeId.Name) is TypeSymbol tupleSymbol
                && TryResolveTypeArguments(indexAccess.Index) is { Count: > 0 } tupleElementTypes)
            {
                var tupleType = new TupleType { ElementTypes = tupleElementTypes };
                _semanticInfo.SetExpressionType(indexAccess, tupleType);
                _semanticInfo.SetGenericReference(indexAccess, new GenericReference
                {
                    Kind = GenericReferenceKind.TupleTypeRef,
                    TargetSymbol = tupleSymbol,
                    TypeArgs = tupleElementTypes,
                });
                resultType = tupleType;
                return true;
            }

            var symbol = _symbolTable.Lookup(typeId.Name);

            // Box[int] -> GenericType. As before, no expression type is recorded for indexAccess.Object
            // (the bare Box identifier), keeping ProtocolValidator quiet.
            if (symbol is TypeSymbol genericTypeSymbol && genericTypeSymbol.IsGeneric)
            {
                var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (typeArgs != null)
                {
                    if (!CheckGenericTypeReferenceArity(genericTypeSymbol, typeArgs, indexAccess))
                        return true; // arity error emitted; handled

                    var genericType = new GenericType
                    {
                        Name = genericTypeSymbol.Name,
                        TypeArguments = typeArgs,
                        GenericDefinition = genericTypeSymbol
                    };
                    _semanticInfo.SetGenericReference(indexAccess, new GenericReference
                    {
                        Kind = GenericReferenceKind.GenericTypeRef,
                        TargetSymbol = genericTypeSymbol,
                        TypeArgs = typeArgs,
                    });
                    resultType = genericType;
                    return true;
                }
            }

            // identity[int] / map[int, int] -> GenericFunctionType.
            if (symbol is FunctionSymbol genericFuncSymbol && genericFuncSymbol.IsGeneric)
            {
                var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (typeArgs != null)
                {
                    // Pick the arity-matching overload for multi-arity builtins (map[int,int,int]);
                    // for user symbols SelectArityMatchingOverload returns the same instance (#999/#1002).
                    var resolvedFuncSymbol = SelectArityMatchingOverload(
                        typeId.Name, typeArgs.Count, genericFuncSymbol);
                    if (!CheckGenericReferenceArity(
                            typeId.Name, resolvedFuncSymbol.TypeParameters.Count, typeArgs.Count, indexAccess))
                        return true; // arity error emitted; handled

                    var funcType = new GenericFunctionType
                    {
                        FunctionSymbol = resolvedFuncSymbol,
                        TypeArguments = typeArgs
                    };
                    _semanticInfo.SetExpressionType(indexAccess, funcType);
                    _semanticInfo.SetGenericReference(indexAccess, new GenericReference
                    {
                        Kind = IsBuiltinGenericFunction(typeId.Name, resolvedFuncSymbol)
                            ? GenericReferenceKind.Builtin
                            : GenericReferenceKind.UserFunction,
                        TargetSymbol = resolvedFuncSymbol,
                        TypeArgs = typeArgs,
                        SelectedOverload = resolvedFuncSymbol,
                    });
                    resultType = funcType;
                    return true;
                }
            }
        }

        // --- member-qualified callees: json.loads[int], mathlib.identity[int],
        //     difflib.SequenceMatcher[str], recv.convert[int], lst.convert_all[str] ---
        if (indexAccess.Object is MemberAccess memberAccessObj)
        {
            var ownerType = CheckExpression(memberAccessObj.Object);
            if (ownerType is ModuleType modType)
            {
                var memName = memberAccessObj.Member;
                if (!modType.Symbol.Exports.ContainsKey(memName) && modType.Symbol.IsNetModule)
                {
                    var pascalName = NameMangler.ToPascalCase(memName);
                    if (modType.Symbol.Exports.ContainsKey(pascalName))
                        memName = pascalName;
                }

                if (modType.Symbol.Exports.TryGetValue(memName, out var exportedSym))
                {
                    // json.loads[int] / mathlib.identity[int] -> GenericFunctionType.
                    if (exportedSym is FunctionSymbol modFuncSymbol && modFuncSymbol.IsGeneric)
                    {
                        var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                        if (typeArgs != null)
                        {
                            if (!CheckGenericReferenceArity(
                                    memberAccessObj.Member, modFuncSymbol.TypeParameters.Count,
                                    typeArgs.Count, indexAccess))
                                return true;

                            var funcType = new GenericFunctionType
                            {
                                FunctionSymbol = modFuncSymbol,
                                TypeArguments = typeArgs
                            };
                            _semanticInfo.SetExpressionType(indexAccess, funcType);
                            _semanticInfo.SetGenericReference(indexAccess, new GenericReference
                            {
                                Kind = GenericReferenceKind.ModuleFunction,
                                TargetSymbol = modFuncSymbol,
                                ReceiverType = modType,
                                TypeArgs = typeArgs,
                                SelectedOverload = modFuncSymbol,
                            });
                            resultType = funcType;
                            return true;
                        }
                    }

                    // difflib.SequenceMatcher[str] -> GenericType. As with bare Box[int], no expression
                    // type is recorded for indexAccess.Object (the json.loads/SequenceMatcher member
                    // access), keeping ProtocolValidator quiet (#1133).
                    if (exportedSym is TypeSymbol modTypeSymbol && modTypeSymbol.IsGeneric)
                    {
                        var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                        if (typeArgs != null)
                        {
                            if (!CheckGenericTypeReferenceArity(modTypeSymbol, typeArgs, indexAccess))
                                return true; // arity error emitted; handled

                            var genericType = new GenericType
                            {
                                Name = modTypeSymbol.Name,
                                TypeArguments = typeArgs,
                                GenericDefinition = modTypeSymbol
                            };
                            _semanticInfo.SetGenericReference(indexAccess, new GenericReference
                            {
                                Kind = GenericReferenceKind.ModuleType,
                                TargetSymbol = modTypeSymbol,
                                ReceiverType = modType,
                                TypeArgs = typeArgs,
                            });
                            resultType = genericType;
                            return true;
                        }
                    }
                }
            }
            // Generic-method reference on an instance receiver (recv.convert[int], self.convert[str],
            // a.b.convert[int]) — user-defined or raw BCL (#1136). Gate on the member resolving to a
            // generic method BEFORE resolving the index as type arguments: the arity that steers BCL
            // overload selection is read cheaply from the index shape (TupleLiteral element count).
            else if (ownerType is not UnknownType)
            {
                var typeArgCount = indexAccess.Index is TupleLiteral argTuple ? argTuple.Elements.Length : 1;
                if (TryResolveGenericInstanceMethod(ownerType, memberAccessObj.Member, typeArgCount)
                        is { } instanceMethod
                    && TryResolveTypeArguments(indexAccess.Index) is { } typeArgs)
                {
                    if (!CheckGenericReferenceArity(
                            memberAccessObj.Member, instanceMethod.TypeParameters.Count,
                            typeArgs.Count, indexAccess))
                        return true;

                    var funcType = new GenericFunctionType
                    {
                        FunctionSymbol = instanceMethod,
                        TypeArguments = typeArgs
                    };
                    _semanticInfo.SetExpressionType(indexAccess, funcType);
                    _semanticInfo.SetGenericReference(indexAccess, new GenericReference
                    {
                        // A BCL-reflected method carries its CLR MethodInfo (#1136); a user method does not.
                        Kind = instanceMethod.ClrMethod != null
                            ? GenericReferenceKind.BclInstanceMethod
                            : GenericReferenceKind.InstanceMethod,
                        TargetSymbol = instanceMethod,
                        ReceiverType = ownerType,
                        TypeArgs = typeArgs,
                        SelectedOverload = instanceMethod,
                    });
                    resultType = funcType;
                    return true;
                }

                // lst.select[str](f) — an extension method on a sequence receiver, written with
                // explicit type arguments (#1163). No instance method by that name exists (the arm
                // above declined) and reflection cannot prove the member absent either, because the
                // extension surface could supply it — which is exactly how the no-type-args spelling
                // lst.first() compiles: nothing resolves it, the emitter writes lst.First() verbatim
                // and C# infers everything. Written type arguments break that, because they are only
                // PART of the C# vector (Select<TSource, TResult> also needs the element type), so the
                // name-only channel emitted lst.Select[string](…) — CS0021 behind SPY0908. Resolving it
                // here computes the whole vector, so codegen can spell the call.
                if (TryResolveBclExtensionMethod(indexAccess, memberAccessObj, ownerType, out var extensionType))
                {
                    resultType = extensionType;
                    return true;
                }

                // No generic method by that name — and when CLR reflection can PROVE the member does
                // not exist on the receiver at all (no member under any mangling candidate, no reachable
                // extension method), reject it here instead of letting the name-only interop channel
                // emit `recv.NoSuchMember<...>(...)` and leak a CS1061 through the SPY0908 net (#1141,
                // #1146). The proof is required, not assumed: anything inconclusive keeps the permissive
                // fall-through. Type arguments must resolve too, so genuine value indexing of an unknown
                // member (`arr.data[0]`) is untouched.
                if (ClrReflectionProvesMemberAbsent(ownerType, memberAccessObj.Member, out var suggestion)
                    && TryResolveTypeArguments(indexAccess.Index) != null)
                {
                    var message = $"Type '{ownerType.GetDisplayName()}' has no member '{memberAccessObj.Member}'";
                    if (suggestion != null)
                        message += $". Did you mean '{suggestion}'?";

                    AddError(
                        message,
                        memberAccessObj.LineStart,
                        memberAccessObj.ColumnStart,
                        code: DiagnosticCodes.Semantic.UndefinedMember,
                        span: memberAccessObj.Span,
                        data: SuggestionData(suggestion));
                    return true;
                }
            }

            // Outer.Inner[int](42) — a nested generic type reference. Its qualifier is a TYPE, so
            // CheckExpression above produced the intentional Unknown that a non-primitive TypeSymbol
            // reference gets and none of the arms above apply. This arm only MATERIALIZES the callee
            // shape the emitter used to re-derive with its own symbol-table walk (#1164): it records
            // the fact and then DECLINES, leaving the reference with exactly the type and diagnostics
            // it had before — the access itself is typed by the value-indexing path in
            // CheckIndexAccessCore and the enclosing construction by CheckCall, both untouched.
            // The one shape it HANDLES is a wrong-arity vector (#1192), which has no fact to record.
            if (RecordNestedGenericTypeRef(indexAccess, memberAccessObj))
                return true;

            // Outer.no_such_generic[str](5) — a member that does not exist on a TYPE qualifier.
            // #1141's value-receiver proof above never runs for this receiver kind: a class-name
            // qualifier types as the intentional Unknown a TypeSymbol reference gets, so the whole
            // instance-member section is skipped and the name-only interop channel emitted
            // Outer.NoSuchGeneric<string>(5) — CS0117 behind SPY0908 (#1194). Runs after the nested
            // arm so an existing nested generic type is never second-guessed.
            if (TypeQualifierProvesMemberAbsent(indexAccess, memberAccessObj))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves <c>receiver.member[T, …]</c> as an extension method on the #1163 acceptance surface
    /// (<c>System.Linq.Enumerable</c> over sequence receivers), recording a
    /// <see cref="GenericReferenceKind.BclExtensionMethod"/> fact with the complete emitted
    /// type-argument vector. Returns <c>true</c> when the reference IS such a call — resolved, or
    /// rejected with a deliberate diagnostic — and <c>false</c> when it is not one at all (no member of
    /// that name on the surface, or a receiver whose CLR type is unknown), leaving the existing
    /// permissive interop channel and the #1141 absence proof to run unchanged.
    /// </summary>
    /// <remarks>
    /// A resolved reference is typed from the CLOSED method's return type, and its value parameters
    /// come along in the fact (#1195): the vector that makes the call spellable also makes its
    /// signature knowable, so <c>list(lst.select[str](f))</c> knows what it wraps and the lambda is
    /// checked against <c>Func&lt;int, str&gt;</c> rather than reaching Roslyn as CS0029.
    /// </remarks>
    private bool TryResolveBclExtensionMethod(
        IndexAccess indexAccess, MemberAccess memberAccess, SemanticType ownerType,
        out SemanticType resultType)
    {
        resultType = SemanticType.Unknown;

        if (!Discovery.ClrExtensionMethodResolver.IsOnAcceptanceSurface(memberAccess.Member))
            return false;

        var receiverClrType = TryGetClrType(ownerType);
        if (receiverClrType == null)
            return false;

        // Past this point the member IS on the acceptance surface, so `[...]` is type arguments and
        // resolving them cannot misfire on a value subscript (the #1136 lesson).
        if (TryResolveTypeArguments(indexAccess.Index) is not { } typeArgs)
            return false;

        var explicitClrArgs = new List<Type>(typeArgs.Count);
        foreach (var typeArg in typeArgs)
        {
            var clrArg = TryGetClrType(typeArg);
            if (clrArg == null)
            {
                // A written argument with no CLR form (an open type parameter, an unresolved name):
                // nothing to compute a vector from.
                ReportUncomputableExtensionTypeArgs(indexAccess, memberAccess, typeArgs);
                return true;
            }
            explicitClrArgs.Add(clrArg);
        }

        var resolution = Discovery.ClrExtensionMethodResolver.TryResolveWithExplicitTypeArguments(
            receiverClrType, memberAccess.Member, explicitClrArgs);
        if (resolution == null)
        {
            ReportUncomputableExtensionTypeArgs(indexAccess, memberAccess, typeArgs);
            return true;
        }

        var loweredTypeArgs = new List<SemanticType>(resolution.TypeArguments.Count);
        foreach (var clrArg in resolution.TypeArguments)
            loweredTypeArgs.Add(_clrTypeBridge.Value.MapClrTypeToSemanticType(clrArg));

        // Uncalled is meaningless for this kind, exactly as for a generic function reference (#1138):
        // the reference is a carrier for the type arguments of its call, not a value. Erroring here
        // keeps `g = lst.select[str]` a deliberate diagnostic instead of element access on a method
        // group (CS0021 → SPY0908) — the same contract the sibling kinds get from the CheckExpression
        // choke point, which cannot see this kind because its type is Unknown.
        if (!IsCurrentCallCallee(indexAccess))
        {
            AddError(
                $"an extension method reference must be called; '{memberAccess.Member}[...]' cannot be used as a value",
                indexAccess.LineStart,
                indexAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.GenericFunctionReferenceNotCalled,
                span: indexAccess.Span);
            return true;
        }

        // The vector that makes the call spellable also closes its signature, so the reference is
        // typed from the closed return type rather than left Unknown, and the value parameters (the
        // `this` receiver dropped) travel in the fact for the call site to check against (#1195).
        var closedParameters = resolution.ClosedMethod.GetParameters();
        var closedParameterTypes = new List<SemanticType>(Math.Max(0, closedParameters.Length - 1));
        for (int i = 1; i < closedParameters.Length; i++)
            closedParameterTypes.Add(_clrTypeBridge.Value.MapClrTypeToSemanticType(closedParameters[i].ParameterType));

        var closedReturnType = _clrTypeBridge.Value.MapClrTypeToSemanticType(resolution.ClosedMethod.ReturnType);

        _semanticInfo.SetGenericReference(indexAccess, new GenericReference
        {
            Kind = GenericReferenceKind.BclExtensionMethod,
            ReceiverType = ownerType,
            TypeArgs = typeArgs,
            LoweredTypeArgs = loweredTypeArgs,
            ClrMemberName = resolution.ClrMethodName,
            ClosedReturnType = closedReturnType,
            ClosedParameterTypes = closedParameterTypes,
        });
        resultType = closedReturnType;
        return true;
    }

    /// <summary>
    /// The deliberate diagnostic for an extension-method reference on the acceptance surface whose
    /// full type-argument vector cannot be computed — nothing by that name binds the receiver, the
    /// counts do not add up, a written argument contradicts what the receiver determines, or two
    /// candidates disagree. Reported instead of emitting a vector that cannot compile (#1146).
    /// </summary>
    private void ReportUncomputableExtensionTypeArgs(
        IndexAccess indexAccess, MemberAccess memberAccess, IReadOnlyList<SemanticType> typeArgs)
    {
        var written = string.Join(", ", typeArgs.Select(t => t.GetDisplayName()));
        AddError(
            $"Cannot determine the type arguments of extension method '{memberAccess.Member}[{written}]' " +
            "on this receiver. Write every type argument the method declares, or drop them and let them " +
            "be inferred from the arguments.",
            indexAccess.LineStart,
            indexAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.CannotInferGenericType,
            span: indexAccess.Span);
    }

    /// <summary>
    /// Records the <see cref="GenericReferenceKind.NestedTypeRef"/> fact for a nested generic type
    /// reference (<c>Outer.Inner[int]</c>, <c>A.B.C[int]</c>), resolving the nested
    /// <see cref="TypeSymbol"/> by walking the member-access chain through the symbol table — the
    /// walk the emitter did at emit time before #1164. Gated exactly as that emitter arm was (a
    /// nested type that exists and is generic), so the fact is present for precisely the shapes the
    /// deleted arm lowered.
    /// <para>Returns <c>true</c> only when the reference was REJECTED for wrong arity (#1192) —
    /// the caller then reports the index access as handled, with the <see cref="SemanticType.Unknown"/>
    /// every rejected reference gets. A recorded fact still returns <c>false</c>, keeping the
    /// type-checking of the access itself on the pre-existing value-indexing path.</para>
    /// </summary>
    private bool RecordNestedGenericTypeRef(IndexAccess indexAccess, MemberAccess memberAccess)
    {
        if (LookupNestedTypeSymbol(memberAccess) is not { IsGeneric: true } nestedTypeSymbol)
            return false;

        // Only once a generic nested type is proven is `[...]` known to be type arguments, so
        // resolving the index as types here cannot emit a spurious "type not found" (#1136 lesson).
        if (TryResolveTypeArguments(indexAccess.Index) is not { } typeArgs)
            return false;

        if (!CheckGenericTypeReferenceArity(nestedTypeSymbol, typeArgs, indexAccess))
            return true; // arity error emitted; handled

        _semanticInfo.SetGenericReference(indexAccess, new GenericReference
        {
            Kind = GenericReferenceKind.NestedTypeRef,
            TargetSymbol = nestedTypeSymbol,
            TypeArgs = typeArgs,
        });
        return false;
    }

    /// <summary>
    /// Proves that a member written on a USER-DECLARED type qualifier does not exist — the #1141
    /// contract for the one receiver kind #1141's reflection proof cannot reach (#1194). A user
    /// declaration's own symbol settles the question outright: if the name is in none of its nested
    /// types, methods, properties or fields, it is absent, no reflection needed. A CLR-backed
    /// qualifier is left entirely to the reflection path (its member surface includes inherited and
    /// extension members this symbol scan does not see), and so is anything inconclusive — the proof
    /// must be of ABSENCE, never of "this shape is not handled here".
    /// </summary>
    /// <returns><c>true</c> when the reference was rejected with SPY0203.</returns>
    private bool TypeQualifierProvesMemberAbsent(IndexAccess indexAccess, MemberAccess memberAccess)
    {
        if (LookupQualifierTypeSymbol(memberAccess.Object) is not { ClrType: null } qualifier)
            return false;

        var memberNames = TypeQualifierMemberNames(qualifier);
        if (memberNames.Contains(memberAccess.Member))
            return false;

        // Only once the index resolves as type arguments is this a generic reference at all, so
        // genuine value indexing through a type qualifier is untouched (the #1141 gate).
        if (TryResolveTypeArguments(indexAccess.Index) == null)
            return false;

        var suggestion = Utilities.EditDistance.FindClosestMatch(memberAccess.Member, memberNames);
        var message = $"Type '{qualifier.Name}' has no member '{memberAccess.Member}'";
        if (suggestion != null)
            message += $". Did you mean '{suggestion}'?";

        AddError(
            message,
            memberAccess.LineStart,
            memberAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.UndefinedMember,
            span: memberAccess.Span,
            data: SuggestionData(suggestion));
        return true;
    }

    /// <summary>
    /// The names a user-declared type exposes to a qualified reference: its nested types, methods,
    /// properties and fields.
    /// </summary>
    private static HashSet<string> TypeQualifierMemberNames(TypeSymbol qualifier)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nested in qualifier.NestedTypes)
            names.Add(nested.Name);
        foreach (var method in qualifier.Methods)
            names.Add(method.Name);
        foreach (var property in qualifier.Properties)
            names.Add(property.Name);
        foreach (var field in qualifier.Fields)
            names.Add(field.Name);
        return names;
    }

    /// <summary>
    /// Resolves the qualifier of <c>Qualifier.member[...]</c> to the <see cref="TypeSymbol"/> it
    /// names — <c>Outer</c> for <c>Outer.member</c>, <c>A.B</c> for <c>A.B.member</c> — or
    /// <c>null</c> when it names something other than a type.
    /// </summary>
    private TypeSymbol? LookupQualifierTypeSymbol(Expression qualifier) => qualifier switch
    {
        Identifier id => _symbolTable.Lookup(id.Name) as TypeSymbol,
        MemberAccess nested => LookupNestedTypeSymbol(nested),
        _ => null,
    };

    /// <summary>
    /// Resolves <c>Outer.Inner</c> / <c>A.B.C</c> to the nested <see cref="TypeSymbol"/> it names, or
    /// <c>null</c> when the qualifier is not a type or declares no such nested type.
    /// </summary>
    private TypeSymbol? LookupNestedTypeSymbol(MemberAccess memberAccess) => memberAccess.Object switch
    {
        Identifier outer => (_symbolTable.Lookup(outer.Name) as TypeSymbol)
            ?.NestedTypes.FirstOrDefault(n => n.Name == memberAccess.Member),
        MemberAccess inner => LookupNestedTypeSymbol(inner)
            ?.NestedTypes.FirstOrDefault(n => n.Name == memberAccess.Member),
        _ => null,
    };

    /// <summary>
    /// The single arity-check seam for generic references (#1004, generalized). Emits the deliberate
    /// wrong-type-argument-count diagnostic (SPY0224) with the exact wording the per-arm checks used
    /// and returns <c>false</c>; returns <c>true</c> when the counts match.
    /// </summary>
    private bool CheckGenericReferenceArity(string calleeName, int expected, int actual, IndexAccess indexAccess)
    {
        if (expected == actual)
            return true;

        AddError(
            $"Generic function '{calleeName}' expects {expected} type argument(s) but got {actual}",
            indexAccess.LineStart,
            indexAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.WrongArgumentCount,
            span: indexAccess.Span);
        return false;
    }

    /// <summary>
    /// The arity-check seam for generic TYPE references — <c>Box[int]</c>, <c>difflib.SequenceMatcher[str]</c>,
    /// <c>Outer.Inner[int]</c> — the type-side counterpart of <see cref="CheckGenericReferenceArity"/>
    /// (#1192). Unlike the function seam it is PEP-696 default-aware: a short vector fills its trailing
    /// parameters from their declared defaults, and only an excess or unfillable vector is rejected.
    /// Both the filling and the diagnostic mirror <c>TypeResolver.ResolveTypeAnnotation</c> exactly, so
    /// <c>Box[int, str]</c> reads identically whether it is written as an annotation or as an expression.
    /// <para><paramref name="typeArgs"/> is completed IN PLACE when defaults are filled: the caller's
    /// recorded <see cref="GenericReference.TypeArgs"/> and <see cref="GenericType.TypeArguments"/> then
    /// carry the whole vector, which is what the emitted C# type argument list is built from.</para>
    /// </summary>
    private bool CheckGenericTypeReferenceArity(
        TypeSymbol typeSymbol, List<SemanticType> typeArgs, IndexAccess indexAccess)
    {
        var expected = typeSymbol.TypeParameters.Count;
        if (typeArgs.Count == expected)
            return true;

        if (typeArgs.Count < expected)
        {
            for (int i = typeArgs.Count; i < expected; i++)
            {
                var typeParam = typeSymbol.TypeParameters[i];
                if (typeParam.DefaultType == null)
                    break;
                typeArgs.Add(_typeResolver.ResolveTypeAnnotation(typeParam.DefaultType));
            }

            if (typeArgs.Count == expected)
                return true;
        }

        AddError(
            $"Type '{typeSymbol.Name}' expects {expected} type arguments but got {typeArgs.Count}",
            indexAccess.LineStart,
            indexAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.WrongArgumentCount,
            span: indexAccess.Span);
        return false;
    }

    /// <summary>
    /// Whether <paramref name="symbol"/> is one of the builtin registry's own generic-function overloads
    /// for <paramref name="name"/> (as opposed to a user function shadowing the builtin name — #1002).
    /// Mirrors the reference-identity check in <see cref="SelectArityMatchingOverload"/>.
    /// </summary>
    private bool IsBuiltinGenericFunction(string name, FunctionSymbol symbol)
    {
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(name);
        if (overloads == null)
            return false;
        foreach (var overload in overloads)
        {
            if (ReferenceEquals(overload, symbol))
                return true;
        }
        return false;
    }
}
