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
    internal ClrMemberResolution Resolve(Type clrType, string memberName)
    {
        try
        {
            return ResolveCore(clrType, memberName);
        }
        catch (Exception ex) when (ex is ReflectionTypeLoadException or TypeLoadException
                                      or System.IO.FileNotFoundException or NotSupportedException)
        {
            return ClrMemberResolution.Inconclusive;
        }
    }

    private ClrMemberResolution ResolveCore(Type clrType, string memberName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

        // 1. Try methods — match Pythonic or verbatim CLR name
        var methods = clrType.GetMethods(flags)
            .Where(m => !m.IsSpecialName && !m.IsGenericMethodDefinition
                && (NameMangler.ToSharpyName(m.Name, ReverseNameContext.Method) == memberName
                    || m.Name == memberName))
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

            if (NameMangler.ToSharpyName(prop.Name, ReverseNameContext.Property) == memberName
                || prop.Name == memberName)
            {
                var propType = _bridge.MapClrTypeToSemanticType(prop.PropertyType);
                if (propType is UnknownType)
                    return ClrMemberResolution.Inconclusive;

                var clrPropertyName = prop.Name;
                return new ClrMemberResolution.Property(propType, clrPropertyName);
            }
        }

        // 3. Try fields — match Pythonic or verbatim CLR name
        foreach (var field in clrType.GetFields(flags))
        {
            if (NameMangler.ToSharpyName(field.Name, ReverseNameContext.Property) == memberName
                || field.Name == memberName)
            {
                var fieldType = _bridge.MapClrTypeToSemanticType(field.FieldType);
                if (fieldType is UnknownType)
                    return ClrMemberResolution.Inconclusive;

                return new ClrMemberResolution.Field(fieldType, field.Name);
            }
        }

        return ClrMemberResolution.Absent;
    }

    private ClrMemberResolution ResolveMethodGroup(List<MethodInfo> methods)
    {
        if (methods.Count == 1)
        {
            var method = methods[0];
            var returnType = _bridge.MapClrTypeToSemanticType(method.ReturnType);
            if (returnType is UnknownType)
                return ClrMemberResolution.Inconclusive;

            // A char-returning method is declined so the call seam handles
            // the char→str projection on the call node (#1291).
            if (returnType is BuiltinType bt && bt.ClrType == typeof(char))
                return ClrMemberResolution.Inconclusive;

            var parameterTypes = new List<SemanticType>();
            foreach (var parameter in method.GetParameters())
            {
                var mapped = _bridge.MapClrTypeToSemanticType(parameter.ParameterType);
                if (mapped is UnknownType)
                    return ClrMemberResolution.Inconclusive;
                parameterTypes.Add(mapped);
            }

            var funcType = new FunctionType
            {
                ParameterTypes = parameterTypes,
                ReturnType = returnType
            };
            return new ClrMemberResolution.Method(funcType, method.Name);
        }

        // Multi-overload: return the group so the call seam can select by arity/types
        return new ClrMemberResolution.MethodGroup(methods);
    }
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
