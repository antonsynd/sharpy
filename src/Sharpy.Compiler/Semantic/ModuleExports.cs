using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The exports of a single module, owning BOTH the value-position lookup and the types-only
/// lookup that annotation-position resolution consults first (#1092).
/// </summary>
/// <remarks>
/// <para>
/// The two lookups used to be independent dictionaries on <see cref="ModuleSymbol"/> and
/// <see cref="ModuleInfo"/>, mirrored by convention at every construction, merge and
/// serialization site. That convention failed four times — the serializer round-trip (#1105),
/// the lazy CLR-namespace <c>new ModuleSymbol</c> sites (#1106), <c>MergeModuleExports</c>
/// (#1135), and one more checker site that appeared after #1106 shipped. Each fix populated the
/// site it knew about; nothing failed when a new parallel site was introduced (#1145, Class G).
/// </para>
/// <para>
/// This type ends that class: the mirror is a <b>derivation</b>, not a discipline. Every write
/// goes through <see cref="Add"/>, which files a <see cref="TypeSymbol"/> into the types view
/// automatically, and <see cref="MergeFrom"/> merges both views as one atomic operation. Only
/// the value view is mutable from outside; <see cref="Types"/> is read-only, so no caller can
/// put the two out of step.
/// </para>
/// <para>
/// Why two views at all: CPython's <c>sqlite3.Row</c> is both a type and the value of
/// <c>row_factory</c>. Sharpy splits it into a <c>Row</c> type and a <c>Row</c> field, which
/// collide in the value view (the field, written last, wins). Keeping types separately means
/// <c>ResolveQualifiedType</c> still resolves <c>sqlite3.Row</c> in annotation position while
/// value lookups see the field.
/// </para>
/// </remarks>
public sealed class ModuleExports : IReadOnlyDictionary<string, Symbol>
{
    private readonly Dictionary<string, Symbol> _symbols;
    private readonly Dictionary<string, TypeSymbol> _types;
    private readonly ReadOnlyDictionary<string, TypeSymbol> _typesView;

    public ModuleExports()
    {
        _symbols = new Dictionary<string, Symbol>();
        _types = new Dictionary<string, TypeSymbol>();
        _typesView = new ReadOnlyDictionary<string, TypeSymbol>(_types);
    }

    /// <summary>
    /// Copies both views of <paramref name="other"/>. The exported symbols themselves are shared
    /// (they are reference-identity records); only the lookups are copied. This is the single
    /// conversion point between the pre-symbol staging layer (<see cref="ModuleInfo"/>) and a
    /// <see cref="ModuleSymbol"/>, so a copy can never take one view without the other.
    /// </summary>
    public ModuleExports(ModuleExports other)
    {
        _symbols = new Dictionary<string, Symbol>(other._symbols);
        _types = new Dictionary<string, TypeSymbol>(other._types);
        _typesView = new ReadOnlyDictionary<string, TypeSymbol>(_types);
    }

    /// <summary>
    /// The types-only view, consulted by annotation-position resolution before the value view
    /// (<see cref="Shared.ModuleSymbolExtensions.TryGetExportedType"/>). Read-only: entries appear
    /// here only by exporting a <see cref="TypeSymbol"/> through <see cref="Add"/> or by merging
    /// another <see cref="ModuleExports"/>. The view is a genuine read-only wrapper, so casting it
    /// back to a writable dictionary throws rather than reaching the underlying map.
    /// </summary>
    public IReadOnlyDictionary<string, TypeSymbol> Types => _typesView;

    /// <summary>
    /// Exports <paramref name="symbol"/> under <paramref name="name"/>, replacing any previous
    /// export of that name (indexer-set semantics). A <see cref="TypeSymbol"/> is filed into
    /// <see cref="Types"/> as well; a value symbol leaves the types view alone, which is what
    /// keeps a same-named field from shadowing its type in annotation position.
    /// </summary>
    public void Add(string name, Symbol symbol)
    {
        _symbols[name] = symbol;

        if (symbol is TypeSymbol type)
            _types[name] = type;
    }

    /// <summary>Looks up a type in the types-only view.</summary>
    public bool TryGetType(string name, [MaybeNullWhen(false)] out TypeSymbol type)
        => _types.TryGetValue(name, out type);

    /// <summary>True if the types-only view has an entry for <paramref name="name"/>.</summary>
    public bool ContainsType(string name) => _types.ContainsKey(name);

    /// <summary>
    /// Merges <paramref name="source"/> into this instance, both views in one operation.
    /// </summary>
    /// <param name="source">The exports to merge in.</param>
    /// <param name="firstImportWins">
    /// When true (the import-merge rule — the same root module imported more than once keeps the
    /// first import's symbols, #1135), an existing entry is never replaced; nested modules present
    /// on both sides are merged recursively instead. When false, <paramref name="source"/>'s
    /// entries replace this instance's.
    /// </param>
    public void MergeFrom(ModuleExports source, bool firstImportWins)
    {
        foreach (var (name, symbol) in source._symbols)
        {
            if (_symbols.TryGetValue(name, out var existing))
            {
                // Two modules under the same name are the same namespace seen twice — merge them
                // rather than picking one, so both imports' members stay reachable.
                if (existing is ModuleSymbol existingModule && symbol is ModuleSymbol sourceModule)
                {
                    existingModule.Exports.MergeFrom(sourceModule.Exports, firstImportWins);
                    continue;
                }

                if (firstImportWins)
                    continue;
            }

            _symbols[name] = symbol;
        }

        // Non-recursive by design: a nested module's own types ride along on its ModuleSymbol
        // through the value branch above.
        foreach (var (name, type) in source._types)
        {
            if (firstImportWins && _types.ContainsKey(name))
                continue;

            _types[name] = type;
        }
    }

    /// <summary>
    /// Restores both views from a persisted symbol cache. Deserialization is the one caller that
    /// legitimately reproduces a value/type pairing it did not build (a type shadowed in the value
    /// view by a same-named field cannot be re-derived from the value view alone), so it is the
    /// only entry point that writes <see cref="Types"/> directly — and it writes both views at
    /// once. See <c>SymbolSerializer.ResolveReferences</c> (#1105).
    /// </summary>
    internal void RestoreFrom(
        IEnumerable<KeyValuePair<string, Symbol>> symbols,
        IEnumerable<KeyValuePair<string, TypeSymbol>> types)
    {
        foreach (var (name, symbol) in symbols)
            _symbols[name] = symbol;

        foreach (var (name, type) in types)
            _types[name] = type;
    }

    // --- Value view (IReadOnlyDictionary) ---------------------------------------------------

    public Symbol this[string name] => _symbols[name];

    public IEnumerable<string> Keys => _symbols.Keys;

    public IEnumerable<Symbol> Values => _symbols.Values;

    public int Count => _symbols.Count;

    public bool ContainsKey(string name) => _symbols.ContainsKey(name);

    public bool TryGetValue(string name, [MaybeNullWhen(false)] out Symbol symbol)
        => _symbols.TryGetValue(name, out symbol);

    public IEnumerator<KeyValuePair<string, Symbol>> GetEnumerator() => _symbols.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
