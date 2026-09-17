using System.Linq;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Total classification of whether an <c>as?</c>/<c>as!</c> coercion COULD succeed at runtime.
///
/// <para>Before #1713 the type checker leaned on a "default: allow, let the C# compiler validate"
/// fall-through, so every statically-impossible coercion (<c>bytes as? long</c>, <c>str as! double</c>,
/// unrelated classes, <c>list[int] as? list[str]</c>) sailed through semantic analysis and blew up in
/// Roslyn as CS8121/CS0030 — surfaced to the user as SPY0908 "generated C# failed to compile", an
/// internal-error net rather than a diagnosis. This classifier is TOTAL: every pair lands in exactly
/// one <see cref="Kind"/>, and only <see cref="Kind.Impossible"/> is refused (SPY0610), by name and with
/// a steer. The refusal is on the EXPRESSION, uniform across <c>as?</c> and <c>as!</c> and across every
/// consumption (value, truthiness, assert) — the consumer never enters into it (R-E).</para>
///
/// <para>The allowed kinds mirror what the emitter and the CLR will accept:
/// interface-satisfiable pairs stay allowed because Sharpy classes are emitted UNSEALED (a subtype could
/// implement the interface); a type parameter on either side is allowed because C# decides possibility at
/// instantiation; the numeric lowering owns the runtime range check for numeric↔numeric.</para>
/// </summary>
internal static class CoercionPossibility
{
    internal enum Kind
    {
        /// <summary>Source and target are the same type.</summary>
        Identity,

        /// <summary>Source is <c>object</c> (or an unmapped CLR type) or an interface — an unboxing/downcast the CLR checks at runtime.</summary>
        Unboxing,

        /// <summary>Target is <c>object</c> — always a valid boxing/upcast.</summary>
        Boxing,

        /// <summary>Both sides are numeric — the numeric coercion lowering owns the runtime narrowing check.</summary>
        Numeric,

        /// <summary>A user-defined <c>__explicit__</c> operator exists in one direction.</summary>
        UserExplicit,

        /// <summary>Both are user-defined types with an inheritance relationship (either direction).</summary>
        Inheritance,

        /// <summary>Target or source is an interface; an unsealed class could satisfy it.</summary>
        InterfaceSatisfiable,

        /// <summary>One side is an enum and the other is a numeric backing type.</summary>
        EnumBacking,

        /// <summary>Same generic definition with the same type arguments (identity that structural <see cref="SemanticType.Equals(object)"/> may miss across definition sources).</summary>
        GenericSameDefinitionSameArgs,

        /// <summary>Source or target is an open type parameter — C# decides possibility at instantiation.</summary>
        TypeParameter,

        /// <summary>No relationship the runtime could satisfy — refused with SPY0610.</summary>
        Impossible,
    }

