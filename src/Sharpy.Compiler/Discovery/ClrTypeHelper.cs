using System;
using System.Collections.Concurrent;
using System.Reflection;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Shared CLR type inspection helpers used by semantic analysis, code generation,
/// and validators.
/// </summary>
internal static class ClrTypeHelper
{
    private static readonly ConcurrentDictionary<Type, bool> _paramsIndexerCache = new();

    // Caches (CLR type, Sharpy member name) -> original CLR method name.
    private static readonly ConcurrentDictionary<(Type, string), string?> _clrMethodNameCache = new();

    // Caches (CLR type, Sharpy member name) -> original CLR property name.
    private static readonly ConcurrentDictionary<(Type, string), string?> _clrPropertyNameCache = new();

    /// <summary>
    /// Resolves the original CLR method name on <paramref name="clrType"/> whose reverse-mangled
    /// Sharpy form equals <paramref name="memberName"/> (e.g. <c>is_os_platform</c> ->
    /// <c>IsOSPlatform</c>), preserving acronym casing for directly-imported .NET types whose
    /// methods are not eagerly discovered (#705). Returns <c>null</c> when there is no unambiguous
    /// match. Lives here (not in the emitter) so code generation performs no reflection (#974); the
    /// TypeChecker materializes the result into <c>SemanticInfo</c> for the emitter to read.
    /// </summary>
    internal static string? ResolveClrMethodName(Type clrType, string memberName)
    {
        if (_clrMethodNameCache.TryGetValue((clrType, memberName), out var cached))
            return cached;

        string? resolved = null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        foreach (var method in clrType.GetMethods(flags))
        {
            // A CLR name already written verbatim (PascalCase) should be left untouched;
            // only match when the Sharpy (reverse-mangled) form equals the written name.
            if (NameMangler.ToSharpyName(method.Name, ReverseNameContext.Method) == memberName)
            {
                if (resolved != null && resolved != method.Name)
                {
                    // Ambiguous (multiple distinct CLR names map to this Sharpy name) — bail out.
                    resolved = null;
                    break;
                }
                resolved = method.Name;
            }
        }

        _clrMethodNameCache[(clrType, memberName)] = resolved;
        return resolved;
    }

    /// <summary>
    /// Resolves the original CLR property name on <paramref name="clrType"/> whose reverse-mangled
    /// Sharpy form equals <paramref name="memberName"/>, mirroring <see cref="ResolveClrMethodName"/>
    /// for the property/field emission path. This lets a verbatim (backtick-escaped) spy-stdlib
    /// property such as socket's lowercase <c>type</c> survive to code generation instead of being
    /// forward-mangled to <c>Type</c> (CS1061, #1093). Indexers (which are never reached by member
    /// name) are skipped. Returns <c>null</c> when there is no unambiguous match. Reflection lives
    /// here (Discovery), never in the emitter (#974); the TypeChecker materializes the result into
    /// <c>SemanticInfo</c> for the emitter to read.
    /// </summary>
    internal static string? ResolveClrPropertyName(Type clrType, string memberName)
    {
        if (_clrPropertyNameCache.TryGetValue((clrType, memberName), out var cached))
            return cached;

        string? resolved = null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        foreach (var prop in clrType.GetProperties(flags))
        {
            // Indexers carry index parameters and are never accessed by member name.
            if (prop.GetIndexParameters().Length > 0)
                continue;

            // A CLR name already written verbatim should be left untouched; only match when the
            // Sharpy (reverse-mangled) form equals the written name.
            if (NameMangler.ToSharpyName(prop.Name, ReverseNameContext.Property) == memberName)
            {
                if (resolved != null && resolved != prop.Name)
                {
                    // Ambiguous (multiple distinct CLR names map to this Sharpy name) — bail out.
                    resolved = null;
                    break;
                }
                resolved = prop.Name;
            }
        }

        _clrPropertyNameCache[(clrType, memberName)] = resolved;
        return resolved;
    }

    /// <summary>
    /// Returns true when the CLR type backing <paramref name="type"/> exposes an indexer whose last
    /// parameter is a C# <c>params</c> array (e.g. numpy's <c>NdArray</c>), so a tuple index
    /// <c>a[1, 2]</c> can be spread into separate element-access arguments (#956). Used by the
    /// TypeChecker to materialize <c>IndexAccessLowering.ParamsSpread</c>; the emitter never reflects.
    /// </summary>
    internal static bool HasParamsIndexer(SemanticType? type)
    {
        var clrType = type switch
        {
            UserDefinedType udt => udt.Symbol?.ClrType,
            BuiltinType bt => bt.ClrType,
            GenericType gt => TryConstructClosedGeneric(gt, t => t.ClrType ?? typeof(object)),
            _ => null
        };

        if (clrType == null)
            return false;

        if (_paramsIndexerCache.TryGetValue(clrType, out var cached))
            return cached;

        var result = false;
        foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var indexParams = prop.GetIndexParameters();
            if (indexParams.Length > 0 &&
                indexParams[indexParams.Length - 1].GetCustomAttribute<ParamArrayAttribute>() != null)
            {
                result = true;
                break;
            }
        }

        _paramsIndexerCache.TryAdd(clrType, result);
        return result;
    }

    internal static Type? TryConstructClosedGeneric(GenericType generic, Func<SemanticType, Type?> resolveClrType)
    {
        var openDef = generic.GenericDefinition?.ClrType;
        if (openDef == null || !openDef.IsGenericTypeDefinition)
            return openDef;

        var clrArgs = new Type[generic.TypeArguments.Count];
        for (int i = 0; i < generic.TypeArguments.Count; i++)
        {
            var arg = resolveClrType(generic.TypeArguments[i]);
            if (arg == null)
                return openDef;
            clrArgs[i] = arg;
        }

        try
        {
            return openDef.MakeGenericType(clrArgs);
        }
        catch (ArgumentException)
        {
            return openDef;
        }
    }

    /// <summary>
    /// Gets the element type if the given type is <c>Sharpy.Iterator&lt;T&gt;</c>
    /// or extends <c>Sharpy.Iterator&lt;T&gt;</c>. Returns <c>null</c> otherwise.
    /// </summary>
    public static Type? GetIteratorElementType(Type clrType)
    {
        var currentType = clrType;
        while (currentType != null)
        {
            if (currentType.IsGenericType &&
                currentType.GetGenericTypeDefinition().FullName == "Sharpy.Iterator`1")
            {
                return currentType.GetGenericArguments()[0];
            }
            currentType = currentType.BaseType;
        }
        return null;
    }
}
