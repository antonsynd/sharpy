namespace Sharpy.Compiler.Semantic;

/// <summary>
/// One row of a <see cref="LocalBindingLedger"/>: the symbol <see cref="SymbolTable.Define"/> bound,
/// the block scope it was bound in, its position within the owning ledger, and its position among
/// every binding of the file.
/// </summary>
/// <param name="Symbol">The bound symbol. Only <see cref="VariableSymbol"/> rows are named by the
/// allocator; type parameters and nested function symbols are recorded for completeness.</param>
/// <param name="ScopeId">The <see cref="Scope.Id"/> of the scope the symbol was defined in — the
/// owner itself or one of its block scopes. Read by tests and the LSP to reconstruct nesting.</param>
/// <param name="Ordinal">Zero-based position within the owning ledger.</param>
/// <param name="Sequence">Position among every binding recorded by the <see cref="SymbolTable"/>.
/// A nested owner's rows (a lambda's or a nested def's) carry sequences interleaved with the
/// enclosing owner's, so the <see cref="LocalNameAllocator"/> can fold them into the enclosing C#
/// method in source order (#1560 D1 §3).</param>
internal record LocalBinding(Symbol Symbol, int ScopeId, int Ordinal, int Sequence);

/// <summary>
/// The total record of every local binding of one function-like scope, in declaration order.
/// <see cref="Scope.Define"/> replaces a rebinding in place, so the scope holds only the LAST
/// binding of each name; the ledger — appended structurally from <see cref="SymbolTable.Define"/>,
/// so no per-site edit can skip it — is what the <see cref="LocalNameAllocator"/> walks (#1560).
/// </summary>
internal sealed class LocalBindingLedger
{
    private readonly List<LocalBinding> _entries = new();

    public LocalBindingLedger(int ownerScopeId, string ownerScopeName, int parentOwnerScopeId)
    {
        OwnerScopeId = ownerScopeId;
        OwnerScopeName = ownerScopeName;
        ParentOwnerScopeId = parentOwnerScopeId;
    }

    /// <summary>The <see cref="Scope.Id"/> of the function-like scope this ledger belongs to.</summary>
    public int OwnerScopeId { get; }

    /// <summary>
    /// The owner's scope name as passed to <see cref="SymbolTable.EnterScope"/> (<c>function:f</c>,
    /// <c>lambda</c>, <c>property:p:Set</c>, …). Classifies the owner for the allocator.
    /// </summary>
    public string OwnerScopeName { get; }

    /// <summary>
    /// The <see cref="OwnerScopeId"/> of the nearest enclosing function-like scope, or -1 when the
    /// owner is not nested inside another (a module-level function, a method, an accessor).
    /// </summary>
    public int ParentOwnerScopeId { get; }

    /// <summary>
    /// True when the owner's body is emitted INSIDE another C# method — a lambda or a nested def —
    /// so its locals share that method's declaration space and must be named against it.
    /// </summary>
    public bool IsNested => ParentOwnerScopeId >= 0;

    /// <summary>
    /// True when C#'s implicit <c>value</c> is live in the owner's body: a property set/init
    /// accessor, an event add/remove accessor, or a property observer (emitted inside the setter).
    /// A local spelled <c>value</c> in such a body collided with it (CS0136, #1560 R5), so the
    /// allocator pre-claims the spelling.
    /// </summary>
    public bool ReservesImplicitValue => SymbolTable.ScopeReservesImplicitValue(OwnerScopeName);

    public IReadOnlyList<LocalBinding> Entries => _entries;

    public void Append(Symbol symbol, int scopeId, int sequence)
    {
        _entries.Add(new LocalBinding(symbol, scopeId, _entries.Count, sequence));
    }
}
