using System.Runtime.CompilerServices;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Centralizes type hierarchy traversal logic — walking base types, collecting interfaces,
/// searching for members up the inheritance chain, etc. All methods are static and stateless.
/// </summary>
internal static class TypeHierarchyService
{
    /// <summary>
    /// Returns the full base type chain for a type (excluding the type itself),
    /// walking from immediate parent to the root. Uses the binding for in-progress
    /// resolution when provided, falling back to materialized symbol properties.
    /// </summary>
    internal static IReadOnlyList<TypeSymbol> GetAllBaseTypes(
        TypeSymbol type, SemanticBinding? binding = null)
    {
        var result = new List<TypeSymbol>();
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        visited.Add(type);

        var current = ResolveBaseType(type, binding);
        while (current != null && visited.Add(current))
        {
            result.Add(current);
            current = ResolveBaseType(current, binding);
        }

        return result;
    }

    /// <summary>
    /// Returns the transitive closure of all interfaces implemented by a type,
    /// including interfaces inherited from base classes and interfaces that extend
    /// other interfaces. Uses BFS and <see cref="ReferenceEqualityComparer"/> for dedup.
    /// </summary>
    internal static IReadOnlySet<TypeSymbol> GetAllInterfaces(
        TypeSymbol type, SemanticBinding? binding = null)
    {
        var result = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<TypeSymbol>();

        // Seed: walk the full base chain (including the type itself) and enqueue direct interfaces
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        visited.Add(type);
        EnqueueDirectInterfaces(type, binding, queue);

        var current = ResolveBaseType(type, binding);
        while (current != null && visited.Add(current))
        {
            EnqueueDirectInterfaces(current, binding, queue);
            current = ResolveBaseType(current, binding);
        }

        // BFS through interface inheritance
        while (queue.Count > 0)
        {
            var iface = queue.Dequeue();
            if (!result.Add(iface))
                continue;

            EnqueueDirectInterfaces(iface, binding, queue);
        }

        return result;
    }

