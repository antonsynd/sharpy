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
            // The decision itself lives in TryBuildTupleTypeReference (TypeChecker.Expressions.
            // Access.cs), shared with the type-ARGUMENT resolver — this arm adds only the
            // SemanticInfo recording a top-level reference needs. #1470 was this rule holding here
            // and nowhere else.
            if (_symbolTable.Lookup(typeId.Name) is TypeSymbol tupleSymbol
                && TryBuildTupleTypeReference(typeId, TryResolveTypeArguments(indexAccess.Index))
                    is { } tupleType)
            {
                _semanticInfo.SetExpressionType(indexAccess, tupleType);
                _semanticInfo.SetGenericReference(indexAccess, new GenericReference
                {
                    Kind = GenericReferenceKind.TupleTypeRef,
                    TargetSymbol = tupleSymbol,
                    TypeArgs = tupleType.ElementTypes,
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
                            typeId.Name, resolvedFuncSymbol.TypeParameters, typeArgs, indexAccess))
                        return true; // arity error emitted; handled

                    if (!CheckGenericReferenceConstraints(
                            resolvedFuncSymbol.TypeParameters, typeArgs, indexAccess))
                        return true; // constraint error emitted; handled (#1289)

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
            // The thing being checked is a member-access QUALIFIER, and must be marked as one for
            // the duration — exactly as CheckMemberAccessCore does at its own qualifier check.
            // Without the marker the value-position choke point sees a bare type name in what looks
            // like a value position and rejects it: SPY0339 for a generic type reference, and — once
            // user classes became constructor references (#1211) — SPY0342 for the plain class
            // qualifier of a nested type (`Outer.Inner[int](6)`). Both are the #1170 over-fire class,
            // and the qualifier tracker is what the rules read to avoid it.
            SemanticType ownerType;
            using (ScopedValue.Push(ref _currentMemberAccessQualifier, memberAccessObj.Object))
                ownerType = CheckExpression(memberAccessObj.Object);

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
                                    memberAccessObj.Member, modFuncSymbol.TypeParameters,
                                    typeArgs, indexAccess))
                                return true;

                            if (!CheckGenericReferenceConstraints(
                                    modFuncSymbol.TypeParameters, typeArgs, indexAccess))
                                return true; // #1289

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
                            memberAccessObj.Member, instanceMethod.TypeParameters,
                            typeArgs, indexAccess))
                        return true;

                    // A BCL-reflected method's type parameters carry no reconstructed constraints
                    // (BuildBclGenericMethodSymbol leaves them to Roslyn), so this is a no-op for that
                    // kind and a real check for a user-declared generic method (#1289).
                    if (!CheckGenericReferenceConstraints(
                            instanceMethod.TypeParameters, typeArgs, indexAccess))
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
        resultType = RecordBclExtensionMethodFact(indexAccess, ownerType, typeArgs, loweredTypeArgs, resolution);
        return true;
    }

    /// <summary>
    /// Builds and records the one <see cref="GenericReferenceKind.BclExtensionMethod"/> fact, returning
    /// the closed return type that types the reference. Shared by the explicit spelling
    /// (<c>lst.select[str](f)</c>, keyed on its <c>IndexAccess</c>) and the staged no-type-args spelling
    /// (<c>lst.select(f)</c>, keyed on its <c>MemberAccess</c>) so the two cannot drift into producing
    /// differently-shaped facts for the same call (#1145).
    /// </summary>
    /// <param name="writtenTypeArgs">
    /// What the SOURCE wrote, which is empty for the staged spelling. The complete vector always travels
    /// in <see cref="GenericReference.LoweredTypeArgs"/>.
    /// </param>
    private SemanticType RecordBclExtensionMethodFact(
        Expression key,
        SemanticType ownerType,
        IReadOnlyList<SemanticType> writtenTypeArgs,
        IReadOnlyList<SemanticType> loweredTypeArgs,
        Discovery.ClrExtensionMethodResolver.Resolution resolution)
    {
        var closedParameters = resolution.ClosedMethod.GetParameters();
        var closedParameterTypes = new List<SemanticType>(Math.Max(0, closedParameters.Length - 1));
        for (int i = 1; i < closedParameters.Length; i++)
            closedParameterTypes.Add(_clrTypeBridge.Value.MapClrTypeToSemanticType(closedParameters[i].ParameterType));

        var closedReturnType = _clrTypeBridge.Value.MapClrTypeToSemanticType(resolution.ClosedMethod.ReturnType);

        _semanticInfo.SetGenericReference(key, new GenericReference
        {
            Kind = GenericReferenceKind.BclExtensionMethod,
            ReceiverType = ownerType,
            TypeArgs = writtenTypeArgs,
            LoweredTypeArgs = loweredTypeArgs,
            ClrMemberName = resolution.ClrMethodName,
            ClosedReturnType = closedReturnType,
            ClosedParameterTypes = closedParameterTypes,
        });
        return closedReturnType;
    }

    /// <summary>
    /// A no-type-args extension call whose receiver-determined type parameters are bound and whose
    /// remaining ones are waiting on the arguments (#1206). Held across the argument-checking seam by
    /// <c>CheckCall</c>: opened by <see cref="TryBeginStagedExtensionCall"/> before the arguments are
    /// checked, closed by <see cref="CompleteStagedExtensionCall"/> after.
    /// </summary>
    private sealed record StagedExtensionCall(
        MemberAccess Callee,
        SemanticType ReceiverType,
        Discovery.ClrExtensionMethodResolver.PartialResolution Partial,
        FunctionSymbol Signature)
    {
        internal IReadOnlyList<Discovery.ClrExtensionMethodResolver.PartialResolution>? AlternateCandidates { get; init; }
    }

    /// <summary>
    /// Opens the staged path for <c>lst.select(f)</c> — an extension call written with NO type
    /// arguments, which nothing resolves today: the callee types Unknown, the emitter writes the member
    /// call verbatim, and C# infers everything. That works only while the call is the whole expression;
    /// wrap it and there is no Sharpy type to wrap, so <c>list(lst.select(f))</c> emits an
    /// unparameterized <c>Sharpy.List</c> and leaks CS0305 (#1206).
    ///
    /// <para>
    /// Returns the staged call when all five conditions hold, or null to leave the call EXACTLY as
    /// permissive as it is today — no fact, no diagnostic, nothing recorded anywhere (D2). The
    /// conditions together are a proof that ordinary member resolution already failed to bind the name,
    /// which is C#'s own rule and the ordering the explicit path gets for free by running after
    /// <c>TryResolveGenericInstanceMethod</c> declined:
    /// </para>
    ///
    /// <list type="number">
    /// <item>the callee is a <see cref="MemberAccess"/> that checked to <c>Unknown</c> on a receiver
    /// whose own type is known — <c>CheckMemberAccessCore</c>'s deliberate interop fall-through;</item>
    /// <item>the member name is on the #1163 acceptance surface;</item>
    /// <item>the receiver maps to a CLR type;</item>
    /// <item>reflection proves no instance member of that name could bind
    /// (<see cref="NoClrInstanceMemberCouldBind"/>) — the load-bearing one, see its remarks;</item>
    /// <item>a single candidate accounts for the written argument shapes.</item>
    /// </list>
    ///
    /// <para>
    /// The gates are tested cheapest-first (name lookup before reflection) as a performance choice on
    /// the checker's hot path; it is their CONJUNCTION that is the contract, not their order.
    /// </para>
    /// </summary>
    private StagedExtensionCall? TryBeginStagedExtensionCall(
        FunctionCall call, Expression callee, SemanticType calleeType)
    {
        // 1. An Unknown callee on a typed receiver. Weaker than "member resolution failed" on its own —
        //    two upstream arms reach Unknown while the instance member genuinely exists — which is why
        //    gate 4 below, not this one, is what excludes the collision set.
        if (calleeType is not UnknownType || callee is not MemberAccess memberAccess)
            return null;

        var receiverType = _semanticInfo.GetExpressionType(memberAccess.Object);
        if (receiverType is null or UnknownType)
            return null;

        // 2. The acceptance surface. A dictionary lookup, so it runs before anything reflective.
        if (!Discovery.ClrExtensionMethodResolver.IsOnAcceptanceSurface(memberAccess.Member))
            return null;

        // 3. A receiver with a CLR form — the same filter that guards the explicit path.
        var receiverClrType = TryGetClrType(receiverType);
        if (receiverClrType == null)
            return null;

        // 4. Instance members beat extension methods.
        if (!NoClrInstanceMemberCouldBind(receiverType, memberAccess.Member))
            return null;

        // 4b. The receiver is wrong for EVERY overload of this name — the one gate here that REPORTS
        // instead of declining. Nothing downstream can rescue it: the emitter writes the call verbatim
        // on the permissive channel and C# answers CS0411/CS1929 behind SPY0908, the compiler filing its
        // own bug for a user's type error (#1146, #1390). `xs: list[int] = lst.order_by(f)` then
        // `xs.then_by(g)` is exactly that shape — the annotation asked for a Sharpy list, and `ThenBy`
        // extends an IOrderedEnumerable<T> and nothing else.
        //
        // Reporting is safe because the proof is about the RECEIVER ALONE: no arity, argument shape or
        // keyword spelling can turn a receiver no `this` parameter accepts into a call that binds, so
        // no gate below can contradict it. An `object` receiver is exempt — `object` is where the bridge
        // GAVE UP, so the emitted expression may well be a sequence the checker cannot see, and refusing
        // on a non-fact is how a permissive channel becomes a false error (#1206 D2).
        if (!IsObjectType(receiverType)
            && !Discovery.ClrExtensionMethodResolver.AnyOverloadAcceptsReceiver(
                receiverClrType, memberAccess.Member))
        {
            var extends = Discovery.ClrExtensionMethodResolver.ReceiverTypeNames(memberAccess.Member);
            var message = $"Type '{receiverType.GetDisplayName()}' has no member '{memberAccess.Member}'";
            if (extends.Count > 0)
            {
                // Naming what the member DOES extend is the whole steer: for `then_by` it says
                // IOrderedEnumerable, which is what tells a reader to chain rather than slot.
                message += $". '{memberAccess.Member}' is a .NET extension method on "
                    + string.Join(" or ", extends.Select(n => $"'{n}'"))
                    + "; chain it onto an expression of that type";
            }

            AddError(message, memberAccess.LineStart, memberAccess.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedMember, span: memberAccess.Span);
            return null;
        }

        // Keyword arguments have no name correspondence to a CLR extension method's parameters, and a
        // spread breaks the positional formal-to-actual alignment the staging relies on.
        if (call.KeywordArguments.Length > 0)
            return null;

        var shapes = new Discovery.ClrExtensionMethodResolver.ExtensionArgumentShape[call.Arguments.Length];
        for (int i = 0; i < call.Arguments.Length; i++)
        {
            if (call.Arguments[i] is SpreadElement)
                return null;

            shapes[i] = UnwrapParenthesized(call.Arguments[i]) is LambdaExpression lambda
                ? Discovery.ClrExtensionMethodResolver.ExtensionArgumentShape.Lambda(lambda.Parameters.Length)
                : Discovery.ClrExtensionMethodResolver.ExtensionArgumentShape.Value;
        }

        // 5. One candidate, closed as far as the receiver goes. When multiple same-arity
        // candidates differ only in parameter types (e.g., Take(int) vs Take(Range)), stage
        // all of them and let CompleteStagedExtensionCall pick by argument types (#1332).
        var partial = Discovery.ClrExtensionMethodResolver.TryResolveFromReceiver(
            receiverClrType, memberAccess.Member, shapes);
        if (partial != null)
        {
            return new StagedExtensionCall(
                memberAccess, receiverType, partial,
                BuildStagedExtensionSymbol(memberAccess.Member, partial));
        }

        var allCandidates = Discovery.ClrExtensionMethodResolver.TryResolveAllFromReceiver(
            receiverClrType, memberAccess.Member, shapes);
        if (allCandidates.Count < 2)
            return null;

        // Multiple same-arity candidates (e.g. Take(int) vs Take(Range)): build a permissive
        // signature with Unknown parameters so argument checking doesn't reject any candidate
        // prematurely. CompleteStagedExtensionCall picks the right one by argument types.
        return new StagedExtensionCall(
            memberAccess, receiverType, allCandidates[0],
            BuildPermissiveStagedExtensionSymbol(memberAccess.Member, allCandidates[0]))
        {
            AlternateCandidates = allCandidates
        };
    }

    /// <summary>
    /// The ephemeral generic <see cref="FunctionSymbol"/> the staged call is checked through: the
    /// receiver-bound candidate's post-<c>this</c> parameters and return type, with the type parameters
    /// the receiver did not determine still open.
    ///
    /// <para>
    /// Follows the #1136 <c>BuildBclGenericMethodSymbol</c> precedent — CLR method name carried
    /// verbatim, types mapped through <see cref="Discovery.ClrTypeBridge"/>, and deliberately NOT
    /// registered in the symbol table. It is consumed only by
    /// <c>TryCheckDeferredLambdaArguments</c>/<c>CheckCallArguments</c> through the existing
    /// <c>earlyFuncSymbol</c> channel, which returns it directly without a symbol-table lookup.
    /// </para>
    ///
    /// <para>
    /// The <c>this</c> parameter is dropped because the receiver is not in the argument list, so the
    /// formal-to-actual offset stays 0. Open type parameters survive as <c>TypeParameterType</c> (never
    /// <c>Unknown</c>, never <c>object</c>), which is what makes an unannotated lambda argument
    /// deferrable and what <c>SubstituteExpectedLambdaType</c> later fills in.
    /// </para>
    /// </summary>
    private FunctionSymbol BuildStagedExtensionSymbol(
        string memberName, Discovery.ClrExtensionMethodResolver.PartialResolution partial)
    {
        var clrParameters = partial.OpenMethod.GetParameters();
        var parameters = new List<ParameterSymbol>(partial.ParameterTypes.Count);
        for (int i = 0; i < partial.ParameterTypes.Count; i++)
        {
            parameters.Add(new ParameterSymbol
            {
                Name = clrParameters[i + 1].Name ?? $"arg{i}",
                Type = _clrTypeBridge.Value.MapClrTypeToSemanticType(partial.ParameterTypes[i])
            });
        }

        return new FunctionSymbol
        {
            Name = memberName,
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            ClrMethodName = partial.ClrMethodName,
            ClrMethod = partial.OpenMethod,
            TypeParameters = partial.OpenTypeParameterNames
                .Select(name => new Parser.Ast.TypeParameterDef { Name = name })
                .ToList(),
            Parameters = parameters,
            ReturnType = _clrTypeBridge.Value.MapClrTypeToSemanticType(partial.ReturnType),
            IsStatic = false
        };
    }

    private FunctionSymbol BuildPermissiveStagedExtensionSymbol(
        string memberName, Discovery.ClrExtensionMethodResolver.PartialResolution partial)
    {
        var clrParameters = partial.OpenMethod.GetParameters();
        var parameters = new List<ParameterSymbol>(partial.ParameterTypes.Count);
        for (int i = 0; i < partial.ParameterTypes.Count; i++)
        {
            parameters.Add(new ParameterSymbol
            {
                Name = clrParameters[i + 1].Name ?? $"arg{i}",
                Type = SemanticType.Unknown
            });
        }

        return new FunctionSymbol
        {
            Name = memberName,
            Kind = SymbolKind.Function,
            AccessLevel = AccessLevel.Public,
            ClrMethodName = partial.ClrMethodName,
            TypeParameters = partial.OpenTypeParameterNames
                .Select(name => new Parser.Ast.TypeParameterDef { Name = name })
                .ToList(),
            Parameters = parameters,
            ReturnType = SemanticType.Unknown,
            IsStatic = false
        };
    }

    /// <summary>
    /// Closes a staged call once its arguments have been checked: unifies the synthesized formals
    /// against the actual argument types to bind the still-open type parameters, completes the CLR
    /// vector, and records the <see cref="GenericReferenceKind.BclExtensionMethod"/> fact keyed on the
    /// <see cref="MemberAccess"/> callee (#1206).
    ///
    /// <para>
    /// Every failure path records NOTHING and reports nothing — a type parameter no argument bound, a
    /// binding with no CLR form, a constraint violation — leaving the call on the permissive channel
    /// exactly as it is today. That is the batch's hardest constraint: this closes a #1146 leak, it does
    /// not narrow an acceptance surface. In particular the explicit path's "cannot determine the type
    /// arguments" diagnostic is unreachable from here, because nothing was written to be wrong about.
    /// </para>
    ///
    /// <para>
    /// Binding runs through <c>GenericTypeInferenceService.UnifyTypes</c>, the same engine the deferred
    /// lambda pass uses, so a structural formal closes structurally: <c>SelectMany</c>'s
    /// <c>Func[int, list[TCollection]]</c> recovers <c>TCollection</c> from the lambda's actual return
    /// type rather than needing a second unifier (#1145).
    /// </para>
    /// </summary>
    private void CompleteStagedExtensionCall(StagedExtensionCall staged, List<SemanticType> argTypes)
    {
        // When multiple same-arity candidates were staged (#1332), try each one and pick
        // the first that completes. This discriminates Take(int) from Take(Range) etc.
        if (staged.AlternateCandidates != null)
        {
            foreach (var candidate in staged.AlternateCandidates)
            {
                var altStaged = staged with
                {
                    Partial = candidate,
                    Signature = BuildStagedExtensionSymbol(staged.Callee.Member, candidate),
                    AlternateCandidates = null
                };
                CompleteStagedExtensionCall(altStaged, argTypes);
                if (_semanticInfo.GetExpressionType(staged.Callee) is not null and not UnknownType)
                    return;
            }
            return;
        }

        var parameters = staged.Signature.Parameters;
        var pairs = Math.Min(parameters.Count, argTypes.Count);
        var formals = new List<SemanticType>(pairs);
        var actuals = new List<SemanticType>(pairs);
        for (int i = 0; i < pairs; i++)
        {
            formals.Add(parameters[i].Type);
            actuals.Add(argTypes[i]);
        }

        var substitutions = _genericInference.UnifyTypes(formals, actuals);

        var inferred = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var openName in staged.Partial.OpenTypeParameterNames)
        {
            if (substitutions == null
                || !substitutions.TryGetValue(openName, out var bound)
                || bound is UnknownType
                || TryGetClrType(bound) is not { } boundClrType)
            {
                return;
            }
            inferred[openName] = boundClrType;
        }

        var resolution = Discovery.ClrExtensionMethodResolver.TryCompleteFromInferredTypeArguments(
            staged.Partial, inferred);
        if (resolution == null)
            return;

        // A return type that maps to `object` means the bridge could not represent the real one.
        // Recording it would type the call `object`, which is strictly WORSE than Unknown.
        if (IsObjectType(_clrTypeBridge.Value.MapClrTypeToSemanticType(resolution.ClosedMethod.ReturnType)))
            return;

        // Verify closed parameter types match actual arguments before recording (#1332).
        // Without this, same-arity candidates (Take(int) vs Take(Range)) record the wrong
        // fact and ValidateClosedExtensionArguments emits a false SPY0220.
        var closedParams = resolution.ClosedMethod.GetParameters();
        for (int i = 0; i < argTypes.Count && i + 1 < closedParams.Length; i++)
        {
            var closedParamType = _clrTypeBridge.Value.MapClrTypeToSemanticType(closedParams[i + 1].ParameterType);
            if (argTypes[i] is not UnknownType && closedParamType is not UnknownType
                && !IsAssignable(argTypes[i], closedParamType))
            {
                return;
            }
        }

        var loweredTypeArgs = new List<SemanticType>(resolution.TypeArguments.Count);
        foreach (var clrArg in resolution.TypeArguments)
            loweredTypeArgs.Add(_clrTypeBridge.Value.MapClrTypeToSemanticType(clrArg));

        RecordBclExtensionMethodFact(
            staged.Callee, staged.ReceiverType, Array.Empty<SemanticType>(), loweredTypeArgs, resolution);
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
    /// The single arity-check seam for generic FUNCTION references (#1004, generalized). Fills a
    /// short vector's trailing PEP-696 defaults through the shared
    /// <see cref="FillTrailingTypeArgumentDefaults"/>, then emits the deliberate
    /// wrong-type-argument-count diagnostic (SPY0224) and returns <c>false</c> only if the vector is
    /// still incomplete or is excessive.
    ///
    /// <para>Function type parameters carry declared defaults exactly as type parameters do —
    /// <c>ValidateTypeParameterDefaultOrdering</c> runs for functionDef alongside
    /// classDef/structDef/interfaceDef — so <c>def pair[K, V = str]</c> then <c>pair[int]</c> must
    /// fill, just as <c>Pair[int]</c> does. It did not until #1219; the type seam had been
    /// default-aware since #1192 and this one was a strict count check.</para>
    ///
    /// <para><b>Ordering:</b> both callers that pre-select a symbol do so on the UNFILLED count and
    /// must keep doing so. <c>SelectArityMatchingOverload</c> picks among multi-arity builtin
    /// overloads (<c>map[int, int]</c> vs <c>map[int, int, int]</c>) and
    /// <c>TryResolveGenericInstanceMethod</c> steers BCL overload selection from the index shape;
    /// filling defaults first would change which overload a deficient vector selects. A defaulted
    /// USER declaration has one overload, so filling after the selection is well-defined for the
    /// case this seam is about.</para>
    ///
    /// <para><paramref name="typeArgs"/> is completed IN PLACE, matching the type seam: the caller
    /// builds its <see cref="GenericFunctionType.TypeArguments"/> from this same list, so a fill
    /// computed into a copy would never reach the emitted C#.</para>
    /// </summary>
    private bool CheckGenericReferenceArity(
        string calleeName, IReadOnlyList<TypeParameterDef> typeParameters,
        List<SemanticType> typeArgs, IndexAccess indexAccess)
    {
        var expected = typeParameters.Count;
        if (typeArgs.Count == expected)
            return true;

        if (typeArgs.Count < expected)
        {
            FillTrailingTypeArgumentDefaults(typeParameters, typeArgs);
            if (typeArgs.Count == expected)
                return true;
        }

        AddError(
            $"Generic function '{calleeName}' expects {expected} type argument(s) but got {typeArgs.Count}",
            indexAccess.LineStart,
            indexAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.WrongArgumentCount,
            span: indexAccess.Span);
        return false;
    }

    /// <summary>
    /// The CONSTRAINT half of the explicit-type-argument seam (#1289), run right after
    /// <see cref="CheckGenericReferenceArity"/> at every site that binds a written vector to a generic
    /// function. Inference has checked its own answers since the constraint machinery existed; the
    /// written spelling was checked by nobody, so <c>describe[Unrelated](u)</c> type-checked clean and
    /// came back from Roslyn as CS0311 behind SPY0908 — a compiler-bug report for a constraint the
    /// user violated in the source.
    ///
    /// <para>The comparator is the inference service's, called with the written vector instead of the
    /// inferred one, so the two paths cannot disagree about what satisfies a constraint. Only the
    /// leading noun of the diagnostic differs ("Type argument" rather than "Inferred type"), and the
    /// code is SPY0237 either way.</para>
    ///
    /// <para>Returns false when a violation was reported, which the callers treat exactly as they
    /// treat an arity failure: the reference is handled, and nothing downstream builds a call whose
    /// type arguments are known not to bind.</para>
    /// </summary>
    private bool CheckGenericReferenceConstraints(
        IReadOnlyList<TypeParameterDef> typeParameters,
        IReadOnlyList<SemanticType> typeArgs,
        IndexAccess indexAccess)
    {
        var result = _genericInference.CheckWrittenTypeArguments(typeParameters, typeArgs);
        if (result.Success)
            return true;

        AddError(
            result.ErrorMessage ?? "Type argument does not satisfy its constraint",
            indexAccess.LineStart,
            indexAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.CannotInferGenericType,
            span: indexAccess.Span);
        return false;
    }

    /// <summary>
    /// Completes a short type-argument vector from its parameters' declared PEP-696 defaults, in
    /// place, stopping at the first parameter without one (defaults are trailing-only, enforced by
    /// <c>ValidateTypeParameterDefaultOrdering</c>). The one answer to "what does a short vector
    /// mean?", shared by the function and type seams (#1219) — the two keep their own diagnostic
    /// wording, which is user-visible and pinned, but not their own notion of completeness.
    ///
    /// <para>Each default resolves through <c>TypeResolver.ResolveTypeParameterDefault</c>, which
    /// puts the PRECEDING parameters in scope and substitutes the arguments filled so far. That is
    /// what makes <c>class Dup[K, V = K]</c> referenced as <c>Dup[str]</c> mean <c>Dup[str, str]</c>
    /// rather than failing SPY0202 on a <c>K</c> that is defined one position to the left (#1245).
    /// Filling in order matters: <paramref name="typeArgs"/> grows as it goes, so a chained default
    /// (the PEP's <c>slice</c> example) sees the value its predecessor just resolved to.</para>
    /// </summary>
    private void FillTrailingTypeArgumentDefaults(
        IReadOnlyList<TypeParameterDef> typeParameters, List<SemanticType> typeArgs)
    {
        for (int i = typeArgs.Count; i < typeParameters.Count; i++)
        {
            var typeParam = typeParameters[i];
            if (typeParam.DefaultType == null)
                break;
            typeArgs.Add(_typeResolver.ResolveTypeParameterDefault(typeParameters, i, typeArgs));
        }
    }

    /// <summary>
    /// The arity-check seam for generic TYPE references — <c>Box[int]</c>, <c>difflib.SequenceMatcher[str]</c>,
    /// <c>Outer.Inner[int]</c> — the type-side counterpart of <see cref="CheckGenericReferenceArity"/>
    /// (#1192). PEP-696 default-aware: a short vector fills its trailing parameters from their
    /// declared defaults through the shared <see cref="FillTrailingTypeArgumentDefaults"/>, and only
    /// an excess or unfillable vector is rejected. The function seam does the same since #1219.
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
            FillTrailingTypeArgumentDefaults(typeSymbol.TypeParameters, typeArgs);
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
