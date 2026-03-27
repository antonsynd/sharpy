using System;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Shared CLR type inspection helpers used by both <see cref="CodeGen.TypeSyntaxMapper"/>
/// and the protocol validator.
/// </summary>
internal static class ClrTypeHelper
{
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
