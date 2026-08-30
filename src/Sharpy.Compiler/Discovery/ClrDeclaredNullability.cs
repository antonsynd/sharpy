using System.Reflection;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Reads the nullable-reference-type annotation a CLR member DECLARES (<c>string?</c>,
/// <c>Dict&lt;string, string&gt;? Proxies</c>) and wraps the member's mapped type in
/// <see cref="NullableType"/> when the declaration says null is a value of it. A reflected
/// <see cref="System.Type"/> carries no NRT information — <c>string?</c> and <c>string</c> are the
/// same <c>typeof(string)</c> — so the annotation must be read from the MEMBER through
/// <see cref="NullabilityInfoContext"/>, at the one point every member-typed reflection site maps
/// through; otherwise a member declared nullable is typed non-nullable and a store of <c>None</c>
/// into it is refused (SPY0229) for a value C# accepts (#1705).
/// </summary>
/// <remarks>
/// <para>
/// Only the top-level state is read: <c>List&lt;string?&gt;</c> stays <c>list[str]</c>. An assembly
/// without NRT annotations reports <see cref="NullabilityState.Unknown"/>, which is treated as
/// non-nullable — exactly the type Sharpy reads today, so unannotated surfaces do not move.
/// </para>
/// <para>
/// A value-typed <c>Nullable&lt;T&gt;</c> is already a distinct <see cref="System.Type"/> and is mapped
/// to <see cref="NullableType"/> by <see cref="ClrTypeBridge.MapClrTypeToSemanticType"/>; it is not
/// wrapped twice here.
/// </para>
/// </remarks>
internal static class ClrDeclaredNullability
{
    // NullabilityInfoContext caches per-member and is not thread-safe; compilations run in
    // parallel under the test host, so one context is shared behind a gate.
    private static readonly NullabilityInfoContext Context = new();
    private static readonly object Gate = new();

    /// <summary>Whether reading the property yields a value declared nullable, or writing one accepts null.</summary>
    internal static bool DeclaresNullable(PropertyInfo property)
    {
        var info = Create(() => Context.Create(property));
        return info?.ReadState == NullabilityState.Nullable || info?.WriteState == NullabilityState.Nullable;
    }

    /// <summary>Whether reading the field yields a value declared nullable, or writing one accepts null.</summary>
    internal static bool DeclaresNullable(FieldInfo field)
    {
        var info = Create(() => Context.Create(field));
        return info?.ReadState == NullabilityState.Nullable || info?.WriteState == NullabilityState.Nullable;
    }

    /// <summary>Whether the method's return value is declared nullable.</summary>
    internal static bool DeclaresNullableReturn(MethodInfo method)
        => Create(() => Context.Create(method.ReturnParameter))?.ReadState == NullabilityState.Nullable;

    /// <summary>Whether the parameter accepts a null argument by declaration.</summary>
    internal static bool DeclaresNullableArgument(ParameterInfo parameter)
        => Create(() => Context.Create(parameter))?.WriteState == NullabilityState.Nullable;

    /// <summary>
    /// The member's mapped type with its declared nullability applied: wrapped in
    /// <see cref="NullableType"/> when <paramref name="declaredNullable"/> and the mapping is not
    /// already nullable or unknown; otherwise <paramref name="mapped"/> itself.
    /// </summary>
    internal static SemanticType Apply(SemanticType mapped, bool declaredNullable)
        => declaredNullable && mapped is not NullableType and not UnknownType and not OptionalType
            ? new NullableType { UnderlyingType = mapped }
            : mapped;

    private static NullabilityInfo? Create(System.Func<NullabilityInfo> create)
    {
        try
        {
            lock (Gate)
            {
                return create();
            }
        }
        catch (System.Exception ex) when (ex is System.NotSupportedException or System.ArgumentException
                                              or System.TypeLoadException or System.IO.FileNotFoundException)
        {
            // A member the context cannot describe (a by-ref-like or open generic shape) keeps the
            // type it has today rather than failing the whole resolution.
            return null;
        }
    }
}