    /// <summary>
    /// Determines whether <paramref name="derived"/> inherits from or implements
    /// <paramref name="baseType"/> by walking the base class chain and interface set.
    /// </summary>
    internal static bool InheritsFrom(
        TypeSymbol? derived, TypeSymbol? baseType, SemanticBinding? binding = null)
    {
        if (derived == null || baseType == null)
            return false;

        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        visited.Add(derived);

        // Walk base class chain
        var current = ResolveBaseType(derived, binding);
        while (current != null && visited.Add(current))
        {
            if (IsSameType(current, baseType))
                return true;
            current = ResolveBaseType(current, binding);
        }

        // Check all interfaces transitively (base class interfaces + interface inheritance)
        foreach (var iface in GetAllInterfaces(derived, binding))
        {
            if (IsSameType(iface, baseType))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Generic member search up the type hierarchy. Searches the type itself first,
    /// then walks base classes, then interfaces. Returns the first match and its owning type.
    /// </summary>
    /// <typeparam name="T">The member type (must be a reference type).</typeparam>
    /// <param name="type">The type to start searching from.</param>
    /// <param name="name">The member name to search for.</param>
    /// <param name="memberSelector">Function that retrieves the relevant member collection from a TypeSymbol.</param>
    /// <param name="searchInterfaces">Whether to also search interfaces after base classes.</param>
    /// <param name="binding">Optional semantic binding for in-progress resolution.</param>
    internal static (T? member, TypeSymbol? owner) FindMember<T>(
        TypeSymbol type,
        string name,
        Func<TypeSymbol, IEnumerable<T>> memberSelector,
        bool searchInterfaces = false,
        SemanticBinding? binding = null) where T : class
    {
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);

        // Search the type itself and its base chain
        var current = type;
        while (current != null && visited.Add(current))
        {
            var member = FindByName(memberSelector(current), name);
            if (member != null)
                return (member, current);
            current = ResolveBaseType(current, binding);
        }

        // Search interfaces if requested
        if (searchInterfaces)
        {
            foreach (var iface in GetAllInterfaces(type, binding))
            {
                var member = FindByName(memberSelector(iface), name);
                if (member != null)
                    return (member, iface);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Finds a field by name in the type's base class hierarchy (fields are not on interfaces).
    /// </summary>
    internal static (VariableSymbol? field, TypeSymbol? owner) FindField(
        TypeSymbol type, string name, SemanticBinding? binding = null)
    {
        return FindMember<VariableSymbol>(type, name, t => t.Fields, searchInterfaces: false, binding);
    }

    /// <summary>
    /// Finds a property by name in the type's hierarchy, including interfaces.
    /// </summary>
    internal static (PropertySymbol? property, TypeSymbol? owner) FindProperty(
        TypeSymbol type, string name, SemanticBinding? binding = null)
    {
        return FindMember<PropertySymbol>(type, name, t => t.Properties, searchInterfaces: true, binding);
    }

    /// <summary>
    /// Finds a method by name in the type's hierarchy, including interfaces.
    /// </summary>
    internal static (FunctionSymbol? method, TypeSymbol? owner) FindMethod(
        TypeSymbol type, string name, SemanticBinding? binding = null)
    {
        return FindMember<FunctionSymbol>(type, name, t => t.Methods, searchInterfaces: true, binding);
    }

    /// <summary>
    /// Returns the ancestor chain for a <see cref="SemanticType"/>, from most specific to
    /// least specific. For <see cref="UserDefinedType"/> walks the symbol's base type chain.
    /// Always terminates with <see cref="SemanticType.Object"/> if not already present.
    /// </summary>
    internal static IReadOnlyList<SemanticType> GetAncestorChain(
        SemanticType type, SemanticBinding? binding = null)
    {
        var chain = new List<SemanticType> { type };

        if (type is UserDefinedType udt && udt.Symbol != null)
        {
            var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
            visited.Add(udt.Symbol);

            var current = ResolveBaseType(udt.Symbol, binding);
            while (current != null && visited.Add(current))
            {
                chain.Add(new UserDefinedType
                {
                    Name = current.Name,
                    Symbol = current
                });
                current = ResolveBaseType(current, binding);
            }
        }

        // Add object as ultimate base if not already there
        var lastTypeName = chain[^1].GetDisplayName().ToLowerInvariant();
        if (lastTypeName != "object" && lastTypeName != "system.object")
        {
            chain.Add(SemanticType.Object);
        }

        return chain;
    }

    /// <summary>
    /// Collects all methods from a type and its full base class chain, producing a merged
    /// view where derived methods override base methods of the same name.
    /// </summary>
    internal static Dictionary<string, FunctionSymbol> CollectAllMethods(
        TypeSymbol type, SemanticBinding? binding = null)
    {
        var result = new Dictionary<string, FunctionSymbol>();
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);

        var current = type;
        while (current != null && visited.Add(current))
        {
            foreach (var method in current.Methods)
            {
                // Only add if not already present (prefer most derived implementation)
                result.TryAdd(method.Name, method);
            }
            current = ResolveBaseType(current, binding);
        }

        return result;
    }

    // ─── Private helpers ──────────────────────────────────────────────

    /// <summary>
    /// Checks whether two TypeSymbol instances represent the same logical type.
    /// Uses reference equality as the fast path. For cross-module type identity,
    /// uses (DefiningModule, Name) or (DefiningFilePath, Name) when available.
    /// Does NOT fall back to name-only comparison to avoid false positives.
    /// </summary>
    internal static bool IsSameType(TypeSymbol a, TypeSymbol b)
    {
        if (ReferenceEquals(a, b))
            return true;

        // Cross-module: both have DefiningModule set
        if (a.DefiningModule != null && b.DefiningModule != null)
            return a.DefiningModule == b.DefiningModule && a.Name == b.Name;

        // Same-project cross-file: both have DefiningFilePath set
        if (a.DefiningFilePath != null && b.DefiningFilePath != null)
            return a.DefiningFilePath == b.DefiningFilePath && a.Name == b.Name;

        // Neither symbol has module/file context — this happens for CLR-discovered types
        // (e.g., Exception, List<T>) which are identified by name alone. User-defined types
        // always have DefiningFilePath set, so they never reach this path.
        if (a.DefiningModule == null && a.DefiningFilePath == null
            && b.DefiningModule == null && b.DefiningFilePath == null)
            return a.Name == b.Name;

        // Mixed context (one has module/file, the other doesn't) — conservative false
        return false;
    }

    /// <summary>
    /// Resolves the base type for a symbol, checking the binding first then the symbol property.
    /// </summary>
    private static TypeSymbol? ResolveBaseType(TypeSymbol symbol, SemanticBinding? binding)
        => binding?.GetBaseType(symbol) ?? symbol.BaseType;

    /// <summary>
    /// Returns the direct interfaces for a symbol (non-transitive), checking the binding first
    /// then the symbol property. Returns the TypeSymbol definitions (unwraps InterfaceReference).
    /// </summary>
    internal static IReadOnlyList<TypeSymbol> GetDirectInterfaces(TypeSymbol symbol, SemanticBinding? binding = null)
    {
        IReadOnlyList<InterfaceReference>? refs = null;
        if (binding != null)
            refs = binding.GetInterfaces(symbol);

        var interfaces = refs ?? (IReadOnlyList<InterfaceReference>)symbol.Interfaces;
        var result = new List<TypeSymbol>(interfaces.Count);
        foreach (var iface in interfaces)
        {
            result.Add(iface.Definition);
        }
        return result;
    }

    /// <summary>
    /// Enqueues direct interface TypeSymbols from a type into the BFS queue.
    /// Delegates to <see cref="GetDirectInterfaces"/> to avoid duplicate logic.
    /// </summary>
    private static void EnqueueDirectInterfaces(
        TypeSymbol symbol, SemanticBinding? binding, Queue<TypeSymbol> queue)
    {
        foreach (var iface in GetDirectInterfaces(symbol, binding))
        {
            queue.Enqueue(iface);
        }
    }

    /// <summary>
    /// Finds a named item in a sequence using the Name property via pattern matching.
    /// Works for VariableSymbol, FunctionSymbol, and PropertySymbol.
    /// </summary>
    private static T? FindByName<T>(IEnumerable<T> items, string name) where T : class
    {
        foreach (var item in items)
        {
            var itemName = item switch
            {
                VariableSymbol v => v.Name,
                FunctionSymbol f => f.Name,
                PropertySymbol p => p.Name,
                _ => null
            };
            if (itemName == name)
                return item;
        }
        return null;
    }
}
