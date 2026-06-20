using System;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Shared CLR type inspection helpers used by semantic analysis, code generation,
/// and validators.
/// </summary>
internal static class ClrTypeHelper
{
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