    /// <summary>
    /// Classifies the coercion <c>source as?/as! target</c>. The order matters: the earlier, broader
    /// allow-arms (identity, type parameter, object, interface) subsume the narrower ones, and only a
    /// pair that matches none is <see cref="Kind.Impossible"/>.
    /// </summary>
    internal static Kind Classify(SemanticType source, SemanticType target, SemanticBinding? binding)
    {
        // Possibility is a property of the UNDERLYING runtime types: `int? as? int` is possible (identity
        // once unwrapped) and `int as? str?` is impossible (int→str, regardless of the target's
        // nullability). Unwrap both sides so the classification below reasons about the underlying types.
        source = Unwrap(source);
        target = Unwrap(target);

        if (source.Equals(target))
            return Kind.Identity;

        // Same underlying type symbol: an import alias and its target share one ClrType (and one
        // definition), so `Match as NetMatch` — where NetMatch is `from ... import Match as NetMatch`
        // — is identity even though the two SemanticType spellings carry different Names and so miss
        // Equals's CanonicalKey comparison. Restricted to NON-generic types: two instantiations of one
        // generic definition (list[int] vs list[str]) share a definition symbol but are NOT identity,
        // and must fall through to the type-argument-matching arm below.
        if (source is not GenericType && target is not GenericType)
        {
            var sSym = TypeSymbolOf(source);
            var tSym = TypeSymbolOf(target);
            if (sSym != null && tSym != null && TypeHierarchyService.IsSameType(sSym, tSym))
                return Kind.Identity;
        }

        // A type parameter on either side is open: the instantiation decides, not this site.
        if (source is TypeParameterType || target is TypeParameterType)
            return Kind.TypeParameter;

        // Unboxing/downcast: object (or an unmapped CLR value) or an interface SOURCE.
        if (IsObjectLike(source) || IsInterface(source))
            return Kind.Unboxing;

        // Boxing/upcast: object TARGET.
        if (IsObjectLike(target))
            return Kind.Boxing;

        // Interface on the target (or source) — an unsealed class could satisfy it.
        if (IsInterface(target))
            return Kind.InterfaceSatisfiable;

        // Enum <-> numeric backing.
        if (IsEnumNumericPair(source, target))
            return Kind.EnumBacking;

        // Numeric <-> numeric — the numeric lowering owns the runtime narrowing check.
        if (PrimitiveCatalog.IsNumeric(source) && PrimitiveCatalog.IsNumeric(target))
            return Kind.Numeric;

        // A user-defined __explicit__ operator in either direction.
        if (HasExplicitConversion(source, target) || HasExplicitConversion(target, source))
            return Kind.UserExplicit;

        // Inheritance relationship (either direction). Reads the declaring symbol from a user-defined
        // type OR a generic type's definition, so a generic subject reaches its (possibly generic) base
        // — Pair[int] as? Box, where Pair[T] derives from Box[list[T]] (#1308).
        // The two symbols must be DISTINCT: InheritsFrom's CLR self-assignability fallback treats a
        // symbol as inheriting from itself, which would wrongly admit two instantiations of ONE
        // definition (list[int] as? list[str]). Same-definition pairs fall through to the arm below,
        // which requires matching type arguments.
        var sourceSymbol = TypeSymbolOf(source);
        var targetSymbol = TypeSymbolOf(target);
        if (sourceSymbol != null && targetSymbol != null
            && !TypeHierarchyService.IsSameType(sourceSymbol, targetSymbol)
            && (TypeHierarchyService.InheritsFrom(sourceSymbol, targetSymbol, binding)
                || TypeHierarchyService.InheritsFrom(targetSymbol, sourceSymbol, binding)))
            return Kind.Inheritance;

        // Same generic definition with the SAME type arguments. Different arguments (list[int] vs
        // list[str]) are NOT castable — Sharpy collections are invariant classes (#1330), so this arm
        // deliberately does not admit them.
        if (source is GenericType sg && target is GenericType tg
            && SameGenericDefinition(sg, tg)
            && sg.TypeArguments.Count == tg.TypeArguments.Count
            && sg.TypeArguments.SequenceEqual(tg.TypeArguments))
            return Kind.GenericSameDefinitionSameArgs;

        return Kind.Impossible;
    }

    private static SemanticType Unwrap(SemanticType type) => type switch
    {
        OptionalType opt => Unwrap(opt.UnderlyingType),
        NullableType nul => Unwrap(nul.UnderlyingType),
        _ => type,
    };

    private static TypeSymbol? TypeSymbolOf(SemanticType type) => type switch
    {
        UserDefinedType udt => udt.Symbol,
        GenericType gt => gt.GenericDefinition,
        _ => null,
    };

    private static bool IsObjectLike(SemanticType type)
        => type is BuiltinType { Name: "object" } or UserDefinedType { Name: "object" } or UnmappedClrType;

    private static bool IsInterface(SemanticType type)
        => type is UserDefinedType { Symbol.TypeKind: TypeKind.Interface };

    private static bool IsEnumNumericPair(SemanticType a, SemanticType b)
        => (a is UserDefinedType { Symbol.TypeKind: TypeKind.Enum } && PrimitiveCatalog.IsNumeric(b))
        || (b is UserDefinedType { Symbol.TypeKind: TypeKind.Enum } && PrimitiveCatalog.IsNumeric(a));

    private static bool SameGenericDefinition(GenericType a, GenericType b)
    {
        if (a.GenericDefinition != null && b.GenericDefinition != null)
            return TypeHierarchyService.IsSameType(a.GenericDefinition, b.GenericDefinition);
        if (a.GenericDefinition == null && b.GenericDefinition == null)
            return a.Name == b.Name;
        return false;
    }

    /// <summary>
    /// Whether <paramref name="source"/> declares a static <c>__explicit__(self-typed) -> target</c>
    /// conversion. Pure — reads only the symbol's declared methods.
    /// </summary>
    private static bool HasExplicitConversion(SemanticType source, SemanticType target)
    {
        if (source is not UserDefinedType { Symbol: { } symbol })
            return false;

        foreach (var method in symbol.Methods)
        {
            if (method.Name != DunderNames.Explicit || !method.IsStatic)
                continue;
            if (method.Parameters.Count == 1 && method.ReturnType != null && method.ReturnType.Equals(target))
                return true;
        }

        return false;
    }
}
