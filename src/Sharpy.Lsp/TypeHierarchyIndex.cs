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

    private TypeHierarchyIndex(Dictionary<TypeSymbol, List<TypeSymbol>> subtypes)
    {
        _subtypes = subtypes;
    }

    /// <summary>
    /// Builds a type hierarchy index from the given symbol table.
    /// Walks all type symbols and records parent-to-child mappings.
    /// </summary>
    public static TypeHierarchyIndex Build(SymbolTable symbolTable)
    {
        var subtypes = new Dictionary<TypeSymbol, List<TypeSymbol>>(
            ReferenceEqualityComparer.Instance);

        foreach (var type in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            // Skip CLR types (builtins from ModuleRegistry)
            if (type.ClrType != null)
                continue;

            if (type.BaseType != null)
            {
                GetOrCreate(subtypes, type.BaseType).Add(type);
            }

            foreach (var iface in type.Interfaces)
            {
                if (iface.Definition != null)
                {
                    GetOrCreate(subtypes, iface.Definition).Add(type);
                }
            }
        }

        return new TypeHierarchyIndex(subtypes);
    }

    /// <summary>
    /// Returns all types that directly extend or implement the given type.
    /// </summary>
    public IReadOnlyList<TypeSymbol> GetDirectSubtypes(TypeSymbol type)
    {
        return _subtypes.TryGetValue(type, out var list) ? list : Empty;
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
