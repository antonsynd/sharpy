using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Resolves the semantic type of a CLR member on any CLR-origin receiver, accepting both
/// the reverse-mangled Pythonic name and the verbatim CLR name. Used by the TypeChecker to
/// type members that the Sharpy discovery layer did not eagerly discover into a TypeSymbol
/// (#1640). Reflection lives here (Discovery), never in the emitter (#974).
/// </summary>
internal sealed class ClrMemberTypeResolver
{
    private readonly ClrTypeBridge _bridge;

    internal ClrMemberTypeResolver(ClrTypeBridge bridge)
    {
        _bridge = bridge;
    }

    /// <summary>
    /// Resolves a member on <paramref name="clrType"/> by <paramref name="memberName"/>,
    /// accepting both the Pythonic (reverse-mangled) spelling and the verbatim CLR name.
    /// </summary>
    /// <param name="receiverKind">
    /// Whether the receiver is a VALUE (<c>dt.year</c>) or the TYPE itself (<c>DateTime.max_value</c>).
    /// The two see disjoint member sets in C#, so the seam asks the same question the emitted code
    /// will: a static member on an instance receiver is CS0176 and an instance member on a type
    /// receiver is CS0120, neither of which the reflected type of the OTHER member describes.
    /// </param>
    internal ClrMemberResolution Resolve(Type clrType, string memberName, ClrReceiverKind receiverKind)
    {
        try
        {
            return ResolveCore(clrType, memberName, receiverKind);
        }
        catch (Exception ex) when (ex is ReflectionTypeLoadException or TypeLoadException
                                      or System.IO.FileNotFoundException or NotSupportedException)
        {
            return ClrMemberResolution.Inconclusive;
        }
    }

    private ClrMemberResolution ResolveCore(Type clrType, string memberName, ClrReceiverKind receiverKind)
    {
        // FlattenHierarchy is what makes an inherited static reachable through the derived spelling,
        // which is exactly how C# binds it.
        var flags = BindingFlags.Public | (receiverKind == ClrReceiverKind.StaticType
            ? BindingFlags.Static | BindingFlags.FlattenHierarchy
            : BindingFlags.Instance);

        // 1. Try methods — match Pythonic or verbatim CLR name
        var methods = clrType.GetMethods(flags)
            .Where(m => !m.IsSpecialName && !m.IsGenericMethodDefinition && Matches(m.Name, memberName, ReverseNameContext.Method))
            .ToList();

        if (methods.Count > 0)
        {
            return ResolveMethodGroup(methods);
        }

        // 2. Try properties — match Pythonic or verbatim CLR name
        foreach (var prop in clrType.GetProperties(flags))
        {
            if (prop.GetIndexParameters().Length > 0)
                continue;

            if (Matches(prop.Name, memberName, ReverseNameContext.Property))
            {
                if (!TryMapFaithfully(prop.PropertyType, ClrDeclaredNullability.DeclaresNullable(prop), out var propType))
                    return ClrMemberResolution.Inconclusive;

                var clrPropertyName = prop.Name;
                return new ClrMemberResolution.Property(propType!, clrPropertyName);
            }
        }

        // 3. Try fields — match Pythonic or verbatim CLR name
        foreach (var field in clrType.GetFields(flags))
        {
            if (Matches(field.Name, memberName, ReverseNameContext.Property))
            {
                if (!TryMapFaithfully(field.FieldType, ClrDeclaredNullability.DeclaresNullable(field), out var fieldType))
                    return ClrMemberResolution.Inconclusive;

                return new ClrMemberResolution.Field(fieldType!, field.Name);
            }
        }

        // The name is absent from the half of the surface this receiver can reach. When the OTHER
        // half has it (`dt.max_value`, `DateTime.year`) the reference is a static/instance mix-up,
        // not a typo: this seam declines rather than refuses, because naming that error is a
        // separate class from typing a member and refusing it here would be the false refusal
        // #1243/#1260 warn about (the permissive channel keeps its current behaviour).
        if (ExistsOnOppositeHalf(clrType, memberName, receiverKind))
            return ClrMemberResolution.Inconclusive;

        return ClrMemberResolution.Absent;
    }

