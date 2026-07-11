using System;
using System.Collections.Concurrent;
using System.Reflection;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Shared CLR type inspection helpers used by semantic analysis, code generation,
/// and validators.
/// </summary>
internal static class ClrTypeHelper
{
    private static readonly ConcurrentDictionary<Type, bool> _paramsIndexerCache = new();

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
