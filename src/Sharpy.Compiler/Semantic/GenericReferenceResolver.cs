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

    /// <summary>A generic type reference, e.g. <c>Box[int]</c>.</summary>
    GenericTypeRef,

    /// <summary>An array type reference, e.g. <c>array[int]</c>.</summary>
    ArrayTypeRef,
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
    /// For <see cref="GenericReferenceKind.Builtin"/>, the arity-selected overload (which may differ
    /// from the first-by-name symbol — #999); otherwise the resolved target itself. Consumers validate
    /// value arguments against this signature (#1148).
    /// </summary>
    public FunctionSymbol? SelectedOverload { get; init; }
}

internal partial class TypeChecker
{
    /// <summary>
    /// Single resolution step for a generic reference <c>callee[T, ...]</c>. Normalizes every callee
    /// kind (array/type/function, bare or module- or instance-qualified) into one
    /// <see cref="GenericReference"/> fact and records it node-keyed in <see cref="SemanticInfo"/>,
    /// alongside the same <see cref="GenericFunctionType"/>/<see cref="GenericType"/> expression type the
    /// per-arm code recorded before. Returns <c>true</c> when the index access IS a generic reference —
    /// either resolved cleanly (<paramref name="resultType"/> is the reference type) or rejected with a
    /// deliberate arity diagnostic (<paramref name="resultType"/> is <see cref="SemanticType.Unknown"/>).
    /// Returns <c>false</c> for ordinary value indexing, having recorded nothing and emitted no
    /// diagnostic, so <see cref="CheckIndexAccessCore"/> proceeds to the value-indexing path.
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

            var symbol = _symbolTable.Lookup(typeId.Name);

            // Box[int] -> GenericType. As before, no expression type is recorded for indexAccess.Object
            // (the bare Box identifier), keeping ProtocolValidator quiet.
            if (symbol is TypeSymbol genericTypeSymbol && genericTypeSymbol.IsGeneric)
            {
                var typeArgs = TryResolveTypeArguments(indexAccess.Index);
                if (typeArgs != null)
                {
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
            }
        }

        return false;
    }

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
