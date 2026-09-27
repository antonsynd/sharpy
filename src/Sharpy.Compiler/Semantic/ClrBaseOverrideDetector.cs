using System;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Decides whether a <c>.spy</c> instance method overrides an abstract/virtual member of a
/// CLR-backed base type (#1122). The decision is a pure function of the derived method's shape,
/// the resolved base-type chain, and a CLR reflection probe, so it lives here as a testable static
/// helper called by <c>TypeChecker.FindClrOverrideTarget</c> — the one decision the override rule
/// (#2138) and the emitter's <c>override</c> modifier both read. Keeping it separate from the
/// TypeChecker's mutable pass state (and from the Discovery layer that owns reflection) lets the D2
/// matching rule be unit-tested against synthetic hierarchies and fake probes.
/// </summary>
internal static class ClrBaseOverrideDetector
{
    /// <summary>
    /// Returns whether the described derived instance method should be emitted with the
    /// <c>override</c> modifier because it overrides an abstract/virtual member of a CLR-backed
    /// base type. Static methods, constructors, and methods already carrying an override (via the
    /// decorator/dunder path) never qualify.
    /// </summary>
    /// <param name="methodName">Sharpy-facing method name (e.g. <c>generate</c>).</param>
    /// <param name="isStatic">Whether the derived method is static (no <c>self</c> receiver).</param>
    /// <param name="isInitConstructor">Whether the derived method is <c>__init__</c>.</param>
    /// <param name="isAlreadyOverride">Whether the method is already an override (decorator/dunder).</param>
    /// <param name="arity">Derived parameter count excluding the implicit <c>self</c>/<c>cls</c> receiver.</param>
    /// <param name="baseTypes">The resolved base-type chain (immediate parent to root).</param>
    /// <param name="clrMemberProbe">
    /// Reflection probe (<c>clrType, sharpyName, arity → bool</c>) that reports whether a CLR type
    /// exposes a matching abstract/virtual member. Supplied by the caller so the Discovery-owned
    /// reflection stays out of this Semantic-layer helper; tests pass a fake. Used for CLR base
    /// types whose members are not eagerly discovered into the bridged <see cref="TypeSymbol"/>.
    /// </param>
    public static bool ShouldEmitClrOverride(
        string methodName,
        bool isStatic,
        bool isInitConstructor,
        bool isAlreadyOverride,
        int arity,
        IReadOnlyList<TypeSymbol> baseTypes,
        Func<Type, string, int, bool>? clrMemberProbe = null)
    {
        if (isAlreadyOverride)
            return false;

        Func<Type, string, int, bool?>? kindProbe = clrMemberProbe == null
            ? null
            : (type, name, n) => clrMemberProbe(type, name, n) ? false : null;
        return FindClrOverrideTarget(methodName, isStatic, isInitConstructor, arity, baseTypes, kindProbe) != null;
    }

    /// <summary>
    /// The CLR-base member the described derived instance method overrides, or <c>null</c> when it
    /// overrides none — the one decision the override rule (#2138: <c>@override</c> required, and
    /// accepted) and the emitter's <c>override</c> modifier both read. Static methods and
    /// constructors never qualify.
    /// </summary>
    /// <param name="clrMemberProbe">
    /// Reflection probe (<c>clrType, sharpyName, arity → abstract? / null</c>) for CLR base types
    /// whose members are not bridged into the <see cref="TypeSymbol"/> (a bare CLR-namespace import);
    /// <c>true</c> = abstract, <c>false</c> = virtual, <c>null</c> = no overridable member.
    /// </param>
    public static ClrOverrideTarget? FindClrOverrideTarget(
        string methodName,
        bool isStatic,
        bool isInitConstructor,
        int arity,
        IReadOnlyList<TypeSymbol> baseTypes,
        Func<Type, string, int, bool?>? clrMemberProbe = null)
    {
        if (isStatic || isInitConstructor)
            return null;

        foreach (var baseType in baseTypes)
        {
            // 1. Bridged members (a [SharpyModule]-stamped module's types populate Methods).
            var bridged = FindOverriddenClrBaseMember(methodName, arity, baseType);
            if (bridged != null)
                return new ClrOverrideTarget(baseType, bridged.IsAbstract);

            // 2. CLR base types whose members are not bridged (a bare CLR-namespace import): probe
            //    via reflection in the Discovery layer. The emitter never reflects.
            if (baseType.ClrType != null && clrMemberProbe != null
                && clrMemberProbe(baseType.ClrType, methodName, arity) is { } isAbstract)
                return new ClrOverrideTarget(baseType, isAbstract);
        }

        return null;
    }

    /// <summary>
    /// Finds an eagerly-discovered CLR-backed base member (in <paramref name="baseType"/>'s
    /// <see cref="TypeSymbol.Methods"/>) overridden by an instance method with the given
    /// Sharpy-facing name and arity, or <c>null</c> when none matches. The matched member must come
    /// from a CLR-backed base (<see cref="TypeSymbol.ClrType"/> != null, or the bridged member
    /// carries <see cref="FunctionSymbol.ClrMethodName"/>), be an instance member, be abstract or
    /// virtual, and match the arity exactly. Sealed base members are not distinguished (bridged
    /// symbols only carry IsVirtual/IsAbstract); a rare override of a sealed CLR member relies on
    /// the C# compiler to reject it.
    /// </summary>
    public static FunctionSymbol? FindOverriddenClrBaseMember(
        string methodName, int arity, TypeSymbol baseType)
    {
        bool baseIsClr = baseType.ClrType != null;
        foreach (var baseMethod in baseType.Methods)
        {
            if (baseMethod.Name != methodName)
                continue;
            // Scope guard: the matched member must come from a CLR-backed base.
            if (!baseIsClr && baseMethod.ClrMethodName == null)
                continue;
            // Overridable only when it is an instance member that is abstract or virtual.
            if (baseMethod.IsStatic || (!baseMethod.IsAbstract && !baseMethod.IsVirtual))
                continue;
            if (baseMethod.Parameters.Count != arity)
                continue;

            return baseMethod;
        }

        return null;
    }
}

/// <summary>A CLR-base member overridden by a <c>.spy</c> method: its owning base and whether it is abstract.</summary>
internal readonly record struct ClrOverrideTarget(TypeSymbol Owner, bool IsAbstract);
