using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp;

/// <summary>
/// Lightweight index mapping parent types to their direct subtypes.
/// Built from a <see cref="SymbolTable"/> by walking all type symbols.
/// Reused by type hierarchy handlers and go-to-implementation.
/// </summary>
internal sealed class TypeHierarchyIndex
{
    private static readonly IReadOnlyList<TypeSymbol> Empty = Array.Empty<TypeSymbol>();

    private readonly Dictionary<TypeSymbol, List<TypeSymbol>> _subtypes;
    private readonly Dictionary<string, TypeSymbol> _nameToSymbol;

    private TypeHierarchyIndex(
        Dictionary<TypeSymbol, List<TypeSymbol>> subtypes,
        Dictionary<string, TypeSymbol> nameToSymbol)
    {
        _subtypes = subtypes;
        _nameToSymbol = nameToSymbol;
    }

    /// <summary>
    /// Builds a type hierarchy index from the given symbol table.
    /// Walks all type symbols and records parent-to-child mappings.
    /// Symbol references from BaseType and InterfaceReference.Definition are
    /// canonicalized through a name-based lookup so that cross-file symbols
    /// (which may be different object instances) map to the same dictionary key.
    /// </summary>
    public static TypeHierarchyIndex Build(SymbolTable symbolTable)
    {
        var subtypes = new Dictionary<TypeSymbol, List<TypeSymbol>>(
            ReferenceEqualityComparer.Instance);

        // Build a name-to-canonical-symbol map from the current symbol table
        // so that BaseType / InterfaceReference.Definition references originating
        // from a different compilation context are resolved to the same instance.
        var nameToSymbol = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal);
        var allSymbols = symbolTable.GlobalScope.GetAllSymbols()
            .Concat(symbolTable.GetAllModuleScopeSymbols())
            .ToList();
        foreach (var sym in allSymbols.OfType<TypeSymbol>())
        {
            nameToSymbol.TryAdd(sym.Name, sym);
        }

        foreach (var type in allSymbols.OfType<TypeSymbol>())
        {
            // Skip CLR types (builtins from ModuleRegistry)
            if (type.ClrType != null)
                continue;

            if (type.BaseType != null)
            {
                var canonicalBase = Canonicalize(type.BaseType, nameToSymbol);
                GetOrCreate(subtypes, canonicalBase).Add(type);
            }

            foreach (var iface in type.Interfaces)
            {
                if (iface.Definition != null)
                {
                    var canonicalIface = Canonicalize(iface.Definition, nameToSymbol);
                    GetOrCreate(subtypes, canonicalIface).Add(type);
                }
            }
        }

        return new TypeHierarchyIndex(subtypes, nameToSymbol);
    }

    /// <summary>
    /// Returns all types that directly extend or implement the given type.
    /// Falls back to a name-based lookup when the caller passes a symbol from
    /// a different compilation context (different object identity).
    /// </summary>
    public IReadOnlyList<TypeSymbol> GetDirectSubtypes(TypeSymbol type)
    {
        if (_subtypes.TryGetValue(type, out var list))
            return list;

        // Name-based fallback for cross-compilation-context symbols.
        if (_nameToSymbol.TryGetValue(type.Name, out var canonical)
            && _subtypes.TryGetValue(canonical, out list))
        {
            return list;
        }

        return Empty;
    }

    /// <summary>
    /// Returns the direct supertypes (base class + interfaces) of the given type.
    /// </summary>
    public IReadOnlyList<TypeSymbol> GetDirectSupertypes(TypeSymbol type)
    {
        var result = new List<TypeSymbol>();

        if (type.BaseType != null)
            result.Add(type.BaseType);

        foreach (var iface in type.Interfaces)
        {
            if (iface.Definition != null)
                result.Add(iface.Definition);
        }

        return result;
    }

    private static TypeSymbol Canonicalize(
        TypeSymbol symbol,
        Dictionary<string, TypeSymbol> nameToSymbol)
    {
        return nameToSymbol.TryGetValue(symbol.Name, out var canonical) ? canonical : symbol;
    }

    private static List<TypeSymbol> GetOrCreate(
        Dictionary<TypeSymbol, List<TypeSymbol>> dict,
        TypeSymbol key)
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = new List<TypeSymbol>();
            dict[key] = list;
        }
        return list;
    }
}
