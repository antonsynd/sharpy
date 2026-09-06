namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The outcome of one <see cref="Scope.ResolveName"/> walk.
/// </summary>
/// <param name="Bound">The symbol the name binds to, or null when nothing bound it.</param>
/// <param name="CrossedMember">
/// A class/struct-body variable the walk passed WITHOUT binding, because a function-like scope lay
/// between it and the name's use (#1786, R-Y). Non-null whether or not <paramref name="Bound"/> is:
/// a module variable shadowed by a class attribute of the same name binds the module variable and
/// still reports the crossing, which is what the emitter needs to qualify the access.
/// </param>
/// <param name="DeclaringScope">The scope that declared <paramref name="Bound"/>, or null.</param>
/// <param name="CrossedScope">
/// The class-like scope that declared <paramref name="CrossedMember"/>, or null. Its name carries
/// the owning type's spelling, which the <c>self.</c>/<c>C.</c> steers quote.
/// </param>
public readonly record struct NameResolution(
    Symbol? Bound,
    VariableSymbol? CrossedMember,
    Scope? DeclaringScope,
    Scope? CrossedScope)
{
    /// <summary>
    /// The spelling of the type whose body declared <see cref="CrossedMember"/>, or null when
    /// nothing was crossed. Taken from the scope name (<c>class:Counter</c> → <c>Counter</c>).
    /// </summary>
    public string? CrossedMemberOwner
        => CrossedScope is null
            ? null
            : CrossedScope.Name[(CrossedScope.Name.IndexOf(':', StringComparison.Ordinal) + 1)..];
}

/// <summary>
/// Manages symbol scopes during semantic analysis
/// </summary>
public class Scope
{
    private readonly Dictionary<string, Symbol> _symbols = new();
    private readonly Dictionary<string, List<FunctionSymbol>> _functionOverloads = new();
    private readonly Scope? _parent;
    public string Name { get; }
    public Scope? Parent => _parent;
    public int Id { get; }
    public int ParentId { get; }

    public Scope(string name, Scope? parent = null, int id = -1)
    {
        Name = name;
        _parent = parent;
        Id = id;
        ParentId = parent?.Id ?? -1;
    }

    public void Define(Symbol symbol)
    {
        if (_symbols.TryGetValue(symbol.Name, out var existingSymbol))
        {
            // Allow redefinition for non-const variables
            // This enables Python-like behavior where variables can be reassigned to different types
            if (existingSymbol is VariableSymbol existingVar && !existingVar.IsConstant &&
                symbol is VariableSymbol newVar && !newVar.IsConstant)
            {
                // Replace the existing symbol with the new one (redefinition)
                _symbols[symbol.Name] = symbol;
                return;
            }

            // Allow shadowing builtins (which have no source location)
            // This matches Python behavior where user code can shadow builtins like print, len, etc.
            bool isBuiltin = existingSymbol.DeclarationLine == null;
            if (isBuiltin)
            {
                _symbols[symbol.Name] = symbol;
                return;
            }

            // For all other cases (constants, functions, types, etc.), redefinition is an error
            throw new InvalidOperationException($"Symbol '{symbol.Name}' is already defined in this scope");
        }

        _symbols[symbol.Name] = symbol;
    }

    internal void DefineAs(string name, Symbol symbol)
    {
        _symbols[name] = symbol;
    }

    public Symbol? Lookup(string name, bool searchParent = true)
    {
        if (!searchParent)
            return _symbols.GetValueOrDefault(name);

        return ResolveName(name, fromFunctionLikeContext: false).Bound;
    }

    /// <summary>
    /// Resolves <paramref name="name"/> up the scope chain and reports both what it bound and what
    /// it had to walk past to get there.
    /// </summary>
    /// <param name="name">The bare name being resolved.</param>
    /// <param name="fromFunctionLikeContext">
    /// Whether a function-like boundary has already been passed before this scope. Callers start a
    /// walk with <c>false</c>; the walk sets it itself as it crosses
    /// <see cref="SymbolTable.IsFunctionLikeScope"/> scopes.
    /// </param>
    /// <remarks>
    /// <para>
    /// This is the ONE walk (#1786, R-Y). Python's class-scope rule — a class-body name is not
    /// visible by its bare name inside a method — is a property of the scope CHAIN, so it is
    /// decided here rather than by a mode the type checker switches on: <see cref="Lookup"/>,
    /// <c>SymbolTable.Lookup</c>, the <c>NameResolver</c> pre-pass and every LSP consumer all
    /// resolve through this method and inherit the rule, and a new function-like host (a property
    /// accessor, an event accessor, a lambda in a field initializer) is covered by having a scope
    /// on the chain, not by remembering to set a flag at a fifth call site. The predecessor was a
    /// <c>SymbolTable.SkipClassScopesInLookup</c> mode set at four checker sites, which left the
    /// property-accessor and field-initializer-lambda hosts ICEing.
    /// </para>
    /// <para>
    /// <b>Variable-only.</b> Once a function-like scope has been passed, a
    /// <see cref="VariableSymbol"/> found in a class-like scope is NOT bound — it is reported as
    /// <see cref="NameResolution.CrossedMember"/> and the walk continues outward, so a module-level
    /// name of the same spelling still binds. Types, type aliases, type parameters and functions
    /// bind as they always did: nested types and generic parameters scope over the whole class
    /// body, including its methods, and a bare method name is already an ordinary unresolved-name
    /// error.
    /// </para>
    /// </remarks>
    public NameResolution ResolveName(string name, bool fromFunctionLikeContext)
    {
        VariableSymbol? crossedMember = null;
        Scope? crossedScope = null;
        bool passedFunctionLike = fromFunctionLikeContext;

        for (Scope? scope = this; scope != null; scope = scope._parent)
        {
            if (scope._symbols.TryGetValue(name, out var hit))
            {
                if (passedFunctionLike
                    && SymbolTable.IsClassLikeScope(scope.Name)
                    && hit is VariableSymbol member)
                {
                    // Innermost crossing wins: an inner class's member is the one the reader meant.
                    crossedMember ??= member;
                    crossedScope ??= scope;
                }
                else
                {
                    return new NameResolution(hit, crossedMember, scope, crossedScope);
                }
            }

            if (SymbolTable.IsFunctionLikeScope(scope.Name))
                passedFunctionLike = true;
        }

        return new NameResolution(null, crossedMember, null, crossedScope);
    }

