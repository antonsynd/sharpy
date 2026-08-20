using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Single authority for deciding which members resolve on builtin exception types.
/// Builtin exceptions present Python's exception surface — members inherited from
/// <c>System.*</c> ancestors that are not redeclared on the Sharpy class are refused
/// with a steer diagnostic (#1515).
/// </summary>
internal static class BuiltinExceptionSurface
{
    private static readonly ConcurrentDictionary<Type, bool> _typeCache = new();
    private static readonly ConcurrentDictionary<(Type, string), bool> _refusedCache = new();

    /// <summary>
    /// Returns <c>true</c> when <paramref name="type"/> is a builtin exception type:
    /// <c>System.Exception</c> itself, or a type in the <c>Sharpy.Core</c> assembly under
    /// the <c>Sharpy</c> namespace that derives from <c>System.Exception</c>.
    /// Imported CLR exception types (e.g. <c>System.IO.IOException</c>) are not matched —
    /// they keep the honest CLR surface per the interop ruling.
    /// </summary>
    internal static bool IsBuiltinExceptionType(Type type)
    {
        if (_typeCache.TryGetValue(type, out var cached))
            return cached;

        bool result = typeof(Exception).IsAssignableFrom(type)
            && (type == typeof(Exception)
                || (type.Assembly.GetName().Name == "Sharpy.Core"
                    && type.Namespace == "Sharpy"));

        _typeCache[type] = result;
        return result;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="memberName"/> (snake_case or verbatim CLR
    /// spelling) maps to a public member declared on a <c>System.*</c> ancestor of
    /// <paramref name="clrType"/> and not redeclared/overridden on the Sharpy class itself.
    /// Returns <c>false</c> when the type is not a builtin exception, when the member is
    /// declared on the Sharpy type, or when no matching member exists (the latter falls
    /// through to the existing "has no member" path).
    /// </summary>
    internal static bool IsRefusedMember(Type clrType, string memberName)
    {
        if (_refusedCache.TryGetValue((clrType, memberName), out var cached))
            return cached;

        bool refused = IsBuiltinExceptionType(clrType) && IsRefusedMemberCore(clrType, memberName);
        _refusedCache[(clrType, memberName)] = refused;
        return refused;
    }

    /// <summary>
    /// Returns a human-readable steer for the refusal diagnostic.
    /// </summary>
    internal static string RefusalSteer(string memberName)
    {
        if (memberName is "message" or "Message")
            return "use str(e) to get the exception message";

        return "builtin exceptions present Python's exception surface; " +
               "this .NET member is not part of it";
    }

    private static bool IsRefusedMemberCore(Type clrType, string memberName)
    {
        // System.Exception itself is a System.* type — it has no Sharpy-declared surface,
        // so every member on it is refused.
        bool typeItselfIsSystem = IsSystemNamespace(clrType);

        // --- Properties ---
        var properties = clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (prop.GetIndexParameters().Length > 0)
                continue;

            var sharpyName = NameMangler.ToSharpyName(prop.Name, ReverseNameContext.Property);
            if (sharpyName != memberName && prop.Name != memberName)
                continue;

            if (typeItselfIsSystem)
                return true;

            // Match found. Check if the Sharpy class declares/overrides this property.
            var declared = clrType.GetProperty(prop.Name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (declared != null)
                return false;

            if (IsSystemNamespace(prop.DeclaringType))
                return true;

            return false;
        }

        // --- Methods ---
        if (typeItselfIsSystem)
        {
            // All methods on a System.* type are refused.
            var allMethods = clrType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in allMethods)
            {
                if (method.IsSpecialName)
                    continue;
                var sharpyName = NameMangler.ToSharpyName(method.Name, ReverseNameContext.Method);
                if (sharpyName == memberName || method.Name == memberName)
                    return true;
            }
            return false;
        }

        // Mirror the DeclaredOnly name-set logic from OverloadIndexBuilder.DiscoverTypeMethods:
        // when the Sharpy type declares a method whose mangled name matches, the inherited
        // version is shadowed and therefore not refused.
        var declaredSharpyNames = new HashSet<string>(
            clrType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Select(m => NameMangler.ToSharpyName(m.Name, ReverseNameContext.Method)));

        var methods = clrType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        foreach (var method in methods)
        {
            if (method.IsSpecialName)
                continue;

            var sharpyName = NameMangler.ToSharpyName(method.Name, ReverseNameContext.Method);
            if (sharpyName != memberName && method.Name != memberName)
                continue;

            // Match found. If the Sharpy class declares a method with this mangled name,
            // the inherited one is shadowed — not refused.
            if (declaredSharpyNames.Contains(sharpyName))
                return false;

            if (IsSystemNamespace(method.DeclaringType))
                return true;

            return false;
        }

        return false;
    }

    private static bool IsSystemNamespace(Type? type)
    {
        return type?.Namespace?.StartsWith("System", StringComparison.Ordinal) == true;
    }
}