    /// <summary>
    /// The both-spellings rule in one place: a reference matches a CLR member when it is written
    /// either as the reverse-mangled Sharpy name (<c>index_of</c>) or verbatim (<c>IndexOf</c>).
    /// </summary>
    private static bool Matches(string clrName, string memberName, ReverseNameContext context)
        => clrName == memberName || NameMangler.ToSharpyName(clrName, context) == memberName;

    private static bool ExistsOnOppositeHalf(Type clrType, string memberName, ClrReceiverKind receiverKind)
    {
        var opposite = BindingFlags.Public | (receiverKind == ClrReceiverKind.StaticType
            ? BindingFlags.Instance
            : BindingFlags.Static | BindingFlags.FlattenHierarchy);

        return clrType.GetMethods(opposite).Any(m => Matches(m.Name, memberName, ReverseNameContext.Method))
            || clrType.GetProperties(opposite).Any(p => Matches(p.Name, memberName, ReverseNameContext.Property))
            || clrType.GetFields(opposite).Any(f => Matches(f.Name, memberName, ReverseNameContext.Property));
    }

    /// <summary>
    /// Maps a reflected CLR type to the semantic type that HONESTLY describes it, or fails.
    /// A member this seam cannot describe faithfully must stay on the permissive channel: typing it
    /// with a lie refuses working interop, which is strictly worse than the Unknown it replaces
    /// (#1243, #1260).
    /// </summary>
    /// <remarks>
    /// Two shapes are declined by identity rather than by their mapping:
    /// <list type="bullet">
    /// <item><b>Enums.</b> The bridge maps a CLR enum onto its underlying <c>int32</c>, which is not
    /// what the value IS: <c>ex.socket_error_code == SocketError.TimedOut</c> would become
    /// int-vs-enum (SPY0222) and <c>sock.shutdown(how as! SocketShutdown)</c> enum-vs-int (SPY0220),
    /// both of them working programs the emitter binds today (measured on
    /// <c>Sharpy.Stdlib/spy/socket_module.spy</c>).</item>
    /// <item><b>Open generics.</b> Reflecting a STATIC member on an unconstructed definition yields
    /// the definition's own type parameters — <c>dict.fromkeys</c> came back as
    /// <c>dict[str, V]</c>, a type with a free variable no destination can accept.</item>
    /// <item><b>Declared nullability.</b> The mapped type is wrapped in <see cref="NullableType"/> when the
    /// member declares <c>T?</c> — read from the member, never from the Type (#1705).</item>
    /// <item><b>A scalar <c>char</c>.</b> Sharpy reads a CLR char as a one-character <c>str</c> at
    /// a call's RESULT (#1291), but a char-typed member is also written as a char ARGUMENT:
    /// <c>IoPath.DirectorySeparatorChar</c> feeds <c>trim_end(char, char)</c> in
    /// <c>Sharpy.Stdlib/spy/tempfile_module.spy</c>, and projecting the field to <c>str</c> made the
    /// emitter materialize a string into a char slot (CS1503). The projection belongs to the seam
    /// that owns the conversion, not to the member's type.</item>
    /// </list>
    /// </remarks>
    private bool TryMapFaithfully(Type clrType, bool declaredNullable, out SemanticType? mapped)
    {
        mapped = null;

        if (clrType.IsEnum || clrType.ContainsGenericParameters || clrType == typeof(char))
            return false;

        var candidate = _bridge.MapClrTypeToSemanticType(clrType);
        if (candidate is UnknownType)
            return false;

        // The reflected Type is NRT-blind; the member's own declaration says whether null is a
        // value of it (#1705).
        mapped = ClrDeclaredNullability.Apply(candidate, declaredNullable);
        return true;
    }

