namespace Sharpy.Compiler.Semantic;

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

    public Scope(string name, Scope? parent = null)
    {
        Name = name;
        _parent = parent;
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

    public Symbol? Lookup(string name, bool searchParent = true)
    {
        if (_symbols.TryGetValue(name, out var symbol))
        {
            return symbol;
        }

        if (searchParent && _parent != null)
        {
            return _parent.Lookup(name, searchParent);
        }

        return null;
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
