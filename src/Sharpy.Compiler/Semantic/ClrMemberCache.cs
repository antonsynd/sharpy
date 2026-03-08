using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Caches CLR type metadata discovered via reflection.
/// NOT thread-safe. Cache is populated lazily per-type; not safe for concurrent access.
///
/// NOTE: Not designed for cross-compilation reuse.
/// </summary>
[NotThreadSafe(Reason = "Uses non-concurrent Dictionary caches; create per-compilation instance")]
internal class ClrMemberCache
{
    // Operator methods cache: Type -> (operator name like "op_Addition" -> MethodInfo list)
    private readonly Dictionary<Type, Dictionary<string, IReadOnlyList<MethodInfo>>> _operatorCache = new();

    // Interface cache: Type -> set of interface types
    private readonly Dictionary<Type, HashSet<Type>> _interfaceCache = new();

    // Indexer cache: Type -> (has indexer, element type)
    private readonly Dictionary<Type, (bool HasIndexer, Type? ElementType)> _indexerCache = new();

    // Enumerator element type cache: Type -> element type (if IEnumerable<T>)
    private readonly Dictionary<Type, Type?> _enumeratorCache = new();

    /// <summary>
    /// Gets operator methods for a CLR type, discovering and caching them if needed.
    /// </summary>
    /// <returns>Read-only dictionary mapping operator names (e.g., "op_Addition") to read-only lists of method overloads.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<MethodInfo>> GetOperatorMethods(Type clrType)
    {
        if (_operatorCache.TryGetValue(clrType, out var cached))
        {
            return cached;
        }

        var operators = DiscoverOperatorMethods(clrType);
        _operatorCache[clrType] = operators;
        return operators;
    }

    private Dictionary<string, IReadOnlyList<MethodInfo>> DiscoverOperatorMethods(Type clrType)
    {
        var result = new Dictionary<string, IReadOnlyList<MethodInfo>>();

        // Find all static operator methods
        var methods = clrType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name.StartsWith("op_"));

        // Group by method name
        var grouped = new Dictionary<string, List<MethodInfo>>();
        foreach (var method in methods)
        {
            if (!grouped.TryGetValue(method.Name, out var methodList))
            {
                methodList = new List<MethodInfo>();
                grouped[method.Name] = methodList;
            }
            methodList.Add(method);
        }

        // Convert to read-only lists
        foreach (var kvp in grouped)
        {
            result[kvp.Key] = kvp.Value.AsReadOnly();
        }

        return result;
    }

    /// <summary>
    /// Gets all interfaces implemented by a CLR type (including inherited).
    /// </summary>
    public IReadOnlySet<Type> GetImplementedInterfaces(Type clrType)
    {
        if (_interfaceCache.TryGetValue(clrType, out var cached))
        {
            return cached;
        }

        var interfaces = new HashSet<Type>(clrType.GetInterfaces());
        _interfaceCache[clrType] = interfaces;
        return interfaces;
    }

    /// <summary>
    /// Checks if a CLR type implements a specific interface (by generic definition).
    /// </summary>
    public bool ImplementsInterface(Type clrType, Type interfaceType)
    {
        var interfaces = GetImplementedInterfaces(clrType);

        if (interfaceType.IsGenericTypeDefinition)
        {
            return interfaces.Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);
        }

        return interfaces.Contains(interfaceType);
    }

    /// <summary>
    /// Checks if a CLR type has an indexer and returns the element type if so.
    /// </summary>
    public (bool HasIndexer, Type? ElementType) GetIndexerInfo(Type clrType)
    {
        if (_indexerCache.TryGetValue(clrType, out var cached))
        {
            return cached;
        }

        // Look for default property (indexer)
        var defaultMembers = clrType.GetDefaultMembers();
        var indexer = defaultMembers.OfType<PropertyInfo>()
            .FirstOrDefault(p => p.GetIndexParameters().Length > 0);

        var result = indexer != null
            ? (true, indexer.PropertyType)
            : (false, (Type?)null);

        _indexerCache[clrType] = result;
        return result;
    }

    /// <summary>
    /// Gets the element type for an IEnumerable<T> implementation, or null if not enumerable.
    /// </summary>
    public Type? GetEnumerableElementType(Type clrType)
    {
        if (_enumeratorCache.TryGetValue(clrType, out var cached))
        {
            return cached;
        }

        Type? elementType = null;

        // Check for IEnumerable<T>
        var interfaces = GetImplementedInterfaces(clrType);
        var enumerableInterface = interfaces
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface != null)
        {
            elementType = enumerableInterface.GetGenericArguments()[0];
        }
        // Check if type is itself IEnumerable<T>
        else if (clrType.IsGenericType &&
                 clrType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = clrType.GetGenericArguments()[0];
        }

        _enumeratorCache[clrType] = elementType;
        return elementType;
    }
}