    private ClrMemberResolution ResolveMethodGroup(List<MethodInfo> methods)
    {
        if (methods.Count == 1)
        {
            var method = methods[0];
            if (!TryMapFaithfully(method.ReturnType, ClrDeclaredNullability.DeclaresNullableReturn(method), out var returnType))
                return ClrMemberResolution.Inconclusive;

            // A char-returning method is declined so the call seam handles
            // the char→str projection on the call node (#1291).
            if (returnType is BuiltinType bt && bt.ClrType == typeof(char))
                return ClrMemberResolution.Inconclusive;

            var parameters = method.GetParameters();

            // A `params` tail or a by-ref parameter has no faithful FunctionType here: typing the
            // member would hand the call seam an arity/assignability rule the CLR does not use, and
            // a call the emitter binds today would be refused — strictly worse than the Unknown it
            // replaces (#1243, #1260). Declined, so the permissive channel keeps them.
            if (parameters.Any(p => p.ParameterType.IsByRef
                    || p.GetCustomAttribute<ParamArrayAttribute>() != null))
                return ClrMemberResolution.Inconclusive;

            // A CHAR parameter is the argument direction of the same projection the char RETURN is
            // declined for: Sharpy has no char, so a `str` argument is what a user writes and the
            // call seam owns that conversion (#1402). Typing the member would make the seam's own
            // assignability rule refuse the working spelling.
            if (parameters.Any(p => p.ParameterType == typeof(char) || p.ParameterType == typeof(char[])))
                return ClrMemberResolution.Inconclusive;

            var parameterTypes = new List<SemanticType>();
            foreach (var parameter in parameters)
            {
                if (!TryMapFaithfully(parameter.ParameterType, ClrDeclaredNullability.DeclaresNullableArgument(parameter), out var mapped))
                    return ClrMemberResolution.Inconclusive;
                parameterTypes.Add(mapped!);
            }

            var funcType = new FunctionType
            {
                ParameterTypes = parameterTypes,
                ReturnType = returnType!,
                // C# optional parameters are omittable at the call site, so the arity check the
                // call seam runs against this type must know they are (`sb.remove(0)` against
                // `Remove(int, int)` is a real error; a call omitting a defaulted tail is not).
                OptionalParameterCount = parameters.Count(p => p.IsOptional)
            };
            return new ClrMemberResolution.Method(funcType, method.Name);
        }

        // Multi-overload: return the group so the call seam can select by arity/types
        return new ClrMemberResolution.MethodGroup(methods);
    }
}

/// <summary>
/// Whether a member reference's receiver is a value of the CLR type or the type itself.
/// </summary>
internal enum ClrReceiverKind
{
    /// <summary>An instance receiver: <c>dt.year</c>, <c>sb.length</c>.</summary>
    Instance,

    /// <summary>A type receiver: <c>DateTime.max_value</c>, <c>Environment.processor_count</c>.</summary>
    StaticType
}

/// <summary>
/// Result of resolving a CLR member. Callers pattern-match on the concrete subtypes.
/// </summary>
internal abstract class ClrMemberResolution
{
    private ClrMemberResolution() { }

    internal static readonly ClrMemberResolution Absent = new AbsentResult();
    internal static readonly ClrMemberResolution Inconclusive = new InconclusiveResult();

    internal sealed class AbsentResult : ClrMemberResolution { }
    internal sealed class InconclusiveResult : ClrMemberResolution { }

    /// <summary>Single-overload method with resolved function type.</summary>
    internal sealed class Method : ClrMemberResolution
    {
        internal FunctionType Type { get; }
        internal string ClrName { get; }
        internal Method(FunctionType type, string clrName) { Type = type; ClrName = clrName; }
    }

    /// <summary>Multi-overload method group — the call seam selects the overload.</summary>
    internal sealed class MethodGroup : ClrMemberResolution
    {
        internal IReadOnlyList<MethodInfo> Candidates { get; }
        internal MethodGroup(IReadOnlyList<MethodInfo> candidates) { Candidates = candidates; }
    }

    /// <summary>Property with resolved semantic type.</summary>
    internal sealed class Property : ClrMemberResolution
    {
        internal SemanticType Type { get; }
        internal string ClrName { get; }
        internal Property(SemanticType type, string clrName) { Type = type; ClrName = clrName; }
    }

    /// <summary>Field with resolved semantic type.</summary>
    internal sealed class Field : ClrMemberResolution
    {
        internal SemanticType Type { get; }
        internal string ClrName { get; }
        internal Field(SemanticType type, string clrName) { Type = type; ClrName = clrName; }
    }
}