    public bool Contains(string name)
    {
        return _symbols.ContainsKey(name);
    }

    /// <summary>
    /// Replaces the binding that <paramref name="previous"/> occupies, in this scope or a parent
    /// scope, with <paramref name="updated"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No production caller since #1491.</b> The type checker used to replace a function's
    /// binding here when it wrote the checked return type; it now mutates the symbol in place
    /// (<c>TypeChecker.Definitions.UpdateFunctionSymbol</c>), because a replacement is a SECOND
    /// representation of one declaration and every other scope holding the pre-update reference —
    /// an importing module's, above all — went on reading the old one. The method and its guard
    /// (<c>ScopeUpdateShadowingTests</c>) stay because the hazard below belongs to any future
    /// attempt to reintroduce a replacement, and that is exactly when it would be rediscovered.
    /// </para>
    /// <para>
    /// The walk stops at the first scope holding the name, so it used to overwrite whatever
    /// binding that was. Return types are resolved with the function's OWN scope pushed and its
    /// parameters already registered, so <c>def month(month: int)</c> found the PARAMETER
    /// <c>month</c> and replaced it with the function symbol: every later read of <c>month</c> in
    /// the body typed as <c>(int) -&gt; None</c>, and a correct program was refused
    /// (SPY0220/SPY0222, #1393). The same overwrite reached one scope further out for a method —
    /// <c>class C: def month(self, month: int)</c> — where it could replace a module-level
    /// <c>month</c> with the method's symbol.
    /// </para>
    /// <para>
    /// So the replacement happens only where <paramref name="previous"/> ITSELF is bound —
    /// reference identity, which <see cref="Symbol"/> overrides record equality to provide. Matching
    /// on the name plus <see cref="Symbol.Kind"/> instead is not enough, and the difference is
    /// measurable: a method's symbol comes from <c>TypeSymbol.Methods</c> and is in no scope at all,
    /// so a kind match sends <c>class C: def month(...)</c> past its own parameter and into the
    /// module scope, where it replaces a module-level <c>def month</c> with the method — the same
    /// overwrite, one scope further out.
    /// </para>
    /// </remarks>
    public bool Update(Symbol previous, Symbol updated)
    {
        if (_symbols.TryGetValue(updated.Name, out var occupant)
            && ReferenceEquals(occupant, previous))
        {
            _symbols[updated.Name] = updated;
            return true;
        }

        if (_parent != null)
        {
            return _parent.Update(previous, updated);
        }

        return false;
    }

    public void DefineFunctionOverloads(string name, List<FunctionSymbol> overloads)
    {
        _functionOverloads[name] = overloads;
    }

    public List<FunctionSymbol>? LookupFunctionOverloads(string name, bool searchParent = true)
    {
        if (_functionOverloads.TryGetValue(name, out var overloads))
            return overloads;

        if (searchParent && _parent != null)
            return _parent.LookupFunctionOverloads(name, searchParent);

        return null;
    }

    public IEnumerable<Symbol> GetAllSymbols()
    {
        return _symbols.Values;
    }

    public IEnumerable<(string Name, List<FunctionSymbol> Overloads)> GetAllFunctionOverloads()
    {
        foreach (var (name, overloads) in _functionOverloads)
        {
            yield return (name, overloads);
        }
    }

    /// <summary>
    /// Removes a symbol from this scope or a parent scope.
    /// Used during incremental compilation to invalidate stale cached symbols.
    /// </summary>
    public bool Remove(string name)
    {
        if (_symbols.ContainsKey(name))
        {
            _symbols.Remove(name);
            return true;
        }

        if (_parent != null)
        {
            return _parent.Remove(name);
        }

        return false;
    }
}
