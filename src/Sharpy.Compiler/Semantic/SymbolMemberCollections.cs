using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// A symbol's member list (a type's fields, methods, properties, interfaces, …): a list that refuses
/// every write once the symbol owning it is frozen (#2250).
/// </summary>
/// <remarks>
/// <para>
/// A master <see cref="Registry.BuiltinRegistry"/> is shared by every analysis on a
/// <c>CompilerApi</c>, and its symbols are frozen when its construction completes
/// (<see cref="Symbol.Freeze"/>). Freezing the settable properties alone left the member LISTS open:
/// a mixed auto/setter property in a <c>class str:</c> nested in a block added a backing field to the
/// master <c>str</c>, and every later document on that CompilerApi saw it. A frozen list throws
/// <see cref="Diagnostics.PhaseViolationException"/> from every mutator (<c>Add</c>, <c>Insert</c>,
/// the indexer setter, <c>Remove</c>, <c>RemoveAt</c>, <c>Clear</c>, <see cref="AddRange"/>).
/// </para>
/// <para>
/// A <c>with</c> copy of a symbol shares its lists (a record copy copies the reference), so a write
/// through a copy of a master symbol is a write to the master and is refused too.
/// </para>
/// <para>
/// A <see cref="List{T}"/> converts implicitly — the list is wrapped, not copied, so a declaration
/// site that builds a list and assigns it keeps one list, exactly as before.
/// </para>
/// </remarks>
public sealed class SymbolMemberList<T> : Collection<T>
{
    private string? _frozenOwner;

    public SymbolMemberList()
        : base(new List<T>())
    {
    }

    public SymbolMemberList(IEnumerable<T> items)
        : base(new List<T>(items))
    {
    }

    private SymbolMemberList(List<T> items)
        : base(items)
    {
    }

    /// <summary>Wraps <paramref name="items"/> (no copy) as a symbol member list.</summary>
    [return: NotNullIfNotNull(nameof(items))]
    public static implicit operator SymbolMemberList<T>?(List<T>? items)
        => items is null ? null : new SymbolMemberList<T>(items);

    /// <summary>True once the owning symbol is frozen: every mutator throws.</summary>
    internal bool IsFrozen => _frozenOwner is not null;

    /// <summary>Freezes this list as a member of the frozen symbol <paramref name="ownerName"/>.</summary>
    internal void Freeze(string ownerName) => _frozenOwner = ownerName;

    private List<T> Backing => (List<T>)Items;

    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
            Add(item);
    }

    public T? Find(Predicate<T> match) => Backing.Find(match);

    public int FindIndex(Predicate<T> match) => Backing.FindIndex(match);

    public bool Exists(Predicate<T> match) => Backing.Exists(match);

    protected override void InsertItem(int index, T item)
    {
        ThrowIfFrozen("add to");
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, T item)
    {
        ThrowIfFrozen("replace an element of");
        base.SetItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        ThrowIfFrozen("remove from");
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        ThrowIfFrozen("clear");
        base.ClearItems();
    }

    private void ThrowIfFrozen(string operation)
    {
        if (_frozenOwner is { } owner)
        {
            throw new Diagnostics.PhaseViolationException(
                $"{operation} a member list of a frozen symbol", "construction of the master BuiltinRegistry", owner);
        }
    }
}

/// <summary>
/// A symbol's name-keyed overload table (<see cref="TypeSymbol.OperatorMethods"/>,
/// <see cref="TypeSymbol.ProtocolMethods"/>, <see cref="TypeSymbol.MethodOverloads"/>): a dictionary of
/// <see cref="SymbolMemberList{T}"/> that refuses every write — to the table or to any overload list
/// in it — once the symbol owning it is frozen (#2250). See <see cref="SymbolMemberList{T}"/>.
/// </summary>
public sealed class SymbolOverloadTable : IReadOnlyDictionary<string, SymbolMemberList<FunctionSymbol>>
{
    private readonly Dictionary<string, SymbolMemberList<FunctionSymbol>> _table = new();
    private string? _frozenOwner;

    /// <summary>Wraps each overload list of <paramref name="table"/> (the lists are not copied).</summary>
    [return: NotNullIfNotNull(nameof(table))]
    public static implicit operator SymbolOverloadTable?(Dictionary<string, List<FunctionSymbol>>? table)
    {
        if (table is null)
            return null;

        var result = new SymbolOverloadTable();
        foreach (var (name, overloads) in table)
            result._table[name] = overloads;
        return result;
    }

    /// <summary>True once the owning symbol is frozen: every mutator throws.</summary>
    internal bool IsFrozen => _frozenOwner is not null;

    /// <summary>Freezes the table and every overload list in it.</summary>
    internal void Freeze(string ownerName)
    {
        _frozenOwner = ownerName;
        foreach (var overloads in _table.Values)
            overloads.Freeze(ownerName);
    }

    public SymbolMemberList<FunctionSymbol> this[string key]
    {
        get => _table[key];
        set
        {
            ThrowIfFrozen("set an entry of");
            _table[key] = value;
        }
    }

    public int Count => _table.Count;

    public IEnumerable<string> Keys => _table.Keys;

    public IEnumerable<SymbolMemberList<FunctionSymbol>> Values => _table.Values;

    public bool ContainsKey(string key) => _table.ContainsKey(key);

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out SymbolMemberList<FunctionSymbol> value)
        => _table.TryGetValue(key, out value);

    public bool TryAdd(string key, SymbolMemberList<FunctionSymbol> value)
    {
        ThrowIfFrozen("add an entry to");
        return _table.TryAdd(key, value);
    }

    public void Add(string key, SymbolMemberList<FunctionSymbol> value)
    {
        ThrowIfFrozen("add an entry to");
        _table.Add(key, value);
    }

    public bool Remove(string key)
    {
        ThrowIfFrozen("remove an entry from");
        return _table.Remove(key);
    }

    public IEnumerator<KeyValuePair<string, SymbolMemberList<FunctionSymbol>>> GetEnumerator() => _table.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void ThrowIfFrozen(string operation)
    {
        if (_frozenOwner is { } owner)
        {
            throw new Diagnostics.PhaseViolationException(
                $"{operation} an overload table of a frozen symbol", "construction of the master BuiltinRegistry", owner);
        }
    }
}
